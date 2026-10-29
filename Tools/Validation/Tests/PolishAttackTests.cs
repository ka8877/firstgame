using System;
using System.Collections.Generic;
using System.Linq;
using FantasyCards.Core;
using FantasyCards.Tests;
using NUnit.Framework;

namespace FirstGame.Validation
{
    public sealed class PolishAttackTests
    {
        const string Plain = "fm_2042920002";
        static BattleEngine Battle() => ClassicFixtures.Battle(rules: new RuleSet { baseCoins = 0, defenderStrikesBack = false });
        static UnitState Unit(BattleEngine engine, int owner, string id, int lane)
        {
            var player = engine.State.players[owner];
            var card = player.deck.Concat(player.hand).FirstOrDefault(c => c.definitionId == id);
            if (card == null) { card = player.deck.Last(); card.definitionId = id; }
            player.deck.Remove(card); player.hand.Remove(card);
            var unit = new UnitState { instanceId = card.instanceId, definitionId = id, lane = lane };
            player.units.Add(unit); return unit;
        }
        static void Stats(UnitState unit, int ap = 3, int dp = 0, int hp = 30)
        {
            unit.bonusPower += ap - CardCatalog.BaseAttack(unit);
            unit.armor += dp - CardCatalog.BaseDefense(unit);
            unit.bonusHealth += hp - CardCatalog.BaseHealth(unit);
        }
        static List<BattlePlaybackFrame> Resolve(BattleEngine engine)
        { engine.BeginPlaybackCapture(); ClassicFixtures.Resolve(engine); return engine.EndPlaybackCapture(); }
        static BattlePlaybackFrame Attack(List<BattlePlaybackFrame> frames, UnitState source) =>
            frames.Single(f => f.item.kind == "attack" && f.item.sourceInstanceId == source.instanceId);

        [Test] public void StrongConcealmentWorksAtZeroAttackAndRecordsRealStatTransition()
        {
            var e = Battle(); var source = Unit(e, 0, "fm_8070223548", 0); Stats(source, 0, 1);
            var target = Unit(e, 1, Plain, 0); Stats(target);
            uint rng = e.State.randomState;
            var frame = Resolve(e).Single(f => f.item.text.Contains("강한은폐 ·"));
            Assert.That(frame.item.hasStatTransition, Is.True);
            Assert.That(frame.item.beforeDefense, Is.EqualTo(1));
            Assert.That(frame.item.afterDefense, Is.EqualTo(3));
            Assert.That(CardCatalog.UnitAttack(frame.players[0].AtLane(0)), Is.Zero);
            Assert.That(e.State.randomState, Is.EqualTo(rng));
        }

        [TestCase(1, 1)] [TestCase(10, 0)] [TestCase(19, 9)]
        public void TimeAttackAddsTurnDigitWithoutChangingAP(int turn, int bonus)
        {
            var e = Battle(); e.State.turn = turn;
            var source = Unit(e, 0, "fm_13100227068", 0); Stats(source, 2);
            var target = Unit(e, 1, Plain, 0); Stats(target);
            var attack = Attack(Resolve(e), source);
            Assert.That(attack.item.additionalAttackDamage, Is.EqualTo(bonus));
            Assert.That(attack.item.amount, Is.EqualTo(2 + bonus));
            Assert.That(CardCatalog.UnitAttack(attack.players[0].AtLane(0)), Is.EqualTo(2));
        }

        [TestCase(2, 2)] [TestCase(7, 3)]
        public void AttackUseClampsTheActualStolenAPAndAddsDamageOnly(int enemyAp, int stolen)
        {
            var e = Battle(); var source = Unit(e, 0, "fm_10080425090", 0); Stats(source, 5);
            var target = Unit(e, 1, Plain, 0); Stats(target, enemyAp);
            var frames = Resolve(e); var attack = Attack(frames, source);
            Assert.That(attack.item.additionalAttackDamage, Is.EqualTo(stolen));
            Assert.That(attack.item.amount, Is.EqualTo(5 + stolen));
            Assert.That(CardCatalog.UnitAttack(attack.players[0].AtLane(0)), Is.EqualTo(5));
            Assert.That(CardCatalog.UnitAttack(attack.players[1].AtLane(0)), Is.EqualTo(enemyAp - stolen));
        }

