using System;
using System.Linq;
using FantasyCards.Core;
using FantasyCards.Tests;
using NUnit.Framework;

namespace FirstGame.Validation
{
    public sealed class PolishPremiumTests
    {
        const string Plain = "fm_2042920002";
        static BattleEngine Battle(bool counters = false) => ClassicFixtures.Battle(rules: new RuleSet { baseCoins = 0, defenderStrikesBack = counters });
        static CardInstance Extract(BattleEngine e, int owner, string id)
        {
            var p = e.State.players[owner]; var card = p.deck.Concat(p.hand).FirstOrDefault(c => c.definitionId == id);
            if (card == null) { card = p.deck.Last(); card.definitionId = id; }
            p.deck.Remove(card); p.hand.Remove(card); return card;
        }
        static UnitState Unit(BattleEngine e, int owner, string id, int lane)
        {
            var c = Extract(e, owner, id); var u = new UnitState { instanceId = c.instanceId, definitionId = id, lane = lane };
            e.State.players[owner].units.Add(u); return u;
        }
        static void Stats(UnitState u, int ap = 3, int dp = 0, int hp = 30)
        { u.bonusPower += ap - CardCatalog.BaseAttack(u); u.armor += dp - CardCatalog.BaseDefense(u); u.bonusHealth += hp - CardCatalog.BaseHealth(u); }
        static BattlePlaybackFrame[] Resolve(BattleEngine e)
        { e.BeginPlaybackCapture(); ClassicFixtures.Resolve(e); return e.EndPlaybackCapture().ToArray(); }

        [Test] public void PremiumExpansionAddsRealPlayableGradesAndKeepsArchiveCompleteness()
        {
            Assert.That(CardCatalog.All.Length, Is.EqualTo(2646));
            Assert.That(CardCatalog.All.Count(c => c.rarity == CardRarity.Rare), Is.EqualTo(49));
            Assert.That(CardCatalog.All.Count(c => c.rarity == CardRarity.Special), Is.EqualTo(64));
            Assert.That(CardCatalog.All.Count(c => c.rarity == CardRarity.Unique), Is.EqualTo(27));
            Assert.That(CardCatalog.All.Count(c => c.rarity == CardRarity.Secret), Is.EqualTo(21));
            Assert.That(OriginalCardArchive.All.Count(c => c.playable), Is.EqualTo(CardCatalog.All.Length));
            Assert.That(OriginalCardArchive.All.Length, Is.EqualTo(15350));
            foreach (var id in new[] { "fm_3022820396", "fm_3041120436", "fm_3061320558", "fm_4021320946" })
                Assert.That(CardCatalog.Get(id).rarity, Is.EqualTo(CardRarity.Unique));
        }

        [TestCase(0)] [TestCase(2)]
        public void CoinDefenseUsesOnlyTheDefendersRealHeads(int heads)
        {
            var e = Battle(); var a = Unit(e, 0, Plain, 0); Stats(a, 5);
            var b = Unit(e, 1, "fm_10070724960", 0); Stats(b); b.extraCoins = 2; b.coinChanceDelta = heads == 2 ? 50 : -50;
            var frames = Resolve(e); var strike = frames.Single(f => f.item.kind == "attack");
            int dp = CardCatalog.UnitDefense(strike.players[1].AtLane(0));
            Assert.That(strike.item.amount, Is.EqualTo(Math.Max(0, 5 - dp - heads)));
            Assert.That(frames.Count(f => f.item.kind == "coin" && f.item.sourceInstanceId == b.instanceId && f.item.coinHeads), Is.EqualTo(heads));
        }

