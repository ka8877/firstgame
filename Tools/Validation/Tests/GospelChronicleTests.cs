using System;
using System.Linq;
using NUnit.Framework;
using PathOfDawn.Core;

namespace FirstGame.Validation
{
    public sealed class GospelChronicleTests
    {
        static void FinishCurrent(GospelChronicle story)
        {
            int index=story.QuestIndex;
            if(story.Stage==GospelQuestStage.Travel)
                Assert.That(story.TryArrive(story.CurrentQuest.LandmarkId,story.CurrentQuest.X,out _),Is.True);
            if(story.Stage==GospelQuestStage.Talk)
                Assert.That(story.TryTalk(story.CurrentQuest.NpcId,out _),Is.True);
            while(!story.IsComplete&&story.QuestIndex==index)Assert.That(story.AdvanceDialogue(),Is.True);
        }
        static GospelJourney AtTemple()
        {
            var journey=GospelJourney.NewChronicleJourney();
            while(journey.Chronicle.CurrentQuest.IsPrologue)FinishCurrent(journey.Chronicle);
            return journey;
        }
        static GospelJourney CompletedLegacy(bool study)
        {
            var j=study?GospelJourney.NewStudyJourney():GospelJourney.New();
            if(study)j.FinishOpening();
            while(j.Phase==GospelPhase.Prologue)j.AdvanceReading();
            while(j.StudyPending)
            {
                j.TryStudyEncounter(j.CurrentStudy.Reading.Id);
                while(j.StudyReadingOpen)j.AdvanceStudyDialogue();
            }
            while(!j.IsComplete) { j.TryInteract(j.CurrentObjective.Id,out _);j.AdvanceReading(); }
            return j;
        }

        [Test] public void AllFifteenExistingReadingsRemainTheExactSameObjectsAndFourAdultSourcesAreSpecific()
        {
            foreach(var reading in GospelStudyCatalog.AllReadings)
                Assert.That(GospelChronicleCatalog.GetReading(reading.Id),Is.SameAs(reading));
            Assert.That(GospelChronicleCatalog.Readings.Count,Is.EqualTo(19));
            Assert.That(GospelChronicleCatalog.Readings.Select(r=>r.Id).Distinct().Count(),Is.EqualTo(19));
            Assert.That(GospelChronicleCatalog.GetReading("baptism-water").SourceUrl,Does.Contain("book=mat&chap=3"));
            Assert.That(GospelChronicleCatalog.GetReading("john-jordan").SourceUrl,Does.Contain("book=luk&chap=3"));
            Assert.That(GospelChronicleCatalog.GetReading("ministry-thirty").Text,Does.Contain("삼십세쯤"));
        }

        [Test] public void FreshBookExposesNoUndiscoveredIdTitleReferenceTextOrReadingObject()
        {
            var story=GospelJourney.NewChronicleJourney().Chronicle;
            Assert.That(story.DiscoveredCount,Is.Zero);
            Assert.That(story.BookPages.Select(p=>p.Number),Is.EqualTo(Enumerable.Range(1,19)));
            foreach(var page in story.BookPages)
            {
                Assert.That(page.IsDiscovered,Is.False);Assert.That(page.Reading,Is.Null);
                Assert.That(page.Title,Is.Empty);Assert.That(page.Reference,Is.Empty);
                Assert.That(page.Text,Is.Empty);Assert.That(page.ReadingId,Is.Empty);
            }
            Assert.That(story.Journal.Count,Is.EqualTo(1),"Journal must not reveal the titles of all future objectives.");
        }

        [Test] public void BirthPrologueOrdersTheActualRecordedEventsAndAwardsOnlyAcknowledgedPages()
        {
            var journey=GospelJourney.NewChronicleJourney();var story=journey.Chronicle;
            string[] expected={"road-bethlehem","birth-manger","night-shepherds","birth-good-news","manger-witnesses","birth-remembered","name-jesus","temple-dedication","simeon-peace","anna-thanksgiving","nazareth-growth"};
            for(int i=0;i<expected.Length;i++)
            {
                Assert.That(story.CurrentQuest.IsPrologue,Is.True);
                Assert.That(story.CurrentReading.Id,Is.EqualTo(expected[i]));
                Assert.That(story.Stage,Is.EqualTo(GospelQuestStage.Reading));
                Assert.That(story.TryArrive(story.CurrentQuest.Id,0,out _),Is.False);
                Assert.That(story.AdvanceDialogue(),Is.True);
                Assert.That(story.DiscoveredCount,Is.EqualTo(i),"Merely opening or advancing to the quote does not award it.");
                FinishCurrent(story);
                Assert.That(story.DiscoveredCount,Is.EqualTo(i+1));
                Assert.That(story.BookPages[i].Reading.Id,Is.EqualTo(expected[i]));
                journey.ValidateSave(journey.CaptureSave());
            }
            Assert.That(story.ChapterId,Is.EqualTo("temple"));Assert.That(story.IsAdult,Is.False);
            Assert.That(story.Stage,Is.EqualTo(GospelQuestStage.Travel));
            Assert.That(story.AgeLabel,Does.Contain("열두 살"));
        }

