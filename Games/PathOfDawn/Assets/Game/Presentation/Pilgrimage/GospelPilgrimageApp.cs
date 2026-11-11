using System;
using System.Collections.Generic;
using System.Text;
using FirstGame.Shared;
using PathOfDawn.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace PathOfDawn.Presentation
{
    /// <summary>The Scripture core owns the story; actual XZ travel owns proximity.</summary>
    public sealed class GospelPilgrimageApp : MonoBehaviour
    {
        public TMP_FontAsset KoreanFont;
        public GospelJourney Journey { get; private set; }
        public GospelChronicle Chronicle => Journey?.Chronicle;
        public GospelPilgrimageWorld World { get; private set; }
        public GospelPilgrimagePeople People { get; private set; }
        public GospelPilgrimageMotor Player { get; private set; }
        public GospelPilgrimageActor Actor { get; private set; }
        public GospelPilgrimageCamera CameraRig { get; private set; }
        public GospelPilgrimageUi Ui { get; private set; }
        public Camera SceneCamera { get; private set; }
        public string ActiveSaveSlot => activeSlot;
        public string MigratedSourceSlot { get; private set; } = "";
        public bool InGame { get; private set; }
        public bool IsModal => Ui != null && Ui.IsModal;
        public GospelReading LastAcquiredReading { get; private set; }
        public GospelNativityVoice Voice { get; private set; }
        public GospelNprPortraitCache PortraitCache { get; private set; }
        public GospelNprOpeningStage OpeningStage { get; private set; }
        public float Zoom => CameraRig != null ? CameraRig.Zoom : 7.2f;
        public Vector3 ObjectivePosition => People?.CurrentPerson != null ? People.CurrentPerson.Position
            : Chronicle?.CurrentQuest != null ? World.PositionForQuest(Chronicle.CurrentQuest)
            : World != null ? new Vector3(34, World.HeightAt(34, 7), 7) : Vector3.zero;
        public string ObjectiveName => Chronicle == null ? "말씀의 여정" : Chronicle.IsComplete
            ? "요단강의 여정 완료" : Chronicle.CurrentQuest.Title;
        public bool CanInteract => InGame && Chronicle != null && !Chronicle.IsComplete &&
            !Chronicle.CurrentQuest.IsPrologue &&
            (Chronicle.Stage != GospelQuestStage.Travel || Mathf.Abs(Player.Position.x - Chronicle.CurrentQuest.X) <= GospelChronicle.InteractionRadius) &&
            Vector2.Distance(new Vector2(Player.Position.x, Player.Position.z),
                new Vector2(ObjectivePosition.x, ObjectivePosition.z)) <= GospelChronicle.InteractionRadius;

        const string ActiveSlotKey = "GOSPEL.activeSlot";
        string activeSlot, renderedChapter;
        Transform npcRoot;
        GospelStorybookAudio music;
        float saveClock;
        readonly Dictionary<GospelNprCharacterView, float> portraitRequests = new Dictionary<GospelNprCharacterView, float>();
        readonly List<GospelNprCharacterView> retiredPortraits = new List<GospelNprCharacterView>();
        GospelNprCharacterView speakingView;

        void Start()
        {
            if (EventSystem.current == null)
                new GameObject("Pilgrimage input", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var input = EventSystem.current.GetComponent<InputSystemUIInputModule>();
            if (input == null) input = EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>();
            input.AssignDefaultActions();
            SceneCamera = new GameObject("Pilgrimage camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            SceneCamera.tag = "MainCamera";
            World = new GameObject("The free-travel field").AddComponent<GospelPilgrimageWorld>();
            World.Build(transform);
            World.SetChapter("temple");
            var root = new GameObject("Jesus · physical traveler and NPR model");
            root.transform.SetParent(transform, false);
            Player = root.AddComponent<GospelPilgrimageMotor>();
            Player.Initialize(SceneCamera);
            Player.SetBounds(World.Bounds);
            Player.SetPose(World.SpawnForChapter("temple"));
            Player.Enabled = false;
            Actor = GospelPilgrimageActor.Build(root.transform, SceneCamera);
            Actor.SetGroundSampler(World.HeightAt);
            People = new GameObject("The living pilgrim company").AddComponent<GospelPilgrimagePeople>();
            People.Initialize(transform, World, SceneCamera, Player);
            CameraRig = SceneCamera.gameObject.AddComponent<GospelPilgrimageCamera>();
            CameraRig.Initialize(SceneCamera, root.transform);
            CameraRig.InputEnabled = false;
            Player.InteractRequested += Interact;
            var openingObject = new GameObject("The NPR opening · presentation only");
            openingObject.transform.SetParent(transform, false);
            OpeningStage = openingObject.AddComponent<GospelNprOpeningStage>();
            OpeningStage.Initialize(SceneCamera, CameraRig, Actor.gameObject);
            activeSlot = PlayerPrefs.GetString(ActiveSlotKey, GospelJourney.SaveSlot);
            music = GospelStorybookAudio.Attach(gameObject, Player);
            Voice = GospelNativityVoice.Attach(gameObject, music);
            PortraitCache = gameObject.AddComponent<GospelNprPortraitCache>();
            Ui = gameObject.AddComponent<GospelPilgrimageUi>();
            Ui.Build(KoreanFont != null ? KoreanFont : Resources.Load<TMP_FontAsset>("Fonts/Korean"));
            Ui.SetCharacterPortraitProvider(CharacterPortrait);
            Ui.OnUserGesture = music.NotifyUserGesture;
            Ui.OnUiClick = () => music.Play("ui");
            Ui.OnStart = StartNew;
            Ui.OnContinue = ContinueGame;
            Ui.OnResume = Resume;
            Ui.OnPause = Pause;
            Ui.OnSave = () => Save();
            Ui.OnExit = () => { if (InGame) Save(false); ShowTitle(); };
            Ui.OnStatus = ShowStatus;
            Ui.OnInventory = ShowInventory;
            Ui.OnRead = ShowBible;
            Ui.OnJournal = ShowQuestLog;
            Ui.OnSettings = ShowSettings;
            Ui.OnLampToggle = ToggleLamp;
            Ui.OnMove = Player.SetTouchInput;
            Ui.OnRun = Player.SetRun;
            Ui.OnJump = Player.RequestJump;
            Ui.OnDash = Player.RequestDash;
            Ui.OnInteract = Interact;
            Ui.OnPageTurn = () => music.Play("page");
            Ui.OnMusicVolume = music.SetVolume;
            Ui.OnMusicMute = music.SetMuted;
            Ui.OnVoiceEnabled = Voice.SetVoiceEnabled;
            ShowTitle();
        }

        void Update()
        {
            if (Ui == null || Player == null) return;
            // Consume once even in menus, so a closed pad cannot release stale
            // drag movement when the field becomes interactive again.
            Vector2 cameraLook = Ui.ConsumeCameraLook();
            var keys = Keyboard.current;
            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame ||
                keys != null && keys.anyKey.wasPressedThisFrame) music.NotifyUserGesture();
            Actor.SetMotion(Player.Velocity, Player.IsGrounded, Player.FacingYaw);
            People?.Tick(InGame && Journey != null && (Ui.CurrentView == "journey" || Ui.CurrentView == "story"), Time.deltaTime);
            UpdatePortraitVisibility();
            UpdateSpeakingModel();
            if (!InGame || Journey == null)
            {
                CameraRig.InputEnabled = false;
                CameraRig.SetUiTouchCapture(false);
                return;
            }
            if (keys != null)
            {
                if (keys.escapeKey.wasPressedThisFrame) { if (IsModal) Resume(); else Pause(); }
                else if (keys.iKey.wasPressedThisFrame) { if (Ui.CurrentView == "status") Resume(); else ShowStatus(); }
                else if (keys.bKey.wasPressedThisFrame) { if (Ui.CurrentView == "inventory") Resume(); else ShowInventory(); }
                else if (keys.mKey.wasPressedThisFrame) { if (Ui.CurrentView == "bible") Resume(); else ShowBible(); }
                else if (keys.jKey.wasPressedThisFrame) { if (Ui.CurrentView == "quests") Resume(); else ShowQuestLog(); }
                else if (keys.hKey.wasPressedThisFrame && !IsModal) Ui.ToggleScenicView();
            }
            Player.Enabled = !IsModal;
            CameraRig.InputEnabled = !IsModal;
            CameraRig.SetUiTouchCapture(!IsModal && Ui.HasCameraTouchPad);
            if (IsModal) return;
            CameraRig.AddLookDelta(cameraLook);
            World.SetObjective(ObjectivePosition, !Chronicle.IsComplete);
            Ui.UpdateHud(Journey, Player.Position, ObjectivePosition, CanInteract, SceneCamera);
            saveClock += Time.deltaTime;
            if (saveClock >= 25) { Save(false); saveClock = 0; }
        }

        public void ShowTitle()
        {
            Voice?.Stop("title");
            SuspendFieldPresentation();
            ReleasePortraitPresentation();
            music.SetOpeningCinematic(false);
            InGame = false;
            OpeningStage?.Hide();
            Ui?.SetSpatialOpeningStageVisible(false);
            Player.Enabled = false;
            CameraRig.InputEnabled = false;
            music.SetChapter("birth");
            Ui.ShowTitle(PersistentSaves.Exists(activeSlot));
        }

        static string NewSlot() => GospelJourney.SaveSlot + "-storybook-" + DateTime.UtcNow.Ticks;

        public void StartNew()
        {
            Voice?.Stop("new-journey");
            SuspendFieldPresentation();
            Journey = GospelJourney.NewChronicleJourney();
            Journey.SetPresentationSpace(GospelJourney.PilgrimageSpace);
            activeSlot = NewSlot();
            MigratedSourceSlot = "";
            LastAcquiredReading = null;
            People.ResetJourney();
            renderedChapter = null;
            InGame = true;
            saveClock = 0;
            Ui.FocusBiblePage(0);
            ConfigureChapter("birth", false);
            Player.SetPose(World.SpawnForChapter("temple"));
            Actor.SnapToPhysicalPose();
            CameraRig.Snap();
            Save(false);
            Resume();
        }

        public void ContinueGame()
        {
            Voice?.Stop("load");
            SuspendFieldPresentation();
            string sourceSlot = activeSlot;
            if (!PersistentSaves.TryLoad(sourceSlot, GospelJourney.GameId, GospelJourney.SchemaVersion,
                GospelJourney.ContentVersion, out var payload, out var error))
            { Ui.Toast("저장을 열 수 없습니다. " + error); return; }
            try
            {
                var load = GospelPilgrimageSave.Prepare(GospelSaveCodec.Decode(payload));
                var peopleSave = GospelSaveCodec.DecodePeople(payload);
                Journey = load.Journey;
                People.ResetJourney();
                LastAcquiredReading = null;
                MigratedSourceSlot = load.RequiresFork ? sourceSlot : "";
                activeSlot = load.RequiresFork ? NewSlot() : sourceSlot;
                InGame = true;
                saveClock = 0;
                ConfigureChapter(Chronicle.ChapterId, false);
                Player.SetBounds(World.Bounds);
                Player.SetPose(load.CanRestorePose
                    ? new Vector3(Journey.PlayerX, Journey.PlayerY, Journey.PlayerZ)
                    : World.SpawnForChapter(Chronicle.ChapterId), load.CanRestorePose ? Journey.PlayerYaw : 180);
                Actor.SnapToPhysicalPose();
                if (load.CanRestorePose && !load.RequiresFork) People.Restore(peopleSave);
                CameraRig.Snap();
                Resume();
                if (load.RequiresFork)
                {
                    Save(false);
                    Ui.Toast("이전 기록을 보존하고 새 필드에서 여정을 이어갑니다.");
                }
                else Ui.Toast("성경과 여정의 기록을 이어갑니다.");
            }
            catch (Exception exception)
            {
                Debug.Log("FIRSTGAME_PEOPLE_RESTORE_REJECTED stage=load exception=" + exception.GetType().Name + " message=" + exception.Message);
                activeSlot = sourceSlot;
                ShowTitle();
                Ui.Toast("저장 원본을 보존했습니다. " + exception.Message);
            }
        }

        public bool Save(bool showNotice = true)
        {
            if (!InGame || Journey == null || Player == null) return false;
            Journey.SetPresentationSpace(GospelJourney.PilgrimageSpace);
            var p = Player.Position;
            Journey.SetPlayerPose(p.x, p.y, p.z, Player.FacingYaw);
            bool saved = PersistentSaves.TrySave(activeSlot, GospelJourney.GameId, GospelJourney.SchemaVersion,
                GospelJourney.ContentVersion, GospelSaveCodec.Encode(Journey.CaptureSave(), People?.Capture()), out var error);
            if (saved) { PlayerPrefs.SetString(ActiveSlotKey, activeSlot); PlayerPrefs.Save(); }
            if (showNotice || !saved) Ui.Toast(saved ? "현재 위치와 말씀 기록을 저장했습니다." : "저장하지 못했습니다. " + error);
            return saved;
        }

        void ConfigureChapter(string chapter, bool relocate)
        {
            World.SetChapter(chapter);
            music?.SetChapter(chapter == "baptism" ? "jordan" : chapter);
            Player.SetBounds(World.Bounds);
            Actor.SetAdult(Chronicle != null && Chronicle.IsAdult);
            Actor.SetLamp(Journey != null && Journey.LampEquipped);
            renderedChapter = chapter;
            if (relocate)
            {
                Player.SetPose(World.SpawnForChapter(chapter));
                Actor.SnapToPhysicalPose();
                CameraRig.Snap();
            }
            RefreshNpc();
        }

        public void Resume()
        {
            Voice?.Stop("resume");
            SuspendFieldPresentation();
            if (!InGame || Journey == null) { ShowTitle(); return; }
            music.SetOpeningCinematic(!Chronicle.IsComplete && Chronicle.CurrentQuest.IsPrologue);
            Player.Enabled = false;
            CameraRig.InputEnabled = false;
            if (renderedChapter != Chronicle.ChapterId)
            {
                ConfigureChapter(Chronicle.ChapterId, true);
                Save(false);
                if (Chronicle.ChapterId != "birth")
                {
                    Ui.ShowChapter(Chronicle.IsAdult ? "요단강 · 공생애의 문턱" : "성전의 소년 · 유월절의 길",
                        Chronicle.AgeLabel,
                        Chronicle.IsAdult ? "갈릴리에서 요단강으로."
                            : "열두 살, 예루살렘의 유월절.", Resume);
                    return;
                }
            }
            RefreshNpc();
            if (Chronicle.Stage == GospelQuestStage.Reading) { ShowStory(); return; }
            Ui.ShowHud(Journey, null);
            World.SetObjective(ObjectivePosition, !Chronicle.IsComplete);
            Player.Enabled = true;
            CameraRig.InputEnabled = true;
            Ui.UpdateHud(Journey, Player.Position, ObjectivePosition, CanInteract, SceneCamera);
        }

        public void Interact()
        {
            if (!InGame || IsModal || Chronicle == null) return;
            if (Chronicle.IsComplete) { ShowBible(); return; }
            if (!CanInteract) { Ui.Toast("다음 만남의 표시를 따라 가까이 다가가세요."); return; }
            var quest = Chronicle.CurrentQuest;
            if (Chronicle.Stage == GospelQuestStage.Travel &&
                !Chronicle.TryArrive(quest.LandmarkId, Player.Position.x, out var arrival))
            { Ui.Toast(arrival); return; }
            if (Chronicle.Stage == GospelQuestStage.Talk)
            {
                if (!Chronicle.TryTalk(quest.NpcId, out var response))
                { Ui.Toast(response); return; }
                // Only a newly accepted encounter turns the traveler. Book
                // returns and old Reading saves keep their recorded heading.
                Player.StopAndFace(ObjectivePosition);
            }
            World.SetObjective(ObjectivePosition, true);
            Save(false);
            if (Chronicle.Stage == GospelQuestStage.Reading) ShowStory();
        }

        void ShowStory()
        {
            SuspendFieldPresentation(false);
            CameraRig.SetConversationTarget(npcRoot);
            if (npcRoot != null)
            {
                var partner = npcRoot.GetComponentInChildren<GospelPilgrimageActor>();
                if (partner != null) partner.FaceConversationPartner(Player.Position);
            }
            Player.Enabled = false;
            CameraRig.InputEnabled = false;
            int eventIndex = Chronicle.QuestIndex;
            string beatKey = Chronicle.StoryVoiceKey;
            bool opening = Chronicle.CurrentQuest.IsPrologue;
            music.SetOpeningCinematic(opening);
            if (opening) OpeningStage.Show(Chronicle);
            else OpeningStage.Hide();
            Ui.SetSpatialOpeningStageVisible(opening && OpeningStage.IsVisible);
            var openedStory = Chronicle;
            Ui.ShowStory(Chronicle, () =>
            {
                // A replaced/double-clicked scene callback cannot skip the next
                // beat, award a later event or mutate a newly loaded story.
                if (!InGame || Ui.CurrentView != "story" || !ReferenceEquals(Chronicle, openedStory) || Chronicle.Stage != GospelQuestStage.Reading ||
                    Chronicle.QuestIndex != eventIndex || Chronicle.StoryVoiceKey != beatKey) return;
                Voice?.Stop("advance");
                StopSpeakingModel();
                if (!Chronicle.AdvanceStory(out var acquired)) return;
                music.Play("dialogue");
                LastAcquiredReading = acquired;
                if (acquired != null)
                {
                    // Completion owns the discovery. The reward and Bible are
                    // presentations of an already saved page, never read gates.
                    Save(false);
                    if (opening)
                    {
                        Resume();
                        Ui.NotifyScriptureAdded();
                        return;
                    }
                    var pages = Chronicle.BookPages;
                    for (int i = 0; i < pages.Count; i++)
                        if (pages[i].ReadingId == acquired.Id) { Ui.FocusBiblePage(i); break; }
                    music.Play("scripture");
                    Ui.ShowStoryReward(acquired, Chronicle.DiscoveredCount, Chronicle.TotalReadings, Resume);
                    return;
                }
                Save(false);
                Resume();
            });
            Voice?.PlayForTurn(beatKey, Chronicle.CurrentStoryBeat);
        }

        void RefreshNpc()
        {
            People.Synchronize(Chronicle);
            npcRoot = People.CurrentPerson != null ? People.CurrentPerson.RootTransform : null;
        }

        public void ShowBible()
        { if (Journey == null) return; Voice?.Stop("bible"); SuspendFieldPresentation(); music.Play("page"); Ui.ShowBible(Chronicle.BookPages, Chronicle.DiscoveredCount); }
        public void ShowStatus()
        {
            if (Journey == null) return;
            Voice?.Stop("status"); SuspendFieldPresentation();
            Ui.ShowStatus(Journey, null, Player.MeasuredSpeed);
        }
        public void ShowInventory()
        { if (Journey == null) return; Voice?.Stop("inventory"); SuspendFieldPresentation(); Ui.ShowInventory(Journey.LampEquipped); }
        public void ShowQuestLog()
        {
            if (Journey == null) return;
            Voice?.Stop("journal");
            SuspendFieldPresentation();
            var body = new StringBuilder().AppendLine(Chronicle.AgeLabel).AppendLine();
            if (Chronicle.IsComplete)
                body.AppendLine("요단강의 세례와 공생애의 시작까지 이번 이야기를 마쳤습니다.\n이후의 사역·십자가·부활은 다음 이야기입니다.");
            else body.AppendLine("현재 목표").AppendLine(Chronicle.CurrentQuest.Objective);
            body.AppendLine().AppendLine("지나온 만남");
            foreach (var entry in Chronicle.Journal)
                body.Append(entry.IsComplete ? "✓ " : "→ ").AppendLine(entry.Title);
            Ui.ShowJournal(body.ToString());
        }
        public void Pause() { if (!InGame) return; Voice?.Stop("pause"); SuspendFieldPresentation(); Ui.ShowPause(); }
        public void ShowSettings() { Voice?.Stop("settings"); SuspendFieldPresentation(); Ui.ShowSettings(music.Volume, music.Muted, Voice == null || Voice.VoiceEnabled); }

        Texture CharacterPortrait(string identity, bool useAdult)
        {
            var view = FindCharacterView(identity, useAdult);
            if (view == null || !view.IsReady) return null;
            portraitRequests[view] = Time.unscaledTime;
            return view.GetPortraitTexture();
        }

        GospelNprCharacterView FindCharacterView(string identity, bool useAdult)
        {
            if (string.IsNullOrWhiteSpace(identity)) return null;
            string key = identity.Trim().ToLowerInvariant();
            // No future-age model is presented as the infant of the opening.
            if (Chronicle != null && !Chronicle.IsComplete && Chronicle.CurrentQuest.IsPrologue
                && (key == "jesus" || key == "jesus-child" || key == "jesus-adult")) return null;
            var openingModel = OpeningStage != null ? OpeningStage.ModelFor(key) : null;
            if (openingModel != null) return openingModel;
            string model;
            switch (key)
            {
                case "jesus": model = useAdult ? "JesusAdult" : "JesusChild"; break;
                case "jesus-child": model = "JesusChild"; useAdult = false; break;
                case "jesus-adult": model = "JesusAdult"; useAdult = true; break;
                case "mary": model = "Mary"; break;
                case "joseph": model = "Joseph"; break;
                case "teacher":
                case "temple-guide":
                case "teacher-listening":
                case "teacher-reading":
                case "jordan-guide":
                case "john-baptist":
                case "jordan-witness":
                case "journey-narrator": model = "Teacher"; break;
                default: return null;
            }
            if ((key == "jesus" || key == "jesus-child" || key == "jesus-adult")
                && Actor != null && Actor.IsAdult == useAdult && Actor.HasSkinnedModel)
                return Actor.CharacterView;

            // The field person's actual instance takes precedence, retaining
            // its present clothes/tint. A portrait does not create a second NPC.
            if (People != null)
            {
                var current = People.CurrentPerson;
                bool currentMatches = current != null && (current.Id == key || key == "teacher"
                    && Chronicle != null && !Chronicle.IsComplete && Chronicle.CurrentQuest.Portrait == "teacher");
                if (currentMatches)
                {
                    var actual = ModelOnPerson(current);
                    if (actual != null) return actual;
                }
                foreach (var person in People.Markers)
                {
                    if (person.IsAmbient || person.Id != key) continue;
                    var actual = ModelOnPerson(person);
                    if (actual != null) return actual;
                }
            }
            // Birth scenes precede the field population. This caller-owned,
            // hidden visual cache contains a real required prefab, not a marker,
            // collider, quest actor or fabricated painted-portrait substitute.
            return PortraitCache != null ? PortraitCache.GetOrCreate(model) : null;
        }

        static GospelNprCharacterView ModelOnPerson(GospelPersonMarker person)
        {
            if (person?.RootTransform == null || !person.RootTransform.gameObject.activeInHierarchy) return null;
            var actor = person.RootTransform.GetComponentInChildren<GospelPilgrimageActor>();
            return actor != null && actor.HasSkinnedModel ? actor.CharacterView : null;
        }

        void UpdateSpeakingModel()
        {
            GospelNprCharacterView current = null;
            if (InGame && Ui.CurrentView == "story" && Chronicle != null && Voice != null
                && Voice.IsPlaying && Voice.CurrentKey == Chronicle.StoryVoiceKey && Voice.Source != null
                && !Voice.Source.mute && Voice.Source.volume > .0001f)
            {
                string identity = GospelPilgrimageUi.ModelSpeakerIdentity(Chronicle.CurrentStoryBeat);
                if (identity != null) current = FindCharacterView(identity, Chronicle.IsAdult);
            }
            if (speakingView != current)
            {
                if (speakingView != null) speakingView.SetSpeaking(false);
                speakingView = current;
            }
            if (speakingView != null) speakingView.SetSpeaking(true);
        }

        void StopSpeakingModel()
        {
            if (speakingView != null) speakingView.SetSpeaking(false);
            speakingView = null;
        }

        void UpdatePortraitVisibility()
        {
            bool canShow = InGame && (Ui.CurrentView == "journey" || Ui.CurrentView == "status"
                || Ui.CurrentView == "story" || Ui.CurrentView == "dialogue");
            retiredPortraits.Clear();
            foreach (var request in portraitRequests)
            {
                var view = request.Key;
                if (view == null) { retiredPortraits.Add(view); continue; }
                if (!canShow || Time.unscaledTime - request.Value > .65f)
                {
                    view.SetPortraitVisible(false);
                    retiredPortraits.Add(view);
                }
            }
            foreach (var view in retiredPortraits) portraitRequests.Remove(view);
            retiredPortraits.Clear();
        }

        void HidePortraits()
        {
            foreach (var view in portraitRequests.Keys)
                if (view != null) view.SetPortraitVisible(false);
            portraitRequests.Clear();
            Actor?.SetPortraitVisible(false);
            if (People != null)
                foreach (var actor in People.GetComponentsInChildren<GospelPilgrimageActor>(true))
                    actor.SetPortraitVisible(false);
            PortraitCache?.HideAll();
        }

        void SuspendFieldPresentation(bool clearConversation = true)
        {
            StopSpeakingModel(); HidePortraits();
            if (clearConversation)
            {
                // Keep the current opening behind a book/menu. Otherwise wide
                // screen gutters could expose the future-age exploration field.
                bool keepOpening = InGame && Chronicle != null && !Chronicle.IsComplete
                    && Chronicle.CurrentQuest.IsPrologue && OpeningStage != null && OpeningStage.IsVisible;
                if (!keepOpening) OpeningStage?.Hide();
                Ui?.SetSpatialOpeningStageVisible(keepOpening);
            }
            if (Player != null) Player.Enabled = false;
            if (CameraRig != null)
            {
                CameraRig.InputEnabled = false;
                CameraRig.SetUiTouchCapture(false);
                if (clearConversation) CameraRig.SetConversationTarget(null);
            }
        }

        void ReleasePortraitPresentation()
        {
            foreach (var view in portraitRequests.Keys)
                if (view != null) view.ReleasePortrait();
            portraitRequests.Clear();
            if (Actor != null) Actor.CharacterView?.ReleasePortrait();
            if (People != null)
                foreach (var actor in People.GetComponentsInChildren<GospelPilgrimageActor>(true))
                    actor.CharacterView?.ReleasePortrait();
            PortraitCache?.ReleaseAll();
        }

        public void ToggleLamp()
        {
            if (Journey == null) return;
            Journey.SetUtility(Journey.LampEquipped ? "" : GospelBelongings.LampId);
            Actor.SetLamp(Journey.LampEquipped);
            Save(false);
            ShowInventory();
        }
        void OnDestroy()
        {
            OpeningStage?.Hide();
            Voice?.Stop("destroy"); StopSpeakingModel(); ReleasePortraitPresentation();
            if (Player != null) Player.InteractRequested -= Interact;
        }
    }
}
