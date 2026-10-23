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
        private Texture2D atlas;
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
            atlas = Resources.Load<Texture2D>("Art/card-atlas");
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
            if (!locked && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (battleScreen) LeaveBattle(ShowTitle); else ShowTitle();
            }
        }
        private void Clear()
        {
            foreach (Transform child in root) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            effectLabel = null; effectObject = null;
        }
        private void ShowTitle()
        {
            battleScreen = false; Clear(); UpdateWorld();
            Panel(root, .045f, .1f, .47f, .9f, ink);
            Text(root, "STARLIGHT OATH", .075f, .79f, .44f, .84f, 22, gold);
            Text(root, "별빛의 서약", .072f, .66f, .58f, .78f, 78, Color.white);
            Text(root, "여명과 조류, 두 세계의 첫 결투", .076f, .60f, .52f, .65f, 26, teal);
            Text(root, "지형을 펼치고 동료를 소환하세요.\n공격을 선언하고, 방어를 선택하고,\n뒤집히는 코인 속에서 서약을 지키세요.", .076f, .44f, .43f, .57f, 25, new Color(.82f, .85f, .9f));
            Button(root, "새로운 결투", .078f, .35f, .255f, .415f, () => StartBattle(731029u), gold, 27);
            Button(root, "이어하기", .27f, .35f, .445f, .415f, Continue, teal, 27, PersistentSaves.Exists(Slot));
            Button(root, "카드 도감", .078f, .26f, .255f, .32f, () => ShowCollection(false), new Color(.16f, .23f, .33f));
            Button(root, "덱 구성 · " + customDeck.Count + "장", .27f, .26f, .445f, .32f, () => ShowCollection(true), new Color(.16f, .23f, .33f));
            Button(root, "저장 내보내기 / 가져오기", .078f, .17f, .445f, .23f, ShowTransfer, new Color(.10f, .17f, .25f), 21);
            Text(root, "첫 결투  /  여명 × 조류\n16종의 카드 · 24장의 덱 · 결정적인 코인", .60f, .20f, .94f, .32f, 26, gold);
            Text(root, StatusMessage(), .06f, .025f, .95f, .08f, 20, Color.white);
        }
        private void StartBattle(uint seed)
        {
            if (!ValidDeck(customDeck)) { message = "24장, 지형 4장 이상, 유닛 6장 이상으로 덱을 구성하세요."; ShowCollection(true); return; }
            engine = new BattleEngine(seed, authoredRules, playerDeck: customDeck); selectedCard = selectedAttacker = -1;
            message = "1. 지형 선택 → 왼쪽 ‘지형 전개’ 2. 유닛 선택 → 빈 아군 칸 3. 공격 단계 → 공격자 → 대상";
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
            battleScreen = true; Clear(); UpdateWorld(); var s = engine.State; var p = s.players[0]; var enemy = s.players[1];
            Panel(root, .015f, .90f, .985f, .985f, ink);
            Text(root, "별빛의 서약", .03f, .925f, .22f, .974f, 34, gold);
            Text(root, s.turn + "턴 · " + (s.activePlayer == 0 ? "나의 " : "상대의 ") + PhaseName(s.phase), .28f, .923f, .67f, .97f, 30, Color.white);
            Button(root, "저장", .77f, .922f, .85f, .969f, () => { if (Save()) message = "전투와 난수 상태를 저장했습니다."; ShowBattle(); }, new Color(.12f, .2f, .3f), 20, !locked);
            Button(root, "타이틀", .865f, .922f, .97f, .969f, () => LeaveBattle(ShowTitle), new Color(.12f, .2f, .3f), 20, !locked);
            Panel(root, .02f, .31f, .22f, .88f, ink); DrawInspector();
            Panel(root, .835f, .31f, .98f, .88f, ink);
            Text(root, "전투 기록", .85f, .83f, .965f, .87f, 23, gold);
            Text(root, string.Join("\n\n", s.events.Skip(Math.Max(0, s.events.Count - 8)).Select(e => e.text)), .85f, .345f, .965f, .82f, 17, new Color(.83f, .87f, .92f), TextAlignmentOptions.TopLeft);
            Button(root, "달의 수호자   생명 " + enemy.health + " / " + s.rules.startingHealth, .37f, .81f, .70f, .87f, () => Attack(-1), new Color(.19f, .13f, .26f), 26, !locked && s.activePlayer == 0 && s.phase == BattlePhase.AttackDeclaration && selectedAttacker >= 0);
            Text(root, "지형 " + enemy.terrain.Count + "   여명 " + enemy.dawn + " / 조류 " + enemy.tide + "   손패 " + enemy.hand.Count + "   덱 " + enemy.deck.Count, .245f, .775f, .81f, .81f, 19, teal);
            DrawLanes(1, .585f, .765f); DrawLanes(0, .365f, .545f);
            Text(root, "나의 생명 " + p.health + " / " + s.rules.startingHealth + "     여명 " + p.dawn + "  ·  조류 " + p.tide + "     덱 " + p.deck.Count, .25f, .31f, .82f, .36f, 24, gold);
            Text(root, "내 지형  " + (p.terrain.Count == 0 ? "아직 전개되지 않음" : string.Join(" · ", p.terrain.Select(c => CardCatalog.Get(c.definitionId).name))), .25f, .548f, .81f, .58f, 16, new Color(.84f, .85f, .8f));
            Panel(root, .02f, .026f, .98f, .29f, new Color(.02f, .035f, .065f, .92f));
            int count = p.hand.Count; float width = Mathf.Min(.113f, .92f / Mathf.Max(1, count)); float left = .5f - count * width / 2f;
            for (int i = 0; i < count; i++)
            {
                var card = p.hand[i]; int id = card.instanceId; var d = CardCatalog.Get(card.definitionId);
                DrawCard(root, d, left + i * width, .045f, left + (i + 1) * width - .007f, .275f, id == selectedCard, () => { selectedCard = id; selectedAttacker = -1; inspectedId = d.id; message = d.kind == CardKind.Terrain ? "왼쪽 ‘지형 전개’를 누르세요." : d.effect == Effect.Damage ? "피해를 줄 적 유닛 칸을 선택하세요." : "카드를 사용할 아군 전장 칸을 선택하세요."; ShowBattle(); }, !locked);
            }
            Text(root, StatusMessage(), .25f, .875f, .975f, .906f, 17, Color.white);
            if (s.phase == BattlePhase.Complete) ShowResult();
        }
        private void DrawInspector()
        {
            var s = engine.State; var d = CardCatalog.Get(inspectedId);
            Artwork(root, d, .035f, .67f, .205f, .855f);
            Text(root, d.name, .035f, .62f, .207f, .67f, 26, gold);
            Text(root, CardCatalog.ElementName(d.element) + " " + d.cost + " · " + CardCatalog.KindName(d.kind), .035f, .58f, .205f, .62f, 20, teal);
            Text(root, d.text, .035f, .48f, .205f, .58f, 21, Color.white, TextAlignmentOptions.TopLeft);
            Button(root, "확대 보기", .035f, .425f, .205f, .47f, () => ShowDetail(d, ShowBattle), new Color(.15f, .22f, .31f), 19, !locked);
            if (s.phase == BattlePhase.BlockResponse && s.activePlayer == 1)
            {
                Button(root, "가로막지 않기", .035f, .335f, .205f, .405f, () => Act(() => engine.ResolveBlock(0, -1, out message)), gold, 22, !locked);
                return;
            }
            bool mine = s.activePlayer == 0;
            if (s.phase == BattlePhase.Main && mine)
            {
                var card = s.players[0].hand.Find(c => c.instanceId == selectedCard);
                if (card != null && CardCatalog.Get(card.definitionId).kind == CardKind.Terrain)
                    Button(root, "지형 전개", .035f, .335f, .115f, .405f, () => Play(0), gold, 19, !locked);
                else Button(root, "턴 종료", .035f, .335f, .115f, .405f, EndTurn, new Color(.2f, .25f, .32f), 18, !locked);
                Button(root, "공격 단계", .122f, .335f, .205f, .405f, () => Act(() => engine.BeginCombat(0, out message)), gold, 19, !locked);
            }
            else if (s.phase == BattlePhase.AttackDeclaration && mine) Button(root, "공격 종료 · 턴 넘기기", .035f, .335f, .205f, .405f, EndTurn, gold, 21, !locked);
            else Text(root, "상대의 판단을 기다리는 중", .035f, .335f, .205f, .41f, 19, teal);
        }
        private void DrawLanes(int owner, float bottom, float top)
        {
            var s = engine.State;
            for (int i = 0; i < s.rules.unitLanes; i++)
            {
                int lane = i; var unit = s.players[owner].AtLane(i); float left = .245f + i * (.57f / s.rules.unitLanes), right = left + .57f / s.rules.unitLanes - .012f;
                bool target = IsLegalLane(owner, lane);
                var panel = Panel(root, left, bottom, right, top, target ? new Color(.16f, .27f, .28f, .94f) : new Color(.055f, .075f, .115f, .85f));
                var outline = panel.gameObject.AddComponent<Outline>(); outline.effectColor = target ? gold : new Color(.25f, .33f, .43f); outline.effectDistance = new Vector2(2, 2);
                if (unit == null) Text(panel, "전장 " + (i + 1) + "\n" + (target ? "여기에 배치" : "비어 있음"), .05f, .2f, .95f, .8f, 23, target ? gold : new Color(.4f, .49f, .6f), TextAlignmentOptions.Center);
                else
                {
                    var d = CardCatalog.Get(unit.definitionId); Artwork(panel, d, .02f, .05f, .43f, .96f);
                    Text(panel, d.name, .46f, .68f, .97f, .94f, 23, gold);
                    Text(panel, "공격 " + (d.attack + unit.bonusPower) + "  생명 " + (d.health - unit.damage) + "\n방어 " + unit.armor + "  코인 " + (s.rules.baseCoins + unit.extraCoins), .46f, .31f, .97f, .68f, 20, Color.white);
                    Text(panel, unit.summoningSick ? "소환 대기" : unit.exhausted ? "행동 완료" : target ? "선택 가능" : "준비", .46f, .07f, .97f, .31f, 19, unit.exhausted ? Color.gray : teal);
                }
                var button = panel.gameObject.AddComponent<Button>(); button.interactable = !locked; button.onClick.AddListener(() => LaneClick(owner, lane));
                if (unit != null && unit.instanceId == selectedAttacker) outline.effectColor = Color.white;
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
            var s = engine.State; var panel = Panel(root, .275f, .30f, .79f, .77f, new Color(.025f, .045f, .085f, .99f));
            string result = s.winner == 0 ? "서약을 지켰습니다" : s.winner == 1 ? "별빛은 다시 떠오릅니다" : "두 서약이 균형을 이루었습니다";
            Text(panel, result, .06f, .66f, .94f, .87f, 42, gold, TextAlignmentOptions.Center);
            Text(panel, (s.winner == 0 ? "승리" : s.winner == 1 ? "패배" : "무승부") + " · " + s.turn + "턴\n지형과 유닛의 균형, 방어에 남길 동료를 다시 생각해 보세요.", .08f, .38f, .92f, .64f, 24, Color.white, TextAlignmentOptions.Center);
            Button(panel, "같은 결투 재도전", .09f, .16f, .48f, .30f, () => StartBattle(731029u), gold, 24, !locked);
            Button(panel, "덱을 다듬기", .53f, .16f, .91f, .30f, () => LeaveBattle(() => ShowCollection(true)), teal, 24, !locked);
        }
        private void ShowCollection(bool edit)
        {
            battleScreen = false; Clear();
            Panel(root, .015f, .015f, .985f, .985f, ink);
            Text(root, edit ? "나의 서약 · 덱 구성" : "별빛의 기록 · 카드 도감", .04f, .91f, .67f, .975f, 40, gold);
            Text(root, edit ? customDeck.Count + " / 24장 · 지형 " + customDeck.Count(id => CardCatalog.Get(id).kind == CardKind.Terrain) + " · 유닛 " + customDeck.Count(id => CardCatalog.Get(id).kind == CardKind.Unit) + " · 카드당 최대 4장" : "여명과 조류 / 지형 4종 · 유닛 6종 · 마법 3종 · 장비 3종", .04f, .865f, .78f, .91f, 23, teal);
            Button(root, "타이틀", .83f, .92f, .96f, .97f, ShowTitle, new Color(.15f, .22f, .32f));
            for (int i = 0; i < CardCatalog.All.Length; i++)
            {
                var d = CardCatalog.All[i]; float left = .04f + (i % 8) * .117f, top = .835f - (i / 8) * .385f, bottom = top - .29f;
                DrawCard(root, d, left, bottom, left + .108f, top, false, () => ShowDetail(d, () => ShowCollection(edit)), true);
                if (edit)
                {
                    Button(root, "-", left, bottom - .058f, left + .03f, bottom - .008f, () => { customDeck.Remove(d.id); ShowCollection(true); }, new Color(.2f, .25f, .34f), 24);
                    Text(root, customDeck.Count(id => id == d.id).ToString(), left + .034f, bottom - .058f, left + .074f, bottom - .008f, 25, Color.white, TextAlignmentOptions.Center);
                    Button(root, "+", left + .078f, bottom - .058f, left + .108f, bottom - .008f, () => { if (customDeck.Count < 30 && customDeck.Count(id => id == d.id) < 4) customDeck.Add(d.id); ShowCollection(true); }, new Color(.2f, .25f, .34f), 24);
                }
            }
            if (edit)
            {
                Button(root, "기본 덱 복원", .04f, .025f, .21f, .085f, () => { customDeck.Clear(); customDeck.AddRange(CardCatalog.DefaultDeck()); ShowCollection(true); }, new Color(.15f, .22f, .32f), 23);
                Button(root, "덱 저장", .24f, .025f, .40f, .085f, () => { SaveDeck(); ShowTitle(); }, gold, 24, ValidDeck(customDeck));
                Text(root, "저장 조건: 총 24장 · 지형 4장 이상 · 유닛 6장 이상", .43f, .025f, .96f, .085f, 23, Color.white);
            }
        }
        private void ShowDetail(CardDefinition d, Action back)
        {
            Clear(); Panel(root, .02f, .02f, .98f, .98f, ink);
            Artwork(root, d, .07f, .12f, .49f, .88f);
            Text(root, CardCatalog.ElementName(d.element) + " / " + CardCatalog.KindName(d.kind), .55f, .77f, .93f, .84f, 30, teal);
            Text(root, d.name, .55f, .65f, .95f, .76f, 54, gold);
            Text(root, "비용 " + d.cost + (d.kind == CardKind.Unit ? "     공격 " + d.attack + "     생명 " + d.health + "\n기본 코인 1 / 앞면마다 공격 +" + d.coinBonus : ""), .55f, .50f, .94f, .64f, 30, Color.white);
            Text(root, d.text + "\n\n" + (d.kind == CardKind.Unit ? "소환한 턴에는 공격하거나 가로막을 수 없습니다. 다음 자신의 턴에 준비됩니다. 공격 또는 방어에 참여하면 행동을 마칩니다." : d.kind == CardKind.Terrain ? "지형은 전장에 남습니다. 다음 자신의 턴마다 자원을 추가합니다. 남은 자원은 최대 12까지 유지됩니다." : "준비 단계에서 선택한 유닛에게 적용합니다. 장비는 유닛당 2개까지 장착할 수 있습니다."), .55f, .24f, .94f, .49f, 26, new Color(.83f, .87f, .92f), TextAlignmentOptions.TopLeft);
            Button(root, "돌아가기", .55f, .12f, .78f, .20f, back, gold, 27);
        }
        private void ShowTransfer()
        {
            battleScreen = false; Clear(); Panel(root, .06f, .06f, .94f, .94f, ink);
            Text(root, "전투 저장 옮기기", .09f, .82f, .85f, .90f, 42, gold);
            Text(root, "웹에서 내보낸 저장 문자열을 PC에서도 가져올 수 있습니다.\n아래 내용을 파일로 보관하거나, 다른 기기에서 복사해 붙여넣으세요.", .09f, .69f, .89f, .81f, 26, Color.white);
            var area = Panel(root, .09f, .23f, .91f, .66f, new Color(.08f, .12f, .18f));
            var input = area.gameObject.AddComponent<TMP_InputField>(); var viewport = Panel(area, .02f, .02f, .98f, .98f, Color.clear); viewport.gameObject.AddComponent<RectMask2D>();
            var text = Text(viewport, "", 0, 0, 1, 1, 18, Color.white, TextAlignmentOptions.TopLeft); input.textViewport = viewport; input.textComponent = text; input.lineType = TMP_InputField.LineType.MultiLineNewline;
            var status = Text(root, "저장을 가져오면 현재 전투 저장이 교체됩니다. 유효하지 않은 저장은 거부합니다.", .09f, .08f, .91f, .14f, 22, teal);
            Button(root, "내보내기", .09f, .15f, .28f, .21f, () => { if (PersistentSaves.TryExport(Slot, out string data, out string error)) { input.text = data; status.text = "저장 내용을 표시했습니다. 전체 선택 후 복사하세요."; } else status.text = error; }, gold);
            Button(root, "검증 후 가져오기", .31f, .15f, .58f, .21f, () => { bool ok = PersistentSaves.TryImport(Slot, GameId, 1, CardCatalog.Version, input.text, ValidatePayload, out string error); if (ok) engine = null; status.text = ok ? "가져오기 완료. 타이틀에서 이어하기를 누르세요." : "가져오기 실패: " + error; }, teal);
            Button(root, "돌아가기", .70f, .15f, .91f, .21f, ShowTitle, new Color(.2f, .25f, .34f));
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
            var panel = Panel(parent, x0, y0, x1, y1, d.element == Element.Dawn ? new Color(.25f, .19f, .12f) : new Color(.08f, .20f, .27f));
            var border = panel.gameObject.AddComponent<Outline>(); border.effectDistance = new Vector2(2, 2); border.effectColor = selected ? Color.white : d.element == Element.Dawn ? gold : teal;
            Artwork(panel, d, .035f, .31f, .965f, .96f);
            Panel(panel, .035f, .74f, .965f, .96f, new Color(.025f, .045f, .065f, .88f));
            Text(panel, d.name, .065f, .77f, .94f, .94f, 21, Color.white);
            Panel(panel, .035f, .58f, .965f, .73f, new Color(.025f, .045f, .065f, .88f));
            Text(panel, CardCatalog.ElementName(d.element) + " " + d.cost + " · " + CardCatalog.KindName(d.kind), .07f, .58f, .93f, .73f, 17, Color.white);
            Panel(panel, .035f, .025f, .965f, .31f, new Color(.025f, .045f, .065f, .95f));
            Text(panel, d.kind == CardKind.Unit ? "공격 " + d.attack + " / 생명 " + d.health + "\n앞면 공격 +" + d.coinBonus : d.text, .065f, .035f, .94f, .30f, 17, new Color(.93f, .92f, .85f), TextAlignmentOptions.Center);
            var button = panel.gameObject.AddComponent<Button>(); button.interactable = interactable; button.onClick.AddListener(() => onClick());
        }
        private void Artwork(Transform parent, CardDefinition d, float x0, float y0, float x1, float y1)
        {
            authoredIllustrations.TryGetValue(d.id, out var sprite); sprite = sprite != null ? sprite : Resources.Load<Sprite>(d.artResource); var p = Panel(parent, x0, y0, x1, y1, Color.white);
            if (sprite != null) { p.GetComponent<Image>().sprite = sprite; return; }
            if (atlas != null)
            {
                p.GetComponent<Image>().enabled = false;
                var rawObject = new GameObject("Illustration", typeof(RectTransform), typeof(RawImage)); var rawRect = rawObject.GetComponent<RectTransform>(); rawRect.SetParent(p, false); Anchor(rawRect, 0, 0, 1, 1);
                var raw = rawObject.GetComponent<RawImage>(); raw.texture = atlas; raw.raycastTarget = false;
                int index = Array.FindIndex(CardCatalog.All, c => c.id == d.id); raw.uvRect = new Rect((index % 4) * .25f, (3 - index / 4) * .25f, .25f, .25f);
            }
            else
            {
                p.GetComponent<Image>().color = d.element == Element.Dawn ? new Color(.38f, .28f, .14f) : new Color(.10f, .29f, .38f);
                Text(p, d.kind == CardKind.Terrain ? "◇" : d.kind == CardKind.Unit ? "◆" : "○", .05f, .05f, .95f, .95f, 70, new Color(1, 1, 1, .25f), TextAlignmentOptions.Center);
            }
        }
        private RectTransform Panel(Transform parent, float x0, float y0, float x1, float y1, Color color)
        {
            var go = new GameObject("Panel", typeof(RectTransform), typeof(Image)); var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false); Anchor(rt, x0, y0, x1, y1); go.GetComponent<Image>().color = color; return rt;
        }
        private TMP_Text Text(Transform parent, string value, float x0, float y0, float x1, float y1, float size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)); var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false); Anchor(rt, x0, y0, x1, y1);
            var text = go.GetComponent<TextMeshProUGUI>(); text.font = font; text.text = value; text.fontSize = size; text.color = color; text.alignment = alignment; text.raycastTarget = false; text.enableAutoSizing = true; text.fontSizeMin = size * .8f; text.fontSizeMax = size; text.overflowMode = TextOverflowModes.Ellipsis; return text;
        }
        private void Button(Transform parent, string label, float x0, float y0, float x1, float y1, Action action, Color color, float size = 22, bool enabled = true)
        {
            var panel = Panel(parent, x0, y0, x1, y1, color); var b = panel.gameObject.AddComponent<Button>(); b.interactable = enabled; var colors = b.colors; colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f); colors.disabledColor = new Color(.35f, .35f, .35f); b.colors = colors;
            b.onClick.AddListener(() => action()); Text(panel, label, .03f, .05f, .97f, .95f, size, color == gold || color == teal ? ink : Color.white, TextAlignmentOptions.Center);
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
