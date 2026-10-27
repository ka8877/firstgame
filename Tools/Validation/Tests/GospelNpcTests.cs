using System;
using System.Collections.Generic;
using System.Linq;
using FirstGame.Shared;
using NUnit.Framework;
using PathOfDawn.Core;

namespace FirstGame.Validation
{
    [TestFixture]
    public sealed class GospelNpcTests
    {
        static GospelJourney AtTemple()
        {var journey=GospelJourney.New();while(journey.Phase==GospelPhase.Prologue)journey.AdvanceReading();return journey;}

        [Test]
        public void WrongNpcCannotStartFutureScriptureAndEveryObjectiveHasARealConversationRoute()
        {
            var journey=AtTemple();
            foreach(var objective in GospelCatalog.Objectives)
            {
                string before=FieldJson.Encode(journey.CaptureSave());
                var wrong=GospelNpcCatalog.All.First(npc=>!GospelNpcCatalog.CanGuide(npc.Id,objective.Id));
                Assert.That(GospelNpcCatalog.TryBeginStory(journey,wrong.Id,out var refusal),Is.False);
                Assert.That(refusal,Is.EqualTo(objective.Prompt));
                Assert.That(FieldJson.Encode(journey.CaptureSave()),Is.EqualTo(before));
                var right=GospelNpcCatalog.All.First(npc=>GospelNpcCatalog.CanGuide(npc.Id,objective.Id));
                int collected=journey.CollectedReadings.Count;
                Assert.That(GospelNpcCatalog.TryBeginStory(journey,right.Id,out var reference),Is.True);
                Assert.That(reference,Is.EqualTo(GospelCatalog.GetReading(objective.ReadingId).Reference));
                Assert.That(journey.CollectedReadings.Count,Is.EqualTo(collected),"Opening a conversation must not acknowledge unread Scripture.");
                Assert.That(GospelNpcCatalog.TryBeginStory(journey,right.Id,out _),Is.False,"A second NPC interaction must not skip an open conversation.");
                var restored=GospelJourney.FromSave(FieldJson.Decode<GospelSave>(FieldJson.Encode(journey.CaptureSave())));
                Assert.That(restored.Phase,Is.EqualTo(GospelPhase.Reading));
                Assert.That(restored.CurrentReading.Id,Is.EqualTo(objective.ReadingId));
                while(journey.Phase==GospelPhase.Reading)Assert.That(journey.AdvanceDialogue(),Is.True);
                Assert.That(journey.CollectedReadings.Count,Is.EqualTo(collected+1));
            }
            Assert.That(journey.IsComplete,Is.True);
        }

        [Test]
        public void BirthCannotBeSkippedByNpcInteraction()
        {
            var journey=GospelJourney.New();string before=FieldJson.Encode(journey.CaptureSave());
            foreach(var npc in GospelNpcCatalog.All)Assert.That(GospelNpcCatalog.TryBeginStory(journey,npc.Id,out _),Is.False);
            Assert.That(FieldJson.Encode(journey.CaptureSave()),Is.EqualTo(before));
        }

        [Test]
        public void EveryRepeatableTopicKeepsExactQuotationBoundToItsDisplayedPassage()
        {
            Assert.That(GospelNpcCatalog.All.Select(n=>n.Id).Distinct().Count(),Is.EqualTo(5));
            foreach(var npc in GospelNpcCatalog.All)
            {
                Assert.That(npc.Topics.Count,Is.GreaterThanOrEqualTo(2));
                foreach(var topic in npc.Topics)
                {
                    var reading=GospelCatalog.GetReading(topic.ReadingId);
                    var turns=GospelNpcCatalog.ForTopic(npc.Id,topic.Id);
                    Assert.That(turns.Any(turn=>turn.IsVerbatim),Is.True);
                    foreach(var turn in turns.Where(turn=>turn.IsVerbatim))
                        Assert.That(reading.Text.Contains(turn.Text,StringComparison.Ordinal),Is.True,"Quoted NPC-topic text must come from the exact passage shown beneath it.");
                    foreach(var turn in turns.Where(turn=>!turn.IsVerbatim))Assert.That(turn.Label,Does.Contain("본문 요약"));
                    Assert.Throws<NotSupportedException>(()=>((IList<GospelDialogueTurn>)turns).Clear());
                }
            }
        }
    }
}
