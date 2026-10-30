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
    /// <summary>
    /// A field-first JRPG interface. The two bars are discovered Scripture and
    /// completed story goals, not invented health, mana or character levels.
    /// Scripture pages and quoted dialogue retain the established book renderer.
    /// </summary>
    public sealed class GospelPilgrimageUi : MonoBehaviour
    {
        public Action OnStart, OnContinue, OnResume, OnPause, OnSave, OnExit, OnStatus,
            OnInventory, OnRead, OnJournal, OnSettings, OnLampToggle, OnInteract, OnJump, OnUserGesture;
        public Action<Vector2> OnMove;
        public Action<bool> OnRun, OnMusicMute;
        public Action<float> OnMusicVolume;

        public string CurrentView => usingBook ? book.CurrentView : currentView;
        public bool IsModal => usingBook ? book.IsModal : modal;
        public Canvas CanvasView => usingBook ? book.CanvasView : canvas;
        public RectTransform Frame => usingBook ? book.Frame : frame;
        public bool ScenicView { get; private set; }

        static readonly Color Ink = new Color(.015f, .037f, .063f, .91f);
        static readonly Color Glass = new Color(.040f, .104f, .146f, .87f);
        static readonly Color Teal = new Color(.31f, .63f, .69f, .80f);
        static readonly Color LineColor = new Color(.50f, .74f, .75f, .45f);
        static readonly Color White = new Color(.91f, .96f, .96f);
        static readonly Color Quiet = new Color(.62f, .76f, .79f);
        static readonly Color Gold = new Color(.89f, .79f, .52f);
        static readonly Color Mint = new Color(.37f, .87f, .74f);

        TMP_FontAsset font, display;
        Canvas canvas;
        RectTransform frame, layer, notice, scenicButton;
        GospelLuminousUi book;
        bool usingBook, modal = true, running;
        string currentView = "none";
        TextMeshProUGUI noticeText, objective, navigation, progressText, ageText, helpText;
        Image scriptureFill, objectiveFill, runTint;
        Button interact;
        DawnPointerPad movementPad;
        Vector2 lastMove;
        float noticeUntil, nextHudUpdate;
        Rect youngPortraitUv = new Rect(0, 0, .455f, 1);
        Rect adultPortraitUv = new Rect(.5f, 0, .5f, 1);
        static Sprite solidSprite;

        public void SetPortraitRegions(Rect youngUv, Rect adultUv)
        { youngPortraitUv = youngUv; adultPortraitUv = adultUv; }

        public void Build(TMP_FontAsset koreanFont)
        {
            if (frame != null) return;
            font = koreanFont != null ? koreanFont : Resources.Load<TMP_FontAsset>("Fonts/Korean");
            display = Resources.Load<TMP_FontAsset>("Fonts/Display") ?? font;
            if (EventSystem.current == null)
                new GameObject("Pilgrimage event system", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var input = EventSystem.current.GetComponent<InputSystemUIInputModule>();
            if (input == null) input = EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>();
            input.AssignDefaultActions();
            var canvasObject = new GameObject("Pilgrimage canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false); canvasObject.layer = 5;
            canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            frame = Rect(canvasObject.transform, "Pilgrimage design frame", 0, 0, 1280, 720);
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(.5f, .5f);
            frame.anchoredPosition = Vector2.zero;
            notice = Panel(frame, "Journey notice", 350, 118, 580, 52, Ink, true);
            noticeText = Text(notice, "", 18, 10, 544, 33, 23, White, TextAlignmentOptions.Center);
            notice.gameObject.SetActive(false);

            var bookObject = new GameObject("Preserved Scripture book and dialogue");
            bookObject.transform.SetParent(transform, false);
            book = bookObject.AddComponent<GospelLuminousUi>(); book.Build(font);
            book.OnResume = () => Invoke(OnResume);
            book.OnPause = () => Invoke(OnPause);
            book.OnRead = () => Invoke(OnRead);
            book.OnJournal = () => Invoke(OnJournal);
            book.OnUserGesture = Gesture;
            book.CanvasView.gameObject.SetActive(false);
        }

        void Update()
        {
            if (notice != null) notice.gameObject.SetActive(!usingBook && Time.unscaledTime < noticeUntil);
            if (usingBook || modal || movementPad == null || !movementPad.gameObject.activeInHierarchy) return;
            var value = movementPad.Value;
            if ((value - lastMove).sqrMagnitude > .00001f)
            { lastMove = value; OnMove?.Invoke(value); }
        }

        void OnDisable() => ReleaseMovement();
        void OnApplicationFocus(bool focused) { if (!focused) ReleaseMovement(); }
        void Gesture() => OnUserGesture?.Invoke();
        void Invoke(Action action) { Gesture(); action?.Invoke(); }

        void ReleaseMovement()
        {
            if (movementPad != null) movementPad.ResetInput();
            lastMove = Vector2.zero; OnMove?.Invoke(Vector2.zero);
            running = false; OnRun?.Invoke(false);
            if (runTint != null) runTint.color = Glass;
        }

        void Begin(string view, bool isModal = true, bool veil = true)
        {
            ReleaseMovement(); usingBook = false; ScenicView = false;
            if (isModal) { noticeUntil = 0; notice.gameObject.SetActive(false); }
            if (book != null) book.CanvasView.gameObject.SetActive(false);
            canvas.gameObject.SetActive(true);
            if (layer != null) { layer.gameObject.SetActive(false); Destroy(layer.gameObject); }
            if (scenicButton != null) { scenicButton.gameObject.SetActive(false); Destroy(scenicButton.gameObject); scenicButton = null; }
            layer = Rect(frame, "Pilgrimage " + view, 0, 0, 1280, 720);
            currentView = view; modal = isModal;
            movementPad = null; runTint = null;
            objective = navigation = progressText = ageText = helpText = null;
            scriptureFill = objectiveFill = null; interact = null;
            if (veil) Panel(layer, "Atmospheric menu shade", 0, 0, 1280, 720, new Color(.008f, .022f, .039f, .76f));
            notice.SetAsLastSibling();
        }

        void BeginBook()
        {
            ReleaseMovement(); ScenicView = false; usingBook = true;
            canvas.gameObject.SetActive(false); book.CanvasView.gameObject.SetActive(true);
        }

        public void ShowTitle(bool hasSave)
        {
            Begin("title", true, false);
            Panel(layer, "Title legibility veil", 0, 0, 678, 720, new Color(.012f, .032f, .052f, .86f));
            Line(layer, 68, 116, 445, Gold);
            Text(layer, "THE GOSPEL JOURNEY", 72, 137, 620, 42, 30, Gold, TextAlignmentOptions.TopLeft, true).characterSpacing = 3;
            Text(layer, "말씀을 따라,\n길 위의 여정", 68, 209, 614, 157, 57, White);
            Text(layer, "예수님의 길", 74, 381, 560, 52, 34, White);
            Text(layer, "탄생과 성전의 만남, 요단강의 기록.\n사람들과 이야기하며 성경을 채워 보세요.", 75, 443, 590, 71, 24, Quiet);
            B(layer, "새 여정 시작", 75, 544, 275, 64, OnStart, true, true);
            B(layer, "이어하기", 365, 544, 226, 64, OnContinue, hasSave);
            B(layer, "소리 설정", 75, 625, 190, 47, OnSettings);
            Text(layer, "탄생 서막 · 열두 살 성전 · 요단강의 세례", 283, 638, 900, 28, 20, Quiet);
            var art = Panel(layer, "Portrait title inset", 791, 126, 354, 470, new Color(.025f, .075f, .10f, .64f), true);
            PaintPortrait(art, false, 2, 2, 350, 466);
            Text(layer, "THE FIRST JOURNEY", 802, 618, 344, 33, 21, Gold, TextAlignmentOptions.Center, true).characterSpacing = 3;
        }

        public void ShowHud(GospelJourney journey, Sprite character)
        {
            Begin("journey", false, false);
            var hero = Panel(layer, "Character identity", 24, 22, 288, 118, new Color(.018f, .058f, .087f, .86f), true);
            hero.GetComponent<Image>().raycastTarget = true;
            var identityHit = hero.gameObject.AddComponent<Button>();
            identityHit.targetGraphic = hero.GetComponent<Image>();
            identityHit.onClick.AddListener(() => Invoke(OnStatus));
            PaintPortrait(hero, Adult(journey), 4, 4, 78, 110);
            var identity = B(hero, Adult(journey) ? "예수님" : "소년 예수님", 87, 7, 193, 38, OnStatus);
            identity.GetComponent<Image>().color = new Color(.08f, .22f, .27f, .35f);
            ageText = Text(hero, Age(journey), 95, 50, 180, 42, 18, Quiet);
            scriptureFill = Bar(hero, "Discovered Scripture", 96, 97, 170, 4, Mint, Fraction(journey.CodexCount, journey.CodexTotal));
            var goal = Panel(layer, "Current story goal", 345, 24, 548, 88, new Color(.018f, .058f, .087f, .82f), true);
            Text(goal, "CURRENT JOURNEY", 17, 9, 480, 22, 15, Gold, TextAlignmentOptions.TopLeft, true).characterSpacing = 2;
            objective = Text(goal, GoalTitle(journey), 17, 36, 514, 40, 25, White);
            B(layer, "성경", 923, 25, 77, 52, OnRead);
            B(layer, "기록", 1009, 25, 77, 52, OnJournal);
            B(layer, "가방", 1095, 25, 77, 52, OnInventory);
            B(layer, "메뉴", 1181, 25, 77, 52, OnPause);
            var record = Panel(layer, "Scripture and story progress", 944, 90, 310, 56, new Color(.014f, .046f, .069f, .73f), true);
            progressText = Text(record, "", 13, 9, 283, 23, 18, Quiet, TextAlignmentOptions.Right);
            objectiveFill = Bar(record, "Completed story goals", 14, 41, 282, 3, Gold, Fraction(Completed(journey), GoalCount(journey)));
            var prompt = Panel(layer, "Current destination", 340, 619, 593, 77, new Color(.018f, .058f, .087f, .86f), true);
            navigation = Text(prompt, "", 18, 10, 557, 29, 22, Gold);
            helpText = Text(prompt, GoalDescription(journey), 18, 43, 557, 24, 19, White);
            interact = B(layer, "대화", 1080, 632, 174, 64, OnInteract, false, true);
            var touch = Rect(layer, "Two axis touch movement", 0, 0, 1280, 720);
            touch.gameObject.SetActive(DawnPlatformInput.UseTouchControls);
            BuildMovementPad(touch);
            var run = B(touch, "달리기", 951, 632, 113, 64, () => {
                running = !running;
                if (runTint != null) runTint.color = running ? new Color(.20f, .49f, .48f, .96f) : Glass;
                OnRun?.Invoke(running);
            });
            runTint = run.GetComponent<Image>();
            B(touch, "점프", 1136, 552, 118, 64, OnJump);
            if (!DawnPlatformInput.UseTouchControls)
                Text(layer, "W A S D 이동   Shift 달리기   Space 점프   E 대화   I 인물   B 가방   M 성경   J 기록", 28, 700, 990, 18, 15, Quiet);
            scenicButton = B(frame, "풍경 보기", 1141, 164, 111, 39, ToggleScenicView).GetComponent<RectTransform>();
            notice.SetAsLastSibling();
            progressText.text = "성경 " + journey.CodexCount + " / " + journey.CodexTotal + "    여정 " + Completed(journey) + " / " + GoalCount(journey);
            navigation.text = journey.IsComplete ? CompletedTitle(journey) : "다음 만남을 향해 걸어 보세요.";
            nextHudUpdate = 0;
        }

        void BuildMovementPad(Transform parent)
        {
            var pad = Panel(parent, "Omnidirectional movement pad", 30, 481, 180, 180, new Color(.018f, .058f, .087f, .63f), true);
            pad.pivot = new Vector2(.5f, .5f); pad.anchoredPosition = new Vector2(120, -571);
            pad.GetComponent<Image>().raycastTarget = true;
            Glyph(pad, "left", 13, 77, 25, 25, Quiet);
            Glyph(pad, "right", 142, 77, 25, 25, Quiet);
            var up = Glyph(pad, "right", 77, 13, 25, 25, Quiet); up.localRotation = Quaternion.Euler(0, 0, 90);
            var down = Glyph(pad, "right", 77, 142, 25, 25, Quiet); down.localRotation = Quaternion.Euler(0, 0, -90);
            var thumb = Panel(pad, "Movement thumb", 0, 0, 38, 38, new Color(.53f, .81f, .81f, .82f), true);
            thumb.anchorMin = thumb.anchorMax = thumb.pivot = new Vector2(.5f, .5f); thumb.anchoredPosition = Vector2.zero;
            thumb.localRotation = Quaternion.Euler(0, 0, 45);
            movementPad = pad.gameObject.AddComponent<DawnPointerPad>(); movementPad.joystick = true;
            movementPad.thumb = thumb; movementPad.radius = 65;
            var events = pad.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            entry.callback.AddListener(_ => Gesture()); events.triggers.Add(entry);
        }

        public void UpdateHud(GospelJourney journey, Vector3 player, Vector3 target, bool canInteract)
        {
            if (usingBook || currentView != "journey" || objective == null) return;
            if (interact != null) interact.interactable = canInteract || journey.IsComplete;
            if (Time.unscaledTime < nextHudUpdate) return;
            nextHudUpdate = Time.unscaledTime + .10f;
            objective.text = GoalTitle(journey); ageText.text = Age(journey);
            progressText.text = "성경 " + journey.CodexCount + " / " + journey.CodexTotal + "    여정 " + Completed(journey) + " / " + GoalCount(journey);
            SetFill(scriptureFill, Fraction(journey.CodexCount, journey.CodexTotal));
            SetFill(objectiveFill, Fraction(Completed(journey), GoalCount(journey)));
            var story = journey.Chronicle;
            if (journey.IsComplete)
            { navigation.text = CompletedTitle(journey); helpText.text = "성경을 펼쳐 발견한 말씀을 다시 읽어 보세요."; return; }
            string npc = story?.CurrentQuest?.NpcName ?? "다음 만남";
            float distance = new Vector2(target.x - player.x, target.z - player.z).magnitude;
            navigation.text = canInteract ? (DawnPlatformInput.UseTouchControls ? "대화 · " : "E · ") + npc
                : npc + "에게 가기  ·  " + distance.ToString("0.0") + "m";
            helpText.text = GoalDescription(journey);
        }

        public void ToggleScenicView()
        {
            if (usingBook || modal || layer == null) return;
            ReleaseMovement(); ScenicView = !ScenicView; layer.gameObject.SetActive(!ScenicView);
            if (scenicButton != null)
                scenicButton.GetComponentInChildren<TextMeshProUGUI>().text = ScenicView ? "게임으로" : "풍경 보기";
        }

        public void ShowStatus(GospelJourney journey, Sprite character, float currentSpeed)
        {
            Begin("status"); Header("CHARACTER & JOURNEY", "인물 · 말씀을 따라 걷는 여정");
            var portrait = Panel(layer, "Character portrait and record", 35, 126, 268, 484, Glass, true);
            PaintPortrait(portrait, Adult(journey), 3, 3, 262, 266);
            Text(portrait, Adult(journey) ? "예수님" : "소년 예수님", 18, 279, 232, 39, 30, White);
            Text(portrait, Age(journey), 18, 322, 232, 54, 21, Quiet);
            Text(portrait, "성경  " + journey.CodexCount + " / " + journey.CodexTotal, 18, 386, 232, 30, 20, Mint);
            Bar(portrait, "Discovered passages", 18, 423, 232, 5, Mint, Fraction(journey.CodexCount, journey.CodexTotal));
            Text(portrait, "여정  " + Completed(journey) + " / " + GoalCount(journey), 18, 443, 232, 29, 20, Gold);

            var status = Panel(layer, "Actual journey status", 318, 126, 300, 484, Glass, true);
            Text(status, "JOURNEY", 17, 14, 266, 30, 25, White, TextAlignmentOptions.TopLeft, true);
            Line(status, 17, 49, 266, LineColor);
            Stat(status, "book", "발견한 말씀", journey.CodexCount + " / " + journey.CodexTotal, 71);
            Stat(status, "leaf", "마친 목표", Completed(journey) + " / " + GoalCount(journey), 132);
            Stat(status, "run", "이동 속도", Mathf.Max(0, currentSpeed).ToString("0.0") + " m/s", 193);
            Stat(status, "lamp", "토기 등잔", journey.LampEquipped ? "장착 중" : "가방에 보관", 254);
            Line(status, 17, 317, 266, LineColor);
            Text(status, "현재 이야기", 17, 337, 266, 26, 18, Gold);
            Scroll(status, GoalTitle(journey), 17, 372, 266, 84, 25, White);

            var equipment = Panel(layer, "Equipment and discovered records", 633, 126, 611, 484, Glass, true);
            Text(equipment, "EQUIPMENT", 20, 14, 540, 30, 25, White, TextAlignmentOptions.TopLeft, true);
            Line(equipment, 20, 49, 571, LineColor);
            for (int i = 0; i < 4; i++)
            {
                var item = GospelBelongings.All[i];
                var row = Panel(equipment, "Equipment " + item.Id, 17, 67 + i * 58, 574, 49, new Color(.13f, .28f, .35f, i % 2 == 0 ? .43f : .24f));
                ItemIcon(row, item, i, 9, 6, 35);
                Text(row, item.Name, 56, 11, 254, 29, 22, White);
                Text(row, i == 3 ? (journey.LampEquipped ? "장착 중" : "보관") : item.Category, 315, 12, 245, 27, 18, Quiet, TextAlignmentOptions.Right);
            }
            Text(equipment, "FIELD RECORD", 20, 320, 530, 30, 22, Gold, TextAlignmentOptions.TopLeft, true);
            Line(equipment, 20, 356, 571, LineColor);
            if (character != null)
            {
                var pixel = Rect(equipment, "Actual pixel character preview", 25, 372, 81, 94).gameObject.AddComponent<Image>();
                pixel.sprite = character; pixel.preserveAspect = true; pixel.raycastTarget = false;
            }
            Text(equipment, "성경을 채우는 발걸음", 123, 374, 440, 34, 24, White);
            Text(equipment, "만남을 마쳐 발견한 말씀만 성경에 기록됩니다.\n소지품은 학습 여정을 위한 게임 설정입니다.", 124, 419, 440, 49, 19, Quiet);
            Footer();
        }

        void Stat(Transform parent, string icon, string label, string value, float y)
        {
            Glyph(parent, icon, 16, y + 1, 27, 27, White);
            Text(parent, label, 52, y, 151, 29, 21, White);
            Text(parent, value, 177, y, 106, 47, 19, Quiet, TextAlignmentOptions.TopRight);
        }

        public void ShowInventory(bool lamp)
        {
            Begin("inventory"); Header("INVENTORY", "여행 가방 · 소지품과 도구");
            var list = Panel(layer, "Travel item list", 36, 128, 509, 486, Glass, true);
            var detail = Panel(layer, "Selected travel item", 561, 128, 682, 486, Glass, true);
            TextMeshProUGUI name = null, category = null, description = null;
            RectTransform largeIcon = null;
            Button use = null;
            Action<int> select = index => {
                var item = GospelBelongings.All[index];
                name.text = index == 4 ? "성경" : item.Name;
                category.text = item.Category;
                description.text = item.Description + "\n\n" + item.Detail;
                if (index == 4) description.text = "여정에서 발견한 말씀으로 채우는 성경책입니다.\n\n아직 만나지 않은 말씀은 감춰져 있고, 실제 대화를 끝낸 본문만 다시 읽을 수 있습니다.";
                ResizeScroll(description, 430, 222);
                if (largeIcon != null) { largeIcon.gameObject.SetActive(false); Destroy(largeIcon.gameObject); }
                largeIcon = Rect(detail, "Selected illustrated item", 24, 77, 168, 168);
                ItemIcon(largeIcon, item, index, 0, 0, 168);
                if (use != null) { use.gameObject.SetActive(false); Destroy(use.gameObject); }
                if (index == 3) use = B(detail, lamp ? "등잔 보관" : "등잔 장착", 26, 397, 283, 61, OnLampToggle, true, true);
                else if (index == 4) use = B(detail, "성경 펼치기", 26, 397, 283, 61, OnRead, true, true);
                else if (index == 5) use = B(detail, "현재 여정 보기", 26, 397, 283, 61, OnJournal, true, true);
            };
            for (int i = 0; i < GospelBelongings.All.Count; i++)
            {
                int index = i; var item = GospelBelongings.All[i];
                var row = B(list, "", 15, 15 + i * 76, 477, 65, () => select(index));
                ItemIcon(row.transform, item, i, 12, 8, 48);
                Text(row.transform, i == 4 ? "성경" : item.Name, 78, 10, 270, 31, 24, White);
                Text(row.transform, i == 3 ? lamp ? "장착 중" : "보관" : item.Category, 78, 41, 365, 23, 16, Quiet);
            }
            name = Text(detail, "", 26, 20, 625, 40, 31, White);
            category = Text(detail, "", 216, 87, 430, 30, 20, Gold);
            description = Scroll(detail, "", 215, 137, 430, 222, 24, White);
            B(detail, "여정으로", 331, 397, 324, 61, OnResume);
            select(3);
            Text(layer, "소지품은 학습을 위한 게임 설정이며, 성경에 소유가 기록되었다는 뜻은 아닙니다.", 38, 654, 1190, 32, 20, Quiet);
        }

        public void ShowPause()
        {
            Begin("pause"); Header("THE JOURNEY", "여정 메뉴");
            var menu = Panel(layer, "Pause actions", 197, 149, 884, 457, Glass, true);
            B(menu, "여정 계속하기", 25, 28, 398, 75, OnResume, true, true);
            B(menu, "현재 여정 저장", 460, 28, 398, 75, OnSave);
            B(menu, "인물 · 상태", 25, 123, 398, 65, OnStatus);
            B(menu, "여행 가방", 460, 123, 398, 65, OnInventory);
            B(menu, "성경 펼치기", 25, 208, 398, 65, OnRead);
            B(menu, "여정 기록", 460, 208, 398, 65, OnJournal);
            B(menu, "소리 설정", 25, 293, 398, 65, OnSettings);
            B(menu, "처음 화면", 460, 293, 398, 65, OnExit);
            Text(menu, "W A S D · 자유 이동    Shift · 달리기    Space · 점프    E · 대화", 26, 391, 829, 36, 21, Quiet, TextAlignmentOptions.Center);
        }

        public void ShowJournal(string body)
        {
            Begin("quests"); Header("JOURNEY RECORD", "지나온 만남과 현재 목표");
            var box = Panel(layer, "Recorded story", 50, 130, 1180, 481, Glass, true);
            Scroll(box, body, 25, 23, 1128, 432, 25, White);
            Footer();
        }

        public void ShowSettings(float volume, bool muted)
        {
            Begin("settings"); Header("SOUND", "여정의 소리");
            var box = Panel(layer, "Sound settings", 110, 165, 1060, 383, Glass, true);
            Text(box, "배경 음악", 35, 28, 720, 46, 30, White);
            var value = Text(box, Mathf.RoundToInt(Mathf.Clamp01(volume) * 100) + "%", 813, 35, 210, 39, 28, Gold, TextAlignmentOptions.Right);
            var slider = VolumeSlider(box, 35, 119, 987, Mathf.Clamp01(volume));
            slider.onValueChanged.AddListener(v => { Gesture(); value.text = Mathf.RoundToInt(v * 100) + "%"; OnMusicVolume?.Invoke(v); });
            var mute = B(box, muted ? "음악 켜기" : "음악 끄기", 35, 222, 458, 65, null);
            mute.onClick.AddListener(() => {
                muted = !muted;
                mute.GetComponentInChildren<TextMeshProUGUI>().text = muted ? "음악 켜기" : "음악 끄기";
                OnMusicMute?.Invoke(muted);
            });
            B(box, "돌아가기", 545, 222, 477, 65, OnResume, true, true);
            Text(box, "처음 클릭하거나 터치하면 음악이 시작됩니다.", 36, 325, 988, 31, 21, Quiet);
        }

        public void ShowChapter(string title, string age, string summary, Action enter)
        {
            Begin("chapter", true, false);
            Backdrop(title.Contains("요단") ? "Art/GospelChronicle/World/jordan-river" : "Art/GospelRenewal/Opening/temple-journey");
            Panel(layer, "Chapter contrast", 0, 0, 960, 720, new Color(.013f, .037f, .06f, .77f));
            Line(layer, 67, 112, 708, Gold);
            Text(layer, "THE GOSPEL JOURNEY", 69, 139, 900, 38, 25, Gold, TextAlignmentOptions.TopLeft, true).characterSpacing = 3;
            Text(layer, title, 64, 208, 1060, 86, 49, White);
            Text(layer, age, 69, 307, 948, 46, 28, Gold);
            Scroll(layer, summary, 69, 388, 800, 126, 27, White);
            B(layer, "이야기 속으로", 68, 562, 371, 70, enter, true, true);
            Text(layer, DawnPlatformInput.UseTouchControls ? "방향 패드로 자유 이동 · 달리기 · 점프 · 대화 · 성경"
                : "W A S D 자유 이동 · Shift 달리기 · Space 점프 · E 대화 · M 성경 · J 기록", 69, 669, 1120, 28, 21, Quiet);
        }

        public void ShowDialogue(GospelChronicle chronicle, Action next)
        {
            BeginBook(); book.ShowChronicleDialogue(chronicle, () => Invoke(next));
            // The two new Jesus portraits match the current age. Other speakers
            // keep their correctly identified original illustrations.
            if (chronicle.CurrentDialogue?.PortraitKey == "jesus")
            {
                var texture = Resources.Load<Texture2D>("Art/GospelPilgrimage/Actors/portraits");
                if (texture != null)
                    foreach (var art in book.Frame.GetComponentsInChildren<RawImage>())
                        if (art.gameObject.name == "Painted speaker")
                        { art.texture = texture; art.uvRect = PortraitCrop(texture, Adult(chronicle) ? adultPortraitUv : youngPortraitUv, 164, 216); }
            }
        }

        public void ShowBible(IReadOnlyList<GospelBookPage> pages, int found)
        { BeginBook(); book.ShowBible(pages, found); }
        public void FocusBiblePage(int page) { if (book != null) book.FocusBiblePage(page); }
        public void Toast(string message)
        {
            if (usingBook) { book.Toast(message); return; }
            if (notice == null) return;
            noticeText.text = message; noticeUntil = Time.unscaledTime + 3.5f;
            notice.SetAsLastSibling(); notice.gameObject.SetActive(true);
        }

        void Header(string english, string korean)
        {
            Text(layer, english, 36, 23, 1190, 55, 40, White, TextAlignmentOptions.TopLeft, true);
            Text(layer, korean, 39, 84, 1180, 29, 20, Quiet);
            Line(layer, 35, 115, 1209, LineColor);
        }

        void Footer()
        {
            B(layer, "소지품", 35, 632, 192, 59, OnInventory);
            B(layer, "성경 펼치기", 244, 632, 241, 59, OnRead);
            B(layer, "여정 기록", 502, 632, 237, 59, OnJournal);
            B(layer, "여정으로", 1000, 632, 244, 59, OnResume, true, true);
        }

        void PaintPortrait(Transform parent, bool adult, float x, float y, float width, float height)
        {
            var texture = Resources.Load<Texture2D>("Art/GospelPilgrimage/Actors/portraits");
            if (texture == null) return;
            var image = Rect(parent, "Illustrated " + (adult ? "adult" : "young") + " Jesus portrait", x, y, width, height).gameObject.AddComponent<RawImage>();
            image.texture = texture; image.uvRect = PortraitCrop(texture, adult ? adultPortraitUv : youngPortraitUv, width, height); image.raycastTarget = false;
        }

        static Rect PortraitCrop(Texture2D texture, Rect source, float width, float height)
        {
            float sourceAspect = texture.width * source.width / (texture.height * source.height);
            float targetAspect = width / height;
            if (sourceAspect > targetAspect)
            {
                float newWidth = source.width * targetAspect / sourceAspect;
                return new Rect(source.x + (source.width - newWidth) * .5f, source.y, newWidth, source.height);
            }
            float newHeight = source.height * sourceAspect / targetAspect;
            // Keep the head in view when the portrait panel is more square than
            // the unchanged source half. Never stretch the face sideways.
            return new Rect(source.x, source.yMax - newHeight, source.width, newHeight);
        }

        void ItemIcon(Transform parent, GospelBelonging item, int index, float x, float y, float size)
        {
            var atlas = Resources.Load<Texture2D>("Art/GospelRenewal/Opening/travel-items");
            var r = Rect(parent, "Illustration " + item.Id, x, y, size, size);
            if (atlas != null)
            {
                var image = r.gameObject.AddComponent<RawImage>(); image.texture = atlas;
                image.uvRect = new Rect(index % 3 / 3f, 1 - (index / 3 + 1) / 2f, 1 / 3f, .5f); image.raycastTarget = false;
            }
            else
            {
                var image = r.gameObject.AddComponent<GospelBelongingIcon>(); image.ItemId = item.Id;
                image.color = Gold; image.raycastTarget = false;
            }
        }

        void Backdrop(string resource)
        {
            var texture = Resources.Load<Texture2D>(resource); if (texture == null) return;
            var image = Rect(layer, "Chapter illustration", 0, 0, 1280, 720).gameObject.AddComponent<RawImage>();
            image.texture = texture; image.raycastTarget = false;
            float ratio = texture.width / (float)texture.height, target = 1280f / 720;
            image.uvRect = ratio > target ? new Rect((1 - target / ratio) * .5f, 0, target / ratio, 1)
                : new Rect(0, (1 - ratio / target) * .5f, 1, ratio / target);
            image.transform.SetAsFirstSibling();
        }

        static bool Adult(GospelJourney journey) => journey?.Chronicle?.IsAdult ?? false;
        static bool Adult(GospelChronicle journey) => journey != null && journey.IsAdult;
        static string Age(GospelJourney journey) => journey.Chronicle?.AgeLabel ?? "이전 여정의 기록";
        static int Completed(GospelJourney journey) => journey.Chronicle?.QuestIndex ?? journey.ObjectiveIndex;
        static int GoalCount(GospelJourney journey) => journey.Chronicle?.QuestCount ?? GospelCatalog.Objectives.Count;
        static string CompletedTitle(GospelJourney journey) => journey.Chronicle != null ? "요단강의 여정 완료" : "이전 여정의 기록";
        static string GoalTitle(GospelJourney journey) => journey.IsComplete ? CompletedTitle(journey)
            : journey.Chronicle?.CurrentQuest?.Title ?? journey.CurrentReading.Title;
        static string GoalDescription(GospelJourney journey) => journey.Chronicle?.CurrentQuest?.Objective
            ?? "발견한 말씀과 지금의 여정을 살펴보세요.";
        static float Fraction(int value, int total) => total <= 0 ? 0 : Mathf.Clamp01(value / (float)total);
        static void SetFill(Image image, float amount) { if (image != null) image.fillAmount = amount; }

        RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
            var r = go.GetComponent<RectTransform>(); r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(width, height); return r;
        }

        RectTransform Panel(Transform parent, string name, float x, float y, float width, float height, Color color, bool border = false)
        {
            var r = Rect(parent, name, x, y, width, height);
            var image = r.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
            if (border)
            {
                Line(r, 0, 0, width, LineColor); Line(r, 0, height - 1, width, LineColor);
                Rule(r, 0, 0, 1, height, LineColor); Rule(r, width - 1, 0, 1, height, LineColor);
            }
            return r;
        }

        TextMeshProUGUI Text(Transform parent, string value, float x, float y, float width, float height, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft, bool serif = false)
        {
            var text = Rect(parent, "Text " + value, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = serif ? display : font; text.text = value ?? ""; text.fontSize = size; text.color = color;
            text.alignment = alignment; text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis; text.lineSpacing = 3; return text;
        }

        Button B(Transform parent, string label, float x, float y, float width, float height, Action action, bool enabled = true, bool primary = false)
        {
            var r = Panel(parent, string.IsNullOrEmpty(label) ? "Item selection" : label, x, y, width, height,
                primary ? new Color(.13f, .37f, .43f, .96f) : Glass, true);
            var image = r.GetComponent<Image>(); image.raycastTarget = true;
            var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = image; b.interactable = enabled;
            var colors = b.colors; colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.65f, .95f, 1); colors.selectedColor = new Color(.70f, .95f, .98f);
            colors.pressedColor = new Color(.40f, .70f, .79f); colors.disabledColor = new Color(.47f, .56f, .60f, .6f);
            colors.fadeDuration = .12f; b.colors = colors;
            if (!string.IsNullOrEmpty(label))
                Text(r, label, 12, 0, width - 24, height, Mathf.Min(24, height * .46f), White, TextAlignmentOptions.Midline);
            b.onClick.AddListener(() => Invoke(action)); return b;
        }

        void Rule(Transform parent, float x, float y, float width, float height, Color color)
        {
            var image = Rect(parent, "Thin menu rule", x, y, width, height).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false;
        }
        void Line(Transform parent, float x, float y, float width, Color color) => Rule(parent, x, y, width, 1, color);
        RectTransform Glyph(Transform parent, string kind, float x, float y, float width, float height, Color color)
        {
            var r = Rect(parent, "Navigation " + kind, x, y, width, height);
            var g = r.gameObject.AddComponent<GospelLuminousGlyph>(); g.Kind = kind; g.color = color; g.raycastTarget = false; return r;
        }

        Image Bar(Transform parent, string name, float x, float y, float width, float height, Color color, float amount)
        {
            var track = Panel(parent, name, x, y, width, height, new Color(.22f, .35f, .40f, .57f));
            var fill = Rect(track, "Current value", 0, 0, width, height).gameObject.AddComponent<Image>();
            if (solidSprite == null)
            {
                var white = Texture2D.whiteTexture;
                solidSprite = Sprite.Create(white, new Rect(0, 0, white.width, white.height), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
                solidSprite.hideFlags = HideFlags.HideAndDontSave;
            }
            fill.sprite = solidSprite;
            fill.color = color; fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0; fill.fillAmount = amount; fill.raycastTarget = false; return fill;
        }

        TextMeshProUGUI Scroll(Transform parent, string value, float x, float y, float width, float height, float size, Color color)
        {
            var holder = Rect(parent, "Journal scroll", x, y, width, height);
            var scroll = holder.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect(holder, "Text viewport", 0, 0, width, height);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, .001f);
            var content = Rect(viewport, "Text content", 0, 0, width - 15, height);
            var text = Text(content, value, 0, 0, width - 18, height, size, color);
            text.overflowMode = TextOverflowModes.Overflow;
            float wanted = Mathf.Max(height, text.GetPreferredValues(value, width - 18, 0).y + 8);
            content.sizeDelta = new Vector2(width - 15, wanted); text.rectTransform.sizeDelta = new Vector2(width - 18, wanted);
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
            Rule(holder, width - 4, 0, 2, height, new Color(.50f, .74f, .75f, .2f));
            return text;
        }

        static void ResizeScroll(TextMeshProUGUI text, float width, float height)
        {
            float wanted = Mathf.Max(height, text.GetPreferredValues(text.text, width - 18, 0).y + 8);
            var content = (RectTransform)text.transform.parent;
            content.sizeDelta = new Vector2(width - 15, wanted);
            text.rectTransform.sizeDelta = new Vector2(width - 18, wanted);
            content.anchoredPosition = Vector2.zero;
        }

        Slider VolumeSlider(Transform parent, float x, float y, float width, float current)
        {
            var r = Rect(parent, "Music volume slider", x, y, width, 51);
            r.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, .001f);
            var slider = r.gameObject.AddComponent<Slider>();
            Panel(r, "Volume track", 0, 21, width, 7, new Color(.22f, .35f, .40f, .7f));
            var area = Rect(r, "Volume fill area", 0, 21, width - 22, 7);
            var fill = Panel(area, "Volume current", 0, 0, width - 22, 7, Mint);
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = fill.offsetMax = Vector2.zero;
            var handles = Rect(r, "Volume handle area", 11, 0, width - 22, 51);
            var handle = Panel(handles, "Volume handle", 0, 11, 27, 27, White, true);
            handle.pivot = new Vector2(.5f, .5f); handle.anchorMin = Vector2.zero; handle.anchorMax = new Vector2(0, 1);
            handle.sizeDelta = new Vector2(27, -20); handle.anchoredPosition = Vector2.zero;
            handle.GetComponent<Image>().raycastTarget = true;
            slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = 0; slider.maxValue = 1; slider.SetValueWithoutNotify(current); return slider;
        }
    }
}
