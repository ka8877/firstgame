using System.Linq;
using NUnit.Framework;
using PathOfDawn.Core;

namespace FirstGame.Validation
{
    public sealed class GospelSceneConversationTests
    {
        static readonly string[] TempleEvents = {
            "nazareth-departure", "pilgrim-spring", "festival-courtyard",
            "listening-court", "teachers-circle", "parents-reunite", "nazareth-return"
        };

        static GospelChronicleQuest Quest(string id)
            => GospelChronicleCatalog.Quests.Single(q => q.Id == id);

        static void AssertSceneLanguage(string text, string context)
        {
            foreach (string lecture in new[] {
                "누가는", "누가복음은", "마태는", "마태복음은", "마태복음에는", "마태복음에서는",
                "복음서는", "복음서가 전하는", "누가와 마태", "이번 장", "다음 장", "여러분",
                "학습", "게임을 위한", "기록되지 않은", "임의의 사건", "구분되는 기록",
                "같은 분입니다", "이름을 얻는 이야기"
            })
                Assert.That(text, Does.Not.Contain(lecture), context + " must describe the scene rather than explain its sources or the game.");
        }

        [Test]
        public void EveryVisibleStoryDetailAndLegacyObjectiveStaysInsideTheScene()
        {
            foreach (var quest in GospelChronicleCatalog.Quests)
            {
                AssertSceneLanguage(quest.Objective, quest.Id + " objective");
                foreach (var legacy in quest.Dialogue) AssertSceneLanguage(legacy.Text, quest.Id + " legacy dialogue");
                foreach (var beat in GospelStorybookCatalog.GetBeats(quest)) AssertSceneLanguage(beat.Text, quest.Id + " story beat");
                for (int legacy = 0; legacy < quest.Dialogue.Count; legacy++)
                    foreach (var detail in GospelStorybookCatalog.GetDetails(quest, legacy))
                    {
                        AssertSceneLanguage(detail.Turn.Text, quest.Id + " visible detail");
                        Assert.That(detail.Reference, Is.Not.Empty, "Sources stay available internally.");
                        if (detail.Scene == null) continue;
                        AssertSceneLanguage(detail.Scene.Title, quest.Id + " scene title");
                        AssertSceneLanguage(detail.Scene.Location, quest.Id + " scene location");
                        AssertSceneLanguage(detail.Scene.TimeLabel, quest.Id + " scene time");
                        AssertSceneLanguage(detail.Scene.Cue, quest.Id + " scene cue");
                    }
            }
        }

        [Test]
        public void AnnunciationAndJosephDreamUseCharacterSpeechWithoutClaimingNewVerbatimBibleText()
        {
            var quest = Quest("birth-road-bethlehem");
            var turns = GospelStorybookCatalog.GetDetails(quest, 0);
            foreach (var item in new[] {
                new { Index = 1, Speaker = "가브리엘", Source = "누가복음 1:28–35" },
                new { Index = 2, Speaker = "마리아", Source = "누가복음 1:38" },
                new { Index = 5, Speaker = "주의 사자", Source = "마태복음 1:20–21" }
            })
            {
                var detail = turns[item.Index];
                Assert.That(detail.Turn.Speaker, Is.EqualTo(item.Speaker));
                Assert.That(detail.Turn.IsDramatized, Is.True);
                Assert.That(detail.Turn.IsVerbatim, Is.False, "A scene paraphrase must not impersonate an exact HAN quotation.");
                Assert.That(detail.Reference, Does.StartWith(item.Source));
                Assert.That(GospelStorybookCatalog.GetStoryBeatIndex(quest, 0, item.Index), Is.EqualTo(item.Index));
            }
            Assert.That(GospelStorybookCatalog.GetStoryBeatCount(quest), Is.EqualTo(10));
        }

