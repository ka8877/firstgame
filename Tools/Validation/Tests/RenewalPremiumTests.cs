using System;
using System.Linq;
using FantasyCards.Core;
using FantasyCards.Tests;
using NUnit.Framework;

namespace FirstGame.Validation
{
    public sealed class RenewalPremiumTests
    {
        static BattleEngine Battle(bool counters = false) => ClassicFixtures.Battle(rules: new RuleSet { baseCoins = 0, defenderStrikesBack = counters });
        static CardInstance Extract(BattleEngine e, int owner, string id)
        {
            var p = e.State.players[owner];
            var card = p.deck.Concat(p.hand).FirstOrDefault(c => c.definitionId == id);
            if (card == null) { card = p.deck.Last(); card.definitionId = id; }
            p.deck.Remove(card); p.hand.Remove(card); return card;
        }
        static UnitState Unit(BattleEngine e, int owner, string id, int lane)
        {
            var card = Extract(e, owner, id); var unit = new UnitState { instanceId = card.instanceId, definitionId = id, lane = lane };
            e.State.players[owner].units.Add(unit); return unit;
        }
        static string Plain => "fm_2042920002"; // Preserved original Death Hand, not a same-name later rank.
        static void SetAp(UnitState u, int ap) { u.bonusPower = ap - CardCatalog.BaseAttack(u); }
        static void SetHp(UnitState u, int hp) { u.bonusHealth = hp - CardCatalog.BaseHealth(u); }
        static void SetDp(UnitState u, int dp) { u.armor = dp - CardCatalog.BaseDefense(u); }

