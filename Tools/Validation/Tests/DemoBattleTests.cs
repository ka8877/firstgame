using System;
using System.Linq;
using FantasyCards.Core;
using NUnit.Framework;

namespace FirstGame.Validation
{
    [TestFixture]
    public sealed class DemoBattleTests
    {
        [Test] public void FourStartersAreLegalDistinctPlayableDecksAndCoverAllNewEffects()
        {
            Assert.That(DemoCatalog.Leaders.Select(x => x.id).Distinct().Count(), Is.EqualTo(4));
            Assert.That(DemoCatalog.StarterDecks.Select(x => x.id).Distinct().Count(), Is.EqualTo(4));
            Assert.That(DemoCatalog.Encounters.Select(x => x.deckId).Distinct().Count(), Is.EqualTo(3));
            foreach (var deck in DemoCatalog.StarterDecks)
            {
                DemoCatalog.ValidatePlayableDeck(deck.cards);
                Assert.That(deck.cards.Length, Is.EqualTo(24));
                Assert.That(deck.cards.GroupBy(x => x).Max(g => g.Count()), Is.LessThanOrEqualTo(4));
                Assert.That(deck.cards.Count(id => CardCatalog.Get(id).kind == CardKind.Terrain), Is.EqualTo(8));
                Assert.That(DemoCatalog.GetLeader(deck.recommendedLeaderId), Is.Not.Null);
                var copy = deck.CopyCards(); copy.Clear(); Assert.That(deck.cards.Length, Is.EqualTo(24));
            }
            Assert.That(DemoCatalog.StarterDecks.Select(x => string.Join("|", x.cards.OrderBy(id => id))).Distinct().Count(), Is.EqualTo(4));
            Assert.That(CardCatalog.All.Select(x => x.id).Distinct().Count(), Is.EqualTo(40));
            Assert.That(CardCatalog.All.Where(c => !CardCatalog.RequiresUnitTarget(c) && c.kind == CardKind.Spell).Select(c => c.effect),
                Is.EquivalentTo(new[] { Effect.Draw, Effect.Resource, Effect.Sweep }));
        }

        [TestCase("forest")] [TestCase("knights")] [TestCase("tides")] [TestCase("stars")]
        public void EveryStarterCanCompleteAndRestoreAllThreeTrials(string deckId)
        {
            var starter = DemoCatalog.GetDeck(deckId);
            for (int stage = 0; stage < 3; stage++)
            for (uint seed = 1; seed <= 3; seed++)
            {
                var live = DemoCatalog.CreateBattle(seed, deckId, starter.recommendedLeaderId, stage);
                Assert.That(live.Mulligan(0, live.State.players[0].hand.Take(2).Select(c => c.instanceId).ToArray(), out var error), Is.True, error);
                var restored = new BattleEngine(FieldJson.Decode<BattleState>(FieldJson.Encode(live.State)));
                int actions = 0;
                while (live.State.phase != BattlePhase.Complete && actions++ < 2000)
                {
                    int actor = live.State.phase == BattlePhase.BlockResponse ? 1 - live.State.activePlayer : live.State.activePlayer;
                    Assert.That(BattleAi.Step(live, actor, out error), Is.True, error);
                    Assert.That(BattleAi.Step(restored, actor, out error), Is.True, error);
                    BattleEngine.Validate(live.State);
                    Assert.That(FieldJson.Encode(restored.State), Is.EqualTo(FieldJson.Encode(live.State)), deckId + "/" + stage + "/" + seed + " action " + actions);
                }
                Assert.That(live.State.phase, Is.EqualTo(BattlePhase.Complete));
                Assert.That(live.State.winner, Is.InRange(0, 2));
                Assert.That(actions, Is.LessThan(2000));
                TestContext.WriteLine(deckId + " trial " + stage + " seed " + seed + ": winner=" + live.State.winner + ", turn=" + live.State.turn + ", actions=" + actions);
            }
        }

