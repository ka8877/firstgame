using System;
using System.Linq;
using FantasyCards.Core;
using NUnit.Framework;

namespace FirstGame.Validation
{
    [TestFixture]
    public sealed class GameInvariantTests
    {
        [TestCase(DamageResolutionOrder.Simultaneous, 0)]
        [TestCase(DamageResolutionOrder.AttackerFirst, 1)]
        public void LethalDuelHonorsConfiguredDamageOrder(DamageResolutionOrder order, int survivingAttackers)
        {
            var engine = PreparedDuel(new RuleSet { baseCoins = 0, damageOrder = order });
            var attacker = engine.State.players[0].units.Single();
            var defender = engine.State.players[1].units.Single();
            attacker.damage = CardCatalog.Get(attacker.definitionId).health - 1;
            defender.damage = CardCatalog.Get(defender.definitionId).health - 1;
            Assert.That(engine.DeclareAttack(0, attacker.instanceId, 0, out var error), Is.True, error);
            Assert.That(engine.ResolveBlock(1, -1, out error), Is.True, error);
            Assert.That(engine.State.players[0].units.Count, Is.EqualTo(survivingAttackers));
            Assert.That(engine.State.players[1].units, Is.Empty);
            BattleEngine.Validate(engine.State);
        }

        [TestCase(CoinResolutionOrder.AttackerFirst, 0, 1)]
        [TestCase(CoinResolutionOrder.DefenderFirst, 1, 0)]
        public void CoinOrderAssignsNextRngResultsToConfiguredActors(CoinResolutionOrder order, int firstActor, int secondActor)
        {
            var engine = PreparedDuel(new RuleSet { baseCoins = 1, coinOrder = order });
            uint expectedRandom = engine.State.randomState;
            int firstCoin = StableRandom.Range(ref expectedRandom, 2);
            int secondCoin = StableRandom.Range(ref expectedRandom, 2);
            int start = engine.State.events.Count;
            Assert.That(engine.DeclareAttack(0, engine.State.players[0].units.Single().instanceId, 0, out var error), Is.True, error);
            Assert.That(engine.ResolveBlock(1, -1, out error), Is.True, error);
            var coins = engine.State.events.Skip(start).Where(e => e.kind == "coin").ToArray();
            Assert.That(coins.Select(e => e.actor), Is.EqualTo(new[] { firstActor, secondActor }));
            Assert.That(coins.Select(e => e.amount), Is.EqualTo(new[] { firstCoin, secondCoin }));
            Assert.That(engine.State.randomState, Is.EqualTo(expectedRandom));
            BattleEngine.Validate(engine.State);
        }

        [Test] public void DisabledCounterattackDoesNotRollDefenderCoinsOrDamageAttacker()
        {
            var engine = PreparedDuel(new RuleSet { defenderStrikesBack = false, baseCoins = 2 });
            int attackerId = engine.State.players[0].units.Single().instanceId;
            int start = engine.State.events.Count;
            Assert.That(engine.DeclareAttack(0, attackerId, 0, out var error), Is.True, error);
            Assert.That(engine.ResolveBlock(1, -1, out error), Is.True, error);
            Assert.That(engine.State.players[0].units.Single().damage, Is.Zero);
            Assert.That(engine.State.events.Skip(start).Where(e => e.kind == "coin").Select(e => e.actor), Is.EqualTo(new[] { 0, 0 }));
            BattleEngine.Validate(engine.State);
        }

        [TestCase(1, 0, true)] [TestCase(3, 2, false)] [TestCase(6, 4, true)]
        public void NonDefaultRulesProduceValidTerminatingSerializableBattles(int lanes, int coins, bool counterattack)
        {
            var rules = new RuleSet { deckSize = 12, startingHealth = 7, openingHand = 3, handLimit = 5,
                unitLanes = lanes, baseCoins = coins, defenderStrikesBack = counterattack,
                terrainLimit = 4, terrainPerTurn = 2, maximumResource = 6, maximumTurns = 14,
                coinOrder = CoinResolutionOrder.DefenderFirst, damageOrder = DamageResolutionOrder.AttackerFirst };
            var deck = CardCatalog.DefaultDeck().Take(rules.deckSize).ToList();
            var engine = new BattleEngine(170u, rules, deck, deck);
            int steps = 0;
            while (engine.State.phase != BattlePhase.Complete && steps++ < 300)
            {
                int actor = engine.State.phase == BattlePhase.BlockResponse ? 1 - engine.State.activePlayer : engine.State.activePlayer;
                Assert.That(BattleAi.Step(engine, actor, out var error), Is.True, error);
                BattleEngine.Validate(engine.State);
                var restored = new BattleEngine(FieldJson.Decode<BattleState>(FieldJson.Encode(engine.State)));
                Assert.That(FieldJson.Encode(restored.State), Is.EqualTo(FieldJson.Encode(engine.State)));
            }
            Assert.That(engine.State.phase, Is.EqualTo(BattlePhase.Complete));
            Assert.That(engine.State.turn, Is.LessThanOrEqualTo(rules.maximumTurns + 1));
        }

        [TestCase("pending-target")] [TestCase("negative-fatigue")]
        [TestCase("negative-armor")] [TestCase("event-sequence")]
        [TestCase("per-player-card-count")] [TestCase("unknown-rule-order")]
        public void MalformedSerializedBattleIsRejectedWithoutMutatingLiveState(string corruption)
        {
            var live = PreparedDuel(new RuleSet());
            Assert.That(live.DeclareAttack(0, live.State.players[0].units.Single().instanceId, 0, out _), Is.True);
            var originalJson = FieldJson.Encode(live.State);
            var damaged = FieldJson.Decode<BattleState>(originalJson);
            switch (corruption)
            {
                case "pending-target": damaged.pendingTargetLane = 2; break; // Empty target lane.
                case "negative-fatigue": damaged.players[1].fatigue = -1; break;
                case "negative-armor": damaged.players[0].units[0].armor = -1; break;
                case "event-sequence": damaged.events[0].sequence = 99; break;
                case "per-player-card-count":
                    var stolen = damaged.players[0].deck[0]; damaged.players[0].deck.RemoveAt(0); damaged.players[1].deck.Add(stolen); break;
                case "unknown-rule-order": damaged.rules.coinOrder = (CoinResolutionOrder)99; break;
            }
            Assert.Throws<ArgumentException>(() => new BattleEngine(damaged));
            Assert.That(FieldJson.Encode(live.State), Is.EqualTo(originalJson));
        }

        static BattleEngine PreparedDuel(RuleSet rules)
        {
            var engine = new BattleEngine(711u, rules);
            for (int player = 0; player < 2; player++)
            {
                var state = engine.State.players[player];
                var card = state.deck.Concat(state.hand).First(c => CardCatalog.Get(c.definitionId).kind == CardKind.Unit);
                state.deck.Remove(card); state.hand.Remove(card);
                state.units.Add(new UnitState { instanceId = card.instanceId, definitionId = card.definitionId, lane = 0 });
            }
            Assert.That(engine.BeginCombat(0, out var error), Is.True, error);
            BattleEngine.Validate(engine.State);
            return engine;
        }
    }
}
