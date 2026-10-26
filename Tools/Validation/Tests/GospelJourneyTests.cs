using System;
using System.Collections.Generic;
using System.Linq;
using FirstGame.Shared;
using NUnit.Framework;
using PathOfDawn.Core;

namespace FirstGame.Validation
{
    [TestFixture]
    public sealed class GospelJourneyTests
    {
        static GospelJourney AtTemple()
        {
            var journey = GospelJourney.New();
            while (journey.Phase == GospelPhase.Prologue) Assert.That(journey.AdvanceReading(), Is.True);
            return journey;
        }

        [Test]
        public void BirthMustBeReadBeforeTempleAndLaterEpisodesRemainUnavailable()
        {
            var journey = GospelJourney.New();
            Assert.That(journey.CurrentEpisode.Id, Is.EqualTo("birth"));
            Assert.That(journey.IsEpisodeUnlocked("birth"), Is.True);
            Assert.That(journey.IsEpisodeUnlocked("temple"), Is.False);
            Assert.That(journey.TryInteract("teachers", out _), Is.False);
            for (int page = 0; page < 3; page++)
            {
                Assert.That(journey.Phase, Is.EqualTo(GospelPhase.Prologue));
                Assert.That(journey.ProloguePage, Is.EqualTo(page));
                Assert.That(journey.CurrentReading.Id, Is.EqualTo(GospelCatalog.PrologueReadings[page].Id));
                Assert.That(journey.AdvanceReading(), Is.True);
            }
            Assert.That(journey.Phase, Is.EqualTo(GospelPhase.Exploring));
            Assert.That(journey.CurrentEpisode.Id, Is.EqualTo("temple"));
            Assert.That(journey.IsEpisodeUnlocked("temple"), Is.True);
            Assert.That(journey.IsEpisodeUnlocked("baptism"), Is.False);
            Assert.That(journey.AdvanceReading(), Is.False, "Walking alone must not acknowledge an unseen reading.");
        }

        [Test]
        public void LandmarksAreSequentialAndEachReadingRequiresItsOwnAcknowledgement()
        {
            var journey = AtTemple();
            foreach (var objective in GospelCatalog.Objectives)
            {
                Assert.That(journey.TryInteract("unknown-landmark", out var message), Is.False);
                Assert.That(message, Is.EqualTo(objective.Prompt));
                int count = journey.CollectedReadings.Count;
                Assert.That(journey.TryInteract(objective.Id, out message), Is.True);
                Assert.That(journey.Phase, Is.EqualTo(GospelPhase.Reading));
                Assert.That(journey.CurrentReading.Id, Is.EqualTo(objective.ReadingId));
                Assert.That(message, Is.EqualTo(journey.CurrentReading.Reference));
                Assert.That(journey.CollectedReadings.Count, Is.EqualTo(count));
                Assert.That(journey.TryInteract(objective.Id, out _), Is.False);
                Assert.That(journey.AdvanceReading(), Is.True);
                Assert.That(journey.CollectedReadings.Count, Is.EqualTo(count + 1));
            }
            Assert.That(journey.IsComplete, Is.True);
            Assert.That(journey.CurrentObjective, Is.Null);
            Assert.That(journey.CurrentReading.Reference, Is.EqualTo("누가복음 2:51–52"));
            Assert.That(journey.CollectedReadings, Is.EqualTo(GospelCatalog.Readings.Select(r => r.Id)));
            Assert.That(journey.AdvanceReading(), Is.False);
            Assert.That(journey.TryInteract("nazareth-gate", out _), Is.False);
            Assert.That(journey.IsEpisodeUnlocked("ascension"), Is.False);
        }

        [Test]
        public void OutOfOrderInteractionCannotSkipScriptureOrChapterObjectives()
        {
            var journey = AtTemple();
            var before = FieldJson.Encode(journey.CaptureSave());
            foreach (var wrong in new[] { "teachers", "parents", "nazareth-gate", null, "", "TEMPLE-GATE" })
                Assert.That(journey.TryInteract(wrong, out _), Is.False);
            Assert.That(FieldJson.Encode(journey.CaptureSave()), Is.EqualTo(before));
        }