        [Test] public void TempleRequiresArrivalCorrectNpcAndCompleteDialogueInSequence()
        {
            var journey=AtTemple();var story=journey.Chronicle;var quest=story.CurrentQuest;
            Assert.That(story.AdvanceDialogue(),Is.False);
            Assert.That(story.TryTalk(quest.NpcId,out _),Is.False);
            Assert.That(story.TryArrive("parents-reunite",quest.X,out _),Is.False);
            Assert.That(story.TryArrive(quest.Id,quest.X+GospelChronicle.InteractionRadius+.01f,out _),Is.False);
            Assert.That(story.TryArrive(quest.Id,float.NaN,out _),Is.False);
            Assert.That(story.TryArrive(quest.Id,quest.X,out _),Is.True);
            Assert.That(story.TryTalk("mary",out _),Is.False);
            Assert.That(story.TryTalk("joseph",out _),Is.True);
            Assert.That(story.DiscoveredCount,Is.EqualTo(11));
            FinishCurrent(story);
            Assert.That(story.CurrentQuest.Id,Is.EqualTo("pilgrim-spring"));
            Assert.That(story.DiscoveredCount,Is.EqualTo(11),"A travel conversation without a verse is not a scripture reward.");
            Assert.That(journey.AdvanceReading(),Is.False,"Legacy API cannot bypass Chronicle requirements.");
        }

        [Test] public void EntireRpgCompletesNineteenDiscoveriesAcrossTwentyThreeOrderedSteps()
        {
            var journey=GospelJourney.NewChronicleJourney();var story=journey.Chronicle;
            int count=0;
            while(!story.IsComplete)
            {
                Assert.That(story.QuestIndex,Is.EqualTo(count));FinishCurrent(story);count++;
                journey.ValidateSave(journey.CaptureSave());
                if(count==18){Assert.That(story.IsAdult,Is.True);Assert.That(story.ChapterId,Is.EqualTo("baptism"));}
            }
            Assert.That(count,Is.EqualTo(23));Assert.That(story.DiscoveredCount,Is.EqualTo(19));
            Assert.That(story.BookPages.All(p=>p.IsDiscovered),Is.True);
            Assert.That(journey.IsComplete,Is.True);Assert.That(journey.CodexCount,Is.EqualTo(19));
            Assert.That(journey.IsEpisodeUnlocked("baptism"),Is.True);
            Assert.That(journey.IsEpisodeUnlocked("ascension"),Is.False);
            Assert.That(story.AdvanceDialogue(),Is.False);Assert.That(story.CurrentReading,Is.Null);
            Assert.That(story.CurrentDialogue,Is.Null);Assert.That(story.CurrentQuest,Is.Null);
        }

        [Test] public void SaveRestoresEveryTravelTalkAndReadingCursorWithoutAwardingUnseenVerses()
        {
            var journey=AtTemple();
            for(int step=0;step<90&&!journey.Chronicle.IsComplete;step++)
            {
                var story=journey.Chronicle;var json=FieldJson.Encode(journey.CaptureSave());
                var restored=GospelJourney.FromSave(FieldJson.Decode<GospelSave>(json));
                Assert.That(FieldJson.Encode(restored.CaptureSave()),Is.EqualTo(json));
                Assert.That(restored.Chronicle.DiscoveredCount,Is.EqualTo(story.DiscoveredCount));
                if(story.Stage==GospelQuestStage.Travel)story.TryArrive(story.CurrentQuest.Id,story.CurrentQuest.X,out _);
                else if(story.Stage==GospelQuestStage.Talk)story.TryTalk(story.CurrentQuest.NpcId,out _);
                else story.AdvanceDialogue();
                journey=GospelJourney.FromSave(journey.CaptureSave());
            }
            Assert.That(journey.Chronicle.IsComplete,Is.True);
        }

        [TestCase(false,7)] [TestCase(true,15)]
        public void CompletedLegacyCanExplicitlyContinueAsAdultWithoutResetOrFreeMissingPages(bool study,int inherited)
        {
            var journey=CompletedLegacy(study);var before=journey.CaptureSave();
            Assert.That(journey.Chronicle,Is.Null);
            journey.EnableChronicle();var story=journey.Chronicle;var after=journey.CaptureSave();
            Assert.That(story.QuestIndex,Is.EqualTo(GospelChronicleCatalog.FirstAdultQuest));
            Assert.That(story.IsAdult,Is.True);Assert.That(story.DiscoveredCount,Is.EqualTo(inherited));
            Assert.That(after.collectedReadings,Is.EqualTo(before.collectedReadings));
            Assert.That(after.studyReadings,Is.EqualTo(before.studyReadings));
            Assert.That(after.playerX,Is.EqualTo(before.playerX));Assert.That(after.phase,Is.EqualTo(before.phase));
            journey.ValidateSave(after);
            while(!story.IsComplete)FinishCurrent(story);
            Assert.That(story.DiscoveredCount,Is.EqualTo(inherited+4));
            Assert.That(story.BookPages.Count(p=>!p.IsDiscovered),Is.EqualTo(study?0:8));
        }

