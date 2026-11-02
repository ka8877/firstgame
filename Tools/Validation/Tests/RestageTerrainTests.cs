using System;
using System.Linq;
using System.Text.RegularExpressions;
using FantasyCards.Core;
using FantasyCards.Tests;
using NUnit.Framework;

namespace FirstGame.Validation
{
    public sealed class RestageTerrainTests
    {
        static BattleEngine Battle() => ClassicFixtures.Battle(rules: new RuleSet { baseCoins = 0, defenderStrikesBack = false });
        static CardInstance Land(BattleEngine battle, string name)
        {
            var card = ClassicFixtures.Hand(battle, 0, name);
            // Several lands in one preparation are legal when previously saved
            // LP exists. Preserve every original card/ID in this fixture.
            battle.State.players[0].levelPoints = 10;
            Assert.That(battle.Play(0, card.instanceId, -1, out var reason), Is.True, reason);
            return card;
        }

        [Test] public void UnusedLandDisposalRemovesItsActualProductionAndPreservesLpCardsAndRandomness()
        {
            var battle = Battle(); var land = Land(battle, "묘지"); var player = battle.State.players[0];
            uint random = battle.State.randomState; int lp = player.levelPoints;
            Assert.That(player.Resource(Element.Darkness), Is.EqualTo(1)); Assert.That(player.genericSoul, Is.EqualTo(1));
            battle.BeginPlaybackCapture(); Assert.That(battle.DiscardField(0, land.instanceId, out var reason), Is.True, reason);
            var frame = battle.EndPlaybackCapture().Single();
            Assert.That(player.Resource(Element.Darkness), Is.Zero); Assert.That(player.genericSoul, Is.Zero);
            Assert.That(player.levelPoints, Is.EqualTo(lp)); Assert.That(battle.State.randomState, Is.EqualTo(random));
            Assert.That(player.discard.Single().instanceId, Is.EqualTo(land.instanceId));
            Assert.That(frame.players[0].terrain, Is.Empty); Assert.That(frame.players[0].discard.Single().instanceId, Is.EqualTo(land.instanceId));
            Assert.That(ClassicFixtures.AllCards(player), Is.EqualTo(50)); BattleEngine.Validate(battle.State);
        }

        [Test] public void ColoredAndTotalPaymentLocksOnlyUsedTerrainAndCannotKeepDiscardedProduction()
        {
            var battle = Battle(); var first = Land(battle, "묘지"); var spare = Land(battle, "죽음의호수");
            var imp = ClassicFixtures.Hand(battle, 0, "데스핸드");
            Assert.That(battle.Play(0, imp.instanceId, 0, out var reason), Is.True, reason);
            Assert.That(first.terrainSoulUsed, Is.True); Assert.That(spare.terrainSoulUsed, Is.False);
            string before = FieldJson.Encode(battle.State);
            Assert.That(battle.DiscardField(0, first.instanceId, out reason), Is.False); Assert.That(reason, Does.Contain("사용"));
            Assert.That(FieldJson.Encode(battle.State), Is.EqualTo(before));
            Assert.That(battle.DiscardField(0, spare.instanceId, out reason), Is.True, reason);
            Assert.That(battle.State.players[0].genericSoul, Is.Zero);
            Assert.That(battle.State.players[0].Resource(Element.Darkness), Is.Zero);
            var restored = new BattleEngine(FieldJson.Decode<BattleState>(FieldJson.Encode(battle.State)));
            Assert.That(restored.State.players[0].terrain.Single().terrainSoulUsed, Is.True);
            Assert.That(restored.CanDiscardField(0, first.instanceId, out _), Is.False); BattleEngine.Validate(restored.State);
        }

        [Test] public void NonTerrainSoulAndCappedProductionAreNotRemovedWithUnusedLand()
        {
            var battle = Battle(); ClassicFixtures.Souls(battle, 0, battle.State.rules.maximumResource);
            var land = Land(battle, "묘지");
            Assert.That(land.remainingColoredProduction, Is.Zero); Assert.That(land.remainingGenericProduction, Is.Zero);
            var imp = ClassicFixtures.Hand(battle, 0, "임프");
            Assert.That(battle.Play(0, imp.instanceId, 0, out _), Is.True);
            Assert.That(land.terrainSoulUsed, Is.False);
            int total = battle.State.players[0].genericSoul, colored = battle.State.players[0].Resource(Element.Darkness);
            Assert.That(battle.DiscardField(0, land.instanceId, out _), Is.True);
            Assert.That(battle.State.players[0].genericSoul, Is.EqualTo(total));
            Assert.That(battle.State.players[0].Resource(Element.Darkness), Is.EqualTo(colored)); BattleEngine.Validate(battle.State);
        }

        [Test] public void RealSoulRefreshEstablishesLegacyLandUsageAndUpkeepLocksItsPayingTerrain()
        {
            var battle = Battle(); var player = battle.State.players[0];
            var land = ClassicFixtures.Extract(battle, 0, "묘지"); player.terrain.Add(land);
            Assert.That(battle.CanDiscardField(0, land.instanceId, out _), Is.False);
            // No unit means no upkeep; only the next real soul-cost stage can
            // establish an old land's fresh unspent production.
            ClassicFixtures.Resolve(battle);
            Assert.That(land.hasSoulProductionRecord, Is.True); Assert.That(land.terrainSoulUsed, Is.False);
            Assert.That(battle.CompletePreparation(1, out _), Is.True);
            Assert.That(battle.CanDiscardField(0, land.instanceId, out _), Is.True);
            ClassicFixtures.Unit(battle, 0, "구울", 0);
            Assert.That(CardCatalog.Get(ClassicFixtures.Id("구울")).genericCost, Is.GreaterThan(0));
            Assert.That(battle.CompletePreparation(0, out _), Is.True);
            Assert.That(land.terrainSoulUsed, Is.True, "Freshly restored soul pays real maintenance before preparation begins.");
            Assert.That(battle.CanDiscardField(0, land.instanceId, out _), Is.False); BattleEngine.Validate(battle.State);
        }

        [Test] public void OptionalTerrainRecordsAreDeepCopiedAndAbsentLegacyJsonContinuesWithIdenticalRandomness()
        {
            var battle = Battle(); var land = Land(battle, "묘지");
            var copied = battle.State.Copy(); copied.players[0].terrain.Single().remainingGenericProduction = 0;
            Assert.That(land.remainingGenericProduction, Is.EqualTo(1));
            string json = FieldJson.Encode(battle.State);
            string legacy = Regex.Replace(json, ",?\"(?:hasSoulProductionRecord|terrainSoulUsed|remainingColoredProduction|remainingGenericProduction)\":(?:true|false|[0-9]+)", "");
            var old = new BattleEngine(FieldJson.Decode<BattleState>(legacy));
            Assert.That(old.State.players[0].terrain.Single().hasSoulProductionRecord, Is.False);
            Assert.That(old.CanDiscardField(0, land.instanceId, out _), Is.False);
            ClassicFixtures.Resolve(battle); ClassicFixtures.Resolve(old);
            Assert.That(FieldJson.Encode(old.State), Is.EqualTo(FieldJson.Encode(battle.State)));
            var corrupt = battle.State.Copy(); corrupt.players[0].terrain.Single().remainingGenericProduction = 99;
            Assert.Throws<ArgumentException>(() => new BattleEngine(corrupt));
        }
    }
}