        [Test] public void StrongDefenseUsePreservesDefenseUntilAfterDamage()
        {
            var e = Battle(); var source = Unit(e, 0, "fm_13100227067", 0); Stats(source, 3);
            var target = Unit(e, 1, Plain, 0); Stats(target, dp: 5);
            var frames = Resolve(e); var attack = Attack(frames, source);
            var after = frames.Single(f => f.item.text.Contains("강한방어이용 · 피해 후"));
            Assert.That(attack.item.additionalAttackDamage, Is.EqualTo(5));
            Assert.That(attack.item.amount, Is.EqualTo(3), "The printed defense still blocks the hit; it must not be bypassed twice.");
            Assert.That(CardCatalog.UnitDefense(attack.players[1].AtLane(0)), Is.EqualTo(5));
            Assert.That(after.item.sequence, Is.GreaterThan(attack.item.sequence));
            Assert.That(CardCatalog.UnitDefense(after.players[1].AtLane(0)), Is.Zero);
        }

        [TestCase(3, false)] [TestCase(2, true)]
        public void FlameAttackRequiresRealDamageAndAppliesItsLossAfterTheHit(int dp, bool triggers)
        {
            var e = Battle(); var source = Unit(e, 0, "fm_10020324679", 0); Stats(source, 3);
            var target = Unit(e, 1, Plain, 0); Stats(target, dp: dp, hp: 20);
            var frames = Resolve(e); var attack = Attack(frames, source);
            var effect = frames.SingleOrDefault(f => f.item.text.Contains("화염공격 · 피해 후"));
            Assert.That(effect != null, Is.EqualTo(triggers));
            if (triggers)
            {
                Assert.That(effect.item.sequence, Is.GreaterThan(attack.item.sequence));
                Assert.That(CardCatalog.UnitDefense(effect.players[1].AtLane(0)), Is.EqualTo(1));
                Assert.That(CardCatalog.UnitHealth(effect.players[1].AtLane(0)), Is.EqualTo(18));
            }
        }

        [TestCase(1)] [TestCase(20)]
        public void WeaponSuppressionSetsSurvivorToOneWithoutResurrectingAKill(int hp)
        {
            var e = Battle(); var source = Unit(e, 0, "fm_10031724759", 0); Stats(source, 3);
            string equipmentUnit = CardCatalog.All.First(c => c.kind == CardKind.Unit && c.capabilityMask == 2 && RenewalCardSkills.AttackCode(c.id) == 0).id;
            var target = Unit(e, 1, equipmentUnit, 0); Stats(target, 6, 0, hp);
            var effect = Resolve(e).Single(f => f.item.text.Contains("무기제압 ·"));
            var affected = effect.players[1].AtLane(0);
            Assert.That(CardCatalog.UnitAttack(affected), Is.Zero);
            Assert.That(CardCatalog.UnitDefense(affected), Is.Zero);
            Assert.That(CardCatalog.UnitHealth(affected), Is.EqualTo(hp == 1 ? 0 : 1));
            Assert.That(e.State.players[1].units.Any(u => u.instanceId == target.instanceId), Is.EqualTo(hp != 1));
        }

        [Test] public void PillageUsesActualResolvedCoinCountAndAddsNoExtraRoll()
        {
            var e = Battle(); var source = Unit(e, 0, "fm_10020324682", 0); Stats(source, 5); source.extraCoins = 2; source.coinChanceDelta = -50;
            var target = Unit(e, 1, Plain, 0); Stats(target);
            var frames = Resolve(e); var attack = Attack(frames, source);
            Assert.That(frames.Count(f => f.item.kind == "coin" && f.item.sourceInstanceId == source.instanceId), Is.EqualTo(2));
            Assert.That(frames.Count(f => f.item.kind == "coin" && f.item.sourceInstanceId == target.instanceId), Is.Zero);
            Assert.That(attack.item.additionalAttackDamage, Is.EqualTo(2));
            Assert.That(attack.item.amount, Is.EqualTo(attack.item.attack + 2));
        }

        [TestCase("fm_10080425093", true, 4)] [TestCase("fm_8070223551", false, 7)]
        public void SoulDamageUsesPreparationBalanceAndOnlyBurstConsumesItsOwnersSoul(string id, bool consumes, int bonus)
        {
            var e = Battle(); var source = Unit(e, 0, id, 0); Stats(source, 0);
            var target = Unit(e, 1, Plain, 0); Stats(target);
            e.State.players[0].genericSoul = 4; e.State.players[1].genericSoul = 3;
            var attack = Attack(Resolve(e), source);
            Assert.That(attack.item.additionalAttackDamage, Is.EqualTo(bonus));
            Assert.That(attack.item.amount, Is.EqualTo(bonus));
            Assert.That(CardCatalog.UnitAttack(attack.players[0].AtLane(0)), Is.Zero);
            Assert.That(attack.players[0].genericSoul, Is.EqualTo(consumes ? 0 : 4));
            Assert.That(attack.players[1].genericSoul, Is.EqualTo(3));
        }

