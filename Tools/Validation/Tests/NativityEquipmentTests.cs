using System.Linq;
using FantasyCards.Core;
using FantasyCards.Tests;
using NUnit.Framework;

namespace FirstGame.Validation
{
    public sealed class NativityEquipmentTests
    {
        const string Recreator = "fm_4021320946", DarkCross = "fm_11030241241";
        static CardInstance Take(BattleEngine engine, int owner, string id)
        {
            var p = engine.State.players[owner];
            var card = p.deck.Concat(p.hand).FirstOrDefault(c => c.definitionId == id);
            if (card == null) { card = p.deck.Last(); card.definitionId = id; }
            p.deck.Remove(card); p.hand.Remove(card); return card;
        }
        static BattleEngine Fixture(int remainingHealth, out UnitState source, out UnitState target, out CardInstance item)
        {
            var engine = ClassicFixtures.Battle(rules: new RuleSet { baseCoins = 0, defenderStrikesBack = false });
            var original = Take(engine, 0, Recreator);
            source = new UnitState { instanceId = original.instanceId, definitionId = original.definitionId, lane = 0,
                damage = CardCatalog.Get(Recreator).health - remainingHealth };
            engine.State.players[0].units.Add(source);
            target = ClassicFixtures.Unit(engine, 1, "크라켄", 0);
            item = Take(engine, 0, DarkCross); engine.State.players[0].discard.Add(item);
            Assert.That(CardCatalog.Get(item.definitionId).health, Is.EqualTo(-2));
            Assert.That(CardCatalog.UnitHealth(source), Is.EqualTo(remainingHealth));
            BattleEngine.Validate(engine.State); return engine;
        }

        [Test] public void LethalAutomaticEquipmentRestorationCannotDeliverAPosthumousStrike()
        {
            var engine = Fixture(1, out var source, out var target, out var item);
            var resumed = new BattleEngine(FieldJson.Decode<BattleState>(FieldJson.Encode(engine.State)));
            uint rng = engine.State.randomState;
            engine.BeginPlaybackCapture(); ClassicFixtures.Resolve(engine); var frames = engine.EndPlaybackCapture();
            ClassicFixtures.Resolve(resumed);
            var restored = frames.Single(frame => frame.item.text.Contains("리크리에이트 ·"));
            Assert.That(CardCatalog.UnitAttack(restored.players[0].AtLane(0)), Is.GreaterThan(0), "Restored AP does not grant a dead unit permission to hit.");
            Assert.That(CardCatalog.UnitHealth(restored.players[0].AtLane(0)), Is.Zero);
            var death = frames.Single(frame => frame.item.kind == "defeat" && frame.item.sourceInstanceId == source.instanceId);
            Assert.That(death.item.sequence, Is.GreaterThan(restored.item.sequence));
            Assert.That(frames.Any(frame => frame.item.kind == "attack" && frame.item.sourceInstanceId == source.instanceId), Is.False);
            Assert.That(frames.All(frame => frame.players[1].AtLane(0) == null || frame.players[1].AtLane(0).damage == 0), Is.True);
            Assert.That(engine.State.players[0].discard.Count(card => card.instanceId == source.instanceId), Is.EqualTo(1));
            Assert.That(engine.State.players[0].discard.Count(card => card.instanceId == item.instanceId), Is.EqualTo(1));
            Assert.That(engine.State.generatedCards, Is.Empty); Assert.That(ClassicFixtures.AllCards(engine.State.players[0]), Is.EqualTo(50));
            Assert.That(engine.State.randomState, Is.EqualTo(rng));
            Assert.That(FieldJson.Encode(engine.State), Is.EqualTo(FieldJson.Encode(resumed.State)));
            restored.players[0].AtLane(0).equipment.Clear();
            Assert.That(death.players[0].discard.Any(card => card.instanceId == item.instanceId), Is.True, "The destruction snapshot retains its independent original item instance.");
            BattleEngine.Validate(engine.State);
        }

        [Test] public void NonlethalRestorationStillHitsWithActualEquipmentAndRetainsItAfterReset()
        {
            var engine = Fixture(3, out var source, out var target, out var item);
            uint rng = engine.State.randomState;
            engine.BeginPlaybackCapture(); ClassicFixtures.Resolve(engine); var frames = engine.EndPlaybackCapture();
            var attack = frames.Single(frame => frame.item.kind == "attack" && frame.item.sourceInstanceId == source.instanceId);
            Assert.That(attack.item.amount, Is.EqualTo(5));
            Assert.That(CardCatalog.UnitHealth(attack.players[0].AtLane(0)), Is.EqualTo(1));
            Assert.That(CardCatalog.UnitHealth(attack.players[1].AtLane(0)), Is.EqualTo(3));
            Assert.That(engine.State.players[0].AtLane(0).equipment.Single().instanceId, Is.EqualTo(item.instanceId));
            Assert.That(CardCatalog.UnitHealth(engine.State.players[0].AtLane(0)), Is.EqualTo(1));
            Assert.That(engine.State.randomState, Is.EqualTo(rng)); BattleEngine.Validate(engine.State);
        }

        [Test] public void ZeroAttackDoesNotRestoreDiscardEquipmentOrPayItsHealthPenalty()
        {
            var engine = Fixture(1, out var source, out var target, out var item); source.bonusPower = -CardCatalog.Get(Recreator).attack;
            engine.BeginPlaybackCapture(); ClassicFixtures.Resolve(engine); var frames = engine.EndPlaybackCapture();
            Assert.That(frames.Any(frame => frame.item.text.Contains("리크리에이트 ·")), Is.False);
            var attack = frames.Single(frame => frame.item.kind == "attack" && frame.item.sourceInstanceId == source.instanceId);
            Assert.That(attack.item.amount, Is.Zero); Assert.That(CardCatalog.UnitHealth(attack.players[0].AtLane(0)), Is.EqualTo(1));
            Assert.That(engine.State.players[0].AtLane(0).equipment, Is.Empty);
            Assert.That(engine.State.players[0].discard.Count(card => card.instanceId == item.instanceId), Is.EqualTo(1));
            BattleEngine.Validate(engine.State);
        }
    }
}
