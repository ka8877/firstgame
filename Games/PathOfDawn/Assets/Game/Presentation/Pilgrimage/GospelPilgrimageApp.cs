using System;
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
        public GospelPilgrimageMotor Player { get; private set; }
        public GospelPilgrimageActor Actor { get; private set; }
        public GospelPilgrimageCamera CameraRig { get; private set; }
        public GospelPilgrimageUi Ui { get; private set; }
        public Camera SceneCamera { get; private set; }
        public string ActiveSaveSlot => activeSlot;
        public string MigratedSourceSlot { get; private set; } = "";
        public bool InGame { get; private set; }
        public bool IsModal => Ui != null && Ui.IsModal;
        public float Zoom => CameraRig != null ? CameraRig.Zoom : 7.2f;
        public Vector3 ObjectivePosition => Chronicle?.CurrentQuest != null
            ? World.PositionForQuest(Chronicle.CurrentQuest) : new Vector3(34, World.HeightAt(34, 7), 7);
        public string ObjectiveName => Chronicle == null ? "말씀의 여정" : Chronicle.IsComplete
            ? "요단강의 여정 완료" : Chronicle.CurrentQuest.Title;
        public bool CanInteract => InGame && Chronicle != null && !Chronicle.IsComplete &&
            !Chronicle.CurrentQuest.IsPrologue &&
            Vector2.Distance(new Vector2(Player.Position.x, Player.Position.z),
                new Vector2(ObjectivePosition.x, ObjectivePosition.z)) <= GospelChronicle.InteractionRadius;

        const string ActiveSlotKey = "GOSPEL.activeSlot";
        string activeSlot, renderedChapter, renderedNpcQuest;
        Transform npcRoot;
        BgmPlayer music;
        float saveClock;

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
            var root = new GameObject("Jesus — directional field traveler");
            root.transform.SetParent(transform, false);
            Player = root.AddComponent<GospelPilgrimageMotor>();
            Player.Initialize(SceneCamera);
            Player.SetBounds(World.Bounds);
            Player.SetPose(World.SpawnForChapter("temple"));
            Player.Enabled = false;
            Actor = GospelPilgrimageActor.Build(root.transform, SceneCamera);
            Actor.SetGroundSampler(World.HeightAt);
            CameraRig = SceneCamera.gameObject.AddComponent<GospelPilgrimageCamera>();
            CameraRig.Initialize(SceneCamera, root.transform);
            CameraRig.InputEnabled = false;
            Player.InteractRequested += Interact;
            activeSlot = PlayerPrefs.GetString(ActiveSlotKey, GospelJourney.SaveSlot);
            music = BgmPlayer.Attach(gameObject, "Audio/dawn-on-galilee", "GOSPEL.music", .5f);
            Ui = gameObject.AddComponent<GospelPilgrimageUi>();
            Ui.Build(KoreanFont != null ? KoreanFont : Resources.Load<TMP_FontAsset>("Fonts/Korean"));
            Ui.OnUserGesture = music.NotifyUserGesture;
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
            Ui.OnInteract = Interact;
            Ui.OnMusicVolume = music.SetVolume;
            Ui.OnMusicMute = music.SetMuted;
            ShowTitle();
        }

        void Update()
        {
            if (Ui == null || Player == null) return;
            var keys = Keyboard.current;
            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame ||
                keys != null && keys.anyKey.wasPressedThisFrame) music.NotifyUserGesture();
            Actor.SetMotion(Player.Velocity, Player.IsGrounded, Player.FacingYaw);
            if (!InGame || Journey == null) return;
            if (keys != null)
            {
                if (keys.escapeKey.wasPressedThisFrame) { if (IsModal) Resume(); else Pause(); }
                else if (keys.iKey.wasPressedThisFrame) { if (Ui.CurrentView == "status") Resume(); else ShowStatus(); }
                else if (keys.bKey.wasPressedThisFrame) { if (Ui.CurrentView == "inventory") Resume(); else ShowInventory(); }
                else if (keys.mKey.wasPressedThisFrame) { if (Ui.CurrentView == "bible") Resume(); else ShowBible(); }
                else if (keys.jKey.wasPressedThisFrame) { if (Ui.CurrentView == "journal") Resume(); else ShowQuestLog(); }
                else if (keys.hKey.wasPressedThisFrame && !IsModal) Ui.ToggleScenicView();
            }
            Player.Enabled = !IsModal;
            CameraRig.InputEnabled = !IsModal;
            if (IsModal) return;
            Ui.UpdateHud(Journey, Player.Position, ObjectivePosition, CanInteract);
            saveClock += Time.deltaTime;
            if (saveClock >= 25) { Save(false); saveClock = 0; }
        }

        public void ShowTitle()
        {
            InGame = false;
            Player.Enabled = false;
            CameraRig.InputEnabled = false;
            Ui.ShowTitle(PersistentSaves.Exists(activeSlot));
        }

        static string NewSlot() => GospelJourney.SaveSlot + "-pilgrimage-" + DateTime.UtcNow.Ticks;

        public void StartNew()
        {
            Journey = GospelJourney.NewChronicleJourney();
            Journey.SetPresentationSpace(GospelJourney.PilgrimageSpace);
            activeSlot = NewSlot();
            MigratedSourceSlot = "";
            renderedChapter = null;
            InGame = true;
            saveClock = 0;
            Ui.FocusBiblePage(0);
            ConfigureChapter("birth", false);
            Player.SetPose(World.SpawnForChapter("temple"));
            CameraRig.Snap();
            Save(false);
            Resume();
        }

        public void ContinueGame()
        {
            string sourceSlot = activeSlot;
            if (!PersistentSaves.TryLoad(sourceSlot, GospelJourney.GameId, GospelJourney.SchemaVersion,
                GospelJourney.ContentVersion, out var payload, out var error))
            { Ui.Toast("저장을 열 수 없습니다. " + error); return; }
            try
            {
                var load = GospelPilgrimageSave.Prepare(GospelSaveCodec.Decode(payload));
                Journey = load.Journey;
                MigratedSourceSlot = load.RequiresFork ? sourceSlot : "";
                activeSlot = load.RequiresFork ? NewSlot() : sourceSlot;
                InGame = true;
                saveClock = 0;
                ConfigureChapter(Chronicle.ChapterId, false);
                Player.SetBounds(World.Bounds);
                Player.SetPose(load.CanRestorePose
                    ? new Vector3(Journey.PlayerX, Journey.PlayerY, Journey.PlayerZ)
                    : World.SpawnForChapter(Chronicle.ChapterId), load.CanRestorePose ? Journey.PlayerYaw : 180);
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
                GospelJourney.ContentVersion, GospelSaveCodec.Encode(Journey.CaptureSave()), out var error);
            if (saved) { PlayerPrefs.SetString(ActiveSlotKey, activeSlot); PlayerPrefs.Save(); }
            if (showNotice || !saved) Ui.Toast(saved ? "현재 위치와 말씀 기록을 저장했습니다." : "저장하지 못했습니다. " + error);
            return saved;
        }

        void ConfigureChapter(string chapter, bool relocate)
        {
            World.SetChapter(chapter);
            Player.SetBounds(World.Bounds);
            Actor.SetAdult(Chronicle != null && Chronicle.IsAdult);
            Actor.SetLamp(Journey != null && Journey.LampEquipped);
            renderedChapter = chapter;
            if (relocate)
            {
                Player.SetPose(World.SpawnForChapter(chapter));
                CameraRig.Snap();
            }
            RefreshNpc();
        }

        public void Resume()
        {
            if (!InGame || Journey == null) { ShowTitle(); return; }
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
                        Chronicle.IsAdult ? "나사렛에서 성장하신 뒤, 성인 예수님으로 요단강에서 세례 요한을 만납니다."
                            : "이제 열두 살 예수님으로 가족과 함께 길을 걷고 성전의 만남을 따라갑니다.", Resume);
                    return;
                }
            }
            RefreshNpc();
            if (Chronicle.Stage == GospelQuestStage.Reading) { ShowReading(); return; }
            Ui.ShowHud(Journey, Actor.CurrentSprite);
            World.SetObjective(ObjectivePosition, !Chronicle.IsComplete);
            Player.Enabled = true;
            CameraRig.InputEnabled = true;
            Ui.UpdateHud(Journey, Player.Position, ObjectivePosition, CanInteract);
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
            if (Chronicle.Stage == GospelQuestStage.Talk && !Chronicle.TryTalk(quest.NpcId, out var response))
            { Ui.Toast(response); return; }
            Save(false);
            if (Chronicle.Stage == GospelQuestStage.Reading) ShowReading();
        }

        void ShowReading()
        {
            Player.Enabled = false;
            CameraRig.InputEnabled = false;
            Ui.ShowDialogue(Chronicle, () =>
            {
                int before = Chronicle.DiscoveredCount;
                var reading = Chronicle.CurrentReading;
                Chronicle.AdvanceDialogue();
                if (Chronicle.DiscoveredCount > before)
                {
                    var pages = Chronicle.BookPages;
                    for (int i = 0; i < pages.Count; i++)
                        if (pages[i].ReadingId == reading.Id) { Ui.FocusBiblePage(i); break; }
                    Ui.Toast("성경에 새 말씀이 기록되었습니다 · " + reading.Title);
                }
                Save(false);
                Resume();
            });
        }

        void RefreshNpc()
        {
            if (Chronicle == null || Chronicle.IsComplete || Chronicle.CurrentQuest.IsPrologue)
            { if (npcRoot != null) npcRoot.gameObject.SetActive(false); return; }
            var quest = Chronicle.CurrentQuest;
            if (renderedNpcQuest == quest.Id && npcRoot != null) { npcRoot.gameObject.SetActive(true); return; }
            if (npcRoot != null) { npcRoot.gameObject.SetActive(false); Destroy(npcRoot.gameObject); }
            npcRoot = new GameObject("Encounter · " + quest.NpcName).transform;
            npcRoot.SetParent(transform, false);
            npcRoot.position = World.PositionForQuest(quest);
            var person = GospelPilgrimageActor.BuildNpc(npcRoot, SceneCamera, quest.NpcId,
                quest.Portrait == "mary" ? 1.63f : 1.78f);
            person.SetGroundSampler(World.HeightAt);
            renderedNpcQuest = quest.Id;
        }

        public void ShowBible()
        { if (Journey == null) return; Player.Enabled = false; Ui.ShowBible(Chronicle.BookPages, Chronicle.DiscoveredCount); }
        public void ShowStatus()
        { if (Journey == null) return; Player.Enabled = false; Ui.ShowStatus(Journey, Actor.CurrentSprite, Player.MeasuredSpeed); }
        public void ShowInventory()
        { if (Journey == null) return; Player.Enabled = false; Ui.ShowInventory(Journey.LampEquipped); }
        public void ShowQuestLog()
        {
            if (Journey == null) return;
            Player.Enabled = false;
            var body = new StringBuilder().AppendLine(Chronicle.AgeLabel).AppendLine();
            if (Chronicle.IsComplete)
                body.AppendLine("요단강의 세례와 공생애의 시작까지 이번 이야기를 마쳤습니다.\n이후의 사역·십자가·부활은 다음 이야기입니다.");
            else body.AppendLine("현재 목표").AppendLine(Chronicle.CurrentQuest.Objective);
            body.AppendLine().AppendLine("지나온 만남");
            foreach (var entry in Chronicle.Journal)
                body.Append(entry.IsComplete ? "✓ " : "→ ").AppendLine(entry.Title);
            Ui.ShowJournal(body.ToString());
        }
        public void Pause() { if (!InGame) return; Player.Enabled = false; Ui.ShowPause(); }
        public void ShowSettings() { Player.Enabled = false; Ui.ShowSettings(music.Volume, music.Muted); }
        public void ToggleLamp()
        {
            if (Journey == null) return;
            Journey.SetUtility(Journey.LampEquipped ? "" : GospelBelongings.LampId);
            Actor.SetLamp(Journey.LampEquipped);
            Save(false);
            ShowInventory();
        }
        void OnDestroy() { if (Player != null) Player.InteractRequested -= Interact; }
    }
}