        [Test] public void EveryLeaderCanBeSelectedIndependentlyOfStarterDeck()
        {
            foreach (var deck in DemoCatalog.StarterDecks)
            foreach (var leader in DemoCatalog.Leaders)
            {
                var engine = DemoCatalog.CreateBattle(91, deck.id, leader.id, 2);
                Assert.That(engine.State.players[0].leaderId, Is.EqualTo(leader.id));
                Assert.That(engine.State.players[0].deckId, Is.EqualTo(deck.id));
                Assert.That(engine.State.players[1].leaderId, Is.EqualTo("noa"));
                Assert.That(engine.State.encounterIndex, Is.EqualTo(2));
                BattleEngine.Validate(engine.State);
            }
        }

        [Test] public void MulliganReturnsRejectedCardsAfterDrawingAndSurvivesWholeStateJson()
        {
            var engine = DemoCatalog.CreateBattle(74, "forest", "lyria");
            var restored = new BattleEngine(FieldJson.Decode<BattleState>(FieldJson.Encode(engine.State)));
            var selected = engine.State.players[0].hand.Take(3).Select(c => c.instanceId).ToArray();
            uint previousRandom = engine.State.randomState;
            Assert.That(engine.Mulligan(0, selected, out var error), Is.True, error);
            Assert.That(restored.Mulligan(0, selected.Reverse().ToArray(), out error), Is.True, error);
            Assert.That(FieldJson.Encode(restored.State), Is.EqualTo(FieldJson.Encode(engine.State)));
            Assert.That(engine.State.players[0].hand.Select(c => c.instanceId).Intersect(selected), Is.Empty);
            Assert.That(engine.State.players[0].deck.Count(c => selected.Contains(c.instanceId)), Is.EqualTo(3));
            Assert.That(engine.State.players[0].hand.Count, Is.EqualTo(5));
            Assert.That(engine.State.players[0].deck.Count, Is.EqualTo(19));
            Assert.That(engine.State.randomState, Is.Not.EqualTo(previousRandom));
            Assert.That(engine.State.openingHandOpen, Is.False);
            Assert.That(engine.State.players[0].mulliganUsed, Is.True);
            BattleEngine.Validate(engine.State);
            var json = FieldJson.Encode(engine.State);
            Assert.That(engine.Mulligan(0, new int[0], out _), Is.False);
            Assert.That(FieldJson.Encode(engine.State), Is.EqualTo(json));
        }

        [TestCase("duplicate")] [TestCase("unknown")] [TestCase("null")] [TestCase("opponent")]
        public void InvalidMulliganDoesNotMutateCardsEventsOrRng(string action)
        {
            var engine = DemoCatalog.CreateBattle(74, "stars", "noa");
            int first = engine.State.players[0].hand[0].instanceId;
            var json = FieldJson.Encode(engine.State);
            bool result;
            if (action == "duplicate") result = engine.Mulligan(0, new[] { first, first }, out _);
            else if (action == "unknown") result = engine.Mulligan(0, new[] { 9999 }, out _);
            else if (action == "null") result = engine.Mulligan(0, null, out _);
            else result = engine.Mulligan(1, new int[0], out _);
            Assert.That(result, Is.False);
            Assert.That(FieldJson.Encode(engine.State), Is.EqualTo(json));
        }

        [Test] public void KeepingOpeningHandConsumesNoRandomnessAndFirstActionClosesSelection()
        {
            var engine = new BattleEngine(42); string hand = string.Join(",", engine.State.players[0].hand.Select(c => c.instanceId));
            uint random = engine.State.randomState;
            Assert.That(engine.Mulligan(0, new int[0], out _), Is.True);
            Assert.That(string.Join(",", engine.State.players[0].hand.Select(c => c.instanceId)), Is.EqualTo(hand));
            Assert.That(engine.State.randomState, Is.EqualTo(random));
            var direct = new BattleEngine(42);
            var terrain = direct.State.players[0].hand.First(c => CardCatalog.Get(c.definitionId).kind == CardKind.Terrain);
            Assert.That(direct.Play(0, terrain.instanceId, -1, out _), Is.True);
            Assert.That(direct.Mulligan(0, new int[0], out _), Is.False);
            BattleEngine.Validate(direct.State);
        }

