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
    public sealed class GospelLuminousApp : MonoBehaviour
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
            Ui.OnJournal = ShowJournal;
            Ui.OnRead = OpenScripture;
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
                else if (keyboard.mKey.wasPressedThisFrame) { if (IsModal) Resume(); else ShowJournal(); }
            }
            if (Player.Enabled == IsModal) Player.Enabled = !IsModal;
            Player.Actor.SetLamp(Journey != null && Journey.LampEquipped);
            if (IsModal || Journey == null) return;
            if (Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > .1f) zoom = Mathf.Clamp(zoom - Mathf.Sign(scroll) * .35f, 3f, 6.4f);
            }
            string nearby = CanInteract ? (Journey.Phase == GospelPhase.Prologue ? "탄생의 말씀 읽기" : ObjectiveName) : "";
            Ui.UpdateHud(ObjectiveName, Journey.CurrentReading.Reference, FirstLine(Journey.CurrentReading.Text),
                Journey.CollectedReadings.Count, GospelCatalog.Readings.Count, nearby, CanInteract);
            saveClock += Time.deltaTime;
            if (saveClock >= 25) { Save(false); saveClock = 0; }
        }

        void LateUpdate()
        {
            if (SceneCamera == null || Player == null || World == null) return;
            float x = inGame ? Player.Position.x + Player.Facing * 1.15f : -20.6f;
            float y = World.HeightAt(inGame ? Player.Position.x : -18) + (inGame ? 2.2f : 2.6f);
            var target = new Vector3(Mathf.Clamp(x, -29, 29), y, -30);
            SceneCamera.transform.position = Vector3.SmoothDamp(SceneCamera.transform.position, target, ref cameraVelocity, .38f);
            SceneCamera.orthographicSize = Mathf.Lerp(SceneCamera.orthographicSize, inGame ? zoom : 4.8f, 1 - Mathf.Exp(-4 * Time.deltaTime));
        }

        public string ObjectiveName => Journey == null ? "성전의 뜰" : Journey.Phase == GospelPhase.Prologue
            ? "성전의 뜰 · 빛나는 길을 따라" : Journey.IsComplete ? "첫 여정의 끝 · 나사렛으로" : Journey.CurrentObjective.Title;
        public float ObjectiveX
        {
            get
            {
                switch (Journey?.CurrentObjective?.Id)
                {
                    case "teachers": return 5;
                    case "parents": return 16;
                    case "nazareth-gate": return 24;
                    default: return -10;
                }
            }
        }
        public bool CanInteract => Journey != null && !Journey.IsComplete &&
            (Journey.Phase == GospelPhase.Prologue || Mathf.Abs(Player.Position.x - ObjectiveX) <= InteractionRadius);

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
            Journey = GospelJourney.New();
            Journey.SetPresentationSpace(GospelJourney.LuminousSpace);
            Player.SetPose(new Vector2(-18, World.HeightAt(-18) + .06f));
            inGame = true;
            Save(false);
            Resume();
        }
        public void ContinueGame()
        {
            if (!PersistentSaves.TryLoad(activeSlot, GospelJourney.GameId, GospelJourney.SchemaVersion,
                GospelJourney.ContentVersion, out var payload, out var error))
            { Ui.Toast("저장을 열 수 없습니다. " + error); return; }
            try
            {
                var saved = GospelJourney.FromSave(JsonUtility.FromJson<GospelSave>(payload));
                // Legacy 3D coordinates are never treated as side-view coordinates or overwritten on read.
                float x = saved.PresentationSpace == GospelJourney.LuminousSpace ? saved.PlayerX : LegacyResumeX(saved);
                float y = saved.PresentationSpace == GospelJourney.LuminousSpace ? saved.PlayerY : World.HeightAt(x) + .06f;
                Journey = saved;
                Player.SetPose(new Vector2(Mathf.Clamp(x, -33, 33), y), saved.PlayerYaw > 90 ? -1 : 1);
                inGame = true;
                Resume();
                if (Journey.Phase == GospelPhase.Reading) ShowCurrentReading();
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
                GospelJourney.ContentVersion, JsonUtility.ToJson(Journey.CaptureSave()), out var error);
            if (ok) PlayerPrefs.Save();
            if (showNotice || !ok) Ui.Toast(ok ? "현재 위치와 말씀 기록을 저장했습니다." : "저장하지 못했습니다. " + error);
            return ok;
        }
        public void Resume()
        {
            if (!inGame || Journey == null) { ShowTitle(); return; }
            inGame = true;
            Ui.ShowHud(ObjectiveName, Journey.CurrentReading.Reference, FirstLine(Journey.CurrentReading.Text),
                Journey.CollectedReadings.Count, GospelCatalog.Readings.Count, DawnPlatformInput.UseTouchControls);
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
            Ui.ShowStatus("예수님\n열두 살 · 성전의 소년\n\n현재 여정  " + ObjectiveName + "\n읽은 말씀  "
                + Journey.CollectedReadings.Count + " / " + GospelCatalog.Readings.Count + "\n장착 도구  "
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
            var body = new StringBuilder("말씀 기록\n\n");
            if (Journey == null || Journey.CollectedReadings.Count == 0) body.Append("아직 읽은 말씀 기록이 없습니다. ‘말씀’에서 탄생의 기록을 읽어보세요.");
            else foreach (string id in Journey.CollectedReadings)
            {
                var reading = GospelCatalog.GetReading(id);
                body.Append(reading.Title).Append(" · ").Append(reading.Reference).Append("\n").Append(reading.Text).Append("\n\n");
            }
            Player.Enabled = false;
            Ui.ShowJournal(body.ToString());
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
            if (!inGame || IsModal || Journey == null) return;
            if (Journey.Phase == GospelPhase.Prologue || Journey.Phase == GospelPhase.Reading)
            { ShowCurrentReading(); return; }
            if (!CanInteract) { Ui.Toast("빛이 머무는 길을 따라 다음 만남으로 걸어가세요."); return; }
            if (Journey.TryInteract(Journey.CurrentObjective.Id, out var message)) ShowCurrentReading();
            else Ui.Toast(message);
        }
        void ShowCurrentReading()
        {
            Player.Enabled = false;
            var reading = Journey.CurrentReading;
            var turn = Journey.CurrentDialogue;
            Ui.ShowReading(reading.Title, turn.Speaker, turn.Text, reading.Reference, reading.Text,
                Journey.ProgressText + (turn.IsVerbatim ? " · 성경 직접 인용" : " · 학습 안내"), () =>
                {
                    if (Journey.Phase == GospelPhase.Prologue) Journey.AdvanceReading();
                    else Journey.AdvanceDialogue();
                    Save(false);
                    if (Journey.Phase == GospelPhase.Prologue || Journey.Phase == GospelPhase.Reading) ShowCurrentReading();
                    else Resume();
                });
        }
        static string FirstLine(string value)
        {
            int newline = value.IndexOf('\n');
            return newline < 0 ? value : value.Substring(0, newline);
        }
        void BuildPeople()
        {
            float[] xs = { -10f, 5f, 16f, 23.5f };
            int[] variants = { 0, 0, 1, 2 };
            for (int i = 0; i < xs.Length; i++)
            {
                var root = new GameObject("A meeting in the courtyard " + i).transform;
                root.SetParent(transform, false);
                root.position = new Vector3(xs[i], World.HeightAt(xs[i]) + .01f, 0);
                var person = GospelLuminousActor.BuildNpc(root, variants[i]);
                if (person != null) person.sortingOrder = 10;
            }
        }
        void OnDestroy()
        {
            if (Player != null) Player.InteractRequested -= Interact;
        }
    }
}
