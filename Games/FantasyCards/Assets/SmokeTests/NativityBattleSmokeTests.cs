using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FantasyCards.Core;
using FantasyCards.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace FantasyCards.SmokeTests
{
    public sealed partial class NativitySmokeTests
    {
        static CardInstance TakeOriginal(BattleEngine engine, int owner, string id)
        {
            var p = engine.State.players[owner];
            var instance = p.deck.Concat(p.hand).Single(c => c.definitionId == id);
            p.deck.Remove(instance); p.hand.Remove(instance); return instance;
        }

        static BattleEngine LiveValuesFixture(out UnitState bearer, out UnitState defender, out CardInstance equipment)
        {
            const string kraken = "fm_2051520026", onyx = "fm_2042940005";
            var ids = new[] { kraken, onyx }.Concat(CardCatalog.All.Where(c => c.id != kraken && c.id != onyx)
                .Select(c => c.id).Take(48)).ToArray();
            var engine = new BattleEngine(713, playerDeck: ids, aiDeck: ids);
            Assert.That(engine.ConfirmOpeningHand(0, out _), Is.True);
            var own = TakeOriginal(engine, 0, kraken); var enemy = TakeOriginal(engine, 1, kraken);
            bearer = new UnitState { instanceId = own.instanceId, definitionId = own.definitionId, lane = 0 };
            defender = new UnitState { instanceId = enemy.instanceId, definitionId = enemy.definitionId, lane = 0 };
            engine.State.players[0].units.Add(bearer); engine.State.players[1].units.Add(defender);
            equipment = TakeOriginal(engine, 0, onyx);
            var player = engine.State.players[0];
            if (player.hand.Count == engine.State.rules.handLimit) { player.deck.Add(player.hand[0]); player.hand.RemoveAt(0); }
            player.hand.Add(equipment);
            foreach (Element element in Enum.GetValues(typeof(Element))) player.SetResource(element, 30);
            player.genericSoul = 30;
            // Native-only authored deterministic input. The actual Core RNG
            // turns this first roll into heads; presentation never rerolls it.
            engine.State.randomState = 2;
            BattleEngine.Validate(engine.State); return engine;
        }

        static void AssertActualCardNumerals(int instance, int attack, int defense, int health)
        {
            var faces = UnityEngine.Object.FindObjectsByType<FmLiveCardValues>(FindObjectsSortMode.None)
                .Where(view => view.isActiveAndEnabled && view.IsCardFace && view.InstanceId == instance).ToArray();
            Assert.That(faces.Length, Is.GreaterThanOrEqualTo(1), "The actual card face must exist, independently of coin-window labels.");
            string[] stat = { "AP", "DP", "HP" }; int[] expected = { attack, defense, health };
            foreach (var face in faces)
                for (int i = 0; i < 3; i++)
                {
                    var text = face.GetComponentsInChildren<TMP_Text>().Single(label => label.name == "Card live " + stat[i] + " " + instance);
                    Assert.That(text.text, Is.EqualTo(expected[i].ToString()), "Actual printed-card " + stat[i] + " must equal the resolved Core frame.");
                    text.ForceMeshUpdate();
                    Assert.That(text.textInfo.characterInfo.Take(text.textInfo.characterCount).Any(character => character.isVisible), Is.True,
                        "A view-model value alone does not prove visible on-card numeral geometry: " + text.name);
                    Assert.That(text.rectTransform.anchorMin.x, Is.GreaterThan(.85f));
                    Assert.That(text.rectTransform.anchorMax.y, Is.LessThan(.24f));
                    Assert.That(face.GetComponentInParent<FmArcanaCardSurface>().UsesFullPrint, Is.True);
                }
        }

        void AssertCommittedFrame(BattlePlaybackFrame expected)
        {
            Assert.That(App.BattlePlaybackStatsCommitted, Is.True);
            Assert.That(App.LastBattlePlaybackEvents.Last().sequence, Is.EqualTo(expected.item.sequence));
            foreach (var owner in expected.players)
                foreach (var unit in owner.units)
                    AssertActualCardNumerals(unit.instanceId, CardCatalog.UnitAttack(unit), CardCatalog.UnitDefense(unit), CardCatalog.UnitHealth(unit));
            AssertFactionHeaders();
        }

        IEnumerator WaitForCommittedCardFrame(int sequence, string stage, float seconds = 12)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            var observed = new List<string>();
            bool Ready() => App.IsBattlePlaybackActive && App.BattlePlaybackStatsCommitted &&
                App.LastBattlePlaybackEvents.Count > 0 && App.LastBattlePlaybackEvents.Last().sequence == sequence;
            while (!Ready() && Time.realtimeSinceStartup < deadline)
            {
                string state = App.BattlePlaybackEventIndex + "/" + App.BattlePlaybackEventCount + " " +
                    App.BattlePlaybackEventKind + " active=" + App.IsBattlePlaybackActive + " committed=" + App.BattlePlaybackStatsCommitted;
                if (observed.Count == 0 || observed.Last() != state) observed.Add(state);
                yield return null;
            }
            Assert.That(Ready(), Is.True, stage + " Expected sequence " + sequence + ". Observed: " + string.Join(" → ", observed));
        }

        [UnityTest] public IEnumerator RealEquipmentHeadsCoinAndDamageChangePhysicalCardNumeralsAtTheirActualImpacts()
        {
            var engine = LiveValuesFixture(out var bearer, out var defender, out var item); Install(engine);
            AssertActualCardNumerals(bearer.instanceId, 5, 0, 8);
            uint random = engine.State.randomState;
            // Native scene/UI construction can take longer than a .48s equip
            // event. Hold the presentation before starting it so the observer
            // cannot miss the entire committed interval in that first frame.
            Set("battlePlaybackSpeed", 0f);
            Set("selectedCard", item.instanceId); Call("Play", 0);
            Assert.That(engine.State.players[0].AtLane(0).equipment.Single().instanceId, Is.EqualTo(item.instanceId), "The actual paid equipment action must succeed before observing its presentation.");
            Assert.That(CardCatalog.UnitAttack(engine.State.players[0].AtLane(0)), Is.EqualTo(6));
            Assert.That(CardCatalog.UnitDefense(engine.State.players[0].AtLane(0)), Is.EqualTo(1));
            Assert.That(CardCatalog.UnitHealth(engine.State.players[0].AtLane(0)), Is.EqualTo(10));
            int equipSequence = engine.State.events.Last().sequence;
            yield return SmokeUi.WaitUntil(() => App.IsBattlePlaybackActive && App.BattlePlaybackEventKind == "play",
                8, "The real equipment action did not enter playback.");
            Set("battlePlaybackSpeed", .25f);
            yield return WaitForCommittedCardFrame(equipSequence, "The real equipment action never committed its bearer's numeric views.");
            Set("battlePlaybackSpeed", 0f);
            Assert.That(engine.State.players[0].AtLane(0).equipment.Single().instanceId, Is.EqualTo(item.instanceId));
            AssertActualCardNumerals(bearer.instanceId, 6, 1, 10);
            Assert.That(engine.State.randomState, Is.EqualTo(random));
            yield return Shot("real-equipment-on-card", inspect: () => { AssertActualCardNumerals(bearer.instanceId, 6, 1, 10); AssertFactionHeaders(); });
            yield return Shot("real-equipment-on-card-touch", 616, 346, () => { AssertActualCardNumerals(bearer.instanceId, 6, 1, 10); AssertFactionHeaders(); });
            Set("battlePlaybackSpeed", 4f); yield return WaitIdle();

            // Resolve an independent Core continuation to obtain the actual
            // event frames and final state before presentation touches them.
            var expected = new BattleEngine(engine.State.Copy());
            Assert.That(expected.CompletePreparation(0, out _), Is.True);
            expected.BeginPlaybackCapture(); Assert.That(expected.CompletePreparation(1, out _), Is.True);
            var frames = expected.EndPlaybackCapture();
            var coin = frames.First(frame => frame.item.kind == "coin" && frame.item.sourceInstanceId == bearer.instanceId);
            Assert.That(coin.item.coinHeads, Is.True); Assert.That(coin.item.afterAttack, Is.EqualTo(7));
            var hit = frames.Single(frame => frame.item.kind == "attack" && frame.item.sourceInstanceId == bearer.instanceId);
            Assert.That(hit.item.targetInstanceId, Is.EqualTo(defender.instanceId)); Assert.That(hit.item.amount, Is.GreaterThan(0));
            Assert.That(engine.CompletePreparation(0, out _), Is.True);
            Set("battlePlaybackSpeed", 0f);
            Call("Act", new Func<bool>(() => engine.CompletePreparation(1, out _)));
            Assert.That(JsonUtility.ToJson(engine.State), Is.EqualTo(JsonUtility.ToJson(expected.State)), "The independent Core continuation must agree before replay begins.");
            yield return SmokeUi.WaitUntil(() => App.IsBattlePlaybackActive, 8, "The real battle action did not enter playback.");
            Set("battlePlaybackSpeed", .25f);
            yield return WaitForCommittedCardFrame(coin.item.sequence, "The first real heads coin was not committed on its card.");
            Set("battlePlaybackSpeed", 0f);
            AssertCommittedFrame(coin); AssertActualCardNumerals(bearer.instanceId, 7, 1, 10);
            string resolved = JsonUtility.ToJson(engine.State);
            Assert.That(resolved, Is.EqualTo(JsonUtility.ToJson(expected.State)), "Replay may neither reroll nor modify the already resolved battle.");
            yield return Shot("first-heads-actual-card", inspect: () => AssertCommittedFrame(coin));
            yield return Shot("first-heads-actual-card-touch", 616, 346, () => AssertCommittedFrame(coin));
            Set("battlePlaybackSpeed", 1f);
            yield return WaitForCommittedCardFrame(hit.item.sequence, "Actual target HP did not update at damage impact.", 20);
            Set("battlePlaybackSpeed", 0f); AssertCommittedFrame(hit);
            yield return Shot("damage-on-actual-card", inspect: () => AssertCommittedFrame(hit));
            yield return Shot("damage-on-actual-card-touch", 616, 346, () => AssertCommittedFrame(hit));
            Assert.That(JsonUtility.ToJson(engine.State), Is.EqualTo(resolved));
            // Stop only this diagnostic's presentation hold. Installing the
            // fully resolved Core state does not run AI or consume a new roll.
            Install(engine); Set("battlePlaybackSpeed", 1f);
            AssertActualCardNumerals(bearer.instanceId, 6, 1, 10);
            Assert.That(JsonUtility.ToJson(engine.State), Is.EqualTo(resolved));
            Assert.That(backend.Read("preserved-player"), Is.EqualTo("original player bytes"));
            BattleEngine.Validate(engine.State); LogAssert.NoUnexpectedReceived();
        }
    }
}
