using System;
using System.Linq;
using FantasyCards.Core;
using FantasyCards.Tests;
using NUnit.Framework;

namespace FirstGame.Validation
{
    [TestFixture] public sealed class GameInvariantTests
    {
        [TestCase(1,0,true)] [TestCase(3,2,false)] [TestCase(5,-1,true)]
        public void NonDefaultRulesStillTerminateConserveCardsAndSerialize(int lanes,int coins,bool counter)
        {
            var rules=new RuleSet{deckSize=12,startingHealth=7,openingHand=3,handLimit=5,unitLanes=lanes,baseCoins=coins,defenderStrikesBack=counter,terrainLimit=4,terrainPerTurn=2,maximumResource=6,maximumTurns=14};
            var e=ClassicFixtures.Battle(170,rules);int steps=0;
            while(e.State.phase!=BattlePhase.Complete&&steps++<300)
            {
                Assert.True(BattleAi.Step(e,e.PreparingPlayer,out var error),error);BattleEngine.Validate(e.State);
                var copy=new BattleEngine(FieldJson.Decode<BattleState>(FieldJson.Encode(e.State)));
                Assert.That(FieldJson.Encode(copy.State),Is.EqualTo(FieldJson.Encode(e.State)));
                foreach(var p in e.State.players)Assert.That(ClassicFixtures.AllCards(p),Is.EqualTo(12));
            }
            Assert.That(e.State.phase,Is.EqualTo(BattlePhase.Complete));Assert.That(e.State.turn,Is.LessThanOrEqualTo(15));
        }
        [TestCase("leader")] [TestCase("deck")] [TestCase("composition")] [TestCase("soul")]
        [TestCase("lp")] [TestCase("shuffle")] [TestCase("played")] [TestCase("phase")]
        [TestCase("instance")] [TestCase("event-sequence")] [TestCase("card-count")]
        public void MalformedClassicSavesCannotMutateLiveState(string corruption)
        {
            var live=DemoCatalog.CreateBattle(13,"forest","lyria");live.ConfirmOpeningHand(0,out _);
            var original=FieldJson.Encode(live.State);var broken=FieldJson.Decode<BattleState>(original);
            switch(corruption)
            {
                case "leader":broken.players[0].leaderId="unknown";break;
                case "deck":broken.players[0].deckId="unknown";break;
                case "composition":broken.players[0].hand[0].definitionId=ClassicFixtures.Id("메테오");break;
                case "soul":broken.players[0].darkness=-1;break;
                case "lp":broken.players[0].levelPoints=-1;break;
                case "shuffle":broken.players[0].lastShuffleTurn=1;break;
                case "played":broken.players[0].playedThisPreparation=true;break;
                case "phase":broken.phase=BattlePhase.BlockResponse;break;
                case "instance":broken.players[0].hand[0].instanceId=broken.players[1].hand[0].instanceId;break;
                case "event-sequence":broken.events[0].sequence=99;break;
                case "card-count":var c=broken.players[0].deck[0];broken.players[0].deck.RemoveAt(0);broken.players[1].deck.Add(c);break;
            }
            Assert.Throws<ArgumentException>(()=>new BattleEngine(broken));Assert.That(FieldJson.Encode(live.State),Is.EqualTo(original));
        }
        [Test] public void PreparedMagicAndNestedEquipmentAreDeepCopied()
        {
            var live=ClassicFixtures.Battle();ClassicFixtures.Souls(live,0);
            ClassicFixtures.Unit(live,0,"농민",1);var item=ClassicFixtures.Hand(live,0,"아이언실드");Assert.True(live.Play(0,item.instanceId,1,out _));
            var spell=ClassicFixtures.Hand(live,0,"파이어볼");Assert.True(live.Play(0,spell.instanceId,-1,out _));
            var copy=new BattleEngine(live.State.Copy());
            copy.State.players[0].preparedSpells[0].lane=4;copy.State.players[0].units[0].equipment.Clear();copy.State.events[0].text="changed";
            Assert.That(live.State.players[0].preparedSpells[0].lane,Is.Zero);Assert.That(live.State.players[0].units[0].equipment.Count,Is.EqualTo(1));Assert.That(live.State.events[0].text,Is.Not.EqualTo("changed"));
            BattleEngine.Validate(live.State);
        }
        [Test] public void RejectedActionsDoNotAdvanceRandomnessOrCommitEvents()
        {
            var e=ClassicFixtures.Battle();var before=FieldJson.Encode(e.State);
            Assert.False(e.Play(0,999999,0,out _));Assert.False(e.ShuffleHand(1,out _));Assert.False(e.CompletePreparation(1,out _));Assert.False(e.UseLeaderSkill(0,out _));
            Assert.That(FieldJson.Encode(e.State),Is.EqualTo(before));
        }
    }
}
