using System;
using System.Text;
using FirstGame.Shared;
using PathOfDawn.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;

namespace PathOfDawn.Presentation
{
    /// <summary>Painted 2D presentation; the existing Gospel core owns all Scripture progression.</summary>
    public sealed partial class GospelLuminousApp : MonoBehaviour
    {
        public TMP_FontAsset KoreanFont;
        public GospelJourney Journey { get; private set; }
        public GospelLuminousWorld World { get; private set; }
        public GospelLuminousController Player { get; private set; }
        public GospelLuminousUi Ui { get; private set; }
        public Camera SceneCamera { get; private set; }
        public string ActiveSaveSlot => activeSlot;
        public bool IsModal => Ui != null && Ui.IsModal;
        public bool InGame => inGame;
        public float Zoom => zoom;
        const string ActiveSlotKey = "GOSPEL.activeSlot";
        const float InteractionRadius = 2.25f;
        string activeSlot;
        bool inGame;
        float zoom = 4.4f, saveClock;
        Vector3 cameraVelocity;
        BgmPlayer music;
        readonly System.Collections.Generic.List<Transform> meetingRoots=new System.Collections.Generic.List<Transform>();
        readonly System.Collections.Generic.List<SpriteRenderer> meetingPeople=new System.Collections.Generic.List<SpriteRenderer>();
        readonly int[] meetingVariants={2,0,0,1,2,0,1,1,2,2};

        void Start()
        {
            if (EventSystem.current == null)
                new GameObject("Luminous input", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var input = EventSystem.current.GetComponent<InputSystemUIInputModule>();
            if (input == null) input = EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>();
            input.AssignDefaultActions();
            var cameraRoot = new GameObject("Luminous main camera", typeof(Camera), typeof(AudioListener));
            SceneCamera = cameraRoot.GetComponent<Camera>();
            cameraRoot.tag = "MainCamera";
            SceneCamera.orthographic = true;
            SceneCamera.orthographicSize = zoom;
            SceneCamera.nearClipPlane = .1f;
            SceneCamera.farClipPlane = 160;
            SceneCamera.backgroundColor = new Color(.025f, .085f, .12f);
            SceneCamera.allowHDR = true;
            cameraRoot.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            World = new GameObject("The painted courtyard").AddComponent<GospelLuminousWorld>();
            World.Build(transform, SceneCamera);
            var playerRoot = new GameObject("Jesus — articulated 2D protagonist");
            playerRoot.transform.SetParent(transform, false);
            Player = playerRoot.AddComponent<GospelLuminousController>();
            Player.Actor = GospelLuminousActor.Build(playerRoot.transform);
            BuildPeople();
            Player.SetPose(new Vector2(-18, World.HeightAt(-18) + .06f));
            Player.Enabled = false;
            Player.InteractRequested += Interact;
            activeSlot = PlayerPrefs.GetString(ActiveSlotKey, GospelJourney.SaveSlot);
            music = BgmPlayer.Attach(gameObject, "Audio/dawn-on-galilee", "GOSPEL.music", .5f);
            Ui = gameObject.AddComponent<GospelLuminousUi>();
            Ui.Build(KoreanFont != null ? KoreanFont : Resources.Load<TMP_FontAsset>("Fonts/Korean"));
            Ui.OnUserGesture = music.NotifyUserGesture;
            Ui.OnStart = StartNew;
            Ui.OnContinue = ContinueGame;
            Ui.OnResume = Resume;
            Ui.OnPause = Pause;
            Ui.OnSave = () => Save();
            Ui.OnInventory = ShowInventory;
            Ui.OnStatus = ShowStatus;
            Ui.OnJournal = ShowQuestLog;
            Ui.OnRead = ShowBible;
            Ui.OnOpeningReplay = () => ShowOpening(0, true);
            Ui.OnSettings = ShowSettings;
            Ui.OnExit = () => { if (inGame) Save(false); ShowTitle(); };
            Ui.OnLampToggle = ToggleLamp;
            Ui.OnMove = Player.SetTouchInput;
            Ui.OnRun = Player.SetRun;
            Ui.OnJump = Player.RequestJump;
            Ui.OnInteract = Interact;
            Ui.OnMusicVolume = music.SetVolume;
            Ui.OnMusicMute = music.SetMuted;
            SceneCamera.transform.position = new Vector3(-20.6f, World.HeightAt(-18) + 2.6f, -30);
            ShowTitle();
        }

        void Update()
        {
            if (Player == null || Ui == null) return;
            var keyboard = Keyboard.current;
            if ((Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
                || (keyboard != null && keyboard.anyKey.wasPressedThisFrame)) music.NotifyUserGesture();
            if (!inGame) return;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) { if (IsModal) Resume(); else Pause(); }
                else if (keyboard.iKey.wasPressedThisFrame) { if (IsModal) Resume(); else ShowStatus(); }
                else if (keyboard.bKey.wasPressedThisFrame) { if (IsModal) Resume(); else ShowInventory(); }
                else if (keyboard.mKey.wasPressedThisFrame) { if (IsModal) Resume(); else ShowBible(); }
                else if (keyboard.jKey.wasPressedThisFrame) { if (IsModal) Resume(); else ShowQuestLog(); }
                else if (keyboard.hKey.wasPressedThisFrame && !IsModal) Ui.ToggleScenicView();
            }
            if (Player.Enabled == IsModal) Player.Enabled = !IsModal;
            Player.Actor.SetLamp(Journey != null && Journey.LampEquipped);
            if (IsModal || Journey == null) return;
            if (Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > .1f) zoom = Mathf.Clamp(zoom - Mathf.Sign(scroll) * .35f, 3f, 6.4f);
            }
            string nearby = CanInteract ? Chronicle != null ? Chronicle.CurrentQuest.NpcName : ObjectiveName : "";
            Ui.UpdateHud(ObjectiveName, HudReference, HudVerse,
                FoundReadingCount, AvailableReadingCount, nearby, CanInteract);
            if (Chronicle != null && !Chronicle.IsComplete)
                Ui.SetChronicleNavigation(CanInteract ? (DawnPlatformInput.UseTouchControls ? "대화 · " : "E · ") + Chronicle.CurrentQuest.NpcName
                    : (Player.Position.x < ObjectiveX ? "→  " : "←  ") + Chronicle.CurrentQuest.NpcName + "에게 가기");
            saveClock += Time.deltaTime;
            if (saveClock >= 25) { Save(false); saveClock = 0; }
        }

