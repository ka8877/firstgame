using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FantasyCards.Core;
using FantasyCards.Data;
using FirstGame.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using Unity.Cinemachine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    public sealed partial class FantasyCardsApp : MonoBehaviour
    {
        private const string Slot = "fantasy-battle-classic-v3", DeckSlot = "fantasy-deck-classic-v3", GameId = "starlight-oath";
        private readonly Color ink = new Color(.030f, .040f, .085f, .98f);
        private readonly Color gold = new Color(.96f, .80f, .48f);
        private readonly Color teal = new Color(.40f, .86f, .80f);
        private readonly List<string> customDeck = CardCatalog.DefaultDeck();
        private readonly Dictionary<string, Sprite> authoredIllustrations = new Dictionary<string, Sprite>();
        private BattleEngine engine;
        private RectTransform root;
        private Canvas canvas;
        private TMP_FontAsset font;
        private BgmPlayer music;
        private Action redraw;
        private Vector2 lastViewport;
        private int collectionPage;
        private bool Compact => canvas != null && (canvas.pixelRect.width < 1400 || canvas.pixelRect.width / Mathf.Max(1, canvas.pixelRect.height) > 1.95f);
        private readonly Color ivory = new Color(.96f, .96f, 1f);
        private readonly Color slate = new Color(.085f, .12f, .215f, .98f);
        private int selectedCard = -1, selectedAttacker = -1;
        private string inspectedId = "dawn_scout", message = "판타지마스터즈에 오신 것을 환영합니다.";
        private string saveError = "";
        private bool locked, battleScreen;
        private AudioSource audioSource;
        private AudioClip coinSound, playSound;
        private TMP_Text effectLabel;
        private TMP_Text musicToggleLabel, musicVolumeLabel;
        private float nextMusicSettingsRefresh;
        private GameObject effectObject;
        private RectTransform effectCoin;
        private string screenRoute;
        private RuleSet authoredRules;
        private CinemachineCamera battleCamera;
        private PlayableDirector revealDirector;
        private Material dawnMiniature, tideMiniature;
        private readonly List<GameObject> worldUnits = new List<GameObject>();

        private void Start()
        {
            var authored = Resources.LoadAll<CardAsset>("Data/Cards");
            if (authored.Length > 0) CardCatalog.ApplyAuthoredDefinitions(authored.Select(asset => asset.ToDefinition()));
            foreach (var asset in authored) if (asset.illustration != null) authoredIllustrations[asset.stableId] = asset.illustration;
            var ruleAsset = Resources.Load<RuleSetAsset>("Data/Rules"); authoredRules = ruleAsset != null ? ruleAsset.CreateRuntimeRules() : new RuleSet();
            font = Resources.Load<TMP_FontAsset>("Fonts/Korean") ?? TMP_Settings.defaultFontAsset;
            // Bundled instrumental playback is supplied by the cross-platform music bridge.
            music = null; InitializeOriginalMusic();
            var c = new GameObject("Fantasy Masters Client", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = c.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            var scaler = c.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            root = c.GetComponent<RectTransform>();
            if (FindFirstObjectByType<EventSystem>() == null) new GameObject("UI Input", typeof(EventSystem), typeof(InputSystemUIInputModule));
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false; audioSource.volume = .24f;
            coinSound = MakeTone(960f, .13f); playSound = MakeTone(260f, .15f);
            inspectedId = CardCatalog.All.First(card => card.kind == CardKind.Unit).id;
            SetupWorld(); LoadDeck(); LoadDemoSelection(); ShowTitle();
            FmBattleAudio.Initialize();
        }
        private void Update()
        {
            if (musicVolumeLabel != null && Time.unscaledTime >= nextMusicSettingsRefresh)
            {
                nextMusicSettingsRefresh = Time.unscaledTime + .2f;
                musicVolumeLabel.text = "음량 " + Mathf.RoundToInt(FmOriginalMusic.Volume * 100) + "%";
                if (musicToggleLabel != null) musicToggleLabel.text = FmOriginalMusic.Muted ? "음악 켜기" : "음악 끄기";
            }
            if (music != null && ((Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) ||
                (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame))) music.NotifyUserGesture();
            if (canvas != null && !locked && redraw != null && Vector2.Distance(lastViewport, canvas.pixelRect.size) > 1)
            { lastViewport = canvas.pixelRect.size; redraw(); }
            if (!locked && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (battleScreen) LeaveBattle(ShowTitle); else ShowTitle();
            }
        }
        private void Clear()
        {
            HideTooltip();
            musicToggleLabel = musicVolumeLabel = null;
            foreach (Transform child in root) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            effectLabel = null; effectObject = null; effectCoin = null;
            string route = redraw?.Method.Name ?? "Transition";
            if (screenRoute != route)
            {
                screenRoute = route;
                var fade = root.GetComponent<UiCanvasFade>();
                if (fade == null) fade = root.gameObject.AddComponent<UiCanvasFade>();
                fade.Begin();
            }
            lastViewport = canvas.pixelRect.size;
        }
        private void ShowTitle()
        {
            ShowRevivalTitle();
        }
        private void ShowMusicSettings(Action back)
        {
            originalMusicMuted = FmOriginalMusic.Muted;
            originalMusicVolume = FmOriginalMusic.Volume;
            redraw = () => ShowMusicSettings(back); Clear(); Backdrop(true);
            var panel = Panel(root, .13f, .15f, .87f, .85f, ink); Frame(panel, gold, 1.5f);
            Text(panel, "판타지마스터즈 음악", .07f, .74f, .93f, .92f, 43, ivory, TextAlignmentOptions.Center);
            Text(panel, "SOUND-I 원작곡 발췌 · S.I.D-Sound 전투곡", .07f, .62f, .93f, .75f, 24, gold, TextAlignmentOptions.Center);
            var toggle = Button(panel, originalMusicMuted ? "음악 켜기" : "음악 끄기", .12f, .40f, .46f, .59f,
                () => { FmOriginalMusic.SetMuted(!FmOriginalMusic.Muted); ShowMusicSettings(back); }, slate, 26);
            musicToggleLabel = toggle.GetComponentInChildren<TMP_Text>();
            musicVolumeLabel = Text(panel, "음량 " + Mathf.RoundToInt(originalMusicVolume * 100) + "%", .49f, .43f, .88f, .57f, 28, ivory, TextAlignmentOptions.Center);
            Button(panel, "음량 -", .12f, .19f, .46f, .38f,
                () => { SetOriginalVolume(FmOriginalMusic.Volume - .15f); ShowMusicSettings(back); }, slate, 25);
            Button(panel, "음량 +", .53f, .19f, .87f, .38f,
                () => { SetOriginalVolume(FmOriginalMusic.Volume + .15f); ShowMusicSettings(back); }, slate, 25);
            Button(root, "돌아가기", .36f, .025f, .64f, .14f, back, gold, 26);
        }
        private void Backdrop(bool calm)
        {
            RevivalBackdrop(calm);
        }
        private void Ornament(Transform parent, Color color, bool card = false)
        {
            // One quiet accent replaces the old repeated engraved corners.
            var accent = Panel(parent, card ? .06f : .035f, .982f, card ? .24f : .17f, .987f,
                new Color(color.r, color.g, color.b, Mathf.Min(.60f, color.a)));
            accent.gameObject.name = "Modern surface accent";
        }
        private void StartBattle(uint seed)
        {
            if (!ValidDeck(customDeck)) { message = "50장 덱과 속성별 지형 구성을 확인하세요."; ShowCollection(true); return; }
            engine = new BattleEngine(seed, authoredRules, playerDeck: CardCatalog.DefaultDeck()); selectedCard = selectedAttacker = -1;
            engine.ConfirmOpeningHand(0, out _);
            message = "지형과 유닛을 준비하고 준비 완료를 누르세요. 전열 순서대로 자동 전투합니다.";
            Save(); locked = true; ShowBattle(); StartCoroutine(RevealBattle());
        }
        private IEnumerator RevealBattle()
        {
            revealDirector.time = 0; revealDirector.Play();
            while (revealDirector.state == PlayState.Playing) yield return null;
            locked = false; ShowBattle();
        }
        private void Continue()
        {
            if (!PersistentSaves.TryLoad(Slot, GameId, 1, CardCatalog.Version, out string payload, out string error)) { message = "불러오기 실패: " + error; ShowTitle(); return; }
            try { engine = new BattleEngine(JsonUtility.FromJson<BattleState>(payload)); selectedCard = selectedAttacker = -1; RestoreDemoSelection(); message = "저장한 전투와 다음 코인 상태를 복원했습니다."; if (engine.State.openingHandOpen) ShowOpeningHand(); else { ShowBattle(); PumpAi(); } }
            catch (Exception e) { message = "저장을 보존했습니다. 불러오기 실패: " + e.Message; ShowTitle(); }
        }
        private bool Save()
        {
            if (engine == null) return true;
            if (!PersistentSaves.TrySave(Slot, GameId, 1, CardCatalog.Version, JsonUtility.ToJson(engine.State), out string error)) { saveError = "저장 실패: " + error; return false; }
            saveError = "";
            return true;
        }
        private void LeaveBattle(Action next)
        {
            if (Save()) next(); else ShowBattle();
        }
        private string StatusMessage() { return string.IsNullOrEmpty(saveError) ? message : message + " · " + saveError; }
        private void Act(Func<bool> action)
        {
            if (locked) return;
            var before = engine.State.Copy(); List<BattlePlaybackFrame> frames;
            bool changed;
            engine.BeginPlaybackCapture();
            try { changed = action(); }
            finally { frames = engine.EndPlaybackCapture(); }
            if (!changed) { FmBattleAudio.Play("error"); ShowBattle(); return; }
            selectedCard = selectedAttacker = -1; Save(); locked = true;
            StartCoroutine(AnimateAndContinue(before, frames));
        }
        private IEnumerator AnimateAndContinue(BattleState before, List<BattlePlaybackFrame> frames)
        {
            yield return ReplayBattleFrames(before, frames);
        }
        private void PumpAi()
        {
            if (locked || !battleScreen || engine.State.phase == BattlePhase.Complete) return;
            var s = engine.State; bool aiAction = engine.PreparingPlayer == 1;
            if (!aiAction) return;
            var before = s.Copy(); List<BattlePlaybackFrame> frames; bool changed;
            engine.BeginPlaybackCapture();
            try { changed = BattleAi.Step(engine, 1, out message); }
            finally { frames = engine.EndPlaybackCapture(); }
            if (changed) { Save(); locked = true; StartCoroutine(AnimateAndContinue(before, frames)); }
        }
        private void ShowEffect(string text, Color color, bool coin = false, int result = 0)
        {
            if (effectObject != null) Destroy(effectObject);
            effectCoin = null;
            var panel = Panel(root, .245f, .43f, .755f, .61f, new Color(.025f, .035f, .075f, .985f)); effectObject = panel.gameObject;
            Frame(panel, new Color(color.r, color.g, color.b, .8f), 1.7f);
            if (coin)
            {
                var holder = Panel(panel, .025f, .13f, .155f, .87f, Color.clear);
                effectCoin = Panel(holder, 0, 0, 1, 1, result > 0 ? gold : new Color(.46f, .54f, .67f));
                effectCoin.GetComponent<UiRoundedImage>().Radius = 1000;
                var fit = effectCoin.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 1;
                Text(effectCoin, result > 0 ? "앞" : "뒤", .02f, .02f, .98f, .98f, 25, ink, TextAlignmentOptions.Center);
                Frame(effectCoin, ivory, 2);
            }
            effectLabel = Text(panel, text, coin ? .18f : .04f, .09f, .96f, .91f, 28, color, TextAlignmentOptions.Center);
        }
        private void ShowResult()
        {
            var s = engine.State;
            RecordLocalBattleResult();
            var shade = Panel(root, 0, 0, 1, 1, new Color(.01f, .02f, .04f, .73f)); shade.GetComponent<Image>().raycastTarget = true;
            var panel = Panel(root, Compact ? .10f : .275f, Compact ? .16f : .30f, Compact ? .90f : .79f, Compact ? .84f : .77f, new Color(.025f, .045f, .085f, .99f));
            Frame(panel, gold, 1); panel.gameObject.AddComponent<UiPanelEntrance>();
            string result = s.winner == 0 ? "승리하였습니다" : s.winner == 1 ? "패배하였습니다" : "무승부입니다";
            Text(panel, result, .06f, .66f, .94f, .87f, 42, gold, TextAlignmentOptions.Center);
            Text(panel, (s.winner == 0 ? "승리" : s.winner == 1 ? "패배" : "무승부") + " · " + s.turn + "턴\n지형과 유닛의 균형, 방어에 남길 동료를 다시 생각해 보세요.", .08f, .38f, .92f, .64f, 24, Color.white, TextAlignmentOptions.Center);
            if (s.winner == 0) RecordDemoVictory();
            Button(panel, FmScenarioCatalog.StageForBattle(s) != null ? (s.winner == 0 ? "시나리오 계속" : "다시 도전") : s.winner == 0 && s.encounterIndex >= 0 && s.encounterIndex < 2 ? "다음 결투" : "다시 도전", .09f, .10f, .48f, .28f,
                RestorationNextBattle, gold, 24, !locked);
            Button(panel, "대기실로", .53f, .10f, .91f, .28f, () => LeaveBattle(ShowLobby), teal, 24, !locked);
        }
        private void ShowTransfer()
        {
            battleScreen = false; redraw = null; Clear(); Backdrop(true); Panel(root, .06f, .06f, .94f, .94f, ink);
            Text(root, "전투 저장 옮기기", .09f, .82f, .85f, .90f, 42, gold);
            Text(root, "웹에서 내보낸 저장 문자열을 PC에서도 가져올 수 있습니다.\n아래 내용을 파일로 보관하거나, 다른 기기에서 복사해 붙여넣으세요.", .09f, .69f, .89f, .81f, 26, Color.white);
            var area = Panel(root, .09f, .30f, .91f, .66f, new Color(.08f, .12f, .18f));
            area.GetComponent<Image>().raycastTarget = true;
            var input = area.gameObject.AddComponent<TMP_InputField>(); var viewport = Panel(area, .02f, .02f, .98f, .98f, Color.clear); viewport.gameObject.AddComponent<RectMask2D>();
            var text = Text(viewport, "", 0, 0, 1, 1, 18, Color.white, TextAlignmentOptions.TopLeft); input.textViewport = viewport; input.textComponent = text; input.lineType = TMP_InputField.LineType.MultiLineNewline;
            var status = Text(root, "저장을 가져오면 현재 전투 저장이 교체됩니다. 유효하지 않은 저장은 거부합니다.", .09f, .08f, .91f, .14f, 22, teal);
            Button(root, "내보내기", .09f, .15f, .28f, .28f, () => { if (PersistentSaves.TryExport(Slot, out string data, out string error)) { input.text = data; status.text = "저장 내용을 표시했습니다. 전체 선택 후 복사하세요."; } else status.text = error; }, gold);
            Button(root, "검증 후 가져오기", .31f, .15f, .58f, .28f, () => { bool ok = PersistentSaves.TryImport(Slot, GameId, 1, CardCatalog.Version, input.text, ValidatePayload, out string error); if (ok) engine = null; status.text = ok ? "가져오기 완료. 타이틀에서 이어하기를 누르세요." : "가져오기 실패: " + error; }, teal);
            Button(root, "돌아가기", .70f, .15f, .91f, .28f, profileTransferReturn ?? ShowTitle, new Color(.2f, .25f, .34f));
        }
        private static bool ValidatePayload(string payload) { try { BattleEngine.Validate(JsonUtility.FromJson<BattleState>(payload)); return true; } catch { return false; } }
        [Serializable] private sealed class DeckSave { public List<string> cards = new List<string>(); }
        private static bool ValidDeck(List<string> deck)
        {
            try { DemoCatalog.ValidatePlayableDeck(deck); return true; } catch { return false; }
        }
        private bool SaveDeck()
        {
            if (!PersistentSaves.TrySave(DeckSlot, GameId, 1, CardCatalog.Version, JsonUtility.ToJson(new DeckSave { cards = customDeck }), out string error))
            { message = "덱 저장 실패: " + error; return false; }
            message = "편집한 덱을 저장했습니다."; return true;
        }
        private void LoadDeck()
        {
            if (!PersistentSaves.Exists(DeckSlot)) return;
            if (!PersistentSaves.TryLoad(DeckSlot, GameId, 1, CardCatalog.Version, out string payload, out string error)) { message = "기존 덱 저장을 보존했습니다: " + error; return; }
            try { var deck = JsonUtility.FromJson<DeckSave>(payload); if (!ValidDeck(deck.cards)) throw new ArgumentException("덱 구성이 잘못되었습니다."); customDeck.Clear(); customDeck.AddRange(deck.cards); } catch (Exception e) { message = "기존 덱 저장을 보존했습니다: " + e.Message; }
        }
        private void DrawCard(Transform parent, CardDefinition card, float x0, float y0, float x1, float y1, bool selected, Action onClick, bool interactable)
        {
            DrawArcanaCard(parent, card, x0, y0, x1, y1, selected, onClick, interactable);
        }
        private void Artwork(Transform parent, CardDefinition d, float x0, float y0, float x1, float y1)
        {
            if (FmStreamingCardArt.IsStreamed(d.artResource))
            { RenewalSourcePrint(parent, d.artResource, x0, y0, x1, y1, "Original streamed illustration", new Rect(.13f, .36f, .805f, .56f)); return; }
            if (!string.IsNullOrEmpty(d.artResource) && d.artResource.StartsWith("Art/ClassicCards/", StringComparison.Ordinal))
            {
                var original = Resources.Load<Texture2D>(d.artResource);
                if (original != null)
                {
                    // Keep the entire illustration window visible. Fit it instead
                    // of cropping heads or stretching its original proportions.
                    FittedImage(parent, original, new Rect(.13f, .36f, .805f, .56f), x0, y0, x1, y1, "Original card illustration");
                    return;
                }
            }
            authoredIllustrations.TryGetValue(d.id, out var sprite);
            sprite = sprite != null ? sprite : Resources.Load<Sprite>(d.artResource);
            if (sprite != null)
            {
                var rect = sprite.rect; var texture = sprite.texture;
                ImageFrame(parent, texture, new Rect((rect.x + .5f) / texture.width, (rect.y + .5f) / texture.height, (rect.width - 1) / texture.width, (rect.height - 1) / texture.height), x0, y0, x1, y1, new Vector2(.5f, .66f), true);
            }
            else Panel(parent, x0, y0, x1, y1, slate);
        }
        private void ImageFrame(Transform parent, Texture texture, Rect uv, float x0, float y0, float x1, float y1, Vector2 focus, bool rounded = false)
        {
            if (rounded)
            {
                var clip = Panel(parent, x0, y0, x1, y1, Color.white);
                clip.gameObject.name = "Rounded artwork window";
                var shape = clip.GetComponent<UiRoundedImage>(); shape.TopTint = shape.BottomTint = Color.white; shape.Radius = 10;
                clip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                parent = clip; x0 = y0 = 0; x1 = y1 = 1;
            }
            var go = new GameObject("Illustration", typeof(RectTransform), typeof(RawImage));
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false); Anchor(rt, x0, y0, x1, y1);
            var image = go.GetComponent<RawImage>(); image.texture = texture; image.raycastTarget = false;
            var crop = go.AddComponent<IllustrationCrop>(); crop.SourceUv = uv; crop.Focus = focus; crop.Refresh();
        }
        private void Gradient(Transform parent, float x0, float y0, float x1, float y1, Color start, Color end, bool horizontal = false)
        {
            var go = new GameObject("Artwork shade", typeof(RectTransform), typeof(UiGradient));
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false); Anchor(rt, x0, y0, x1, y1);
            var gradient = go.GetComponent<UiGradient>(); gradient.StartColor = start; gradient.EndColor = end; gradient.Horizontal = horizontal; gradient.raycastTarget = false; gradient.SetAllDirty();
        }
        private void FittedImage(Transform parent, Texture texture, Rect uv, float x0, float y0, float x1, float y1, string name)
        {
            var bounds = new GameObject(name + " bounds", typeof(RectTransform)).GetComponent<RectTransform>();
            bounds.SetParent(parent, false); Anchor(bounds, x0, y0, x1, y1);
            var image = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.transform.SetParent(bounds, false); Anchor((RectTransform)image.transform, 0, 0, 1, 1);
            image.texture = texture; image.uvRect = uv; image.raycastTarget = false;
            var fit = image.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = texture.width * uv.width / (texture.height * uv.height);
        }
        private void ElementIcon(Transform parent, Element element, float x0, float y0, float x1, float y1)
        {
            var icon = new GameObject(CardCatalog.ElementName(element) + " emblem", typeof(RectTransform), typeof(UiElementGlyph)).GetComponent<UiElementGlyph>();
            icon.transform.SetParent(parent, false); Anchor((RectTransform)icon.transform, x0, y0, x1, y1);
            icon.ElementIndex = (int)element; icon.color = ElementColor(element); icon.raycastTarget = false; icon.SetAllDirty();
        }
        private TMP_Text ReadableText(Transform parent, string value, float x0, float y0, float x1, float y1, float size, Color tint, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var label = Text(parent, value, x0, y0, x1, y1, size, tint, alignment);
            label.overflowMode = TextOverflowModes.Overflow;
            return label;
        }
        private void Frame(RectTransform rect, Color color, float pixels)
        {
            var existing = rect.Find("Fine rounded frame");
            var frame = existing != null ? (RectTransform)existing : new GameObject("Fine rounded frame", typeof(RectTransform), typeof(UiRoundedImage)).GetComponent<RectTransform>();
            if (existing == null) { frame.SetParent(rect, false); Anchor(frame, 0, 0, 1, 1); }
            frame.SetAsLastSibling();
            var shape = frame.GetComponent<UiRoundedImage>(); shape.Fill = false; shape.raycastTarget = false;
            shape.Radius = rect.GetComponent<UiRoundedImage>()?.Radius ?? 10f; shape.BorderColor = new Color(color.r, color.g, color.b, pixels >= 2.5f ? color.a : color.a * .48f); shape.BorderWidth = pixels >= 2.5f ? 2 : Mathf.Min(1, pixels); shape.SetAllDirty();
        }
        private RectTransform Panel(Transform parent, float x0, float y0, float x1, float y1, Color color)
        {
            var go = new GameObject("Panel", typeof(RectTransform), typeof(UiRoundedImage));
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false); Anchor(rt, x0, y0, x1, y1);
            var image = go.GetComponent<UiRoundedImage>(); image.color = color; image.raycastTarget = false;
            image.Radius = 18; image.TopTint = new Color(1.025f, 1.035f, 1.045f, 1); image.BottomTint = new Color(.98f, .99f, 1f, 1);
            return rt;
        }
        private TMP_Text Text(Transform parent, string value, float x0, float y0, float x1, float y1, float size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            float authoredSize = size;
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)); var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false); Anchor(rt, x0, y0, x1, y1);
            float scale = Mathf.Sqrt(Mathf.Max(1, canvas.pixelRect.width) / 1920f * Mathf.Max(1, canvas.pixelRect.height) / 1080f);
            bool small = Compact && canvas.pixelRect.height < 600;
            bool heading = size >= 34; // Font role follows authored hierarchy, not the mobile pixel floor.
            if (small) size = Mathf.Max(size, 13f / scale);
            var text = go.GetComponent<TextMeshProUGUI>(); text.font = heading ? Resources.Load<TMP_FontAsset>("Fonts/Display") ?? font : font; text.text = value; text.fontSize = size; text.color = color; text.alignment = alignment; text.fontStyle = FontStyles.Normal; text.raycastTarget = false; text.enableAutoSizing = true; text.fontSizeMin = small ? Mathf.Max(size * .8f, 12f / scale) : size * .8f; text.fontSizeMax = size; text.overflowMode = TextOverflowModes.Truncate;
            go.AddComponent<UiCanvasTextSize>().Configure(authoredSize);
            return text;
        }
        private Button Button(Transform parent, string label, float x0, float y0, float x1, float y1, Action action, Color color, float size = 22, bool enabled = true)
        {
            var panel = Panel(parent, x0, y0, x1, y1, color); panel.gameObject.name = "Button " + label; panel.GetComponent<Image>().raycastTarget = true;
            panel.GetComponent<UiRoundedImage>().Radius = 18;
            var b = panel.gameObject.AddComponent<Button>(); b.interactable = enabled;
            var colors = b.colors; colors.fadeDuration = .14f;
            colors.highlightedColor = new Color(1.16f, 1.18f, 1.20f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(.43f, .48f, .54f, .65f);
            colors.pressedColor = new Color(.80f, .86f, .90f); b.colors = colors;
            var shadow = panel.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(.005f, .015f, .035f, .27f); shadow.effectDistance = new Vector2(0, -4);
            Frame(panel, color == gold ? new Color(1, .94f, .76f, .65f) : new Color(.72f, .84f, .94f, .28f), 1);
            b.onClick.AddListener(() => { music?.NotifyUserGesture(); FmBattleAudio.Play(ButtonCue(label)); action(); });
            FmBattleAudio.AttachHover(panel.gameObject);
            var hover = panel.gameObject.AddComponent<UiCardHover>(); hover.HoverScale = 1.025f; hover.HoverLift = 3;
            var text = Text(panel, label, .04f, .07f, .96f, .93f, size, color == gold || color == teal ? ink : ivory, TextAlignmentOptions.Center);
            text.font = Resources.Load<TMP_FontAsset>("Fonts/Display") ?? font;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            BindTooltip(panel.gameObject, label, ButtonHelp(label));
            return b;
        }
        private static void Anchor(RectTransform r, float x0, float y0, float x1, float y1) { r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1); r.offsetMin = r.offsetMax = Vector2.zero; }
        private static string PhaseName(BattlePhase phase) { switch (phase) { case BattlePhase.Main: return "공격 준비"; case BattlePhase.DefensePreparation: return "방어 준비"; default: return "결투 종료"; } }
        private void SetupWorld()
        {
            var camera = Camera.main;
            if (camera == null) { var go = new GameObject("Battle Camera", typeof(Camera)); camera = go.GetComponent<Camera>(); go.tag = "MainCamera"; }
            if (FindFirstObjectByType<AudioListener>() == null) camera.gameObject.AddComponent<AudioListener>();
            camera.transform.position = new Vector3(0, 11, -13); camera.transform.LookAt(new Vector3(0, 0, 1)); camera.fieldOfView = 47; camera.backgroundColor = new Color(.025f, .04f, .08f); camera.clearFlags = CameraClearFlags.SolidColor;
            if (camera.GetComponent<CinemachineBrain>() == null) camera.gameObject.AddComponent<CinemachineBrain>();
            battleCamera = new GameObject("Cinemachine Battle Camera", typeof(CinemachineCamera)).GetComponent<CinemachineCamera>();
            battleCamera.transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation); battleCamera.Lens.FieldOfView = 47;
            revealDirector = gameObject.AddComponent<PlayableDirector>(); revealDirector.playOnAwake = false; revealDirector.extrapolationMode = DirectorWrapMode.None;
            var reveal = ScriptableObject.CreateInstance<TimelineAsset>(); var revealTrack = reveal.CreateTrack<PlayableTrack>(null, "판타지마스터즈 전장 도입");
            var clip = revealTrack.CreateClip<BattleRevealClip>(); clip.start = 0; clip.duration = 1.2;
            var revealClip = (BattleRevealClip)clip.asset; revealClip.cameraTransform.exposedName = "battle-camera"; revealDirector.playableAsset = reveal; revealDirector.SetReferenceValue("battle-camera", battleCamera.transform);
            dawnMiniature = Material(gold); tideMiniature = Material(teal);
            if (FindFirstObjectByType<Light>() == null) { var light = new GameObject("Moonlight", typeof(Light)).GetComponent<Light>(); light.type = LightType.Directional; light.color = new Color(.63f, .77f, 1f); light.intensity = 1.6f; light.transform.rotation = Quaternion.Euler(42, -25, 0); }
            RenderSettings.ambientLight = new Color(.20f, .27f, .39f);
            if (GameObject.Find("StarfallArena") == null)
            {
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder); floor.name = "Runtime arena fallback"; floor.transform.localScale = new Vector3(13, .3f, 13); floor.transform.position = new Vector3(0, -.3f, 0); floor.GetComponent<Renderer>().material = Material(new Color(.075f, .12f, .17f));
                for (int i = 0; i < 10; i++) { float a = i * Mathf.PI * 2 / 10; var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder); pillar.name = "Arena pillar"; pillar.transform.position = new Vector3(Mathf.Sin(a) * 6, .65f, Mathf.Cos(a) * 6); pillar.transform.localScale = new Vector3(.45f, .8f, .45f); pillar.GetComponent<Renderer>().material = Material(new Color(.17f, .23f, .3f)); }
            }
        }
        private void UpdateWorld()
        {
            foreach (var unit in worldUnits) Destroy(unit); worldUnits.Clear(); if (engine == null || !battleScreen) return;
            for (int player = 0; player < 2; player++) foreach (var u in engine.State.players[player].units)
            {
                var piece = GameObject.CreatePrimitive(PrimitiveType.Capsule); piece.name = "Unit miniature " + u.instanceId; piece.transform.position = new Vector3((u.lane - 1) * 2.8f, .8f, player == 0 ? -1.7f : 1.7f); piece.transform.localScale = new Vector3(.55f, .8f, .55f); piece.GetComponent<Renderer>().sharedMaterial = CardCatalog.Get(u.definitionId).element == Element.Dawn ? dawnMiniature : tideMiniature; worldUnits.Add(piece);
            }
        }
        private static Material Material(Color color) { var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"); var m = new Material(shader); m.color = color; return m; }
        private static AudioClip MakeTone(float hz, float duration)
        {
            const int sampleRate = 22050; int count = (int)(sampleRate * duration); var samples = new float[count];
            for (int i = 0; i < count; i++) { float t = (float)i / sampleRate; samples[i] = Mathf.Sin(2 * Mathf.PI * hz * t) * Mathf.Exp(-t * 24) * .3f; }
            var clip = AudioClip.Create("Starlight chime " + hz, count, 1, sampleRate, false); clip.SetData(samples, 0); return clip;
        }
        private void OnApplicationPause(bool pause) { if (pause && engine != null) Save(); }
        private void OnApplicationQuit() { if (engine != null) Save(); }
    }
}
