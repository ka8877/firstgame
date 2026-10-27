using System;
using System.Linq;
using FantasyCards.Core;
using FantasyCards.Tests;
using NUnit.Framework;

namespace FirstGame.Validation
{
    [TestFixture] public sealed class DemoBattleTests
    {
        [Test] public void TwentyStarterDecksRetainTheEighteenPriorPresetsAndCoverMonoAndDualElements()
        {
            Assert.That(DemoCatalog.StarterDecks.Length,Is.EqualTo(20));Assert.That(DemoCatalog.Leaders.Length,Is.EqualTo(4));Assert.That(DemoCatalog.Encounters.Length,Is.EqualTo(7));
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
            Assert.That(DemoCatalog.StarterDecks.Select(d=>string.Join(",",d.cards.OrderBy(id=>id))).Distinct().Count(),Is.EqualTo(20));
            Assert.That(DemoCatalog.StarterDecks.SelectMany(d=>d.cards).Select(CardCatalog.Get).Select(c=>c.element).Distinct().Count(),Is.EqualTo(7));
            var mono=DemoCatalog.StarterDecks.Where(d=>d.id.StartsWith("mono-")).ToArray();
            Assert.That(mono.Length,Is.EqualTo(7));
            Assert.That(mono.Select(d=>d.cards.Select(CardCatalog.Get).Select(c=>c.element).Distinct().Single()),Is.EquivalentTo(Enum.GetValues(typeof(Element))));
            var dual=DemoCatalog.StarterDecks.Where(d=>d.cards.Select(CardCatalog.Get).Select(c=>c.element).Distinct().Count()==2).ToArray();
            Assert.That(dual.Length,Is.EqualTo(13));
            Assert.That(dual.Select(d=>string.Join(",",d.cards.Select(CardCatalog.Get).Select(c=>(int)c.element).Distinct().OrderBy(e=>e))).Distinct().Count(),Is.EqualTo(12));
            foreach(var deck in DemoCatalog.StarterDecks.Skip(5).Take(13))
            {
                DemoCatalog.ValidatePlayableDeck(deck.cards);
                var definitions=deck.cards.Select(CardCatalog.Get).ToArray();
                Assert.That(definitions.Count(c=>c.kind==CardKind.Terrain),Is.EqualTo(16),deck.id);
                Assert.That(definitions.Count(c=>c.kind==CardKind.Unit),Is.EqualTo(24),deck.id);
                Assert.That(definitions.Count(c=>c.kind==CardKind.Spell),Is.EqualTo(6),deck.id);
                Assert.That(definitions.Count(c=>c.kind==CardKind.Equipment),Is.EqualTo(4),deck.id);
                Assert.That(definitions.Count(c=>c.kind==CardKind.Unit&&c.level<=2),Is.GreaterThanOrEqualTo(14),deck.id);
                Assert.That(definitions.Where(c=>c.kind==CardKind.Unit&&c.level<=3).Sum(c=>c.genericCost),Is.LessThanOrEqualTo(16),deck.id);
                foreach(var source in definitions.Where(c=>c.kind==CardKind.Terrain))
                { Assert.That(source.landPointCost,Is.EqualTo(1));Assert.That(source.producedColored,Is.EqualTo(1));Assert.That(source.producedGeneric,Is.EqualTo(1)); }
                foreach(var item in definitions.Where(c=>c.kind==CardKind.Equipment))
                    Assert.That(definitions.Any(c=>c.kind==CardKind.Unit&&(item.requiredCapabilityMask==0||(item.requiredCapabilityMask&c.capabilityMask)!=0)),Is.True,deck.id+" item "+item.name);
                foreach(var spell in definitions.Where(c=>c.kind==CardKind.Spell))foreach(var op in spell.operations.Where(o=>o.side==OperationSide.Ally&&o.requiredCapabilityMask!=0))
                    Assert.That(definitions.Any(c=>c.kind==CardKind.Unit&&(c.capabilityMask&op.requiredCapabilityMask)!=0),Is.True,deck.id+" spell "+spell.name);
                foreach(var group in definitions.GroupBy(c=>c.element))
                    Assert.That(group.Count(c=>c.kind==CardKind.Terrain),Is.EqualTo(deck.id.StartsWith("mono-")?16:8),deck.id+" soul source "+group.Key);
            }
        }
        [Test] public void NewPracticePresetsDeployRealUnitsAndFinishFromShuffledUnmodifiedDecks()
        {
            foreach(var deck in DemoCatalog.StarterDecks.Skip(5))for(uint seed=17;seed<=19;seed++)
            {
                var e=DemoCatalog.CreateBattle(seed,deck.id,deck.recommendedLeaderId,(int)(seed%3));
                int actions=0;bool deployedTerrain=false,deployedUnit=false;
                while(e.State.phase!=BattlePhase.Complete&&actions++<2000)
                {
                    Assert.That(BattleAi.Step(e,e.PreparingPlayer,out var error),Is.True,deck.id+" "+error);
                    BattleEngine.Validate(e.State);
                    deployedTerrain|=e.State.players[0].terrain.Count>0;
                    deployedUnit|=e.State.players[0].units.Count>0;
                }
                Assert.That(deployedTerrain,Is.True,deck.id+" seed "+seed+" never deployed a soul source");
                Assert.That(deployedUnit,Is.True,deck.id+" seed "+seed+" never afforded a unit");
                Assert.That(e.State.phase,Is.EqualTo(BattlePhase.Complete),deck.id+" seed "+seed);
                Assert.That(actions,Is.LessThan(2000),deck.id);
                TestContext.WriteLine(deck.id+" / seed "+seed+": winner="+e.State.winner+", turns="+e.State.turn+", actions="+actions);
            }
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