        [TestCase("fm_9080524204", 2)] [TestCase("fm_8070223547", 3)]
        public void SoundAttacksChangeNextEnemyOnlyAndUltrasoundSkipsExhaustedUnits(string id, int loss)
        {
            var e = Battle(); var source = Unit(e, 0, id, 0); Stats(source, 1);
            var target = Unit(e, 1, Plain, 0); Stats(target);
            var skipped = Unit(e, 1, "fm_2042920037", 1); Stats(skipped, 5, 5); skipped.exhausted = true;
            var next = Unit(e, 1, "fm_2042820004", 3); Stats(next, 5, 5);
            var effect = Resolve(e).Single(f => f.item.text.Contains(loss == 3 ? "초음파 ·" : "음파공격 ·"));
            Assert.That(effect.item.targetInstanceId, Is.EqualTo(loss == 3 ? next.instanceId : skipped.instanceId));
            var affected = effect.players[1].units.Single(u => u.instanceId == effect.item.targetInstanceId);
            Assert.That(CardCatalog.UnitAttack(affected), Is.EqualTo(5 - loss));
            Assert.That(CardCatalog.UnitDefense(affected), Is.EqualTo(5 - loss));
            Assert.That(CardCatalog.UnitDefense(effect.players[1].AtLane(0)), Is.Zero);
        }

        [Test] public void MultihitReducesNextEnemyHealthEvenWhenPrimaryAttackIsBlocked()
        {
            var e = Battle(); var source = Unit(e, 0, "fm_10080425089", 0); Stats(source, 1);
            var target = Unit(e, 1, Plain, 0); Stats(target, dp: 5);
            var next = Unit(e, 1, "fm_2042920037", 4); Stats(next, hp: 8);
            var frames = Resolve(e); var effect = frames.Single(f => f.item.text.Contains("멀티어택 ·"));
            Assert.That(effect.item.targetInstanceId, Is.EqualTo(next.instanceId));
            Assert.That(CardCatalog.UnitHealth(effect.players[1].AtLane(4)), Is.EqualTo(7));
            Assert.That(Attack(frames, source).item.amount, Is.Zero);
        }

        [TestCase(0, false)] [TestCase(1, true)]
        public void BranchAttackRequiresPositiveEnemyDefenseAndChangesActualAPAndHealth(int dp, bool triggers)
        {
            var e = Battle(); var source = Unit(e, 0, "fm_10020324684", 0); Stats(source, 2, hp: 10);
            var target = Unit(e, 1, Plain, 0); Stats(target, dp: dp);
            var attack = Attack(Resolve(e), source); var attacker = attack.players[0].AtLane(0);
            Assert.That(CardCatalog.UnitAttack(attacker), Is.EqualTo(triggers ? 4 : 2));
            Assert.That(CardCatalog.UnitHealth(attacker), Is.EqualTo(triggers ? 11 : 10));
            Assert.That(attack.item.additionalAttackDamage, Is.Zero);
        }

        [Test] public void ThrowingAttackTargetsTheNextLivingReadyEnemyOnlyAfterAKill()
        {
            var e = Battle(); var source = Unit(e, 0, "fm_10080425087", 0); Stats(source, 5);
            var target = Unit(e, 1, Plain, 0); Stats(target, hp: 1);
            var skipped = Unit(e, 1, "fm_2042920037", 2); Stats(skipped, 5, 5, 8); skipped.exhausted = true;
            var next = Unit(e, 1, "fm_2042820004", 4); Stats(next, 5, 5, 8);
            var frames = Resolve(e); var effect = frames.Single(f => f.item.text.Contains("투척공격 ·"));
            Assert.That(effect.item.sequence, Is.GreaterThan(Attack(frames, source).item.sequence));
            Assert.That(effect.item.targetInstanceId, Is.EqualTo(next.instanceId));
            var affected = effect.players[1].AtLane(4);
            Assert.That(new[] { CardCatalog.UnitAttack(affected), CardCatalog.UnitDefense(affected), CardCatalog.UnitHealth(affected) }, Is.EqualTo(new[] { 4, 4, 7 }));
            Assert.That(CardCatalog.UnitAttack(effect.players[1].AtLane(2)), Is.EqualTo(5));
        }

