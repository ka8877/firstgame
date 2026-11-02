using System.Linq;
using FantasyCards.Core;
using FantasyCards.Tests;
using NUnit.Framework;

namespace FirstGame.Validation
{
    public sealed class RestageValueFramesTests
    {
        [Test] public void OriginalSetOperatorGearIsPreservedAsUnsupportedInsteadOfBeingFlattenedToAdditiveGear()
        {
            // The actual 2015 guide shows 칠성보검 '=4 / 0 / +3'. Its
            // preserved DB row has ATKStatType33(EQUAL), not31(PLUS).
            const string sword = "fm_11080341289";
            var original = OriginalCardArchive.Get(sword);
            Assert.That(original.name, Is.EqualTo("칠성보검"));
            Assert.That(original.attack, Is.EqualTo(4)); Assert.That(original.health, Is.EqualTo(3));
            Assert.That(original.playable, Is.False); Assert.That(CardCatalog.Contains(sword), Is.False);
            Assert.That(original.unsupportedReason, Is.Not.Empty); Assert.That(original.artResource, Is.Not.Empty);
        }

        [Test] public void EquipmentBearerSnapshotsChangeEvenThoughTheActingCardIsAnItem()
        {
            var battle = ClassicFixtures.Battle(); ClassicFixtures.Souls(battle, 0);
            var unit = ClassicFixtures.Unit(battle, 0, "크라켄", 2);
            var item = ClassicFixtures.Hand(battle, 0, "골렘의심장");
            int printedHp = CardCatalog.Get(unit.definitionId).health;
            uint random = battle.State.randomState;
            battle.BeginPlaybackCapture(); Assert.That(battle.Play(0, item.instanceId, 2, out var reason), Is.True, reason);
            var equipped = battle.EndPlaybackCapture().Single();
            Assert.That(equipped.item.sourceInstanceId, Is.EqualTo(item.instanceId));
            Assert.That(equipped.item.targetInstanceId, Is.EqualTo(unit.instanceId));
            Assert.That(equipped.item.hasStatTransition, Is.False, "Bearer values must come from the frame, not actor-only coin metadata.");
            Assert.That(CardCatalog.UnitHealth(equipped.players[0].AtLane(2)), Is.EqualTo(printedHp + 3));
            battle.BeginPlaybackCapture(); Assert.That(battle.DiscardField(0, item.instanceId, out reason), Is.True, reason);
            var removed = battle.EndPlaybackCapture().Single();
            Assert.That(CardCatalog.UnitHealth(removed.players[0].AtLane(2)), Is.EqualTo(printedHp));
            Assert.That(CardCatalog.UnitHealth(equipped.players[0].AtLane(2)), Is.EqualTo(printedHp + 3), "Later removal cannot mutate the earlier visible frame.");
            Assert.That(battle.State.randomState, Is.EqualTo(random)); BattleEngine.Validate(battle.State);
        }

        [Test] public void DamageSnapshotContainsActualTargetHpAndUnspentDefenseBeforeTheDestructionFrame()
        {
            var battle = ClassicFixtures.Battle(rules: new RuleSet { baseCoins = 0, defenderStrikesBack = false });
            var attacker = ClassicFixtures.Unit(battle, 0, "데몬", 0);
            var defender = ClassicFixtures.Unit(battle, 1, "크라켄", 0);
            attacker.bonusPower = 30; defender.armor = 2;
            int dp = CardCatalog.UnitDefense(defender);
            battle.BeginPlaybackCapture(); ClassicFixtures.Resolve(battle); var frames = battle.EndPlaybackCapture();
            var hit = frames.Single(f => f.item.kind == "attack");
            Assert.That(hit.item.targetInstanceId, Is.EqualTo(defender.instanceId));
            Assert.That(CardCatalog.UnitHealth(hit.players[1].AtLane(0)), Is.Zero);
            Assert.That(CardCatalog.UnitDefense(hit.players[1].AtLane(0)), Is.EqualTo(dp), "An ordinary hit does not spend DP.");
            var death = frames.Single(f => f.item.kind == "defeat" && f.item.sourceInstanceId == defender.instanceId);
            Assert.That(death.item.sequence, Is.GreaterThan(hit.item.sequence));
            Assert.That(death.players[1].AtLane(0), Is.Null);
            Assert.That(hit.players[1].AtLane(0), Is.Not.Null, "The impact view must retain the card until destruction animates.");
        }
    }
}
