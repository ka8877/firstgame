using System;
using System.Linq;
using NUnit.Framework;
using PathOfDawn.Core;

namespace FirstGame.Validation
{
    public sealed class GospelStorybookTests
    {
        static void EnterEvent(GospelChronicle story)
        {
            if (story.Stage == GospelQuestStage.Travel)
                Assert.That(story.TryArrive(story.CurrentQuest.LandmarkId, story.CurrentQuest.X, out _), Is.True);
            if (story.Stage == GospelQuestStage.Talk)
                Assert.That(story.TryTalk(story.CurrentQuest.NpcId, out _), Is.True);
        }

        [Test]
        public void EveryRecordedEventHasAStorySceneAndTheExactLegacyCursorRange()
        {
            Assert.That(GospelChronicleCatalog.Quests.Count, Is.EqualTo(23));
            foreach (var quest in GospelChronicleCatalog.Quests)
            {
                var scene = GospelStorybookCatalog.GetScene(quest);
                var beats = GospelStorybookCatalog.GetBeats(quest);
                Assert.That(scene.Title, Is.Not.Empty, quest.Id);
                Assert.That(scene.Location, Is.Not.Empty, quest.Id);
                Assert.That(scene.TimeLabel, Is.Not.Empty, quest.Id);
                Assert.That(scene.ArtKey, Does.StartWith("Art/GospelStorybook/"));
                Assert.That(scene.Cue, Is.Not.Empty, quest.Id);
                Assert.That(beats.Count, Is.EqualTo(quest.Dialogue.Count), quest.Id + " changed a saved dialogue index.");
                Assert.That(beats.All(b => !string.IsNullOrWhiteSpace(b.Text)), Is.True);
            }
        }

        [Test]
        public void RecordedSpeechesAreExactSourceExcerptsAndInventedJesusSpeechIsAbsent()
        {
            int speeches = 0;
            foreach (var quest in GospelChronicleCatalog.Quests)
                foreach (var beat in GospelStorybookCatalog.GetBeats(quest))
                {
                    if (beat.Speaker == "예수님") Assert.That(beat.IsVerbatim, Is.True);
                    if (beat.IsDramatized)
                    {
                        Assert.That(beat.IsVerbatim, Is.False);
                        Assert.That(beat.Label, Does.Contain("성경 직접 인용 아님"));
                    }
                    if (!beat.IsVerbatim) continue;
                    speeches++;
                    Assert.That(quest.Reading, Is.Not.Null, quest.Id);
                    Assert.That(quest.Reading.Text, Does.Contain(beat.Text), quest.Id);
                    Assert.That(beat.Text, Is.Not.EqualTo(quest.Reading.Text), "Full readings belong to the acquired book.");
                }
            Assert.That(speeches, Is.GreaterThanOrEqualTo(8));
        }

        [Test]
        public void FinishingAnEventAwardsItsBookPageWithoutOpeningOrAcknowledgingAReading()
        {
            var journey = GospelJourney.NewChronicleJourney();
            var story = journey.Chronicle;
            string firstId = story.CurrentQuest.ReadingId;
            Assert.That(story.BookPages.All(p => !p.IsDiscovered && p.Reading == null), Is.True);
            for (int i = 0; i < story.DialogueCount - 1; i++)
            {
                Assert.That(story.CurrentStoryBeat, Is.Not.Null);
                Assert.That(story.AdvanceStory(out var notYet), Is.True);
                Assert.That(notYet, Is.Null);
                Assert.That(story.DiscoveredCount, Is.Zero);
            }
            Assert.That(story.AdvanceStory(out var acquired), Is.True);
            Assert.That(acquired.Id, Is.EqualTo(firstId));
            Assert.That(story.DiscoveredCount, Is.EqualTo(1));
            Assert.That(story.BookPages[0].Text, Is.EqualTo(acquired.Text));
            Assert.That(story.BookPages.Skip(1).All(p => !p.IsDiscovered && p.Reading == null), Is.True);
            Assert.That(journey.AdvanceReading(), Is.False, "The old read-to-advance API must not become a bypass.");
        }

        [Test]
        public void EveryOldMidDialogueCursorStillLoadsWithoutChangingBytesOrAwardingFuturePages()
        {
            var journey = GospelJourney.NewChronicleJourney();
            int count = 0;
            while (!journey.Chronicle.IsComplete)
            {
                var story = journey.Chronicle;
                EnterEvent(story);
                string original = FieldJson.Encode(journey.CaptureSave());
                var restored = GospelJourney.FromSave(FieldJson.Decode<GospelSave>(original));
                Assert.That(FieldJson.Encode(restored.CaptureSave()), Is.EqualTo(original));
                Assert.That(restored.Chronicle.CurrentStoryScene.Title, Is.EqualTo(story.CurrentStoryScene.Title));
                Assert.That(restored.Chronicle.CurrentStoryBeat.Text, Is.EqualTo(story.CurrentStoryBeat.Text));
                Assert.That(restored.Chronicle.DiscoveredCount, Is.EqualTo(story.DiscoveredCount));
                Assert.That(story.AdvanceStory(out _), Is.True);
                count++;
            }
            Assert.That(count, Is.GreaterThan(45));
            Assert.That(journey.Chronicle.DiscoveredCount, Is.EqualTo(19));
        }

        [Test]
        public void EntireStoryAwardsNineteenDistinctPassagesForTwentyThreeOrderedEventsOnlyOnce()
        {
            var journey = GospelJourney.NewChronicleJourney();
            var story = journey.Chronicle;
            var acquiredIds = new System.Collections.Generic.List<string>();
            int eventCount = 0;
            while (!story.IsComplete)
            {
                int index = story.QuestIndex;
                EnterEvent(story);
                while (!story.IsComplete && story.QuestIndex == index)
                {
                    Assert.That(story.CurrentStoryBeat, Is.Not.Null);
                    Assert.That(story.AdvanceStory(out var acquired), Is.True);
                    if (acquired != null) acquiredIds.Add(acquired.Id);
                    journey.ValidateSave(journey.CaptureSave());
                }
                eventCount++;
            }
            Assert.That(eventCount, Is.EqualTo(23));
            Assert.That(acquiredIds.Count, Is.EqualTo(19));
            Assert.That(acquiredIds.Distinct().Count(), Is.EqualTo(19));
            Assert.That(acquiredIds, Is.EqualTo(GospelChronicleCatalog.Readings.Select(r => r.Id)));
            Assert.That(story.AdvanceStory(out var duplicate), Is.False);
            Assert.That(duplicate, Is.Null);
            Assert.That(story.CurrentStoryBeat, Is.Null);
            Assert.That(story.CurrentStoryScene, Is.Null);
        }

        [Test]
        public void OpeningTheBookCannotAdvanceTheStoryOrRevealFutureText()
        {
            var journey = GospelJourney.NewChronicleJourney();
            string before = FieldJson.Encode(journey.CaptureSave());
            var story = journey.Chronicle;
            for (int repeat = 0; repeat < 5; repeat++)
            {
                foreach (var page in story.BookPages)
                {
                    Assert.That(page.IsDiscovered, Is.False);
                    Assert.That(page.Title, Is.Empty);
                    Assert.That(page.Reference, Is.Empty);
                    Assert.That(page.Text, Is.Empty);
                }
                _ = story.CurrentStoryScene;
                _ = story.CurrentStoryBeat;
            }
            Assert.That(FieldJson.Encode(journey.CaptureSave()), Is.EqualTo(before));
        }
    }
}