        [TestCase(2, false)] [TestCase(3, true)]
        public void IceBreathChecksThresholdThenRoundsEachEnemyAttackUp(int ap, bool triggers)
        {
            var e = Battle(); var dragon = Unit(e, 0, "fm_3010820341", 0); SetAp(dragon, ap);
            var first = Unit(e, 1, Plain, 0); SetAp(first, 5); SetHp(first, 20);
            var second = Unit(e, 1, "fm_2042920037", 3); SetAp(second, 3); SetHp(second, 20);
            e.BeginPlaybackCapture(); ClassicFixtures.Resolve(e); var frames = e.EndPlaybackCapture();
            var breath = frames.SingleOrDefault(f => f.item.text.Contains("아이스브레스 · AP -3"));
            Assert.That(breath != null, Is.EqualTo(triggers));
            if (triggers)
            {
                Assert.That(CardCatalog.UnitAttack(breath.players[0].AtLane(0)), Is.Zero);
                Assert.That(CardCatalog.UnitAttack(breath.players[1].AtLane(0)), Is.EqualTo(3));
                Assert.That(CardCatalog.UnitAttack(breath.players[1].AtLane(3)), Is.EqualTo(2));
            }
            BattleEngine.Validate(e.State);
        }
        [Test] public void MoralePermanentAttackSurvivesCombatResetAndSaveRestore()
        {
            var e = Battle(); var general = Unit(e, 0, "fm_3010820343", 0); SetAp(general, 2);
            var ally = Unit(e, 0, Plain, 1); SetHp(ally, 20);
            var enemy = Unit(e, 1, "fm_2042920037", 0); SetHp(enemy, 30);
            int baseline = CardCatalog.Get(ally.definitionId).attack;
            ClassicFixtures.Resolve(e);
            Assert.That(ally.permanentPower, Is.EqualTo(1));
            Assert.That(CardCatalog.UnitAttack(ally), Is.EqualTo(baseline + 1));
            Assert.That(general.permanentPower, Is.Zero);
            var restored = new BattleEngine(FieldJson.Decode<BattleState>(FieldJson.Encode(e.State)));
            Assert.That(restored.State.players[0].units.Single(u => u.instanceId == ally.instanceId).permanentPower, Is.EqualTo(1));
        }
        [Test] public void PiercingIgnoreBypassesDefenseAndChangesOnlyFirstTwoFixedSlotsAfterDamage()
        {
            var e = Battle(); var attacker = Unit(e, 0, "fm_3061320557", 0);
            var first = Unit(e, 1, Plain, 0); SetDp(first, 4); SetHp(first, 20);
            var second = Unit(e, 1, "fm_2042920037", 1); SetDp(second, 6); SetHp(second, 20);
            var last = Unit(e, 1, "fm_2042820004", 4); SetDp(last, 6); SetHp(last, 20);
            e.BeginPlaybackCapture(); ClassicFixtures.Resolve(e); var frames = e.EndPlaybackCapture();
            var attack = frames.Single(f => f.item.kind == "attack" && f.item.sourceInstanceId == attacker.instanceId);
            Assert.That(attack.item.amount, Is.EqualTo(7));
            var effect = frames.Single(f => f.item.text.Contains("관통무시 · 적 1·2전열"));
            Assert.That(effect.item.sequence, Is.GreaterThan(attack.item.sequence));
            Assert.That(CardCatalog.UnitDefense(effect.players[1].AtLane(0)), Is.Zero);
            Assert.That(CardCatalog.UnitDefense(effect.players[1].AtLane(1)), Is.EqualTo(2));
            Assert.That(CardCatalog.UnitDefense(effect.players[1].AtLane(4)), Is.EqualTo(6));
        }
        [Test] public void LuckStealingChangesFutureProbabilitiesWithoutRerollingResolvedCoinsAndResetsNextTurn()
        {
            var e = Battle(); var thief = Unit(e, 0, "fm_4021320948", 0);
            var enemy = Unit(e, 1, Plain, 0); SetHp(enemy, 20);
            uint rng = e.State.randomState;
            e.BeginPlaybackCapture(); ClassicFixtures.Resolve(e); var frames = e.EndPlaybackCapture();
            var changed = frames.Single(f => f.item.text.Contains("행운훔치기 · 아군 코인률"));
            Assert.That(OriginalEquipmentModifiers.ChancePercent(changed.players[0].AtLane(0)), Is.EqualTo(70));
            Assert.That(OriginalEquipmentModifiers.ChancePercent(changed.players[1].AtLane(0)), Is.EqualTo(30));
            Assert.That(thief.coinChanceDelta, Is.Zero); Assert.That(enemy.coinChanceDelta, Is.Zero);
            Assert.That(e.State.randomState, Is.EqualTo(rng), "baseCoins=0 has no random operation; technique must not reroll");
        }
        [Test] public void StoneThrowCreatesDeferredRealSpellAndGeneratedInstanceSurvivesSave()
        {
            var e = Battle(); var king = Unit(e, 0, "fm_3010820344", 0);
            var target = Unit(e, 1, Plain, 0); SetHp(target, 1);
            e.BeginPlaybackCapture(); ClassicFixtures.Resolve(e); var frames = e.EndPlaybackCapture();
            var stone = e.State.players[0].preparedSpells.Single(s => s.definitionId == "fm_2050330032");
            Assert.That(stone.lane, Is.EqualTo(1));
            Assert.That(frames.Any(f => f.item.kind == "spell" && f.item.sourceInstanceId == stone.instanceId), Is.False);
            BattleEngine.Validate(e.State);
            var restored = new BattleEngine(FieldJson.Decode<BattleState>(FieldJson.Encode(e.State)));
            Assert.That(restored.State.players[0].preparedSpells.Single().instanceId, Is.EqualTo(stone.instanceId));
            ClassicFixtures.Resolve(e);
            Assert.That(e.State.events.Count(x => x.kind == "spell" && x.sourceInstanceId == stone.instanceId), Is.EqualTo(1));
        }
        [Test] public void EchoCopiesEligibleOpponentHandWithoutConsumingItAndGeneratesDistinctInstance()
        {
            var e = Battle(); var attacker = Unit(e, 0, Plain, 0); SetAp(attacker, 12);
            var echo = Unit(e, 1, "fm_4021320944", 0); SetHp(echo, 1);
            var copy = Extract(e, 0, "fm_2042920037");
            var own = e.State.players[0]; own.deck.AddRange(own.hand); own.hand.Clear(); own.hand.Add(copy);
            ClassicFixtures.Resolve(e);
            var generated = e.State.players[1].units.Single(u => u.definitionId == copy.definitionId);
            Assert.That(generated.instanceId, Is.Not.EqualTo(copy.instanceId));
            Assert.That(generated.instanceId, Is.GreaterThan(e.State.rules.deckSize * 2));
            Assert.That(own.hand.Any(c => c.instanceId == copy.instanceId), Is.True);
            BattleEngine.Validate(e.State);
        }
        [TestCase(false, 3)] [TestCase(true, 6)]
        public void GloryDefenseUsesMonoLightProductionCapBeforeAttackerTechnique(bool monoLight, int expectedDamage)
        {
            var e = Battle(true); var attacker = Unit(e, 0, Plain, 0); SetAp(attacker, 6); SetHp(attacker, 5);
            var saint = Unit(e, 1, "fm_8062923223", 0);
            var p = e.State.players[1]; p.deck.AddRange(p.hand); p.hand.Clear();
            var land = CardCatalog.All.First(c => c.kind == CardKind.Terrain && c.element == (monoLight ? Element.Dawn : Element.Fire));
            p.terrain.Add(Extract(e, 1, land.id));
            e.BeginPlaybackCapture(); ClassicFixtures.Resolve(e); var frames = e.EndPlaybackCapture();
            var defense = frames.First(f => f.item.text.Contains("영광의기사 · 선제 HP 피해"));
            Assert.That(defense.item.amount, Is.EqualTo(expectedDamage));
            if (monoLight)
            {
                Assert.That(frames.Any(f => f.item.kind == "attack" && f.item.sourceInstanceId == attacker.instanceId && f.item.amount > 0), Is.False);
                Assert.That(e.State.players[0].discard.Any(c => c.instanceId == attacker.instanceId), Is.True);
            }
            BattleEngine.Validate(e.State);
        }
        [TestCase(CoinResolutionOrder.DefenderFirst)]
        [TestCase(CoinResolutionOrder.AttackerFirst)]
        public void CoinFlameExcludesActualBattleOpponentWhenGloryKillsItBeforeAnyStrike(CoinResolutionOrder order)
        {
            var e = Battle(true); e.State.rules.baseCoins = 2; e.State.rules.coinOrder = order;
            var flame = Unit(e, 0, "fm_4021320943", 0); SetHp(flame, 1); flame.coinChanceDelta = 50;
            var saint = Unit(e, 1, "fm_8062923223", 0); SetHp(saint, 20);
            var other = Unit(e, 1, Plain, 1); SetHp(other, 20);
            var defending = e.State.players[1]; defending.deck.AddRange(defending.hand); defending.hand.Clear();
            e.BeginPlaybackCapture(); ClassicFixtures.Resolve(e); var frames = e.EndPlaybackCapture();
            var deathEffect = frames.Single(f => f.item.text.Contains("코인플레임 · 전투 상대 외"));
            Assert.That(deathEffect.item.amount, Is.EqualTo(2), "Both actual coins are heads at100% and feed the death effect");
            Assert.That(frames.Any(f => (f.item.kind == "attack" || f.item.kind == "counter") && (f.item.sourceInstanceId == flame.instanceId || f.item.targetInstanceId == flame.instanceId)), Is.False);
            Assert.That(deathEffect.players[1].units.Single(u => u.instanceId == saint.instanceId).damage, Is.Zero, "The actual current battle opponent is exempt even without a strike event");
            Assert.That(deathEffect.players[1].units.Single(u => u.instanceId == other.instanceId).damage, Is.EqualTo(2));
            BattleEngine.Validate(e.State);
        }
    }
}
