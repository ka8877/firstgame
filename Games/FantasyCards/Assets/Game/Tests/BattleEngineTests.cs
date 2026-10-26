using System;
using System.Collections.Generic;
using System.Linq;
using FantasyCards.Core;
using NUnit.Framework;

namespace FantasyCards.Tests
{
    public static class ClassicFixtures
    {
        public static string Id(string name) => CardCatalog.All.Single(c => c.name == name).id;
        public static BattleEngine Battle(uint seed = 42, RuleSet rules = null)
        {
            rules = rules ?? new RuleSet();
            var pool = CardCatalog.All.Where(c => c.kind == CardKind.Terrain).Select(c => c.id)
                .Concat(CardCatalog.All.Where(c => c.kind == CardKind.Unit).OrderBy(c => c.level).Select(c => c.id)).ToList();
            var deck = pool.Take(rules.deckSize).ToList();
            var e = new BattleEngine(seed, rules, deck, deck); e.ConfirmOpeningHand(0, out _); return e;
        }
        public static CardInstance Extract(BattleEngine engine, int player, string name)
        {
            var p = engine.State.players[player]; string id = Id(name);
            var card = p.deck.Concat(p.hand).FirstOrDefault(c => c.definitionId == id);
            if (card == null)
            {
                // Build a legal custom fixture by replacing one unplayed card, preserving its stable instance.
                card = p.deck.Last(c => p.deck.Concat(p.hand).Count(x => x.definitionId == c.definitionId) == 1);
                card.definitionId = id;
            }
            p.deck.Remove(card); p.hand.Remove(card); return card;
        }
        public static UnitState Unit(BattleEngine e, int player, string name, int lane)
        {
            var c = Extract(e, player, name); var unit = new UnitState { instanceId = c.instanceId, definitionId = c.definitionId, lane = lane };
            e.State.players[player].units.Add(unit); return unit;
        }
        public static CardInstance Hand(BattleEngine e, int player, string name)
        {
            var c = Extract(e, player, name); var p = e.State.players[player];
            if (p.hand.Count >= e.State.rules.handLimit) { p.deck.Add(p.hand[0]); p.hand.RemoveAt(0); }
            p.hand.Add(c); return c;
        }
        public static void Souls(BattleEngine e, int player, int count = 30)
        { var p = e.State.players[player]; foreach (Element element in Enum.GetValues(typeof(Element))) p.SetResource(element, count); p.genericSoul = count; }
        public static void Resolve(BattleEngine e)
        { Assert.True(e.CompletePreparation(e.PreparingPlayer, out var error), error); Assert.True(e.CompletePreparation(e.PreparingPlayer, out error), error); }
        public static int AllCards(PlayerState p) => p.deck.Count + p.hand.Count + p.terrain.Count + p.discard.Count + p.units.Count + p.units.Sum(u => u.equipment.Count) + p.preparedSpells.Count;
    }

