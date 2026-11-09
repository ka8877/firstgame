using System;
using System.Collections.Generic;
using System.Linq;
using FantasyCards.Core;
using FantasyCards.Tests;
using NUnit.Framework;

namespace FirstGame.Validation
{
    public sealed class CanonicalAttackSkillsTests
    {
        const string Suppression = "fm_11070625788";
        const string Charm = "fm_16070628420";
        const string Frenzy = "fm_14100127645";
        const string Plain = "fm_2042920002";

        static BattleEngine Battle(bool counters = false, int coins = 0) =>
            ClassicFixtures.Battle(721, new RuleSet { baseCoins = coins, defenderStrikesBack = counters });

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
            unit.bonusHealth += hp + unit.damage - CardCatalog.BaseHealth(unit);
        }

        static string PlainWithAbility(int mask) => CardCatalog.All.First(c =>
            c.kind == CardKind.Unit && c.capabilityMask == mask && RenewalCardSkills.AttackCode(c.id) == 0).id;

        static List<BattlePlaybackFrame> Resolve(BattleEngine engine)
        { engine.BeginPlaybackCapture(); ClassicFixtures.Resolve(engine); return engine.EndPlaybackCapture(); }

        static BattlePlaybackFrame Strike(IEnumerable<BattlePlaybackFrame> frames, UnitState source, string kind = "attack") =>
            frames.Single(f => f.item.kind == kind && f.item.sourceInstanceId == source.instanceId);

        [Test] public void OnlyThreeReviewedNRankRepresentativesEnterTheSupportedCatalog()
        {
            var expected = new[] { Suppression, Charm, Frenzy };
            var actual = CardCatalog.All.Where(c => new[] { 602, 198, 54 }.Contains(RenewalCardSkills.AttackCode(c.id))).Select(c => c.id);
            Assert.That(actual, Is.EquivalentTo(expected));
            foreach (string id in expected)
            {
                var card = CardCatalog.Get(id); var original = OriginalCardArchive.Get(id);
                Assert.That(card.rank, Is.EqualTo(CardRank.N));
                Assert.That(card.deckLimit, Is.EqualTo(4));
                Assert.That(card.kind, Is.EqualTo(CardKind.Unit));
                Assert.That(original.playable, Is.True);
                Assert.That(original.unsupportedReason, Is.Empty);
                Assert.That(card.sourceUrl, Is.EqualTo("https://fmlunatic.com/card/" + id.Substring(3)));
                Assert.That(card.artResource, Is.EqualTo("StreamingCards/" + id));
            }
            // Similar names/skills with other rules remain view-only.
            foreach (string id in new[] { "fm_16080328461", "fm_10010624538", "fm_10010624550" })
                Assert.That(OriginalCardArchive.Get(id).playable, Is.False, id);
        }

        [TestCase(1, 3, false)] [TestCase(2, 3, false)]
        // The supported catalog's real compound units are all-capability
        // (mask7), not mask6. Check that actual compound type at AP3 and AP0.
        [TestCase(4, 3, true)] [TestCase(7, 3, true)] [TestCase(7, 0, false)]
        [TestCase(4, 0, false)]
        public void SpellSuppressionUsesTheMagicBitAndRequiresPositiveAttack(int ability, int ap, bool triggers)
        {
            var engine = Battle(); var source = Unit(engine, 0, Suppression, 0); Stats(source, ap);
            var target = Unit(engine, 1, PlainWithAbility(ability), 0); Stats(target, 8, 5, 40);
            Assert.That(CardCatalog.Get(target.definitionId).capabilityMask, Is.EqualTo(ability));
            var frames = Resolve(engine); var attack = Strike(frames, source);
            var affected = attack.players[1].AtLane(0);
            Assert.That(CardCatalog.UnitAttack(affected), Is.EqualTo(triggers ? 0 : 8));
            Assert.That(CardCatalog.UnitDefense(affected), Is.EqualTo(triggers ? 0 : 5));
            Assert.That(attack.item.amount, Is.EqualTo(triggers ? ap : 0));
            Assert.That(CardCatalog.UnitHealth(affected), Is.EqualTo(40 - attack.item.amount));
            var effect = frames.SingleOrDefault(f => f.item.kind == "skill" && f.item.sourceInstanceId == source.instanceId);
            Assert.That(effect != null, Is.EqualTo(triggers));
            if (effect != null) Assert.That(effect.item.sequence, Is.LessThan(attack.item.sequence));
        }

        [Test] public void SuppressionAlreadyChangesASurvivingMagicCounterattack()
        {
            var engine = Battle(true); var source = Unit(engine, 0, Suppression, 0); Stats(source, 3);
            var target = Unit(engine, 1, PlainWithAbility(4), 0); Stats(target, 8, 5);
            var frames = Resolve(engine); var counter = Strike(frames, target, "counter");
            Assert.That(counter.item.attack, Is.Zero);
            Assert.That(counter.item.amount, Is.Zero);
            Assert.That(CardCatalog.UnitHealth(counter.players[0].AtLane(0)), Is.EqualTo(30));
        }