        [Test]
        public void JsonSnapshotsResumeEveryReachablePhaseIncludingOpenReadingAndCompletedChapter()
        {
            var journey = GospelJourney.New();
            var snapshots = new List<GospelSave> { journey.CaptureSave() };
            for (int page = 0; page < 3; page++)
            { journey.AdvanceReading(); snapshots.Add(journey.CaptureSave()); }
            foreach (var objective in GospelCatalog.Objectives)
            {
                Assert.That(journey.TryInteract(objective.Id, out _), Is.True);
                snapshots.Add(journey.CaptureSave());
                journey.AdvanceReading(); snapshots.Add(journey.CaptureSave());
            }
            foreach (var snapshot in snapshots)
            {
                var json = FieldJson.Encode(snapshot);
                var restored = GospelJourney.FromSave(FieldJson.Decode<GospelSave>(json));
                Assert.That(FieldJson.Encode(restored.CaptureSave()), Is.EqualTo(json));
                Assert.That(restored.CurrentReading, Is.Not.Null);
            }
        }

        [Test]
        public void SavedPositionAndHeadingResumeExactlyAndSnapshotListsDoNotAliasLiveState()
        {
            var journey = AtTemple();
            journey.SetPlayerPose(4.25f, 0.01f, -2.75f, -450f);
            var snapshot = journey.CaptureSave();
            Assert.That(snapshot.playerYaw, Is.EqualTo(270f));
            var restored = GospelJourney.FromSave(snapshot);
            snapshot.collectedReadings.Clear(); snapshot.playerX = 88;
            Assert.That(journey.CollectedReadings.Count, Is.EqualTo(3));
            Assert.That(restored.CollectedReadings.Count, Is.EqualTo(3));
            Assert.That(restored.PlayerX, Is.EqualTo(4.25f));
            Assert.That(restored.PlayerZ, Is.EqualTo(-2.75f));
            Assert.That(restored.PlayerYaw, Is.EqualTo(270f));
            Assert.That(((IList<string>)restored.CollectedReadings).IsReadOnly, Is.True);
        }

        [TestCase(float.NaN, 0, 0, 0)]
        [TestCase(0, float.PositiveInfinity, 0, 0)]
        [TestCase(0, 0, float.NegativeInfinity, 0)]
        [TestCase(0, 0, 0, float.NaN)]
        [TestCase(101, 0, 0, 0)]
        [TestCase(0, 31, 0, 0)]
        public void InvalidMotionCannotPoisonAnOtherwiseRestorableSnapshot(float x, float y, float z, float yaw)
        {
            var journey = AtTemple();
            var before = FieldJson.Encode(journey.CaptureSave());
            Assert.Throws<ArgumentOutOfRangeException>(() => journey.SetPlayerPose(x, y, z, yaw));
            Assert.That(FieldJson.Encode(journey.CaptureSave()), Is.EqualTo(before));
        }

        [Test]
        public void MalformedFutureAndInconsistentSavesAreRejectedBeforeChangingLiveProgress()
        {
            var journey = AtTemple();
            Assert.That(journey.TryInteract("temple-gate", out _), Is.True);
            var baseline = journey.CaptureSave(); var before = FieldJson.Encode(baseline);
            var corruptions = new Action<GospelSave>[]
            {
                s => s.schemaVersion++, s => s.contentVersion = "galilee-0.1.0",
                s => s.phase = (GospelPhase)99, s => s.prologuePage = 0,
                s => s.objectiveIndex = 4, s => s.objectiveIndex = -1,
                s => s.phase = GospelPhase.Completed, s => s.collectedReadings = null,
                s => s.collectedReadings.Add("temple-parents"),
                s => s.collectedReadings[0] = "unknown-reading",
                s => s.collectedReadings.Reverse(), s => s.playerX = float.NaN,
                s => s.playerYaw = 360f, s => s.playerZ = -101f
            };
            foreach (var corrupt in corruptions)
            {
                var save = baseline.Copy(); corrupt(save);
                Assert.Throws<ArgumentException>(() => journey.RestoreSave(save));
                Assert.That(FieldJson.Encode(journey.CaptureSave()), Is.EqualTo(before));
            }
            Assert.Throws<ArgumentException>(() => journey.RestoreSave(null));
        }