    public sealed class BattleEngineTests
    {
        [Test] public void PreparedEnemyMagicHidesItsNameAndLevelUntilResolution()
        {
            var e=ClassicFixtures.Battle();ClassicFixtures.Souls(e,1);
            var spell=ClassicFixtures.Hand(e,1,"죽음의물결");
            Assert.True(e.CompletePreparation(0,out _));
            Assert.True(e.Play(1,spell.instanceId,-1,out var error),error);
            var announcement=e.State.events.Last();
            Assert.That(announcement.text,Does.Not.Contain("죽음의물결"));
            Assert.That(announcement.amount,Is.Zero);
            Assert.True(e.CompletePreparation(1,out error),error);
            Assert.True(e.State.events.Any(ev=>ev.kind=="spell"&&ev.text.Contains("죽음의물결 공개")));
        }
        [Test] public void OriginalCatalogHasExactKindsSevenElementsAndSeparateSoulCosts()
        {
            Assert.That(CardCatalog.All.Length, Is.EqualTo(96));
            Assert.That(CardCatalog.All.GroupBy(c => c.kind).ToDictionary(g => g.Key, g => g.Count()), Is.EquivalentTo(new Dictionary<CardKind,int> { {CardKind.Terrain,14},{CardKind.Unit,56},{CardKind.Spell,14},{CardKind.Equipment,12} }));
            foreach (Element element in Enum.GetValues(typeof(Element)))
            { Assert.That(CardCatalog.All.Count(c => c.element == element && c.kind == CardKind.Unit), Is.EqualTo(8)); Assert.That(CardCatalog.All.Count(c => c.element == element && c.kind == CardKind.Terrain), Is.EqualTo(2)); }
            foreach (var card in CardCatalog.All) { CardCatalog.ValidateDefinition(card); Assert.That(card.id, Is.EqualTo("fm_" + card.sourceId)); }
            var deathKnight = CardCatalog.Get(ClassicFixtures.Id("듀라한"));
            Assert.That(new[] { deathKnight.level,deathKnight.coloredCost,deathKnight.genericCost,deathKnight.cost }, Is.EqualTo(new[] {4,2,2,4}));
            Assert.That(CardCatalog.All.Where(c=>c.kind==CardKind.Unit).Select(c=>c.level).Distinct(), Is.EquivalentTo(Enumerable.Range(1,7)));
        }
        [Test] public void RoundStartsWithSixRealRandomCardsBothLpAndNoLeaderSkill()
        {
            var e = new BattleEngine(7);
            Assert.That(e.State.players.Select(p=>p.hand.Count), Is.EqualTo(new[]{6,6}));
            Assert.That(e.State.players.Select(p=>p.deck.Count), Is.EqualTo(new[]{44,44}));
            Assert.That(e.State.players.Select(p=>p.levelPoints), Is.EqualTo(new[]{1,1}));
            Assert.That(e.PreparingPlayer, Is.Zero); Assert.False(e.UseLeaderSkill(0,out _));
            Assert.False(e.CompletePreparation(0,out _)); Assert.True(e.ConfirmOpeningHand(0,out _));
            Assert.True(e.CompletePreparation(0,out _)); Assert.That(e.PreparingPlayer, Is.EqualTo(1));
            Assert.That(e.State.phase, Is.EqualTo(BattlePhase.DefensePreparation));
            BattleEngine.Validate(e.State);
        }
        [Test] public void LandPaysLpAndProducesColoredAndTotalSoulsImmediately()
        {
            var e = ClassicFixtures.Battle(); var p=e.State.players[0];
            var land=ClassicFixtures.Hand(e,0,"묘지");
            Assert.True(e.Play(0,land.instanceId,-1,out var error),error);
            Assert.That(p.levelPoints, Is.Zero); Assert.That(p.darkness,Is.EqualTo(1)); Assert.That(p.genericSoul,Is.EqualTo(1));
            var other=ClassicFixtures.Hand(e,0,"요정의숲");
            Assert.False(e.Play(0,other.instanceId,-1,out _)); Assert.That(p.hand.Contains(other),Is.True);
            Assert.False(e.CanShuffleHand(0,out _));
        }
        [Test] public void SummoningConsumesColoredRequirementAndWholeLevelOfTotalSoul()
        {
            var e=ClassicFixtures.Battle();var p=e.State.players[0];ClassicFixtures.Souls(e,0,10);
            var card=ClassicFixtures.Hand(e,0,"듀라한"); Assert.True(e.Play(0,card.instanceId,0,out var error),error);
            Assert.That(p.darkness,Is.EqualTo(8)); Assert.That(p.genericSoul,Is.EqualTo(6));
            Assert.False(p.AtLane(0).summoningSick);
        }
        [Test] public void InvalidPaymentAndWrongPreparationHaveNoSideEffects()
        {
            var e=ClassicFixtures.Battle();var card=ClassicFixtures.Hand(e,0,"듀라한");
            var p=e.State.players[0];p.darkness=2;p.genericSoul=3;
            uint random=e.State.randomState;int events=e.State.events.Count;
            Assert.False(e.Play(0,card.instanceId,0,out _)); Assert.False(e.Play(1,card.instanceId,0,out _));
            Assert.That(p.darkness,Is.EqualTo(2));Assert.That(p.genericSoul,Is.EqualTo(3));Assert.That(p.hand.Contains(card),Is.True);
            Assert.That(e.State.randomState,Is.EqualTo(random));Assert.That(e.State.events.Count,Is.EqualTo(events));
        }
        [Test] public void WholeHandShuffleWorksOnceBeforeAnyPlayInEveryPreparation()
        {
            var e=new BattleEngine(91);var p=e.State.players[0];uint rng=e.State.randomState;
            Assert.True(e.MulliganWholeHand(0,out _));Assert.True(e.State.openingHandOpen);Assert.That(p.lastShuffleTurn,Is.EqualTo(1));
            Assert.False(e.MulliganWholeHand(0,out _));Assert.That(e.State.randomState,Is.Not.EqualTo(rng));
            Assert.True(e.ConfirmOpeningHand(0,out _));ClassicFixtures.Resolve(e);
            Assert.That(e.PreparingPlayer,Is.EqualTo(1));Assert.True(e.CompletePreparation(1,out _));
            Assert.That(e.PreparingPlayer,Is.Zero);Assert.True(e.ShuffleHand(0,out _));Assert.False(e.ShuffleHand(0,out _));
            Assert.That(p.hand.Count,Is.EqualTo(6));Assert.That(ClassicFixtures.AllCards(p),Is.EqualTo(50));BattleEngine.Validate(e.State);
        }
        [Test] public void PartialMulliganIsRejectedAndKeepingHandDoesNotConsumeRandomness()
        {
            var e=new BattleEngine(11);uint rng=e.State.randomState;
            Assert.False(e.Mulligan(0,new[]{e.State.players[0].hand[0].instanceId},out _));
            Assert.True(e.ConfirmOpeningHand(0,out _));Assert.That(e.State.randomState,Is.EqualTo(rng));
        }
        [Test] public void DirectAttackBurnsLevelInsteadOfApAndNewlyDeployedUnitsFight()
        {
            var e=ClassicFixtures.Battle(rules:new RuleSet{baseCoins=0});ClassicFixtures.Souls(e,0);
            var c=ClassicFixtures.Hand(e,0,"데몬");Assert.True(e.Play(0,c.instanceId,0,out _));
            int before=e.State.players[1].deck.Count;ClassicFixtures.Resolve(e);
            Assert.That(e.State.players[1].deck.Count,Is.EqualTo(before-5));
            Assert.That(e.State.events.Any(x=>x.kind=="damage" && x.text.Contains("직접 공격 · 레벨 5")),Is.True);
            BattleEngine.Validate(e.State);
        }
        [Test] public void FirstStrikeKillPreventsRetaliationAndSurvivorsResetEachRound()
        {
            var e=ClassicFixtures.Battle(rules:new RuleSet{baseCoins=0});
            var a=ClassicFixtures.Unit(e,0,"데몬",0);ClassicFixtures.Unit(e,1,"파이어버그",0);
            ClassicFixtures.Resolve(e);
            Assert.That(e.State.players[1].units,Is.Empty);Assert.That(e.State.players[0].units.Single(),Is.SameAs(a));
            Assert.That(a.damage,Is.Zero);Assert.False(a.coinsRolled);Assert.That(e.State.events.Any(x=>x.kind=="damage" && x.text.Contains("반격 없음")),Is.True);
        }
        [Test] public void SurvivingDefenderBlocksMultipleAttackersInLaneOrder()
        {
            var e=ClassicFixtures.Battle(rules:new RuleSet{baseCoins=0});
            ClassicFixtures.Unit(e,0,"파이어버그",3);ClassicFixtures.Unit(e,0,"임프",1);ClassicFixtures.Unit(e,1,"골드드래곤",4);
            int deck=e.State.players[1].deck.Count;ClassicFixtures.Resolve(e);
            var hits=e.State.events.Where(x=>x.kind=="damage"&&x.text.Contains("→")).Select(x=>x.text).ToArray();
            Assert.That(hits.Length,Is.EqualTo(2));Assert.That(hits[0],Does.StartWith("임프"));Assert.That(hits[1],Does.StartWith("파이어버그"));
            Assert.That(hits.All(x=>x.Contains("골드드래곤")),Is.True);Assert.That(e.State.players[1].deck.Count,Is.EqualTo(deck-4)); // Unit's upkeep, never direct damage.
            Assert.That(e.State.events.Any(x=>x.text.Contains("직접 공격")),Is.False);
        }
        [Test] public void CoinsShareOneOrderedSequenceAndSetMultiplyOperatorsArePreserved()
        {
            var e=ClassicFixtures.Battle(103);ClassicFixtures.Unit(e,0,"아이언타워",0);
            uint expected=e.State.randomState;int head=StableRandom.Range(ref expected,2);
            ClassicFixtures.Resolve(e);
            var coins=e.State.events.Where(x=>x.kind=="coin").ToArray();Assert.That(coins.Length,Is.EqualTo(1));Assert.That(coins[0].amount,Is.EqualTo(head));
            Assert.That(e.State.randomState,Is.EqualTo(expected));
            Assert.That(e.State.events.Single(x=>x.kind=="power").text,Does.Contain("DP "+(2*head)));
            Assert.That(CardCatalog.Get(ClassicFixtures.Id("실프")).defenseCoin.operation,Is.EqualTo(CoinOperation.Multiply));
        }
        [Test] public void FireballIsReservedOccupiesFrontAndHitsOnlySpecifiedEnemySlots()
        {
            var e=ClassicFixtures.Battle(rules:new RuleSet{baseCoins=0});ClassicFixtures.Souls(e,0);
            ClassicFixtures.Unit(e,1,"파이어버그",0);var rear=ClassicFixtures.Unit(e,1,"임프",4);
            var spell=ClassicFixtures.Hand(e,0,"파이어볼");Assert.True(e.Play(0,spell.instanceId,-1,out _));
            Assert.That(e.State.players[1].units.Count,Is.EqualTo(2));Assert.That(e.State.players[0].SpellAtLane(0),Is.Not.Null);
            var unit=ClassicFixtures.Hand(e,0,"농민");Assert.False(e.Play(0,unit.instanceId,0,out _));
            ClassicFixtures.Resolve(e);
            Assert.That(e.State.players[1].units.Single(),Is.SameAs(rear));Assert.That(e.State.players[0].preparedSpells,Is.Empty);
            Assert.That(e.State.players[0].discard.Any(c=>c.instanceId==spell.instanceId),Is.True);BattleEngine.Validate(e.State);
        }
        [Test] public void PhysicalSpellHonorsDpWhereHpReductionDoesNot()
        {
            var e=ClassicFixtures.Battle(rules:new RuleSet{baseCoins=0});ClassicFixtures.Souls(e,0);
            ClassicFixtures.Unit(e,1,"파이어버그",0);var protectedUnit=ClassicFixtures.Unit(e,1,"스캘럽",1);
            var spell=ClassicFixtures.Hand(e,0,"낙석");Assert.True(e.Play(0,spell.instanceId,-1,out _));ClassicFixtures.Resolve(e);
            Assert.That(e.State.players[1].units.Single(),Is.SameAs(protectedUnit));
        }
        [Test] public void AttackDefenseSpellRestrictionsAndLowerLevelOrderAreEnforced()
        {
            var e=ClassicFixtures.Battle();ClassicFixtures.Souls(e,0);ClassicFixtures.Souls(e,1);
            var defense=ClassicFixtures.Hand(e,0,"겨울잠");Assert.False(e.Play(0,defense.instanceId,-1,out _));
            var high=ClassicFixtures.Hand(e,0,"메테오");Assert.True(e.Play(0,high.instanceId,-1,out _));
            Assert.True(e.CompletePreparation(0,out _));
            var attack=ClassicFixtures.Hand(e,1,"파이어볼");Assert.False(e.Play(1,attack.instanceId,-1,out _));
            var low=ClassicFixtures.Hand(e,1,"겨울잠");Assert.True(e.Play(1,low.instanceId,-1,out _));Assert.True(e.CompletePreparation(1,out _));
            Assert.That(e.State.events.Where(x=>x.kind=="spell").Select(x=>x.text.Split(' ')[0]),Is.EqualTo(new[]{"겨울잠","메테오"}));
        }
        [Test] public void EquipmentRequiresMatchingAbilityAndAppliesSignedStats()
        {
            var e=ClassicFixtures.Battle();ClassicFixtures.Souls(e,0);
            var beast=ClassicFixtures.Unit(e,0,"스웜",0);var gear=ClassicFixtures.Unit(e,0,"농민",1);
            var item=ClassicFixtures.Hand(e,0,"아이언실드");
            Assert.False(e.Play(0,item.instanceId,0,out _));Assert.True(e.Play(0,item.instanceId,1,out var error),error);
            Assert.That(CardCatalog.UnitAttack(gear),Is.EqualTo(1));Assert.That(CardCatalog.UnitDefense(gear),Is.EqualTo(3));Assert.That(beast.equipment,Is.Empty);
            BattleEngine.Validate(e.State);
        }
        [Test] public void UnitsPayUpkeepAfterSoulRefreshButItemsDoNot()
        {
            var e=ClassicFixtures.Battle(rules:new RuleSet{baseCoins=0});var p=e.State.players[0];
            ClassicFixtures.Unit(e,0,"듀라한",0);var land=ClassicFixtures.Extract(e,0,"묘지");p.terrain.Add(land);
            p.genericSoul=40;int before=p.discard.Count;ClassicFixtures.Resolve(e);
            Assert.That(p.genericSoul,Is.Zero);Assert.That(p.discard.Count,Is.EqualTo(before+1));
            Assert.That(e.State.events.Any(x=>x.kind=="upkeep"&&x.actor==0&&x.amount==2),Is.True);
        }
        [Test] public void SavedDefensePreparationRestoresNextCoinsAndAllCards()
        {
            var a=ClassicFixtures.Battle(77);ClassicFixtures.Unit(a,0,"아이언타워",0);ClassicFixtures.Unit(a,1,"골드드래곤",0);
            Assert.True(a.CompletePreparation(0,out _));var b=new BattleEngine(a.State.Copy());
            Assert.True(a.CompletePreparation(1,out _));Assert.True(b.CompletePreparation(1,out _));
            Assert.That(a.State.randomState,Is.EqualTo(b.State.randomState));
            Assert.That(a.State.events.Select(x=>x.sequence+":"+x.kind+":"+x.text),Is.EqualTo(b.State.events.Select(x=>x.sequence+":"+x.kind+":"+x.text)));
            foreach(var p in b.State.players)Assert.That(ClassicFixtures.AllCards(p),Is.EqualTo(50));BattleEngine.Validate(b.State);
        }
        [Test] public void PreviousVersionAndDuplicateInstancesAreRejectedWithoutMutatingSource()
        {
            var e=ClassicFixtures.Battle();var old=e.State.Copy();old.schemaVersion=2;old.contentVersion="starlight-0.2.0";
            Assert.Throws<ArgumentException>(()=>new BattleEngine(old));Assert.That(old.schemaVersion,Is.EqualTo(2));
            var corrupt=e.State.Copy();corrupt.players[0].deck[0].instanceId=corrupt.players[1].deck[0].instanceId;
            Assert.Throws<ArgumentException>(()=>new BattleEngine(corrupt));BattleEngine.Validate(e.State);
        }
        [Test] public void TurnLimitResultPreservesLastPreparationRecordsAndCanBeSaved()
        {
            var e=ClassicFixtures.Battle(rules:new RuleSet{maximumTurns=2});
            ClassicFixtures.Resolve(e);
            var land=ClassicFixtures.Hand(e,1,"묘지");Assert.True(e.Play(1,land.instanceId,-1,out _));
            ClassicFixtures.Resolve(e);
            Assert.That(e.State.phase,Is.EqualTo(BattlePhase.Complete));
            Assert.That(e.State.turn,Is.EqualTo(2));
            Assert.DoesNotThrow(()=>new BattleEngine(e.State.Copy()));
        }
        [Test] public void LifeModeAndConcessionReachExplicitResults()
        {
            var e=ClassicFixtures.Battle(rules:new RuleSet{baseCoins=0,victoryRule=VictoryRule.Life,startingHealth=4});ClassicFixtures.Unit(e,0,"데몬",0);ClassicFixtures.Resolve(e);
            Assert.That(e.State.phase,Is.EqualTo(BattlePhase.Complete));Assert.That(e.State.winner,Is.Zero);BattleEngine.Validate(e.State);
            var other=ClassicFixtures.Battle();Assert.True(other.Concede(0));Assert.That(other.State.winner,Is.EqualTo(1));BattleEngine.Validate(other.State);
        }
#if UNITY_EDITOR
        [TestCase("forest")] [TestCase("knights")] [TestCase("tides")] [TestCase("stars")]
        public void UnityJsonPreservesClassicPreparationShuffleAndEntireContinuation(string deckId)
        {
            var a=DemoCatalog.CreateBattle(31,deckId,DemoCatalog.GetDeck(deckId).recommendedLeaderId);
            Assert.True(a.MulliganWholeHand(0,out _));Assert.True(a.ConfirmOpeningHand(0,out _));
            for(int i=0;i<4 && a.State.phase!=BattlePhase.Complete;i++)Assert.True(BattleAi.Step(a,a.PreparingPlayer,out _));
            var json=UnityEngine.JsonUtility.ToJson(a.State);var b=new BattleEngine(UnityEngine.JsonUtility.FromJson<BattleState>(json));
            Assert.That(UnityEngine.JsonUtility.ToJson(b.State),Is.EqualTo(json));
            int actions=0;
            while(a.State.phase!=BattlePhase.Complete && actions++<2000)
            { int actor=a.PreparingPlayer;Assert.True(BattleAi.Step(a,actor,out _));Assert.True(BattleAi.Step(b,actor,out _));BattleEngine.Validate(b.State);Assert.That(UnityEngine.JsonUtility.ToJson(b.State),Is.EqualTo(UnityEngine.JsonUtility.ToJson(a.State))); }
            Assert.That(a.State.phase,Is.EqualTo(BattlePhase.Complete));
        }
#endif
    }
}