        [TestCase("lyria", "forest")] [TestCase("kael", "knights")]
        [TestCase("serin", "tides")] [TestCase("noa", "stars")]
        public void LeaderSkillPaysOnceHasItsRealEffectAndRestoresUsage(string leaderId, string deckId)
        {
            var engine = DemoCatalog.CreateBattle(23, deckId, leaderId);
            var p = engine.State.players[0]; p.dawn = p.tide = 6;
            UnitState unit = null;
            if (leaderId == "kael") unit = InstallUnit(engine, 0, "dawn_guard", 0);
            if (leaderId == "serin") { p.health = 12; unit = InstallUnit(engine, 0, "tide_oracle", 0); unit.damage = 2; }
            if (leaderId == "lyria") unit = InstallUnit(engine, 1, "dawn_sprite", 0);
            int handBefore = p.hand.Count, deckBefore = p.deck.Count;
            uint random = engine.State.randomState;
            Assert.That(engine.UseLeaderSkill(0, out var error), Is.True, error);
            Assert.That(p.lastLeaderSkillTurn, Is.EqualTo(1)); Assert.That(p.leaderSkillUses, Is.EqualTo(1));
            Assert.That(p.Resource(DemoCatalog.GetLeader(leaderId).skillElement), Is.EqualTo(4));
            Assert.That(engine.State.randomState, Is.EqualTo(random));
            if (leaderId == "lyria") Assert.That(engine.State.players[1].units, Is.Empty);
            if (leaderId == "kael") Assert.That(unit.bonusPower, Is.EqualTo(1));
            if (leaderId == "serin") { Assert.That(p.health, Is.EqualTo(15)); Assert.That(unit.damage, Is.EqualTo(1)); }
            if (leaderId == "noa") { Assert.That(p.hand.Count, Is.EqualTo(handBefore + 1)); Assert.That(p.deck.Count, Is.EqualTo(deckBefore - 1)); Assert.That(p.dawn, Is.EqualTo(7)); }
            BattleEngine.Validate(engine.State);
            string json = FieldJson.Encode(engine.State);
            Assert.That(engine.UseLeaderSkill(0, out _), Is.False); Assert.That(FieldJson.Encode(engine.State), Is.EqualTo(json));
            var restored = new BattleEngine(FieldJson.Decode<BattleState>(json));
            Assert.That(restored.UseLeaderSkill(0, out _), Is.False);
            Assert.That(FieldJson.Encode(restored.State), Is.EqualTo(json));
            Assert.That(engine.EndTurn(0, out _), Is.True); Assert.That(engine.EndTurn(1, out _), Is.True);
            Assert.That(engine.UseLeaderSkill(0, out error), Is.True, error);
            Assert.That(p.leaderSkillUses, Is.EqualTo(2)); Assert.That(p.lastLeaderSkillTurn, Is.EqualTo(3));
            BattleEngine.Validate(engine.State);
        }

        [Test] public void LeaderSkillInvalidPhaseResourcesAndMissingTargetsDoNotPay()
        {
            var engine = DemoCatalog.CreateBattle(10, "knights", "kael");
            var before = FieldJson.Encode(engine.State);
            Assert.That(engine.UseLeaderSkill(0, out _), Is.False);
            Assert.That(engine.UseLeaderSkill(1, out _), Is.False);
            Assert.That(FieldJson.Encode(engine.State), Is.EqualTo(before));
            engine.State.players[0].dawn = 5; before = FieldJson.Encode(engine.State);
            Assert.That(engine.UseLeaderSkill(0, out _), Is.False); Assert.That(FieldJson.Encode(engine.State), Is.EqualTo(before));
            InstallUnit(engine, 0, "dawn_guard", 0); Assert.That(engine.BeginCombat(0, out _), Is.True);
            before = FieldJson.Encode(engine.State);
            Assert.That(engine.UseLeaderSkill(0, out _), Is.False); Assert.That(FieldJson.Encode(engine.State), Is.EqualTo(before));
        }

