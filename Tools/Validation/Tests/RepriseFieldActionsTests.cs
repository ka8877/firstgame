using System;
using System.Linq;
using FantasyCards.Core;
using FantasyCards.Tests;
using NUnit.Framework;

namespace FirstGame.Validation
{
    public sealed class RepriseFieldActionsTests
    {
        static BattleEngine Battle() => ClassicFixtures.Battle(rules: new RuleSet { baseCoins = 0, defenderStrikesBack = false });
        static CardInstance Extract(BattleEngine e, int owner, string id)
        {
            var p = e.State.players[owner];
            var card = p.deck.Concat(p.hand).FirstOrDefault(c => c.definitionId == id);
            if (card == null) { card = p.deck.Last(); card.definitionId = id; }
            p.deck.Remove(card); p.hand.Remove(card); return card;
        }
        static PreparedSpellState Spell(BattleEngine e, int owner, string id, int lane, int target = -1, int targetOwner = -1)
        {
            var card = Extract(e, owner, id);
            var spell = new PreparedSpellState { instanceId = card.instanceId, definitionId = id, lane = lane, targetLane = target, targetPlayer = targetOwner };
            e.State.players[owner].preparedSpells.Add(spell); return spell;
        }
        static CardInstance Equip(BattleEngine e, int owner, UnitState unit, string name)
        {
            var card = ClassicFixtures.Extract(e, owner, name); unit.equipment.Add(card); return card;
        }

        [Test] public void RepositionChangesActualAttackOrderAndDoesNotSpendResourcesOrRollCoins()
        {
            var e = Battle(); var first = ClassicFixtures.Unit(e, 0, "임프", 0);
            var last = ClassicFixtures.Unit(e, 0, "데몬", 4);
            var blocker = ClassicFixtures.Unit(e, 1, "크라켄", 0); blocker.bonusHealth = 50;
            uint random = e.State.randomState;
            int lp = e.State.players[0].levelPoints, soul = e.State.players[0].genericSoul;
            Assert.That(e.MoveFieldCard(0, last.instanceId, 0, out var reason), Is.True, reason);
            Assert.That(first.lane, Is.EqualTo(4)); Assert.That(last.lane, Is.Zero);
            Assert.That(e.State.randomState, Is.EqualTo(random));
            Assert.That(e.State.players[0].levelPoints, Is.EqualTo(lp));
            Assert.That(e.State.players[0].genericSoul, Is.EqualTo(soul));
            Assert.That(e.State.players[0].playedThisPreparation, Is.False);
            e.BeginPlaybackCapture(); ClassicFixtures.Resolve(e); var frames = e.EndPlaybackCapture();
            Assert.That(frames.First(f => f.item.kind == "attack").item.sourceInstanceId, Is.EqualTo(last.instanceId));
            Assert.That(frames.Where(f => f.item.kind == "attack").Select(f => f.item.sourceInstanceId),
                Is.EqualTo(new[] { last.instanceId, first.instanceId }));
            BattleEngine.Validate(e.State);
        }

        [Test] public void ReservedSpellSwapsWithUnitWithoutRevealingItsNameOrChangingItsTargetPolicy()
        {
            var e = Battle(); var unit = ClassicFixtures.Unit(e, 0, "임프", 3);
            // Current admitted spells use printed field ranges, not a selected
            // single-unit target. Use the real Fireball policy in this fixture.
            string id = ClassicFixtures.Id("파이어볼");
            var spell = Spell(e, 0, id, 0);
            Assert.That(e.MoveFieldCard(0, spell.instanceId, 3, out var reason), Is.True, reason);
            Assert.That(spell.lane, Is.EqualTo(3)); Assert.That(unit.lane, Is.Zero);
            Assert.That(spell.targetLane, Is.EqualTo(-1)); Assert.That(spell.targetPlayer, Is.EqualTo(-1));
            Assert.That(CardCatalog.Get(id).operations.Single().laneMask, Is.EqualTo(7));
            var item = e.State.events.Last();
            Assert.That(item.kind, Is.EqualTo("reposition"));
            Assert.That(item.text, Does.Not.Contain(CardCatalog.Get(id).name));
            Assert.That(new[] { item.sourceInstanceId, item.sourceLane, item.targetLane, item.targetInstanceId },
                Is.EqualTo(new[] { spell.instanceId, 0, 3, unit.instanceId }));
            var restored = new BattleEngine(FieldJson.Decode<BattleState>(FieldJson.Encode(e.State)));
            Assert.That(FieldJson.Encode(restored.State), Is.EqualTo(FieldJson.Encode(e.State)));
        }

