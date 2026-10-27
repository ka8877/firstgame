using System;
using System.Linq;
using FantasyCards.Core;
using FantasyCards.Tests;
using NUnit.Framework;

namespace FirstGame.Validation
{
    [TestFixture] public sealed class DemoBattleTests
    {
        [Test] public void FiveStarterDecksRetainTheFourOriginalCurvesAndUseAllSevenElements()
        {
            Assert.That(DemoCatalog.StarterDecks.Length,Is.EqualTo(5));Assert.That(DemoCatalog.Leaders.Length,Is.EqualTo(4));Assert.That(DemoCatalog.Encounters.Length,Is.EqualTo(3));
            foreach(var deck in DemoCatalog.StarterDecks.Take(4))
            {
                DemoCatalog.ValidatePlayableDeck(deck.cards);
                Assert.That(deck.cards.Length,Is.EqualTo(50));Assert.That(deck.cards.Select(CardCatalog.Get).Count(c=>c.kind==CardKind.Terrain),Is.EqualTo(16));
                Assert.That(deck.cards.Select(CardCatalog.Get).Count(c=>c.kind==CardKind.Unit),Is.EqualTo(24));
                Assert.That(deck.cards.Select(CardCatalog.Get).Count(c=>c.kind==CardKind.Spell),Is.EqualTo(6));
                Assert.That(deck.cards.Select(CardCatalog.Get).Count(c=>c.kind==CardKind.Equipment),Is.EqualTo(4));
                Assert.That(deck.cards.Select(CardCatalog.Get).Max(c=>c.level),Is.GreaterThanOrEqualTo(6));
                var copy=deck.CopyCards();copy.Clear();Assert.That(deck.cards.Length,Is.EqualTo(50));
            }
            DemoCatalog.ValidatePlayableDeck(DemoCatalog.GetDeck("fidelity").cards);
            Assert.That(DemoCatalog.GetDeck("fidelity").cards.Length,Is.EqualTo(50));
            Assert.That(DemoCatalog.StarterDecks.Select(d=>string.Join(",",d.cards)).Distinct().Count(),Is.EqualTo(5));
            Assert.That(DemoCatalog.StarterDecks.SelectMany(d=>d.cards).Select(CardCatalog.Get).Select(c=>c.element).Distinct().Count(),Is.EqualTo(7));
        }
        [TestCase("forest")] [TestCase("knights")] [TestCase("tides")] [TestCase("stars")] [TestCase("fidelity")]
        public void EveryDeckCompletesThreeTrialsAndRestoresEachActionForSeveralSeeds(string deckId)
        {
            for(int trial=0;trial<3;trial++)for(uint seed=1;seed<=3;seed++)
            {
                var a=DemoCatalog.CreateBattle(seed,deckId,DemoCatalog.GetDeck(deckId).recommendedLeaderId,trial);
                Assert.True(a.MulliganWholeHand(0,out _));Assert.True(a.ConfirmOpeningHand(0,out _));
                var b=new BattleEngine(FieldJson.Decode<BattleState>(FieldJson.Encode(a.State)));
                int actions=0;
                while(a.State.phase!=BattlePhase.Complete&&actions++<2000)
                {
                    int player=a.PreparingPlayer;Assert.That(BattleAi.Step(a,player,out var error),Is.True,error);Assert.That(BattleAi.Step(b,player,out error),Is.True,error);
                    BattleEngine.Validate(a.State);BattleEngine.Validate(b.State);
                    Assert.That(FieldJson.Encode(b.State),Is.EqualTo(FieldJson.Encode(a.State)),deckId+"/"+trial+"/"+seed+" action "+actions);
                }
                Assert.That(a.State.phase,Is.EqualTo(BattlePhase.Complete));Assert.That(actions,Is.LessThan(2000));
                TestContext.WriteLine(deckId+" / trial "+trial+" / seed "+seed+": winner="+a.State.winner+", turns="+a.State.turn+", actions="+actions);
            }
        }
        [Test] public void AllAvatarsAreSelectableWithoutChangingTheDeckOrGrantingSkills()
        {
            foreach(var deck in DemoCatalog.StarterDecks)foreach(var avatar in DemoCatalog.Leaders)
            {
                var e=DemoCatalog.CreateBattle(91,deck.id,avatar.id,2);Assert.That(e.State.players[0].leaderId,Is.EqualTo(avatar.id));Assert.That(e.State.players[0].deckId,Is.EqualTo(deck.id));
                Assert.False(e.CanUseLeaderSkill(0,out _));BattleEngine.Validate(e.State);
            }
        }
        [Test] public void SameSeedHasSameHandWhileDifferentSeedsChangeRealShuffleOrder()
        {
            var a=new BattleEngine(78);var b=new BattleEngine(78);var c=new BattleEngine(79);
            Assert.That(FieldJson.Encode(a.State),Is.EqualTo(FieldJson.Encode(b.State)));
            Assert.That(string.Join(",",a.State.players[0].deck.Select(x=>x.instanceId)),Is.Not.EqualTo(string.Join(",",c.State.players[0].deck.Select(x=>x.instanceId))));
        }
        [Test] public void AiCanReachBothNaturalWinAndLossWithoutSyntheticResults()
        {
            var winners=new System.Collections.Generic.HashSet<int>();
            for(uint seed=1;seed<=16&&winners.Count<2;seed++)foreach(var deck in DemoCatalog.StarterDecks)
            {
                var e=DemoCatalog.CreateBattle(seed,deck.id,deck.recommendedLeaderId,0);int actions=0;
                while(e.State.phase!=BattlePhase.Complete&&actions++<2000)Assert.True(BattleAi.Step(e,e.PreparingPlayer,out _));
                Assert.That(e.State.phase,Is.EqualTo(BattlePhase.Complete));if(e.State.winner<2)winners.Add(e.State.winner);
            }
            Assert.That(winners,Is.EquivalentTo(new[]{0,1}));
        }
        [Test] public void NativeTutorialSearchCanFindARealPlayableOpening()
        {
            bool found=false;
            for(uint seed=1;seed<=512&&!found;seed++)
            {
                var e=new BattleEngine(seed);e.ConfirmOpeningHand(0,out _);var p=e.State.players[0];
                foreach(var card in p.hand.Where(c=>CardCatalog.Get(c.definitionId).kind==CardKind.Terrain).ToArray())
                {
                    if(!e.CanPlay(0,card.instanceId,-1,out _))continue;e.Play(0,card.instanceId,-1,out _);
                    found=p.hand.Any(c=>CardCatalog.Get(c.definitionId).kind==CardKind.Unit&&e.CanPlay(0,c.instanceId,0,out _));if(found)break;
                }
            }
            Assert.True(found,"Native UI smoke must find an actual affordable land/unit opening within its bounded search.");
        }
    }
}