        [Test] public void ConsumeHealsByPreHitHealthNotOverkillDamage()
        {
            var e = Battle(); var source = Unit(e, 0, "fm_9090224321", 0); Stats(source, 12, hp: 5);
            Assert.That(CardCatalog.Get(Plain).level, Is.LessThanOrEqualTo(2));
            var target = Unit(e, 1, Plain, 0); Stats(target, hp: 3);
            var effect = Resolve(e).Single(f => f.item.text.Contains("컨슘 ·"));
            Assert.That(effect.item.amount, Is.EqualTo(3));
            Assert.That(CardCatalog.UnitHealth(effect.players[0].AtLane(0)), Is.EqualTo(8));
        }

        [Test] public void AlliedAttackCountsItsOwnAbilityIncludingItselfWithoutChangingAP()
        {
            var e = Battle(); var source = Unit(e, 0, "fm_10020324681", 0); Stats(source, 2);
            int mask = CardCatalog.Get(source.definitionId).capabilityMask;
            string same = CardCatalog.All.First(c => c.kind == CardKind.Unit && c.capabilityMask == mask && c.id != source.definitionId && RenewalCardSkills.AttackCode(c.id) == 0).id;
            string different = CardCatalog.All.First(c => c.kind == CardKind.Unit && c.capabilityMask != mask && RenewalCardSkills.AttackCode(c.id) == 0).id;
            Stats(Unit(e, 0, same, 1), 0); Stats(Unit(e, 0, different, 3), 0);
            Stats(Unit(e, 1, Plain, 0), hp: 60);
            var attack = Attack(Resolve(e), source);
            Assert.That(attack.item.additionalAttackDamage, Is.EqualTo(2));
            Assert.That(CardCatalog.UnitAttack(attack.players[0].AtLane(0)), Is.EqualTo(2));
        }

        [Test] public void ForestAllianceExcludesItselfAndAlliesOfOtherElements()
        {
            var e = Battle(); var source = Unit(e, 0, "fm_9120224507", 0); Stats(source, 2, hp: 10);
            string forest = CardCatalog.All.First(c => c.kind == CardKind.Unit && c.element == Element.Forest && c.id != source.definitionId && RenewalCardSkills.AttackCode(c.id) == 0).id;
            string nonforest = CardCatalog.All.First(c => c.kind == CardKind.Unit && c.element != Element.Forest && RenewalCardSkills.AttackCode(c.id) == 0).id;
            Stats(Unit(e, 0, forest, 1), 0); Stats(Unit(e, 0, nonforest, 3), 0);
            Stats(Unit(e, 1, Plain, 0), hp: 60);
            var attack = Attack(Resolve(e), source); var affected = attack.players[0].AtLane(0);
            Assert.That(CardCatalog.UnitAttack(affected), Is.EqualTo(3));
            Assert.That(CardCatalog.UnitHealth(affected), Is.EqualTo(11));
            Assert.That(attack.item.additionalAttackDamage, Is.Zero);
        }

        [TestCase(CardRarity.Secret, 1)]
        [TestCase(CardRarity.Special, 2)] [TestCase(CardRarity.Rare, 2)]
        [TestCase(CardRarity.Common, 3)] [TestCase(CardRarity.Unique, 0)]
        public void AppraisalUsesOnlyExplicitOriginalRarityGroups(CardRarity rarity, int expected)
        {
            var e = Battle(); var source = Unit(e, 0, "fm_14010827215", 0); Stats(source, 2);
            string id = CardCatalog.All.First(c => c.kind == CardKind.Unit && c.rarity == rarity).id;
            var target = Unit(e, 1, id, 0); Stats(target, 0, 0, 60);
            var attack = Attack(Resolve(e), source);
            Assert.That(attack.item.additionalAttackDamage, Is.EqualTo(expected));
            Assert.That(attack.item.amount, Is.EqualTo(2 + expected));
        }

        [TestCase(CardRarity.Legend)] [TestCase(CardRarity.Epic)]
        public void AppraisalKeepsPrintedRuleForGradesWithoutPlayableUnitFixtures(CardRarity rarity)
        {
            Assert.That(CardCatalog.All.Any(c => c.kind == CardKind.Unit && c.rarity == rarity), Is.False,
                "This fixture deliberately verifies the printed future-grade mapping without inventing a playable unit.");
            var rule = typeof(BattleEngine).GetMethod("PolishAppraisal", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(rule, Is.Not.Null);
            Assert.That((int)rule.Invoke(null, new object[] { rarity }), Is.EqualTo(1));
        }
    }
}
