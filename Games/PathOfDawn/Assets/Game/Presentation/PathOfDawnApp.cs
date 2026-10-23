using System;
using System.Collections;
using System.Collections.Generic;
using FirstGame.Shared;
using Ink.UnityIntegration;
using PathOfDawn.Authoring;
using PathOfDawn.Core;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Playables;
using UnityEngine.Rendering.Universal;
using UnityEngine.Timeline;
using UnityEngine.UI;

namespace PathOfDawn.Presentation
{
    /// <summary>Entry point for the independently buildable Galilee slice. Runtime state lives in Core.</summary>
    public sealed class PathOfDawnApp : MonoBehaviour
    {
        public GameObject EnvironmentPrefab, JesusPrefab, TravelerPrefab, MiriamPrefab;
        public TMP_FontAsset KoreanFont;
        public StoryChapter Chapter;
        const string SaveSlot = "path-of-dawn", SaveGame = "path-of-dawn", PortableVersion = "portable-1";
        readonly Dictionary<string, Transform> actors = new Dictionary<string, Transform>();
        NarrativeSession session;
        NarrativeSaveSlot saves;
        DawnUi ui;
        RectTransform canvas, screen, hud, notificationBackdrop;
        GameObject touchControls;
        DawnPointerPad touchMove, touchLook;
        TextMeshProUGUI prompt, objective, notification;
        GameObject worldRoot, sharedBasket, shelterRug, connectionMarker;
        CharacterController controller;
        Transform pivot;
        Camera mainCamera;
        CinemachineCamera playCamera, dialogueCamera, introCamera;
        PlayableDirector director;
        AudioSource ambience, cue;
        BgmPlayer music;
        DialogueVoicePlayer voice;
        AudioSource musicSource;
        DawnIllustratedStage illustratedStage;
        GameObject explorationHints;
        InputActionMap input;
        InputAction move, mouseLook, stickLook, interact, menu, memories;
        bool inGame, modal, inIntro, busy;
        float yaw, pitch = 9, verticalSpeed, textScale = 1, volume = .65f, musicDuck = 1;
        Vector3 planarVelocity, cameraFollowVelocity;
        int lineIndex;
        int lastChoiceFrame = -1;
        List<string> activeLines;
        string compiledStory, message = "";
        string activeSaveSlot = SaveSlot;
        Action dialogueAdvance;

