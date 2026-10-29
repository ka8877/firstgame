using System;
using System.Collections;
using System.Linq;
using FirstGame.Shared;
using NUnit.Framework;
using PathOfDawn.Core;
using PathOfDawn.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace PathOfDawn.SmokeTests
{
    // Reuses the isolated storage and physical keyboard setup in RenewalSmokeTests.
    // New-story progress comes exclusively from actual visible UI callbacks and E.
    public sealed partial class RenewalSmokeTests
    {
        string chronicleCase = "unscoped";
        IEnumerator ChronicleShot(string name) => SmokeUi.Capture("PathOfDawn",
            "chronicle-" + attempt + "-" + chronicleCase + "-" + name, 1280, 720);

        IEnumerator ChronicleClick(string label)
        {
            SmokeUi.Click(label);
            yield return null;
            yield return null; // Destroyed modal text must be gone before checking visibility.
        }

        static string[] ChronicleVisibleText() => UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
            .Where(t => t.gameObject.activeInHierarchy).Select(t => t.text).ToArray();

        void AssertChronicleLockedBook()
        {
            Assert.That(App.Ui.CurrentView, Is.EqualTo("bible"));
            var visible = ChronicleVisibleText();
            Assert.That(visible, Does.Contain("아직 펼쳐지지 않은 말씀"));
            foreach (var reading in GospelChronicleCatalog.Readings.Where(r => !App.Chronicle.HasReading(r.Id)))
            {
                Assert.That(visible.Any(t => t.Contains(reading.Title)), Is.False, "Locked title leaked: " + reading.Id);
                Assert.That(visible.Any(t => t.Contains(reading.Reference)), Is.False, "Locked reference leaked: " + reading.Id);
                Assert.That(visible.Any(t => t.Contains(reading.Text)), Is.False, "Locked quote leaked: " + reading.Id);
            }
            Assert.That(GameObject.Find("Discovered Scripture illustration"), Is.Null,
                "A locked book spread must not instantiate the hidden reading illustration.");
        }

        void AssertChronicleOpenSpread(GospelReading reading)
        {
            Assert.That(App.Ui.CurrentView, Is.EqualTo("bible"));
            var visible = ChronicleVisibleText();
            Assert.That(visible, Does.Contain(reading.Title));
            Assert.That(visible, Does.Contain(reading.Reference));
            Assert.That(visible, Does.Contain(reading.Text), "The book must show the exact acquired source quotation.");
            Assert.That(visible, Does.Contain("성경 본문 · 개역한글"));
            var left = GameObject.Find("Bible left leaf");
            var right = GameObject.Find("Bible Scripture leaf");
            Assert.That(left, Is.Not.Null); Assert.That(right, Is.Not.Null);
            Assert.That(left.GetComponent<RectTransform>().anchoredPosition.x,
                Is.LessThan(right.GetComponent<RectTransform>().anchoredPosition.x), "Actual book has facing leaves.");
            Assert.That(GameObject.Find("Discovered Scripture illustration"), Is.Not.Null);
        }

        IEnumerator ChronicleFinishCurrentDialogue()
        {
            int quest = App.Chronicle.QuestIndex;
            int before = App.Chronicle.DiscoveredCount;
            bool hasReading = App.Chronicle.CurrentQuest.Reading != null;
            int guard = 0;
            while (!App.Chronicle.IsComplete && App.Chronicle.QuestIndex == quest && guard++ < 16)
            {
                Assert.That(App.Ui.CurrentView, Is.EqualTo("reading"));
                bool lastTurn = App.Chronicle.DialogueIndex + 1 == App.Chronicle.DialogueCount;
                yield return ChronicleClick("다음 말씀");
                Assert.That(App.Chronicle.DiscoveredCount,
                    Is.EqualTo(before + (lastTurn && hasReading ? 1 : 0)),
                    "Only the final real dialogue callback can award this reading.");
            }
            Assert.That(App.Chronicle.QuestIndex, Is.EqualTo(quest + 1), "Actual dialogue did not advance one ordered quest.");
        }

        IEnumerator ChronicleSaveAndReloadDialogue(string captureName)
        {
            Assert.That(App.Ui.CurrentView, Is.EqualTo("reading"));
            int quest = App.Chronicle.QuestIndex, dialogue = App.Chronicle.DialogueIndex;
            int discovered = App.Chronicle.DiscoveredCount;
            string chapter = App.Chronicle.ChapterId;
            string quotation = App.Chronicle.CurrentReading?.Text;
            float x = App.Player.Position.x;
            yield return ChronicleClick("메뉴");
            Assert.That(App.Ui.CurrentView, Is.EqualTo("pause"));
            yield return ChronicleClick("현재 여정 저장");
            string slot = App.ActiveSaveSlot;
            var envelope = JsonUtility.FromJson<PortableSave>(backend.Read(slot));
            var payload = JsonUtility.FromJson<GospelSave>(envelope.payload);
            Assert.That(payload.chronicle.questIndex, Is.EqualTo(quest));
            Assert.That(payload.chronicle.dialoguePage, Is.EqualTo(dialogue));
            Assert.That(payload.chronicle.stage, Is.EqualTo(GospelQuestStage.Reading));
            yield return SmokeUi.LoadBoot();
            yield return ChronicleClick("이어하기");
            Assert.That(App.ActiveSaveSlot, Is.EqualTo(slot));
            Assert.That(App.Ui.CurrentView, Is.EqualTo("reading"));
            Assert.That(App.Chronicle.QuestIndex, Is.EqualTo(quest));
            Assert.That(App.Chronicle.DialogueIndex, Is.EqualTo(dialogue));
            Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(discovered));
            Assert.That(App.Chronicle.CurrentReading?.Text, Is.EqualTo(quotation));
            Assert.That(App.RenderedChronicleChapter, Is.EqualTo(chapter));
            Assert.That(App.Player.Position.x, Is.EqualTo(x).Within(.08f));
            Assert.That(App.Player.Actor.IsAdult, Is.EqualTo(App.Chronicle.IsAdult));
            yield return ChronicleShot(captureName);
        }

        IEnumerator ChronicleEnterChapter(string chapter, bool adult, int discovered)
        {
            Assert.That(App.Ui.CurrentView, Is.EqualTo("chapter"));
            Assert.That(App.RenderedChronicleChapter, Is.EqualTo(chapter));
            Assert.That(App.Chronicle.ChapterId, Is.EqualTo(chapter));
            Assert.That(App.Chronicle.IsAdult, Is.EqualTo(adult));
            Assert.That(App.Player.Actor.IsAdult, Is.EqualTo(adult));
            Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(discovered));
            Assert.That(App.Player.Enabled, Is.False);
            yield return ChronicleShot(chapter + "-chapter-" + discovered + "-discovered");
            yield return ChronicleClick("이야기 속으로");
            Assert.That(App.Ui.CurrentView, Is.EqualTo("journey"));
            Assert.That(App.Player.Enabled, Is.True);
        }

        IEnumerator ChronicleWalkAndTalk(int index)
        {
            var quest = App.Chronicle.CurrentQuest;
            Assert.That(App.Chronicle.QuestIndex, Is.EqualTo(index));
            Assert.That(App.Chronicle.Stage, Is.EqualTo(GospelQuestStage.Travel));
            Assert.That(App.ChronicleNpcId, Is.EqualTo(quest.NpcId));
            Assert.That(App.ObjectiveX, Is.EqualTo(quest.X));
            Assert.That(App.CanInteract, Is.False, "The next encounter must require walking, not a global verse button.");
            yield return Key(UnityEngine.InputSystem.Key.E);
            Assert.That(App.Chronicle.Stage, Is.EqualTo(GospelQuestStage.Travel));
            Assert.That(App.Ui.CurrentView, Is.EqualTo("journey"));
            // Walk to the reachable edge for the two landmarks at x=34. No pose injection.
            yield return MoveTo(Mathf.Clamp(quest.X, App.Player.MinX, App.Player.MaxX));
            Assert.That(App.CanInteract, Is.True);
            Assert.That(Mathf.Abs(App.Player.Position.x - quest.X), Is.LessThanOrEqualTo(GospelChronicle.InteractionRadius));
            var npc = GameObject.Find("Chronicle encounter · " + quest.NpcName);
            Assert.That(npc, Is.Not.Null);
            Assert.That(npc.transform.position.x, Is.EqualTo(quest.X).Within(.01f));
            yield return Key(UnityEngine.InputSystem.Key.E);
            Assert.That(App.Chronicle.Stage, Is.EqualTo(GospelQuestStage.Reading));
            Assert.That(App.Ui.CurrentView, Is.EqualTo("reading"));
            Assert.That(App.Chronicle.DialogueIndex, Is.Zero);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator ChroniclePhysicalRpgDiscoversNineteenBookPagesAndResumesActualDialogue()
        {
            chronicleCase = "full-route";
            Assert.That(App.Ui.CurrentView, Is.EqualTo("title"));
            yield return ChronicleShot("title");
            yield return ChronicleClick("새 여정 시작");
            Assert.That(App.Ui.CurrentView, Is.EqualTo("reading"));
            Assert.That(App.Chronicle.QuestCount, Is.EqualTo(23));
            Assert.That(App.Chronicle.DiscoveredCount, Is.Zero);
            Assert.That(App.Chronicle.CurrentQuest.IsPrologue, Is.True);
            yield return ChronicleClick("성경");
            AssertChronicleLockedBook();
            yield return ChronicleShot("book-unopened");
            yield return ChronicleClick("여정으로");
            yield return ChronicleClick("다음 말씀");
            Assert.That(App.Chronicle.DialogueIndex, Is.EqualTo(1));
            Assert.That(App.Chronicle.DiscoveredCount, Is.Zero);
            yield return ChronicleSaveAndReloadDialogue("prologue-resumed");
            yield return ChronicleFinishCurrentDialogue();
            Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(1));
            yield return ChronicleClick("성경");
            AssertChronicleOpenSpread(GospelChronicleCatalog.Readings[0]);
            yield return ChronicleShot("book-first-discovery");
            yield return ChronicleClick("다음 면");
            AssertChronicleLockedBook();
            yield return ChronicleClick("여정으로");
            for (int i = 1; i < 11; i++)
            {
                Assert.That(App.Chronicle.QuestIndex, Is.EqualTo(i));
                Assert.That(App.Chronicle.CurrentQuest.IsPrologue, Is.True);
                yield return ChronicleFinishCurrentDialogue();
            }
            yield return ChronicleEnterChapter("temple", false, 11);
            yield return ChronicleActualCharacterPoses("child", false);
            yield return ChronicleShot("child-journey");
            for (int i = 11; i < 18; i++)
            {
                yield return ChronicleWalkAndTalk(i);
                if (i == 13)
                {
                    yield return ChronicleClick("다음 말씀");
                    Assert.That(App.Chronicle.DialogueIndex, Is.EqualTo(1));
                    yield return ChronicleSaveAndReloadDialogue("temple-dialogue-resumed");
                }
                yield return ChronicleFinishCurrentDialogue();
            }
            yield return ChronicleEnterChapter("baptism", true, 15);
            yield return ChronicleActualCharacterPoses("adult", true);
            yield return ChronicleShot("adult-jordan-journey");
            for (int i = 18; i < 23; i++)
            {
                yield return ChronicleWalkAndTalk(i);
                if (i == 19) yield return ChronicleShot("john-baptist-dialogue");
                yield return ChronicleFinishCurrentDialogue();
            }
            Assert.That(App.Journey.IsComplete, Is.True);
            Assert.That(App.Chronicle.QuestIndex, Is.EqualTo(23));
            Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(19));
            Assert.That(App.Ui.CurrentView, Is.EqualTo("journey"));
            yield return Key(UnityEngine.InputSystem.Key.M);
            AssertChronicleOpenSpread(GospelChronicleCatalog.Readings[18]);
            for (int i = 18; i > 0; i--) yield return ChronicleClick("이전 면");
            for (int i = 0; i < 19; i++)
            {
                AssertChronicleOpenSpread(GospelChronicleCatalog.Readings[i]);
                if (i == 17) yield return ChronicleShot("book-baptism-discovered");
                if (i < 18) yield return ChronicleClick("다음 면");
            }
            Assert.That(SmokeUi.FindButton("다음 면").interactable, Is.False);
            yield return ChronicleClick("여정으로");
            yield return Key(UnityEngine.InputSystem.Key.Escape);
            yield return ChronicleClick("현재 여정 저장");
            string completeSlot = App.ActiveSaveSlot;
            yield return SmokeUi.LoadBoot();
            yield return ChronicleClick("이어하기");
            Assert.That(App.ActiveSaveSlot, Is.EqualTo(completeSlot));
            Assert.That(App.Chronicle.IsComplete, Is.True);
            Assert.That(App.Journey.CodexCount, Is.EqualTo(19));
            Assert.That(App.Chronicle.BookPages.All(p => p.IsDiscovered), Is.True);
            Assert.That(App.Player.Actor.IsAdult, Is.True);
            Assert.That(App.RenderedChronicleChapter, Is.EqualTo("baptism"));
            yield return ChronicleShot("complete-resumed");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ChronicleExplicitLegacyContinuationForksSlotWithoutFreeDiscoveries()
        {
            chronicleCase = "legacy-seven";
            // Authored prior-version save fixture, not simulated gameplay or proof of completion.
            // It deliberately omits every later optional field including chronicle and studyJourney.
            const string oldSlot = "chronicle-native-legacy-seven";
            const string oldPayload = "{\"schemaVersion\":1,\"contentVersion\":\"gospel-0.1.0\",\"phase\":3,\"prologuePage\":3,\"objectiveIndex\":4,\"dialoguePage\":0,\"collectedReadings\":[\"birth-manger\",\"birth-good-news\",\"birth-remembered\",\"temple-passover\",\"temple-teachers\",\"temple-parents\",\"temple-nazareth\"],\"playerX\":0,\"playerY\":0,\"playerZ\":-9,\"playerYaw\":0}";
            var direct = JsonUtility.FromJson<GospelSave>(oldPayload);
            var overwrite = new GospelSave(); JsonUtility.FromJsonOverwrite(oldPayload, overwrite);
            string encodedLegacy = JsonUtility.ToJson(overwrite);
            var roundtrip = new GospelSave(); JsonUtility.FromJsonOverwrite(encodedLegacy, roundtrip);
            var explicitZero = new GospelSave();
            JsonUtility.FromJsonOverwrite(oldPayload.Substring(0, oldPayload.Length - 1) + ",\"chronicle\":{\"version\":0}}", explicitZero);
            TestContext.WriteLine("CHRONICLE_JSON_PROBE direct=" + (direct.chronicle == null ? "null" : "version:" + direct.chronicle.version));
            TestContext.WriteLine("CHRONICLE_JSON_PROBE overwrite=" + (overwrite.chronicle == null ? "null" : "version:" + overwrite.chronicle.version));
            TestContext.WriteLine("CHRONICLE_JSON_PROBE legacyEncoded=" + encodedLegacy);
            TestContext.WriteLine("CHRONICLE_JSON_PROBE roundtrip=" + (roundtrip.chronicle == null ? "null" : "version:" + roundtrip.chronicle.version));
            string explicitZeroError = "accepted";
            try { GospelJourney.FromSave(explicitZero); } catch (Exception ex) { explicitZeroError = ex.Message; }
            TestContext.WriteLine("CHRONICLE_JSON_PROBE explicitZero=" + explicitZeroError);
            var decodedLegacy = GospelSaveCodec.Decode(oldPayload);
            Assert.That(decodedLegacy.chronicle, Is.Null, "Only raw top-level absence may clear Unity's synthesized object.");
            Assert.That(GospelJourney.FromSave(decodedLegacy).CodexCount, Is.EqualTo(7));
            string codecLegacy = GospelSaveCodec.Encode(decodedLegacy);
            Assert.That(codecLegacy.Contains("\"chronicle\":"), Is.False, "Legacy writes must omit the synthetic optional object.");
            Assert.That(GospelSaveCodec.Decode(codecLegacy).chronicle, Is.Null);
            Assert.That(GospelJourney.FromSave(GospelSaveCodec.Decode(codecLegacy)).CodexCount, Is.EqualTo(7));
            string rawPrefix = oldPayload.Substring(0, oldPayload.Length - 1);
            Assert.That(GospelSaveCodec.Decode(rawPrefix + ",\"chronicle\":null}").chronicle, Is.Null);
            Assert.That(GospelSaveCodec.Decode(rawPrefix + ",\"unrelated\":{\"chronicle\":{\"version\":0}}}").chronicle, Is.Null,
                "A nested key must not masquerade as the optional top-level field.");
            Assert.That(GospelSaveCodec.Decode(rawPrefix + ",\"unrelated\":\"the word \\\"chronicle\\\" is text\"}").chronicle, Is.Null);
            Assert.That(() => GospelJourney.FromSave(GospelSaveCodec.Decode(rawPrefix + ",\"chronicle\":{\"version\":0}}")), Throws.Exception);
            Assert.That(() => GospelJourney.FromSave(GospelSaveCodec.Decode(rawPrefix + ",\"\\u0063hronicle\":{\"version\":0}}")), Throws.Exception,
                "Escaped real field names must retain strict version rejection.");
            Assert.That(() => GospelSaveCodec.Decode(rawPrefix + ",\"chronicle\":null,\"chronicle\":null}"), Throws.Exception);
            Assert.That(() => GospelSaveCodec.Decode(rawPrefix + ",\"chronicle\":42}"), Throws.Exception);
            Assert.That(() => GospelSaveCodec.Decode(rawPrefix), Throws.Exception);
            Assert.That(() => GospelSaveCodec.Decode(oldPayload + "{}"), Throws.Exception);
            var newSnapshot = GospelJourney.NewChronicleJourney().CaptureSave();
            Assert.That(GospelSaveCodec.Encode(newSnapshot), Is.EqualTo(JsonUtility.ToJson(newSnapshot)),
                "Existing nonnull Chronicle JSON keeps Unity's exact serialization.");
            Assert.That(GospelJourney.FromSave(GospelSaveCodec.Decode(GospelSaveCodec.Encode(newSnapshot))).Chronicle.QuestIndex, Is.Zero);
            TestContext.WriteLine("CHRONICLE_JSON_CODEC old/null/nested/escaped/duplicate/malformed/explicit-v0/legacy-write/new-roundtrip checks passed");
            Assert.That(PersistentSaves.TrySave(oldSlot, GospelJourney.GameId, GospelJourney.SchemaVersion,
                GospelJourney.ContentVersion, oldPayload, out var error), Is.True, error);
            PlayerPrefs.SetString("GOSPEL.activeSlot", oldSlot);
            string originalEnvelope = backend.Read(oldSlot);
            yield return SmokeUi.LoadBoot();
            yield return ChronicleClick("이어하기");
            if (App.Journey == null) yield return ChronicleShot("legacy-load-result-diagnostic");
            Assert.That(App.Journey, Is.Not.Null, "Legacy Continue must restore the actual journey. Visible UI: " + SmokeUi.VisibleUi());
            Assert.That(App.Chronicle, Is.Null, "Loading a legacy slot alone must not opt the user into a new story.");
            Assert.That(App.Journey.IsComplete, Is.True);
            Assert.That(App.Journey.CodexCount, Is.EqualTo(7));
            yield return Key(UnityEngine.InputSystem.Key.J);
            Assert.That(App.Ui.CurrentView, Is.EqualTo("quests"));
            yield return ChronicleClick("요단강의 여정으로");
            Assert.That(App.ActiveSaveSlot, Is.Not.EqualTo(oldSlot));
            Assert.That(App.ActiveSaveSlot, Does.Contain("-chronicle-"));
            Assert.That(backend.Read(oldSlot), Is.EqualTo(originalEnvelope), "The completed old slot must remain byte-for-byte intact.");
            Assert.That(App.Chronicle.QuestIndex, Is.EqualTo(GospelChronicleCatalog.FirstAdultQuest));
            Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(7));
            Assert.That(App.Chronicle.BookPages.Count(p => p.IsDiscovered), Is.EqualTo(7));
            yield return ChronicleEnterChapter("baptism", true, 7);
            yield return Key(UnityEngine.InputSystem.Key.M);
            AssertChronicleLockedBook(); // New first birth page was never read in the old seven-reading journey.
            yield return ChronicleClick("다음 면");
            AssertChronicleOpenSpread(GospelChronicleCatalog.GetReading("birth-manger"));
            yield return ChronicleShot("legacy-preserved-book");
            yield return ChronicleClick("여정으로");
            yield return ChronicleWalkAndTalk(GospelChronicleCatalog.FirstAdultQuest);
            yield return ChronicleFinishCurrentDialogue();
            Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(7), "Navigation dialogue must not award a verse.");
            Assert.That(backend.Read(oldSlot), Is.EqualTo(originalEnvelope));
            string continuedSlot = App.ActiveSaveSlot;
            yield return SmokeUi.LoadBoot();
            yield return ChronicleClick("이어하기");
            Assert.That(App.ActiveSaveSlot, Is.EqualTo(continuedSlot));
            Assert.That(App.Chronicle.QuestIndex, Is.EqualTo(19));
            Assert.That(App.Chronicle.DiscoveredCount, Is.EqualTo(7));
            Assert.That(backend.Read(oldSlot), Is.EqualTo(originalEnvelope));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