        void LateUpdate()
        {
            if (SceneCamera == null || Player == null || World == null) return;
            float x = Player.Position.x + (inGame ? Player.Facing * 1.15f : -2.6f);
            float y = World.HeightAt(Player.Position.x) + (inGame ? 2.2f : 2.6f);
            var target = new Vector3(Mathf.Clamp(x, -29, 29), y, -30);
            SceneCamera.transform.position = Vector3.SmoothDamp(SceneCamera.transform.position, target, ref cameraVelocity, .38f);
            SceneCamera.orthographicSize = Mathf.Lerp(SceneCamera.orthographicSize, inGame ? zoom : 4.8f, 1 - Mathf.Exp(-4 * Time.deltaTime));
        }

        GospelReading VisualReading => Journey.StudyPending ? Journey.CurrentStudy.Reading : Journey.CurrentReading;
        public string ObjectiveName => Chronicle != null ? Chronicle.IsComplete ? "요단강의 여정 완료" : Chronicle.CurrentQuest.Title : Journey==null ? "성전의 뜰" : !Journey.OpeningFinished ? "여정의 시작" : Journey.StudyPending
            ? "기억의 길 · "+Journey.CurrentStudy.Reading.Title : Journey.Phase==GospelPhase.Prologue
            ? Journey.CurrentReading.Title : Journey.IsComplete ? "첫 여정 완료 · 나사렛으로" : Journey.CurrentObjective.Title;
        public float ObjectiveX
        {
            get
            {
                if (Chronicle != null) return Chronicle.CurrentQuest != null ? Chronicle.CurrentQuest.X : 34f;
                if(Journey!=null&&Journey.StudyPending)return Journey.CurrentStudy.X;
                if(Journey!=null&&Journey.Phase==GospelPhase.Prologue&&Journey.HasStudyJourney)
                    return new[]{-26f,-18f,-10f}[Journey.ProloguePage];
                switch (Journey?.CurrentObjective?.Id)
                {
                    case "teachers": return 5;
                    case "parents": return 16;
                    case "nazareth-gate": return 24;
                    default: return -10;
                }
            }
        }
        public bool CanInteract => Chronicle != null ? !Chronicle.IsComplete && !Chronicle.CurrentQuest.IsPrologue
            && Mathf.Abs(Player.Position.x - ObjectiveX) <= InteractionRadius : Journey!=null && Journey.OpeningFinished && !Journey.IsComplete &&
            ((!Journey.HasStudyJourney&&Journey.Phase==GospelPhase.Prologue) || Mathf.Abs(Player.Position.x-ObjectiveX)<=InteractionRadius);