        [Test]
        public void TempleConversationsStayInsideTheSceneAndKeepTheirSourceClassification()
        {
            foreach (string id in TempleEvents)
            {
                var quest = Quest(id);
                for (int legacy = 0; legacy < quest.Dialogue.Count; legacy++)
                {
                    var detail = GospelStorybookCatalog.GetDetail(quest, legacy, 0);
                    Assert.That(detail.Reference, Is.Not.Empty, id + " retains internal provenance.");
                    foreach (string meta in new[] {
                        "본문에서", "성경에는", "극화", "학습", "여러분", "게임을",
                        "기록되지", "기록은 모든", "누가가 남긴", "걸어 보기 위한"
                    })
                        Assert.That(detail.Turn.Text, Does.Not.Contain(meta), id + " must stay in the encounter.");
                    if (detail.Turn.Speaker == "이야기" || detail.Turn.IsVerbatim) continue;
                    Assert.That(detail.Turn.IsDramatized, Is.True, id + " cannot claim newly authored character words are Scripture.");
                }
            }
            foreach (string id in new[] { "nazareth-departure", "pilgrim-spring", "listening-court" })
            {
                var turns = GospelStorybookCatalog.GetBeats(Quest(id));
                Assert.That(turns.All(t => t.Speaker != "이야기" && t.IsDramatized && !t.IsVerbatim), Is.True,
                    id + " keeps an actual character exchange rather than replacing it with a lecture.");
            }
        }

        [Test]
        public void MaryAndJesusReunionKeepTheExactRecordedWordsAndNarrationKeys()
        {
            var quest = Quest("parents-reunite");
            var turns = GospelStorybookCatalog.GetBeats(quest);
            string[] speakers = { "마리아", "예수님" };
            string[] portraits = { "mary", "jesus" };
            string[] texts = {
                "아이야 어찌하여 우리에게 이렇게 하였느냐 보라 네 아버지와 내가 근심하여 너를 찾았노라",
                "어찌하여 나를 찾으셨나이까 내가 내 아버지 집에 있어야 될 줄을 알지 못하셨나이까"
            };
            for (int index = 0; index < texts.Length; index++)
            {
                Assert.That(turns[index].Speaker, Is.EqualTo(speakers[index]));
                Assert.That(turns[index].PortraitKey, Is.EqualTo(portraits[index]));
                Assert.That(turns[index].Text, Is.EqualTo(texts[index]));
                Assert.That(turns[index].IsVerbatim, Is.True);
                Assert.That(turns[index].IsDramatized, Is.False);
                Assert.That(quest.Reading.Text, Does.Contain(texts[index]));
                Assert.That(quest.Id + ":" + GospelStorybookCatalog.GetStoryBeatIndex(quest, index, 0),
                    Is.EqualTo("parents-reunite:" + index));
            }
        }

        [Test]
        public void SceneConnectionsKeepTheSourceForTheEventTheyActuallyDescribe()
        {
            Assert.That(GospelStorybookCatalog.GetDetail(Quest("festival-courtyard"), 2, 0).Reference,
                Does.StartWith("누가복음 2:43–46"), "The parents' search follows their departure from Jerusalem.");
            Assert.That(GospelStorybookCatalog.GetDetail(Quest("parents-reunite"), 3, 0).Reference,
                Does.StartWith("누가복음 2:51"), "The return to Nazareth follows the recorded exchange.");
            Assert.That(GospelStorybookCatalog.GetDetail(Quest("nazareth-return"), 2, 0).Reference,
                Does.StartWith("마태복음 3:13"), "The adult journey to Jordan is a later recorded event.");
            var reaction = GospelStorybookCatalog.GetDetail(Quest("teachers-circle"), 1, 0);
            Assert.That(reaction.Reference, Does.StartWith("누가복음 2:47"));
            Assert.That(reaction.Turn.IsDramatized, Is.True,
                "The recorded astonishment does not make the new teacher dialogue a verbatim quotation.");
        }