        [Test] public void DualTechniqueKeepsBothFearCounterAndLevelDefense()
        {
            var e = Battle(true); var a = Unit(e, 0, Plain, 0); Stats(a, 3);
            var b = Unit(e, 1, "fm_15050627919", 0); Stats(b, 2);
            var frames = Resolve(e);
            Assert.That(frames.Single(f => f.item.kind == "attack").item.amount, Is.EqualTo(2));
            Assert.That(frames.Single(f => f.item.kind == "counter").item.additionalAttackDamage, Is.EqualTo(1));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void SecretChainMovesTopExistingUnitsWithinHandCapacityWithoutCreatingCards(int vacancies)
        {
            var e = Battle(); var a = Unit(e, 0, Plain, 0); Stats(a, 20);
            var b = Unit(e, 1, "fm_15100728142", 0); Stats(b, hp: 1);
            var p = e.State.players[1];
            while (p.hand.Count > e.State.rules.handLimit - vacancies) { var c = p.hand.Last(); p.hand.Remove(c); p.deck.Add(c); }
            var old = Extract(e, 1, "fm_2042920037"); p.discard.Add(old);
            var frame = Resolve(e).Single(f => f.item.text.Contains("영혼의사슬 ·"));
            Assert.That(frame.item.amount, Is.EqualTo(vacancies));
            Assert.That(frame.players[1].hand.Any(c => c.instanceId == b.instanceId), Is.EqualTo(vacancies > 0));
            Assert.That(frame.players[1].hand.Any(c => c.instanceId == old.instanceId), Is.EqualTo(vacancies == 2));
            Assert.That(e.State.generatedCards, Is.Empty);
            BattleEngine.Validate(e.State);
        }

        [Test] public void UniqueDeathSoulRefundIsConsumedExactlyOnceAtSoulCost()
        {
            var e = Battle(); var a = Unit(e, 0, Plain, 0); Stats(a, 20);
            var b = Unit(e, 1, "fm_3022820396", 0); Stats(b, hp: 1);
            Resolve(e); var p = e.State.players[1];
            Assert.That(p.dawn, Is.EqualTo(2)); Assert.That(p.genericSoul, Is.EqualTo(2));
            Assert.That(p.nextColoredSoulBonus.All(n => n == 0), Is.True); Assert.That(p.nextGenericSoulBonus, Is.Zero);
            ClassicFixtures.Resolve(e);
            Assert.That(p.dawn, Is.Zero); Assert.That(p.genericSoul, Is.Zero);
            BattleEngine.Validate(e.State);
        }

        [Test] public void SoulRecoveryUsesKilledCardsOriginalColoredAndTotalCost()
        {
            var e = Battle(); var a = Unit(e, 0, "fm_3061320558", 0); Stats(a, 20);
            string id = CardCatalog.All.First(c => c.kind == CardKind.Unit && c.element == Element.Fire && c.coloredCost == 2 && c.genericCost == 2 && RenewalCardSkills.AttackCode(c.id) == 0).id;
            var b = Unit(e, 1, id, 0); Stats(b, hp: 1);
            var frame = Resolve(e).Single(f => f.item.text.Contains("소울 회수 예약"));
            Assert.That(frame.players[0].nextColoredSoulBonus[(int)Element.Fire], Is.EqualTo(2));
            Assert.That(frame.players[0].nextGenericSoulBonus, Is.EqualTo(4));
            Assert.That(e.State.players[0].fire, Is.EqualTo(2));
        }

        [Test] public void DeathMirrorCopiesActualLowerLevelKillerAndKeepsGeneratedSaveProof()
        {
            var e = Battle(); var killer = Unit(e, 0, Plain, 0); Stats(killer, 20);
            var mirror = Unit(e, 1, "fm_3041120436", 0); Stats(mirror, hp: 1);
            Resolve(e);
            var copy = e.State.players[1].units.Single();
            Assert.That(copy.definitionId, Is.EqualTo(killer.definitionId));
            Assert.That(copy.instanceId, Is.Not.EqualTo(killer.instanceId));
            Assert.That(e.State.generatedCards.Single().reason, Is.EqualTo("polish-death-mirror"));
            var restored = new BattleEngine(FieldJson.Decode<BattleState>(FieldJson.Encode(e.State)));
            Assert.That(restored.State.players[1].units.Single().definitionId, Is.EqualTo(Plain));
            var corrupt = restored.State.Copy(); corrupt.generatedCards.Clear(); Assert.Throws<ArgumentException>(() => new BattleEngine(corrupt));
        }

        [Test] public void SelfDestructionFromCoinsDoesNotInventAKillerForDeathMirror()
        {
            var e = Battle(); e.State.rules.baseCoins = 1;
            var a = Unit(e, 0, Plain, 0); Stats(a);
            var b = Unit(e, 1, "fm_3041120436", 0); Stats(b, hp: 1); b.coinChanceDelta = 50;
            Resolve(e);
            Assert.That(e.State.players[1].units, Is.Empty);
            Assert.That(e.State.generatedCards, Is.Empty);
        }

        [Test] public void DeathMirrorAttributesTheActualCollateralDeathTechnique()
        {
            var e = Battle(); var attacker = Unit(e, 0, Plain, 0); Stats(attacker, 20);
            var mirror = Unit(e, 0, "fm_3041120436", 1); Stats(mirror, hp: 1);
            var swordsman = Unit(e, 1, "fm_3022820394", 0); Stats(swordsman, 5, hp: 1);
            Resolve(e);
            var proof = e.State.generatedCards.Single(p => p.reason == "polish-death-mirror");
            Assert.That(proof.definitionId, Is.EqualTo(swordsman.definitionId));
            Assert.That(proof.sourceInstanceId, Is.EqualTo(mirror.instanceId));
            BattleEngine.Validate(e.State);
        }

        [Test] public void RecreateReusesTheDiscardItemAndDoesNotRerollAlreadyResolvedCoins()
        {
            var e = Battle(); var source = Unit(e, 0, "fm_4021320946", 0); Stats(source);
            var target = Unit(e, 1, Plain, 0); Stats(target);
            var itemDefinition = CardCatalog.All.First(c => c.kind == CardKind.Equipment && c.requiredCapabilityMask == 0 && c.attack > 0);
            var item = Extract(e, 0, itemDefinition.id); e.State.players[0].discard.Add(item);
            uint rng = e.State.randomState;
            var frame = Resolve(e).Single(f => f.item.text.Contains("리크리에이트 ·"));
            var restored = frame.players[0].AtLane(0);
            Assert.That(restored.equipment.Single().instanceId, Is.EqualTo(item.instanceId));
            Assert.That(CardCatalog.UnitAttack(restored), Is.EqualTo(3 + itemDefinition.attack));
            Assert.That(e.State.randomState, Is.EqualTo(rng));
            Assert.That(e.State.generatedCards, Is.Empty); BattleEngine.Validate(e.State);
        }

        [Test] public void SecondUpgradeProducesSoulOnlyWhileItsOriginalUnitRemainsOnField()
        {
            var e = Battle(); var unit = Unit(e, 0, "fm_10022424696", 0); Stats(unit, 0);
            var target = Unit(e, 1, Plain, 0); Stats(target);
            var d = CardCatalog.Get(unit.definitionId); Resolve(e);
            Assert.That(e.State.players[0].Resource(d.element), Is.EqualTo(1));
            Assert.That(e.State.events.Any(x => x.kind == "upkeep" && x.actor == 0), Is.True);
            Assert.That(PolishCardSkills.UpgradeProduction(d.id), Is.True);
        }

        [Test] public void LegacyMissingSoulArrayDefaultsToZeroAndCopyNeverAliasesQueuedBonuses()
        {
            var e = Battle(); var legacy = e.State.Copy(); legacy.players[0].nextColoredSoulBonus = null;
            var restored = new BattleEngine(legacy);
            Assert.That(legacy.players[0].nextColoredSoulBonus, Is.Null);
            Assert.That(restored.State.players[0].nextColoredSoulBonus, Is.EqualTo(new int[7]));
            restored.State.players[0].nextColoredSoulBonus[2] = 3;
            var copy = restored.State.Copy(); copy.players[0].nextColoredSoulBonus[2] = 1;
            Assert.That(restored.State.players[0].nextColoredSoulBonus[2], Is.EqualTo(3));
            var corrupt = e.State.Copy(); corrupt.players[0].nextColoredSoulBonus = new int[6];
            Assert.Throws<ArgumentException>(() => new BattleEngine(corrupt));
        }
    }
}