        public void ShowTitle()
        {
            inGame = false;
            if (Player != null) Player.Enabled = false;
            Ui.ShowTitle(PersistentSaves.Exists(activeSlot));
        }
        public void StartNew()
        {
            activeSlot = PersistentSaves.Exists(GospelJourney.SaveSlot)
                ? GospelJourney.SaveSlot + "-" + DateTime.UtcNow.Ticks : GospelJourney.SaveSlot;
            PlayerPrefs.SetString(ActiveSlotKey, activeSlot);
            Journey = GospelJourney.NewChronicleJourney();
            Ui.FocusBiblePage(0);
            Journey.SetPresentationSpace(GospelJourney.LuminousSpace);
            Player.SetPose(new Vector2(-30, World.HeightAt(-30) + .06f));
            saveClock = 0;
            inGame = true;
            Save(false);
            ResumeChronicle(true);
        }
        public void ContinueGame()
        {
            if (!PersistentSaves.TryLoad(activeSlot, GospelJourney.GameId, GospelJourney.SchemaVersion,
                GospelJourney.ContentVersion, out var payload, out var error))
            { Ui.Toast("저장을 열 수 없습니다. " + error); return; }
            try
            {
                var saved = GospelJourney.FromSave(GospelSaveCodec.Decode(payload));
                // Legacy 3D coordinates are never treated as side-view coordinates or overwritten on read.
                float x = saved.PresentationSpace == GospelJourney.LuminousSpace ? saved.PlayerX : LegacyResumeX(saved);
                float y = saved.PresentationSpace == GospelJourney.LuminousSpace ? saved.PlayerY : World.HeightAt(x) + .06f;
                Journey = saved;
                Player.SetPose(new Vector2(Mathf.Clamp(x, -33, 33), y), saved.PlayerYaw > 90 ? -1 : 1);
                saveClock = 0;
                inGame = true;
                renderedChronicleChapter = Journey.Chronicle?.ChapterId;
                Resume();
                if (Chronicle != null) Ui.Toast("성경과 여정의 기록을 이어갑니다.");
                else if (!Journey.OpeningFinished) ShowOpening();
                else if(Journey.StudyReadingOpen) ShowStudyReading();
                else if (Journey.Phase == GospelPhase.Reading) ShowCurrentReading();
                else Ui.Toast("저장한 말씀과 여정에서 이어갑니다.");
            }
            catch (Exception exception) { Ui.Toast("저장 원본을 보존했습니다. " + exception.Message); }
        }
        static float LegacyResumeX(GospelJourney journey)
        {
            if (journey.Phase == GospelPhase.Prologue) return -18;
            return new[] { -11.5f, 3.5f, 14.5f, 22.5f, 24f }[Mathf.Clamp(journey.ObjectiveIndex, 0, 4)];
        }
        public bool Save(bool showNotice = true)
        {
            if (Journey == null || Player == null) return false;
            Journey.SetPresentationSpace(GospelJourney.LuminousSpace);
            Journey.SetPlayerPose(Player.Position.x, Player.Position.y, 0, Player.Facing < 0 ? 180 : 0);
            bool ok = PersistentSaves.TrySave(activeSlot, GospelJourney.GameId, GospelJourney.SchemaVersion,
                GospelJourney.ContentVersion, GospelSaveCodec.Encode(Journey.CaptureSave()), out var error);
            if (ok) PlayerPrefs.Save();
            if (showNotice || !ok) Ui.Toast(ok ? "현재 위치와 말씀 기록을 저장했습니다." : "저장하지 못했습니다. " + error);
            return ok;
        }
        public void Resume()
        {
            if (!inGame || Journey == null) { ShowTitle(); return; }
            if (Chronicle != null) { ResumeChronicle(); return; }
            World.SetChronicleChapter("temple");
            Player.Actor.SetAdult(false);
            Player.Actor.SetLamp(Journey.LampEquipped);
            if (chronicleNpcRoot != null) chronicleNpcRoot.gameObject.SetActive(false);
            foreach (var person in meetingRoots) person.gameObject.SetActive(true);
            if(!Journey.OpeningFinished){ShowOpening();return;}
            RefreshPeople();
            inGame = true;
            Ui.ShowHud(ObjectiveName, VisualReading.Reference, FirstLine(VisualReading.Text),
                Journey.CodexCount, Journey.CodexTotal, DawnPlatformInput.UseTouchControls);
            Player.Enabled = true;
            World.SetObjective(ObjectiveX, !Journey.IsComplete);
        }
        void Pause() { Player.Enabled = false; Ui.ShowPause(); }
        void ShowSettings() { Player.Enabled = false; Ui.ShowSettings(music.Volume, music.Muted); }
        void ShowInventory() { if (Journey == null) return; Player.Enabled = false; Ui.ShowInventory(Journey.LampEquipped); }
        void ShowStatus()
        {
            if (Journey == null) return;
            Player.Enabled = false;
            if (Chronicle != null)
            {
                Ui.ShowStatus("예수님\n" + Chronicle.AgeLabel + "\n\n현재 여정  " + ObjectiveName
                    + "\n현재 목표  " + ChronicleObjective + "\n발견한 말씀  " + FoundReadingCount + " / " + AvailableReadingCount
                    + "\n장착 도구  " + (Journey.LampEquipped ? "토기 등잔" : "없음")
                    + "\n\n탄생의 서막 → 성전 방문 → 요단강의 세례", "예수님 · " + Chronicle.AgeLabel);
                return;
            }
            Ui.ShowStatus("예수님\n열두 살 · 성전의 소년\n\n현재 여정  " + ObjectiveName + "\n읽은 말씀  "
                + Journey.CodexCount + " / " + Journey.CodexTotal + "\n장착 도구  "
                + (Journey.LampEquipped ? "토기 등잔" : "없음") + "\n\n현재 구간: 탄생의 말씀 / 누가복음 2장 성전 방문");
        }
        void ToggleLamp()
        {
            if (Journey == null) return;
            Journey.SetUtility(Journey.LampEquipped ? "" : GospelBelongings.LampId);
            Player.Actor.SetLamp(Journey.LampEquipped);
            Save(false);
            Ui.ShowInventory(Journey.LampEquipped);
        }
        void ShowJournal()
        {
            if(Journey==null)return;
            Player.Enabled=false;
            Ui.ShowCodex(Journey.HasStudyJourney ? GospelStudyCatalog.AllReadings : GospelCatalog.Readings, Journey.HasReading, Journey.CodexCount);
        }
        void ShowOpening(int page=-1,bool replay=false)
        {
            if(Journey==null)return;
            Player.Enabled=false;
            int index=page<0?Journey.OpeningPage:page;
            index=Mathf.Clamp(index,0,GospelStudyCatalog.Opening.Count-1);
            Ui.ShowOpening(GospelStudyCatalog.Opening[index],index,GospelStudyCatalog.Opening.Count,()=>{
                if(replay){if(index+1<GospelStudyCatalog.Opening.Count)ShowOpening(index+1,true);else Resume();return;}
                Journey.AdvanceOpening();Save(false);
                if(Journey.OpeningFinished)Resume();else ShowOpening();
            },()=>{if(!replay){Journey.FinishOpening();Save(false);}Resume();});
        }
        void ShowStudyReading()
        {
            if(!Journey.StudyReadingOpen)return;
            Player.Enabled=false;
            var encounter=Journey.CurrentStudy;
            bool childTurn=Journey.StudyDialogueIndex%2==1;
            string speaker=childTurn?"예수님":encounter.Portrait=="mary"?"마리아":encounter.Portrait=="joseph"?"요셉":"성전의 선생";
            Ui.ShowReading(encounter.Reading.Title,speaker,encounter.Turns[Journey.StudyDialogueIndex],
                encounter.Reading.Reference,encounter.Reading.Text,
                "만남 "+(Journey.StudyCount+1)+"/8 · 대화 "+(Journey.StudyDialogueIndex+1)+"/"+encounter.Turns.Count,
                ()=>{var title=encounter.Reading.Title;Journey.AdvanceStudyDialogue();Save(false);
                    if(Journey.StudyReadingOpen)ShowStudyReading();else{Resume();Ui.Toast("말씀 도감 획득 · "+title);}},
                childTurn?"jesus":encounter.Portrait,()=>Ui.ShowStudyQuestions(encounter,ShowStudyReading));
        }
        void OpenScripture()
        {
            if (Journey == null) return;
            if (Journey.Phase == GospelPhase.Prologue || Journey.Phase == GospelPhase.Reading) { ShowCurrentReading(); return; }
            var reading = Journey.CurrentReading;
            Player.Enabled = false;
            Ui.ShowReading(reading.Title, "말씀 기록", "복음서의 기록을 천천히 읽어보세요.", reading.Reference,
                reading.Text, "개역한글 · 성경 본문", Resume);
        }
        public void Interact()
        {
            if(!inGame||IsModal||Journey==null)return;
            if (Chronicle != null) { InteractChronicle(); return; }
            if(!CanInteract){Ui.Toast("빛이 머무는 만남으로 걸어가 대화를 시작하세요.");return;}
            if(Journey.StudyPending)
            {
                if(Journey.StudyReadingOpen||Journey.TryStudyEncounter(Journey.CurrentStudy.Reading.Id))ShowStudyReading();
                return;
            }
            if(Journey.Phase==GospelPhase.Prologue||Journey.Phase==GospelPhase.Reading){ShowCurrentReading();return;}
            if(Journey.TryInteract(Journey.CurrentObjective.Id,out var message))ShowCurrentReading();else Ui.Toast(message);
        }
        void ShowCurrentReading()
        {
            Player.Enabled = false;
            var reading = Journey.CurrentReading;
            var turn = Journey.CurrentDialogue;
            Ui.ShowReading(reading.Title, turn.Speaker, turn.Text, reading.Reference, reading.Text,
                Journey.ProgressText + (turn.IsVerbatim ? " · 성경 직접 인용" : " · 학습 안내"), () =>
                {
                    int before=Journey.CodexCount;
                    Journey.AdvanceDialogue();
                    Save(false);
                    if(Journey.Phase==GospelPhase.Reading || (Journey.Phase==GospelPhase.Prologue&&Journey.CodexCount==before))ShowCurrentReading();
                    else {Resume();if(Journey.CodexCount>before)Ui.Toast("말씀 도감 획득 · "+reading.Title);}
                },turn.PortraitKey);
        }
        static string FirstLine(string value)
        {
            int newline = value.IndexOf('\n');
            return newline < 0 ? value : value.Substring(0, newline);
        }
        void BuildPeople()
        {
            float[] xs = { -26f,-18f,-10f,-3f,5f,12f,16f,20f,24f,27f };
            int[] variants = { 2,0,0,1,2,0,1,1,2,2 };
            for (int i = 0; i < xs.Length; i++)
            {
                var root = new GameObject("A meeting in the courtyard " + i).transform;
                root.SetParent(transform, false);
                root.position = new Vector3(xs[i], World.HeightAt(xs[i]) + .01f, 0);
                var person = GospelLuminousActor.BuildNpc(root, variants[i]);
                if (person != null) person.sortingOrder = 10;
                meetingRoots.Add(root);meetingPeople.Add(person);
            }
        }
        void RefreshPeople()
        {
            // Each memory marker contains the speaker of that encounter. The same
            // place can be revisited at a later recorded time with a different speaker.
            for(int i=0;i<meetingRoots.Count;i++)
            {
                int desired=new[]{2,0,0,1,2,0,1,1,2,2}[i];
                if(Journey!=null&&Journey.Phase==GospelPhase.Prologue)
                {if(i==0)desired=1;if(i==1)desired=2;if(i==2)desired=1;}
                if(i==4&&Journey!=null&&!Journey.StudyPending&&Journey.Phase!=GospelPhase.Prologue)desired=0;
                if(meetingVariants[i]==desired)continue;
                if(meetingPeople[i]!=null)Destroy(meetingPeople[i].gameObject);
                meetingPeople[i]=GospelLuminousActor.BuildNpc(meetingRoots[i],desired);
                if(meetingPeople[i]!=null)meetingPeople[i].sortingOrder=10;
                meetingVariants[i]=desired;
            }
        }
        void OnDestroy()
        {
            if (Player != null) Player.InteractRequested -= Interact;
            if (johnSprite != null) Destroy(johnSprite);
        }
    }
}