        [Test]
        public void EmptyOrMissingVersionJsonCannotMasqueradeAsANewJourney()
        {
            var journey = AtTemple();
            var before = FieldJson.Encode(journey.CaptureSave());
            foreach (var json in new[] { "{}", "{\"phase\":0}", "{\"schemaVersion\":1}", "{\"contentVersion\":\"gospel-0.1.0\"}" })
                Assert.Throws<ArgumentException>(() => journey.RestoreSave(FieldJson.Decode<GospelSave>(json)));
            Assert.That(FieldJson.Encode(journey.CaptureSave()), Is.EqualTo(before));
        }

        [Test]
        public void ScriptureCatalogHasTwoPlayableEpisodesAndNoInventedQuotesOrFuturePlayableClaims()
        {
            Assert.That(GospelCatalog.Episodes.Count, Is.EqualTo(26));
            Assert.That(GospelCatalog.Episodes.Select(e => e.Id).Distinct().Count(), Is.EqualTo(26));
            Assert.That(GospelCatalog.Episodes.Where(e => e.IsPlayable).Select(e => e.Id), Is.EqualTo(new[] { "birth", "temple" }));
            Assert.That(GospelCatalog.Episodes.Last().Id, Is.EqualTo("ascension"));
            Assert.That(GospelCatalog.Objectives.Select(o => o.Id), Is.EqualTo(new[] { "temple-gate", "teachers", "parents", "nazareth-gate" }));
            foreach (var reading in GospelCatalog.Readings)
            {
                Assert.That(reading.Reference, Does.StartWith("누가복음 2:"));
                Assert.That(reading.Translation, Is.EqualTo("개역한글"));
                Assert.That(reading.Text, Is.Not.Empty); Assert.That(reading.Context, Is.Not.Empty);
                Assert.That(reading.Text, Is.Not.EqualTo(reading.Context));
            }
            Assert.Throws<ArgumentException>(() => GospelCatalog.GetReading("unwritten-dialogue"));
        }

        [Test]
        public void GospelPortableSlotDoesNotOverwriteOrSilentlyAcceptThePreviousNarrativeGame()
        {
            var backend = new MemorySlots(); var service = FieldJson.Service(backend);
            Assert.That(service.TrySave("legacy-galilee", "path-of-dawn", 2, "galilee-0.1.0", "{\"legacy\":true}", out var error), Is.True, error);
            var original = backend.Read("legacy-galilee");
            var journey = AtTemple(); journey.TryInteract("temple-gate", out _);
            Assert.That(service.TrySave(GospelJourney.SaveSlot, GospelJourney.GameId, GospelJourney.SchemaVersion,
                GospelJourney.ContentVersion, FieldJson.Encode(journey.CaptureSave()), out error), Is.True, error);
            Assert.That(service.TryLoad(GospelJourney.SaveSlot, GospelJourney.GameId, GospelJourney.SchemaVersion,
                GospelJourney.ContentVersion, out var payload, out error), Is.True, error);
            Assert.That(GospelJourney.FromSave(FieldJson.Decode<GospelSave>(payload)).Phase, Is.EqualTo(GospelPhase.Reading));
            Assert.That(service.TryLoad("legacy-galilee", GospelJourney.GameId, GospelJourney.SchemaVersion,
                GospelJourney.ContentVersion, out _, out _), Is.False);
            Assert.That(backend.Read("legacy-galilee"), Is.EqualTo(original));
        }

        sealed class MemorySlots : ISaveBackend
        {
            readonly Dictionary<string, string> slots = new Dictionary<string, string>();
            public bool Exists(string slot) => slots.ContainsKey(slot);
            public string Read(string slot) => slots[slot];
            public void WriteAtomic(string slot, string contents) => slots[slot] = contents;
        }
    }
}