        [Test] public void DefenderRepositionChangesTheRealFirstBlockerAfterAttackerReady()
        {
            var e = Battle(); var attacker = ClassicFixtures.Unit(e, 0, "데몬", 1);
            var originalFirst = ClassicFixtures.Unit(e, 1, "임프", 0);
            var guard = ClassicFixtures.Unit(e, 1, "크라켄", 4);
            Assert.That(e.CompletePreparation(0, out _), Is.True);
            Assert.That(e.MoveFieldCard(1, guard.instanceId, 0, out var reason), Is.True, reason);
            Assert.That(originalFirst.lane, Is.EqualTo(4));
            e.BeginPlaybackCapture(); Assert.That(e.CompletePreparation(1, out reason), Is.True, reason);
            var strike = e.EndPlaybackCapture().Single(f => f.item.kind == "attack");
            Assert.That(strike.item.sourceInstanceId, Is.EqualTo(attacker.instanceId));
            Assert.That(strike.item.targetInstanceId, Is.EqualTo(guard.instanceId));
            Assert.That(strike.item.targetLane, Is.Zero);
            BattleEngine.Validate(e.State);
        }

        [Test] public void RepositionCaptureContainsExactPostMoveStateAndIsADeepCopy()
        {
            var e = Battle(); var unit = ClassicFixtures.Unit(e, 0, "임프", 0);
            e.BeginPlaybackCapture(); Assert.That(e.MoveFieldCard(0, unit.instanceId, 4, out _), Is.True);
            var frame = e.EndPlaybackCapture().Single();
            Assert.That(frame.item.sourceLane, Is.Zero); Assert.That(frame.item.targetLane, Is.EqualTo(4));
            Assert.That(frame.players[0].AtLane(0), Is.Null);
            Assert.That(frame.players[0].AtLane(4).instanceId, Is.EqualTo(unit.instanceId));
            frame.players[0].AtLane(4).lane = 2;
            Assert.That(unit.lane, Is.EqualTo(4)); BattleEngine.Validate(e.State);
        }

        [Test] public void FieldActionsRejectForeignCardsInvalidSlotsOpeningAndReadyWithoutMutation()
        {
            var e = Battle(); var own = ClassicFixtures.Unit(e, 0, "임프", 0);
            var enemy = ClassicFixtures.Unit(e, 1, "크라켄", 0);
            string before = FieldJson.Encode(e.State);
            Assert.That(e.MoveFieldCard(0, own.instanceId, -1, out _), Is.False);
            Assert.That(e.MoveFieldCard(0, own.instanceId, 5, out _), Is.False);
            Assert.That(e.MoveFieldCard(0, own.instanceId, 0, out _), Is.False);
            Assert.That(e.MoveFieldCard(0, enemy.instanceId, 4, out _), Is.False);
            Assert.That(e.DiscardField(0, enemy.instanceId, out _), Is.False);
            Assert.That(e.DiscardField(-1, own.instanceId, out _), Is.False);
            Assert.That(FieldJson.Encode(e.State), Is.EqualTo(before));
            Assert.That(e.CompletePreparation(0, out _), Is.True);
            before = FieldJson.Encode(e.State);
            Assert.That(e.MoveFieldCard(0, own.instanceId, 3, out _), Is.False);
            Assert.That(e.DiscardField(0, own.instanceId, out _), Is.False);
            Assert.That(FieldJson.Encode(e.State), Is.EqualTo(before));
            Assert.That(e.MoveFieldCard(1, enemy.instanceId, 3, out _), Is.True, "Defender preparation permits its own order changes.");
            var opening = new BattleEngine(42); before = FieldJson.Encode(opening.State);
            Assert.That(opening.MoveFieldCard(0, 1, 3, out _), Is.False);
            Assert.That(opening.DiscardField(0, 1, out _), Is.False);
            Assert.That(FieldJson.Encode(opening.State), Is.EqualTo(before));
        }

