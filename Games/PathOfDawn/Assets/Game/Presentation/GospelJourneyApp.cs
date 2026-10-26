using System;
using System.Collections;
using FirstGame.Shared;
using PathOfDawn.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PathOfDawn.Presentation
{
    /// <summary>The new, Scripture-led journey. Legacy observer fiction remains separate and inactive.</summary>
    public sealed class GospelJourneyApp : MonoBehaviour
    {
        public TMP_FontAsset KoreanFont;
        public GospelJourney Journey { get; private set; }
        public GospelTempleWorld World { get; private set; }
        public GospelThirdPersonMotor Player => World != null ? World.Player : null;
        public bool IsModal => modal;
        public string ActiveSaveSlot => activeSlot;
        DawnUi ui;
        RectTransform frame, screen, hud;
        TextMeshProUGUI objective, prompt, distance, notice, pinned;
        DawnPointerPad movePad, lookPad;
        GameObject touchRoot, noticeRoot;
        Button interactionButton;
        Button jumpButton, actionButton, runButton;
        BgmPlayer music;
        bool inGame, modal;
        float noticeUntil, autosaveTimer;
        string activeSlot = GospelJourney.SaveSlot, message = "";
        const string ActiveSlotPreference = "GOSPEL.activeSlot";
        const float InteractionRadius = 2.3f;
        public bool TouchInterface => DawnPlatformInput.UseTouchControls;

        void Start()
        {
            if(EventSystem.current==null)new GameObject("Gospel EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            var input=EventSystem.current.GetComponent<InputSystemUIInputModule>();
            if(input==null)input=EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>();input.AssignDefaultActions();
            var canvas=new GameObject("Gospel journey UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.layer=5;
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            ui=new DawnUi{Font=KoreanFont!=null?KoreanFont:Resources.Load<TMP_FontAsset>("Fonts/Korean")};
            frame=ui.Box(canvas.transform,"Gospel design frame",0,0,1280,720);frame.anchorMin=frame.anchorMax=frame.pivot=new Vector2(.5f,.5f);frame.anchoredPosition=Vector2.zero;
            activeSlot=PlayerPrefs.GetString(ActiveSlotPreference,GospelJourney.SaveSlot);
            World=gameObject.AddComponent<GospelTempleWorld>();World.Build();
            music=BgmPlayer.Attach(gameObject,"Audio/dawn-on-galilee","GOSPEL.music",.55f);ui.BeforeInteraction=()=>music.NotifyUserGesture();
            ShowTitle();
        }
        void Update()
        {
            if(World==null)return;
            if((Pointer.current!=null&&Pointer.current.press.wasPressedThisFrame)||(Keyboard.current!=null&&Keyboard.current.anyKey.wasPressedThisFrame))music.NotifyUserGesture();
            if(noticeRoot!=null)noticeRoot.SetActive(Time.unscaledTime<noticeUntil);
            if(!inGame)return;
            if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){if(modal&&Journey.Phase!=GospelPhase.Prologue&&Journey.Phase!=GospelPhase.Reading)ResumeJourney();else if(!modal)ShowSettings();}
            if(!modal&&Journey.Phase==GospelPhase.Exploring)
            {
                Player.TouchMove=movePad!=null?movePad.Value:Vector2.zero;
                Player.TouchLook=lookPad!=null?lookPad.ConsumeLook()*.13f:Vector2.zero;
                float meters=Vector3.Distance(Player.transform.position,World.LandmarkPosition(Journey.CurrentObjective.Id));
                bool nearby=meters<InteractionRadius;
                if(distance!=null)distance.text=nearby?"말씀을 읽을 수 있습니다":Mathf.CeilToInt(meters)+" m · 빛나는 표식으로";
                if(prompt!=null)prompt.text=nearby?"E · "+Journey.CurrentObjective.Title:
                    Player.Actions.IsInTrainingArea?"자유 연습 구역 · F 동작 연습 · 성경 본문 외 조작":"WASD 이동 · Shift 달리기 · Space 점프 · E 대화";
                if(interactionButton!=null)interactionButton.interactable=nearby;
                if(jumpButton!=null)jumpButton.interactable=Player.CanJump;
                if(actionButton!=null)actionButton.interactable=Player.CanPerformAction;
                if(nearby&&Keyboard.current!=null&&Keyboard.current.eKey.wasPressedThisFrame)Interact();
                autosaveTimer+=Time.deltaTime;if(autosaveTimer>18){Save(false);autosaveTimer=0;}
            }
            if(touchRoot!=null)touchRoot.SetActive(!modal&&Journey.Phase==GospelPhase.Exploring&&TouchInterface);
        }
        void ClearScreen()
        {
            if(screen!=null){screen.gameObject.SetActive(false);Destroy(screen.gameObject);}screen=ui.Fill(frame,"Gospel screen");
            Player?.StopInput();if(touchRoot!=null)touchRoot.SetActive(false);if(movePad!=null)movePad.ResetInput();if(lookPad!=null)lookPad.ResetInput();
            if(runButton!=null)runButton.GetComponentInChildren<TextMeshProUGUI>().text="달리기 OFF";
        }
        TextMeshProUGUI Text(Transform parent,string text,float x,float y,float w,float h,float size=24,Color? color=null)
        {var t=ui.Text(parent,text,x,y,w,h,size,color);t.overflowMode=TextOverflowModes.Overflow;return t;}
        Button Button(Transform parent,string label,float x,float y,float w,float h,UnityEngine.Events.UnityAction action,bool enabled=true)
        {var b=ui.Button(parent,label,x,y,w,h,action,enabled);b.GetComponentInChildren<TextMeshProUGUI>().fontSize=24;return b;}
        Button Primary(Transform parent,string label,float x,float y,float w,float h,UnityEngine.Events.UnityAction action)
        {var b=ui.Primary(parent,label,x,y,w,h,action);b.GetComponentInChildren<TextMeshProUGUI>().fontSize=24;return b;}
        void Heading(string eyebrow,string title,string subtitle)
        {
            Text(screen,eyebrow,44,30,1160,42,22,DawnUi.Gold).characterSpacing=3;
            Text(screen,title,44,80,1170,78,48);
            if(!string.IsNullOrEmpty(subtitle))Text(screen,subtitle,48,160,1150,90,24);
        }
        void ShowTitle()
        {
            inGame=false;modal=true;if(hud!=null){Destroy(hud.gameObject);hud=null;}if(touchRoot!=null)Destroy(touchRoot);
            World.SetOverview(false);ClearScreen();
            var veil=ui.Box(screen,"Title veil",0,0,720,720,new Color(.045f,.075f,.105f,.90f));
            Text(veil,"THE GOSPEL JOURNEY",48,52,620,40,22,DawnUi.Gold).characterSpacing=3;
            Text(veil,"말씀을 따라\n예수님의 길",44,124,650,162,60);
            Text(veil,"탄생에서 부활과 승천까지,\n복음서의 기록을 따라 걷는 여정",48,309,620,86,27);
            Primary(veil,"새 여정",48,430,576,64,StartNew);
            Button(veil,"이어하기",48,508,280,62,ContinueGame,PersistentSaves.Exists(activeSlot));
            Button(veil,"장 선택",344,508,280,62,ShowChapters);
            Button(veil,"설정",48,584,280,62,ShowSettings);
            Text(veil,"첫 여정 · 베들레헴의 탄생 / 성전의 소년",48,665,620,36,20,DawnUi.Gold);
            if(!string.IsNullOrEmpty(message))Toast(message);
        }
        public void StartNew()
        {
            // Each new journey has a separate slot; observer fiction and older Gospel journeys remain intact.
            activeSlot=PersistentSaves.Exists(GospelJourney.SaveSlot)?GospelJourney.SaveSlot+"-"+DateTime.UtcNow.Ticks:GospelJourney.SaveSlot;
            PlayerPrefs.SetString(ActiveSlotPreference,activeSlot);PlayerPrefs.Save();
            Journey=GospelJourney.New();Player.SetPose(new Vector3(0,.08f,-9),0);inGame=true;Save(false);RenderJourney();
        }
        public void ContinueGame()
        {
            if(!PersistentSaves.TryLoad(activeSlot,GospelJourney.GameId,GospelJourney.SchemaVersion,GospelJourney.ContentVersion,out var payload,out var error))
            {message="저장된 여정을 불러오지 못했습니다. "+error;ShowTitle();return;}
            try
            {
                var snapshot=JsonUtility.FromJson<GospelSave>(payload);Journey=GospelJourney.FromSave(snapshot);
                Player.SetPose(new Vector3(snapshot.playerX,snapshot.playerY,snapshot.playerZ),snapshot.playerYaw);inGame=true;RenderJourney();Toast("저장한 말씀과 위치에서 이어갑니다.");
            }
            catch(Exception exception){message="저장 내용을 확인해 주세요. 원본은 보존되었습니다. "+exception.Message;ShowTitle();}
        }
        public bool Save(bool showNotice=true)
        {
            if(Journey==null)return false;
            var pose=Player.transform.position;Journey.SetPlayerPose(pose.x,pose.y,pose.z,Player.transform.eulerAngles.y);
            bool saved=PersistentSaves.TrySave(activeSlot,GospelJourney.GameId,GospelJourney.SchemaVersion,GospelJourney.ContentVersion,JsonUtility.ToJson(Journey.CaptureSave()),out var error);
            if(showNotice||!saved)Toast(saved?"현재 위치와 읽은 말씀을 저장했습니다.":"저장하지 못했습니다. "+error);
            return saved;
        }
        void RenderJourney()
        {
            if(Journey.Phase==GospelPhase.Prologue){ShowReading(true);return;}
            World.BeginExploration();
            if(Journey.Phase==GospelPhase.Reading){BuildHud();ShowReading(false);return;}
            if(Journey.IsComplete){BuildHud();ShowCompletion();return;}
            BuildHud();ResumeJourney();
        }
        void BuildHud()
        {
            if(hud!=null)Destroy(hud.gameObject);hud=ui.Fill(frame,"Gospel exploration HUD");
            var top=ui.Box(hud,"Objective panel",24,20,740,118,new Color(.045f,.075f,.10f,.88f));
            Text(top,"02  성전의 소년  ·  누가복음 2:41–52",20,12,690,30,20,DawnUi.Gold);
            objective=Text(top,Journey.CurrentObjective?.Title??"첫 여정 완료",20,45,690,38,28);
            distance=Text(top,"",20,87,690,28,20);
            Button(hud,"성경 기록",790,24,148,54,ShowJournal);Button(hud,"저장",950,24,136,54,()=>Save());Button(hud,"설정",1098,24,158,54,ShowSettings);
            var scripture=ui.Box(hud,"Pinned Scripture",240,572,800,120,new Color(.045f,.075f,.10f,.88f));
            pinned=Text(scripture,Journey.CurrentReading.Reference+" · 개역한글",18,10,760,30,20,DawnUi.Gold);
            Text(scripture,FirstVerseLine(Journey.CurrentReading.Text),18,43,760,69,23);
            prompt=Text(hud,"",242,526,800,38,21,DawnUi.Gold);
            interactionButton=Button(hud,"대화 · E",1060,604,196,64,Interact,false);
            jumpButton=Button(hud,"점프 · Space",1060,530,196,58,()=>Player.JumpPressed());
            actionButton=Button(hud,"동작 연습 · F",1060,456,196,58,()=>{
                if(!Player.TryAction())Toast("뜰 남서쪽의 자유 연습 구역에서 사용할 수 있습니다.");
            },false);
            BuildTouch();
        }
        static string FirstVerseLine(string text){int newline=text.IndexOf('\n');return newline<0?text:text.Substring(0,newline);}
        void BuildTouch()
        {
            if(touchRoot!=null)Destroy(touchRoot);touchRoot=ui.Fill(frame,"Gospel touch controls").gameObject;
            var look=ui.Box(touchRoot.transform,"Camera drag",680,150,570,290);look.gameObject.AddComponent<Image>().color=new Color(0,0,0,.001f);lookPad=look.gameObject.AddComponent<DawnPointerPad>();
            var pad=ui.Card(touchRoot.transform,"Move pad",32,472,180,180,new Color(.09f,.15f,.18f,.70f));
            pad.pivot=new Vector2(.5f,.5f);pad.anchoredPosition=new Vector2(122,-562);movePad=pad.gameObject.AddComponent<DawnPointerPad>();movePad.joystick=true;movePad.radius=67;
            var thumb=ui.Card(pad,"Move thumb",0,0,72,72,DawnUi.Gold);thumb.anchorMin=thumb.anchorMax=thumb.pivot=new Vector2(.5f,.5f);thumb.anchoredPosition=Vector2.zero;thumb.GetComponent<Image>().raycastTarget=false;movePad.thumb=thumb;
            Text(pad,"이동",-30,100,80,38,20,DawnUi.Gold).raycastTarget=false;
            runButton=Button(touchRoot.transform,"달리기 OFF",32,386,180,64,()=>{
                Player.TouchRun=!Player.TouchRun;
                runButton.GetComponentInChildren<TextMeshProUGUI>().text=Player.TouchRun?"달리기 ON":"달리기 OFF";
            });
            touchRoot.SetActive(TouchInterface);
        }
        void ResumeJourney()
        {
            if(Journey==null){ShowTitle();return;}
            if(Journey.Phase==GospelPhase.Prologue||Journey.Phase==GospelPhase.Reading){ShowReading(Journey.Phase==GospelPhase.Prologue);return;}
            if(Journey.IsComplete){ShowCompletion();return;}
            ClearScreen();modal=false;Player.InputEnabled=true;Player.CameraFollow=true;World.SetCurrentLandmark(Journey.CurrentObjective.Id);
            if(hud==null)BuildHud();hud.gameObject.SetActive(true);
        }
        void ShowReading(bool birth)
        {
            modal=true;Player.InputEnabled=false;if(hud!=null)hud.gameObject.SetActive(false);ClearScreen();
            if(birth)World.SetOverview(true);
            var reading=Journey.CurrentReading;
            var turn=Journey.CurrentDialogue;
            ui.Card(screen,"Conversation heading shade",24,14,1232,122,new Color(.04f,.07f,.10f,.92f));
            Text(screen,birth?"01  BETHLEHEM · "+Journey.ProgressText:"02  JERUSALEM · "+Journey.ProgressText,40,26,1190,35,22,DawnUi.Gold).characterSpacing=2;
            Text(screen,reading.Title,36,70,1190,62,42);
            var dialogue=ui.Card(screen,"NPC conversation",36,150,1208,288,new Color(.055f,.09f,.12f,.97f));
            var portrait=ui.Art(dialogue,"Art/GospelRemaster/portraits",16,15,258,258,false);
            if(portrait!=null)portrait.uvRect=PortraitUv(turn.PortraitKey);
            Text(dialogue,turn.Speaker,294,20,854,43,30,DawnUi.Gold);
            Text(dialogue,turn.Label+"  ·  "+(Journey.DialogueIndex+1)+" / "+Journey.DialogueCount,294,69,854,32,20,DawnUi.Teal);
            ScrollText(dialogue,"대화",turn.Text,294,118,870,144,27);
            var card=ui.Card(screen,"Scripture reading",36,456,1208,228,new Color(.055f,.09f,.12f,.97f));
            Text(card,reading.Reference+"  /  "+reading.Translation,26,12,1148,38,25,DawnUi.Gold);
            Text(card,"성경 본문 · 스크롤",925,18,245,30,19,DawnUi.Teal);
            ScrollText(card,"성경 본문",reading.Text,26,58,1148,96,24);
            bool more=Journey.DialogueIndex+1<Journey.DialogueCount;
            var label=more?"다음 대화":birth?(Journey.ProloguePage<GospelCatalog.PrologueReadings.Count-1?"다음 구절":"성전으로"):"말씀을 따라 걷기";
            Primary(card,label,826,166,350,48,AdvanceReading);
            Text(card,GospelCatalog.Attribution,26,176,760,32,20,DawnUi.Gold);
        }
        static Rect PortraitUv(string key)
        {
            switch(key){case "mary":return new Rect(.5f,.5f,.5f,.5f);case "joseph":return new Rect(0,0,.5f,.5f);
                case "jesus":return new Rect(0,.5f,.5f,.5f);default:return new Rect(.5f,0,.5f,.5f);}
        }
        void ScrollText(Transform parent,string name,string value,float x,float y,float w,float h,float size)
        {
            var viewport=ui.Box(parent,name,x,y,w,h);viewport.gameObject.AddComponent<Image>().color=new Color(0,0,0,.002f);viewport.gameObject.AddComponent<RectMask2D>();
            var content=ui.Box(viewport,"본문",0,0,w,h);var text=Text(content,value,0,0,w,h,size);
            text.rectTransform.anchorMin=new Vector2(0,1);text.rectTransform.anchorMax=new Vector2(1,1);text.rectTransform.offsetMin=new Vector2(0,-Mathf.Max(h,text.GetPreferredValues(value,w,10000).y+8));text.rectTransform.offsetMax=Vector2.zero;
            content.sizeDelta=new Vector2(w,Mathf.Max(h,text.GetPreferredValues(value,w,10000).y+8));
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=24;
        }
        public void AdvanceReading()
        {
            if(!Journey.AdvanceDialogue())return;Save(false);RenderJourney();
        }
        public void Interact()
        {
            if(Journey==null||Journey.Phase!=GospelPhase.Exploring||modal)return;
            float meters=Vector3.Distance(Player.transform.position,World.LandmarkPosition(Journey.CurrentObjective.Id));
            if(meters>InteractionRadius){Toast("표식 가까이 걸어가 말씀을 읽어 보세요.");return;}
            if(Journey.TryInteract(Journey.CurrentObjective.Id,out var result)){Save(false);ShowReading(false);}else Toast(result);
        }
        void ShowJournal()
        {
            modal=true;Player.InputEnabled=false;ClearScreen();if(hud!=null)hud.gameObject.SetActive(false);
            Heading("SCRIPTURE JOURNAL","말씀 기록",Journey!=null?"지금까지 읽은 구절을 다시 살펴보세요.":"복음서의 기록을 따라 걷는 여정입니다.");
            if(Journey==null||Journey.CollectedReadings.Count==0)Text(screen,"아직 읽은 말씀이 없습니다.",48,264,1100,80);
            else
            {
                int count=Journey.CollectedReadings.Count;
                for(int i=0;i<count;i++)
                {
                    var reading=GospelCatalog.GetReading(Journey.CollectedReadings[i]);int index=i;
                    Button(screen,reading.Reference+" · "+reading.Title,48+(i%2)*598,252+(i/2)*79,574,64,()=>ShowJournalReading(Journey.CollectedReadings[index]));
                }
            }
            Button(screen,"돌아가기",952,640,280,58,()=>{if(inGame)ResumeJourney();else ShowTitle();});
        }
        void ShowJournalReading(string id)
        {
            ClearScreen();var reading=GospelCatalog.GetReading(id);Heading("SCRIPTURE JOURNAL",reading.Title,reading.Reference+" · "+reading.Translation);
            var panel=ui.Card(screen,"Journal reading",36,260,1208,360);
            ScrollText(panel,"성경 본문",reading.Text,24,22,1160,218,25);Text(panel,reading.Context,24,264,1160,76,22);
            Button(screen,"구절 모음",952,640,280,58,ShowJournal);
        }
        void ShowChapters()
        {
            modal=true;Player.InputEnabled=false;ClearScreen();if(hud!=null)hud.gameObject.SetActive(false);
            Heading("THE LIFE OF JESUS","복음서의 여정","탄생에서 공생애, 수난과 부활, 승천까지");
            string roadmap="";
            foreach(var chapter in GospelCatalog.Episodes)roadmap+=(chapter.IsPlayable?"● ":"○ ")+chapter.AgeLabel+" · "+chapter.Title+"  /  "+chapter.Reference+(chapter.IsPlayable?"":"  ·  준비 중")+"\n\n";
            var panel=ui.Card(screen,"Chapter catalogue",36,250,1208,360);ScrollText(panel,"장 목록",roadmap,24,18,1160,324,24);
            Button(screen,"돌아가기",48,642,280,58,()=>{if(inGame)ResumeJourney();else ShowTitle();});
            if(Journey!=null&&Journey.Phase!=GospelPhase.Prologue)Button(screen,"현재 여정으로",952,642,280,58,ResumeJourney);
        }
        void ShowCompletion()
        {
            modal=true;Player.InputEnabled=false;ClearScreen();if(hud!=null)hud.gameObject.SetActive(false);World.SetCurrentLandmark(null);
            Heading("THE FIRST JOURNEY","나사렛으로","누가복음 2:51–52 · 첫 여정을 마쳤습니다.");
            var panel=ui.Card(screen,"First journey completed",48,270,1184,290);
            Text(panel,"예수는 그 지혜와 그 키가 자라가며\n하나님과 사람에게 더 사랑스러워 가시더라",30,28,1124,114,34,DawnUi.Gold);
            Text(panel,"읽은 말씀은 기록에 남았습니다. 다음 여정에서는 세례와 공생애의 시작을 이어갑니다.",30,172,1124,74,24);
            Button(screen,"성경 기록",48,622,280,64,ShowJournal);Button(screen,"장 선택",348,622,280,64,ShowChapters);Button(screen,"저장",648,622,280,64,()=>Save());Button(screen,"처음 화면",948,622,280,64,()=>{Save(false);ShowTitle();});
        }
        void ShowSettings()
        {
            modal=true;if(Player!=null)Player.InputEnabled=false;ClearScreen();if(hud!=null)hud.gameObject.SetActive(false);
            Heading("SETTINGS","설정","WASD 이동 · Shift 달리기 · Space 점프 · F 동작 연습 · E 대화 · Esc 설정");
            var panel=ui.Card(screen,"Journey settings",48,270,1184,290);
            Button(panel,music.Muted?"음악 켜기":"음악 끄기",26,24,350,58,()=>{music.SetMuted(!music.Muted);ShowSettings();});
            Button(panel,"음량 -",400,24,260,58,()=>{music.SetVolume(music.Volume-.1f);ShowSettings();});Button(panel,"음량 +",682,24,260,58,()=>{music.SetVolume(music.Volume+.1f);ShowSettings();});
            Text(panel,"음량 "+Mathf.RoundToInt(music.Volume*100)+"%",970,32,180,42,24,DawnUi.Gold);
            Text(panel,"성경전서 개역한글판 · 대한성서공회. 인물의 이동과 성전 공간은 기록을 따라 읽기 위한 재현입니다.\n진행 상황은 현재 브라우저·기기에 저장됩니다.",26,118,1132,118,24);
            Button(screen,"돌아가기",48,624,370,64,()=>{if(inGame)ResumeJourney();else ShowTitle();});
            if(inGame)Button(screen,"처음 화면",862,624,370,64,()=>{Save(false);ShowTitle();});
        }
        void Toast(string value)
        {
            if(notice==null){var box=ui.Card(frame,"Gospel notice",250,148,780,88,new Color(.06f,.11f,.15f,.97f));box.GetComponent<Image>().raycastTarget=false;noticeRoot=box.gameObject;notice=Text(box,"",18,12,744,64,22,DawnUi.Gold);}
            notice.text=value;noticeRoot.SetActive(true);noticeUntil=Time.unscaledTime+5;
        }
        void OnApplicationPause(bool paused){if(paused&&inGame)Save(false);}
        void OnApplicationQuit(){if(inGame)Save(false);}
        void OnDestroy(){if(frame!=null&&frame.root!=null)Destroy(frame.root.gameObject);}
    }
}