        [TestCase(0, 0, true)] [TestCase(3, 3, true)] [TestCase(3, 2, false)]
        public void CharmUsesActualZeroDamageAndWorksAtZeroAttack(int ap, int dp, bool triggers)
        {
            var engine = Battle(true); var source = Unit(engine, 0, Charm, 0); Stats(source, ap);
            var target = Unit(engine, 1, Plain, 0); Stats(target, 8, dp);
            uint random = engine.State.randomState;
            var frames = Resolve(engine); var attack = Strike(frames, source);
            var effect = frames.SingleOrDefault(f => f.item.text.Contains("매료공격 · 피해 없음"));
            Assert.That(effect != null, Is.EqualTo(triggers));
            Assert.That(attack.item.amount, Is.EqualTo(Math.Max(0, ap - dp)));
            Assert.That(CardCatalog.UnitAttack(attack.players[1].AtLane(0)), Is.EqualTo(8), "The AP change must follow the hit.");
            var counter = Strike(frames, target, "counter");
            Assert.That(counter.item.attack, Is.EqualTo(triggers ? 0 : 8));
            Assert.That(counter.item.amount, Is.EqualTo(triggers ? 0 : 8));
            if (effect != null)
            {
                Assert.That(effect.item.sequence, Is.GreaterThan(attack.item.sequence));
                Assert.That(effect.item.sequence, Is.LessThan(counter.item.sequence));
                Assert.That(CardCatalog.UnitAttack(effect.players[1].AtLane(0)), Is.Zero);
                string unchanged = FieldJson.Encode(engine.State);
                effect.players[1].AtLane(0).rolledAttack = 99;
                Assert.That(CardCatalog.UnitAttack(counter.players[1].AtLane(0)), Is.Zero);
                Assert.That(FieldJson.Encode(engine.State), Is.EqualTo(unchanged));
            }
            Assert.That(engine.State.randomState, Is.EqualTo(random));
        }

        [TestCase(3, 3, 0, 2, 5)] // Official FAQ736: AP3(+2), HP1.
        [TestCase(0, 4, 0, 3, 3)] // The source says on attack, without AP >= 1.
        [TestCase(3, 1, 0, 0, 3)]
        [TestCase(3, 3, 4, 2, 5)] // Prior wounds do not count twice.
        public void FrenzyUsesRemainingHealthAsAdditionalDamageWithoutChangingAttack(int ap, int hp, int wounds, int bonus, int damage)
        {
            var engine = Battle(); var source = Unit(engine, 0, Frenzy, 0); source.damage = wounds; Stats(source, ap, hp: hp);
            var target = Unit(engine, 1, Plain, 0); Stats(target);
            uint random = engine.State.randomState;
            var frames = Resolve(engine); var attack = Strike(frames, source);
            var acting = attack.players[0].AtLane(0);
            Assert.That(attack.item.attack, Is.EqualTo(ap));
            Assert.That(attack.item.additionalAttackDamage, Is.EqualTo(bonus));
            Assert.That(attack.item.amount, Is.EqualTo(damage));
            Assert.That(CardCatalog.UnitAttack(acting), Is.EqualTo(ap));
            Assert.That(CardCatalog.UnitHealth(acting), Is.EqualTo(1));
            Assert.That(acting.damage, Is.EqualTo(wounds));
            Assert.That(acting.rolledHealth, Is.EqualTo(wounds + 1));
            Assert.That(CardCatalog.UnitHealth(attack.players[1].AtLane(0)), Is.EqualTo(30 - damage));
            Assert.That(engine.State.randomState, Is.EqualTo(random));
            Assert.That(engine.State.players[0].AtLane(0).coinsRolled, Is.False);
            Assert.That(CardCatalog.UnitHealth(engine.State.players[0].AtLane(0)), Is.EqualTo(CardCatalog.Get(Frenzy).health));
        }

        [Test] public void FrenzyAlsoActivatesOnASurvivingCounterattack()
        {
            var engine = Battle(true); var source = Unit(engine, 0, Plain, 0); Stats(source, 2, hp: 30);
            var target = Unit(engine, 1, Frenzy, 0); Stats(target, 3, hp: 6);
            var counter = Strike(Resolve(engine), target, "counter");
            Assert.That(counter.item.attack, Is.EqualTo(3));
            Assert.That(counter.item.additionalAttackDamage, Is.EqualTo(3), "Six HP minus two damage leaves four HP; exactly three becomes bonus damage.");
            Assert.That(counter.item.amount, Is.EqualTo(6));
            Assert.That(CardCatalog.UnitHealth(counter.players[1].AtLane(0)), Is.EqualTo(1));
        }

        [TestCase(Suppression)] [TestCase(Charm)] [TestCase(Frenzy)]
        public void NewSkillsKeepSchemaThreeSaveContinuationAndRealCoinRandomness(string id)
        {
            var engine = Battle(true, -1); var source = Unit(engine, 0, id, 0); Stats(source, 3, hp: 20);
            var target = Unit(engine, 1, PlainWithAbility(4), 0); Stats(target, 1, 5, 40);
            string saved = FieldJson.Encode(engine.State);
            var restored = new BattleEngine(FieldJson.Decode<BattleState>(saved));
            Assert.That(FieldJson.Encode(engine.State), Is.EqualTo(saved));
            engine.BeginPlaybackCapture(); ClassicFixtures.Resolve(engine); var frames = engine.EndPlaybackCapture();
            ClassicFixtures.Resolve(restored);
            Assert.That(frames.Any(f => f.item.kind == "coin" && f.item.sourceInstanceId == source.instanceId), Is.True);
            Assert.That(engine.State.randomState, Is.EqualTo(restored.State.randomState));
            Assert.That(FieldJson.Encode(engine.State), Is.EqualTo(FieldJson.Encode(restored.State)));
            BattleEngine.Validate(engine.State); BattleEngine.Validate(restored.State);
        }
    }
}
