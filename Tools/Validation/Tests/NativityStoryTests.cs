using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PathOfDawn.Core;

namespace FirstGame.Validation
{
    public sealed class NativityStoryTests
    {
        [Test]
        public void FiftyOneShortOpeningStepsRetainAllLegacyEventAndPassageSlots()
        {
            var opening = GospelChronicleCatalog.Quests.TakeWhile(q => q.IsPrologue).ToArray();
            Assert.That(opening.Length, Is.EqualTo(11));
            Assert.That(GospelChronicleCatalog.Quests.Count, Is.EqualTo(23));
            Assert.That(GospelChronicleCatalog.Readings.Count, Is.EqualTo(19));
            Assert.That(opening.Sum(GospelStorybookCatalog.GetStoryBeatCount), Is.EqualTo(51));
            foreach (var quest in GospelChronicleCatalog.Quests)
            {
                Assert.That(GospelStorybookCatalog.GetBeats(quest).Count, Is.EqualTo(quest.Dialogue.Count));
                int flattened = 0;
                for (int legacy = 0; legacy < quest.Dialogue.Count; legacy++)
                {
                    var details = GospelStorybookCatalog.GetDetails(quest, legacy);
                    Assert.That(details.Count, Is.GreaterThan(0));
                    for (int detail = 0; detail < details.Count; detail++)
                    {
                        var step = details[detail];
                        Assert.That(GospelStorybookCatalog.GetStoryBeatIndex(quest, legacy, detail), Is.EqualTo(flattened++));
                        Assert.That(step.Reference, Is.Not.Empty);
                        Assert.That(step.Turn.Text, Is.Not.Empty);
                        Assert.That(step.Turn.Text.Length, Is.LessThan(300), "Each visible narration should fit a short, readable turn.");
                        if (step.Turn.IsVerbatim)
                        {
                            Assert.That(quest.Reading, Is.Not.Null);
                            Assert.That(quest.Reading.Text, Does.Contain(step.Turn.Text), quest.Id);
                            if (quest.IsPrologue) Assert.That(step.Reference, Does.Contain("개역한글 직접 인용"));
                        }
                        if (step.Turn.Speaker == "예수님") Assert.That(step.Turn.IsVerbatim, Is.True);
                        if (step.Turn.IsDramatized) Assert.That(step.Turn.IsVerbatim, Is.False);
                    }
                }
                Assert.That(flattened, Is.EqualTo(GospelStorybookCatalog.GetStoryBeatCount(quest)));
            }
        }

        [Test]
        public void OldJsonWithoutOptionalDetailResumesItsOriginalLegacyPageWithoutFutureAcquisition()
        {
            var journey = GospelJourney.NewChronicleJourney();
            var old = journey.CaptureSave();
            old.chronicle.dialoguePage = 1;
            old.chronicle.storyDetailPage = 0;
            string original = FieldJson.Encode(old).Replace(",\"storyDetailPage\":0", "").Replace("\"storyDetailPage\":0,", "");
            Assert.That(original, Does.Not.Contain("storyDetailPage"));
            var loaded = GospelJourney.FromSave(FieldJson.Decode<GospelSave>(original));
            Assert.That(loaded.Chronicle.QuestIndex, Is.Zero);
            Assert.That(loaded.Chronicle.DialogueIndex, Is.EqualTo(1));
            Assert.That(loaded.Chronicle.StoryDetailIndex, Is.Zero);
            Assert.That(loaded.Chronicle.StoryBeatIndex, Is.EqualTo(7));
            Assert.That(loaded.Chronicle.StoryVoiceKey, Is.EqualTo("birth-road-bethlehem:7"));
            Assert.That(loaded.Chronicle.CurrentStoryReference, Does.StartWith("누가복음 2:1–3"));
            Assert.That(loaded.Chronicle.DiscoveredCount, Is.Zero);
            Assert.That(loaded.Chronicle.BookPages.All(p => p.Reading == null), Is.True);
            Assert.That(old.chronicle.storyDetailPage, Is.Zero);
            Assert.That(old.chronicle.dialoguePage, Is.EqualTo(1), "Reading the legacy object cannot mutate its source cursor.");
        }

