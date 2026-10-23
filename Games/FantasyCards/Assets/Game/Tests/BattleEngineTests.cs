using System;
using System.Linq;
using FantasyCards.Core;
using NUnit.Framework;

namespace FantasyCards.Tests
{
    public sealed class BattleEngineTests
    {
        [Test] public void CatalogAndDeckHaveSeparateCounts()
        {
            Assert.AreEqual(16, CardCatalog.All.Length);
            Assert.AreEqual(4, CardCatalog.All.Count(x => x.kind == CardKind.Terrain));
            Assert.AreEqual(6, CardCatalog.All.Count(x => x.kind == CardKind.Unit));
            Assert.AreEqual(3, CardCatalog.All.Count(x => x.kind == CardKind.Spell));
            Assert.AreEqual(3, CardCatalog.All.Count(x => x.kind == CardKind.Equipment));
            Assert.AreEqual(24, CardCatalog.DefaultDeck().Count);
        }
        [Test] public void TerrainProducesResourceAndCanOnlyBePlayedOnce()
        {
            var e = new BattleEngine(12); var p = e.State.players[0];
            var terrain = p.hand.Where(c => CardCatalog.Get(c.definitionId).kind == CardKind.Terrain).ToArray();
            Assert.True(e.Play(0, terrain[0].instanceId, 0, out _));
            Assert.AreEqual(1, p.Resource(CardCatalog.Get(terrain[0].definitionId).element));
            uint before = e.State.randomState;
            Assert.False(e.Play(0, terrain[1].instanceId, 0, out _));
            Assert.AreEqual(before, e.State.randomState);
            e.EndTurn(0, out _); e.EndTurn(1, out _);
            Assert.AreEqual(2, p.Resource(CardCatalog.Get(terrain[0].definitionId).element));
        }
        [Test] public void InvalidPlayDoesNotPayOrRemoveCard()
        {
            var e = new BattleEngine(1); var p = e.State.players[0];
            var unit = p.deck.Concat(p.hand).First(c => CardCatalog.Get(c.definitionId).kind == CardKind.Unit);
            p.deck.Remove(unit); if (!p.hand.Contains(unit)) p.hand.Add(unit);
            var before = p.hand.Count;
            Assert.False(e.Play(0, unit.instanceId, 0, out _));
            Assert.AreEqual(before, p.hand.Count); Assert.AreEqual(0, p.dawn + p.tide);
        }
        [Test] public void UnitsRemainAndBecomeReadyOnTheirNextTurn()
        {
            var e = new BattleEngine(91); var p = e.State.players[0]; p.dawn = 8; p.tide = 8;
            var c = p.deck.Concat(p.hand).First(x => CardCatalog.Get(x.definitionId).kind == CardKind.Unit);
            p.deck.Remove(c); if (!p.hand.Contains(c)) p.hand.Add(c);
            Assert.True(e.Play(0, c.instanceId, 1, out _)); Assert.True(p.AtLane(1).summoningSick);
            e.BeginCombat(0, out _); Assert.False(e.DeclareAttack(0, c.instanceId, -1, out _));
            e.EndTurn(0, out _); e.EndTurn(1, out _);
            Assert.False(p.AtLane(1).summoningSick); Assert.AreEqual(c.definitionId, p.AtLane(1).definitionId);
        }
        [Test] public void SaveDuringBlockRestoresNextCoinAndEveryEvent()
        {
            var engine = ReachPendingCombat(44); var restored = new BattleEngine(engine.State.Copy());
            uint rng = engine.State.randomState;
            Assert.False(engine.ResolveBlock(engine.State.activePlayer, -1, out _));
            Assert.AreEqual(rng, engine.State.randomState);
            int defender = 1 - engine.State.activePlayer;
            int guard = BattleAi.ChooseBlocker(engine.State, defender);
            Assert.True(engine.ResolveBlock(defender, guard, out _)); Assert.True(restored.ResolveBlock(defender, guard, out _));
            Assert.AreEqual(engine.State.randomState, restored.State.randomState);
            Assert.AreEqual(string.Join("|", engine.State.events.Select(e => e.sequence + e.kind + e.text)), string.Join("|", restored.State.events.Select(e => e.sequence + e.kind + e.text)));
            Assert.AreEqual(engine.State.players[0].health, restored.State.players[0].health);
            Assert.AreEqual(engine.State.players[1].health, restored.State.players[1].health);
            Assert.AreEqual(engine.State.players[0].units.Count, restored.State.players[0].units.Count);
            BattleEngine.Validate(restored.State);
        }
#if UNITY_EDITOR
        // This must run in Unity: System.Text.Json in the external harness cannot
        // establish JsonUtility's behavior for the actual nested save payload.
        [TestCase(1u)] [TestCase(44u)] [TestCase(188u)]
        public void UnityJsonSaveDuringBlockResumesSameCoinsEventsAndRemainingBattle(uint seed)
        {
            var source = ReachPendingCombat(seed);
            var json = UnityEngine.JsonUtility.ToJson(source.State);
            var decoded = UnityEngine.JsonUtility.FromJson<BattleState>(json);
            Assert.That(decoded, Is.Not.Null);
            var restored = new BattleEngine(decoded);
            Assert.That(restored.State.phase, Is.EqualTo(BattlePhase.BlockResponse));
            Assert.That(UnityEngine.JsonUtility.ToJson(restored.State), Is.EqualTo(json),
                "Unity serialization must preserve every pending combat field, deck order and event.");

            // A response from the attacking player is illegal and must leave even RNG unchanged.
            Assert.That(source.ResolveBlock(source.State.activePlayer, -1, out _), Is.False);
            Assert.That(restored.ResolveBlock(restored.State.activePlayer, -1, out _), Is.False);
            Assert.That(UnityEngine.JsonUtility.ToJson(source.State), Is.EqualTo(json));
            Assert.That(UnityEngine.JsonUtility.ToJson(restored.State), Is.EqualTo(json));

            int defender = 1 - source.State.activePlayer;
            int blocker = BattleAi.ChooseBlocker(source.State, defender);
            Assert.That(BattleAi.ChooseBlocker(restored.State, defender), Is.EqualTo(blocker));
            int priorEvents = source.State.events.Count;
            Assert.That(source.ResolveBlock(defender, blocker, out var error), Is.True, error);
            Assert.That(restored.ResolveBlock(defender, blocker, out error), Is.True, error);
            var coins = source.State.events.Skip(priorEvents).Where(e => e.kind == "coin").ToArray();
            Assert.That(coins, Is.Not.Empty, "The first resumed action must really consume coin RNG.");
            Assert.That(restored.State.events.Skip(priorEvents).Where(e => e.kind == "coin")
                .Select(e => e.sequence + ":" + e.actor + ":" + e.amount + ":" + e.text),
                Is.EqualTo(coins.Select(e => e.sequence + ":" + e.actor + ":" + e.amount + ":" + e.text)));
            Assert.That(UnityEngine.JsonUtility.ToJson(restored.State),
                Is.EqualTo(UnityEngine.JsonUtility.ToJson(source.State)), "First response after Unity JSON restore diverged.");

            int actions = 0;
            while (source.State.phase != BattlePhase.Complete && actions++ < 2000)
            {
                int actor = source.State.phase == BattlePhase.BlockResponse
                    ? 1 - source.State.activePlayer : source.State.activePlayer;
                Assert.That(BattleAi.Step(source, actor, out error), Is.True, error);
                Assert.That(BattleAi.Step(restored, actor, out error), Is.True, error);
                BattleEngine.Validate(restored.State);
                Assert.That(UnityEngine.JsonUtility.ToJson(restored.State),
                    Is.EqualTo(UnityEngine.JsonUtility.ToJson(source.State)), "Resumed action " + actions);
            }
            Assert.That(source.State.phase, Is.EqualTo(BattlePhase.Complete));
            Assert.That(actions, Is.LessThan(2000));
        }
#endif
        [Test] public void WholeSeededBattleIsDeterministicAndTerminates()
        {
            for (uint seed = 1; seed <= 20; seed++)
            {
                var a = new BattleEngine(seed); var b = new BattleEngine(seed);
                int actions = 0;
                while (a.State.phase != BattlePhase.Complete && actions++ < 2000)
                {
                    int actor = a.State.phase == BattlePhase.BlockResponse ? 1 - a.State.activePlayer : a.State.activePlayer;
                    Assert.True(BattleAi.Step(a, actor, out _)); Assert.True(BattleAi.Step(b, actor, out _));
                    BattleEngine.Validate(a.State);
                    Assert.AreEqual(a.State.randomState, b.State.randomState);
                }
                Assert.Less(actions, 2000); Assert.AreEqual(BattlePhase.Complete, a.State.phase);
                Assert.AreEqual(a.State.winner, b.State.winner); Assert.AreEqual(a.State.events.Count, b.State.events.Count);
                Assert.AreEqual(string.Join("|", a.State.events.Select(x => x.text)), string.Join("|", b.State.events.Select(x => x.text)));
            }
        }
        [Test] public void ConcedeAndLethalDamageReachBothResults()
        {
            var loss = new BattleEngine(6); Assert.True(loss.Concede(0)); Assert.AreEqual(1, loss.State.winner);
            var victory = new BattleEngine(6); Assert.True(victory.Concede(1)); Assert.AreEqual(0, victory.State.winner);
            Assert.False(victory.EndTurn(0, out _)); Assert.False(victory.Concede(0));
            var lethal = ReachPendingCombat(12); int defender = 1 - lethal.State.activePlayer;
            lethal.State.players[defender].health = 1; lethal.State.pendingTargetLane = -1;
            Assert.True(lethal.ResolveBlock(defender, -1, out _)); Assert.AreEqual(1 - defender, lethal.State.winner);
        }
        [Test] public void DuplicateIdsAndDifferentContentAreRejected()
        {
            var s = new BattleEngine(4).State.Copy(); s.contentVersion = "future";
            Assert.Throws<ArgumentException>(() => new BattleEngine(s)); s.contentVersion = CardCatalog.Version;
            s.players[0].hand[0].instanceId = s.players[0].hand[1].instanceId;
            Assert.Throws<ArgumentException>(() => new BattleEngine(s));
        }
        [Test] public void RuleSetChangesCoinOrderWithoutPresentationRandomness()
        {
            var a = PreparedCombat(new RuleSet { coinOrder = CoinResolutionOrder.AttackerFirst });
            var b = PreparedCombat(new RuleSet { coinOrder = CoinResolutionOrder.DefenderFirst });
            a.ResolveBlock(1, a.State.players[1].AtLane(0).instanceId, out _);
            b.ResolveBlock(1, b.State.players[1].AtLane(0).instanceId, out _);
            Assert.AreEqual(0, a.State.events.First(x => x.kind == "coin").actor);
            Assert.AreEqual(1, b.State.events.First(x => x.kind == "coin").actor);
            Assert.AreEqual(a.State.randomState, b.State.randomState);
        }
        [Test] public void RuleSetAttackerFirstDamageCancelsDefeatedDefendersRetaliation()
        {
            var simultaneous = PreparedCombat(new RuleSet { baseCoins = 0 });
            var firstStrike = PreparedCombat(new RuleSet { baseCoins = 0, damageOrder = DamageResolutionOrder.AttackerFirst });
            simultaneous.ResolveBlock(1, simultaneous.State.players[1].AtLane(0).instanceId, out _);
            firstStrike.ResolveBlock(1, firstStrike.State.players[1].AtLane(0).instanceId, out _);
            Assert.AreEqual(1, simultaneous.State.players[0].AtLane(0).damage);
            Assert.AreEqual(0, firstStrike.State.players[0].AtLane(0).damage);
            Assert.AreEqual(0, firstStrike.State.players[1].units.Count);
            BattleEngine.Validate(firstStrike.State);
        }
        [Test] public void EquipmentLimitAndSpellEffectsPersistAcrossCopy()
        {
            var e = new BattleEngine(9); var p = e.State.players[0]; p.dawn = p.tide = 10;
            var unit = InstallUnit(e, 0, "dawn_guard"); var enemy = InstallUnit(e, 1, "tide_serpent");
            Assert.True(e.Play(0, TakeCard(e, 0, "tide_mail").instanceId, 0, out _));
            Assert.True(e.Play(0, TakeCard(e, 0, "tide_charm").instanceId, 0, out _));
            int resources = p.dawn;
            Assert.False(e.Play(0, TakeCard(e, 0, "dawn_blade").instanceId, 0, out _));
            Assert.AreEqual(resources, p.dawn); Assert.AreEqual(1, unit.armor); Assert.AreEqual(1, unit.extraCoins);
            unit.damage = 2;
            Assert.True(e.Play(0, TakeCard(e, 0, "dawn_mend").instanceId, 0, out _)); Assert.AreEqual(0, unit.damage);
            Assert.True(e.Play(0, TakeCard(e, 0, "dawn_oath").instanceId, 0, out _)); Assert.AreEqual(1, unit.bonusPower);
            Assert.True(e.Play(0, TakeCard(e, 0, "tide_bolt").instanceId, 0, out _)); Assert.AreEqual(2, enemy.damage);
            var saved = new BattleEngine(e.State.Copy());
            Assert.AreEqual(2, saved.State.players[0].AtLane(0).equipment.Count);
            Assert.AreEqual(1, saved.State.players[0].AtLane(0).armor);
            Assert.AreEqual(1, saved.State.players[0].AtLane(0).extraCoins);
            Assert.AreEqual(2, saved.State.players[1].AtLane(0).damage);
        }
        [Test] public void ExhaustedBlockerIsRejectedWithoutRandomOrStateChange()
        {
            var e = PreparedCombat(new RuleSet()); var unit = e.State.players[1].AtLane(0); unit.exhausted = true;
            uint rng = e.State.randomState; int eventCount = e.State.events.Count;
            Assert.False(e.ResolveBlock(1, unit.instanceId, out _));
            Assert.AreEqual(BattlePhase.BlockResponse, e.State.phase); Assert.AreEqual(rng, e.State.randomState); Assert.AreEqual(eventCount, e.State.events.Count);
        }
        private static BattleEngine PreparedCombat(RuleSet rules)
        {
            var e = new BattleEngine(42, rules); var attacker = InstallUnit(e, 0, "dawn_lancer"); InstallUnit(e, 1, "tide_wisp");
            e.BeginCombat(0, out _); Assert.True(e.DeclareAttack(0, attacker.instanceId, -1, out _)); return e;
        }
        private static UnitState InstallUnit(BattleEngine e, int player, string definition)
        {
            var c = TakeCard(e, player, definition); var p = e.State.players[player]; p.hand.Remove(c);
            var u = new UnitState { instanceId = c.instanceId, definitionId = c.definitionId, lane = 0 }; p.units.Add(u); return u;
        }
        private static CardInstance TakeCard(BattleEngine e, int player, string definition)
        {
            var p = e.State.players[player]; var c = p.deck.Concat(p.hand).First(x => x.definitionId == definition);
            p.deck.Remove(c); if (!p.hand.Contains(c)) p.hand.Add(c); return c;
        }
        private static BattleEngine ReachPendingCombat(uint seed)
        {
            var e = new BattleEngine(seed);
            for (int action = 0; action < 300; action++)
            {
                if (e.State.phase == BattlePhase.BlockResponse) return e;
                Assert.True(BattleAi.Step(e, e.State.activePlayer, out _));
            }
            throw new Exception("AI did not reach combat.");
        }
    }
}
