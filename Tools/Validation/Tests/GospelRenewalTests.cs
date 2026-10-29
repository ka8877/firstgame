using System;
using System.Linq;
using FirstGame.Shared;
using NUnit.Framework;
using PathOfDawn.Core;

namespace FirstGame.Validation
{
    public sealed class GospelRenewalTests
    {
        static GospelJourney AtStudyTrail()
        {
            var journey=GospelJourney.NewStudyJourney();journey.FinishOpening();
            for(int i=0;i<3;i++)Assert.That(journey.AdvanceReading(),Is.True);
            return journey;
        }
        [Test]
        public void IllustratedOpeningCanResumeOrSkipWithoutAwardingScripture()
        {
            var journey=GospelJourney.NewStudyJourney();
            Assert.That(journey.AdvanceReading(),Is.False);
            journey.AdvanceOpening();journey.AdvanceOpening();
            var loaded=GospelJourney.FromSave(FieldJson.Decode<GospelSave>(FieldJson.Encode(journey.CaptureSave())));
            Assert.That(loaded.OpeningPage,Is.EqualTo(2));Assert.That(loaded.OpeningFinished,Is.False);
            Assert.That(loaded.CodexCount,Is.Zero);
            loaded.FinishOpening();loaded.FinishOpening();
            Assert.That(loaded.OpeningFinished,Is.True);Assert.That(loaded.CodexCount,Is.Zero);
            Assert.That(loaded.Phase,Is.EqualTo(GospelPhase.Prologue));
        }
        [Test]
        public void EveryStudyConversationSurvivesReloadAndAwardsItsCardOnlyOnLastTurn()
        {
            var journey=AtStudyTrail();
            for(int index=0;index<8;index++)
            {
                string id=journey.CurrentStudy.Reading.Id;
                Assert.That(journey.TryInteract("temple-gate",out _),Is.False);
                Assert.That(journey.TryStudyEncounter("not-this-meeting"),Is.False);
                Assert.That(journey.TryStudyEncounter(id),Is.True);
                for(int turn=0;turn<8;turn++)
                {
                    journey=GospelJourney.FromSave(FieldJson.Decode<GospelSave>(FieldJson.Encode(journey.CaptureSave())));
                    Assert.That(journey.StudyDialogueIndex,Is.EqualTo(turn));
                    Assert.That(journey.HasReading(id),Is.False);
                    Assert.That(journey.StudyCount,Is.EqualTo(index));
                    Assert.That(journey.AdvanceStudyDialogue(),Is.True);
                }
                Assert.That(journey.HasReading(id),Is.True);
                Assert.That(journey.StudyReadingOpen,Is.False);
                Assert.That(journey.AdvanceStudyDialogue(),Is.False);
                Assert.That(journey.StudyCount,Is.EqualTo(index+1));
                Assert.That(journey.TryStudyEncounter(id),Is.False);
            }
            Assert.That(journey.TryInteract("temple-gate",out _),Is.True);
            Assert.That(journey.CodexCount,Is.EqualTo(11));
        }
        [Test]
        public void NewJourneyCompletionRequiresAllFifteenReadingsAndKeepsEquipment()
        {
            var journey=AtStudyTrail();journey.SetUtility(GospelBelongings.LampId);
            while(journey.StudyPending)
            {
                journey.TryStudyEncounter(journey.CurrentStudy.Reading.Id);
                while(journey.StudyReadingOpen)journey.AdvanceStudyDialogue();
            }
            while(!journey.IsComplete)
            {
                Assert.That(journey.TryInteract(journey.CurrentObjective.Id,out _),Is.True);
                while(journey.Phase==GospelPhase.Reading)journey.AdvanceDialogue();
            }
            var loaded=GospelJourney.FromSave(FieldJson.Decode<GospelSave>(FieldJson.Encode(journey.CaptureSave())));
            Assert.That(loaded.IsComplete,Is.True);Assert.That(loaded.CodexCount,Is.EqualTo(15));
            Assert.That(loaded.CollectedReadings.Count,Is.EqualTo(7));Assert.That(loaded.LampEquipped,Is.True);
            Assert.That(GospelStudyCatalog.AllReadings.All(r=>loaded.HasReading(r.Id)),Is.True);
        }
        [Test]
        public void LegacyCompletedSevenReadingSaveRemainsCompletedWithoutNewRequirements()
        {
            var journey=GospelJourney.New();for(int i=0;i<3;i++)journey.AdvanceReading();
            while(!journey.IsComplete){journey.TryInteract(journey.CurrentObjective.Id,out _);journey.AdvanceReading();}
            // Decode a pre-extension payload containing only original fields.
            string json="{\"schemaVersion\":1,\"contentVersion\":\"gospel-0.1.0\",\"phase\":3,\"prologuePage\":3,\"objectiveIndex\":4,\"dialoguePage\":0,\"collectedReadings\":[\"birth-manger\",\"birth-good-news\",\"birth-remembered\",\"temple-passover\",\"temple-teachers\",\"temple-parents\",\"temple-nazareth\"],\"playerX\":24,\"playerY\":0,\"playerZ\":0,\"playerYaw\":0}";
            var loaded=GospelJourney.FromSave(FieldJson.Decode<GospelSave>(json));
            Assert.That(loaded.IsComplete,Is.True);Assert.That(loaded.HasStudyJourney,Is.False);
            Assert.That(loaded.OpeningFinished,Is.True);Assert.That(loaded.CodexTotal,Is.EqualTo(7));
            Assert.That(loaded.CollectedReadings,Is.EqualTo(journey.CollectedReadings));
        }
        [Test]
        public void CorruptOrSkippedCodexCannotReplaceExistingJourney()
        {
            var journey=AtStudyTrail();string before=FieldJson.Encode(journey.CaptureSave());
            var corrupt=journey.CaptureSave();corrupt.studyReadings.Add("simeon-peace");
            Assert.Throws<ArgumentException>(()=>journey.RestoreSave(corrupt));
            Assert.That(FieldJson.Encode(journey.CaptureSave()),Is.EqualTo(before));
            corrupt=journey.CaptureSave();corrupt.objectiveIndex=1;corrupt.collectedReadings.Add("temple-passover");
            Assert.Throws<ArgumentException>(()=>journey.RestoreSave(corrupt));
            corrupt=journey.CaptureSave();corrupt.phase=GospelPhase.Reading;
            Assert.Throws<ArgumentException>(()=>journey.RestoreSave(corrupt));
            corrupt=journey.CaptureSave();corrupt.studyReadingOpen=true;corrupt.studyDialoguePage=8;
            Assert.Throws<ArgumentException>(()=>journey.RestoreSave(corrupt));
            Assert.That(FieldJson.Encode(journey.CaptureSave()),Is.EqualTo(before));
        }
        [Test]
        public void AddedScriptureExcludesWebsiteFootnotesAndPreservesVerifiedVerseWords()
        {
            var reading=GospelStudyCatalog.GetReading("temple-dedication");
            Assert.That(reading.Text,Is.EqualTo("22 모세의 법대로 결례의 날이 차매 아기를 데리고 예루살렘에 올라가니\n23 이는 주의 율법에 쓴바 첫 태에 처음 난 남자마다 주의 거룩한 자라 하리라 한대로 아기를 주께 드리고\n24 또 주의 율법에 말씀하신대로 비둘기 한 쌍이나 혹 어린 반구 둘로 제사하려함이더라"));
            Assert.That(GospelStudyCatalog.AllReadings.Select(r=>r.Id).Distinct().Count(),Is.EqualTo(15));
            foreach(var encounter in GospelStudyCatalog.Encounters)
            {
                Assert.That(encounter.Reading.Text,Does.Not.Contain("ㄱ)"));
                Assert.That(encounter.Reading.Text,Does.Not.Contain("레 12"));
                Assert.That(encounter.Reading.SourceUrl,Is.EqualTo(GospelCatalog.ScriptureSource));
                Assert.That(GospelStudyQuestions.For(encounter.Reading.Id).Length,Is.EqualTo(4));
            }
        }
    }
}