        [Test] public void DiscardUnitPreservesOriginalUnitAndEquipmentInstancesAndDoesNotRefundSouls()
        {
            var e = Battle(); var unit = ClassicFixtures.Unit(e, 0, "크라켄", 2);
            var item = Equip(e, 0, unit, "골렘의심장");
            ClassicFixtures.Souls(e, 0, 7);
            var p = e.State.players[0]; int lp = p.levelPoints; uint random = e.State.randomState;
            Assert.That(e.DiscardField(0, unit.instanceId, out var reason), Is.True, reason);
            Assert.That(p.units, Is.Empty);
            Assert.That(p.discard.Select(c => c.instanceId), Does.Contain(unit.instanceId).And.Contain(item.instanceId));
            Assert.That(p.discard.Count(c => c.instanceId == item.instanceId), Is.EqualTo(1));
            Assert.That(p.genericSoul, Is.EqualTo(7)); Assert.That(p.levelPoints, Is.EqualTo(lp));
            Assert.That(p.Resource(Element.Tide), Is.EqualTo(7)); Assert.That(e.State.randomState, Is.EqualTo(random));
            Assert.That(ClassicFixtures.AllCards(p), Is.EqualTo(50));
            Assert.That(e.State.events.Last().amount, Is.EqualTo(2));
            string before = FieldJson.Encode(e.State);
            Assert.That(e.DiscardField(0, unit.instanceId, out _), Is.False);
            Assert.That(FieldJson.Encode(e.State), Is.EqualTo(before));
            var restored = new BattleEngine(FieldJson.Decode<BattleState>(before));
            Assert.That(FieldJson.Encode(restored.State), Is.EqualTo(before));
        }

        [Test] public void DiscardReservedMagicDoesNotResolveItsEffectAndFreesItsActualSlot()
        {
            var e = Battle(); var enemy = ClassicFixtures.Unit(e, 1, "크라켄", 1);
            var spell = Spell(e, 0, ClassicFixtures.Id("파이어볼"), 3);
            int hp = CardCatalog.UnitHealth(enemy); uint random = e.State.randomState;
            Assert.That(e.DiscardField(0, spell.instanceId, out var reason), Is.True, reason);
            Assert.That(e.State.players[0].SpellAtLane(3), Is.Null);
            Assert.That(e.State.players[0].discard.Single().instanceId, Is.EqualTo(spell.instanceId));
            Assert.That(CardCatalog.UnitHealth(enemy), Is.EqualTo(hp)); Assert.That(e.State.randomState, Is.EqualTo(random));
            Assert.That(e.State.events.Any(x => x.kind == "spell" && x.sourceInstanceId == spell.instanceId), Is.False);
            Assert.That(e.State.events.Last().text, Does.Not.Contain("파이어볼"));
            BattleEngine.Validate(e.State);
        }

        [Test] public void RemovingHealthEquipmentRunsTheBearersRealDestructionChain()
        {
            var e = Battle(); var unit = ClassicFixtures.Unit(e, 0, "크라켄", 0);
            var item = Equip(e, 0, unit, "골렘의심장");
            unit.damage = CardCatalog.BaseHealth(unit) - 1;
            Assert.That(CardCatalog.UnitHealth(unit), Is.EqualTo(1));
            Assert.That(e.DiscardField(0, item.instanceId, out var reason), Is.True, reason);
            Assert.That(e.State.players[0].units, Is.Empty);
            Assert.That(e.State.players[0].discard.Select(c => c.instanceId), Is.EqualTo(new[] { item.instanceId, unit.instanceId }));
            Assert.That(e.State.events.TakeLast(2).Select(x => x.kind), Is.EqualTo(new[] { "discard-field", "defeat" }));
            BattleEngine.Validate(e.State);
        }

        [Test] public void VoluntaryFieldDestructionRetainsThePrintedLandRuinAbility()
        {
            var e = Battle(); var unit = ClassicFixtures.Unit(e, 0, "랜드트랩퍼", 0);
            e.State.players[1].levelPoints = 3;
            Assert.That(e.DiscardField(0, unit.instanceId, out var reason), Is.True, reason);
            Assert.That(e.State.players[1].levelPoints, Is.EqualTo(2));
            Assert.That(e.State.events.Last().text, Does.Contain("랜드루인"));
            Assert.That(e.State.events.Last().sourceInstanceId, Is.EqualTo(unit.instanceId));
            BattleEngine.Validate(e.State);
        }

        [Test] public void TerrainDiscardIsNotGuessedAndPreparationControlsKeepHandShuffleAvailable()
        {
            var e = Battle(); var card = ClassicFixtures.Extract(e, 0, "묘지");
            e.State.players[0].terrain.Add(card);
            string before = FieldJson.Encode(e.State);
            Assert.That(e.DiscardField(0, card.instanceId, out var reason), Is.False);
            Assert.That(reason, Does.Contain("지형")); Assert.That(FieldJson.Encode(e.State), Is.EqualTo(before));
            var unit = ClassicFixtures.Unit(e, 0, "임프", 0);
            Assert.That(e.MoveFieldCard(0, unit.instanceId, 1, out _), Is.True);
            Assert.That(e.CanShuffleHand(0, out _), Is.True);
            Assert.That(e.DiscardField(0, unit.instanceId, out _), Is.True);
            Assert.That(e.CanShuffleHand(0, out _), Is.True);
            BattleEngine.Validate(e.State);
        }
    }
}
