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
    /// A field-first interface for the NPR journey. Progress is read from the
    /// real Scripture/story record; the physical Scripture book owns its pages.
    /// </summary>
    public sealed partial class GospelPilgrimageUi : MonoBehaviour
    {
        public Action OnStart, OnContinue, OnResume, OnPause, OnSave, OnExit, OnStatus,
            OnInventory, OnRead, OnJournal, OnSettings, OnLampToggle, OnInteract, OnJump, OnDash, OnPageTurn, OnUiClick, OnUserGesture;
        public Action<Vector2> OnMove;
        public Action<bool> OnRun, OnMusicMute, OnVoiceEnabled;
        public Action<float> OnMusicVolume;

        public string CurrentView => usingBook ? book.CurrentView : currentView;
        public bool IsModal => usingBook ? book.IsModal : modal;
        public Canvas CanvasView => usingBook ? book.CanvasView : canvas;
        public RectTransform Frame => usingBook ? book.Frame : frame;
        public bool ScenicView { get; private set; }
        public bool IsScriptureNoticeVisible => scriptureNotice != null && scriptureNotice.gameObject.activeInHierarchy;
        public int ScriptureNoticeCount { get; private set; }
        public GospelPilgrimageMapGraphic Minimap => minimap;
        public bool HasCameraTouchPad => cameraPad != null && cameraPad.gameObject.activeInHierarchy;
        public Texture CurrentCharacterPortrait => PortraitTexture(portraitAdult, out _);

        static readonly Color Ink = new Color(.10f, .14f, .21f, .94f);
        static readonly Color Glass = new Color(.12f, .18f, .25f, .80f);
        static readonly Color LineColor = new Color(.82f, .78f, .66f, .36f);
        static readonly Color White = new Color(.98f, .98f, .95f);
        static readonly Color Quiet = new Color(.84f, .88f, .89f);
        static readonly Color Gold = new Color(.93f, .84f, .63f);
        static readonly Color Mint = new Color(.65f, .88f, .80f);
        static readonly Color Paper = new Color(.97f, .967f, .938f, .98f);
        static readonly Color PaperInk = new Color(.17f, .23f, .29f);
        static readonly Color PaperQuiet = new Color(.40f, .46f, .49f);
        static readonly Color PaperGold = new Color(.54f, .42f, .22f);
        static readonly Color PaperTeal = new Color(.20f, .48f, .43f);

        TMP_FontAsset font, display;
        Canvas canvas;
        RectTransform frame, layer, notice, scenicButton, scriptureNotice;
        GospelLuminousUi book;
        bool usingBook, modal = true, running;
        string currentView = "none";
        TextMeshProUGUI noticeText, objective, navigation, progressText, ageText, helpText;
        Image scriptureFill, objectiveFill, runTint;
        Button interact;
        DawnPointerPad movementPad, cameraPad;
        GospelPilgrimageMapGraphic minimap;
        TextMeshProUGUI mapPlace, mapDestination;
        Vector2 lastMove;
        float noticeUntil, nextHudUpdate, scriptureNoticeUntil, nextPortraitUpdate;
        bool portraitAdult;
        bool spatialOpeningStageVisible;
        Func<string, bool, Texture> characterPortraitProvider;
        Func<string, Texture> itemPreviewProvider;
        readonly List<ModelPortraitBinding> modelPortraits = new List<ModelPortraitBinding>();
        Rect youngPortraitUv = new Rect(0, 0, .455f, 1);
        Rect adultPortraitUv = new Rect(.5f, 0, .5f, 1);
        Rect livingYoungPortraitUv = new Rect(0, 0, .5f, 1);
        Rect livingAdultPortraitUv = new Rect(.5f, 0, .5f, 1);
        static Sprite solidSprite;
        readonly DawnUi canopySkin = new DawnUi { ModernStyle = true };

        sealed class ModelPortraitBinding
        {
            public RawImage Image;
            public string Identity;
            public bool Adult;
            public bool Available;
            public float Width, Height;
            public Action<bool> AvailabilityChanged;
        }

        bool PaperMenu => currentView == "status" || currentView == "inventory" || currentView == "map"
            || currentView == "pause" || currentView == "quests" || currentView == "settings";

        /// <summary>Supply the current rigged model's portrait, including its real clothes and age.</summary>
        public void SetCharacterPortraitProvider(Func<string, bool, Texture> provider)
        {
            characterPortraitProvider = provider;
            RefreshModelPortraits();
        }

        public void SetItemPreviewProvider(Func<string, Texture> provider) => itemPreviewProvider = provider;

        /// <summary>Consume only the pointer captured by the right-hand camera pad.</summary>
        public Vector2 ConsumeCameraLook()
        {
            if (!HasCameraTouchPad) return Vector2.zero;
            // DawnPointerPad accumulates Canvas-local units. The camera owns
            // screen-pixel sensitivity, including on a scaled mobile Canvas.
            return cameraPad.ConsumeLook() * (canvas != null ? canvas.scaleFactor : 1f);
        }

        // The actual App sets this only after a prepared 3D opening is visible.
        public void SetSpatialOpeningStageVisible(bool visible) => spatialOpeningStageVisible = visible;

        public void SetPortraitRegions(Rect youngUv, Rect adultUv)
        { youngPortraitUv = youngUv; adultPortraitUv = adultUv; }
        public void SetLivingPortraitRegions(Rect youngUv, Rect adultUv)
        { livingYoungPortraitUv = youngUv; livingAdultPortraitUv = adultUv; }

        public void Build(TMP_FontAsset koreanFont)
        {
            if (frame != null) return;
            font = koreanFont != null ? koreanFont : Resources.Load<TMP_FontAsset>("Fonts/Korean");
            display = font;
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
            notice = Panel(frame, "Journey notice", 350, 112, 580, 54, Ink, true);
            noticeText = Text(notice, "", 18, 10, 544, 34, 23, White, TextAlignmentOptions.Center);
            notice.gameObject.SetActive(false);
            scriptureNotice = Panel(frame, "Quiet Scripture addition notice", 828, 213, 400, 38,
                new Color(.10f, .16f, .23f, .78f), true);
            Text(scriptureNotice, "성경에 말씀이 추가되었습니다.", 10, 0, 380, 38, 18,
                Gold, TextAlignmentOptions.Midline);
            scriptureNotice.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            scriptureNotice.gameObject.SetActive(false);

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
            if (Time.unscaledTime >= nextPortraitUpdate)
            { nextPortraitUpdate = Time.unscaledTime + .25f; RefreshModelPortraits(); }
            if (notice != null) notice.gameObject.SetActive(!usingBook && Time.unscaledTime < noticeUntil);
            if (scriptureNotice != null)
            {
                bool visible = !usingBook && Time.unscaledTime < scriptureNoticeUntil;
                scriptureNotice.gameObject.SetActive(visible);
                if (visible) scriptureNotice.GetComponent<CanvasGroup>().alpha = Mathf.Clamp01((scriptureNoticeUntil - Time.unscaledTime) / .45f);
            }
            if (usingBook || modal || movementPad == null || !movementPad.gameObject.activeInHierarchy) return;
            var value = movementPad.Value;
            if ((value - lastMove).sqrMagnitude > .00001f)
            { lastMove = value; OnMove?.Invoke(value); }
        }

        void OnDisable() => ReleaseMovement();
        void OnApplicationFocus(bool focused) { if (!focused) ReleaseMovement(); }
        void Gesture() => OnUserGesture?.Invoke();
        void Invoke(Action action) { Gesture(); if (action != null) { OnUiClick?.Invoke(); action(); } }

        void ReleaseMovement()
        {
            if (movementPad != null) movementPad.ResetInput();
            if (cameraPad != null) cameraPad.ResetInput();
            lastMove = Vector2.zero; OnMove?.Invoke(Vector2.zero);
            running = false; OnRun?.Invoke(false);
            if (runTint != null) runTint.color = Glass;
        }

        void Begin(string view, bool isModal = true, bool veil = true)
        {
            ReleaseMovement(); usingBook = false; ScenicView = false;
            if (isModal) { noticeUntil = 0; notice.gameObject.SetActive(false); }
            if (view != "story" && view != "chapter") scriptureNoticeUntil = 0;
            if (book != null) book.CanvasView.gameObject.SetActive(false);
            canvas.gameObject.SetActive(true);
            if (layer != null) { layer.gameObject.SetActive(false); Destroy(layer.gameObject); }
            if (scenicButton != null) { scenicButton.gameObject.SetActive(false); Destroy(scenicButton.gameObject); scenicButton = null; }
            layer = Rect(frame, "Pilgrimage " + view, 0, 0, 1280, 720);
            currentView = view; modal = isModal;
            // Story illustrations remain opaque during every dialogue change.
            // Fading the entire layer exposed the future exploration field.
            if (isModal && view != "story") layer.gameObject.AddComponent<GospelUiMotion>();
            movementPad = cameraPad = null; runTint = null;
            minimap = null; mapPlace = mapDestination = null;
            objective = navigation = progressText = ageText = helpText = null;
            scriptureFill = objectiveFill = null; interact = null;
            if (veil)
            {
                if (PaperMenu)
                {
                    Panel(layer, "Quiet ivory menu surface", 0, 0, 1280, 720, Paper);
                    Rule(layer, 0, 0, 1280, 5, new Color(.78f, .69f, .48f, .90f));
                }
                else Panel(layer, "Atmospheric menu shade", 0, 0, 1280, 720, new Color(.07f, .10f, .16f, .72f));
            }
            notice.SetAsLastSibling();
            scriptureNotice.SetAsLastSibling();
        }

        void BeginBook()
        {
            ReleaseMovement(); ScenicView = false; usingBook = true;
            canvas.gameObject.SetActive(false); book.CanvasView.gameObject.SetActive(true);
        }

        public void ShowTitle(bool hasSave)
        {
            Begin("title", true, false);
            // The new 3D field remains visible behind the title. A painted
            // splash must not disguise the appearance of the playable scene.
            var titleShade = Rect(layer, "Title legibility gradient", 0, 0, 895, 720).gameObject.AddComponent<DawnGradient>();
            titleShade.left = new Color(.08f, .13f, .20f, .93f);
            titleShade.right = new Color(.08f, .13f, .20f, 0); titleShade.raycastTarget = false;
            Glyph(layer, "leaf", 79, 112, 39, 39, Gold);
            Line(layer, 138, 134, 164, new Color(.93f, .84f, .63f, .60f));
            Text(layer, "예수님의 길", 73, 187, 666, 94, 68, White, TextAlignmentOptions.TopLeft, true);
            Text(layer, "말씀을 따라, 길 위에서", 80, 303, 615, 43, 28, Gold);
            Text(layer, "한 사람의 만남이 새로운 여정이 됩니다.", 82, 364, 610, 35, 22, Quiet);
            B(layer, "새 여정 시작", 80, 480, 303, 65, OnStart, true, true);
            B(layer, "이어하기", 80, 560, 303, 65, OnContinue, hasSave);
            B(layer, "소리 설정", 80, 653, 154, 40, OnSettings);
            Text(layer, "탄생 · 성전 · 요단강", 278, 663, 500, 25, 18, Quiet);
        }

        public void ShowHud(GospelJourney journey, Sprite character)
        {
            Begin("journey", false, false);
            portraitAdult = Adult(journey);
            BuildMinimap();
            var goal = Panel(layer, "Current story goal", 30, 246, 318, 91, new Color(.10f, .16f, .23f, .68f), true);
            Rule(goal, 0, 17, 3, 54, Gold);
            Text(goal, "다음 만남", 17, 10, 277, 21, 16, Gold);
            objective = Text(goal, GoalTitle(journey), 17, 36, 277, 43, 23, White, TextAlignmentOptions.TopLeft, true);

            Utility("성경", "book", 908, OnRead);
            Utility("기록", "leaf", 995, OnJournal);
            Utility("가방", "bag", 1082, OnInventory);
            Utility("메뉴", "settings", 1169, OnPause);
            progressText = Text(layer, "", 907, 112, 343, 29, 18, White, TextAlignmentOptions.Right);

            // Identity sits below the action, leaving the model and horizon in
            // view. The two fine rules show the actual story/Scripture record.
            var hero = Panel(layer, "Character identity", 440, 644, 397, 58, new Color(.10f, .16f, .23f, .62f), true);
            hero.GetComponent<Image>().raycastTarget = true;
            var identityHit = hero.gameObject.AddComponent<Button>();
            identityHit.targetGraphic = hero.GetComponent<Image>();
            identityHit.onClick.AddListener(() => Invoke(OnStatus));
            PaintPortrait(hero, portraitAdult, 4, 2, 52, 52);
            var identity = B(hero, portraitAdult ? "예수님" : "소년 예수님", 67, 0, 166, 33, OnStatus);
            identity.GetComponent<Image>().color = Color.clear;
            identity.GetComponentInChildren<TextMeshProUGUI>().fontSize = 21;
            ageText = Text(hero, ShortAge(journey), 242, 8, 140, 28, 18, Quiet, TextAlignmentOptions.Right);
            scriptureFill = Bar(hero, "Discovered Scripture progress", 79, 44, 143, 3, Mint, Fraction(journey.CodexCount, journey.CodexTotal));
            objectiveFill = Bar(hero, "Journey progress", 235, 44, 145, 3, Gold, Fraction(Completed(journey), GoalCount(journey)));

            var prompt = Panel(layer, "Current destination", 402, 552, 475, 71, new Color(.10f, .16f, .23f, .60f), true);
            navigation = Text(prompt, "", 17, 10, 441, 29, 22, Gold, TextAlignmentOptions.Center);
            helpText = Text(prompt, GoalDescription(journey), 17, 43, 441, 22, 16, Quiet, TextAlignmentOptions.Center);
            interact = B(layer, "대화", 1101, 617, 148, 79, OnInteract, false, true);

            var touch = Rect(layer, "Two axis touch movement", 0, 0, 1280, 720);
            touch.gameObject.SetActive(DawnPlatformInput.UseTouchControls);
            BuildCameraPad(touch);
            BuildMovementPad(touch);
            var run = B(touch, "달리기", 991, 617, 96, 79, () => {
                running = !running;
                if (runTint != null) runTint.color = running ? new Color(.29f, .52f, .50f, .96f) : Glass;
                OnRun?.Invoke(running);
            });
            runTint = run.GetComponent<Image>();
            var jump = B(touch, "점프", 1153, 505, 96, 88, OnJump);
            var dash = B(touch, "대시", 1041, 505, 96, 88, OnDash);
            canopySkin.Round(jump.GetComponent<RectTransform>(), 25);
            canopySkin.Round(dash.GetComponent<RectTransform>(), 25);
            if (!DawnPlatformInput.UseTouchControls)
                Text(layer, "WASD 이동   Shift 달리기   Space 점프   E 대화   우클릭 시점", 31, 687, 398, 22, 15, White);
            scenicButton = B(frame, "풍경 보기", 1125, 163, 124, 39, ToggleScenicView).GetComponent<RectTransform>();
            notice.SetAsLastSibling();
            progressText.text = "성경 " + journey.CodexCount + " / " + journey.CodexTotal + "    여정 " + Completed(journey) + " / " + GoalCount(journey);
            navigation.text = journey.IsComplete ? CompletedTitle(journey) : "다음 만남을 향해 걸어 보세요.";
            nextHudUpdate = 0;
        }

        void Utility(string label, string glyph, float x, Action action)
        {
            var button = B(layer, label, x, 25, 80, 78, action);
            button.GetComponent<Image>().color = new Color(.10f, .16f, .23f, .64f);
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            text.rectTransform.anchoredPosition = new Vector2(7, -45);
            text.rectTransform.sizeDelta = new Vector2(66, 28);
            text.fontSize = 20;
            Glyph(button.transform, glyph, 25, 10, 30, 30, White);
        }

        void BuildMinimap()
        {
            var app = GetComponent<GospelPilgrimageApp>();
            var panel = Panel(layer, "Field minimap", 30, 25, 260, 202,
                new Color(.10f, .16f, .23f, .76f), true);
            panel.GetComponent<Image>().raycastTarget = true;
            var open = panel.gameObject.AddComponent<Button>(); open.targetGraphic = panel.GetComponent<Image>();
            open.onClick.AddListener(() => Invoke(ShowMap));
            mapPlace = Text(panel, "여정 지도", 14, 10, 212, 25, 18, White, TextAlignmentOptions.MidlineLeft, true);
            Text(panel, "북", 228, 10, 21, 25, 15, Gold, TextAlignmentOptions.Center);
            var area = Rect(panel, "North-up live map", 12, 42, 236, 102);
            area.gameObject.AddComponent<CanvasRenderer>();
            minimap = area.gameObject.AddComponent<GospelPilgrimageMapGraphic>(); minimap.Bind(app);
            mapDestination = Text(panel, "", 14, 151, 232, 24, 18, White, TextAlignmentOptions.MidlineLeft);
            Text(panel, "지도 펼치기", 14, 178, 232, 20, 16, Gold, TextAlignmentOptions.Right);
        }

        public void ShowMap()
        {
            var app = GetComponent<GospelPilgrimageApp>();
            if (app == null || app.World == null || app.Player == null || !app.InGame) return;
            Begin("map"); Header("길과 만남", app.World.Chapter == "baptism" ? "요단강의 길" : "유월절의 길");
            var page = Panel(layer, "Unfolded field map", 35, 136, 844, 466, Glass, true);
            var area = Rect(page, "North-up full live map", 16, 37, 812, 409);
            area.gameObject.AddComponent<CanvasRenderer>();
            var graphic = area.gameObject.AddComponent<GospelPilgrimageMapGraphic>(); graphic.Bind(app);
            Text(page, "북", 778, 6, 46, 26, 18, Gold, TextAlignmentOptions.Center);
            Text(page, "흰 화살표  내 위치       푸른 점  사람들       금빛 마름모  다음 만남", 20, 8, 730, 22, 16, Quiet);
            var people = Panel(layer, "People and next meeting", 902, 136, 342, 466, Glass, true);
            Text(people, "다음 만남", 22, 20, 296, 27, 18, Gold, TextAlignmentOptions.TopLeft, true);
            string name = app.Chronicle != null && !app.Chronicle.IsComplete ? app.Chronicle.CurrentQuest.NpcName : "여정의 기록";
            Text(people, name, 22, 57, 296, 42, 30, White, TextAlignmentOptions.TopLeft, true);
            float distance = Vector2.Distance(new Vector2(app.Player.Position.x, app.Player.Position.z),
                new Vector2(app.ObjectivePosition.x, app.ObjectivePosition.z));
            Text(people, app.Chronicle != null && app.Chronicle.IsComplete ? "발견한 말씀을 다시 읽어 보세요." : "지금 있는 곳에서 " + distance.ToString("0.0") + "m",
                22, 105, 296, 30, 19, Quiet);
            Rule(people, 22, 151, 296, 1, LineColor);
            Text(people, "같은 길 위의 사람들", 22, 173, 296, 30, 20, Gold, TextAlignmentOptions.TopLeft, true);
            int row = 0;
            if (app.People != null)
                foreach (var person in app.People.Markers)
                {
                    if (row >= 5) break;
                    Text(people, person.DisplayName, 23, 215 + row * 42, 155, 28, 19, White);
                    Text(people, person.IsWalking ? "걷는 중" : "머무는 중", 179, 218 + row * 42, 138, 26, 16, Quiet, TextAlignmentOptions.Right);
                    row++;
                }
            B(layer, "여정 기록", 35, 632, 241, 59, OnJournal);
            B(layer, "성경 펼치기", 293, 632, 241, 59, OnRead);
            B(layer, "여정으로", 1000, 632, 244, 59, OnResume, true, true);
        }

        void BuildMovementPad(Transform parent)
        {
            var pad = Panel(parent, "Omnidirectional movement pad", 30, 481, 180, 180, new Color(.11f, .16f, .24f, .25f), true);
            canopySkin.Round(pad, 90);
            pad.pivot = new Vector2(.5f, .5f); pad.anchoredPosition = new Vector2(120, -571);
            pad.GetComponent<Image>().raycastTarget = true;
            Glyph(pad, "left", 13, 77, 25, 25, Quiet);
            Glyph(pad, "right", 142, 77, 25, 25, Quiet);
            var up = Glyph(pad, "right", 77, 13, 25, 25, Quiet); up.localRotation = Quaternion.Euler(0, 0, 90);
            var down = Glyph(pad, "right", 77, 142, 25, 25, Quiet); down.localRotation = Quaternion.Euler(0, 0, -90);
            var thumb = Panel(pad, "Movement thumb", 0, 0, 38, 38, new Color(.98f, .97f, .92f, .74f), true);
            canopySkin.Round(thumb, 19);
            thumb.anchorMin = thumb.anchorMax = thumb.pivot = new Vector2(.5f, .5f); thumb.anchoredPosition = Vector2.zero;
            movementPad = pad.gameObject.AddComponent<DawnPointerPad>(); movementPad.joystick = true;
            movementPad.thumb = thumb; movementPad.radius = 65;
            var events = pad.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            entry.callback.AddListener(_ => Gesture()); events.triggers.Add(entry);
        }

        void BuildCameraPad(Transform parent)
        {
            var pad = Rect(parent, "Touch camera orbit", 620, 120, 630, 370);
            var target = pad.gameObject.AddComponent<Image>();
            target.color = new Color(1, 1, 1, .001f);
            target.raycastTarget = true;
            cameraPad = pad.gameObject.AddComponent<DawnPointerPad>();
            cameraPad.joystick = false;
        }

        public void UpdateHud(GospelJourney journey, Vector3 player, Vector3 target, bool canInteract, Camera view = null)
        {
            if (usingBook || currentView != "journey" || objective == null) return;
            if (interact != null) interact.interactable = canInteract || journey.IsComplete;
            if (Time.unscaledTime < nextHudUpdate) return;
            nextHudUpdate = Time.unscaledTime + .10f;
            objective.text = GoalTitle(journey); ageText.text = ShortAge(journey);
            progressText.text = "성경 " + journey.CodexCount + " / " + journey.CodexTotal + "    여정 " + Completed(journey) + " / " + GoalCount(journey);
            SetFill(scriptureFill, Fraction(journey.CodexCount, journey.CodexTotal));
            SetFill(objectiveFill, Fraction(Completed(journey), GoalCount(journey)));
            var story = journey.Chronicle;
            if (mapPlace != null) mapPlace.text = story != null && story.IsAdult ? "요단강 주변" : "유월절의 길";
            if (mapDestination != null)
            {
                float mapDistance = Vector2.Distance(new Vector2(player.x, player.z), new Vector2(target.x, target.z));
                mapDestination.color = canInteract ? Mint : Gold;
                mapDestination.text = journey.IsComplete ? "발견한 말씀의 여정" : (story?.CurrentQuest?.NpcName ?? "다음 만남") + "  " + mapDistance.ToString("0.0") + "m";
            }
            if (journey.IsComplete)
            { navigation.text = CompletedTitle(journey); helpText.text = "성경을 펼쳐 발견한 말씀을 다시 읽어 보세요."; return; }
            string npc = story?.CurrentQuest?.NpcName ?? "다음 만남";
            float distance = new Vector2(target.x - player.x, target.z - player.z).magnitude;
            bool walkingTogether = false;
            var app = GetComponent<GospelPilgrimageApp>();
            if (app != null && app.People != null)
                foreach (var person in app.People.Markers)
                    if (person.IsCurrentObjective) { walkingTogether = person.IsWalking; break; }
            var offset = new Vector2(target.x - player.x, target.z - player.z);
            if (view != null)
            {
                var from = view.WorldToViewportPoint(player);
                var to = view.WorldToViewportPoint(target);
                offset = new Vector2(to.x - from.x, to.y - from.y);
            }
            navigation.color = canInteract ? Mint : Gold;
            navigation.text = walkingTogether && !canInteract
                ? npc + " · 걷는 중  " + distance.ToString("0.0") + "m"
                : canInteract
                ? npc + " · 대화 가능  " + (DawnPlatformInput.UseTouchControls ? "[대화]" : "[E]")
                : npc + " · " + EncounterDirection(offset) + "  " + distance.ToString("0.0") + "m";
            helpText.text = GoalDescription(journey);
        }

        static string EncounterDirection(Vector2 screenOffset)
        {
            if (screenOffset.sqrMagnitude < .000001f) return "바로 앞";
            var direction = screenOffset.normalized;
            string horizontal = Mathf.Abs(direction.x) >= .45f ? (direction.x > 0 ? "오른쪽" : "왼쪽") : "";
            string vertical = Mathf.Abs(direction.y) >= .45f ? (direction.y > 0 ? "위쪽" : "아래쪽") : "";
            return "화면 " + horizontal + (horizontal.Length > 0 && vertical.Length > 0 ? " " : "") + vertical;
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
            Begin("status"); portraitAdult = Adult(journey); Header("인물", "길 위의 인물");
            var portrait = Panel(layer, "Character portrait and record", 35, 126, 268, 484, Glass, true);
            PaintPortrait(portrait, Adult(journey), 3, 3, 262, 266);
            Text(portrait, Adult(journey) ? "예수님" : "소년 예수님", 18, 279, 232, 39, 30, White);
            Text(portrait, Age(journey), 18, 322, 232, 54, 21, Quiet);
            Text(portrait, "성경  " + journey.CodexCount + " / " + journey.CodexTotal, 18, 386, 232, 30, 20, Mint);
            Bar(portrait, "Discovered passages", 18, 423, 232, 5, Mint, Fraction(journey.CodexCount, journey.CodexTotal));
            Text(portrait, "여정  " + Completed(journey) + " / " + GoalCount(journey), 18, 443, 232, 29, 20, Gold);

            var status = Panel(layer, "Actual journey status", 318, 126, 300, 484, Glass, true);
            Text(status, "여정", 17, 14, 266, 30, 25, White, TextAlignmentOptions.TopLeft, true);
            Line(status, 17, 49, 266, LineColor);
            Stat(status, "book", "발견한 말씀", journey.CodexCount + " / " + journey.CodexTotal, 71);
            Stat(status, "leaf", "마친 목표", Completed(journey) + " / " + GoalCount(journey), 132);
            Stat(status, "run", "이동 속도", Mathf.Max(0, currentSpeed).ToString("0.0") + " m/s", 193);
            Stat(status, "lamp", "토기 등잔", journey.LampEquipped ? "장착 중" : "가방에 보관", 254);
            Line(status, 17, 317, 266, LineColor);
            Text(status, "현재 이야기", 17, 337, 266, 26, 18, Gold);
            Scroll(status, GoalTitle(journey), 17, 372, 266, 84, 25, White);

            var equipment = Panel(layer, "Equipment and discovered records", 633, 126, 611, 484, Glass, true);
            Text(equipment, "여행 차림", 20, 14, 540, 30, 25, White, TextAlignmentOptions.TopLeft, true);
            Line(equipment, 20, 49, 571, LineColor);
            for (int i = 0; i < 4; i++)
            {
                var item = GospelBelongings.All[i];
                var row = Panel(equipment, "Equipment " + item.Id, 17, 67 + i * 58, 574, 49,
                    new Color(.86f, .89f, .87f, i % 2 == 0 ? .72f : .38f));
                ItemIcon(row, item, i, 9, 6, 35);
                Text(row, item.Name, 56, 11, 254, 29, 22, White);
                Text(row, i == 3 ? (journey.LampEquipped ? "장착 중" : "보관") : item.Category, 315, 12, 245, 27, 18, Quiet, TextAlignmentOptions.Right);
            }
            Text(equipment, "함께하는 도구", 20, 320, 530, 30, 22, Gold, TextAlignmentOptions.TopLeft, true);
            Line(equipment, 20, 356, 571, LineColor);
            Glyph(equipment, "lamp", 39, 386, 64, 64, PaperGold);
            Text(equipment, journey.LampEquipped ? "토기 등잔 · 장착 중" : "토기 등잔 · 보관 중", 123, 374, 440, 34, 24, White);
            Text(equipment, journey.LampEquipped ? "토기 등잔을 들고 있습니다." : "토기 등잔은 가방에 보관 중입니다.",
                124, 419, 440, 49, 19, Quiet);
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
            Begin("inventory"); Header("소지품", "여행 가방");
            var list = Panel(layer, "Travel item list", 36, 128, 509, 486, Glass, true);
            var detail = Panel(layer, "Selected travel item", 561, 128, 682, 486, Glass, true);
            TextMeshProUGUI name = null, category = null, description = null;
            RectTransform largeIcon = null;
            Button use = null;
            var choices = new List<Button>();
            Action<int> select = index => {
                var item = GospelBelongings.All[index];
                for (int i = 0; i < choices.Count; i++)
                    choices[i].GetComponent<Image>().color = i == index
                        ? new Color(.87f, .83f, .70f, .88f) : new Color(.92f, .94f, .91f, .56f);
                name.text = index == 4 ? "성경" : item.Name;
                category.text = item.Category;
                description.text = item.Description + (index == 3 ? "\n\n" + item.Detail : "");
                if (index == 4) description.text = "여정에서 발견한 말씀을 다시 읽을 수 있습니다.";
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
                choices.Add(row);
                ItemIcon(row.transform, item, i, 12, 8, 48);
                Text(row.transform, i == 4 ? "성경" : item.Name, 78, 10, 270, 31, 24, White);
                Text(row.transform, i == 3 ? lamp ? "장착 중" : "보관" : item.Category, 78, 41, 365, 23, 16, Quiet);
            }
            name = Text(detail, "", 26, 20, 625, 40, 31, White);
            category = Text(detail, "", 216, 87, 430, 30, 20, Gold);
            description = Scroll(detail, "", 215, 137, 430, 222, 24, White);
            B(detail, "여정으로", 331, 397, 324, 61, OnResume);
            select(3);
        }

        public void ShowPause()
        {
            Begin("pause"); Header("예수님의 길", "여정 메뉴");
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
            Begin("quests"); Header("기록", "지나온 만남");
            var box = Panel(layer, "Recorded story", 50, 130, 1180, 481, Glass, true);
            Scroll(box, body, 25, 23, 1128, 432, 25, White);
            Footer();
        }

        public void ShowSettings(float volume, bool muted, bool voiceEnabled = true)
        {
            Begin("settings"); Header("설정", "여정의 소리");
            var box = Panel(layer, "Sound settings", 110, 145, 1060, 445, Glass, true);
            Text(box, "전체 음량", 35, 28, 720, 46, 30, White);
            var value = Text(box, Mathf.RoundToInt(Mathf.Clamp01(volume) * 100) + "%", 813, 35, 210, 39, 28, Gold, TextAlignmentOptions.Right);
            var slider = VolumeSlider(box, 35, 119, 987, Mathf.Clamp01(volume));
            slider.onValueChanged.AddListener(v => { Gesture(); value.text = Mathf.RoundToInt(v * 100) + "%"; OnMusicVolume?.Invoke(v); });
            var mute = B(box, muted ? "소리 켜기" : "소리 끄기", 35, 213, 458, 58, null);
            mute.onClick.AddListener(() => {
                muted = !muted;
                mute.GetComponentInChildren<TextMeshProUGUI>().text = muted ? "소리 켜기" : "소리 끄기";
                OnMusicMute?.Invoke(muted);
            });
            var voiceToggle = B(box, voiceEnabled ? "대사 음성 · 켜짐" : "대사 음성 · 꺼짐", 545, 213, 477, 58, null);
            voiceToggle.onClick.AddListener(() => {
                Gesture(); voiceEnabled = !voiceEnabled;
                voiceToggle.GetComponentInChildren<TextMeshProUGUI>().text = voiceEnabled ? "대사 음성 · 켜짐" : "대사 음성 · 꺼짐";
                OnVoiceEnabled?.Invoke(voiceEnabled);
            });
            B(box, "돌아가기", 545, 300, 477, 62, OnResume, true, true);
            Text(box, "핵심 장면에는 한국어 음성이 재생됩니다.\n처음 클릭하거나 터치하면 소리가 시작됩니다.", 36, 306, 480, 81, 19, Quiet);
        }

        public void ShowChapter(string title, string age, string summary, Action enter)
        {
            Begin("chapter", true, false);
            var contrast = Rect(layer, "Chapter legibility gradient", 0, 0, 1110, 720).gameObject.AddComponent<DawnGradient>();
            contrast.left = new Color(.08f, .13f, .20f, .91f);
            contrast.right = new Color(.08f, .13f, .20f, 0);
            contrast.raycastTarget = false;
            Line(layer, 67, 112, 708, Gold);
            Text(layer, "말씀을 따라 걷는 길", 69, 139, 900, 38, 23, Gold, TextAlignmentOptions.TopLeft, true).characterSpacing = 1;
            Text(layer, title, 64, 208, 1060, 86, 49, White);
            Text(layer, age, 69, 307, 948, 46, 28, Gold);
            Scroll(layer, summary, 69, 388, 800, 126, 27, White);
            B(layer, "이야기 속으로", 68, 562, 371, 70, enter, true, true);
        }

        public void ShowDialogue(GospelChronicle chronicle, Action next)
        {
            BeginBook(); book.ShowChronicleDialogue(chronicle, () => Invoke(next));
            portraitAdult = Adult(chronicle);
            var speaker = ModelSpeakerIdentity(chronicle.CurrentDialogue);
            var texture = string.IsNullOrWhiteSpace(speaker) ? null : characterPortraitProvider?.Invoke(speaker, portraitAdult);
            foreach (var art in book.Frame.GetComponentsInChildren<RawImage>())
            {
                if (art.gameObject.name != "Painted speaker") continue;
                art.enabled = texture != null;
                if (texture != null)
                { art.texture = texture; art.uvRect = PortraitCrop(texture, new Rect(0, 0, 1, 1), 164, 216); }
            }
        }

        /// <summary>Return a human speaker's model key; narration and unseen voices have no stand-in model.</summary>
        public static string ModelSpeakerIdentity(GospelDialogueTurn turn)
        {
            if (turn == null || string.IsNullOrWhiteSpace(turn.PortraitKey)) return null;
            // Historical art keys sometimes use a teacher tile for narration
            // or an unseen voice. Do not present that tile's human model as
            // the narrator, an angel, or the voice from heaven.
            if (turn.Speaker == "이야기" || turn.Speaker == "복음서의 기록"
                || turn.Speaker == "복음서의 이야기" || turn.Speaker == "성경의 기록"
                || turn.Speaker == "천사" || turn.Speaker == "가브리엘" || turn.Speaker == "주의 사자"
                || turn.Speaker == "천군" || turn.Speaker == "하늘에서 들린 소리") return null;
            return turn.PortraitKey;
        }

        public void Toast(string message)
        {
            if (usingBook) { book.Toast(message); return; }
            if (notice == null) return;
            noticeText.text = message; noticeUntil = Time.unscaledTime + 3.5f;
            notice.SetAsLastSibling(); notice.gameObject.SetActive(true);
        }

        public void NotifyScriptureAdded()
        {
            if (scriptureNotice == null) return;
            ScriptureNoticeCount++;
            scriptureNoticeUntil = Time.unscaledTime + 2.8f;
            scriptureNotice.GetComponent<CanvasGroup>().alpha = 1;
            scriptureNotice.SetAsLastSibling();
            scriptureNotice.gameObject.SetActive(true);
        }

        void Header(string english, string korean)
        {
            Text(layer, english, 39, 24, 1180, 25, 17, Gold, TextAlignmentOptions.TopLeft, true).characterSpacing = 1;
            Text(layer, korean, 36, 59, 1190, 47, 35, White, TextAlignmentOptions.TopLeft, true);
            Line(layer, 35, 115, 1209, new Color(.63f, .59f, .47f, .27f));
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
            ModelPortrait(parent, "Actual model " + (adult ? "adult" : "young") + " Jesus portrait",
                "jesus", adult, x, y, width, height);
        }

        ModelPortraitBinding ModelPortrait(Transform parent, string name, string identity, bool adult,
            float x, float y, float width, float height)
        {
            var texture = string.IsNullOrWhiteSpace(identity) ? null : characterPortraitProvider?.Invoke(identity, adult);
            var image = Rect(parent, name, x, y, width, height).gameObject.AddComponent<RawImage>();
            image.texture = texture; image.raycastTarget = false; image.enabled = texture != null;
            if (texture != null) image.uvRect = PortraitCrop(texture, new Rect(0, 0, 1, 1), width, height);
            var binding = new ModelPortraitBinding { Image = image, Identity = identity, Adult = adult,
                Available = texture != null, Width = width, Height = height };
            modelPortraits.Add(binding);
            return binding;
        }

        Texture PortraitTexture(bool adult, out Rect uv)
        {
            uv = new Rect(0, 0, 1, 1);
            return characterPortraitProvider?.Invoke("jesus", adult);
        }

        void RefreshModelPortraits()
        {
            for (int i = modelPortraits.Count - 1; i >= 0; i--)
            {
                var binding = modelPortraits[i];
                if (binding.Image == null) { modelPortraits.RemoveAt(i); continue; }
                if (!binding.Image.gameObject.activeInHierarchy) continue;
                var texture = string.IsNullOrWhiteSpace(binding.Identity) ? null
                    : characterPortraitProvider?.Invoke(binding.Identity, binding.Adult);
                bool available = texture != null;
                binding.Image.texture = texture;
                binding.Image.enabled = available;
                if (available != binding.Available)
                {
                    binding.Available = available;
                    binding.AvailabilityChanged?.Invoke(available);
                }
                if (texture == null) continue;
                binding.Image.uvRect = PortraitCrop(texture, new Rect(0, 0, 1, 1), binding.Width, binding.Height);
            }
        }

        static Rect PortraitCrop(Texture texture, Rect source, float width, float height)
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
            var preview = itemPreviewProvider?.Invoke(item.Id);
            var r = Rect(parent, "Travel item " + item.Id, x, y, size, size);
            if (preview != null)
            {
                var image = r.gameObject.AddComponent<RawImage>(); image.texture = preview;
                image.raycastTarget = false;
            }
            else
            {
                var image = r.gameObject.AddComponent<GospelBelongingIcon>(); image.ItemId = item.Id;
                image.color = PaperMenu ? PaperGold : Gold; image.raycastTarget = false;
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
        static string ShortAge(GospelJourney journey)
        {
            string value = Age(journey);
            int separator = value.IndexOf('·');
            return separator > 0 ? value.Substring(0, separator).Trim() : value;
        }
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
            if (PaperMenu && color == Glass) color = new Color(.995f, .992f, .973f, .94f);
            if (name == "Story dialogue") color = new Color(.08f, .13f, .20f, .89f);
            if (name == "Field conversation context") color = new Color(.09f, .15f, .22f, .54f);
            if (border)
            {
                var rounded = canopySkin.Card(parent, name, x, y, width, height, color);
                canopySkin.Round(rounded, Mathf.Min(8, height * .22f));
                var edge = rounded.GetComponentInChildren<DawnFrame>();
                if (edge != null)
                {
                    edge.thickness = 1;
                    edge.color = PaperMenu ? new Color(.61f, .56f, .43f, .20f) : new Color(.98f, .96f, .86f, .27f);
                    edge.sheen = .012f;
                }
                var shadow = rounded.GetComponent<Shadow>();
                if (shadow != null)
                {
                    shadow.effectDistance = new Vector2(0, -3);
                    shadow.effectColor = new Color(.06f, .10f, .16f, PaperMenu ? .07f : .14f);
                }
                rounded.GetComponent<Image>().raycastTarget = false;
                return rounded;
            }
            var r = Rect(parent, name, x, y, width, height);
            var image = r.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
            return r;
        }

        TextMeshProUGUI Text(Transform parent, string value, float x, float y, float width, float height, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft, bool serif = false)
        {
            var text = Rect(parent, "Text " + value, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = serif ? display : font; text.text = value ?? ""; text.fontSize = size; text.color = ThemeText(color);
            text.fontStyle = serif ? FontStyles.Bold : FontStyles.Normal;
            text.alignment = alignment; text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis; text.lineSpacing = 3; text.characterSpacing = .1f; return text;
        }

        Color ThemeText(Color value)
        {
            if (!PaperMenu) return value;
            if (value == White) return PaperInk;
            if (value == Quiet) return PaperQuiet;
            if (value == Gold) return PaperGold;
            if (value == Mint) return PaperTeal;
            return value;
        }

        Button B(Transform parent, string label, float x, float y, float width, float height, Action action, bool enabled = true, bool primary = false)
        {
            var r = Panel(parent, string.IsNullOrEmpty(label) ? "Item selection" : label, x, y, width, height,
                primary ? new Color(.89f, .85f, .72f, .98f) : Glass, true);
            var image = r.GetComponent<Image>(); image.raycastTarget = true;
            var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = image; b.interactable = enabled;
            var colors = b.colors; colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, .98f, .89f); colors.selectedColor = new Color(.97f, .94f, .84f);
            colors.pressedColor = new Color(.76f, .82f, .82f); colors.disabledColor = new Color(.71f, .74f, .73f, .48f);
            colors.fadeDuration = .12f; b.colors = colors;
            if (!string.IsNullOrEmpty(label))
                Text(r, label, 12, 0, width - 24, height, Mathf.Min(24, height * .46f),
                    primary ? PaperInk : White, TextAlignmentOptions.Midline, true);
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
            var g = r.gameObject.AddComponent<GospelLuminousGlyph>(); g.Kind = kind; g.color = ThemeText(color); g.raycastTarget = false; return r;
        }

        Image Bar(Transform parent, string name, float x, float y, float width, float height, Color color, float amount)
        {
            var track = Panel(parent, name, x, y, width, height, PaperMenu
                ? new Color(.72f, .77f, .73f, .48f) : new Color(.81f, .84f, .85f, .29f));
            var fill = Rect(track, "Current value", 0, 0, width, height).gameObject.AddComponent<Image>();
            if (solidSprite == null)
            {
                var white = Texture2D.whiteTexture;
                solidSprite = Sprite.Create(white, new Rect(0, 0, white.width, white.height), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
                solidSprite.hideFlags = HideFlags.HideAndDontSave;
            }
            fill.sprite = solidSprite;
            fill.color = ThemeText(color); fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal;
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
            Rule(holder, width - 4, 0, 2, height, PaperMenu
                ? new Color(.54f, .53f, .45f, .17f) : new Color(.86f, .88f, .86f, .17f));
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
            Panel(r, "Volume track", 0, 21, width, 7, new Color(.72f, .77f, .73f, .54f));
            var area = Rect(r, "Volume fill area", 0, 21, width - 22, 7);
            var fill = Panel(area, "Volume current", 0, 0, width - 22, 7, PaperTeal);
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = fill.offsetMax = Vector2.zero;
            var handles = Rect(r, "Volume handle area", 11, 0, width - 22, 51);
            var handle = Panel(handles, "Volume handle", 0, 11, 27, 27, new Color(.98f, .97f, .91f), true);
            handle.pivot = new Vector2(.5f, .5f); handle.anchorMin = Vector2.zero; handle.anchorMax = new Vector2(0, 1);
            handle.sizeDelta = new Vector2(27, -20); handle.anchoredPosition = Vector2.zero;
            handle.GetComponent<Image>().raycastTarget = true;
            slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = 0; slider.maxValue = 1; slider.SetValueWithoutNotify(current); return slider;
        }
    }
}