        [Test] public void NewTargetlessSpellsAndInnateUnitTraitsResolveAndPreserveCards()
        {
            var engine = DemoCatalog.CreateBattle(44, "stars", "noa", 1);
            var p = engine.State.players[0]; p.dawn = p.tide = 8;
            var caster = PutInHand(engine, 0, "tide_astrologer");
            Assert.That(engine.Play(0, caster.instanceId, 0, out var error), Is.True, error);
            Assert.That(p.AtLane(0).extraCoins, Is.EqualTo(1));
            var armor = PutInHand(engine, 0, "tide_oracle");
            Assert.That(engine.Play(0, armor.instanceId, 1, out error), Is.True, error);
            Assert.That(p.AtLane(1).armor, Is.EqualTo(1));
            p.tide = 8;
            var draw = PutInHand(engine, 0, "tide_insight"); int hand = p.hand.Count, deck = p.deck.Count;
            Assert.That(engine.Play(0, draw.instanceId, -1, out error), Is.True, error);
            Assert.That(p.hand.Count, Is.EqualTo(hand + 1)); Assert.That(p.deck.Count, Is.EqualTo(deck - 2));
            var resource = PutInHand(engine, 0, "dawn_radiance"); int dawn = p.dawn;
            Assert.That(engine.Play(0, resource.instanceId, -1, out error), Is.True, error);
            Assert.That(p.dawn, Is.EqualTo(dawn + 2));
            BattleEngine.Validate(engine.State);

            var sweepEngine = DemoCatalog.CreateBattle(19, "tides", "serin", 1);
            sweepEngine.State.players[0].tide = 6;
            var enemyA = InstallUnit(sweepEngine, 1, "tide_apprentice", 0);
            var enemyB = InstallUnit(sweepEngine, 1, "tide_oracle", 1); enemyB.armor = 1;
            var sweep = PutInHand(sweepEngine, 0, "tide_tempest");
            Assert.That(sweepEngine.Play(0, sweep.instanceId, -1, out error), Is.True, error);
            Assert.That(sweepEngine.State.players[1].AtLane(0), Is.Null);
            Assert.That(enemyB.damage, Is.EqualTo(1));
            BattleEngine.Validate(sweepEngine.State);
        }

        [TestCase("leader")] [TestCase("deck")] [TestCase("composition")]
        [TestCase("skill-turn")] [TestCase("skill-count")] [TestCase("mulligan")] [TestCase("encounter")]
        public void CorruptDemoSaveNeverChangesTheLiveBattle(string damage)
        {
            var live = DemoCatalog.CreateBattle(13, "forest", "lyria"); string before = FieldJson.Encode(live.State);
            var broken = FieldJson.Decode<BattleState>(before);
            switch (damage)
            {
                case "leader": broken.players[0].leaderId = "unknown"; break;
                case "deck": broken.players[0].deckId = "unknown"; break;
                case "composition": broken.players[0].hand[0].definitionId = "tide_crown"; break;
                case "skill-turn": broken.players[0].lastLeaderSkillTurn = 1; break;
                case "skill-count": broken.players[0].leaderSkillUses = 1; broken.players[0].lastLeaderSkillTurn = 1; break;
                case "mulligan": broken.players[0].mulliganUsed = true; break;
                case "encounter": broken.encounterIndex = 2; break;
            }
            Assert.Throws<ArgumentException>(() => new BattleEngine(broken));
            Assert.That(FieldJson.Encode(live.State), Is.EqualTo(before));
        }

        [Test] public void PreviousContentIsRejectedWithoutRewritingTheProvidedState()
        {
            var old = new BattleEngine(8).State.Copy(); old.schemaVersion = 1; old.contentVersion = "starlight-0.1.0";
            string json = FieldJson.Encode(old);
            Assert.Throws<ArgumentException>(() => new BattleEngine(old));
            Assert.That(FieldJson.Encode(old), Is.EqualTo(json));
        }

        static CardInstance PutInHand(BattleEngine engine, int player, string definition)
        {
            var p = engine.State.players[player]; var card = p.hand.Concat(p.deck).First(c => c.definitionId == definition);
            p.deck.Remove(card); if (!p.hand.Contains(card)) p.hand.Add(card); return card;
        }
        static UnitState InstallUnit(BattleEngine engine, int player, string definition, int lane)
        {
            var card = PutInHand(engine, player, definition); var p = engine.State.players[player]; p.hand.Remove(card);
            var unit = new UnitState { instanceId = card.instanceId, definitionId = definition, lane = lane };
            p.units.Add(unit); return unit;
        }
    }
}
