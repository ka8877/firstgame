using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FirstGame.Shared;
using NUnit.Framework;
using PathOfDawn.Core;
using PathOfDawn.Presentation;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PathOfDawn.SmokeTests
{
    /// <summary>Physical reading surface and exact linked source flow on the actual Boot camera, with isolated saves.</summary>
    public sealed class GospelScriptureReadingSmokeTests
    {
        IDisposable storage;
        bool hadSlot;
        string oldSlot;
        GospelPilgrimageUi ui;

        [UnitySetUp] public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            storage = PersistentSaves.UseBackendForTesting(new MemorySaveBackend());
            hadSlot = PlayerPrefs.HasKey("GOSPEL.activeSlot");
            oldSlot = PlayerPrefs.GetString("GOSPEL.activeSlot", "");
            PlayerPrefs.DeleteKey("GOSPEL.activeSlot");
            yield return SmokeUi.LoadBoot();
            ui = UnityEngine.Object.FindFirstObjectByType<GospelPilgrimageApp>().Ui;
            Assert.That(Camera.main, Is.Not.Null, "Use the actual Boot scene camera for Scripture originals.");
            yield return new WaitForSecondsRealtime(.25f);
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            yield return SmokeUi.UnloadBoot();
            storage?.Dispose();
            if (hadSlot) PlayerPrefs.SetString("GOSPEL.activeSlot", oldSlot);
            else PlayerPrefs.DeleteKey("GOSPEL.activeSlot");
            PlayerPrefs.Save();
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        TMP_Text[] VisibleTexts() => ui.CanvasView.GetComponentsInChildren<TMP_Text>()
            .Where(t => t.gameObject.activeInHierarchy).ToArray();

        Button Button(string label) => ui.CanvasView.GetComponentsInChildren<Button>()
            .Single(b => b.gameObject.activeInHierarchy && b.GetComponentsInChildren<TMP_Text>().Any(t => t.text == label));

        static GospelChronicle CompleteChronicle()
        {
            var chronicle = GospelJourney.NewChronicleJourney().Chronicle;
            int safety = 0;
            while (!chronicle.IsComplete && safety++ < 1000)
            {
                var quest = chronicle.CurrentQuest;
                if (chronicle.Stage == GospelQuestStage.Travel)
                    Assert.That(chronicle.TryArrive(quest.LandmarkId, quest.X, out _), Is.True);
                else if (chronicle.Stage == GospelQuestStage.Talk)
                    Assert.That(chronicle.TryTalk(quest.NpcId, out _), Is.True);
                else Assert.That(chronicle.AdvanceStory(out _), Is.True);
            }
            Assert.That(chronicle.IsComplete, Is.True, "Complete the actual catalog without injecting discoveries.");
            Assert.That(chronicle.DiscoveredCount, Is.EqualTo(chronicle.TotalReadings));
            return chronicle;
        }

        [UnityTest] public IEnumerator DialogueShowsTheWordsAndSpeakerWithoutPerLineEditorialMetadata()
        {
            var story = GospelJourney.NewChronicleJourney().Chronicle;
            string provenance = story.CurrentStoryReference, voiceKey = story.StoryVoiceKey;
            var beat = story.CurrentStoryBeat;
            int advances = 0, books = 0;
            ui.OnRead = () => books++;
            ui.ShowStory(story, () => advances++);
            yield return new WaitForSecondsRealtime(.25f);
            var shown = VisibleTexts().Select(t => t.text).ToArray();
            Assert.That(shown, Does.Contain(beat.Text));
            Assert.That(shown, Does.Contain(beat.Speaker));
            Assert.That(shown, Does.Not.Contain("극화 대화"));
            Assert.That(shown, Does.Not.Contain("성경 직접 인용"));
            var panel = ui.CanvasView.GetComponentsInChildren<RectTransform>().Single(t => t.name == "Story dialogue");
            var topRow = panel.GetComponentsInChildren<TMP_Text>()
                .Where(t => t.transform.parent == panel && t.rectTransform.anchoredPosition.y > -60).ToArray();
            CollectionAssert.AreEqual(new[] { beat.Speaker }, topRow.Select(t => t.text).ToArray(),
                "The dialogue heading contains its speaker only; a narrator named 이야기 is legitimate speech, not an editorial badge.");
            if (!string.IsNullOrEmpty(provenance)) Assert.That(shown, Does.Not.Contain(provenance));
            Assert.That(story.CurrentStoryReference, Is.EqualTo(provenance), "Presentation must retain internal source provenance.");
            Assert.That(story.StoryVoiceKey, Is.EqualTo(voiceKey));
            Button("성경").onClick.Invoke(); Button("이야기 계속").onClick.Invoke();
            Assert.That(books, Is.EqualTo(1)); Assert.That(advances, Is.EqualTo(1));
            var dialogue = VisibleTexts().Single(t => t.gameObject.name == "Current story dialogue text");
            Assert.That(dialogue.transform.parent.parent.parent.GetComponent<ScrollRect>(), Is.Not.Null,
                "Long spoken lines must remain fully readable by ordinary scrolling.");
        }

        [UnityTest] public IEnumerator EveryDiscoveredPassageFlowsAcrossTwoColumnsWithoutOmittingOrDuplicatingPrintedCharacters()
        {
            var story = CompleteChronicle();
            var pages = story.BookPages;
            for (int index = 0; index < pages.Count; index++)
            {
                ui.FocusBiblePage(index); ui.ShowBible(pages, story.DiscoveredCount);
                yield return null;
                Canvas.ForceUpdateCanvases();
                var reading = pages[index].Reading;
                var first = VisibleTexts().OfType<TextMeshProUGUI>().Single(t => t.gameObject.name == "Discovered Scripture text " + reading.Id);
                var second = VisibleTexts().OfType<TextMeshProUGUI>().Single(t => t.gameObject.name == "Discovered Scripture continuation " + reading.Id);
                Assert.That(first.text, Is.EqualTo(reading.Text)); Assert.That(second.text, Is.EqualTo(reading.Text));
                Assert.That(first.richText, Is.False); Assert.That(second.richText, Is.False);
                Assert.That(first.overflowMode, Is.EqualTo(TextOverflowModes.Linked));
                Assert.That(first.linkedTextComponent, Is.SameAs(second));
                first.ForceMeshUpdate(); second.ForceMeshUpdate();
                Assert.That(second.firstVisibleCharacter, Is.GreaterThan(0), "The second print column must contain the continuation: " + reading.Id);
                var printed = new List<int>();
                foreach (var column in new[] { first, second })
                    for (int c = 0; c < column.textInfo.characterCount; c++)
                    {
                        var character = column.textInfo.characterInfo[c];
                        if (character.isVisible) printed.Add(character.index);
                    }
                var expected = Enumerable.Range(0, reading.Text.Length).Where(i => !char.IsWhiteSpace(reading.Text[i])).ToArray();
                CollectionAssert.AreEquivalent(expected, printed, "Every original printed character must occur once across both columns: " + reading.Id);
                var scroll = first.transform.parent.parent.parent.GetComponent<ScrollRect>();
                Assert.That(scroll, Is.Not.Null); Assert.That(scroll.horizontal, Is.False);
                Assert.That(scroll.content.rect.height, Is.GreaterThanOrEqualTo(scroll.viewport.rect.height));
                float deepestGlyph = 0;
                foreach (var column in new[] { first, second })
                    for (int c = 0; c < column.textInfo.characterCount; c++)
                    {
                        var character = column.textInfo.characterInfo[c];
                        if (character.isVisible) deepestGlyph = Mathf.Max(deepestGlyph,
                            -scroll.content.InverseTransformPoint(column.transform.TransformPoint(character.bottomLeft)).y);
                    }
                Assert.That(deepestGlyph, Is.LessThanOrEqualTo(scroll.content.rect.height + 1),
                    "The last printed glyph must be reachable within the actual shared scroll extent: " + reading.Id);
                Assert.That(story.DiscoveredCount, Is.EqualTo(story.TotalReadings));
            }
            yield return SmokeUi.Capture("PathOfDawn", "canon-scripture-last-spread", 1280, 720, settleSeconds: .35f);
            yield return SmokeUi.Capture("PathOfDawn", "canon-scripture-last-spread-mobile", 844, 390, settleSeconds: .35f);
        }

        [UnityTest] public IEnumerator BlankLeavesHideAllFutureMetadataAndPageTurnsKeepTheDiscoveredCursor()
        {
            var story = GospelJourney.NewChronicleJourney().Chronicle;
            ui.ShowBible(story.BookPages, 0); yield return null;
            foreach (var reading in GospelChronicleCatalog.Readings)
            {
                var shown = VisibleTexts().Select(t => t.text).ToArray();
                Assert.That(shown, Does.Not.Contain(reading.Title));
                Assert.That(shown, Does.Not.Contain(reading.Reference));
                Assert.That(shown, Does.Not.Contain(reading.Text));
            }
            Assert.That(VisibleTexts().Any(t => t.name.StartsWith("Discovered Scripture", StringComparison.Ordinal)), Is.False);
            var complete = CompleteChronicle(); int turns = 0, resumed = 0;
            ui.OnPageTurn = () => turns++; ui.OnResume = () => resumed++;
            ui.FocusBiblePage(0); ui.ShowBible(complete.BookPages, complete.DiscoveredCount);
            Button("다음 면").onClick.Invoke();
            Assert.That(turns, Is.EqualTo(1));
            Assert.That(ui.CanvasView.GetComponentsInChildren<Transform>().Any(t => t.name == "Turning Scripture leaf"), Is.True);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(ui.CanvasView.GetComponentsInChildren<Transform>().Any(t => t.name == "Turning Scripture leaf"), Is.False);
            Assert.That(Button("이전 면").interactable, Is.True);
            Button("최근 말씀").onClick.Invoke(); yield return null;
            Assert.That(Button("다음 면").interactable, Is.False);
            Button("여정으로").onClick.Invoke(); Assert.That(resumed, Is.EqualTo(1));
            Assert.That(complete.DiscoveredCount, Is.EqualTo(complete.TotalReadings));
            Assert.That(story.DiscoveredCount, Is.Zero);
        }
    }
}
