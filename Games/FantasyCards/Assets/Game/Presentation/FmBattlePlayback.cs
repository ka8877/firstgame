using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FantasyCards.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    public sealed partial class FantasyCardsApp
    {
        private BattleState battleVisualState;
        private bool battlePlaybackActive, battleSkipRequested;
        private float battlePlaybackSpeed = 1;
        private int battlePlaybackIndex, battlePlaybackCount;
        private string battlePlaybackKind = "", battlePlaybackCaption = "";
        private TMP_Text battleSpeedLabel;
        private readonly Dictionary<int, RectTransform> battleUnitViews = new Dictionary<int, RectTransform>();
        private readonly List<BattleEvent> battlePresentedEvents = new List<BattleEvent>();
        private readonly HashSet<int> battleRevealedSpells = new HashSet<int>();
        private readonly Dictionary<int, List<bool>> battleCoinResults = new Dictionary<int, List<bool>>();
        private BattleEvent battleClashContext;
        private BattleState BattleDisplayState => battleVisualState ?? engine.State;

        // Read-only observations for native/browser verification. Playback never owns battle state.
        public bool IsBattlePlaybackActive => battlePlaybackActive;
        public BattleState BattlePlaybackDisplaySnapshot => engine == null ? null : BattleDisplayState.Copy();
        public int BattlePlaybackEventIndex => battlePlaybackIndex;
        public int BattlePlaybackEventCount => battlePlaybackCount;
        public string BattlePlaybackEventKind => battlePlaybackKind;
        public float BattlePlaybackSpeed => battlePlaybackSpeed;
        public IReadOnlyList<BattleEvent> LastBattlePlaybackEvents => battlePresentedEvents.AsReadOnly();

        private IEnumerator ReplayBattleFrames(BattleState before, List<BattlePlaybackFrame> frames)
        {
            locked = battlePlaybackActive = true; battleSkipRequested = false;
            battlePlaybackIndex = 0; battlePlaybackCount = frames.Count;
            battlePresentedEvents.Clear(); battleRevealedSpells.Clear(); battleCoinResults.Clear(); battlePlaybackCaption = "전투 진행";
            battleVisualState = before; ShowBattle();
            // All events are retained. Speed and skip only shorten presentation, never reroll a coin.
            for (int i = 0; i < frames.Count; i++)
            {
                var frame = frames[i]; var item = frame.item;
                battlePlaybackIndex = i + 1; battlePlaybackKind = item.kind;
                battlePresentedEvents.Add(item.Copy());
                if (item.kind == "spell" && item.sourceInstanceId >= 0) battleRevealedSpells.Add(item.sourceInstanceId);
                if (item.kind == "coin" && item.sourceInstanceId >= 0)
                {
                    if (!battleCoinResults.TryGetValue(item.sourceInstanceId, out var faces))
                    { faces = new List<bool>(); battleCoinResults[item.sourceInstanceId] = faces; }
                    faces.Add(item.coinHeads);
                }
                if (item.kind == "coin" || item.kind == "power")
                    battleClashContext = frames.Skip(i).Select(next => next.item).FirstOrDefault(next =>
                        (next.kind == "attack" || next.kind == "counter" || next.kind == "direct") &&
                        (next.sourceInstanceId == item.sourceInstanceId || next.targetInstanceId == item.sourceInstanceId)) ?? item;
                else if (item.kind == "attack" || item.kind == "counter" || item.kind == "direct") battleClashContext = item;
                else if (item.kind != "defeat" && item.kind != "damage") battleClashContext = null;
                battlePlaybackCaption = PlaybackCaption(item);
                if (!battleSkipRequested)
                {
                    ShowBattle();
                    yield return PresentBattleEvent(item);
                }
                battleVisualState.players = frame.players.ConvertAll(player => player.Copy());
                battleVisualState.turn = frame.turn; battleVisualState.activePlayer = frame.activePlayer;
                battleVisualState.phase = frame.phase; battleVisualState.winner = frame.winner;
            }
            battleVisualState = null; battlePlaybackCaption = ""; battlePlaybackKind = ""; battleClashContext = null;
            battlePlaybackActive = false; locked = false; message = Hint();
            ShowBattle(); PumpAi();
        }

        private void DrawBattlePlaybackControls()
        {
            if (!battlePlaybackActive) return;
            // These cover the disabled Ready control while every state-changing action stays locked.
            float buttonTop = .058f + Mathf.Max(.073f, 44f / Mathf.Max(1, canvas.pixelRect.height));
            var speed = Button(root, "×" + battlePlaybackSpeed.ToString("0"), .822f, .058f, .891f, buttonTop,
                () => { battlePlaybackSpeed = battlePlaybackSpeed < 1.5f ? 2 : battlePlaybackSpeed < 3 ? 4 : 1;
                    if (battleSpeedLabel != null) battleSpeedLabel.text = "×" + battlePlaybackSpeed.ToString("0"); }, slate, 23);
            battleSpeedLabel = speed.GetComponentInChildren<TMP_Text>();
            speed.gameObject.name = "Battle playback speed";
            BindTooltip(speed.gameObject, "전투 연출 속도", "연출만 1·2·4배속으로 바꿉니다. 카드 판정과 코인 결과는 바뀌지 않습니다.");
            var skip = Button(root, "끝까지", .897f, .058f, .974f, buttonTop,
                () => { battleSkipRequested = true; }, gold, 22);
            skip.gameObject.name = "Battle playback skip";
            BindTooltip(skip.gameObject, "현재 연출 건너뛰기", "이미 확정된 이번 행동의 나머지 연출만 건너뜁니다. 전투 결과와 저장은 그대로입니다.");
        }

        private IEnumerator PresentBattleEvent(BattleEvent item)
        {
            bool attack = item.kind == "attack" || item.kind == "counter" || item.kind == "direct";
            bool hit = item.kind == "attack" || item.kind == "counter";
            bool spell = item.kind == "spell" || item.kind == "spell-target";
            bool coin = item.kind == "coin";
            bool defeat = item.kind == "defeat";
            bool burn = item.kind == "burn";
            bool play = item.kind == "play";
            bool terrainPlay = play && !string.IsNullOrEmpty(item.sourceDefinitionId) && CardCatalog.Get(item.sourceDefinitionId).kind == CardKind.Terrain;
            bool heal = spell && (item.stat == "Health" || item.stat == "Heal") && item.amount >= 0;
            float duration = coin ? .58f : attack ? .65f : defeat ? .54f : burn ? .40f : spell ? .54f : play ? .4f : item.kind == "power" ? .46f : .25f;

            RectTransform source = null, target = null;
            if (item.sourceInstanceId >= 0) battleUnitViews.TryGetValue(item.sourceInstanceId, out source);
            if (item.targetInstanceId >= 0) battleUnitViews.TryGetValue(item.targetInstanceId, out target);
            Vector2 sourcePoint = source != null ? BattleViewCenter(source) : LaneCenter(item.sourcePlayer >= 0 ? item.sourcePlayer : item.actor, item.sourceLane);
            Vector2 targetPoint = target != null ? BattleViewCenter(target) : LaneCenter(item.targetPlayer, item.targetLane);
            if (item.kind == "direct" || burn) targetPoint = DeckCenter(item.targetPlayer >= 0 ? item.targetPlayer : item.actor);
            if (defeat) targetPoint = sourcePoint;
            if (item.kind == "spell" && target == null) targetPoint = sourcePoint;
            if (terrainPlay)
            {
                int owner = item.sourcePlayer >= 0 ? item.sourcePlayer : item.actor;
                sourcePoint = new Vector2(.42f, owner == 0 ? .17f : .97f);
                int index = battleVisualState.players[owner].terrain.Count;
                targetPoint = BackfieldCardCenter(owner, index);
            }

            var layer = new GameObject("Battle event " + item.sequence + " " + item.kind, typeof(RectTransform)).GetComponent<RectTransform>();
            layer.SetParent(root, false); Anchor(layer, 0, 0, 1, 1);
            if (battleClashContext != null)
            {
                // The 2017 original moves the pair into a central duel. These are visual copies only.
                Panel(layer, .025f, .706f, .805f, .912f, new Color(.01f, .025f, .02f, .73f));
                Panel(layer, .025f, .442f, .805f, .648f, new Color(.01f, .025f, .02f, .73f));
                var copies = new Dictionary<int, RectTransform>();
                AddClashCard(layer, battleClashContext.sourcePlayer, battleClashContext.sourceInstanceId, copies);
                AddClashCard(layer, battleClashContext.targetPlayer, battleClashContext.targetInstanceId, copies);
                if (copies.TryGetValue(item.sourceInstanceId, out var centralSource))
                { source = centralSource; sourcePoint = ClashCenter(item.sourcePlayer); }
                if (copies.TryGetValue(item.targetInstanceId, out var centralTarget))
                { target = centralTarget; targetPoint = ClashCenter(item.targetPlayer); }
                if (defeat) targetPoint = sourcePoint;
            }
            var group = layer.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = group.interactable = false;
            FmBattleFxGraphic effect = null;
            TMP_Text number = null;
            RectTransform flyingCard = null, coinFace = null;
            Vector3 sourcePosition = source != null ? source.localPosition : Vector3.zero;
            Vector3 targetPosition = target != null ? target.localPosition : Vector3.zero;
            Vector3 sourceScale = source != null ? source.localScale : Vector3.one;
            Vector3 targetScale = target != null ? target.localScale : Vector3.one;
            Quaternion sourceRotation = source != null ? source.localRotation : Quaternion.identity;
            CanvasGroup sourceOpacity = null;
            Color tint = EventColor(item);

            if (coin && battleClashContext != null)
            {
                coinFace = DrawClashCoinTray(layer, item); FmBattleAudio.Play(audioSource, "coin");
            }
            else if (coin)
            {
                var coinPanel = Panel(layer, sourcePoint.x - .026f, sourcePoint.y - .035f, sourcePoint.x + .026f, sourcePoint.y + .057f,
                    item.coinHeads ? gold : new Color(.24f, .30f, .29f));
                coinFace = coinPanel; coinFace.gameObject.name = "Actual coin " + item.coinIndex + (item.coinHeads ? " heads" : " tails");
                coinPanel.GetComponent<UiRoundedImage>().Radius = 1000;
                var fit = coinPanel.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 1;
                Frame(coinPanel, item.coinHeads ? ivory : new Color(.6f, .7f, .65f), 2);
                Text(coinPanel, item.coinHeads ? "앞" : "뒤", .05f, .05f, .95f, .95f, 28, item.coinHeads ? ink : ivory, TextAlignmentOptions.Center);
                Text(layer, "C" + item.coinIndex, sourcePoint.x - .05f, sourcePoint.y - .086f, sourcePoint.x + .05f, sourcePoint.y - .038f, 23, ivory, TextAlignmentOptions.Center);
                FmBattleAudio.Play(audioSource, "coin");
            }
            else if (attack || spell || defeat || burn || play)
            {
                var style = defeat ? FmBattleFxGraphic.EffectStyle.Defeat : burn ? FmBattleFxGraphic.EffectStyle.Burn :
                    heal ? FmBattleFxGraphic.EffectStyle.Heal : spell ? FmBattleFxGraphic.EffectStyle.Spell :
                    play ? FmBattleFxGraphic.EffectStyle.Summon : item.amount <= 0 && hit ? FmBattleFxGraphic.EffectStyle.Shield : FmBattleFxGraphic.EffectStyle.Slash;
                effect = CreateBattleFx(layer, style, targetPoint, tint);
                if (item.kind == "direct") effect.Style = FmBattleFxGraphic.EffectStyle.Impact;
                if (play && !terrainPlay) { targetPoint = sourcePoint; PositionBattleFx(effect, targetPoint); }
                if (terrainPlay) flyingCard = BattleMovingCard(layer, item.sourceDefinitionId, sourcePoint, false);
                string amount = defeat ? "소각장" : burn ? "소각 −1" : hit ? item.amount == 0 ? "방어" : "−" + item.amount :
                    item.kind == "spell-target" ? (item.amount >= 0 ? "+" : "") + item.amount : "";
                if (!string.IsNullOrEmpty(amount))
                    number = Text(layer, amount, targetPoint.x - .10f, targetPoint.y - .025f, targetPoint.x + .10f, targetPoint.y + .08f, 40, tint, TextAlignmentOptions.Center);
                FmBattleAudio.Play(audioSource, defeat ? "defeat" : burn ? "burn" : heal ? "heal" : spell ? "spell" : attack ? "attack" : "play");
                if (defeat && source != null)
                {
                    sourceOpacity = source.GetComponent<CanvasGroup>();
                    if (sourceOpacity == null) sourceOpacity = source.gameObject.AddComponent<CanvasGroup>();
                }
                if (burn && !string.IsNullOrEmpty(item.targetDefinitionId))
                    flyingCard = BattleMovingCard(layer, item.targetDefinitionId, targetPoint, false);
            }
            else if (item.kind == "power")
            {
                effect = CreateBattleFx(layer, FmBattleFxGraphic.EffectStyle.Summon, sourcePoint, gold);
                number = Text(layer, "AP " + item.attack + " · DP " + item.defense + " · HP " + item.health,
                    sourcePoint.x - .115f, sourcePoint.y - .025f, sourcePoint.x + .115f, sourcePoint.y + .08f, 26, ivory, TextAlignmentOptions.Center);
            }
            else if (item.kind == "draw")
            {
                sourcePoint = DeckCenter(item.actor); targetPoint = new Vector2(.42f, item.actor == 0 ? .17f : .97f);
                flyingCard = BattleMovingCard(layer, item.sourceDefinitionId, sourcePoint, item.actor != 0);
                FmBattleAudio.Play(audioSource, "play");
            }
            else if (item.kind == "phase" || item.kind == "result") FmBattleAudio.Play(audioSource, "phase");

            bool impactPlayed = false;
            for (float elapsed = 0; elapsed < duration && !battleSkipRequested; elapsed += Time.unscaledDeltaTime * battlePlaybackSpeed)
            {
                float p = Mathf.Clamp01(elapsed / duration), punch = Mathf.Sin(Mathf.PI * Mathf.Clamp01(p * 1.5f));
                if (effect != null) { effect.Progress = p; effect.SetAllDirty(); }
                if (coinFace != null)
                {
                    float flip = p < .74f ? Mathf.Max(.075f, Mathf.Abs(Mathf.Cos(p * Mathf.PI * 5))) : Mathf.SmoothStep(.15f, 1, (p - .74f) / .26f);
                    coinFace.localScale = new Vector3(flip, 1, 1);
                }
                if (source != null && attack)
                {
                    Vector3 direction = source.parent.InverseTransformVector(root.TransformVector(new Vector3((targetPoint.x - sourcePoint.x) * root.rect.width, (targetPoint.y - sourcePoint.y) * root.rect.height, 0))).normalized;
                    source.localPosition = sourcePosition + direction * Mathf.Min(root.rect.height * .06f, 64) * punch;
                    source.localScale = sourceScale * (1 + punch * .055f);
                }
                if (target != null && hit && p >= .30f)
                    target.localPosition = targetPosition + Vector3.right * Mathf.Sin(p * 70) * (1 - p) * (item.amount > 0 ? 8 : 3);
                if (source != null && defeat)
                {
                    source.localScale = sourceScale * (1 - p * .62f); source.localRotation = Quaternion.Euler(0, 0, -p * 16);
                    if (sourceOpacity != null) sourceOpacity.alpha = 1 - p;
                }
                if (number != null)
                {
                    number.rectTransform.anchoredPosition = Vector2.up * (Mathf.SmoothStep(0, 1, p) * 28);
                    var c = number.color; c.a = Mathf.Clamp01((1 - p) * 4); number.color = c;
                }
                if (flyingCard != null)
                {
                    Vector2 position = item.kind == "draw" || terrainPlay ? Vector2.Lerp(sourcePoint, targetPoint, Mathf.SmoothStep(0, 1, p)) :
                        Vector2.Lerp(targetPoint, GraveyardCenter(item.targetPlayer >= 0 ? item.targetPlayer : item.actor), Mathf.SmoothStep(0, 1, p));
                    Anchor(flyingCard, position.x - .024f, position.y - .05f, position.x + .024f, position.y + .05f);
                    flyingCard.localRotation = Quaternion.Euler(0, 0, p * (burn ? 30 : -6));
                }
                if (attack && p >= .34f && !impactPlayed)
                { impactPlayed = true; FmBattleAudio.Play(audioSource, hit && item.amount <= 0 ? "shield" : "hit"); }
                yield return null;
            }
            if (source != null) { source.localPosition = sourcePosition; source.localScale = sourceScale; source.localRotation = sourceRotation; }
            if (target != null) { target.localPosition = targetPosition; target.localScale = targetScale; }
            if (sourceOpacity != null) sourceOpacity.alpha = 1;
            if (layer != null) Destroy(layer.gameObject);
        }

        private Vector2 BattleViewCenter(RectTransform view)
        {
            var point = root.InverseTransformPoint(view.TransformPoint(view.rect.center));
            return new Vector2((point.x - root.rect.xMin) / root.rect.width, (point.y - root.rect.yMin) / root.rect.height);
        }
        private Vector2 BackfieldCardCenter(int owner, int index)
        {
            float slot = .81f / Mathf.Max(1, BattleDisplayState.rules.terrainLimit);
            return new Vector2(.025f + .78f * (.18f + slot * (Mathf.Clamp(index, 0, BattleDisplayState.rules.terrainLimit - 1) + .5f)), owner == 0 ? .3975f : .957f);
        }
        private Vector2 LaneCenter(int owner, int lane) => new Vector2(lane >= 0 ? .025f + (.78f / engine.State.rules.unitLanes) * (lane + .5f) : .42f, owner == 1 ? .809f : .545f);
        private static Vector2 DeckCenter(int owner) => new Vector2(.90f, owner == 1 ? .49f : .42f);
        private static Vector2 GraveyardCenter(int owner) => new Vector2(.90f, owner == 1 ? .43f : .37f);
        private static Vector2 ClashCenter(int owner) => new Vector2(.416f, owner == 1 ? .809f : .545f);
        private void AddClashCard(Transform parent, int owner, int instanceId, Dictionary<int, RectTransform> copies)
        {
            if (owner < 0 || owner > 1 || instanceId < 0 || copies.ContainsKey(instanceId)) return;
            var unit = battleVisualState.players[owner].units.Find(value => value.instanceId == instanceId);
            // No opponent hand or unrevealed reserved spell is ever materialized here.
            if (unit == null) return;
            var definition = CardCatalog.Get(unit.definitionId); var point = ClashCenter(owner);
            var card = ArcanaCardFace(parent, definition, point.x - .083f, point.y - .12f, point.x + .083f, point.y + .12f,
                ArcanaCardMode.Duel, false, unit);
            card.gameObject.name = "Central duel card " + instanceId;
            copies[instanceId] = card;

        }
        private RectTransform DrawClashCoinTray(Transform parent, BattleEvent item)
        {
            Panel(parent, .025f, .930f, .805f, .995f, new Color(.02f, .035f, .025f, .99f));
            Text(parent, "코인 판정", .035f, .935f, .23f, .99f, 25, gold, TextAlignmentOptions.Center);
            var tray = Panel(parent, .24f, .931f, .61f, .994f, new Color(.035f, .05f, .035f, .99f));
            tray.gameObject.name = "Central actual coin tray"; Frame(tray, gold, 1.5f);
            RectTransform current = null;
            var faces = battleCoinResults[item.sourceInstanceId];
            for (int i = 0; i < faces.Count; i++)
            {
                var cell = Panel(tray, .025f + i * .19f, .08f, .195f + i * .19f, .92f, faces[i] ? gold : new Color(.24f, .30f, .29f));
                cell.GetComponent<UiRoundedImage>().Radius = 1000;
                var fit = cell.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 1;
                Text(cell, faces[i] ? "앞" : "뒤", .03f, .03f, .97f, .97f, 25, faces[i] ? ink : ivory, TextAlignmentOptions.Center);
                Frame(cell, ivory, 1); current = cell;
            }
            Text(parent, "C" + item.coinIndex + " · " + (item.coinHeads ? "앞면" : "뒷면"), .62f, .935f, .79f, .99f, 24, gold, TextAlignmentOptions.Center);
            return current;
        }
        private FmBattleFxGraphic CreateBattleFx(Transform parent, FmBattleFxGraphic.EffectStyle style, Vector2 point, Color tint)
        {
            var fx = new GameObject("Battle " + style + " effect", typeof(RectTransform), typeof(FmBattleFxGraphic)).GetComponent<FmBattleFxGraphic>();
            fx.transform.SetParent(parent, false); fx.Style = style; fx.Tint = tint; PositionBattleFx(fx, point); return fx;
        }
        private static void PositionBattleFx(FmBattleFxGraphic effect, Vector2 point)
        { Anchor((RectTransform)effect.transform, point.x - .078f, point.y - .12f, point.x + .078f, point.y + .12f); }
        private RectTransform BattleMovingCard(Transform parent, string definition, Vector2 point, bool hidden)
        {
            if (!hidden && !string.IsNullOrEmpty(definition))
                return ArcanaCardFace(parent, CardCatalog.Get(definition), point.x - .029f, point.y - .065f,
                    point.x + .029f, point.y + .065f, ArcanaCardMode.Field);
            var card = Panel(parent, point.x - .029f, point.y - .065f, point.x + .029f, point.y + .065f, Color.clear);
            ArcanaHiddenBack(card, 0, 0, 1, 1);
            return card;

        }
        private Color EventColor(BattleEvent item)
        {
            if (item.kind == "burn" || item.kind == "defeat") return new Color(1, .53f, .24f);
            if (item.kind == "attack" || item.kind == "counter") return item.amount == 0 ? new Color(.62f, .87f, 1) : new Color(1, .78f, .42f);
            if (!string.IsNullOrEmpty(item.sourceDefinitionId))
            {
                var card = CardCatalog.Get(item.sourceDefinitionId);
                // An enemy's reserved spell must not reveal its face or attribute before reveal.
                if (item.kind == "play" && item.sourcePlayer == 1 && card.kind == CardKind.Spell) return gold;
                return Color.Lerp(ElementColor(card.element), ivory, .35f);
            }
            return gold;
        }
        private static string PlaybackCaption(BattleEvent item)
        {
            switch (item.kind)
            {
                case "attack": return "공격 · " + item.text;
                case "counter": return "반격 · " + item.text;
                case "coin": return "코인 판정 · " + item.text;
                case "power": return "능력 확정 · " + item.text;
                case "burn": return "덱 소각 · " + item.text;
                case "defeat": return "전장 이탈 · " + item.text;
                default: return item.text;
            }
        }
    }
}