        [Test]
        public void SceneRewritePreservesEveryTempleSaveCursorAndDiscoveryBoundary()
        {
            int[] legacyCounts = { 2, 2, 3, 2, 3, 4, 3 };
            var journey = GospelJourney.NewChronicleJourney();
            var story = journey.Chronicle;
            while (story.CurrentQuest.IsPrologue) story.AdvanceDialogue();
            Assert.That(story.DiscoveredCount, Is.EqualTo(11));
            for (int eventIndex = 0; eventIndex < TempleEvents.Length; eventIndex++)
            {
                string id = TempleEvents[eventIndex];
                Assert.That(story.CurrentQuest.Id, Is.EqualTo(id));
                Assert.That(story.TryArrive(id, story.CurrentQuest.X, out _), Is.True);
                Assert.That(story.TryTalk(story.CurrentQuest.NpcId, out _), Is.True);
                Assert.That(story.DialogueCount, Is.EqualTo(legacyCounts[eventIndex]));
                Assert.That(story.StoryBeatCount, Is.EqualTo(legacyCounts[eventIndex]));
                int found = story.DiscoveredCount;
                var reading = story.CurrentQuest.Reading;
                for (int page = 0; page < legacyCounts[eventIndex]; page++)
                {
                    Assert.That(story.DialogueIndex, Is.EqualTo(page));
                    Assert.That(story.StoryDetailIndex, Is.Zero);
                    Assert.That(story.StoryVoiceKey, Is.EqualTo(id + ":" + page));
                    string original = FieldJson.Encode(journey.CaptureSave());
                    var loaded = GospelJourney.FromSave(FieldJson.Decode<GospelSave>(original));
                    Assert.That(FieldJson.Encode(loaded.CaptureSave()), Is.EqualTo(original));
                    Assert.That(loaded.Chronicle.CurrentStoryBeat.Text, Is.EqualTo(story.CurrentStoryBeat.Text));
                    Assert.That(story.DiscoveredCount, Is.EqualTo(found));
                    Assert.That(story.AdvanceStory(out var acquired), Is.True);
                    bool finishes = page + 1 == legacyCounts[eventIndex];
                    Assert.That(acquired == null, Is.EqualTo(!finishes || reading == null));
                    Assert.That(story.DiscoveredCount, Is.EqualTo(found + (finishes && reading != null ? 1 : 0)));
                }
            }
            Assert.That(story.CurrentQuest.Id, Is.EqualTo("adult-jordan-road"));
            Assert.That(story.DiscoveredCount, Is.EqualTo(15));
            Assert.That(story.QuestCount, Is.EqualTo(23));
            Assert.That(story.TotalReadings, Is.EqualTo(19));
        }

        [Test]
        public void GrowthTransitionKeepsItsTwoUnvoicedDetailSlotsAndMovesIntoPassover()
        {
            var growth = Quest("birth-nazareth-growth");
            var details = GospelStorybookCatalog.GetDetails(growth, 1);
            Assert.That(details.Count, Is.EqualTo(2));
            Assert.That(GospelStorybookCatalog.GetStoryBeatIndex(growth, 1, 0), Is.EqualTo(5));
            Assert.That(GospelStorybookCatalog.GetStoryBeatIndex(growth, 1, 1), Is.EqualTo(6));
            Assert.That(GospelStorybookCatalog.GetStoryBeatCount(growth), Is.EqualTo(7));
            Assert.That(details[1].Turn.Text, Does.Contain("열두 살"));
            Assert.That(details[1].Turn.Text, Does.Contain("유월절"));
            foreach (var detail in details)
            {
                Assert.That(detail.Turn.Speaker, Is.EqualTo("이야기"));
                Assert.That(detail.Turn.IsVerbatim, Is.False);
                Assert.That(detail.Reference, Is.Not.Empty);
                Assert.That(detail.Turn.Text, Does.Not.Contain("기록되지 않은"));
                Assert.That(detail.Turn.Text, Does.Not.Contain("여러분"));
            }
        }
    }
}
