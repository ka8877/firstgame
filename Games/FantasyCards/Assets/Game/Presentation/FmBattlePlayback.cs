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
        private string polishClashPair = "";
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
            battlePlaybackIndex = 0; battlePlaybackCount = frames.Count; polishClashPair = "";
            chronicleCoinTotals.Clear();
            foreach (var group in frames.Where(frame => frame.item.kind == "coin").GroupBy(frame => frame.item.sourceInstanceId))
                chronicleCoinTotals[group.Key] = group.Max(frame => frame.item.coinIndex);
            battlePresentedEvents.Clear(); battleRevealedSpells.Clear(); battleCoinResults.Clear(); battlePlaybackCaption = "전투 진행";
            battleVisualState = before; ShowBattle();
            // All events are retained. Speed and skip only shorten presentation, never reroll a coin.
            for (int i = 0; i < frames.Count; i++)
            {
                var frame = frames[i]; var item = frame.item;
                liveCardImpactFrame = frame; BattlePlaybackStatsCommitted = false;
                battlePlaybackIndex = i + 1; battlePlaybackKind = item.kind;
                battlePresentedEvents.Add(item.Copy());
                if (item.kind == "spell" && item.sourceInstanceId >= 0) battleRevealedSpells.Add(item.sourceInstanceId);
                if (item.kind == "coin" && item.sourceInstanceId >= 0)
                {
                    if (!battleCoinResults.TryGetValue(item.sourceInstanceId, out var faces))
                    { faces = new List<bool>(); battleCoinResults[item.sourceInstanceId] = faces; }
                    if (item.coinIndex == 1) faces.Clear();
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
            liveCardImpactFrame = null; BattlePlaybackStatsCommitted = false;
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
            bool reposition = item.kind == "reposition";
            bool fieldDiscard = item.kind == "discard-field";
            bool summon = play && !string.IsNullOrEmpty(item.sourceDefinitionId) && CardCatalog.Get(item.sourceDefinitionId).kind == CardKind.Unit;
            bool terrainPlay = play && !string.IsNullOrEmpty(item.sourceDefinitionId) && CardCatalog.Get(item.sourceDefinitionId).kind == CardKind.Terrain;
            bool terrainDiscard = fieldDiscard && !string.IsNullOrEmpty(item.sourceDefinitionId) && CardCatalog.Get(item.sourceDefinitionId).kind == CardKind.Terrain;
            bool healingSpell = item.kind == "spell" && !string.IsNullOrEmpty(item.sourceDefinitionId) &&
                (CardCatalog.Get(item.sourceDefinitionId).effect == Effect.Heal || CardCatalog.Get(item.sourceDefinitionId).effect == Effect.HealAll);
            bool heal = spell && (healingSpell || (item.stat == "Health" || item.stat == "Heal") && item.amount >= 0);
            float duration = coin ? .78f : attack ? .92f : defeat || fieldDiscard ? .74f : burn ? .48f : spell ? .72f : summon ? .68f : play ? .48f : reposition ? .48f : item.kind == "power" ? .52f : .25f;

            RectTransform source = null, target = null;
            if (item.sourceInstanceId >= 0) battleUnitViews.TryGetValue(item.sourceInstanceId, out source);
            if (item.targetInstanceId >= 0) battleUnitViews.TryGetValue(item.targetInstanceId, out target);
            if (terrainDiscard)
            {
                int owner = item.sourcePlayer >= 0 ? item.sourcePlayer : item.actor;
                int position = battleVisualState.players[owner].terrain.FindIndex(c => c.instanceId == item.sourceInstanceId);
                source = root.GetComponentsInChildren<RectTransform>().FirstOrDefault(view => view.name == "Backfield " + owner + " " + position);
            }
            Vector2 sourcePoint = source != null ? BattleViewCenter(source) : LaneCenter(item.sourcePlayer >= 0 ? item.sourcePlayer : item.actor, item.sourceLane);
            Vector2 targetPoint = target != null ? BattleViewCenter(target) : LaneCenter(item.targetPlayer, item.targetLane);
            if (item.kind == "direct" || burn) targetPoint = DeckCenter(item.targetPlayer >= 0 ? item.targetPlayer : item.actor);
            if (defeat || fieldDiscard) targetPoint = sourcePoint;
            if (reposition) targetPoint = LaneCenter(item.sourcePlayer >= 0 ? item.sourcePlayer : item.actor, item.targetLane);
            Vector2 summonOrigin = new Vector2(sourcePoint.x, item.actor == 0 ? .105f : .965f);
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
            var arrivals = new Dictionary<RectTransform, Vector3>();
            var arrivalDestinations = new Dictionary<RectTransform, Vector3>();
            if (battleClashContext != null)
            {
                // The 2017 original moves the pair into a central duel. These are visual copies only.
                ChroniclePanel(layer, .025f, .660f, .805f, .910f, new Color(.004f, .011f, .035f, .62f), false);
                ChroniclePanel(layer, .025f, .365f, .805f, .615f, new Color(.004f, .011f, .035f, .62f), false);
                var copies = new Dictionary<int, RectTransform>();
                AddClashCard(layer, battleClashContext.sourcePlayer, battleClashContext.sourceInstanceId, copies);
                AddClashCard(layer, battleClashContext.targetPlayer, battleClashContext.targetInstanceId, copies);
                string pair = Mathf.Min(battleClashContext.sourceInstanceId, battleClashContext.targetInstanceId) + ":" +
                    Mathf.Max(battleClashContext.sourceInstanceId, battleClashContext.targetInstanceId);
                if (pair != polishClashPair && (coin || item.kind == "power"))
                {
                    foreach (var entry in copies)
                    {
                        if (!battleUnitViews.TryGetValue(entry.Key, out var original)) continue;
                        int owner = entry.Key == battleClashContext.sourceInstanceId ? battleClashContext.sourcePlayer : battleClashContext.targetPlayer;
                        Vector2 offset = BattleViewCenter(original) - ClashCenter(owner);
                        Vector3 displacement = entry.Value.parent.InverseTransformVector(root.TransformVector(new Vector3(offset.x * root.rect.width, offset.y * root.rect.height, 0)));
                        arrivals[entry.Value] = entry.Value.localPosition + displacement;
                        arrivalDestinations[entry.Value] = entry.Value.localPosition;
                    }
                }
                polishClashPair = pair;
                if (copies.TryGetValue(item.sourceInstanceId, out var centralSource))
                { source = centralSource; sourcePoint = ClashCenter(item.sourcePlayer); }
                if (copies.TryGetValue(item.targetInstanceId, out var centralTarget))
                { target = centralTarget; targetPoint = ClashCenter(item.targetPlayer); }
                if (defeat) targetPoint = sourcePoint;
            }
            var group = layer.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = group.interactable = false;
            var hiddenOriginals = new Dictionary<CanvasGroup, float>();
            if (reposition || defeat || fieldDiscard)
            {
                // Moving inside a lane's stencil clips the card at the edge.
                // Use public visual copies on the event layer, then restore the
                // untouched lane views before applying the next real snapshot.
                void Hide(RectTransform original)
                {
                    if (original == null) return;
                    var opacity = original.GetComponent<CanvasGroup>();
                    if (opacity == null) opacity = original.gameObject.AddComponent<CanvasGroup>();
                    hiddenOriginals[opacity] = opacity.alpha; opacity.alpha = 0;
                }
                Hide(source);
                source = BattleFieldCopy(layer, item.sourcePlayer >= 0 ? item.sourcePlayer : item.actor,
                    item.sourceInstanceId, item.sourceDefinitionId, sourcePoint);
                if (reposition && target != null && item.targetInstanceId >= 0)
                {
                    Hide(target);
                    target = BattleFieldCopy(layer, item.targetPlayer, item.targetInstanceId, item.targetDefinitionId, targetPoint);
                }
            }
            FmBattleFxGraphic effect = null, impactEffect = null;
            TMP_Text number = null;
            RectTransform flyingCard = null, coinFace = null;
            Vector3 sourcePosition = source != null ? source.localPosition : Vector3.zero;
            Vector3 targetPosition = target != null ? target.localPosition : Vector3.zero;
            Vector3 sourceScale = source != null ? source.localScale : Vector3.one;
            Vector3 targetScale = target != null ? target.localScale : Vector3.one;
            Quaternion sourceRotation = source != null ? source.localRotation : Quaternion.identity;
            CanvasGroup sourceOpacity = null;
            Color tint = EventColor(item);
            coinFace = DrawChronicleCoinWindows(layer, item);

            if (coin && battleClashContext != null)
            {
                FmBattleAudio.PlayCoin(item.coinHeads, item.coinIndex, (sourcePoint.x - .415f) * 1.5f);
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
                FmBattleAudio.PlayCoin(item.coinHeads, item.coinIndex, (sourcePoint.x - .415f) * 1.5f);
            }
            else if (attack || spell || defeat || fieldDiscard || burn || play)
            {
                var style = defeat || fieldDiscard ? FmBattleFxGraphic.EffectStyle.Defeat : burn ? FmBattleFxGraphic.EffectStyle.Burn :
                    heal ? FmBattleFxGraphic.EffectStyle.Heal : spell ? FmBattleFxGraphic.EffectStyle.Spell :
                    summon ? FmBattleFxGraphic.EffectStyle.Summon : play ? FmBattleFxGraphic.EffectStyle.Impact : item.amount <= 0 && hit ? FmBattleFxGraphic.EffectStyle.Shield : FmBattleFxGraphic.EffectStyle.Slash;
                effect = CreateBattleFx(layer, style, targetPoint, tint);
                if (attack && item.amount > 0) impactEffect = CreateBattleFx(layer, FmBattleFxGraphic.EffectStyle.Impact, targetPoint, new Color(1, .79f, .40f));
                if (item.kind == "direct") effect.Style = FmBattleFxGraphic.EffectStyle.Impact;
                if (play && !terrainPlay) { targetPoint = sourcePoint; PositionBattleFx(effect, targetPoint); }
                if (terrainPlay) flyingCard = BattleMovingCard(layer, item.sourceDefinitionId, sourcePoint, false);
                if (summon) flyingCard = BattleMovingCard(layer, item.sourceDefinitionId, summonOrigin, false);
                string amount = defeat || fieldDiscard ? "소각장" : burn ? "소각 −1" : hit ? item.amount == 0 ? "방어" : "−" + item.amount :
                    item.kind == "spell-target" ? (item.amount >= 0 ? "+" : "") + item.amount : "";
                if (!string.IsNullOrEmpty(amount))
                {
                    number = Text(layer, amount, targetPoint.x - .10f, targetPoint.y - .025f, targetPoint.x + .10f, targetPoint.y + .08f, hit && item.amount >= 6 ? 58 : 46, hit ? Color.white : tint, TextAlignmentOptions.Center);
                    number.fontStyle = FontStyles.Bold; number.outlineWidth = .24f;
                    number.outlineColor = hit && item.amount > 0 ? new Color32(115, 20, 8, 255) : new Color32(6, 15, 24, 255);
                }
                string playCue = terrainPlay ? "terrain" : play && !string.IsNullOrEmpty(item.sourceDefinitionId) &&
                    CardCatalog.Get(item.sourceDefinitionId).kind == CardKind.Equipment ? "equip" :
                    play && !string.IsNullOrEmpty(item.sourceDefinitionId) && CardCatalog.Get(item.sourceDefinitionId).kind == CardKind.Spell ? "card-draw" : "summon";
                FmBattleAudio.PlayAt(defeat || fieldDiscard ? "destroy" : burn ? "burn" : heal ? "heal" : spell ? "spell" :
                    attack ? item.kind == "counter" ? "counter" : "attack" : playCue, (sourcePoint.x - .415f) * 1.5f);
                if ((defeat || fieldDiscard) && source != null)
                {
                    sourceOpacity = source.GetComponent<CanvasGroup>();
                    if (sourceOpacity == null) sourceOpacity = source.gameObject.AddComponent<CanvasGroup>();
                }
                if (burn && !string.IsNullOrEmpty(item.targetDefinitionId))
                    flyingCard = BattleMovingCard(layer, item.targetDefinitionId, targetPoint, false);
            }
            else if (reposition) FmBattleAudio.Play("card-pick");
            else if (item.kind == "power")
            {
                if (item.hasStatTransition)
                {
                    int change = item.afterAttack + item.afterDefense + item.afterHealth - item.beforeAttack - item.beforeDefense - item.beforeHealth;
                    if (change != 0) FmBattleAudio.Play(change > 0 ? "stat-up" : "stat-down");
                }
            }
            else if (item.kind == "draw")
            {
                sourcePoint = DeckCenter(item.actor); targetPoint = new Vector2(.42f, item.actor == 0 ? .17f : .97f);
                flyingCard = BattleMovingCard(layer, item.sourceDefinitionId, sourcePoint, item.actor != 0);
                FmBattleAudio.Play("card-draw");
            }
            else if (item.kind == "phase" || item.kind == "turn") FmBattleAudio.Play("turn");
            else if (item.kind == "ready") FmBattleAudio.Play("confirm");
            else if (item.kind == "discard") FmBattleAudio.Play("burn");
            else if (item.kind == "result") FmBattleAudio.Play(item.amount == 0 ? "victory" : item.amount == 1 ? "loss" : "turn");

            DrawRestageFactionHeaders(layer);

            bool impactPlayed = false;
            for (float elapsed = 0; elapsed < duration && !battleSkipRequested; elapsed += Time.unscaledDeltaTime * battlePlaybackSpeed)
            {
                float p = Mathf.Clamp01(elapsed / duration);
                // Keep positions and card visibility at the pre-event state
                // for movement/destruction. Commit only the numeric views at
                // the actual coin settle, equipment landing or damage impact.
                float statImpact = coin ? .62f : attack ? .42f : play ? .52f : spell ? .45f : 0;
                if (!BattlePlaybackStatsCommitted && p >= statImpact) CommitLiveCardImpact();
                if (effect != null) { effect.Progress = attack ? Mathf.InverseLerp(.34f, 1, p) : summon ? Mathf.InverseLerp(.22f, 1, p) : p; effect.SetAllDirty(); }
                if (impactEffect != null) { impactEffect.Progress = Mathf.InverseLerp(.42f, .93f, p); impactEffect.SetAllDirty(); }
                foreach (var arrival in arrivals)
                    if (arrival.Key != null) arrival.Key.localPosition = Vector3.Lerp(arrival.Value, arrivalDestinations[arrival.Key], Mathf.SmoothStep(0, 1, p / .32f));
                if (coinFace != null)
                {
                    float flip = p < .62f ? Mathf.Max(.075f, Mathf.Abs(Mathf.Cos(p * Mathf.PI * 5))) : Mathf.SmoothStep(.15f, 1, (p - .62f) / .12f);
                    coinFace.localScale = new Vector3(flip, 1, 1);
                }
                if (source != null && attack)
                {
                    Vector3 direction = source.parent.InverseTransformVector(root.TransformVector(new Vector3((targetPoint.x - sourcePoint.x) * root.rect.width, (targetPoint.y - sourcePoint.y) * root.rect.height, 0))).normalized;
                    // Wind up, strike, hold the impact briefly, then return.
                    float movement = p < .20f ? -.22f * Mathf.SmoothStep(0, 1, p / .20f) :
                        p < .42f ? Mathf.Lerp(-.22f, 1, Mathf.SmoothStep(0, 1, (p - .20f) / .22f)) :
                        p < .50f ? 1 : 1 - Mathf.SmoothStep(0, 1, (p - .50f) / .50f);
                    source.localPosition = sourcePosition + direction * Mathf.Min(root.rect.height * .065f, 70) * movement;
                    source.localScale = sourceScale * (1 + Mathf.Max(0, movement) * .045f);
                    source.localRotation = sourceRotation * Quaternion.Euler(0, 0, movement * (item.sourcePlayer == 0 ? -2.5f : 2.5f));
                }
                if (target != null && hit && p >= .42f)
                    target.localPosition = targetPosition + Vector3.right * Mathf.Sin((p - .42f) * 74) * (1 - p) * (item.amount > 0 ? 10 : 3);
                if (source != null && (defeat || fieldDiscard))
                {
                    Vector2 end = GraveyardCenter(item.sourcePlayer >= 0 ? item.sourcePlayer : item.actor);
                    Vector2 offset = (end - sourcePoint) * Mathf.SmoothStep(0, 1, p);
                    Vector3 travel = root.TransformVector(new Vector3(offset.x * root.rect.width,
                        offset.y * root.rect.height + Mathf.Sin(p * Mathf.PI) * root.rect.height * .06f, 0));
                    source.localPosition = sourcePosition + source.parent.InverseTransformVector(travel);
                    source.localScale = sourceScale * (1 - p * .80f); source.localRotation = sourceRotation * Quaternion.Euler(0, 0, -p * 24);
                    if (sourceOpacity != null) sourceOpacity.alpha = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.42f, 1, p));
                }
                if (source != null && reposition)
                {
                    Vector2 offset = (targetPoint - sourcePoint) * Mathf.SmoothStep(0, 1, p);
                    source.localPosition = sourcePosition + source.parent.InverseTransformVector(root.TransformVector(new Vector3(offset.x * root.rect.width,
                        offset.y * root.rect.height + Mathf.Sin(p * Mathf.PI) * root.rect.height * .035f, 0)));
                    if (target != null)
                        target.localPosition = targetPosition + target.parent.InverseTransformVector(root.TransformVector(new Vector3(-offset.x * root.rect.width,
                            -offset.y * root.rect.height - Mathf.Sin(p * Mathf.PI) * root.rect.height * .025f, 0)));
                }
                if (source != null && item.kind == "power") source.localScale = sourceScale * (1 + Mathf.Sin(p * Mathf.PI) * .04f);
                if (number != null)
                {
                    number.rectTransform.anchoredPosition = Vector2.up * (Mathf.SmoothStep(0, 1, p) * 28);
                    var c = number.color; c.a = attack && p < .42f ? 0 : Mathf.Clamp01((1 - p) * 4); number.color = c;
                }
                if (flyingCard != null)
                {
                    Vector2 position = summon ? Vector2.Lerp(summonOrigin, targetPoint, Mathf.SmoothStep(0, 1, Mathf.Clamp01(p / .66f))) :
                        item.kind == "draw" || terrainPlay ? Vector2.Lerp(sourcePoint, targetPoint, Mathf.SmoothStep(0, 1, p)) :
                        Vector2.Lerp(targetPoint, GraveyardCenter(item.targetPlayer >= 0 ? item.targetPlayer : item.actor), Mathf.SmoothStep(0, 1, p));
                    float halfWidth = summon ? .066f : .024f, halfHeight = summon ? .127f : .05f;
                    Anchor(flyingCard, position.x - halfWidth, position.y - halfHeight, position.x + halfWidth, position.y + halfHeight);
                    flyingCard.localRotation = Quaternion.Euler(0, 0, summon ? (1 - Mathf.Clamp01(p / .66f)) * (item.actor == 0 ? -9 : 9) : p * (burn ? 30 : -6));
                }
                if (play && p >= (summon ? .66f : .52f) && !impactPlayed)
                { impactPlayed = true; FmBattleAudio.Play("card-land"); }
                if (attack && p >= .42f && !impactPlayed)
                { impactPlayed = true; FmBattleAudio.PlayImpact(hit ? item.amount : Mathf.Max(1, item.amount), (targetPoint.x - .415f) * 1.5f); }
                yield return null;
            }
            if (!BattlePlaybackStatsCommitted) CommitLiveCardImpact();
            if (source != null) { source.localPosition = sourcePosition; source.localScale = sourceScale; source.localRotation = sourceRotation; }
            if (target != null) { target.localPosition = targetPosition; target.localScale = targetScale; }
            if (sourceOpacity != null) sourceOpacity.alpha = 1;
            foreach (var original in hiddenOriginals) if (original.Key != null) original.Key.alpha = original.Value;
            if (layer != null) Destroy(layer.gameObject);
        }

        private RectTransform BattleFieldCopy(Transform parent, int owner, int instance, string definitionId, Vector2 point)
        {
            if (owner < 0 || owner > 1 || string.IsNullOrEmpty(definitionId)) return null;
            var definition = CardCatalog.Get(definitionId);
            if (owner == 1 && definition.kind == CardKind.Spell && !battleRevealedSpells.Contains(instance))
                return BattleMovingCard(parent, definitionId, point, true);
            var live = battleVisualState.players[owner].units.Find(unit => unit.instanceId == instance);
            var copy = ArcanaCardFace(parent, definition, point.x - .073f, point.y - .119f,
                point.x + .073f, point.y + .119f, ArcanaCardMode.Field, false, live);
            copy.gameObject.name = "Unclipped field card " + instance;
            return copy;
        }

        private Vector2 BattleViewCenter(RectTransform view)
        {
            var point = root.InverseTransformPoint(view.TransformPoint(view.rect.center));
            return new Vector2((point.x - root.rect.xMin) / root.rect.width, (point.y - root.rect.yMin) / root.rect.height);
        }
        private Vector2 BackfieldCardCenter(int owner, int index)
        {
            if (owner == 1)
            {
                int position = Mathf.Clamp(index, 0, BattleDisplayState.rules.terrainLimit - 1);
                int rows = Mathf.Max(2, (BattleDisplayState.rules.terrainLimit + 4) / 5);
                return new Vector2(.49f + .315f * (.305f + position % 5 * .15f),
                    .919f + .076f * (.96f - ((position / 5) * .92f + .44f) / rows));
            }
            float slot = .81f / Mathf.Max(1, BattleDisplayState.rules.terrainLimit);
            return new Vector2(.025f + .78f * (.18f + slot * (Mathf.Clamp(index, 0, BattleDisplayState.rules.terrainLimit - 1) + .5f)), owner == 0 ? .3215f : .957f);
        }
        private Vector2 LaneCenter(int owner, int lane) => new Vector2(lane >= 0 ? .085f + (.67f / engine.State.rules.unitLanes) * (lane + .5f) : .42f, RestageLaneCenterY(owner));
        private Vector2 DeckCenter(int owner)
        {
            bool small = canvas.pixelRect.height < 600 && canvas.pixelRect.width < 900;
            float y = small ? .977f - (15 + 23 * (owner == 1 ? 7 : 8)) / canvas.pixelRect.height : owner == 1 ? .47454f : .40763f;
            return new Vector2(owner == 1 ? .898f : .862f, y);
        }
        private Vector2 GraveyardCenter(int owner)
        { var point = DeckCenter(owner); point.x = .937f; return point; }
        private Vector2 ClashCenter(int owner) => new Vector2(.416f, RestageLaneCenterY(owner));
        private void AddClashCard(Transform parent, int owner, int instanceId, Dictionary<int, RectTransform> copies)
        {
            if (owner < 0 || owner > 1 || instanceId < 0 || copies.ContainsKey(instanceId)) return;
            var unit = battleVisualState.players[owner].units.Find(value => value.instanceId == instanceId);
            // No opponent hand or unrevealed reserved spell is ever materialized here.
            if (unit == null) return;
            var definition = CardCatalog.Get(unit.definitionId); var point = ClashCenter(owner);
            float halfHeight = Mathf.Min(.125f, (RestageLaneTop(owner) - (owner == 1 ? .660f : .365f)) * .49f);
            var card = ArcanaCardFace(parent, definition, point.x - .115f, point.y - halfHeight, point.x + .115f, point.y + halfHeight,
                ArcanaCardMode.Duel, false, unit);
            card.gameObject.name = "Central duel card " + instanceId;
            copies[instanceId] = card.GetComponentInChildren<FmArcanaCardSurface>().GetComponent<RectTransform>();

        }
        private FmBattleFxGraphic CreateBattleFx(Transform parent, FmBattleFxGraphic.EffectStyle style, Vector2 point, Color tint)
        {
            var fx = new GameObject("Battle " + style + " effect", typeof(RectTransform), typeof(FmBattleFxGraphic)).GetComponent<FmBattleFxGraphic>();
            fx.transform.SetParent(parent, false); fx.Style = style; fx.Tint = tint; PositionBattleFx(fx, point); return fx;
        }
        private static void PositionBattleFx(FmBattleFxGraphic effect, Vector2 point)
        { Anchor((RectTransform)effect.transform, point.x - .105f, point.y - .17f, point.x + .105f, point.y + .17f); }
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
            if (item.kind == "burn" || item.kind == "defeat" || item.kind == "discard-field") return new Color(1, .53f, .24f);
            if (item.kind == "attack" || item.kind == "counter") return item.amount == 0 ? new Color(.62f, .87f, 1) : new Color(1, .32f, .20f);
            if (!string.IsNullOrEmpty(item.sourceDefinitionId))
            {
                var card = CardCatalog.Get(item.sourceDefinitionId);
                // An enemy's reserved spell must not reveal its face or attribute before reveal.
                if (item.kind == "play" && item.sourcePlayer == 1 && card.kind == CardKind.Spell) return gold;
                return Color.Lerp(ElementColor(card.element), ivory, .35f);
            }
            return gold;
        }
        private static string StatTransition(string label, int before, int after)
        { int delta = after - before; return label + " " + before + "→" + after + (delta == 0 ? "" : " (" + (delta > 0 ? "+" : "") + delta + ")"); }

        private static string PlaybackCaption(BattleEvent item)
        {
            switch (item.kind)
            {
                case "attack": return "공격 · " + (item.additionalAttackDamage > 0 ? "+" + item.additionalAttackDamage + " 추가피해 · " : "") + item.text;
                case "counter": return "반격 · " + (item.additionalAttackDamage > 0 ? "+" + item.additionalAttackDamage + " 추가피해 · " : "") + item.text;
                case "coin":
                    if (item.hasStatTransition)
                        return "C" + item.coinIndex + (item.coinHeads ? " 앞면 · " : " 뒷면 · ") +
                            StatTransition("AP", item.beforeAttack, item.afterAttack) + "  " +
                            StatTransition("DP", item.beforeDefense, item.afterDefense) + "  " +
                            StatTransition("HP", item.beforeHealth, item.afterHealth);
                    return "코인 판정 · " + item.text;
                case "power": return "능력 확정 · " + item.text;
                case "burn": return "덱 소각 · " + item.text;
                case "defeat": return "전장 이탈 · " + item.text;
                case "discard-field": return "필드 소각 · " + item.text;
                case "reposition": return "전열 재배치 · " + item.text;
                default: return item.text;
            }
        }
    }
}
