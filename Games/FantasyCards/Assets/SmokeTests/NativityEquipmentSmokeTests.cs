using System;
using System.Collections;
using System.Linq;
using FantasyCards.Core;
using FantasyCards.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FantasyCards.SmokeTests
{
    public sealed partial class NativitySmokeTests
    {
        void OpenEquipmentThroughActualFieldHelp(int owner, int instanceId)
        {
            var lane = GameObject.Find("Lane " + owner + " 0");
            Assert.That(lane, Is.Not.Null);
            var help = lane.GetComponent<UiCardHover>();
            Assert.That(help.LongPress, Is.Not.Null); help.LongPress();
            Assert.That(App.QuestCurrentScreen, Is.EqualTo("unit-equipment"));
            Assert.That(GameObject.Find("Equipped bearer " + instanceId), Is.Not.Null);
        }

        IEnumerator FinishActualEquipmentAction()
        {
            yield return SmokeUi.WaitUntil(() => App.IsBattlePlaybackActive, 8, "The actual equipment action did not enter its held presentation.");
            Set("battlePlaybackSpeed", 4f); yield return WaitIdle(); Set("battlePlaybackSpeed", 1f);
        }

        [UnityTest] public IEnumerator ActualAttachedItemsCanBeInspectedAndDisposedWithOwnershipPreparationAndHealthRules()
        {
            var engine = LiveValuesFixture(out var bearer, out var defender, out var item);
            // A public, already-equipped enemy original is part of this native
            // fixture. Its hand stays private; no anonymous or fake gear is used.
            var enemyItem = TakeOriginal(engine, 1, item.definitionId);
            defender.equipment.Add(enemyItem); BattleEngine.Validate(engine.State);
            Install(engine); Set("battlePlaybackSpeed", 0f); Set("selectedCard", item.instanceId); Call("Play", 0);
            Assert.That(engine.State.players[0].AtLane(0).equipment.Single().instanceId, Is.EqualTo(item.instanceId));
            yield return FinishActualEquipmentAction();
            var equipped = engine.State.Copy(); string beforeInspection = JsonUtility.ToJson(engine.State);
            OpenEquipmentThroughActualFieldHelp(0, bearer.instanceId);
            void Inspect()
            {
                AssertActualCardNumerals(bearer.instanceId, 6, 1, 10);
                var original = GameObject.Find("Attached original item " + item.instanceId).GetComponentInChildren<FmArcanaCardSurface>();
                Assert.That(original.DefinitionId, Is.EqualTo(item.definitionId)); Assert.That(original.UsesFullPrint, Is.True);
                Assert.That(NamedButton("Discard attached item " + item.instanceId).interactable, Is.True);
            }
            yield return Shot("attached-original-equipment", inspect: Inspect);
            yield return Shot("attached-original-equipment-touch", 616, 346, Inspect);
            Assert.That(JsonUtility.ToJson(engine.State), Is.EqualTo(beforeInspection));
            NamedButton("Equipment inspection back").onClick.Invoke();
            OpenEquipmentThroughActualFieldHelp(1, defender.instanceId);
            var foreign = NamedButton("Discard attached item " + enemyItem.instanceId);
            Assert.That(foreign.interactable, Is.False); foreign.onClick.Invoke();
            Assert.That(JsonUtility.ToJson(engine.State), Is.EqualTo(beforeInspection), "Even a stale readonly callback must respect Core ownership.");

            var ready = new BattleEngine(equipped); Assert.That(ready.CompletePreparation(0, out _), Is.True);
            Install(ready); OpenEquipmentThroughActualFieldHelp(0, bearer.instanceId);
            var locked = NamedButton("Discard attached item " + item.instanceId); Assert.That(locked.interactable, Is.False);
            string readyState = JsonUtility.ToJson(ready.State); locked.onClick.Invoke();
            Assert.That(JsonUtility.ToJson(ready.State), Is.EqualTo(readyState), "Ready cannot be bypassed through the equipment modal.");

            engine = new BattleEngine(equipped); Install(engine); OpenEquipmentThroughActualFieldHelp(0, bearer.instanceId);
            var expected = new BattleEngine(equipped); Assert.That(expected.DiscardField(0, item.instanceId, out _), Is.True);
            Set("battlePlaybackSpeed", 0f); NamedButton("Discard attached item " + item.instanceId).onClick.Invoke();
            yield return FinishActualEquipmentAction();
            Assert.That(JsonUtility.ToJson(engine.State), Is.EqualTo(JsonUtility.ToJson(expected.State)));
            AssertActualCardNumerals(bearer.instanceId, 5, 0, 8);
            Assert.That(engine.State.players[0].discard.Count(card => card.instanceId == item.instanceId), Is.EqualTo(1));
            Assert.That(engine.State.players[1].AtLane(0).equipment.Single().instanceId, Is.EqualTo(enemyItem.instanceId));

            // Removing the same genuine HP item from a wounded bearer must run
            // the real destruction path rather than merely hiding its picture.
            var wounded = new BattleEngine(equipped); wounded.State.players[0].AtLane(0).damage = 9;
            BattleEngine.Validate(wounded.State); Install(wounded); OpenEquipmentThroughActualFieldHelp(0, bearer.instanceId);
            Set("battlePlaybackSpeed", 0f); NamedButton("Discard attached item " + item.instanceId).onClick.Invoke();
            yield return FinishActualEquipmentAction();
            Assert.That(wounded.State.players[0].AtLane(0), Is.Null);
            Assert.That(wounded.State.players[0].discard.Count(card => card.instanceId == bearer.instanceId), Is.EqualTo(1));
            Assert.That(wounded.State.players[0].discard.Count(card => card.instanceId == item.instanceId), Is.EqualTo(1));
            Assert.That(wounded.State.randomState, Is.EqualTo(equipped.randomState));
            BattleEngine.Validate(wounded.State); LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator AutomaticLethalEquipmentRestorationShowsZeroHealthAndNeverHitsTheOpponent()
        {
            string[] required = { "fm_4021320946", "fm_11030241241", "fm_2051520026" };
            var deck = required.Concat(CardCatalog.All.Where(card => !required.Contains(card.id)).Select(card => card.id).Take(47)).ToArray();
            var engine = new BattleEngine(419, new RuleSet { baseCoins = 0 }, deck, deck);
            Assert.That(engine.ConfirmOpeningHand(0, out _), Is.True);
            var unitCard = TakeOriginal(engine, 0, required[0]); var item = TakeOriginal(engine, 0, required[1]);
            var enemyCard = TakeOriginal(engine, 1, required[2]);
            var source = new UnitState { instanceId = unitCard.instanceId, definitionId = unitCard.definitionId, lane = 0, damage = 2 };
            var target = new UnitState { instanceId = enemyCard.instanceId, definitionId = enemyCard.definitionId, lane = 0 };
            engine.State.players[0].units.Add(source); engine.State.players[0].discard.Add(item); engine.State.players[1].units.Add(target);
            Assert.That(engine.CompletePreparation(0, out _), Is.True); BattleEngine.Validate(engine.State);
            var expected = new BattleEngine(engine.State.Copy()); expected.BeginPlaybackCapture();
            Assert.That(expected.CompletePreparation(1, out _), Is.True); var frames = expected.EndPlaybackCapture();
            var restore = frames.Single(frame => frame.item.text.Contains("리크리에이트 ·"));
            Assert.That(frames.Any(frame => frame.item.kind == "attack" && frame.item.sourceInstanceId == source.instanceId), Is.False);
            Assert.That(frames.Any(frame => frame.item.kind == "defeat" && frame.item.sourceInstanceId == source.instanceId), Is.True);
            Install(engine); Set("battlePlaybackSpeed", 0f); Call("Act", new Func<bool>(() => engine.CompletePreparation(1, out _)));
            yield return SmokeUi.WaitUntil(() => App.IsBattlePlaybackActive, 8, "The real destruction interaction never entered playback.");
            Set("battlePlaybackSpeed", .25f);
            yield return WaitForCommittedCardFrame(restore.item.sequence, "Automatic equipment restoration did not display its actual HP-zero frame.");
            Set("battlePlaybackSpeed", 0f);
            yield return Shot("automatic-equipment-hp-zero", inspect: () => AssertCommittedFrame(restore));
            yield return Shot("automatic-equipment-hp-zero-touch", 616, 346, () => AssertCommittedFrame(restore));
            Assert.That(CardCatalog.UnitHealth(restore.players[0].AtLane(0)), Is.Zero);
            Assert.That(restore.players[1].AtLane(0).damage, Is.Zero);
            Assert.That(JsonUtility.ToJson(engine.State), Is.EqualTo(JsonUtility.ToJson(expected.State)));
            Install(engine); Set("battlePlaybackSpeed", 1f);
            Assert.That(engine.State.players[0].AtLane(0), Is.Null);
            Assert.That(engine.State.players[0].discard.Count(card => card.instanceId == item.instanceId), Is.EqualTo(1));
            Assert.That(engine.State.players[0].discard.Count(card => card.instanceId == source.instanceId), Is.EqualTo(1));
            Assert.That(engine.State.randomState, Is.EqualTo(expected.State.randomState));
            BattleEngine.Validate(engine.State); LogAssert.NoUnexpectedReceived();
        }
    }
}
