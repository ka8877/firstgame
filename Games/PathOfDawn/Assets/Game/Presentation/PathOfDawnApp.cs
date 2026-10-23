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
        TextMeshProUGUI prompt, objective, notification;
        GameObject worldRoot, sharedBasket, shelterRug, connectionMarker;
        CharacterController controller;
        Transform pivot;
        Camera mainCamera;
        CinemachineCamera playCamera, dialogueCamera, introCamera;
        PlayableDirector director;
        AudioSource ambience, cue;
        InputActionMap input;
        InputAction move, mouseLook, stickLook, interact, menu, memories;
        bool inGame, modal, inIntro, busy;
        float yaw, pitch = 15, verticalSpeed, textScale = 1, volume = .65f;
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
        void OnDisable() { input?.Disable(); }
        void OnDestroy() { input?.Dispose(); }
        void Start()
        {
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem));
            var uiInput = EventSystem.current.GetComponent<InputSystemUIInputModule>();
            if (uiInput == null) uiInput = EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>();
            uiInput.AssignDefaultActions();
            var go = new GameObject("Path of Dawn UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = go.GetComponent<RectTransform>(); go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            ui = new DawnUi { Font = KoreanFont != null ? KoreanFont : Resources.Load<TMP_FontAsset>("Fonts/Korean") ?? TMP_Settings.defaultFontAsset, TextScale = textScale };
            if (Chapter == null) Chapter = Resources.Load<StoryChapter>("Content/Galilee");
            var ink = Chapter != null && Chapter.story != null ? Chapter.story : Resources.Load<InkFile>("Story/galilee");
            if (ink == null || !ink.isCompiled)
            {
                ui.Fill(canvas, "Fatal", DawnUi.Ink); ui.Text(canvas, "이야기 파일을 불러올 수 없습니다.\nUnity에서 galilee.ink의 Ink 가져오기 오류를 확인해 주세요.", 180, 300, 1500, 350, 34); return;
            }
            if (Chapter != null && (Chapter.contentVersion != NarrativeSession.ContentVersion || Chapter.stableId != "galilee_dawn"))
                throw new InvalidOperationException("StoryChapter 메타데이터와 실행 콘텐츠 버전이 다릅니다.");
            compiledStory = ink.storyJson; activeSaveSlot = PlayerPrefs.GetString("POD.activeSlot", SaveSlot);
            CreateSaveSlot(); BuildWorld(); BuildCameras(); BuildAudio(); ShowTitle();
        }
        void CreateSaveSlot() => saves = new NarrativeSaveSlot(new SharedStorage(activeSaveSlot), new UnityCodec());
        void Update()
        {
            if (compiledStory == null) return;
            if (inGame && !inIntro && menu.WasPressedThisFrame()) { if (modal) CloseModal(); else ShowSettings(); }
            if (inGame && !modal && !inIntro && memories.WasPressedThisFrame()) ShowMemories();
            if (modal && !inIntro && dialogueAdvance != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            { dialogueAdvance(); return; }
            if (!inGame || modal || inIntro || busy) return;
            Vector2 look = stickLook.ReadValue<Vector2>() * (100 * Time.deltaTime);
            if (Mouse.current != null && Mouse.current.rightButton.isPressed) look += mouseLook.ReadValue<Vector2>() * .10f;
            yaw += look.x; pitch = Mathf.Clamp(pitch - look.y, -8, 42);
            pivot.rotation = Quaternion.Euler(pitch, yaw, 0);
            var movement = move.ReadValue<Vector2>(); Vector3 direction = Quaternion.Euler(0, yaw, 0) * new Vector3(movement.x, 0, movement.y);
            if (direction.sqrMagnitude > 1) direction.Normalize();
            if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            verticalSpeed += Physics.gravity.y * Time.deltaTime;
            controller.Move((direction * 2.3f + Vector3.up * verticalSpeed) * Time.deltaTime);
            var actor = actors["traveler"];
            if (direction.sqrMagnitude > .01f) actor.rotation = Quaternion.Slerp(actor.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 10);
            // Bounded authored slice; collision walls prevent leaving through geometry gaps.
            var p = actor.position; p.x = Mathf.Clamp(p.x, -11, 4.2f); p.z = Mathf.Clamp(p.z, -9, 9);
            if (p.y < -2) p = new Vector3(0, .15f, -5);
            if (p != actor.position) { controller.enabled = false; actor.position = p; controller.enabled = true; }
            pivot.position = actor.position + Vector3.up * 1.45f;
            UpdatePrompt(); if (interact.WasPressedThisFrame()) Interact();
        }
        void OnApplicationPause(bool paused) { if (paused && inGame && session != null) Save(); }
        void OnApplicationQuit() { if (inGame && session != null) Save(); }
        void ClearScreen()
        {
            dialogueAdvance = null;
            if (screen != null) Destroy(screen.gameObject);
            screen = ui.Fill(canvas, "Screen");
        }
        void ShowTitle()
        {
            CancelPresentation(); inGame = false; modal = true; if (hud != null) Destroy(hud.gameObject);
            ClearScreen(); dialogueCamera.gameObject.SetActive(false); introCamera.gameObject.SetActive(true); playCamera.gameObject.SetActive(false);
            introCamera.transform.SetPositionAndRotation(new Vector3(6, 3, -8), Quaternion.LookRotation(new Vector3(-5, -1.5f, 8)));
            var panel = ui.Box(screen, "Title", 80, 90, 700, 900, DawnUi.Ink);
            ui.Text(panel, "PATH OF DAWN", 54, 50, 590, 55, 24, DawnUi.Gold);
            // CJK line metrics need more than the nominal point size, especially at 130%.
            ui.Text(panel, "새벽의 길", 50, 108, 600, 150, 64);
            ui.Text(panel, "갈릴리, 처음 건네는 손길", 54, 265, 600, 65, 26);
            ui.Text(panel, "낯선 시대의 여행자가 되어\n한 번의 선택과 다시 찾은 시간을 따라가세요.", 54, 340, 590, 135, 22);
            ui.Button(panel, "새 게임", 54, 480, 590, 70, NewGame);
            ui.Button(panel, "이어하기", 54, 566, 590, 70, ContinueGame, saves.Exists);
            ui.Button(panel, "설정 · 저장 가져오기", 54, 652, 590, 70, ShowSettings);
            ui.Text(panel, "첫 구간 개발판 · 대사는 창작입니다.\n임시 인물·공간이며 교체 예정입니다.", 54, 755, 590, 125, 20, new Color(.7f,.75f,.73f));
            ui.Text(screen, message, 830, 870, 950, 140, 26);
            ambience.Play();
        }
        void NewGame()
        {
            if (saves.Exists && !saves.WriteBlocked)
            {
                ClearScreen(); var panel = ui.Box(screen,"New Game",410,250,1100,560,DawnUi.Ink);
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
            session = new NarrativeSession(compiledStory); ApplyWorld(); inGame = true; modal = false;
            ClearScreen(); BuildHud(); Save(); StartCoroutine(PlayIntro());
        }
        void ContinueGame()
        {
            var candidate = new NarrativeSession(compiledStory);
            if (!saves.LoadInto(candidate, out var error)) { message = "이어하기 실패 — 원본 보존\n" + error; ShowTitle(); return; }
            CancelPresentation(); session = candidate; ApplyWorld(); inGame = true; modal = false;
            ClearScreen(); BuildHud(); SwitchToPlay();
            if (session.Lines.Count > 0 || session.Choices.Count > 0) ShowDialogue(session.Lines);
        }
        void BuildHud()
        {
            if (hud != null) Destroy(hud.gameObject); hud = ui.Fill(canvas,"HUD"); hud.SetAsFirstSibling();
            var top = ui.Box(hud,"Top",24,24,1872,114,DawnUi.Ink);
            objective = ui.Text(top,"",25,7,1060,100,23);
            ui.Button(top,"기억 / 회귀 [M]",1110,26,255,62,ShowMemories);
            ui.Button(top,"저장",1380,26,145,62,() => { if (Save()) Notify("여정을 저장했습니다."); });
            ui.Button(top,"설정 [Esc]",1540,26,305,62,ShowSettings);
            ui.Box(hud,"Interaction backdrop",24,885,1872,180,new Color(.08f,.13f,.15f,.82f));
            prompt = ui.Text(hud,"",50,902,1300,100,28);
            notificationBackdrop = ui.Box(hud,"Notification backdrop",24,810,1552,90,DawnUi.Ink);
            notificationBackdrop.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
            notificationBackdrop.gameObject.SetActive(false);
            notification = ui.Text(hud,"",50,825,1500,70,25,DawnUi.Gold);
            ui.Text(hud,"WASD / 방향키: 걷기   ·   마우스 오른쪽 드래그: 카메라   ·   E: 대화·조사",50,1015,1810,50,18);
            RefreshObjective();
        }
        void RefreshObjective()
        {
            if (objective == null || session == null) return;
            string goal = session.World.pointId == "shore" ? "예수님 곁으로 다가가 대화하세요." : session.World.pointId == "room" ? "왼쪽 석조 방에서 미리암과 이야기하세요." : "발견한 길을 돌아보고 다른 선택을 시도하세요.";
            objective.text = "갈릴리 · 소지품 " + session.World.inventory.Count + " · 미리암의 신뢰 " + session.Trust + "\n" + goal;
        }
        void UpdatePrompt()
        {
            if (prompt == null) return;
            var p = actors["traveler"].position;
            if (session.World.pointId == "shore" && Vector3.Distance(p,actors["jesus"].position)<3) prompt.text = "[E] 예수님과 미리암에게 말 걸기";
            else if (session.World.pointId == "room" && Vector3.Distance(p,actors["miriam"].position)<2.8f) prompt.text = "[E] 미리암과 바구니에 관해 이야기하기";
            else if (Vector3.Distance(p,new Vector3(-2,0,-2))<2) prompt.text = "[E] 호숫가 바구니 조사";
            else if (Vector3.Distance(p,new Vector3(-5,0,-2))<2.3f) prompt.text = "[E] 돌벽의 등불 자리 조사";
            else prompt.text = "[E] 멈춘 시계 살펴보기";
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
            activeLines = new List<string>(lines); lineIndex = 0; modal = true; SwitchToDialogue(); RenderDialogue();
        }
        void RenderDialogue()
        {
            ClearScreen(); var panel = ui.Box(screen,"Dialogue",115,600,1690,430,DawnUi.Ink);
            ui.Text(panel,"새벽의 길  ·  " + (session.World.pointId == "shore" ? "호숫가" : "석조 방"),38,22,1550,50,22,DawnUi.Gold);
            if (lineIndex < activeLines.Count)
            {
                ui.Text(panel,activeLines[lineIndex],38,92,1600,210,31);
                dialogueAdvance = () => { lineIndex++; RenderDialogue(); };
                ui.Button(panel,lineIndex+1 < activeLines.Count ? "다음 대사 [E]" : session.Choices.Count > 0 ? "선택하기 [E]" : "계속 [E]",1250,330,400,64,() => dialogueAdvance?.Invoke());
                ui.Text(panel,(lineIndex+1)+" / "+activeLines.Count,40,345,200,40,20);
                return;
            }
            if (session.Choices.Count > 0)
            {
                ui.Text(panel,"어떤 손길을 건네시겠습니까?",38,76,1550,65,30);
                for (int i = 0; i < session.Choices.Count; ++i)
                {
                    int choiceIndex = i; ui.Button(panel,session.Choices[i].text,38,153+i*82,1600,72,() => MakeChoice(choiceIndex));
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
            modal = true; ClearScreen(); var p = ui.Box(screen,"Outcome",340,210,1240,700,DawnUi.Ink);
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
            ui.Button(p,"기억을 품고 과거로",50,460,540,80,ShowMemories);
            var outcomeStatus = ui.Text(p,"첫 구간을 마쳤습니다. 다른 선택으로 세 가지 길을 발견할 수 있습니다.",50,590,1140,65,24);
            ui.Button(p,"저장하고 타이틀로",640,460,540,80,() => {
                if (!Save()) { outcomeStatus.text = message; return; }
                ShowTitle();
            });
        }
        void ShowMemories()
        {
            if (!inGame || session == null || inIntro) return; modal = true; ClearScreen();
            var p = ui.Box(screen,"Memories",110,170,1700,810,DawnUi.Ink);
            ui.Text(p,"기억의 지도",45,32,1550,110,48); ui.Text(p,"세계는 돌아가도, 당신이 발견한 기억은 남습니다.",45,150,1560,70,27,DawnUi.Gold);
            string routes = ""; foreach (var id in session.Meta.routes) routes += "● " + (id=="witness"?"나눔의 목격자":id=="shelter"?"문턱의 등불":"이어진 발걸음")+"\n";
            ui.Text(p,"발견한 길  "+session.Meta.routes.Count+" / 3\n"+routes+"\n발견한 단서  "+session.Meta.discoveries.Count+" / 3",45,230,690,280,28);
            ui.Text(p,"이번 여정과 회귀의 선택 기록",810,225,820,55,28);
            var history = session.Meta.choiceHistory; string recent = ""; for(int i=Math.Max(0,history.Count-4);i<history.Count;++i) recent += history[i].Replace("shore_before_choice","호숫가").Replace("room_before_choice","석조 방")+"\n\n";
            ui.Text(p,recent.Length==0?"아직 선택하지 않았습니다.":recent,810,300,820,310,23);
            for(int i=0;i<session.Checkpoints.Count;++i)
            {
                string id=session.Checkpoints[i].id; ui.Button(p,id=="shore_before_choice"?"첫 선택 전 · 호숫가로 회귀":"두 번째 선택 전 · 석조 방으로 회귀",45,530+i*85,690,72,()=>Rewind(id));
            }
            ui.Button(p,"돌아가기",1240,700,390,70,CloseModal);
        }
        void Rewind(string id)
        {
            CancelPresentation(); session.Rewind(id); ApplyWorld(); inGame = true; BuildHud(); Save(); ambience.Play(); ShowDialogue(session.Lines);
        }
        void ShowSettings()
        {
            if (inIntro) return; modal = true; ClearScreen(); var p = ui.Box(screen,"Settings",300,110,1320,900,DawnUi.Ink);
            ui.Text(p,"설정과 여정 보관",48,34,1220,105,44);
            ui.Text(p,"한국어 자막 크기  "+Mathf.RoundToInt(textScale*100)+"%",48,145,660,65,28);
            ui.Button(p,"작게",760,140,210,65,()=>ChangeText(-.1f)); ui.Button(p,"크게",1000,140,260,65,()=>ChangeText(.1f));
            ui.Text(p,"전체 음량  "+Mathf.RoundToInt(volume*100)+"%",48,232,660,65,28);
            ui.Button(p,"줄이기",760,230,210,65,()=>ChangeVolume(-.1f)); ui.Button(p,"높이기",1000,230,260,65,()=>ChangeVolume(.1f));
            ui.Text(p,"저장 옮기기 · 웹에서 내보낸 JSON을 PC에서 그대로 가져올 수 있습니다.",48,330,1210,75,23);
            var field=ui.Input(p,48,415,1210,220);
            ui.Button(p,"저장 내보내기",48,660,360,65,()=> {
                if(inGame && !Save()) { field.text=message; return; }
                if(PersistentSaves.TryExport(activeSaveSlot,out var portable,out var error)) { field.text=portable; GUIUtility.systemCopyBuffer=portable; Notify("저장 JSON을 표시했습니다. 전체 선택 후 복사할 수 있습니다."); }
                else { field.text=error; }
            });
            ui.Button(p,"JSON 가져오기",438,660,360,65,()=> {
                string importSlot = SaveSlot + "-import-" + DateTime.UtcNow.Ticks;
                bool valid(string payload) { try { sessionCheck(payload); return true; } catch { return false; } }
                if(PersistentSaves.TryImport(importSlot,SaveGame,1,PortableVersion,field.text,valid,out var error))
                { activeSaveSlot=importSlot; PlayerPrefs.SetString("POD.activeSlot",activeSaveSlot); PlayerPrefs.Save(); CreateSaveSlot(); ContinueGame(); }
                else field.text="가져오기 실패 — 기존 여정은 보존했습니다.\n"+error;
            });
            ui.Button(p,inGame?"저장하고 타이틀로":"타이틀로",48,760,560,75,()=> {
                if(inGame && !Save()) { field.text=message; return; }
                ShowTitle();
            });
            ui.Button(p,"돌아가기",650,760,610,75,()=> { if(inGame) CloseModal(); else ShowTitle(); });
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
            controller.enabled=true; verticalSpeed=0; pivot.position=actors["traveler"].position+Vector3.up*1.45f;
            sharedBasket.SetActive(session.World.basketShared); shelterRug.SetActive(session.World.shelterPrepared); connectionMarker.SetActive(session.World.peopleConnected);
            playCamera.PreviousStateIsValid=false; dialogueCamera.PreviousStateIsValid=false;
        }
        void SwitchToPlay() { introCamera.gameObject.SetActive(false); dialogueCamera.gameObject.SetActive(false); playCamera.gameObject.SetActive(true); }
        void SwitchToDialogue()
        {
            introCamera.gameObject.SetActive(false); playCamera.gameObject.SetActive(true); dialogueCamera.gameObject.SetActive(true);
            Transform target=actors[session.World.pointId=="shore"?"jesus":"miriam"];
            var focus=target.position+Vector3.up*1.25f;
            dialogueCamera.transform.position=focus+(session.World.pointId=="shore" ? new Vector3(1.8f,.35f,-3.2f) : new Vector3(.9f,.25f,-1.5f));
            dialogueCamera.transform.LookAt(focus);
        }
        void CancelPresentation()
        {
            // Stop ALL time-dependent presentation before replacing world/Ink state.
            StopAllCoroutines(); if(director!=null) director.Stop();
            if(ambience!=null) ambience.Stop(); if(cue!=null) cue.Stop();
            foreach(var source in worldRoot.GetComponentsInChildren<AudioSource>(true)) source.Stop();
            inIntro=false; busy=false;
        }
        IEnumerator PlayIntro()
        {
            inIntro=true; modal=true; playCamera.gameObject.SetActive(false); dialogueCamera.gameObject.SetActive(false); introCamera.gameObject.SetActive(true);
            ClearScreen(); ui.Box(screen,"LetterboxTop",0,0,1920,120,Color.black); ui.Box(screen,"LetterboxBottom",0,950,1920,130,Color.black);
            ui.Text(screen,"갈릴리 호숫가 · 낯선 새벽",200,975,1520,65,33);
            director.Play(); ambience.Play();
            while(director.state==PlayState.Playing) yield return null;
            inIntro=false; ShowDialogue(session.Lines);
        }
        void BuildWorld()
        {
            worldRoot=new GameObject("Galilee Dawn");
            // Unity's imported FBX coordinates are hut (+4,+3), water -X, doorway +Z.
            // Normalize this environment once: hut (-4,-3), water +X, doorway -Z.
            // Character FBXs already face +Z and intentionally keep their own convention.
            if(EnvironmentPrefab!=null) Instantiate(EnvironmentPrefab,Vector3.zero,Quaternion.Euler(0,180,0),worldRoot.transform);
            else { Primitive("Temporary shore",PrimitiveType.Cube,new Vector3(0,-.12f,0),new Vector3(24,.2f,22),new Color(.65f,.58f,.39f));
                Primitive("Temporary lake",PrimitiveType.Cube,new Vector3(13,-.08f,2),new Vector3(16,.1f,28),new Color(.23f,.48f,.53f));
                Primitive("Temporary stone back",PrimitiveType.Cube,new Vector3(-4,1.1f,-1.3f),new Vector3(5,2.2f,.4f),new Color(.64f,.59f,.46f)); }
            // Separate collision shell so replacement FBXs need no runtime mesh colliders.
            ColliderBox("Walkable ground",new Vector3(0,-.2f,0),new Vector3(24,.3f,22));
            ColliderBox("Room back",new Vector3(-4,1.1f,-1.3f),new Vector3(5,2.2f,.42f));
            ColliderBox("Room west",new Vector3(-6.4f,1.1f,-3),new Vector3(.44f,2.2f,3.4f));
            ColliderBox("Room east",new Vector3(-1.6f,1.1f,-3),new Vector3(.44f,2.2f,3.4f));
            ColliderBox("Room doorway west",new Vector3(-5.7f,1.1f,-4.7f),new Vector3(1.4f,2.2f,.45f));
            ColliderBox("Room doorway east",new Vector3(-2.3f,1.1f,-4.7f),new Vector3(1.4f,2.2f,.45f));
            CreateActor("traveler",TravelerPrefab,new Vector3(0,.1f,-5),new Color(.25f,.37f,.42f));
            CreateActor("jesus",JesusPrefab,new Vector3(0,.1f,0),new Color(.9f,.81f,.65f));
            CreateActor("miriam",MiriamPrefab,new Vector3(-2,.1f,1),new Color(.52f,.35f,.28f));
            controller=actors["traveler"].gameObject.AddComponent<CharacterController>(); controller.height=1.8f; controller.radius=.28f; controller.center=Vector3.up*.9f; controller.stepOffset=.3f;
            Primitive("Basket to examine",PrimitiveType.Cylinder,new Vector3(-2,.25f,-2),new Vector3(.6f,.25f,.6f),new Color(.5f,.3f,.13f));
            sharedBasket=Primitive("Shared basket",PrimitiveType.Cylinder,new Vector3(.6f,.25f,1),new Vector3(.7f,.25f,.7f),new Color(.77f,.55f,.23f));
            shelterRug=Primitive("Shelter rug",PrimitiveType.Cube,new Vector3(-4,.04f,-3.6f),new Vector3(2,.035f,1.4f),new Color(.56f,.27f,.20f));
            connectionMarker=Primitive("Gathered water jars",PrimitiveType.Cylinder,new Vector3(-1,.3f,0),new Vector3(.42f,.3f,.42f),new Color(.64f,.43f,.22f));
            sharedBasket.SetActive(false); shelterRug.SetActive(false); connectionMarker.SetActive(false);
            var sun=new GameObject("Warm dawn sunlight",typeof(Light)); sun.transform.rotation=Quaternion.Euler(32,-35,0); var light=sun.GetComponent<Light>(); light.type=LightType.Directional; light.color=new Color(1,.84f,.63f); light.intensity=1.6f; light.shadows=LightShadows.Soft;
            RenderSettings.ambientLight=new Color(.43f,.5f,.53f); RenderSettings.fog=true; RenderSettings.fogColor=new Color(.65f,.73f,.7f); RenderSettings.fogDensity=.012f;
        }
        void CreateActor(string id,GameObject prefab,Vector3 position,Color color)
        {
            var root=new GameObject(id); root.layer=2; root.transform.SetParent(worldRoot.transform); root.transform.position=position; actors[id]=root.transform;
            if(prefab!=null) { var model=Instantiate(prefab,root.transform); model.name=id+" · Temporary Blender proxy"; }
            else { var proxy=Primitive("Temporary "+id,PrimitiveType.Capsule,position+Vector3.up*.9f,new Vector3(.65f,.9f,.65f),color); proxy.transform.SetParent(root.transform,true); Destroy(proxy.GetComponent<Collider>()); }
            if (id != "traveler") { var collider=root.AddComponent<CapsuleCollider>(); collider.height=1.8f; collider.radius=.3f; collider.center=Vector3.up*.9f; }
        }
        GameObject Primitive(string name,PrimitiveType type,Vector3 position,Vector3 scale,Color color)
        {
            var go=GameObject.CreatePrimitive(type); go.name=name; go.transform.SetParent(worldRoot.transform); go.transform.position=position; go.transform.localScale=scale;
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color=color; go.GetComponent<Renderer>().sharedMaterial=material; return go;
        }
        void ColliderBox(string name,Vector3 position,Vector3 size)
        { var box=new GameObject(name,typeof(BoxCollider)); box.transform.SetParent(worldRoot.transform); box.transform.position=position; box.GetComponent<BoxCollider>().size=size; }
        void BuildCameras()
        {
            mainCamera=Camera.main; if(mainCamera==null) { var go=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)); go.tag="MainCamera"; mainCamera=go.GetComponent<Camera>(); }
            mainCamera.backgroundColor=new Color(.68f,.76f,.77f); mainCamera.clearFlags=CameraClearFlags.SolidColor;
            if(mainCamera.GetComponent<CinemachineBrain>()==null) mainCamera.gameObject.AddComponent<CinemachineBrain>();
            pivot=new GameObject("Third person target").transform; pivot.position=actors["traveler"].position+Vector3.up*1.45f;
            playCamera=new GameObject("Play Camera",typeof(CinemachineCamera)).GetComponent<CinemachineCamera>(); playCamera.Follow=pivot; playCamera.LookAt=pivot; playCamera.Priority=10; playCamera.Lens.FieldOfView=48;
            var follow=playCamera.gameObject.AddComponent<CinemachineFollow>(); follow.FollowOffset=new Vector3(0,1,-5);
            follow.TrackerSettings.BindingMode=Unity.Cinemachine.TargetTracking.BindingMode.LockToTarget;
            playCamera.gameObject.AddComponent<CinemachineRotationComposer>();
            var obstacles = playCamera.gameObject.AddComponent<CinemachineDeoccluder>();
            obstacles.CollideAgainst = 1;
            obstacles.AvoidObstacles = new CinemachineDeoccluder.ObstacleAvoidance { Enabled=true, CameraRadius=.22f,
                MaximumEffort=4, Damping=.2f, DampingWhenOccluded=.1f, Strategy=CinemachineDeoccluder.ObstacleAvoidance.ResolutionStrategy.PullCameraForward };
            dialogueCamera=new GameObject("Conversation Camera",typeof(CinemachineCamera)).GetComponent<CinemachineCamera>(); dialogueCamera.Priority=20; dialogueCamera.Lens.FieldOfView=43;
            introCamera=new GameObject("Timeline Camera",typeof(CinemachineCamera)).GetComponent<CinemachineCamera>(); introCamera.Priority=30; introCamera.Lens.FieldOfView=48;
            director=gameObject.AddComponent<PlayableDirector>(); director.playOnAwake=false; director.extrapolationMode=DirectorWrapMode.None;
            var timeline=ScriptableObject.CreateInstance<TimelineAsset>(); timeline.name="Galilee dawn opening";
            var track=timeline.CreateTrack<DawnShotTrack>(null,"Dawn camera movement"); var clip=track.CreateClip<DawnShotAsset>(); clip.duration=6; clip.displayName="Arrive at Galilee";
            var shot=(DawnShotAsset)clip.asset; shot.from=new Vector3(7,4.5f,-7); shot.to=new Vector3(2.8f,2.5f,-4); shot.lookAt=new Vector3(-1,1,1);
            director.playableAsset=timeline; director.SetGenericBinding(track,introCamera.transform);
        }
        void BuildAudio()
        {
            ambience=gameObject.AddComponent<AudioSource>(); ambience.loop=true; ambience.volume=.15f; ambience.playOnAwake=false;
            const int rate=22050, seconds=4; var samples=new float[rate*seconds]; uint noise=12345;
            for(int i=0;i<samples.Length;i++) { noise=noise*1664525+1013904223; float n=((noise>>8)/(float)0xFFFFFF)*2-1; float wave=.45f+.4f*Mathf.Sin(i/(float)rate*Mathf.PI*.5f); samples[i]=n*.11f*wave; }
            ambience.clip=AudioClip.Create("Procedural shoreline placeholder",samples.Length,1,rate,false); ambience.clip.SetData(samples,0);
            cue=gameObject.AddComponent<AudioSource>(); cue.playOnAwake=false; cue.volume=.15f;
            var tone=new float[rate/3]; for(int i=0;i<tone.Length;i++) tone[i]=Mathf.Sin(2*Mathf.PI*440*i/rate)*Mathf.Exp(-12*i/(float)rate)*.2f;
            cue.clip=AudioClip.Create("Choice acknowledgement placeholder",tone.Length,1,rate,false); cue.clip.SetData(tone,0);
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