        void Awake()
        {
            textScale = Mathf.Clamp(PlayerPrefs.GetFloat("POD.textScale", 1), .9f, 1.3f);
            volume = Mathf.Clamp01(PlayerPrefs.GetFloat("POD.volume", .65f)); AudioListener.volume = volume;
            input = new InputActionMap("PathOfDawn");
            move = input.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");
            mouseLook = input.AddAction("MouseLook", InputActionType.Value, "<Pointer>/delta", expectedControlLayout: "Vector2");
            stickLook = input.AddAction("StickLook", InputActionType.Value, "<Gamepad>/rightStick", expectedControlLayout: "Vector2");
            interact = input.AddAction("Interact", InputActionType.Button, "<Keyboard>/e"); interact.AddBinding("<Gamepad>/buttonSouth");
            menu = input.AddAction("Menu", InputActionType.Button, "<Keyboard>/escape"); menu.AddBinding("<Gamepad>/start");
            memories = input.AddAction("Memories", InputActionType.Button, "<Keyboard>/m"); memories.AddBinding("<Gamepad>/select");
        }
        void OnEnable() => input?.Enable();
        void OnDisable() { input?.Disable(); voice?.Clear(); StopLocomotion(); }
        void OnDestroy()
        {
            input?.Dispose();
        }
        void Start()
        {
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem));
            var uiInput = EventSystem.current.GetComponent<InputSystemUIInputModule>();
            if (uiInput == null) uiInput = EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>();
            uiInput.AssignDefaultActions();
            var go = new GameObject("Path of Dawn UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = 5;
            var canvasRoot = go.GetComponent<RectTransform>(); go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            ui = new DawnUi { Font = KoreanFont != null ? KoreanFont : Resources.Load<TMP_FontAsset>("Fonts/Korean") ?? TMP_Settings.defaultFontAsset, TextScale = textScale };
            // A centered 16:9 frame remains completely visible on wide phones and small windows.
            canvas = ui.Box(canvasRoot, "Design frame", 0, 0, 1920, 1080);
            canvas.anchorMin = canvas.anchorMax = canvas.pivot = new Vector2(.5f, .5f); canvas.anchoredPosition = Vector2.zero;
            if (Chapter == null) Chapter = Resources.Load<StoryChapter>("Content/Galilee");
            var ink = Chapter != null && Chapter.story != null ? Chapter.story : Resources.Load<InkFile>("Story/galilee");
            if (ink == null || !ink.isCompiled)
            {
                ui.Fill(canvas, "Fatal", DawnUi.Ink); ui.Text(canvas, "이야기 파일을 불러올 수 없습니다.\nUnity에서 galilee.ink의 Ink 가져오기 오류를 확인해 주세요.", 180, 300, 1500, 350, 34); return;
            }
            if (Chapter != null && (Chapter.contentVersion != NarrativeSession.ContentVersion || Chapter.stableId != "galilee_dawn"))
                throw new InvalidOperationException("StoryChapter 메타데이터와 실행 콘텐츠 버전이 다릅니다.");
            compiledStory = ink.storyJson; activeSaveSlot = PlayerPrefs.GetString("POD.activeSlot", SaveSlot);
            CreateSaveSlot(); BuildWorld(); BuildCameras();
            illustratedStage = gameObject.AddComponent<DawnIllustratedStage>();
            illustratedStage.Initialize(canvas, actors);
            BuildAudio(); ShowTitle();
        }
        void CreateSaveSlot() => saves = new NarrativeSaveSlot(new SharedStorage(activeSaveSlot), new UnityCodec());
        void Update()
        {
            if (compiledStory == null) return;
            if (music != null && ((Pointer.current != null && Pointer.current.press.wasPressedThisFrame) ||
                (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
                (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame))) music.NotifyUserGesture();
            if (explorationHints != null) explorationHints.SetActive(inGame && !modal && !inIntro);
            if (touchControls != null) touchControls.SetActive(inGame && !modal && !inIntro && TouchInterface);
            if (inGame && !inIntro && menu.WasPressedThisFrame()) { if (modal) CloseModal(); else ShowSettings(); }
            if (inGame && !modal && !inIntro && memories.WasPressedThisFrame()) ShowMemories();
            if (modal && !inIntro && dialogueAdvance != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            { dialogueAdvance(); return; }
            if (!inGame || modal || inIntro || busy) { StopLocomotion(); return; }
            Vector2 look = stickLook.ReadValue<Vector2>() * (100 * Time.deltaTime);
            if (Mouse.current != null && Mouse.current.rightButton.isPressed) look += mouseLook.ReadValue<Vector2>() * .10f;
            if (touchLook != null && touchLook.isActiveAndEnabled) look += touchLook.ConsumeLook() * .10f;
            illustratedStage.SetLook(look);
            var movement = Vector2.ClampMagnitude(move.ReadValue<Vector2>() + (touchMove != null && touchMove.isActiveAndEnabled ? touchMove.Value : Vector2.zero), 1);
            Vector3 direction = new Vector3(movement.x, 0, movement.y);
            if (direction.sqrMagnitude > 1) direction.Normalize();
            var actor = actors["traveler"];
            float turnAllowance = 1;
            if (direction.sqrMagnitude > .0001f)
            {
                var facing = Quaternion.LookRotation(direction);
                actor.rotation = Quaternion.RotateTowards(actor.rotation, facing, 440 * Time.deltaTime);
                // Let the hips turn before a sharp reverse step instead of skating sideways.
                turnAllowance = Mathf.InverseLerp(-.15f, .85f, Vector3.Dot(actor.forward, direction.normalized));
            }
            var wantedVelocity = direction * (DawnLocomotion.WalkingSpeed * turnAllowance);
            planarVelocity = Vector3.MoveTowards(planarVelocity, wantedVelocity,
                (wantedVelocity.sqrMagnitude > planarVelocity.sqrMagnitude ? 7.5f : 11f) * Time.deltaTime);
            if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            verticalSpeed += Physics.gravity.y * Time.deltaTime;
            controller.Move((planarVelocity + Vector3.up * verticalSpeed) * Time.deltaTime);
            // Keep physical movement inside the visible painted floor in each scene.
            bool shoreFloor = session.World.pointId == "shore";
            var p = actor.position;
            p.x = Mathf.Clamp(p.x, shoreFloor ? -8.2f : -11, shoreFloor ? 4.2f : 2.3f);
            p.z = Mathf.Clamp(p.z, shoreFloor ? -8.7f : -9, 9);
            if (p.y < -2) p = new Vector3(0, .15f, -5);
            if (p != actor.position) { controller.enabled = false; actor.position = p; controller.enabled = true; }
            pivot.position = Vector3.SmoothDamp(pivot.position, actor.position + Vector3.up * 1.1f,
                ref cameraFollowVelocity, .10f, Mathf.Infinity, Time.deltaTime);
            UpdatePrompt(); if (interact.WasPressedThisFrame()) Interact();
        }
        void LateUpdate()
        {
            // BgmPlayer owns saved volume/fade. Duck only the current frame's output, never preferences.
            float target = voice != null && voice.IsPlaying ? .30f : 1;
            musicDuck = Mathf.MoveTowards(musicDuck, target, Time.unscaledDeltaTime / (target < musicDuck ? .12f : .42f));
            if (musicSource != null) musicSource.volume *= musicDuck;
        }
        void StopLocomotion()
        {
            planarVelocity = Vector3.zero;
        }
        void OnApplicationPause(bool paused) { if (paused && inGame && session != null) Save(); }
        void OnApplicationQuit() { if (inGame && session != null) Save(); }
        void ClearScreen()
        {
            voice?.Clear();
            dialogueAdvance = null;
            if (screen != null) { screen.gameObject.SetActive(false); Destroy(screen.gameObject); }
            screen = ui.Fill(canvas, "Screen");
            touchMove?.ResetInput(); touchLook?.ResetInput();
        }
        void ShowTitle()
        {
            CancelPresentation(); inGame = false; modal = true;
            if (hud != null) { hud.gameObject.SetActive(false); Destroy(hud.gameObject); }
            ClearScreen(); dialogueCamera.gameObject.SetActive(false); introCamera.gameObject.SetActive(false); playCamera.gameObject.SetActive(false);
            // Title artwork is its own composition. No stale Cinemachine blend or live actors behind it.
            worldRoot.SetActive(false); illustratedStage.SetVisible(false); mainCamera.cullingMask = 0; mainCamera.backgroundColor = DawnUi.Ink; mainCamera.clearFlags = CameraClearFlags.SolidColor;
            ui.Fill(screen, "Title background", DawnUi.Ink);
            ui.Art(screen, "Art/title-hero", 0, 0, 1920, 1080);
            var gradient = ui.Box(screen, "Title readability", 0, 0, 1380, 1080).gameObject.AddComponent<DawnGradient>(); gradient.raycastTarget = false; gradient.SetAllDirty();
            var lower = ui.Box(screen, "Title lower shade", 0, 860, 1920, 220).gameObject.AddComponent<DawnGradient>();
            lower.vertical = true; lower.left = new Color(.03f,.07f,.12f,.85f); lower.raycastTarget = false; lower.SetAllDirty();
            var panel = ui.Box(screen, "Title", 100, 80, 690, 960);
            ui.Rule(panel, 4, 35, 76, DawnUi.Gold);
            ui.Text(panel, "PATH OF DAWN", 102, 14, 520, 65, 25, DawnUi.Gold).characterSpacing = 5;
            ui.Text(panel, "새벽의 길", 0, 120, 680, 190, 84);
            ui.Text(panel, "갈릴리, 처음 건네는 손길", 6, 318, 650, 85, 30, DawnUi.Gold);
            ui.Text(panel, "낯선 시대에 닿은 당신.\n한 번의 선택이 누군가의 아침이 됩니다.", 6, 418, 660, 145, 27);
            ui.Primary(panel, "새 게임", 6, 588, 590, 100, NewGame);
            ui.Button(panel, "이어하기", 6, 708, 590, 96, ContinueGame, saves.Exists);
            ui.Button(panel, "설정 · 저장 가져오기", 6, 822, 590, 96, ShowSettings);
            ui.Text(screen, "첫 번째 이야기  /  갈릴리의 아침", 1190, 960, 640, 70, 27, DawnUi.Paper);
            if (!string.IsNullOrWhiteSpace(message))
            {
                var notice = ui.Card(screen, "Title notice", 850, 790, 970, 145);
                ui.Text(notice, message, 28, 18, 914, 110, 25, DawnUi.Gold);
            }
            ambience.Play();
        }
        void NewGame()
        {
            if (saves.Exists && !saves.WriteBlocked)
            {
                ClearScreen(); var panel = ui.Card(screen,"New Game",410,250,1100,560,DawnUi.Ink);
                ui.Text(panel,"새 여정은 별도 슬롯에 저장됩니다.",45,45,1010,80,36);
                ui.Text(panel,"기존 이어하기 데이터는 보존합니다. 새 여정을 시작하면 이 기기의 이어하기가 새 슬롯을 가리킵니다.",45,145,1010,130,27);
                ui.Button(panel,"새 여정 시작",45,330,480,80,() => { activeSaveSlot = SaveSlot + "-" + DateTime.UtcNow.Ticks; PlayerPrefs.SetString("POD.activeSlot",activeSaveSlot); PlayerPrefs.Save(); CreateSaveSlot(); StartNew(); });
                ui.Button(panel,"돌아가기",565,330,480,80,ShowTitle); return;
            }
            if (saves.WriteBlocked) { activeSaveSlot = SaveSlot + "-" + DateTime.UtcNow.Ticks; PlayerPrefs.SetString("POD.activeSlot",activeSaveSlot); PlayerPrefs.Save(); CreateSaveSlot(); }
            StartNew();
        }
        void StartNew()
        {
            session = new NarrativeSession(compiledStory); ApplyWorld(); yaw = 0; pitch = 9; pivot.rotation = Quaternion.Euler(pitch, yaw, 0); inGame = true; modal = false;
            ClearScreen(); BuildHud(); Save(); StartCoroutine(PlayIntro());
        }
        void ContinueGame()
        {
            var candidate = new NarrativeSession(compiledStory);
            if (!saves.LoadInto(candidate, out var error)) { message = "이어하기 실패 — 원본 보존\n" + error; ShowTitle(); return; }
            CancelPresentation(); session = candidate; ApplyWorld(); yaw = actors["traveler"].eulerAngles.y; pitch = 9; pivot.rotation = Quaternion.Euler(pitch, yaw, 0); inGame = true; modal = false;
            ClearScreen(); BuildHud(); SwitchToPlay();
            if (session.Lines.Count > 0 || session.Choices.Count > 0) ShowDialogue(session.Lines);
        }
        void BuildHud()
        {
            if (hud != null) { hud.gameObject.SetActive(false); Destroy(hud.gameObject); }
            hud = ui.Fill(canvas,"HUD"); hud.SetAsFirstSibling(); illustratedStage.EnsureBehindHud();
            BuildTouchControls();
            var top = ui.Card(hud,"Top",32,28,1856,128,DawnUi.Ink);
            ui.Box(top,"Chapter accent",13,35,3,58,DawnUi.Gold);
            objective = ui.Text(top,"",28,12,1020,112,27);
            ui.Button(top,"기억 / 회귀 [M]",1080,19,300,90,ShowMemories);
            ui.Button(top,"저장",1398,19,156,90,() => { if (Save()) Notify("여정을 저장했습니다."); });
            ui.Button(top,"설정 [Esc]",1572,19,260,90,ShowSettings);
            var hints = ui.Fill(hud, "Exploration hints"); explorationHints = hints.gameObject;
            var interaction = ui.Card(hints,"Interaction backdrop",380,916,1150,110,DawnUi.Ink);
            interaction.GetComponent<Image>().raycastTarget = false;
            prompt = ui.Text(interaction,"",28,16,1094,82,30); prompt.alignment = TextAlignmentOptions.MidlineLeft;
            notificationBackdrop = ui.Card(hud,"Notification backdrop",380,800,1150,100,DawnUi.Ink);
            notificationBackdrop.GetComponent<Image>().raycastTarget = false;
            notification = ui.Text(notificationBackdrop,"",28,12,1094,80,27,DawnUi.Gold);
            notificationBackdrop.gameObject.SetActive(false);
            if (TouchInterface)
            {
                var guideBackdrop = ui.Card(hints,"Touch guide backdrop",370,1030,1450,50,DawnUi.Ink);
                guideBackdrop.GetComponent<Image>().raycastTarget = false;
                ui.Text(hints,"왼쪽 패드로 걷기 · 오른쪽 빈 화면을 밀어 둘러보기",380,1012,1430,66,34).alignment = TextAlignmentOptions.BottomLeft;
            }
            else
            {
                var guideBackdrop = ui.Card(hints,"Desktop guide backdrop",370,1030,1450,50,DawnUi.Ink);
                guideBackdrop.GetComponent<Image>().raycastTarget = false;
                ui.Text(hints,"WASD / 방향키: 걷기   ·   마우스 오른쪽 드래그: 풍경 둘러보기   ·   E: 대화·조사",380,1038,1430,40,20);
            }
            RefreshObjective();
        }
        bool TouchInterface => DawnPlatformInput.UseTouchControls;
        void BuildTouchControls()
        {
            var controls = ui.Fill(hud, "Touch controls"); touchControls = controls.gameObject;
            var look = ui.Box(controls, "Drag to look", 1000, 180, 860, 590, new Color(0, 0, 0, .001f));
            touchLook = look.gameObject.AddComponent<DawnPointerPad>();
            var pad = ui.Card(controls, "Movement pad", 80, 770, 230, 230, new Color(.055f,.105f,.16f,.65f));
            ui.Round(pad, 115);
            pad.pivot = new Vector2(.5f,.5f); pad.anchoredPosition = new Vector2(195,-885);
            touchMove = pad.gameObject.AddComponent<DawnPointerPad>(); touchMove.joystick = true;
            var thumb = ui.Card(pad, "Movement thumb", 0, 0, 78, 78, new Color(.98f,.78f,.43f,.88f));
            thumb.anchorMin = thumb.anchorMax = thumb.pivot = new Vector2(.5f,.5f); thumb.anchoredPosition = Vector2.zero;
            thumb.GetComponent<Image>().raycastTarget = false; touchMove.thumb = thumb;
            ui.Text(controls,"이동",142,1000,160,74,36,DawnUi.Paper).alignment = TextAlignmentOptions.BottomLeft;
            ui.Primary(controls,"대화 · 조사",1570,875,270,120,() => { if(inGame && !modal && !inIntro && !busy) Interact(); });
            touchControls.SetActive(false);
        }
        void RefreshObjective()
        {
            if (objective == null || session == null) return;
            string goal = session.World.pointId == "shore" ? "예수님 곁으로 다가가 대화하세요." : session.World.pointId == "room" ? "미리암에게 다가가 이야기하세요." : "발견한 길을 돌아보고 다른 선택을 시도하세요.";
            objective.text = "갈릴리 · 소지품 " + session.World.inventory.Count + " · 미리암의 신뢰 " + session.Trust + "\n" + goal;
        }
        void UpdatePrompt()
        {
            if (prompt == null) return;
            var p = actors["traveler"].position;
            string key = TouchInterface ? "" : "[E] ";
            if (session.World.pointId == "shore" && Vector3.Distance(p,actors["jesus"].position)<3) prompt.text = key + "예수님과 미리암에게 말 걸기";
            else if (session.World.pointId == "room" && Vector3.Distance(p,actors["miriam"].position)<2.8f) prompt.text = key + "미리암과 바구니에 관해 이야기하기";
            else if (Vector3.Distance(p,new Vector3(-2,0,-2))<2) prompt.text = key + "호숫가 바구니 조사";
            else if (Vector3.Distance(p,new Vector3(-5,0,-2))<2.3f) prompt.text = key + "돌벽의 등불 자리 조사";
            else prompt.text = key + "멈춘 시계 살펴보기";
        }
        void Interact()
        {
            if (session == null) return; SyncPositions(); var p = actors["traveler"].position;
            if (session.World.pointId == "shore" && Vector3.Distance(p,actors["jesus"].position)<3)
            { session.BeginShoreChoice(); Save(); ShowDialogue(session.Lines); }
            else if (session.World.pointId == "room" && Vector3.Distance(p,actors["miriam"].position)<2.8f)
            { session.BeginRoomChoice(); Save(); ShowDialogue(session.Lines); }
            else
            {
                string id = Vector3.Distance(p,new Vector3(-2,0,-2))<2 ? "shore_basket" : Vector3.Distance(p,new Vector3(-5,0,-2))<2.3f ? "room_lamp" : "stopped_watch";
                ShowDialogue(new List<string> { session.Examine(id) }); Save();
            }
        }
        void ShowDialogue(IReadOnlyList<string> lines)
        {
            StopLocomotion(); activeLines = new List<string>(lines); lineIndex = 0; modal = true; SwitchToDialogue(); RenderDialogue();
        }
        void RenderDialogue()
        {
            ClearScreen();
            ui.Fill(screen, "Dialogue input shield", new Color(0,0,0,0));
            bool reading = lineIndex < activeLines.Count;
            var panel = ui.Card(screen,"Dialogue",reading ? 100 : 60,reading ? 660 : 570,reading ? 1720 : 1800,reading ? 380 : 470,new Color(.035f,.065f,.11f,.94f));
            if (lineIndex < activeLines.Count)
            {
                string fullLine = activeLines[lineIndex], line = fullLine, speaker = "여행자의 기록", portrait = "traveler";
                Color accent = DawnUi.Gold;
                if (line.StartsWith("미리암: ", StringComparison.Ordinal)) { speaker = "미리암"; portrait = "miriam"; accent = DawnUi.Teal; line = line.Substring(5); }
                else if (line.StartsWith("예수님: ", StringComparison.Ordinal)) { speaker = "예수님"; portrait = "jesus"; accent = new Color(1,.68f,.46f); line = line.Substring(5); }
                else if (line.StartsWith("당신: ", StringComparison.Ordinal)) { speaker = "여행자"; line = line.Substring(4); }
                illustratedStage.SetSpeaker(portrait);
                ui.Box(panel,"Speaker accent",42,24,4,42,accent);
                ui.Text(panel,speaker,64,14,700,58,30,accent);
                ui.Text(panel,illustratedStage.CurrentScene == "shore" ? "갈릴리 · 호숫가" : "갈릴리 · 석조 방",1130,24,530,45,22,new Color(.76f,.83f,.83f));
                ui.Rule(panel,44,77,1630,new Color(.98f,.85f,.58f,.35f));
                ui.Text(panel,line,44,96,1632,160,34);
                dialogueAdvance = () => { lineIndex++; RenderDialogue(); };
                ui.Primary(panel,lineIndex+1 < activeLines.Count ? "다음 대사 [E]" : session.Choices.Count > 0 ? "선택하기 [E]" : "계속 [E]",1230,278,440,80,() => dialogueAdvance?.Invoke());
                ui.Text(panel,(lineIndex+1).ToString("00")+"  /  "+activeLines.Count.ToString("00"),44,294,240,48,23,new Color(.7f,.8f,.83f));
                if (voice != null && voice.PlayLine(fullLine))
                {
                    ui.Text(panel,"대사 음성",310,294,260,48,23,DawnUi.Teal);
                    ui.Button(panel,"다시 듣기",920,278,280,80,() => voice.Replay(),!voice.Muted);
                }
                return;
            }
            if (session.Choices.Count > 0)
            {
                ui.Box(panel,"Choice accent",35,32,6,64,DawnUi.Gold);
                ui.Text(panel,"어떤 손길을 건네시겠습니까?",65,25,1650,85,34,DawnUi.Gold);
                for (int i = 0; i < session.Choices.Count; ++i)
                {
                    int choiceIndex = i; ui.Button(panel,session.Choices[i].text,38,130+i*106,1724,94,() => MakeChoice(choiceIndex));
                }
            }
            else if (session.World.pointId == "ending") ShowEnding();
            else CloseModal();
        }
        void MakeChoice(int index)
        {
            if (busy || lastChoiceFrame == Time.frameCount || index < 0 || index >= session.Choices.Count) return;
            lastChoiceFrame = Time.frameCount; busy = true; session.Choose(index); ApplyWorld(); RefreshObjective(); Save();
            busy = false; PlayCue(); ShowDialogue(session.Lines);
        }
        void ShowEnding()
        {
            modal = true; ClearScreen(); ui.Fill(screen,"Outcome veil",new Color(.02f,.06f,.1f,.5f));
            var p = ui.Card(screen,"Outcome",340,210,1240,730,DawnUi.Ink);
            ui.Rule(p,50,25,96,DawnUi.Gold);
            string title = session.Route == "witness" ? "나눔의 목격자" : session.Route == "shelter" ? "문턱의 등불" : "이어진 발걸음";
            ui.Text(p,"발견한 길  " + session.Meta.routes.Count + " / 3",50,45,1140,55,25,DawnUi.Gold);
            ui.Text(p,title,50,126,1140,130,56);
            string note = session.Route == "witness" ? "기준 루트의 문턱 · 마가복음 6:30–44, 요한복음 6:1–13\n여행자와 미리암, 대사는 창작 연결 장면입니다." : "가상 루트 · 플레이어의 선택을 위한 창작입니다.\n성경 사건의 대체 결말로 제시하지 않습니다.";
            if (Chapter != null)
            {
                var source = Array.Find(Chapter.sources, s => s.routeId == session.Route);
                if (source != null) note = (source.biblicalBaseline ? "기준 루트의 문턱 · " : "가상 루트 · ") + source.sourceReferences + "\n" + source.fictionalElements;
            }
            ui.Text(p,note,50,265,1130,165,29);
            ui.Primary(p,"기억을 품고 과거로",50,460,540,100,ShowMemories);
            var outcomeStatus = ui.Text(p,"첫 구간을 마쳤습니다. 다른 선택으로 세 가지 길을 발견할 수 있습니다.",50,596,1140,95,25);
            ui.Button(p,"저장하고 타이틀로",640,460,540,100,() => {
                if (!Save()) { outcomeStatus.text = message; return; }
                Notify(""); ShowTitle();
            });
        }
        void ShowMemories()
        {
            if (!inGame || session == null || inIntro) return; modal = true; ClearScreen();
            ui.Fill(screen,"Memories veil",new Color(.02f,.06f,.1f,.5f));
            var p = ui.Card(screen,"Memories",110,170,1700,810,DawnUi.Ink);
            ui.Text(p,"기억의 지도",45,32,1550,110,48); ui.Text(p,"세계는 돌아가도, 당신이 발견한 기억은 남습니다.",45,150,1560,70,27,DawnUi.Gold);
            string routes = ""; foreach (var id in session.Meta.routes) routes += "● " + (id=="witness"?"나눔의 목격자":id=="shelter"?"문턱의 등불":"이어진 발걸음")+"\n";
            ui.Text(p,"발견한 길  "+session.Meta.routes.Count+" / 3\n"+routes+"\n발견한 단서  "+session.Meta.discoveries.Count+" / 3",45,230,690,280,28);
            ui.Text(p,"이번 여정과 회귀의 선택 기록",810,225,820,55,28);
            var history = session.Meta.choiceHistory; string recent = ""; for(int i=Math.Max(0,history.Count-4);i<history.Count;++i) recent += history[i].Replace("shore_before_choice","호숫가").Replace("room_before_choice","석조 방")+"\n\n";
            ui.Text(p,recent.Length==0?"아직 선택하지 않았습니다.":recent,810,300,820,310,23);
            for(int i=0;i<session.Checkpoints.Count;++i)
            {
                string id=session.Checkpoints[i].id; ui.Button(p,id=="shore_before_choice"?"첫 선택 전 · 호숫가로 회귀":"두 번째 선택 전 · 석조 방으로 회귀",45,530+i*100,690,90,()=>Rewind(id));
            }
            ui.Button(p,"돌아가기",1240,690,390,90,CloseModal);
        }
        void Rewind(string id)
        {
            CancelPresentation(); session.Rewind(id); ApplyWorld(); inGame = true; BuildHud(); Save(); ambience.Play(); ShowDialogue(session.Lines);
        }
        void ShowSettings()
        {
            if (inIntro) return; modal = true; ClearScreen();
            ui.Fill(screen,"Settings veil",new Color(.02f,.06f,.1f,.75f));
            var p = ui.Card(screen,"Settings",250,70,1420,960,DawnUi.Ink);
            ui.Text(p,"설정과 여정 보관",48,34,1220,105,44);
            ui.Text(p,"한국어 자막 크기  "+Mathf.RoundToInt(textScale*100)+"%",48,145,660,65,28);
            ui.Button(p,"작게",860,135,210,90,()=>ChangeText(-.1f)); ui.Button(p,"크게",1090,135,270,90,()=>ChangeText(.1f));
            ui.Text(p,"전체 음량  "+Mathf.RoundToInt(volume*100)+"%",48,255,660,65,28);
            ui.Button(p,"줄이기",860,245,210,90,()=>ChangeVolume(-.1f)); ui.Button(p,"높이기",1090,245,270,90,()=>ChangeVolume(.1f));
            ui.Text(p,"배경 음악 · 갈릴리의 새벽",48,345,780,60,26,DawnUi.Teal);
            ui.Button(p,music != null && music.Muted ? "배경 음악 켜기" : "배경 음악 끄기",860,335,500,76,()=> {
                if(music != null) music.SetMuted(!music.Muted);
                ShowSettings();
            });
            ui.Text(p,"초반 대사 음성",48,428,780,60,26,DawnUi.Teal);
            ui.Button(p,voice != null && voice.Muted ? "대사 음성 켜기" : "대사 음성 끄기",860,418,500,76,()=> {
                if (voice != null) voice.SetMuted(!voice.Muted);
                ShowSettings();
            });
            ui.Text(p,"저장 옮기기 · 다른 기기에서도 같은 여정을 이어갈 수 있습니다.",48,507,1320,50,26);
            var field=ui.Input(p,48,565,1320,125);
            ui.Button(p,"저장 내보내기",48,710,420,80,()=> {
                if(inGame && !Save()) { field.text=message; return; }
                if(PersistentSaves.TryExport(activeSaveSlot,out var portable,out var error)) { field.text=portable; GUIUtility.systemCopyBuffer=portable; Notify("저장 JSON을 표시했습니다. 전체 선택 후 복사할 수 있습니다."); }
                else { field.text=error; }
            });
            ui.Button(p,"JSON 가져오기",488,710,420,80,()=> {
                string importSlot = SaveSlot + "-import-" + DateTime.UtcNow.Ticks;
                bool valid(string payload) { try { sessionCheck(payload); return true; } catch { return false; } }
                if(PersistentSaves.TryImport(importSlot,SaveGame,1,PortableVersion,field.text,valid,out var error))
                { activeSaveSlot=importSlot; PlayerPrefs.SetString("POD.activeSlot",activeSaveSlot); PlayerPrefs.Save(); CreateSaveSlot(); ContinueGame(); }
                else field.text="가져오기 실패 — 기존 여정은 보존했습니다.\n"+error;
            });
            ui.Button(p,inGame?"저장하고 타이틀로":"타이틀로",48,810,630,100,()=> {
                if(inGame && !Save()) { field.text=message; return; }
                Notify(""); ShowTitle();
            });
            ui.Button(p,"돌아가기",710,810,650,100,()=> { if(inGame) CloseModal(); else ShowTitle(); });
            ui.Text(p,"첫 구간 개발판 · 대사와 여행자·미리암은 창작입니다.",48,916,1320,40,18,new Color(.63f,.73f,.77f));
        }
        void sessionCheck(string payload) { var test = new NarrativeSession(compiledStory); test.ValidateSave(JsonUtility.FromJson<NarrativeSave>(payload)); }
        void ChangeText(float delta) { textScale=Mathf.Clamp(textScale+delta,.9f,1.3f); ui.TextScale=textScale; PlayerPrefs.SetFloat("POD.textScale",textScale); PlayerPrefs.Save(); if(inGame) BuildHud(); ShowSettings(); }
        void ChangeVolume(float delta) { volume=Mathf.Clamp01(volume+delta); AudioListener.volume=volume; PlayerPrefs.SetFloat("POD.volume",volume); PlayerPrefs.Save(); ShowSettings(); }
        void CloseModal()
        {
            if(!inGame) { ShowTitle(); return; }
            if(session.Choices.Count>0) { ShowDialogue(session.Lines); return; }
            modal=false; ClearScreen(); SwitchToPlay(); RefreshObjective();
        }
        bool Save()
        {
            if(session==null) return false; SyncPositions();
            try { saves.Save(session); return true; } catch(Exception ex) { Notify("저장하지 못했습니다: "+ex.Message); return false; }
        }
        void Notify(string text)
        {
            message=text;
            if(notification!=null) notification.text=text;
            if(notificationBackdrop!=null) notificationBackdrop.gameObject.SetActive(!string.IsNullOrWhiteSpace(text));
        }
        void SyncPositions()
        {
            if(session==null) return;
            foreach(var p in session.World.actors) if(actors.TryGetValue(p.id,out var actor)) { p.x=actor.position.x; p.y=actor.position.y; p.z=actor.position.z; p.yaw=actor.eulerAngles.y; }
        }
        void ApplyWorld()
        {
            controller.enabled=false;
            foreach(var p in session.World.actors) if(actors.TryGetValue(p.id,out var actor)) actor.SetPositionAndRotation(new Vector3(p.x,p.y,p.z),Quaternion.Euler(0,p.yaw,0));
            controller.enabled=true; verticalSpeed=0; StopLocomotion(); cameraFollowVelocity=Vector3.zero;
            pivot.position=actors["traveler"].position+Vector3.up*1.1f;
            sharedBasket.SetActive(session.World.basketShared); shelterRug.SetActive(session.World.shelterPrepared); connectionMarker.SetActive(session.World.peopleConnected);
            string scene = session.World.pointId == "ending"
                ? (session.World.shelterPrepared ? "room" : "shore") : session.World.pointId;
            illustratedStage.SetScene(scene);
            actors["jesus"].GetComponent<CapsuleCollider>().enabled = scene == "shore";
            illustratedStage.SetWorldFlags(session.World.basketShared, session.World.shelterPrepared, session.World.peopleConnected);
            illustratedStage.ResetLook();
            playCamera.PreviousStateIsValid=false; dialogueCamera.PreviousStateIsValid=false;
        }
        void RevealWorld() { worldRoot.SetActive(true); mainCamera.cullingMask = 0; mainCamera.backgroundColor = DawnUi.Ink; mainCamera.clearFlags = CameraClearFlags.SolidColor; illustratedStage.SetVisible(true); }
        void SwitchToPlay() { RevealWorld(); illustratedStage.SetMode(DawnIllustratedStage.ViewMode.Exploration); introCamera.gameObject.SetActive(false); dialogueCamera.gameObject.SetActive(false); playCamera.gameObject.SetActive(true); }
        void SwitchToDialogue()
        {
            RevealWorld(); illustratedStage.SetMode(DawnIllustratedStage.ViewMode.Dialogue);
            introCamera.gameObject.SetActive(false); playCamera.gameObject.SetActive(true); dialogueCamera.gameObject.SetActive(true);
            Transform target=actors[session.World.pointId=="shore"?"jesus":"miriam"];
            var traveler=actors["traveler"];
            var toward=target.position-traveler.position; toward.y=0;
            if (toward.sqrMagnitude>.01f) traveler.rotation=Quaternion.LookRotation(toward);
            // A longer conversation lens and eye-level framing complement the drawn portrait.
            var focus=target.position+Vector3.up*1.32f;
            dialogueCamera.transform.position=focus+(session.World.pointId=="shore" ? new Vector3(1.35f,.15f,-3.8f) : new Vector3(.7f,.18f,-2.3f));
            dialogueCamera.transform.LookAt(focus);
        }
        void CancelPresentation()
        {
            // Stop ALL time-dependent presentation before replacing world/Ink state.
            StopAllCoroutines(); if(director!=null) director.Stop();
            StopLocomotion(); voice?.Clear();
            if(ambience!=null) ambience.Stop(); if(cue!=null) cue.Stop();
            foreach(var source in worldRoot.GetComponentsInChildren<AudioSource>(true)) source.Stop();
            inIntro=false; busy=false;
        }
        IEnumerator PlayIntro()
        {
            RevealWorld(); illustratedStage.SetMode(DawnIllustratedStage.ViewMode.Intro);
            inIntro=true; modal=true; if(hud!=null) hud.gameObject.SetActive(false); playCamera.gameObject.SetActive(false); dialogueCamera.gameObject.SetActive(false); introCamera.gameObject.SetActive(true);
            ClearScreen(); ui.Box(screen,"LetterboxTop",0,0,1920,120,Color.black); ui.Box(screen,"LetterboxBottom",0,950,1920,130,Color.black);
            ui.Text(screen,"갈릴리 호숫가 · 낯선 새벽",200,975,1520,65,33);
            director.Play(); ambience.Play();
            while(director.state==PlayState.Playing) yield return null;
            inIntro=false; if(hud!=null) hud.gameObject.SetActive(true); ShowDialogue(session.Lines);
        }
        void BuildWorld()
        {
            // The painted stage owns visible art. The compact 3D shell only owns
            // movement/contact, so no obsolete mesh appears behind dialogue.
            worldRoot = new GameObject("Galilee interaction world");
            ColliderBox("Walkable ground", new Vector3(0,-.2f,0), new Vector3(24,.3f,22));
            CreateActor("traveler", new Vector3(0,.1f,-5));
            CreateActor("jesus", new Vector3(0,.1f,0));
            CreateActor("miriam", new Vector3(-2,.1f,1));
            controller = actors["traveler"].gameObject.AddComponent<CharacterController>();
            controller.height=1.8f; controller.radius=.28f; controller.center=Vector3.up*.9f; controller.stepOffset=.3f;
            sharedBasket = new GameObject("Shared basket state");
            shelterRug = new GameObject("Shelter rug state");
            connectionMarker = new GameObject("Gathered water jars state");
            foreach (var marker in new[] { sharedBasket, shelterRug, connectionMarker })
            { marker.transform.SetParent(worldRoot.transform); marker.SetActive(false); }
            RenderSettings.skybox = null; RenderSettings.fog = false;
        }
        void CreateActor(string id, Vector3 position)
        {
            var root = new GameObject(id); root.layer=2; root.transform.SetParent(worldRoot.transform);
            root.transform.position=position; actors[id]=root.transform;
            if (id != "traveler")
            {
                var collider=root.AddComponent<CapsuleCollider>(); collider.height=1.8f;
                collider.radius=.3f; collider.center=Vector3.up*.9f;
            }
        }
        void ColliderBox(string name,Vector3 position,Vector3 size)
        { var box=new GameObject(name,typeof(BoxCollider)); box.transform.SetParent(worldRoot.transform); box.transform.position=position; box.GetComponent<BoxCollider>().size=size; }
        void BuildCameras()
        {
            mainCamera=Camera.main; if(mainCamera==null) { var go=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)); go.tag="MainCamera"; mainCamera=go.GetComponent<Camera>(); }
            mainCamera.backgroundColor=DawnUi.Ink; mainCamera.clearFlags=CameraClearFlags.SolidColor; mainCamera.cullingMask=0;
            mainCamera.nearClipPlane=.08f; mainCamera.farClipPlane=160; mainCamera.allowMSAA=true;
            var cameraData=mainCamera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing=false; cameraData.antialiasing=AntialiasingMode.FastApproximateAntialiasing;
            cameraData.antialiasingQuality=AntialiasingQuality.High; cameraData.stopNaN=true; cameraData.dithering=true;
            var brain=mainCamera.GetComponent<CinemachineBrain>() ?? mainCamera.gameObject.AddComponent<CinemachineBrain>();
            brain.DefaultBlend=new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut,.4f);
            pivot=new GameObject("Third person target").transform; pivot.position=actors["traveler"].position+Vector3.up*1.1f;
            playCamera=new GameObject("Play Camera",typeof(CinemachineCamera)).GetComponent<CinemachineCamera>(); playCamera.Follow=pivot; playCamera.LookAt=pivot; playCamera.Priority=10; playCamera.Lens.FieldOfView=42;
            var follow=playCamera.gameObject.AddComponent<CinemachineFollow>(); follow.FollowOffset=new Vector3(.35f,.85f,-5.8f);
            follow.TrackerSettings.BindingMode=Unity.Cinemachine.TargetTracking.BindingMode.LockToTarget;
            playCamera.gameObject.AddComponent<CinemachineRotationComposer>();
            var obstacles = playCamera.gameObject.AddComponent<CinemachineDeoccluder>();
            obstacles.CollideAgainst = 1;
            obstacles.AvoidObstacles = new CinemachineDeoccluder.ObstacleAvoidance { Enabled=true, CameraRadius=.22f,
                MaximumEffort=4, Damping=.2f, DampingWhenOccluded=.1f, Strategy=CinemachineDeoccluder.ObstacleAvoidance.ResolutionStrategy.PullCameraForward };
            dialogueCamera=new GameObject("Conversation Camera",typeof(CinemachineCamera)).GetComponent<CinemachineCamera>(); dialogueCamera.Priority=20; dialogueCamera.Lens.FieldOfView=36;
            introCamera=new GameObject("Timeline Camera",typeof(CinemachineCamera)).GetComponent<CinemachineCamera>(); introCamera.Priority=30; introCamera.Lens.FieldOfView=48;
            director=gameObject.AddComponent<PlayableDirector>(); director.playOnAwake=false; director.extrapolationMode=DirectorWrapMode.None;
            var timeline=ScriptableObject.CreateInstance<TimelineAsset>(); timeline.name="Galilee dawn opening";
            var track=timeline.CreateTrack<DawnShotTrack>(null,"Dawn camera movement"); var clip=track.CreateClip<DawnShotAsset>(); clip.duration=6; clip.displayName="Arrive at Galilee";
            var shot=(DawnShotAsset)clip.asset; shot.from=new Vector3(7,4.5f,-7); shot.to=new Vector3(2.8f,2.5f,-4); shot.lookAt=new Vector3(-1,1,1);
            director.playableAsset=timeline; director.SetGenericBinding(track,introCamera.transform);
        }
        void BuildAudio()
        {
            ambience=gameObject.AddComponent<AudioSource>(); ambience.loop=true; ambience.volume=.03f; ambience.playOnAwake=false;
            const int rate=22050, seconds=4; var samples=new float[rate*seconds]; uint noise=12345;
            for(int i=0;i<samples.Length;i++) { noise=noise*1664525+1013904223; float n=((noise>>8)/(float)0xFFFFFF)*2-1; float wave=.45f+.4f*Mathf.Sin(i/(float)rate*Mathf.PI*.5f); samples[i]=n*.11f*wave; }
            ambience.clip=AudioClip.Create("Procedural shoreline placeholder",samples.Length,1,rate,false); ambience.clip.SetData(samples,0);
            cue=gameObject.AddComponent<AudioSource>(); cue.playOnAwake=false; cue.volume=.15f;
            var tone=new float[rate/3]; for(int i=0;i<tone.Length;i++) tone[i]=Mathf.Sin(2*Mathf.PI*440*i/rate)*Mathf.Exp(-12*i/(float)rate)*.2f;
            cue.clip=AudioClip.Create("Choice acknowledgement placeholder",tone.Length,1,rate,false); cue.clip.SetData(tone,0);
            music=BgmPlayer.Attach(gameObject,"Audio/dawn-on-galilee","POD.music",.65f);
            foreach (var source in GetComponents<AudioSource>()) if(source.clip==music.Clip) musicSource=source;
            voice=DialogueVoicePlayer.Attach(gameObject);
            ui.BeforeInteraction=()=>music?.NotifyUserGesture();
        }
        void PlayCue() { cue.Stop(); cue.Play(); }
        sealed class UnityCodec : ISnapshotCodec
        { public string Encode(NarrativeSave value)=>JsonUtility.ToJson(value); public NarrativeSave Decode(string json)=>JsonUtility.FromJson<NarrativeSave>(json); }
        sealed class SharedStorage : ITextSaveStorage
        {
            readonly string slot; public SharedStorage(string slot){this.slot=slot;} public bool Exists=>PersistentSaves.Exists(slot);
            public string Read() { if(PersistentSaves.TryLoad(slot,SaveGame,1,PortableVersion,out var value,out var error)) return value; throw new FormatException(error); }
            public void Write(string value) { if(!PersistentSaves.TrySave(slot,SaveGame,1,PortableVersion,value,out var error)) throw new InvalidOperationException(error); }
        }
    }
}