        [Test] public void PartialLegacyDiscoveryIsRetainedWithoutAwardingOtherBirthPages()
        {
            var journey=GospelJourney.New();journey.AdvanceReading();journey.EnableChronicle();
            Assert.That(journey.Chronicle.DiscoveredCount,Is.EqualTo(1));
            Assert.That(journey.Chronicle.HasReading("birth-manger"),Is.True);
            Assert.That(journey.Chronicle.CurrentReading.Id,Is.EqualTo("road-bethlehem"));
            FinishCurrent(journey.Chronicle);
            Assert.That(journey.Chronicle.CurrentReading.Id,Is.EqualTo("night-shepherds"),"Already inherited manger reading is not awarded twice.");
            journey.ValidateSave(journey.CaptureSave());
        }

        [Test] public void CorruptFutureDiscoveryAndDialogueCursorCannotReplaceCurrentSave()
        {
            var journey=AtTemple();string before=FieldJson.Encode(journey.CaptureSave());
            var corrupt=journey.CaptureSave();corrupt.chronicle.discoveredReadings.Add("baptism-water");
            Assert.Throws<ArgumentException>(()=>journey.RestoreSave(corrupt));
            corrupt=journey.CaptureSave();corrupt.chronicle.stage=GospelQuestStage.Complete;
            Assert.Throws<ArgumentException>(()=>journey.RestoreSave(corrupt));
            corrupt=journey.CaptureSave();corrupt.chronicle.dialoguePage=1;
            Assert.Throws<ArgumentException>(()=>journey.RestoreSave(corrupt));
            corrupt=journey.CaptureSave();corrupt.chronicle.version=0;
            Assert.Throws<ArgumentException>(()=>journey.RestoreSave(corrupt));
            Assert.That(FieldJson.Encode(journey.CaptureSave()),Is.EqualTo(before));
        }

        [Test] public void SnapshotCopiesNeverShareMutableDiscoveryLists()
        {
            var journey=GospelJourney.NewChronicleJourney();FinishCurrent(journey.Chronicle);
            var copied=journey.CaptureSave();copied.chronicle.discoveredReadings.Clear();copied.chronicle.inheritedReadings.Add("birth-manger");
            Assert.That(journey.Chronicle.DiscoveredCount,Is.EqualTo(1));
            Assert.That(journey.CaptureSave().chronicle.inheritedReadings,Is.Empty);
        }

        [Test] public void MissingChroniclePayloadKeepsLegacyAndMissingNestedVersionIsRejected()
        {
            string old="{\"schemaVersion\":1,\"contentVersion\":\"gospel-0.1.0\",\"phase\":0,\"prologuePage\":0,\"objectiveIndex\":0,\"dialoguePage\":0,\"collectedReadings\":[],\"playerX\":0,\"playerY\":0,\"playerZ\":-9,\"playerYaw\":0}";
            var legacy=GospelJourney.FromSave(FieldJson.Decode<GospelSave>(old));
            Assert.That(legacy.Chronicle,Is.Null);Assert.That(legacy.CodexTotal,Is.EqualTo(7));
            var invalid=legacy.CaptureSave();invalid.chronicle=new GospelChronicleSave();
            Assert.Throws<ArgumentException>(()=>GospelJourney.FromSave(invalid));
            Assert.That(legacy.Chronicle,Is.Null);
        }

        [Test] public void NewStoryDoesNotInventJesusDialogueAndLabelsNavigationAsDramatization()
        {
            var turns=GospelChronicleCatalog.Quests.SelectMany(q=>q.Dialogue).ToArray();
            foreach(var turn in turns.Where(t=>t.Speaker=="예수님"))Assert.That(turn.IsVerbatim,Is.True);
            Assert.That(turns.Any(t=>t.IsDramatized),Is.True);
            foreach(var turn in turns.Where(t=>t.IsDramatized))
            {Assert.That(turn.IsVerbatim,Is.False);Assert.That(turn.Label,Does.Contain("성경 직접 인용 아님"));}
            Assert.That(GospelChronicleCatalog.GetReading("baptism-water").Text,Does.Contain("이는 내 사랑하는 아들이요"));
            Assert.That(GospelChronicleCatalog.GetReading("baptism-water").Text,Does.Not.Contain("너는 내 사랑하는 아들이라"));
        }
    }
}
