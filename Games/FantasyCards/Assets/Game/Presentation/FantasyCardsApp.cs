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
        private readonly Color ink = new Color(.055f, .065f, .135f, .93f);
        private readonly Color gold = new Color(.94f, .80f, .52f);
        private readonly Color teal = new Color(.49f, .86f, .88f);
        private readonly List<string> customDeck = CardCatalog.DefaultDeck();
        private readonly Dictionary<string, Sprite> authoredIllustrations = new Dictionary<string, Sprite>();
        private BattleEngine engine;
        private RectTransform root;
        private Canvas canvas;
        private TMP_FontAsset font;
        private Texture2D titleArt, battleArt;
        private BgmPlayer music;
        private Action redraw;
        private Vector2 lastViewport;
        private int collectionPage;
        private bool Compact => canvas != null && (canvas.pixelRect.width < 1400 || canvas.pixelRect.width / Mathf.Max(1, canvas.pixelRect.height) > 1.95f);
        private readonly Color ivory = new Color(.99f, .96f, .89f);
        private readonly Color slate = new Color(.16f, .18f, .30f, .94f);
        private int selectedCard = -1, selectedAttacker = -1;
        private string inspectedId = "dawn_scout", message = "지형을 전개하고, 서약을 지킬 동료를 불러오세요.";
        private string saveError = "";
        private bool locked, battleScreen;
        private AudioSource audioSource;
        private AudioClip coinSound, playSound;
        private TMP_Text effectLabel;
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
            titleArt = Resources.Load<Texture2D>("Art/title-hero");
            battleArt = Resources.Load<Texture2D>("Art/battle-background");
            music = BgmPlayer.Attach(gameObject, "Audio/starlight-overture", "FC.music", .6f);
            var c = new GameObject("Starlight UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = c.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            var scaler = c.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            root = c.GetComponent<RectTransform>();
            if (FindFirstObjectByType<EventSystem>() == null) new GameObject("UI Input", typeof(EventSystem), typeof(InputSystemUIInputModule));
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false; audioSource.volume = .24f;
            coinSound = MakeTone(960f, .13f); playSound = MakeTone(260f, .15f);
            inspectedId = CardCatalog.All.First(card => card.kind == CardKind.Unit).id;
            SetupWorld(); LoadDeck(); LoadDemoSelection(); ShowTitle();
        }
        private void Update()
        {
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
            battleScreen = false; redraw = ShowTitle; Clear(); UpdateWorld();
            if (titleArt != null) ImageFrame(root, titleArt, new Rect(0, 0, 1, 1), 0, 0, 1, 1, new Vector2(.65f, .55f));
            Gradient(root, 0, 0, .83f, 1, new Color(.014f, .025f, .047f, .99f), new Color(.014f, .025f, .047f, 0), true);
            Gradient(root, 0, 0, 1, .45f, new Color(.014f, .025f, .047f, .97f), Color.clear);
            bool compact = Compact;
            Text(root, "FANTASY MASTERS  /  UNOFFICIAL CLASSIC PLAYTEST", .055f, .84f, .57f, .91f, compact ? 24 : 21, gold);
            Text(root, "판타지마스터즈", .05f, .665f, .61f, .83f, compact ? 80 : 96, ivory);
            Panel(root, .058f, .638f, .13f, .641f, gold);
            Text(root, "개인용 규칙 재현 · 비공식 체험판", .057f, .57f, .62f, .63f, compact ? 28 : 27, new Color(.84f, .87f, .88f));
            float top = compact ? .54f : .52f, bottom = compact ? .405f : .425f;
            Button(root, "새로운 결투", .058f, bottom, .275f, top, ShowLeaderSelection, gold, 28);
            Button(root, "이어하기", .287f, bottom, .49f, top, Continue, slate, 27, PersistentSaves.Exists(Slot));
            bottom = compact ? .25f : .323f; top = compact ? .385f : .405f;
            Button(root, "카드 도감", .058f, bottom, .275f, top, () => ShowCollection(false), slate, 25);
            Button(root, "덱 구성 · " + customDeck.Count + "장", .287f, bottom, .49f, top, () => ShowCollection(true), slate, 25);
            Button(root, "저장 내보내기 / 가져오기", .058f, compact ? .095f : .225f, .49f, compact ? .23f : .303f, ShowTransfer, slate, 23);
            if (!compact)
            {
                Text(root, CardCatalog.All.Length + " CARDS  /  7 ELEMENTS", .735f, .13f, .96f, .175f, 20, gold, TextAlignmentOptions.MidlineRight);
                Text(root, "일곱 속성으로 펼치는 결투", .66f, .075f, .96f, .13f, 30, ivory, TextAlignmentOptions.MidlineRight);
            }
            Text(root, StatusMessage(), .058f, .018f, .96f, .073f, 19, new Color(.78f, .81f, .91f));
            Button(root, "음악", .84f, .84f, .965f, .965f, () => ShowMusicSettings(ShowTitle), slate, 24);
        }
        private void ShowMusicSettings(Action back)
        {
            redraw = () => ShowMusicSettings(back); Clear(); Backdrop(true);
            var panel = Panel(root, .13f, .15f, .87f, .85f, ink); Frame(panel, new Color(gold.r, gold.g, gold.b, .7f), 1.5f);
            Text(panel, "별빛의 선율", .07f, .74f, .93f, .92f, 43, ivory, TextAlignmentOptions.Center);
            Text(panel, "별빛의 회상 · 현악과 하프의 테마", .07f, .62f, .93f, .75f, 24, gold, TextAlignmentOptions.Center);
            Button(panel, music != null && music.Muted ? "음악 켜기" : "음악 끄기", .12f, .40f, .46f, .59f,
                () => { if (music != null) music.SetMuted(!music.Muted); ShowMusicSettings(back); }, slate, 26);
            Text(panel, "음량 " + Mathf.RoundToInt((music != null ? music.Volume : .6f) * 100) + "%", .49f, .43f, .88f, .57f, 28, ivory, TextAlignmentOptions.Center);
            Button(panel, "음량 -", .12f, .19f, .46f, .38f,
                () => { music?.SetVolume(Mathf.Max(0, music.Volume - .15f)); ShowMusicSettings(back); }, slate, 25);
            Button(panel, "음량 +", .53f, .19f, .87f, .38f,
                () => { music?.SetVolume(Mathf.Min(1, music.Volume + .15f)); ShowMusicSettings(back); }, slate, 25);
            Button(root, "돌아가기", .36f, .025f, .64f, .14f, back, gold, 26);
        }
        private void Backdrop(bool calm)
        {
            Texture2D art = battleArt != null ? battleArt : titleArt;
            if (art != null) ImageFrame(root, art, new Rect(0, 0, 1, 1), 0, 0, 1, 1, new Vector2(.5f, .53f));
            Panel(root, 0, 0, 1, 1, new Color(.045f, .055f, .13f, calm ? .86f : .26f));
            Ornament(root, new Color(.89f, .86f, 1f, calm ? .24f : .44f));
        }
        private void Ornament(Transform parent, Color color, bool card = false)
        {
            var go = new GameObject("Starlight filigree", typeof(RectTransform), typeof(UiConstellation));
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false); Anchor(rt, 0, 0, 1, 1);
            var ornament = go.GetComponent<UiConstellation>(); ornament.color = color; ornament.CardCorners = card; ornament.raycastTarget = false; ornament.SetAllDirty();
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
            if (locked) return; int start = engine.State.events.Count;
            if (!action()) { ShowBattle(); return; }
            selectedCard = selectedAttacker = -1; Save(); StartCoroutine(AnimateAndContinue(start));
        }
        private IEnumerator AnimateAndContinue(int start)
        {
            locked = true; ShowBattle();
            var recorded = engine.State.events.Skip(start)
                .Where(item => item.kind == "coin" || item.kind == "damage" || item.kind == "play" || item.kind == "spell" || item.kind == "upkeep").ToArray();
            // Keep a long clash responsive. Every core event remains in the battle
            // record; the animation highlights its opening and final outcomes.
            var highlights = recorded.Length <= 18 ? recorded : recorded.Take(9).Concat(recorded.Skip(recorded.Length - 9)).ToArray();
            float duration = Mathf.Clamp(3f / Mathf.Max(1, highlights.Length), .16f, .48f);
            foreach (var item in highlights)
            {
                bool coin = item.kind == "coin";
                ShowEffect(item.text, coin ? gold : teal, coin, item.amount);
                if (audioSource != null) audioSource.PlayOneShot(coin ? coinSound : playSound, coin ? .7f : .65f);
                var rect = effectObject != null ? effectObject.GetComponent<RectTransform>() : null;
                var opacity = effectObject != null ? effectObject.AddComponent<CanvasGroup>() : null;
                for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
                {
                    float progress = Mathf.Clamp01(t / duration), ease = 1 - Mathf.Pow(1 - progress, 3);
                    if (rect != null) rect.anchoredPosition = Vector2.up * Mathf.Lerp(-9, 0, ease);
                    if (opacity != null) opacity.alpha = Mathf.Clamp01(progress * 6) * Mathf.Clamp01((1 - progress) * 7);
                    if (effectCoin != null)
                    {
                        float flip = Mathf.Abs(Mathf.Cos(progress * Mathf.PI * 3));
                        effectCoin.localScale = new Vector3(Mathf.Lerp(.12f, 1, progress < .78f ? flip : Mathf.SmoothStep(0, 1, (progress - .78f) / .22f)), 1, 1);
                    }
                    yield return null;
                }
            }
            locked = false; message = Hint(); ShowBattle(); PumpAi();
        }
        private void PumpAi()
        {
            if (locked || !battleScreen || engine.State.phase == BattlePhase.Complete) return;
            var s = engine.State; bool aiAction = engine.PreparingPlayer == 1;
            if (!aiAction) return;
            int start = s.events.Count; if (BattleAi.Step(engine, 1, out message)) { Save(); StartCoroutine(AnimateAndContinue(start)); }
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
            var shade = Panel(root, 0, 0, 1, 1, new Color(.01f, .02f, .04f, .73f)); shade.GetComponent<Image>().raycastTarget = true;
            var panel = Panel(root, Compact ? .10f : .275f, Compact ? .16f : .30f, Compact ? .90f : .79f, Compact ? .84f : .77f, new Color(.025f, .045f, .085f, .99f));
            Frame(panel, gold, 1); panel.gameObject.AddComponent<UiPanelEntrance>();
            string result = s.winner == 0 ? "서약을 지켰습니다" : s.winner == 1 ? "별빛은 다시 떠오릅니다" : "두 서약이 균형을 이루었습니다";
            Text(panel, result, .06f, .66f, .94f, .87f, 42, gold, TextAlignmentOptions.Center);
            Text(panel, (s.winner == 0 ? "승리" : s.winner == 1 ? "패배" : "무승부") + " · " + s.turn + "턴\n지형과 유닛의 균형, 방어에 남길 동료를 다시 생각해 보세요.", .08f, .38f, .92f, .64f, 24, Color.white, TextAlignmentOptions.Center);
            if (s.winner == 0) RecordDemoVictory();
            Button(panel, s.winner == 0 && s.encounterIndex >= 0 && s.encounterIndex < DemoCatalog.Encounters.Length - 1 ? "다음 결투" : "다시 도전", .09f, .10f, .48f, .28f,
                () => { RestoreDemoSelection(); if (s.winner == 0) selectedEncounter = Mathf.Min(DemoCatalog.Encounters.Length - 1, s.encounterIndex + 1); ShowEncounterSelection(); }, gold, 24, !locked);
            Button(panel, "아바타 · 덱 바꾸기", .53f, .10f, .91f, .28f, () => LeaveBattle(ShowLeaderSelection), teal, 24, !locked);
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
            Button(root, "돌아가기", .70f, .15f, .91f, .28f, ShowTitle, new Color(.2f, .25f, .34f));
        }
        private static bool ValidatePayload(string payload) { try { BattleEngine.Validate(JsonUtility.FromJson<BattleState>(payload)); return true; } catch { return false; } }
        [Serializable] private sealed class DeckSave { public List<string> cards = new List<string>(); }
        private static bool ValidDeck(List<string> deck)
        {
            try { DemoCatalog.ValidatePlayableDeck(deck); return true; } catch { return false; }
        }
        private void SaveDeck()
        {
            if (!PersistentSaves.TrySave(DeckSlot, GameId, 1, CardCatalog.Version, JsonUtility.ToJson(new DeckSave { cards = customDeck }), out string error)) message = "덱 저장 실패: " + error; else message = "새로운 서약의 덱을 저장했습니다.";
        }
        private void LoadDeck()
        {
            if (!PersistentSaves.Exists(DeckSlot)) return;
            if (!PersistentSaves.TryLoad(DeckSlot, GameId, 1, CardCatalog.Version, out string payload, out string error)) { message = "기존 덱 저장을 보존했습니다: " + error; return; }
            try { var deck = JsonUtility.FromJson<DeckSave>(payload); if (!ValidDeck(deck.cards)) throw new ArgumentException("덱 구성이 잘못되었습니다."); customDeck.Clear(); customDeck.AddRange(deck.cards); } catch (Exception e) { message = "기존 덱 저장을 보존했습니다: " + e.Message; }
        }
        private void DrawCard(Transform parent, CardDefinition card, float x0, float y0, float x1, float y1, bool selected, Action onClick, bool interactable)
        {
            Color element = ElementColor(card.element), rarity = RarityColor(card.rarity);
            bool small = Compact && canvas.pixelRect.height < 600;
            var shadow = Panel(parent, x0 - .001f, y0 - .004f, x1 + .001f, y1 - .002f, new Color(.006f, .01f, .035f, .55f));
            shadow.gameObject.name = "Card soft shadow"; shadow.GetComponent<UiRoundedImage>().Radius = small ? 15 : 22;
            var panel = Panel(parent, x0, y0, x1, y1, ink); panel.gameObject.name = "Card " + card.id;
            panel.GetComponent<UiRoundedImage>().Radius = small ? 14 : 20;
            panel.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Artwork(panel, card, .015f, .015f, .985f, .985f);
            Gradient(panel, .01f, .01f, .99f, .58f, new Color(.012f, .018f, .045f, 1), Color.clear);
            Panel(panel, .015f, .01f, .985f, small ? .43f : .34f, new Color(.015f, .02f, .045f, .85f));
            Text(panel, card.name, .06f, small ? .22f : .16f, .94f, small ? .43f : .335f, Compact ? 29 : 28, ivory);
            string stats = card.kind == CardKind.Unit ? "AP " + card.attack + "  DP " + card.defense + "  HP " + card.health : CardCatalog.KindName(card.kind) + " · " + CardCatalog.ElementName(card.element);
            Text(panel, stats, .06f, .025f, .94f, small ? .22f : .155f, Compact ? 23 : 21, element);
            if (small && battleScreen)
            {
                // The short mobile hand needs a full line of glyph height. Splitting
                // its old corner pill vertically left both labels below that height.
                var badge = Panel(panel, .035f, .72f, .965f, .975f, new Color(.015f, .025f, .055f, .96f));
                badge.gameObject.name = "Hand level soul rarity";
                Text(badge, "Lv " + card.level, .015f, .02f, .36f, .98f, 24, ivory, TextAlignmentOptions.Center);
                Text(badge, card.coloredCost + "+" + card.genericCost, .36f, .02f, .77f, .98f, 23, element, TextAlignmentOptions.Center);
                Text(badge, CardCatalog.RarityCode(card.rarity), .78f, .02f, .985f, .98f, 24, rarity, TextAlignmentOptions.Center);
                Frame(badge, new Color(rarity.r, rarity.g, rarity.b, .7f), 1);
            }
            else
            {
                var cost = Panel(panel, .04f, small ? .65f : .73f, .37f, .96f, new Color(.015f, .025f, .055f, .95f));
                Text(cost, "Lv " + card.level, .04f, .48f, .96f, .97f, 24, ivory, TextAlignmentOptions.Center);
                Text(cost, card.coloredCost + "+" + card.genericCost, .04f, .025f, .96f, .49f, 23, element, TextAlignmentOptions.Center);
                Frame(cost, element, 1.2f);
                var grade = Panel(panel, .60f, small ? .76f : .84f, .96f, .96f, new Color(.015f, .025f, .055f, .94f));
                Text(grade, CardCatalog.RarityCode(card.rarity), .02f, .03f, .98f, .97f, 24, rarity, TextAlignmentOptions.Center);
                Frame(grade, new Color(rarity.r, rarity.g, rarity.b, .65f), 1);
            }
            Frame(panel, selected ? ivory : rarity, selected ? 3.2f : 1.6f);
            Ornament(panel, new Color(rarity.r, rarity.g, rarity.b, .8f), true);
            panel.GetComponent<Image>().raycastTarget = true;
            var button = panel.gameObject.AddComponent<Button>(); button.interactable = interactable; button.targetGraphic = panel.GetComponent<Image>();
            button.onClick.AddListener(() => onClick());
            BindCardHelp(panel.gameObject, card, () => ShowDetail(card, redraw));
            var hover = panel.GetComponent<UiCardHover>(); hover.HoverScale = 1.035f; hover.HoverLift = 5;
        }
        private void Artwork(Transform parent, CardDefinition d, float x0, float y0, float x1, float y1)
        {
            if (!string.IsNullOrEmpty(d.artResource) && d.artResource.StartsWith("Art/ClassicCards/", StringComparison.Ordinal))
            {
                var original = Resources.Load<Texture2D>(d.artResource);
                if (original != null)
                {
                    // The source card is preserved. Only its common illustration window is sampled by the UI.
                    ImageFrame(parent, original, new Rect(.13f, .36f, .805f, .56f), x0, y0, x1, y1, new Vector2(.5f, .64f), true);
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
                var shape = clip.GetComponent<UiRoundedImage>(); shape.TopTint = shape.BottomTint = Color.white; shape.Radius = 20;
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
        private void Frame(RectTransform rect, Color color, float pixels)
        {
            var existing = rect.Find("Fine rounded frame");
            var frame = existing != null ? (RectTransform)existing : new GameObject("Fine rounded frame", typeof(RectTransform), typeof(UiRoundedImage)).GetComponent<RectTransform>();
            if (existing == null) { frame.SetParent(rect, false); Anchor(frame, 0, 0, 1, 1); }
            frame.SetAsLastSibling();
            var shape = frame.GetComponent<UiRoundedImage>(); shape.Fill = false; shape.raycastTarget = false;
            shape.Radius = rect.GetComponent<UiRoundedImage>()?.Radius ?? 20f; shape.BorderColor = color; shape.BorderWidth = pixels; shape.SetAllDirty();
        }
        private RectTransform Panel(Transform parent, float x0, float y0, float x1, float y1, Color color)
        {
            var go = new GameObject("Panel", typeof(RectTransform), typeof(UiRoundedImage));
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false); Anchor(rt, x0, y0, x1, y1);
            var image = go.GetComponent<UiRoundedImage>(); image.color = color; image.raycastTarget = false;
            image.Radius = x0 == 0 && y0 == 0 && x1 == 1 && y1 == 1 ? 0 : 20;
            return rt;
        }
        private TMP_Text Text(Transform parent, string value, float x0, float y0, float x1, float y1, float size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)); var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false); Anchor(rt, x0, y0, x1, y1);
            float scale = Mathf.Sqrt(Mathf.Max(1, canvas.pixelRect.width) / 1920f * Mathf.Max(1, canvas.pixelRect.height) / 1080f);
            bool small = Compact && canvas.pixelRect.height < 600;
            if (small) size = Mathf.Max(size, 13f / scale);
            var text = go.GetComponent<TextMeshProUGUI>(); text.font = font; text.text = value; text.fontSize = size; text.color = color; text.alignment = alignment; text.raycastTarget = false; text.enableAutoSizing = true; text.fontSizeMin = small ? Mathf.Max(size * .8f, 12f / scale) : size * .8f; text.fontSizeMax = size; text.overflowMode = TextOverflowModes.Ellipsis; return text;
        }
        private Button Button(Transform parent, string label, float x0, float y0, float x1, float y1, Action action, Color color, float size = 22, bool enabled = true)
        {
            var panel = Panel(parent, x0, y0, x1, y1, color); panel.gameObject.name = "Button " + label; panel.GetComponent<Image>().raycastTarget = true;
            var b = panel.gameObject.AddComponent<Button>(); b.interactable = enabled; var colors = b.colors; colors.highlightedColor = new Color(1.13f, 1.13f, 1.13f); colors.disabledColor = new Color(.48f, .48f, .48f); colors.pressedColor = new Color(.76f, .80f, .86f); b.colors = colors;
            var shadow = panel.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(.015f, .02f, .06f, .24f); shadow.effectDistance = new Vector2(0, -3);
            Frame(panel, new Color(gold.r, gold.g, gold.b, color == gold ? .85f : .35f), 1.25f);
            b.onClick.AddListener(() => { music?.NotifyUserGesture(); action(); });
            panel.gameObject.AddComponent<UiCardHover>().HoverScale = 1.012f;
            Text(panel, label, .035f, .07f, .965f, .93f, size, color == gold || color == teal ? ink : ivory, TextAlignmentOptions.Center);
            BindTooltip(panel.gameObject, label, ButtonHelp(label));
            return b;
        }
        private static void Anchor(RectTransform r, float x0, float y0, float x1, float y1) { r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1); r.offsetMin = r.offsetMax = Vector2.zero; }
        private static string PhaseName(BattlePhase phase) { switch (phase) { case BattlePhase.Main: return "공격 준비"; case BattlePhase.DefensePreparation: return "방어 준비"; default: return "결투 종료"; } }
        private void SetupWorld()
        {
            var camera = Camera.main;
            if (camera == null) { var go = new GameObject("Battle Camera", typeof(Camera), typeof(AudioListener)); camera = go.GetComponent<Camera>(); go.tag = "MainCamera"; }
            camera.transform.position = new Vector3(0, 11, -13); camera.transform.LookAt(new Vector3(0, 0, 1)); camera.fieldOfView = 47; camera.backgroundColor = new Color(.025f, .04f, .08f); camera.clearFlags = CameraClearFlags.SolidColor;
            if (camera.GetComponent<CinemachineBrain>() == null) camera.gameObject.AddComponent<CinemachineBrain>();
            battleCamera = new GameObject("Cinemachine Battle Camera", typeof(CinemachineCamera)).GetComponent<CinemachineCamera>();
            battleCamera.transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation); battleCamera.Lens.FieldOfView = 47;
            revealDirector = gameObject.AddComponent<PlayableDirector>(); revealDirector.playOnAwake = false; revealDirector.extrapolationMode = DirectorWrapMode.None;
            var reveal = ScriptableObject.CreateInstance<TimelineAsset>(); var revealTrack = reveal.CreateTrack<PlayableTrack>(null, "별빛 전장 도입");
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
