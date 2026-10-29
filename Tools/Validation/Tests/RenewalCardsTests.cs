using System;
using System.Linq;
using FantasyCards.Core;
using FantasyCards.Tests;
using NUnit.Framework;
namespace FirstGame.Validation
{
    public sealed class RenewalCardsTests
    {
        static UnitState UnitWithCode(BattleEngine e, int owner, int code, int lane)
        {
            var unit = ClassicFixtures.Unit(e, owner, "농민", lane);
            unit.definitionId = CardCatalog.All.First(c => c.kind == CardKind.Unit && RenewalCardSkills.AttackCode(c.id) == code).id;
            return unit;
        }
        static void Stats(UnitState unit, int ap, int dp, int hp)
        { var d = CardCatalog.Get(unit.definitionId); unit.bonusPower = ap - d.attack; unit.armor = dp - d.defense; unit.bonusHealth = hp - d.health; }

        [Test] public void EveryPartialCoinShowsItsActualSnapshotAndPlaybackDoesNotConsumeExtraRandomness()
        {
            var a = ClassicFixtures.Battle(912); var unit = ClassicFixtures.Unit(a, 0, "임프", 0);
            var b = new BattleEngine(a.State.Copy()); uint expected = a.State.randomState;
            int heads = StableRandom.Range(ref expected, 2);
            a.BeginPlaybackCapture(); ClassicFixtures.Resolve(a); ClassicFixtures.Resolve(b); var frames = a.EndPlaybackCapture();
            var coin = frames.Single(f => f.item.kind == "coin" && f.item.sourceInstanceId == unit.instanceId);
            Assert.That(coin.item.hasStatTransition, Is.True);
            Assert.That(coin.item.beforeAttack, Is.EqualTo(CardCatalog.Get(unit.definitionId).attack));
            Assert.That(coin.item.afterAttack, Is.EqualTo(coin.item.beforeAttack + heads));
            Assert.That(coin.item.afterAttack, Is.EqualTo(CardCatalog.UnitAttack(coin.players[0].AtLane(0))));
            Assert.That(coin.item.afterDefense, Is.EqualTo(CardCatalog.UnitDefense(coin.players[0].AtLane(0))));
            Assert.That(a.State.randomState, Is.EqualTo(expected));
            Assert.That(FieldJson.Encode(a.State), Is.EqualTo(FieldJson.Encode(b.State)));
        }
        [Test] public void DashDamageIsSeparateFromApAndIsNotLostBehindTargetDefense()
        {
            var e = ClassicFixtures.Battle(rules: new RuleSet { baseCoins = 0, defenderStrikesBack = false });
            var a = UnitWithCode(e, 0, 135, 0); var b = ClassicFixtures.Unit(e, 1, "크라켄", 0);
            Stats(a, 3, 2, 30); Stats(b, 2, 4, 30); ClassicFixtures.Resolve(e);
            var strike = e.State.events.Single(x => x.kind == "attack" && x.sourceInstanceId == a.instanceId);
            Assert.That(strike.attack, Is.EqualTo(3), "Bonus damage must not become AP.");
            Assert.That(strike.additionalAttackDamage, Is.EqualTo(2)); Assert.That(strike.amount, Is.EqualTo(1));
        }
        [Test] public void ADefendersAttackTechniqueRunsOnItsSurvivingCounterattack()
        {
            var e = ClassicFixtures.Battle(rules: new RuleSet { baseCoins = 0 });
            var a = ClassicFixtures.Unit(e, 0, "농민", 0); var b = UnitWithCode(e, 1, 572, 0);
            Stats(a, 1, 0, 30); Stats(b, 1, 2, 30); e.BeginPlaybackCapture(); ClassicFixtures.Resolve(e);
            var frames = e.EndPlaybackCapture(); var regen = frames.Single(f => f.item.kind == "skill" && f.item.sourceInstanceId == b.instanceId);
            Assert.That(regen.item.afterHealth, Is.EqualTo(31));
            Assert.That(regen.item.sequence, Is.LessThan(frames.Single(f=>f.item.kind=="counter").item.sequence));
        }
        [Test] public void LongRangeRequiresActualHpDamageToPreventCounterattack()
        {
            foreach (bool blocked in new[] { false, true })
            {
                var e = ClassicFixtures.Battle(rules: new RuleSet { baseCoins = 0 });
                var a = UnitWithCode(e, 0, 568, 0); var b = ClassicFixtures.Unit(e, 1, "크라켄", 0);
                Stats(a, 3, 0, 30); Stats(b, 2, blocked ? 4 : 0, 30); ClassicFixtures.Resolve(e);
                Assert.That(e.State.events.Any(x=>x.kind=="counter"), Is.EqualTo(blocked));
            }
        }
        [Test] public void DoubleChainCopiesOnceToARealUnarmedUnitAndPreservesOriginalDeckCards()
        {
            var e = ClassicFixtures.Battle(); ClassicFixtures.Souls(e, 0);
            var a = ClassicFixtures.Unit(e, 0, "농민", 0); var b = ClassicFixtures.Unit(e, 0, "크라켄", 2);
            var item = ClassicFixtures.Hand(e, 0, "신록의활"); int originalNext = e.State.nextInstanceId;
            Assert.True(e.Play(0, item.instanceId, 0, out var error), error);
            Assert.That(a.equipment.Count, Is.EqualTo(1)); Assert.That(b.equipment.Count, Is.EqualTo(1));
            Assert.That(b.equipment[0].definitionId, Is.EqualTo(item.definitionId));
            Assert.That(e.State.nextInstanceId, Is.EqualTo(originalNext + 1));
            Assert.That(e.State.generatedCards.Single().reason, Is.EqualTo("double-chain"));
            BattleEngine.Validate(e.State);
            var restored = new BattleEngine(FieldJson.Decode<BattleState>(FieldJson.Encode(e.State)));
            Assert.That(restored.State.generatedCards.Single().instanceId, Is.EqualTo(b.equipment[0].instanceId));
            var corrupt = restored.State.Copy(); corrupt.generatedCards.Clear(); Assert.Throws<ArgumentException>(()=>new BattleEngine(corrupt));
            corrupt.generatedCards = null; Assert.Throws<ArgumentException>(()=>new BattleEngine(corrupt), "A generated instance still requires its provenance even when an importer leaves the list null");
        }
        [Test] public void LegacySchemaThreeWithoutGenerationListPreservesOriginalDeckAndRandomContinuation()
        {
            var a = ClassicFixtures.Battle(318); ClassicFixtures.Unit(a, 0, "임프", 0);
            var legacy = a.State.Copy(); legacy.generatedCards = null;
            BattleEngine.Validate(legacy);
            Assert.That(legacy.generatedCards, Is.Null, "Validation must not rewrite the caller's preserved payload");
            var b = new BattleEngine(legacy);
            Assert.That(b.State.generatedCards, Is.Empty);
            ClassicFixtures.Resolve(a); ClassicFixtures.Resolve(b);
            Assert.That(FieldJson.Encode(a.State), Is.EqualTo(FieldJson.Encode(b.State)));
        }
        [Test] public void IdCopyCreatesUnEquippedLowLevelUnitAndDoesNotCopyHighLevelUnits()
        {
            foreach (string name in new[] { "농민", "크라켄" })
            {
                var e = ClassicFixtures.Battle(); ClassicFixtures.Souls(e, 0); var unit = ClassicFixtures.Unit(e, 0, name, 0);
                var item = ClassicFixtures.Hand(e, 0, "스틱키휩");
                Assert.True(e.Play(0, item.instanceId, 0, out var error), error);
                if (name == "농민")
                {
                    var copied = e.State.players[0].AtLane(1);
                    Assert.That(copied, Is.Not.Null); Assert.That(copied.definitionId, Is.EqualTo(unit.definitionId));
                    Assert.That(copied.equipment, Is.Empty); Assert.That(copied.instanceId, Is.Not.EqualTo(unit.instanceId));
                    Assert.That(e.State.generatedCards.Single().reason, Is.EqualTo("id-copy"));
                }
                else Assert.That(e.State.generatedCards, Is.Empty);
                BattleEngine.Validate(e.State);
            }
        }
        [Test] public void NewSevenPlayablePresetsHaveOriginalSecretBuildsAndKeepEveryCardLimit()
        {
            var decks = DemoCatalog.StarterDecks.Where(d=>d.id.StartsWith("renewal-")).ToArray(); Assert.That(decks.Length, Is.EqualTo(7));
            foreach (var deck in decks)
            {
                DemoCatalog.ValidatePlayableDeck(deck.cards);
                Assert.That(deck.cards.Select(CardCatalog.Get).Count(c=>c.rarity==CardRarity.Secret), Is.GreaterThanOrEqualTo(2));
                Assert.That(deck.cards.Any(RenewalCardSkills.DoubleChain), Is.True);
                Assert.That(deck.cards.Any(RenewalCardSkills.IdCopy), Is.True);
                Assert.That(deck.cards.Select(CardCatalog.Get).Count(c=>c.kind==CardKind.Unit && c.level<=2), Is.GreaterThanOrEqualTo(12));
            }
        }
    }
}
