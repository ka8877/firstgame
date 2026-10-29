using System;
using System.Collections.Generic;
using PathOfDawn.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PathOfDawn.Presentation
{
    /// <summary>Lightweight, landscape-first interface for the painted Gospel journey.</summary>
    public sealed class GospelLuminousUi : MonoBehaviour
    {
        public Action OnStart, OnContinue, OnResume, OnPause, OnSave, OnInventory, OnJournal,
            OnStatus, OnSettings, OnExit, OnLampToggle, OnJump, OnInteract, OnRead, OnUserGesture, OnOpeningReplay;
        public Action<float> OnMove, OnMusicVolume;
        public Action<bool> OnRun, OnMusicMute;
        public bool IsModal { get; private set; }
        public string CurrentView { get; private set; } = "none";
        public Canvas CanvasView { get; private set; }
        public RectTransform Frame => frame;

        static readonly Color Ink = new Color(.035f, .085f, .115f, .91f);
        static readonly Color LightInk = new Color(.075f, .16f, .19f, .87f);
        static readonly Color Cream = new Color(.97f, .95f, .85f, 1);
        static readonly Color Quiet = new Color(.69f, .80f, .79f, 1);
        static readonly Color Gold = new Color(.94f, .80f, .48f, 1);
        static readonly Color Mint = new Color(.56f, .91f, .81f, 1);
        TMP_FontAsset font, display;
        RectTransform frame, modal, hud, touch, notification, pinnedScripture;
        TextMeshProUGUI objectiveText, referenceText, verseText, nearbyText, noticeText;
        Image[] progress;
        Button interact;
        Image runImage;
        bool leftHeld, rightHeld, running;
        float noticeUntil;
        static Sprite round;
        static Texture2D titleShade;

        public void Build(TMP_FontAsset koreanFont)
        {
            if (frame != null) return;
            font = koreanFont != null ? koreanFont : Resources.Load<TMP_FontAsset>("Fonts/Korean");
            display = Resources.Load<TMP_FontAsset>("Fonts/Display") ?? font;
            if (EventSystem.current == null)
                new GameObject("Luminous event system", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var input = EventSystem.current.GetComponent<InputSystemUIInputModule>();
            if (input == null) input = EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>();
            input.AssignDefaultActions();
            var go = new GameObject("Luminous canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false); go.layer = 5;
            CanvasView = go.GetComponent<Canvas>(); CanvasView.renderMode = RenderMode.ScreenSpaceOverlay;
            // Keep interface graphics above foreground sprites in both Overlay
            // and the supported camera-rendered native capture path.
            CanvasView.sortingOrder = 1000;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            frame = Rect(go.transform, "Luminous design frame", 0, 0, 1280, 720);
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(.5f, .5f); frame.anchoredPosition = Vector2.zero;
            notification = Panel(frame, "Saved journey notice", 362, 98, 556, 54, Ink);
            noticeText = Text(notification, "", 20, 11, 516, 34, 23, Cream, TextAlignmentOptions.Center);
            notification.gameObject.SetActive(false);
        }

        void Update()
        {
            if (notification != null) notification.gameObject.SetActive(Time.unscaledTime < noticeUntil);
        }
        void OnDisable() => ReleaseMovement();

        void Gesture() => OnUserGesture?.Invoke();
        void Invoke(Action action) { Gesture(); action?.Invoke(); }
        void ReleaseMovement()
        {
            leftHeld = rightHeld = false; OnMove?.Invoke(0);
            if (running) { running = false; OnRun?.Invoke(false); }
            if (runImage != null) runImage.color = LightInk;
        }
        void StartModal(string view, bool backdrop = true)
        {
            ReleaseMovement();
            if (modal != null) { modal.gameObject.SetActive(false); Destroy(modal.gameObject); }
            modal = Rect(frame, "Luminous " + view, 0, 0, 1280, 720);
            if (hud != null) hud.gameObject.SetActive(false);
            IsModal = true; CurrentView = view;
            if (backdrop)
            {
                var shade = Rect(modal, "Atmospheric menu veil", 0, 0, 1280, 720);
                shade.gameObject.AddComponent<Image>().color = new Color(.012f, .035f, .06f, .56f);
            }
            notification?.SetAsLastSibling();
        }
        public void CloseModal()
        {
            if (modal != null) { modal.gameObject.SetActive(false); Destroy(modal.gameObject); modal = null; }
            IsModal = false; CurrentView = "journey";
            if (hud != null) hud.gameObject.SetActive(true);
            notification?.SetAsLastSibling();
        }

        public void ShowTitle(bool canContinue)
        {
            StartModal("title", false);
            StoryArt(modal,"Art/GospelRenewal/Opening/temple-journey");
            var veil = Rect(modal, "Title soft shade", 0, 0, 666, 720);
            var gradient = veil.gameObject.AddComponent<RawImage>(); gradient.texture = TitleGradient(); gradient.raycastTarget = false;
            Ornament(modal, 72, 126, 342, Gold);
            var eyebrow = Text(modal, "THE GOSPEL JOURNEY", 74, 158, 520, 36, 21, Gold); eyebrow.characterSpacing = 5;
            var first = Text(modal, "빛을 따라,", 68, 214, 570, 82, 64, Cream);
            var second = Text(modal, "말씀을 따라", 68, 292, 570, 82, 64, Cream);
            first.textWrappingMode = second.textWrappingMode = TextWrappingModes.NoWrap;
            first.overflowMode = second.overflowMode = TextOverflowModes.Overflow;
            Text(modal, "예수님의 길", 74, 391, 550, 58, 38, Cream);
            Text(modal, "탄생의 기억에서 열두 살의 성전으로.\n사람을 만나고, 말씀을 모으는 첫 여정.", 76, 462, 542, 76, 25, Quiet);
            Button(modal, "새 여정 시작", "play", 74, 566, 292, 66, () => OnStart?.Invoke(), true, true);
            Button(modal, "이어하기", "leaf", 380, 566, 208, 66, () => OnContinue?.Invoke(), canContinue);
            Button(modal, "설정", "settings", 74, 651, 142, 48, () => OnSettings?.Invoke());
            Text(modal, "첫 장 · 열두 살의 예수님 · 말씀 도감 15장", 224, 662, 605, 30, 21, Quiet);
            var tag = Panel(modal, "Place label", 939, 61, 281, 68, new Color(.025f, .09f, .12f, .67f));
            Text(tag, "JERUSALEM", 18, 9, 242, 24, 20, Gold).characterSpacing = 4;
            Text(tag, "예루살렘 · 올리브 뜰", 18, 34, 242, 27, 21, Cream);
        }

        public void ShowHud(string objective, string reference, string verse, int read, int total, bool useTouch)
        {
            if (hud == null) BuildHud();
            CloseModal(); touch.gameObject.SetActive(useTouch);
            UpdateHud(objective, reference, verse, read, total, "", false);
        }
        public void UpdateHud(string objective, string reference, string verse, int read, int total, string nearby, bool canInteract)
        {
            if (hud == null) return;
            objectiveText.text = objective ?? "말씀을 따라 걷기";
            referenceText.text = reference ?? "";
            verseText.text = (verse ?? "").Replace('\n', ' ');
            pinnedScripture.gameObject.SetActive(!string.IsNullOrEmpty(reference) || !string.IsNullOrEmpty(verse));
            nearbyText.text = string.IsNullOrEmpty(nearby) ? "" : "E  ·  " + nearby;
            interact.interactable = canInteract;
            int count = Mathf.Clamp(total, 0, 15);
            for (int i = 0; i < progress.Length; i++)
            {
                progress[i].gameObject.SetActive(i < count);
                progress[i].color = i < read ? Mint : new Color(.68f, .79f, .78f, .28f);
            }
        }
        void BuildHud()
        {
            hud = Rect(frame, "Luminous exploration HUD", 0, 0, 1280, 720);
            var identity = Button(hud, "예수님", "leaf", 30, 26, 171, 56, () => OnStatus?.Invoke());
            identity.GetComponent<Image>().color = new Color(.018f, .07f, .09f, .68f);
            Button(hud, "도감", "book", 216, 26, 157, 56, () => OnRead?.Invoke());
            var aim = Panel(hud, "Current Scripture objective", 405, 24, 470, 62, new Color(.025f, .075f, .09f, .70f));
            Ornament(aim, 16, 9, 438, new Color(Gold.r, Gold.g, Gold.b, .44f));
            objectiveText = Text(aim, "말씀을 따라 걷기", 20, 25, 430, 31, 23, Cream, TextAlignmentOptions.Center);
            Button(hud, "", "book", 1020, 26, 58, 56, () => OnJournal?.Invoke());
            Button(hud, "", "bag", 1090, 26, 58, 56, () => OnInventory?.Invoke());
            Button(hud, "", "pause", 1160, 26, 58, 56, () => OnPause?.Invoke());
            progress = new Image[15];
            for (int i = 0; i < progress.Length; i++)
            {
                var pip = Rect(hud, "Read Scripture light " + (i + 1), 34 + i * 15, 98, 9, 9);
                pip.localRotation = Quaternion.Euler(0, 0, 45); var image = pip.gameObject.AddComponent<Image>();
                image.raycastTarget = false; progress[i] = image;
            }
            nearbyText = Text(hud, "", 384, 559, 512, 37, 23, Cream, TextAlignmentOptions.Center);
            nearbyText.gameObject.AddComponent<Shadow>().effectColor = new Color(.01f, .035f, .05f, .9f);
            var verse = Panel(hud, "Quiet pinned Scripture", 290, 622, 700, 78, new Color(.025f, .075f, .09f, .76f));
            pinnedScripture = verse;
            referenceText = Text(verse, "", 20, 7, 660, 28, 20, Gold);
            verseText = Text(verse, "", 20, 36, 660, 32, 23, Cream); verseText.maxVisibleLines = 1;
            touch = Rect(hud, "Luminous touch controls", 0, 0, 1280, 720);
            Hold(touch, "왼쪽", "left", 28, 599, 78, 78, v => { leftHeld = v; OnMove?.Invoke((rightHeld ? 1 : 0) - (leftHeld ? 1 : 0)); });
            Hold(touch, "오른쪽", "right", 118, 599, 78, 78, v => { rightHeld = v; OnMove?.Invoke((rightHeld ? 1 : 0) - (leftHeld ? 1 : 0)); });
            var run = Button(touch, "", "run", 208, 615, 58, 58, () =>
            {
                running = !running; runImage.color = running ? new Color(.30f, .57f, .47f, .94f) : LightInk; OnRun?.Invoke(running);
            }); runImage = run.GetComponent<Image>();
            Button(touch, "", "jump", 1022, 599, 78, 78, () => OnJump?.Invoke());
            interact = Button(hud, "대화", "spark", 1116, 606, 122, 68, () => OnInteract?.Invoke(), false);
        }

        void StoryArt(Transform parent,string key)
        {
            var texture=Resources.Load<Texture2D>(key);if(texture==null)return;
            var image=Rect(parent,"Illustrated story painting",0,0,1280,720).gameObject.AddComponent<RawImage>();
            image.texture=texture;image.raycastTarget=false;
            float imageAspect=(float)texture.width/texture.height,targetAspect=1280f/720f;
            image.uvRect=imageAspect>targetAspect?new Rect((1-targetAspect/imageAspect)/2,0,targetAspect/imageAspect,1)
                :new Rect(0,(1-imageAspect/targetAspect)/2,1,imageAspect/targetAspect);
        }
        public void ShowOpening(GospelOpeningPage page,int index,int total,Action next,Action skip)
        {
            StartModal("opening",false);
            StoryArt(modal,index<3?"Art/GospelRenewal/Opening/nativity":"Art/GospelRenewal/Opening/temple-journey");
            var veil=Rect(modal,"Opening reading shade",0,0,810,720).gameObject.AddComponent<RawImage>();
            veil.texture=TitleGradient();veil.raycastTarget=false;
            Ornament(modal,58,70,530,Gold);
            Text(modal,"PROLOGUE · "+(index+1)+" / "+total,60,95,540,34,22,Gold).characterSpacing=3;
            Text(modal,page.Title,58,151,650,75,47,Cream);
            Text(modal,page.Subtitle,62,241,620,42,29,Gold);
            ScrollText(modal,page.Body,62,305,590,180,28,Cream);
            Text(modal,page.Reference+" · 학습용 요약",62,506,640,45,21,Quiet);
            Button(modal,index+1==total?"열두 살의 여정 시작":"이야기 계속", "right",60,585,345,70,next,true,true);
            Button(modal,"도입 건너뛰기","play",428,594,258,60,skip);
            Text(modal,"도입은 메뉴에서 다시 볼 수 있습니다. 말씀은 만남을 통해 도감에 모입니다.",60,675,1100,28,20,Quiet);
        }
        void Portrait(Transform parent,string portrait,float x,float y,float width,float height)
        {
            var panel=Panel(parent,"Speaker portrait",x,y,width,height,new Color(.13f,.24f,.24f,.55f));
            var texture=Resources.Load<Texture2D>("Art/GospelLuminous/Actors/npcs");
            if(texture==null)return;
            int variant=portrait=="mary"?1:portrait=="joseph"?2:0;
            var source=GospelLuminousActor.NpcRects[variant];
            // Face and shoulders from the unchanged original NPC atlas.
            float cropHeight=source.width*height/width;
            var art=Rect(panel,"Painted speaker",0,0,width,height).gameObject.AddComponent<RawImage>();
            art.texture=texture;art.uvRect=new Rect(source.x/texture.width,(source.yMax-cropHeight)/texture.height,source.width/texture.width,cropHeight/texture.height);art.raycastTarget=false;
            if(portrait=="jesus")
            {
                // A portrait of the child protagonist is cropped from the new title illustration;
                // do not label the teacher atlas tile as Jesus.
                art.texture=Resources.Load<Texture2D>("Art/GospelRenewal/Opening/temple-journey");
                art.uvRect=new Rect(.728f,.45f,.098f,.23f);
            }
        }
        public void ShowReading(string heading,string speaker,string dialogue,string reference,string verses,string progressLabel,Action next,string portrait="teacher",Action question=null)
        {
            StartModal("reading",false);
            var top=Panel(modal,"Reading chapter label",34,26,800,57,new Color(.025f,.075f,.09f,.74f));
            Text(top,heading,21,10,750,36,25,Cream);
            var box=Panel(modal,"Story and Scripture",42,286,1196,412,new Color(.022f,.068f,.09f,.96f));
            Portrait(box,portrait,24,28,164,216);
            Text(box,speaker,216,23,936,34,25,Gold);
            ScrollText(box,dialogue,216,76,936,126,27,Cream);
            Ornament(box,216,218,936,new Color(.80f,.83f,.70f,.35f));
            Text(box,reference+" · 개역한글 · 위아래로 읽기",216,236,936,30,22,Gold);
            ScrollText(box,verses,216,278,684,105,23,Cream);
            Text(box,progressLabel,918,280,250,41,19,Quiet,TextAlignmentOptions.Center);
            Button(box,"다음 말씀","right",918,331,250,60,next,true,true);
            Text(box,"성경 본문\n위아래로 스크롤",24,268,170,65,18,Quiet);
            if(question!=null)Button(box,"질문하기","spark",24,346,168,46,question);
        }
        public void ShowStudyQuestions(GospelStudyEncounter encounter,Action back)
        {
            var box=Menu("study-question",encounter.Reading.Title+" · 더 이야기하기","궁금한 것을 물어보고 대화로 돌아갈 수 있습니다.");
            var options=GospelStudyQuestions.For(encounter.Reading.Id);
            var answer=Text(box,"궁금한 내용을 골라보세요.",48,330,1060,130,26,Cream);
            answer.overflowMode=TextOverflowModes.Overflow;
            Button(box,options[0],"book",36,184,1106,55,()=>answer.text=options[1]);
            Button(box,options[2],"spark",36,252,1106,55,()=>answer.text=options[3]);
            Button(box,"대화로 돌아가기","back",844,484,298,58,back,true,true);
        }
        public void ShowCodex(IReadOnlyList<GospelReading> readings,Func<string,bool> acquired,int count,int page=0,string selected=null)
        {
            var box=Menu("journal","말씀 도감", "획득 "+count+" / "+readings.Count+" · 만남의 대화를 끝까지 읽으면 한 장씩 기록됩니다.");
            const int perPage=5;int pageCount=(readings.Count+perPage-1)/perPage;page=Mathf.Clamp(page,0,pageCount-1);
            var chosen=readings[Mathf.Clamp(page*perPage,0,readings.Count-1)];
            if(selected!=null)foreach(var r in readings)if(r.Id==selected)chosen=r;
            for(int i=page*perPage;i<Math.Min(readings.Count,(page+1)*perPage);i++)
            {
                var reading=readings[i];bool open=acquired(reading.Id);
                var button=Button(box,(open?"✓ ":"◇ ")+reading.Title,"",36,176+(i%perPage)*53,379,47,()=>ShowCodex(readings,acquired,count,page,reading.Id));
                if(chosen.Id==reading.Id)button.GetComponent<Image>().color=new Color(.24f,.40f,.36f,.95f);
            }
            var detail=Panel(box,"Selected Scripture page",444,173,698,286,new Color(.10f,.20f,.22f,.8f));
            bool unlocked=acquired(chosen.Id);
            Text(detail,chosen.Title,24,18,650,40,29,Gold);
            Text(detail,unlocked?chosen.Reference+" · 개역한글":"아직 만나지 않은 말씀",24,65,650,31,22,Quiet);
            ScrollText(detail,unlocked?chosen.Text:"이 말씀은 아직 도감에 기록되지 않았습니다.\n\n빛나는 만남으로 걸어가 대화를 끝까지 읽어 보세요. 미리 보기만으로 말씀을 획득하지는 않습니다.",24,112,650,151,25,Cream);
            Button(box,"이전","left",36,484,142,58,()=>ShowCodex(readings,acquired,count,page-1),page>0);
            Text(box,(page+1)+" / "+pageCount,188,496,75,35,24,Quiet,TextAlignmentOptions.Center);
            Button(box,"다음","right",272,484,143,58,()=>ShowCodex(readings,acquired,count,page+1),page+1<pageCount);
            Button(box,"여정으로","right",868,484,274,58,()=>OnResume?.Invoke(),true,true);
        }

        RectTransform Menu(string view, string title, string subtitle)
        {
            StartModal(view);
            var box = Panel(modal, "Luminous " + view + " panel", 50, 77, 1180, 574, Ink);
            Ornament(box, 36, 25, 1108, new Color(Gold.r, Gold.g, Gold.b, .46f));
            Text(box, title, 34, 47, 1096, 59, 40, Cream);
            Text(box, subtitle, 36, 110, 1100, 42, 23, Quiet);
            return box;
        }
        public void ShowPause()
        {
            var box = Menu("pause", "여정의 쉼표", "숲의 빛을 바라보며, 다음 말씀을 준비하세요.");
            Button(box, "여정 계속하기", "play", 36, 197, 530, 74, () => OnResume?.Invoke(), true, true);
            Button(box, "현재 여정 저장", "leaf", 36, 288, 530, 66, () => OnSave?.Invoke());
            Button(box, "소지품", "bag", 610, 197, 530, 74, () => OnInventory?.Invoke());
            Button(box, "말씀 기록", "book", 610, 288, 530, 66, () => OnJournal?.Invoke());
            Button(box, "소리 설정", "settings", 36, 397, 530, 66, () => OnSettings?.Invoke());
            Button(box, "처음 화면", "back", 610, 397, 530, 66, () => OnExit?.Invoke());
            Button(box,"도입 다시보기","book",36,484,295,54,()=>OnOpeningReplay?.Invoke());
            Text(box,"A / D 이동 · Shift 달리기 · Space 점프 · E 대화",356,497,780,35,21,Quiet,TextAlignmentOptions.Center);
        }
        public void ShowInventory(bool lamp)
        {
            var box = Menu("inventory", "여행 가방", "소지품과 장착 연출은 학습용 게임 설정입니다.");
            string[] titles = { "아마포 튜닉", "가죽 샌들", "여행 물주머니", "토기 등잔", "말씀 수첩", "성전의 뜰 안내도" };
            string[] icons = { "tunic", "sandals", "water", "lamp", "book", "map" };
            string[] descriptions = {
                "바람과 햇빛을 받아 부드럽게 움직이는 아마포 옷입니다.\n\n학습 여정을 위한 인물 디자인이며, 성경의 복장 고증을 단정하지 않습니다.",
                "돌길과 흙길을 걷는 가죽 신발입니다.\n\n현재 이동과 달리기·점프는 별도의 조작으로 사용할 수 있습니다.",
                "여행에 가져가는 물주머니입니다.\n\n소지품 설명을 위한 소품이며 생존 수치나 소모 재화를 만들지 않습니다.",
                "작은 불빛이 손과 발치의 길을 따뜻하게 비춥니다.\n\n장착 여부는 현재 여정과 함께 저장됩니다.",
                "여정에서 만난 말씀을 모아 읽습니다.\n\n직접 인용한 본문과 학습용 해설을 구분해 살펴보세요.",
                "올리브 뜰을 지나 성전으로 이어지는 길입니다.\n\n실제 고대 예루살렘을 측량한 지도가 아닌 게임 공간 안내입니다."
            };
            var detail = Panel(box, "Selected travel item", 641, 178, 501, 281, new Color(.08f, .17f, .19f, .71f));
            var heading = Text(detail, "", 28, 22, 445, 40, 29, Gold);
            var body = Text(detail, "", 28, 77, 445, 179, 23, Cream);
            body.overflowMode=TextOverflowModes.Overflow;
            Button lampButton = null, notesButton = null;
            Action<int> select = i =>
            {
                heading.text = titles[i] + (i == 3 && lamp ? " · 장착 중" : ""); body.text = descriptions[i];
                if (lampButton != null) lampButton.gameObject.SetActive(i == 3);
                if (notesButton != null) notesButton.gameObject.SetActive(i == 4);
            };
            for (int i = 0; i < titles.Length; i++)
            {
                int index = i; var b = Button(box, "", "", 36 + i % 3 * 194, 178 + i / 3 * 145, 178, 130, () => select(index));
                var atlas=Resources.Load<Texture2D>("Art/GospelRenewal/Opening/travel-items");
                if(atlas!=null){var art=Rect(b.transform,"Painted item "+titles[i],49,0,80,80).gameObject.AddComponent<RawImage>();art.texture=atlas;art.uvRect=new Rect((i%3)/3f,1-(i/3+1)/2f,1/3f,.5f);art.raycastTarget=false;}
                else Glyph(b.transform, icons[i], 66, 15, 46, 46, Gold);
                Text(b.transform, i==5?"성전 안내도":titles[i], 8, 86, 162, 33, 20, Cream, TextAlignmentOptions.Center);
            }
            lampButton = Button(box, lamp ? "등잔 보관" : "등잔 장착", "lamp", 642, 484, 226, 58, () => OnLampToggle?.Invoke());
            notesButton = Button(box, "말씀 기록 열기", "book", 642, 484, 226, 58, () => OnJournal?.Invoke());
            Button(box, "여정으로", "right", 896, 484, 246, 58, () => OnResume?.Invoke(), true, true);
            select(3);
        }
        public void ShowJournal(string body)
        {
            var box = Menu("journal", "말씀의 발자취", "지금까지 읽은 본문과 현재 여정의 기록입니다.");
            ScrollText(box, body, 36, 178, 1104, 278, 26, Cream);
            Button(box, "여정으로", "right", 868, 484, 274, 58, () => OnResume?.Invoke(), true, true);
        }
        public void ShowStatus(string body)
        {
            var box = Menu("status", "예수님 · 성전의 소년", "현재 여정에서 확인한 말씀과 소지품을 살펴봅니다.");
            ScrollText(box, body, 36, 178, 1104, 278, 27, Cream);
            Button(box, "소지품", "bag", 36, 484, 248, 58, () => OnInventory?.Invoke());
            Button(box, "말씀 기록", "book", 302, 484, 248, 58, () => OnJournal?.Invoke());
            Button(box, "여정으로", "right", 868, 484, 274, 58, () => OnResume?.Invoke(), true, true);
        }
        public void ShowSettings(float volume, bool muted)
        {
            var box = Menu("settings", "숲과 여정의 소리", "음악은 첫 실제 입력 뒤 재생됩니다.");
            Text(box, "음악 음량", 36, 198, 220, 42, 28, Cream);
            var value = Text(box, Mathf.RoundToInt(Mathf.Clamp01(volume) * 100) + "%", 924, 198, 216, 42, 28, Gold, TextAlignmentOptions.Right);
            var slider = Slider(box, 36, 270, 1104, Mathf.Clamp01(volume));
            slider.onValueChanged.AddListener(v => { Gesture(); value.text = Mathf.RoundToInt(v * 100) + "%"; OnMusicVolume?.Invoke(v); });
            var muteButton = Button(box, muted ? "음악 켜기" : "음악 끄기", "settings", 36, 359, 530, 66, null);
            muteButton.onClick.AddListener(() => { muted = !muted; muteButton.GetComponentInChildren<TextMeshProUGUI>().text = muted ? "음악 켜기" : "음악 끄기"; OnMusicMute?.Invoke(muted); });
            Button(box, "돌아가기", "back", 866, 484, 276, 58, () => OnResume?.Invoke(), true, true);
        }
        public void Toast(string message)
        {
            if (notification == null) return;
            noticeText.text = message; noticeUntil = Time.unscaledTime + 3.5f; notification.SetAsLastSibling(); notification.gameObject.SetActive(true);
        }

        RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); return rect;
        }
        RectTransform Panel(Transform parent, string name, float x, float y, float width, float height, Color color)
        {
            var r = Rect(parent, name, x, y, width, height); var image = r.gameObject.AddComponent<Image>();
            image.sprite = Rounded(); image.type = Image.Type.Sliced; image.color = color; image.raycastTarget = false;
            return r;
        }
        TextMeshProUGUI Text(Transform parent, string value, float x, float y, float width, float height, float size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            var t = Rect(parent, "Text " + value, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            t.font = size >= 34 ? display : font; t.text = value ?? ""; t.fontSize = size; t.color = color;
            t.alignment = alignment; t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis; t.lineSpacing = 4; return t;
        }
        Button Button(Transform parent, string label, string icon, float x, float y, float width, float height, Action action, bool enabled = true, bool primary = false)
        {
            var rect = Panel(parent, string.IsNullOrEmpty(label) ? "Luminous " + icon : label, x, y, width, height, primary ? Cream : LightInk);
            var image = rect.GetComponent<Image>(); image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.interactable = enabled;
            var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(.78f, 1, .95f); colors.pressedColor = new Color(.54f, .85f, .78f);
            colors.disabledColor = new Color(.67f, .72f, .71f, .62f); colors.fadeDuration = .12f; button.colors = colors;
            if (!string.IsNullOrEmpty(icon)) Glyph(rect, icon, string.IsNullOrEmpty(label) ? (width - 29) / 2 : 17, (height - 29) / 2, 29, 29, primary ? Ink : Gold);
            if (!string.IsNullOrEmpty(label))
            {
                float inset = string.IsNullOrEmpty(icon) ? 18 : 59;
                var text = Text(rect, label, inset, 0, width - inset - 15, height, 24, primary ? Ink : Cream, TextAlignmentOptions.MidlineLeft);
                text.textWrappingMode = TextWrappingModes.NoWrap;
            }
            button.onClick.AddListener(() => Invoke(action)); return button;
        }
        void Hold(Transform parent, string label, string icon, float x, float y, float width, float height, Action<bool> held)
        {
            var button = Button(parent, "", icon, x, y, width, height, null);
            button.gameObject.name = "Hold " + label;
            var relay = button.gameObject.AddComponent<GospelLuminousHold>();
            relay.Changed = v => { if (v) Gesture(); held(v); };
        }
        void Ornament(Transform parent, float x, float y, float width, Color color)
        {
            var line = Rect(parent, "Quiet gold line", x + 10, y + 4, width - 20, 1); line.gameObject.AddComponent<Image>().color = color;
            line.GetComponent<Image>().raycastTarget = false;
            foreach (float end in new[] { x + 4, x + width - 4 })
            {
                var mark = Rect(parent, "Light diamond", end, y + 4, 6, 6); mark.pivot = new Vector2(.5f, .5f); mark.localRotation = Quaternion.Euler(0, 0, 45);
                var image = mark.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
            }
        }
        void ScrollText(Transform parent, string value, float x, float y, float width, float height, float size, Color color)
        {
            var holder = Rect(parent, "Scripture scroll", x, y, width, height); var scroll = holder.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect(holder, "Scripture viewport", 0, 0, width, height); viewport.gameObject.AddComponent<RectMask2D>();
            var background = viewport.gameObject.AddComponent<Image>(); background.color = new Color(1, 1, 1, .001f);
            var content = Rect(viewport, "Scripture content", 0, 0, width - 18, height);
            var t = Text(content, value, 0, 0, width - 22, height, size, color); t.overflowMode = TextOverflowModes.Overflow;
            var fitter = t.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            float wanted = Mathf.Max(height, t.GetPreferredValues(value, width - 22, 0).y + 10);
            content.sizeDelta = new Vector2(width - 18, wanted); t.rectTransform.sizeDelta = new Vector2(width - 22, wanted);
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 28;
            var track = Panel(holder, "Scripture scroll track", width - 5, 0, 3, height, new Color(.7f, .85f, .8f, .16f));
            track.GetComponent<Image>().raycastTarget = false;
        }
        Slider Slider(Transform parent, float x, float y, float width, float current)
        {
            var rect = Rect(parent, "Music volume slider", x, y, width, 48);
            // The entire 48px strip accepts taps and drags, not just its small handle.
            var hit = rect.gameObject.AddComponent<Image>(); hit.color = new Color(1, 1, 1, .001f);
            var slider = rect.gameObject.AddComponent<Slider>();
            Panel(rect, "Volume background", 0, 20, width, 8, new Color(.4f, .57f, .57f, .35f));
            var fillArea = Rect(rect, "Volume fill area", 0, 20, width - 24, 8);
            var fill = Panel(fillArea, "Volume fill", 0, 0, width - 24, 8, Mint);
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = fill.offsetMax = Vector2.zero;
            var handles = Rect(rect, "Volume handle area", 12, 0, width - 24, 48);
            var handle = Panel(handles, "Volume handle", 0, 10, 28, 28, Cream); handle.pivot = new Vector2(.5f, .5f);
            handle.anchorMin = Vector2.zero; handle.anchorMax = new Vector2(0, 1);
            handle.sizeDelta = new Vector2(28, -20); handle.anchoredPosition = Vector2.zero;
            handle.GetComponent<Image>().raycastTarget = true;
            slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = 0; slider.maxValue = 1; slider.SetValueWithoutNotify(current); return slider;
        }
        void Glyph(Transform parent, string kind, float x, float y, float width, float height, Color color)
        {
            var r = Rect(parent, "Luminous glyph " + kind, x, y, width, height);
            var graphic = r.gameObject.AddComponent<GospelLuminousGlyph>(); graphic.Kind = kind; graphic.color = color; graphic.raycastTarget = false;
        }
        static Sprite Rounded()
        {
            if (round != null) return round;
            const int n = 64; var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Luminous UI round", hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Max(0, Mathf.Abs(x - 31.5f) - 15.5f), dy = Mathf.Max(0, Mathf.Abs(y - 31.5f) - 15.5f);
                pixels[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(16 - Mathf.Sqrt(dx * dx + dy * dy)));
            }
            texture.SetPixels(pixels); texture.Apply(false, true);
            round = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(18, 18, 18, 18));
            round.hideFlags = HideFlags.HideAndDontSave; return round;
        }
        static Texture2D TitleGradient()
        {
            if (titleShade != null) return titleShade;
            var texture = new Texture2D(128, 1, TextureFormat.RGBA32, false) { name = "Luminous title gradient", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[128]; for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(.01f, .04f, .065f, .86f * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.55f, 1, i / 127f))));
            texture.SetPixels(pixels); texture.Apply(false, true); titleShade = texture; return texture;
        }
    }

    public sealed class GospelLuminousHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, ICancelHandler
    {
        public Action<bool> Changed;
        int? pointer;
        public void OnPointerDown(PointerEventData e) { if (pointer.HasValue || e.button != PointerEventData.InputButton.Left) return; pointer = e.pointerId; Changed?.Invoke(true); }
        public void OnPointerUp(PointerEventData e) { if (pointer != e.pointerId) return; pointer = null; Changed?.Invoke(false); }
        public void OnCancel(BaseEventData e) { pointer = null; Changed?.Invoke(false); }
        void OnDisable() { if (!pointer.HasValue) return; pointer = null; Changed?.Invoke(false); }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GospelLuminousGlyph : MaskableGraphic
    {
        public string Kind;
        public GospelLuminousGlyph() { useLegacyMeshGeneration = false; }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect;
            Vector2 P(float x, float y) => new Vector2(r.x + x * r.width, r.y + y * r.height);
            void Tri(Vector2 a, Vector2 b, Vector2 c) { int i = vh.currentVertCount; vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero); vh.AddVert(c, color, Vector2.zero); vh.AddTriangle(i, i + 1, i + 2); }
            void Line(float ax, float ay, float bx, float by, float thick = .065f)
            {
                var a = P(ax, ay); var b = P(bx, by); var n = new Vector2(-(b - a).y, (b - a).x).normalized * r.width * thick * .5f;
                Tri(a + n, b + n, b - n); Tri(a + n, b - n, a - n);
            }
            void Poly(params Vector2[] points) { for (int i = 1; i < points.Length; i++) Line(points[i - 1].x, points[i - 1].y, points[i].x, points[i].y); }
            void Circle(float cx, float cy, float radius) { for (int i = 0; i < 20; i++) { float a = i * Mathf.PI * .1f, b = (i + 1) * Mathf.PI * .1f; Line(cx + Mathf.Cos(a) * radius, cy + Mathf.Sin(a) * radius, cx + Mathf.Cos(b) * radius, cy + Mathf.Sin(b) * radius, .055f); } }
            switch (Kind)
            {
                case "play": Tri(P(.30f, .18f), P(.78f, .50f), P(.30f, .82f)); break;
                case "left": case "back": Poly(new Vector2(.64f, .18f), new Vector2(.30f, .5f), new Vector2(.64f, .82f)); break;
                case "right": Poly(new Vector2(.36f, .18f), new Vector2(.70f, .5f), new Vector2(.36f, .82f)); break;
                case "pause": Line(.34f, .20f, .34f, .80f, .13f); Line(.67f, .20f, .67f, .80f, .13f); break;
                case "book": Poly(new Vector2(.14f, .22f), new Vector2(.14f, .80f), new Vector2(.48f, .73f), new Vector2(.84f, .80f), new Vector2(.84f, .22f), new Vector2(.48f, .15f), new Vector2(.14f, .22f)); Line(.48f, .16f, .48f, .73f); break;
                case "bag": Poly(new Vector2(.20f, .18f), new Vector2(.14f, .68f), new Vector2(.86f, .68f), new Vector2(.80f, .18f), new Vector2(.20f, .18f)); Poly(new Vector2(.34f, .68f), new Vector2(.34f, .88f), new Vector2(.66f, .88f), new Vector2(.66f, .68f)); break;
                case "leaf": Poly(new Vector2(.20f, .18f), new Vector2(.22f, .65f), new Vector2(.56f, .86f), new Vector2(.82f, .82f), new Vector2(.78f, .42f), new Vector2(.44f, .18f), new Vector2(.20f, .18f)); Line(.22f, .18f, .69f, .68f); break;
                case "settings": Circle(.5f, .5f, .19f); Circle(.5f, .5f, .36f); for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4; Line(.5f + Mathf.Cos(a) * .30f, .5f + Mathf.Sin(a) * .30f, .5f + Mathf.Cos(a) * .46f, .5f + Mathf.Sin(a) * .46f); } break;
                case "jump": Poly(new Vector2(.22f, .56f), new Vector2(.5f, .84f), new Vector2(.78f, .56f)); Line(.5f, .20f, .5f, .82f); Line(.20f, .14f, .80f, .14f); break;
                case "run": Circle(.63f, .81f, .10f); Poly(new Vector2(.24f, .56f), new Vector2(.45f, .64f), new Vector2(.66f, .48f), new Vector2(.85f, .53f)); Poly(new Vector2(.45f, .64f), new Vector2(.40f, .36f), new Vector2(.19f, .18f)); Poly(new Vector2(.40f, .36f), new Vector2(.66f, .30f), new Vector2(.73f, .11f)); break;
                case "tunic": Poly(new Vector2(.15f, .65f), new Vector2(.30f, .88f), new Vector2(.43f, .79f), new Vector2(.57f, .79f), new Vector2(.70f, .88f), new Vector2(.85f, .65f), new Vector2(.70f, .52f), new Vector2(.66f, .70f), new Vector2(.72f, .15f), new Vector2(.28f, .15f), new Vector2(.34f, .70f), new Vector2(.30f, .52f), new Vector2(.15f, .65f)); break;
                case "sandals": Circle(.32f, .60f, .22f); Circle(.71f, .37f, .22f); Line(.15f, .55f, .48f, .65f); Line(.55f, .32f, .86f, .42f); break;
                case "water": Poly(new Vector2(.39f, .87f), new Vector2(.61f, .87f), new Vector2(.61f, .70f), new Vector2(.80f, .48f), new Vector2(.73f, .18f), new Vector2(.27f, .18f), new Vector2(.20f, .48f), new Vector2(.39f, .70f), new Vector2(.39f, .87f)); break;
                case "lamp": Poly(new Vector2(.16f, .36f), new Vector2(.30f, .16f), new Vector2(.66f, .16f), new Vector2(.85f, .48f), new Vector2(.35f, .42f), new Vector2(.16f, .36f)); Poly(new Vector2(.69f, .57f), new Vector2(.77f, .88f), new Vector2(.88f, .65f)); break;
                case "map": Poly(new Vector2(.13f, .20f), new Vector2(.13f, .80f), new Vector2(.38f, .70f), new Vector2(.63f, .83f), new Vector2(.87f, .72f), new Vector2(.87f, .15f), new Vector2(.63f, .26f), new Vector2(.38f, .14f), new Vector2(.13f, .20f)); Line(.38f, .14f, .38f, .70f); Line(.63f, .26f, .63f, .83f); break;
                default: Poly(new Vector2(.5f, .08f), new Vector2(.60f, .39f), new Vector2(.91f, .5f), new Vector2(.60f, .61f), new Vector2(.5f, .92f), new Vector2(.39f, .61f), new Vector2(.08f, .5f), new Vector2(.39f, .39f), new Vector2(.5f, .08f)); break;
            }
        }
    }
}