        [Test]
        public void MidDetailRoundTripKeepsVisibleTurnAndAwardsOnlyAtTheActualEventBoundary()
        {
            var journey = GospelJourney.NewChronicleJourney();
            var story = journey.Chronicle;
            int count = story.StoryBeatCount;
            for (int index = 0; index < count; index++)
            {
                Assert.That(story.StoryBeatIndex, Is.EqualTo(index));
                string text = story.CurrentStoryBeat.Text, key = story.StoryVoiceKey, reference = story.CurrentStoryReference;
                string json = FieldJson.Encode(journey.CaptureSave());
                var restored = GospelJourney.FromSave(FieldJson.Decode<GospelSave>(json));
                Assert.That(FieldJson.Encode(restored.CaptureSave()), Is.EqualTo(json));
                Assert.That(restored.Chronicle.StoryBeatIndex, Is.EqualTo(index));
                Assert.That(restored.Chronicle.StoryVoiceKey, Is.EqualTo(key));
                Assert.That(restored.Chronicle.CurrentStoryBeat.Text, Is.EqualTo(text));
                Assert.That(restored.Chronicle.CurrentStoryReference, Is.EqualTo(reference));
                Assert.That(story.AdvanceStory(out var acquired), Is.True);
                Assert.That(story.DiscoveredCount, Is.EqualTo(index + 1 == count ? 1 : 0));
                Assert.That(acquired == null, Is.EqualTo(index + 1 != count));
            }
            Assert.That(story.QuestIndex, Is.EqualTo(1));
            Assert.That(story.StoryDetailIndex, Is.Zero);
            Assert.That(story.StoryBeatIndex, Is.Zero);
            Assert.That(story.BookPages[0].ReadingId, Is.EqualTo("road-bethlehem"));
            Assert.That(story.BookPages.Skip(1).All(p => p.Reading == null), Is.True);
        }

        [Test]
        public void InvalidOptionalDetailCursorIsRejectedWithoutReplacingTheCurrentJourney()
        {
            var journey = GospelJourney.NewChronicleJourney();
            Assert.That(journey.Chronicle.AdvanceStory(out _), Is.True);
            string before = FieldJson.Encode(journey.CaptureSave());
            foreach (int invalid in new[] { -1, 7, int.MaxValue })
            {
                var save = journey.CaptureSave();
                save.chronicle.storyDetailPage = invalid;
                Assert.Throws<ArgumentException>(() => journey.RestoreSave(save));
                Assert.That(FieldJson.Encode(journey.CaptureSave()), Is.EqualTo(before));
            }
            while (journey.Chronicle.CurrentQuest.IsPrologue) journey.Chronicle.AdvanceDialogue();
            var travel = journey.CaptureSave();
            travel.chronicle.storyDetailPage = 1;
            Assert.Throws<ArgumentException>(() => journey.RestoreSave(travel));
            Assert.That(journey.Chronicle.Stage, Is.EqualTo(GospelQuestStage.Travel));
        }

        [Test]
        public void AnnunciationAndJosephPrecedeTheCensusAndOtherGospelSourcesRemainInternal()
        {
            var story = GospelJourney.NewChronicleJourney().Chronicle;
            var references = new List<string>();
            var art = new List<string>();
            while (story.QuestIndex == 0)
            {
                references.Add(story.CurrentStoryReference);
                art.Add(story.CurrentStoryScene.ArtKey);
                story.AdvanceStory(out _);
            }
            Assert.That(references.Take(4).All(r => r.StartsWith("누가복음 1:", StringComparison.Ordinal)), Is.True);
            Assert.That(references.Skip(4).Take(3).All(r => r.StartsWith("마태복음 1:", StringComparison.Ordinal)), Is.True);
            Assert.That(references[7], Does.StartWith("누가복음 2:1–3"));
            Assert.That(art[0], Does.EndWith("nativity-annunciation"));
            Assert.That(art[3], Does.EndWith("nativity-visitation"));
            Assert.That(art[4], Does.EndWith("nativity-joseph"));
            var growth = GospelChronicleCatalog.Quests.Single(q => q.Id == "birth-nazareth-growth");
            var matthew = GospelStorybookCatalog.GetDetails(growth, 0).Take(4).ToArray();
            Assert.That(matthew.All(s => s.Reference.StartsWith("마태복음 2:", StringComparison.Ordinal)), Is.True);
            Assert.That(matthew.All(s => s.Scene.TimeLabel == "탄생 뒤"), Is.True);
            Assert.That(matthew[0].Turn.Text, Does.Contain("집에 들어가"));
            Assert.That(matthew[0].Turn.Text, Does.Not.Contain("세 명"));
        }
    }
}
