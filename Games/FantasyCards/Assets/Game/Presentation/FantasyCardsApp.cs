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
    public sealed class FantasyCardsApp : MonoBehaviour
    {
        private const string Slot = "fantasy-battle", GameId = "starlight-oath";
        private readonly Color ink = new Color(.025f, .045f, .085f, .95f);
        private readonly Color gold = new Color(.88f, .71f, .39f);
        private readonly Color teal = new Color(.30f, .73f, .78f);
        private readonly List<string> customDeck = CardCatalog.DefaultDeck();
        private readonly Dictionary<string, Sprite> authoredIllustrations = new Dictionary<string, Sprite>();
        private BattleEngine engine;
        private RectTransform root;
        private Canvas canvas;
        private TMP_FontAsset font;
        private Texture2D titleArt;
        private Action redraw;
        private Vector2 lastViewport;
        private int collectionPage;
        private bool Compact => canvas != null && (canvas.pixelRect.width < 1400 || canvas.pixelRect.width / Mathf.Max(1, canvas.pixelRect.height) > 1.95f);
        private readonly Color ivory = new Color(.96f, .93f, .84f);
        private readonly Color slate = new Color(.075f, .115f, .17f, .96f);
        private int selectedCard = -1, selectedAttacker = -1;
        private string inspectedId = "dawn_scout", message = "지형을 전개하고, 서약을 지킬 동료를 불러오세요.";
        private string saveError = "";
        private bool locked, battleScreen;
        private AudioSource audioSource;
        private AudioClip coinSound, playSound;
        private TMP_Text effectLabel;
        private GameObject effectObject;
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
            var c = new GameObject("Starlight UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = c.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            var scaler = c.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            root = c.GetComponent<RectTransform>();
            if (FindFirstObjectByType<EventSystem>() == null) new GameObject("UI Input", typeof(EventSystem), typeof(InputSystemUIInputModule));
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false; audioSource.volume = .24f;
            coinSound = MakeTone(960f, .13f); playSound = MakeTone(260f, .15f);
            SetupWorld(); LoadDeck(); ShowTitle();
        }
        private void Update()
        {
            if (canvas != null && !locked && redraw != null && Vector2.Distance(lastViewport, canvas.pixelRect.size) > 1)
            { lastViewport = canvas.pixelRect.size; redraw(); }
            if (!locked && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (battleScreen) LeaveBattle(ShowTitle); else ShowTitle();
            }
        }
        private void Clear()
        {
            foreach (Transform child in root) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            effectLabel = null; effectObject = null;
            lastViewport = canvas.pixelRect.size;
        }
        private void ShowTitle()
        {
            battleScreen = false; redraw = ShowTitle; Clear(); UpdateWorld();
            if (titleArt != null) ImageFrame(root, titleArt, new Rect(0, 0, 1, 1), 0, 0, 1, 1, new Vector2(.65f, .55f));
            Gradient(root, 0, 0, .83f, 1, new Color(.014f, .025f, .047f, .99f), new Color(.014f, .025f, .047f, 0), true);
            Gradient(root, 0, 0, 1, .45f, new Color(.014f, .025f, .047f, .97f), Color.clear);
            bool compact = Compact;
            Text(root, "STARLIGHT OATH  /  THE FIRST CHAPTER", .055f, .84f, .57f, .91f, compact ? 24 : 21, gold);
            Text(root, "별빛의 서약", .05f, .665f, .61f, .83f, compact ? 80 : 96, ivory);
            Panel(root, .058f, .638f, .13f, .641f, gold);
            Text(root, "여명과 조류, 두 세계의 첫 결투", .057f, .57f, .62f, .63f, compact ? 28 : 27, new Color(.84f, .87f, .88f));
            float top = compact ? .54f : .52f, bottom = compact ? .405f : .425f;
            Button(root, "새로운 결투", .058f, bottom, .275f, top, () => StartBattle(731029u), gold, 28);
            Button(root, "이어하기", .287f, bottom, .49f, top, Continue, slate, 27, PersistentSaves.Exists(Slot));
            bottom = compact ? .25f : .323f; top = compact ? .385f : .405f;
            Button(root, "카드 도감", .058f, bottom, .275f, top, () => ShowCollection(false), slate, 25);
            Button(root, "덱 구성 · " + customDeck.Count + "장", .287f, bottom, .49f, top, () => ShowCollection(true), slate, 25);
            Button(root, "저장 내보내기 / 가져오기", .058f, compact ? .095f : .225f, .49f, compact ? .23f : .303f, ShowTransfer, slate, 23);
            if (!compact)
            {
                Text(root, "16 ORIGINAL CARDS", .735f, .13f, .96f, .175f, 20, gold, TextAlignmentOptions.MidlineRight);
                Text(root, "나만의 서약을 펼치다", .66f, .075f, .96f, .13f, 30, ivory, TextAlignmentOptions.MidlineRight);
            }
            Text(root, StatusMessage(), .058f, .018f, .96f, .073f, 19, new Color(.70f, .76f, .82f));
        }
        private void StartBattle(uint seed)
        {
            if (!ValidDeck(customDeck)) { message = "24장, 지형 4장 이상, 유닛 6장 이상으로 덱을 구성하세요."; ShowCollection(true); return; }
            engine = new BattleEngine(seed, authoredRules, playerDeck: customDeck); selectedCard = selectedAttacker = -1;
            message = "지형 선택 → 지형 전개  /  유닛 선택 → 빈 아군 칸  /  공격 단계 → 공격자 → 대상";
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
            try { engine = new BattleEngine(JsonUtility.FromJson<BattleState>(payload)); selectedCard = selectedAttacker = -1; message = "저장한 전투와 다음 코인 상태를 복원했습니다."; ShowBattle(); PumpAi(); }
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
        private void ShowBattle()
        {
            battleScreen = true; redraw = ShowBattle; Clear(); UpdateWorld();
            var s = engine.State; var p = s.players[0]; var enemy = s.players[1]; bool compact = Compact;
            Gradient(root, 0, 0, 1, .43f, new Color(.012f, .023f, .043f, .99f), Color.clear);
            Gradient(root, 0, .69f, 1, 1, Color.clear, new Color(.012f, .023f, .043f, .96f));
            Panel(root, .02f, compact ? .879f : .925f, .98f, .995f, ink);
            Text(root, "별빛의 서약", .035f, compact ? .93f : .936f, .25f, .986f, compact ? 29 : 31, gold);
            Text(root, s.turn + "턴 · " + (s.activePlayer == 0 ? "나의 " : "상대의 ") + PhaseName(s.phase), .275f, compact ? .93f : .936f, .735f, .986f, compact ? 26 : 27, ivory);
            Button(root, "저장", .755f, compact ? .876f : .938f, .855f, .991f, () => { if (Save()) message = "전투와 난수 상태를 저장했습니다."; ShowBattle(); }, slate, 23, !locked);
            Button(root, "타이틀", .868f, compact ? .876f : .938f, .974f, .991f, () => LeaveBattle(ShowTitle), slate, 23, !locked);
            Text(root, StatusMessage(), .035f, compact ? .883f : .885f, compact ? .738f : .975f, compact ? .929f : .922f, compact ? 24 : 20, ivory);
            float enemyBottom = compact ? .762f : .813f, enemyTop = compact ? .874f : .875f;
            Button(root, "달의 수호자   생명 " + enemy.health + " / " + s.rules.startingHealth, compact ? .025f : .325f, enemyBottom, compact ? .56f : .745f, enemyTop, () => Attack(-1), new Color(.14f, .15f, .23f, .95f), 27,
                !locked && s.activePlayer == 0 && s.phase == BattlePhase.AttackDeclaration && selectedAttacker >= 0);
            Text(root, "지형 " + enemy.terrain.Count + "  여명 " + enemy.dawn + " / 조류 " + enemy.tide + "  손패 " + enemy.hand.Count + "  덱 " + enemy.deck.Count,
                compact ? .58f : .25f, compact ? .77f : .78f, compact ? .975f : .79f, compact ? .868f : .81f, compact ? 23 : 20, new Color(.68f, .80f, .88f), compact ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft);
            DrawLanes(1, compact ? .62f : .585f, compact ? .752f : .772f);
            DrawLanes(0, compact ? .445f : .365f, compact ? .575f : .55f);
            Text(root, "내 지형  " + (p.terrain.Count == 0 ? "아직 전개되지 않음" : string.Join(" · ", p.terrain.Select(c => CardCatalog.Get(c.definitionId).name))),
                compact ? .025f : .25f, compact ? .575f : .552f, compact ? .975f : .79f, compact ? .62f : .581f, compact ? 23 : 18, new Color(.86f, .84f, .73f));
            Text(root, "나의 생명 " + p.health + " / " + s.rules.startingHealth + "     여명 " + p.dawn + "  ·  조류 " + p.tide + "     덱 " + p.deck.Count,
                compact ? .025f : .25f, compact ? .275f : .326f, .975f, compact ? .323f : .36f, compact ? 24 : 25, gold);
            if (compact)
            {
                Button(root, "확대 보기", .025f, .325f, .20f, .438f, () => ShowDetail(CardCatalog.Get(inspectedId), ShowBattle), slate, 23, !locked);
                DrawActions(.215f, .325f, .785f, .438f);
                Button(root, "전투 기록", .80f, .325f, .975f, .438f, ShowBattleLog, slate, 23, !locked);
            }
            else
            {
                Panel(root, .02f, .365f, .23f, .875f, ink); DrawInspector();
                Panel(root, .81f, .365f, .98f, .875f, ink);
                Text(root, "BATTLE RECORD", .825f, .829f, .965f, .861f, 16, gold);
                Text(root, string.Join("\n\n", s.events.Skip(Math.Max(0, s.events.Count - 7)).Select(e => e.text)), .825f, .39f, .965f, .818f, 18, new Color(.75f, .80f, .85f), TextAlignmentOptions.TopLeft);
            }
            int count = p.hand.Count; float width = Mathf.Min(compact ? .18f : .133f, .94f / Mathf.Max(1, count)); float left = .5f - count * width / 2f;
            for (int i = 0; i < count; i++)
            {
                var card = p.hand[i]; int id = card.instanceId; var d = CardCatalog.Get(card.definitionId);
                DrawCard(root, d, left + i * width, .022f, left + (i + 1) * width - .008f, compact ? .273f : .308f, id == selectedCard,
                    () => { selectedCard = id; selectedAttacker = -1; inspectedId = d.id; message = d.kind == CardKind.Terrain ? "‘지형 전개’를 누르세요." : d.effect == Effect.Damage ? "피해를 줄 적 유닛 칸을 선택하세요." : "카드를 사용할 아군 전장 칸을 선택하세요."; ShowBattle(); }, !locked);
            }
            if (s.phase == BattlePhase.Complete) ShowResult();
        }
        private void DrawInspector()
        {
            var d = CardCatalog.Get(inspectedId);
            Artwork(root, d, .032f, .638f, .218f, .859f);
            Text(root, d.name, .035f, .587f, .215f, .632f, 28, ivory);
            Text(root, CardCatalog.ElementName(d.element) + " " + d.cost + " · " + CardCatalog.KindName(d.kind), .035f, .55f, .215f, .587f, 21, gold);
            Text(root, d.text, .035f, .485f, .215f, .55f, 20, new Color(.78f, .83f, .88f), TextAlignmentOptions.TopLeft);
            Button(root, "확대 보기", .035f, .43f, .215f, .479f, () => ShowDetail(d, ShowBattle), slate, 21, !locked);
            DrawActions(.035f, .374f, .215f, .421f);
        }
        private void DrawActions(float x0, float y0, float x1, float y1)
        {
            var s = engine.State;
            if (s.phase == BattlePhase.BlockResponse && s.activePlayer == 1)
            { Button(root, "가로막지 않기", x0, y0, x1, y1, () => Act(() => engine.ResolveBlock(0, -1, out message)), gold, 23, !locked); return; }
            if (s.phase == BattlePhase.Main && s.activePlayer == 0)
            {
                var card = s.players[0].hand.Find(c => c.instanceId == selectedCard); float middle = (x0 + x1) / 2;
                if (card != null && CardCatalog.Get(card.definitionId).kind == CardKind.Terrain)
                    Button(root, "지형 전개", x0, y0, middle - .004f, y1, () => Play(0), gold, 23, !locked);
                else Button(root, "턴 종료", x0, y0, middle - .004f, y1, EndTurn, slate, 23, !locked);
                Button(root, "공격 단계", middle + .004f, y0, x1, y1, () => Act(() => engine.BeginCombat(0, out message)), gold, 23, !locked);
            }
            else if (s.phase == BattlePhase.AttackDeclaration && s.activePlayer == 0)
                Button(root, "공격 종료 · 턴 넘기기", x0, y0, x1, y1, EndTurn, gold, 23, !locked);
            else Text(root, s.phase == BattlePhase.Complete ? "결투 종료" : "상대의 판단을 기다리는 중", x0, y0, x1, y1, 23, teal, TextAlignmentOptions.Center);
        }
        private void ShowBattleLog()
        {
            redraw = ShowBattleLog; Clear(); Panel(root, .05f, .06f, .95f, .94f, ink);
            Text(root, "전투 기록", .09f, .79f, .85f, .9f, 40, gold);
            Text(root, string.Join("\n", engine.State.events.Skip(Math.Max(0, engine.State.events.Count - 10)).Select(e => e.text)), .09f, .24f, .91f, .78f, 24, ivory, TextAlignmentOptions.TopLeft);
            Button(root, "돌아가기", .64f, .085f, .91f, .22f, ShowBattle, gold, 27);
        }
        private void DrawLanes(int owner, float bottom, float top)
        {
            var s = engine.State; float start = Compact ? .025f : .25f, span = Compact ? .96f : .55f;
            for (int i = 0; i < s.rules.unitLanes; i++)
            {
                int lane = i; var unit = s.players[owner].AtLane(i); float left = start + i * span / s.rules.unitLanes, right = left + span / s.rules.unitLanes - .012f;
                bool target = IsLegalLane(owner, lane);
                var panel = Panel(root, left, bottom, right, top, new Color(.025f, .045f, .075f, target ? .88f : .65f));
                panel.gameObject.name = "Lane " + owner + " " + lane;
                Frame(panel, target ? gold : new Color(.40f, .48f, .58f, .55f), target ? 2 : 1);
                if (unit == null) Text(panel, "전장 " + (i + 1) + "  /  " + (target ? "여기에 배치" : "비어 있음"), .05f, .14f, .95f, .86f, Compact ? 24 : 22, target ? gold : new Color(.55f, .62f, .70f), TextAlignmentOptions.Center);
                else
                {
                    var d = CardCatalog.Get(unit.definitionId);
                    string stats = "공격 " + (d.attack + unit.bonusPower) + "  생명 " + (d.health - unit.damage);
                    if (Compact && canvas.pixelRect.height < 600)
                    {
                        // A 51px lane fits two full Korean text lines; three short rows clip TMP glyphs.
                        Artwork(panel, d, .01f, .015f, .295f, .985f);
                        Panel(panel, .295f, 0, 1, 1, new Color(.014f, .025f, .045f, .96f));
                        Text(panel, d.name, .31f, .51f, .745f, .98f, 25, ivory);
                        Text(panel, "코인 " + (s.rules.baseCoins + unit.extraCoins), .765f, .51f, .985f, .98f, 23, teal);
                        Text(panel, stats, .31f, .02f, .985f, .49f, 24, gold);
                        var badge = Panel(panel, .01f, .02f, .295f, .47f, new Color(.014f, .025f, .045f, .92f));
                        Text(badge, unit.summoningSick ? "소환 대기" : unit.exhausted ? "행동 완료" : "방어 " + unit.armor,
                            .025f, .05f, .975f, .95f, 23, unit.exhausted ? Color.gray : teal, TextAlignmentOptions.Center);
                    }
                    else
                    {
                        Artwork(panel, d, .01f, .015f, .99f, .985f);
                        Gradient(panel, .3f, 0, 1, 1, Color.clear, new Color(.014f, .025f, .045f, .98f), true);
                        Panel(panel, .47f, 0, 1, 1, new Color(.014f, .025f, .045f, .75f));
                        Text(panel, d.name, .5f, .66f, .97f, .95f, Compact ? 25 : 23, ivory);
                        Text(panel, stats, .5f, .37f, .97f, .67f, Compact ? 24 : 21, gold);
                        Text(panel, unit.summoningSick ? "소환 대기" : unit.exhausted ? "행동 완료" : "방어 " + unit.armor + " · 코인 " + (s.rules.baseCoins + unit.extraCoins), .5f, .07f, .97f, .36f, Compact ? 23 : 19, unit.exhausted ? Color.gray : teal);
                    }
                }
                panel.GetComponent<Image>().raycastTarget = true;
                var button = panel.gameObject.AddComponent<Button>(); button.interactable = !locked; button.onClick.AddListener(() => LaneClick(owner, lane));
                if (unit != null && unit.instanceId == selectedAttacker) Frame(panel, ivory, 3);
            }
        }
        private bool IsLegalLane(int owner, int lane)
        {
            if (locked) return false; var s = engine.State; var u = s.players[owner].AtLane(lane);
            if (s.phase == BattlePhase.BlockResponse) return s.activePlayer == 1 && owner == 0 && u != null && !u.exhausted && !u.summoningSick;
            if (s.activePlayer != 0) return false;
            if (s.phase == BattlePhase.AttackDeclaration) return owner == 0 ? u != null && !u.exhausted && !u.summoningSick : selectedAttacker >= 0 && u != null;
            var c = s.players[0].hand.Find(x => x.instanceId == selectedCard); if (c == null) return false;
            var d = CardCatalog.Get(c.definitionId); bool enemyTarget = d.effect == Effect.Damage;
            return d.kind != CardKind.Terrain && owner == (enemyTarget ? 1 : 0) && engine.CanPlay(0, selectedCard, lane, out _);
        }
        private void LaneClick(int owner, int lane)
        {
            if (locked) return; var s = engine.State; var u = s.players[owner].AtLane(lane);
            if (u != null) inspectedId = u.definitionId;
            if (!IsLegalLane(owner, lane)) { message = "선택한 카드와 현재 단계에 맞는 빛나는 전장 칸을 선택하세요."; ShowBattle(); return; }
            if (s.phase == BattlePhase.BlockResponse) { Act(() => engine.ResolveBlock(0, u.instanceId, out message)); return; }
            if (s.phase == BattlePhase.AttackDeclaration)
            {
                if (owner == 0) { selectedAttacker = u.instanceId; selectedCard = -1; message = "공격할 상대 유닛 또는 위쪽 상대 생명 표시를 선택하세요. 상대가 가로막을 수 있습니다."; ShowBattle(); }
                else Attack(lane);
                return;
            }
            Play(lane);
        }
        private void Play(int lane) { int id = selectedCard; Act(() => engine.Play(0, id, lane, out message)); }
        private void Attack(int targetLane) { int id = selectedAttacker; Act(() => engine.DeclareAttack(0, id, targetLane, out message)); }
        private void EndTurn() { Act(() => engine.EndTurn(0, out message)); }
        private void Act(Func<bool> action)
        {
            if (locked) return; int start = engine.State.events.Count;
            if (!action()) { ShowBattle(); return; }
            selectedCard = selectedAttacker = -1; Save(); StartCoroutine(AnimateAndContinue(start));
        }
        private IEnumerator AnimateAndContinue(int start)
        {
            locked = true; ShowBattle(); var events = engine.State.events.Skip(start).ToArray();
            foreach (var e in events)
            {
                if (e.kind != "coin" && e.kind != "damage" && e.kind != "play" && e.kind != "declare") continue;
                ShowEffect(e.text, e.kind == "coin" ? gold : teal);
                if (audioSource != null) audioSource.PlayOneShot(e.kind == "coin" ? coinSound : playSound);
                float duration = e.kind == "coin" ? .55f : .3f;
                for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
                {
                    if (effectLabel != null) effectLabel.transform.localScale = Vector3.one * (1f + .04f * Mathf.Sin(t / duration * Mathf.PI));
                    yield return null;
                }
            }
            locked = false; message = Hint(); ShowBattle(); PumpAi();
        }
        private void PumpAi()
        {
            if (locked || !battleScreen || engine.State.phase == BattlePhase.Complete) return;
            var s = engine.State; bool aiAction = s.phase == BattlePhase.BlockResponse ? s.activePlayer == 0 : s.activePlayer == 1;
            if (!aiAction) return;
            int start = s.events.Count; if (BattleAi.Step(engine, 1, out message)) { Save(); StartCoroutine(AnimateAndContinue(start)); }
        }
        private string Hint()
        {
            var s = engine.State;
            if (s.phase == BattlePhase.BlockResponse && s.activePlayer == 1) return "방어 대응: 준비된 아군을 선택해 가로막거나 ‘가로막지 않기’를 선택하세요.";
            if (s.activePlayer == 1) return "달의 수호자가 전투를 준비합니다.";
            if (s.phase == BattlePhase.AttackDeclaration) return "공격할 아군 → 적 유닛 또는 적 서약자를 선택하세요. 공격하지 않은 유닛은 다음 상대 턴에 방어할 수 있습니다.";
            return "지형은 턴당 한 장. 자원은 남아 다음 턴에 이어집니다. 준비를 마치면 공격 단계로 이동하세요.";
        }
        private void ShowEffect(string text, Color color)
        {
            if (effectObject != null) Destroy(effectObject);
            var panel = Panel(root, .27f, .45f, .79f, .60f, new Color(.025f, .04f, .075f, .98f)); effectObject = panel.gameObject;
            effectLabel = Text(panel, text, .04f, .10f, .96f, .90f, 32, color, TextAlignmentOptions.Center);
        }
        private void ShowResult()
        {
            var s = engine.State;
            var shade = Panel(root, 0, 0, 1, 1, new Color(.01f, .02f, .04f, .73f)); shade.GetComponent<Image>().raycastTarget = true;
            var panel = Panel(root, Compact ? .10f : .275f, Compact ? .16f : .30f, Compact ? .90f : .79f, Compact ? .84f : .77f, new Color(.025f, .045f, .085f, .99f));
            Frame(panel, gold, 1);
            string result = s.winner == 0 ? "서약을 지켰습니다" : s.winner == 1 ? "별빛은 다시 떠오릅니다" : "두 서약이 균형을 이루었습니다";
            Text(panel, result, .06f, .66f, .94f, .87f, 42, gold, TextAlignmentOptions.Center);
            Text(panel, (s.winner == 0 ? "승리" : s.winner == 1 ? "패배" : "무승부") + " · " + s.turn + "턴\n지형과 유닛의 균형, 방어에 남길 동료를 다시 생각해 보세요.", .08f, .38f, .92f, .64f, 24, Color.white, TextAlignmentOptions.Center);
            Button(panel, "같은 결투 재도전", .09f, .10f, .48f, .28f, () => StartBattle(731029u), gold, 24, !locked);
            Button(panel, "덱을 다듬기", .53f, .10f, .91f, .28f, () => LeaveBattle(() => ShowCollection(true)), teal, 24, !locked);
        }
        private void ShowCollection(bool edit)
        {
            battleScreen = false; redraw = () => ShowCollection(edit); Clear();
            Panel(root, 0, 0, 1, 1, ink); bool compact = Compact;
            Text(root, edit ? "나의 서약 · 덱 구성" : "별빛의 기록 · 카드 도감", .04f, .89f, .78f, .98f, 42, ivory);
            Text(root, edit ? customDeck.Count + " / 24장 · 지형 " + customDeck.Count(id => CardCatalog.Get(id).kind == CardKind.Terrain) + " · 유닛 " + customDeck.Count(id => CardCatalog.Get(id).kind == CardKind.Unit) + " · 카드당 최대 4장" : "여명과 조류 / 지형 4종 · 유닛 6종 · 마법 3종 · 장비 3종", .04f, .815f, .79f, .88f, 24, gold);
            Button(root, "타이틀", .83f, compact ? .85f : .90f, .96f, .98f, ShowTitle, slate, 25);
            for (int i = 0; i < 4; i++)
            {
                var d = CardCatalog.All[collectionPage * 4 + i]; float left = .045f + i * .238f, right = left + .211f;
                DrawCard(root, d, left, compact ? .275f : .235f, right, compact ? .785f : .795f, false, () => ShowDetail(d, () => ShowCollection(edit)), true);
                if (edit)
                {
                    Button(root, "-", left, .135f, left + .065f, compact ? .255f : .215f, () => { customDeck.Remove(d.id); ShowCollection(true); }, slate, 34);
                    Text(root, customDeck.Count(id => id == d.id) + "장", left + .069f, .135f, right - .069f, compact ? .255f : .215f, 28, ivory, TextAlignmentOptions.Center);
                    Button(root, "+", right - .065f, .135f, right, compact ? .255f : .215f, () => { if (customDeck.Count < 30 && customDeck.Count(id => id == d.id) < 4) customDeck.Add(d.id); ShowCollection(true); }, slate, 34);
                }
                else Text(root, "0" + (collectionPage * 4 + i + 1) + "  /  " + CardCatalog.ElementName(d.element), left, .15f, right, .215f, 22, gold, TextAlignmentOptions.Center);
            }
            if (edit)
            {
                Button(root, "기본 덱 복원", .04f, .012f, .24f, .128f, () => { customDeck.Clear(); customDeck.AddRange(CardCatalog.DefaultDeck()); ShowCollection(true); }, slate, 23);
                Button(root, "덱 저장", .255f, .012f, .455f, .128f, () => { SaveDeck(); ShowTitle(); }, gold, 25, ValidDeck(customDeck));
            }
            else Text(root, "카드를 선택해 원화와 능력을 확인하세요.", .045f, .015f, .53f, .125f, 23, new Color(.69f, .76f, .83f));
            Button(root, "이전", .59f, .012f, .72f, .128f, () => { collectionPage--; ShowCollection(edit); }, slate, 25, collectionPage > 0);
            Text(root, (collectionPage + 1) + " / 4", .73f, .015f, .81f, .125f, 26, gold, TextAlignmentOptions.Center);
            Button(root, "다음", .825f, .012f, .955f, .128f, () => { collectionPage++; ShowCollection(edit); }, slate, 25, collectionPage < 3);
        }
        private void ShowDetail(CardDefinition d, Action back)
        {
            redraw = () => ShowDetail(d, back); Clear(); Panel(root, 0, 0, 1, 1, ink);
            Artwork(root, d, .025f, .055f, .525f, .945f); // Near-square at 16:9; crop preserves proportions on wider screens.
            Gradient(root, .39f, .055f, .545f, .945f, Color.clear, ink, true);
            Text(root, CardCatalog.ElementName(d.element) + " / " + CardCatalog.KindName(d.kind), .565f, .79f, .95f, .88f, 28, gold);
            Text(root, d.name, .56f, .64f, .965f, .80f, Compact ? 51 : 59, ivory);
            Panel(root, .565f, .615f, .66f, .619f, gold);
            Text(root, "비용 " + d.cost + (d.kind == CardKind.Unit ? "     공격 " + d.attack + "     생명 " + d.health + "\n기본 코인 1 / 앞면마다 공격 +" + d.coinBonus : ""), .565f, .45f, .95f, .60f, 28, gold);
            Text(root, d.text + "\n\n" + (d.kind == CardKind.Unit ? "소환한 턴에는 공격하거나 가로막을 수 없습니다. 다음 자신의 턴에 준비됩니다." : d.kind == CardKind.Terrain ? "지형은 전장에 남습니다. 다음 자신의 턴마다 자원을 추가합니다. 남은 자원은 최대 12까지 유지됩니다." : "준비 단계에서 선택한 유닛에게 적용합니다. 장비는 유닛당 2개까지 장착할 수 있습니다."), .565f, .225f, .95f, .445f, 25, new Color(.80f, .84f, .88f), TextAlignmentOptions.TopLeft);
            Button(root, "돌아가기", .565f, .065f, .87f, .195f, back, gold, 28);
        }
        private void ShowTransfer()
        {
            battleScreen = false; redraw = null; Clear(); Panel(root, .06f, .06f, .94f, .94f, ink);
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
            try { return deck.Count == 24 && deck.All(id => CardCatalog.Get(id) != null) && deck.GroupBy(id => id).All(g => g.Count() <= 4) && deck.Count(id => CardCatalog.Get(id).kind == CardKind.Terrain) >= 4 && deck.Count(id => CardCatalog.Get(id).kind == CardKind.Unit) >= 6; } catch { return false; }
        }
        private void SaveDeck()
        {
            if (!PersistentSaves.TrySave("fantasy-deck", GameId, 1, CardCatalog.Version, JsonUtility.ToJson(new DeckSave { cards = customDeck }), out string error)) message = "덱 저장 실패: " + error; else message = "새로운 서약의 덱을 저장했습니다.";
        }
        private void LoadDeck()
        {
            if (!PersistentSaves.Exists("fantasy-deck")) return;
            if (!PersistentSaves.TryLoad("fantasy-deck", GameId, 1, CardCatalog.Version, out string payload, out string error)) { message = "기존 덱 저장을 보존했습니다: " + error; return; }
            try { var deck = JsonUtility.FromJson<DeckSave>(payload); if (!ValidDeck(deck.cards)) throw new ArgumentException("덱 구성이 잘못되었습니다."); customDeck.Clear(); customDeck.AddRange(deck.cards); } catch (Exception e) { message = "기존 덱 저장을 보존했습니다: " + e.Message; }
        }
        private void DrawCard(Transform parent, CardDefinition d, float x0, float y0, float x1, float y1, bool selected, Action onClick, bool interactable)
        {
            Color accent = d.element == Element.Dawn ? gold : teal;
            bool small = Compact && canvas.pixelRect.height < 600;
            var panel = Panel(parent, x0, y0, x1, y1, ink); panel.gameObject.name = "Card " + d.id;
            Artwork(panel, d, .012f, .01f, .988f, .99f);
            Gradient(panel, .012f, .01f, .988f, .48f, new Color(.01f, .02f, .04f, 1), new Color(.01f, .02f, .04f, 0));
            Panel(panel, .015f, .01f, .985f, small ? .44f : .345f, new Color(.015f, .025f, .045f, .82f));
            // Reserve a complete TMP line plus ascender/descender space at 720p as well as 1080p.
            Text(panel, d.name, .065f, small ? .22f : .158f, .94f, small ? .435f : .338f, Compact ? 30 : 29, ivory);
            Text(panel, CardCatalog.KindName(d.kind) + (d.kind == CardKind.Unit ? "  /  공격 " + d.attack + "  HP " + d.health : "  /  " + CardCatalog.ElementName(d.element)), .065f, .025f, .94f, small ? .22f : .155f, Compact ? 24 : 22, accent);
            var cost = Panel(panel, .045f, small ? .73f : .795f, .24f, .965f, new Color(.02f, .04f, .065f, .94f));
            Text(cost, d.cost.ToString(), .04f, .04f, .96f, .96f, 34, accent, TextAlignmentOptions.Center); Frame(cost, new Color(accent.r, accent.g, accent.b, .75f), 1);
            Frame(panel, selected ? ivory : accent, selected ? 3 : 1);
            panel.GetComponent<Image>().raycastTarget = true;
            var button = panel.gameObject.AddComponent<Button>(); button.interactable = interactable; button.onClick.AddListener(() => onClick());
            panel.gameObject.AddComponent<UiCardHover>();
        }
        private void Artwork(Transform parent, CardDefinition d, float x0, float y0, float x1, float y1)
        {
            authoredIllustrations.TryGetValue(d.id, out var sprite);
            sprite = sprite != null ? sprite : Resources.Load<Sprite>(d.artResource);
            if (sprite != null)
            {
                var rect = sprite.rect; var texture = sprite.texture;
                ImageFrame(parent, texture, new Rect((rect.x + .5f) / texture.width, (rect.y + .5f) / texture.height, (rect.width - 1) / texture.width, (rect.height - 1) / texture.height), x0, y0, x1, y1, new Vector2(.5f, .66f));
            }
            else Panel(parent, x0, y0, x1, y1, slate);
        }
        private void ImageFrame(Transform parent, Texture texture, Rect uv, float x0, float y0, float x1, float y1, Vector2 focus)
        {
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
            var outline = rect.GetComponent<Outline>() ?? rect.gameObject.AddComponent<Outline>(); outline.effectColor = color; outline.effectDistance = new Vector2(pixels, -pixels);
        }
        private RectTransform Panel(Transform parent, float x0, float y0, float x1, float y1, Color color)
        {
            var go = new GameObject("Panel", typeof(RectTransform), typeof(Image)); var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false); Anchor(rt, x0, y0, x1, y1); var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return rt;
        }
        private TMP_Text Text(Transform parent, string value, float x0, float y0, float x1, float y1, float size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)); var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false); Anchor(rt, x0, y0, x1, y1);
            float scale = Mathf.Sqrt(Mathf.Max(1, canvas.pixelRect.width) / 1920f * Mathf.Max(1, canvas.pixelRect.height) / 1080f);
            bool small = Compact && canvas.pixelRect.height < 600;
            if (small) size = Mathf.Max(size, 13f / scale);
            var text = go.GetComponent<TextMeshProUGUI>(); text.font = font; text.text = value; text.fontSize = size; text.color = color; text.alignment = alignment; text.raycastTarget = false; text.enableAutoSizing = true; text.fontSizeMin = small ? Mathf.Max(size * .8f, 12f / scale) : size * .8f; text.fontSizeMax = size; text.overflowMode = TextOverflowModes.Ellipsis; return text;
        }
        private void Button(Transform parent, string label, float x0, float y0, float x1, float y1, Action action, Color color, float size = 22, bool enabled = true)
        {
            var panel = Panel(parent, x0, y0, x1, y1, color); panel.gameObject.name = "Button " + label; panel.GetComponent<Image>().raycastTarget = true;
            var b = panel.gameObject.AddComponent<Button>(); b.interactable = enabled; var colors = b.colors; colors.highlightedColor = new Color(1.13f, 1.13f, 1.13f); colors.disabledColor = new Color(.48f, .48f, .48f); colors.pressedColor = new Color(.76f, .80f, .86f); b.colors = colors;
            Frame(panel, new Color(gold.r, gold.g, gold.b, color == gold ? .85f : .28f), 1);
            b.onClick.AddListener(() => action()); Text(panel, label, .035f, .07f, .965f, .93f, size, color == gold || color == teal ? ink : ivory, TextAlignmentOptions.Center);
        }
        private static void Anchor(RectTransform r, float x0, float y0, float x1, float y1) { r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1); r.offsetMin = r.offsetMax = Vector2.zero; }
        private static string PhaseName(BattlePhase phase) { switch (phase) { case BattlePhase.Main: return "준비 단계"; case BattlePhase.AttackDeclaration: return "공격 선언"; case BattlePhase.BlockResponse: return "방어 대응"; default: return "결투 종료"; } }
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
