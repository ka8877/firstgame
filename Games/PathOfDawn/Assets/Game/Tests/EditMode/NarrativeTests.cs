using System;
using System.IO;
using Ink;
using NUnit.Framework;
using PathOfDawn.Core;

namespace PathOfDawn.Tests
{
    public sealed class NarrativeTests
    {
        string compiled;
        [OneTimeSetUp] public void CompileEditableStory()
        {
            var path = Environment.GetEnvironmentVariable("PATH_OF_DAWN_INK") ?? "Assets/Game/Resources/Story/galilee.ink";
            compiled = new Compiler(File.ReadAllText(path)).Compile().ToJson();
        }
        NarrativeSession New() => new NarrativeSession(compiled);
        [Test] public void ThreeRoutesHaveDistinctWorldOutcomes()
        {
            var witness = New(); witness.BeginShoreChoice(); witness.Choose(0); witness.BeginRoomChoice(); witness.Choose(0);
            Assert.That(witness.Route, Is.EqualTo("witness")); Assert.That(witness.World.basketShared, Is.True);
            Assert.That(witness.World.inventory.Count, Is.Zero);
            var shelter = New(); shelter.BeginShoreChoice(); shelter.Choose(0); shelter.BeginRoomChoice(); shelter.Choose(1);
            Assert.That(shelter.Route, Is.EqualTo("shelter")); Assert.That(shelter.World.shelterPrepared, Is.True);
            Assert.That(shelter.World.inventory.Count, Is.EqualTo(1));
            var connected = New(); connected.BeginShoreChoice(); connected.Choose(1); connected.BeginRoomChoice(); connected.Choose(0);
            Assert.That(connected.Route, Is.EqualTo("reconcile")); Assert.That(connected.World.peopleConnected, Is.True);
        }
        [Test] public void RewindRestoresInkInventoryPositionsRngAndKeepsMemories()
        {
            var s = New(); s.Examine("stopped_watch"); s.World.actors[0].x = 1.25f;
            s.BeginShoreChoice(); uint expected = s.NextRandom(); s.Choose(0); s.BeginRoomChoice(); s.Choose(0);
            s.World.actors[0].x = 11; s.Rewind("shore_before_choice");
            Assert.That(s.Trust, Is.Zero); Assert.That(s.FirstChoice, Is.Empty); Assert.That(s.World.inventory, Is.Empty);
            Assert.That(s.World.basketShared, Is.False); Assert.That(s.World.waterCarried, Is.False);
            Assert.That(s.World.actors[0].x, Is.EqualTo(1.25f)); Assert.That(s.NextRandom(), Is.EqualTo(expected));
            Assert.That(s.Meta.routes, Does.Contain("witness")); Assert.That(s.Meta.discoveries, Does.Contain("stopped_watch"));
            Assert.That(s.Checkpoints.Count, Is.EqualTo(1));
            s.Choose(1); s.BeginRoomChoice(); s.Choose(0);
            Assert.That(s.Route, Is.EqualTo("reconcile")); Assert.That(s.World.waterCarried, Is.False);
        }
        [Test] public void RepeatedRewindDoesNotDuplicateRewardsAndMemoryChangesChoices()
        {
            var s = New(); s.BeginShoreChoice(); s.Choose(0); s.BeginRoomChoice(); s.Choose(0);
            for (int i = 0; i < 8; ++i)
            {
                s.Rewind("room_before_choice"); Assert.That(s.World.inventory.Count, Is.EqualTo(1));
                Assert.That(s.Trust, Is.EqualTo(2)); Assert.That(s.World.basketShared, Is.False);
                s.Choose(1); Assert.That(s.World.inventory.Count, Is.EqualTo(1));
            }
            s.Rewind("room_before_choice"); Assert.That(s.Choices.Count, Is.EqualTo(3));
            s.Choose(2); Assert.That(s.Route, Is.EqualTo("reconcile")); Assert.That(s.Meta.routes.Count, Is.EqualTo(3));
        }
        [Test] public void SaveRoundtripKeepsChoiceAndNextRandom()
        {
            var source = New(); source.BeginShoreChoice(); source.Choose(0); source.BeginRoomChoice();
            var snapshot = source.CaptureSave(); var target = New(); target.RestoreSave(snapshot);
            Assert.That(target.Choices.Count, Is.EqualTo(source.Choices.Count));
            Assert.That(target.NextRandom(), Is.EqualTo(source.NextRandom()));
            target.Choose(0); source.Choose(0);
            Assert.That(target.Route, Is.EqualTo(source.Route)); Assert.That(target.Trust, Is.EqualTo(source.Trust));
        }
        [Test] public void SchemaOneMigratesInMemoryAndUnknownVersionsReject()
        {
            var s = New(); s.BeginShoreChoice(); var legacy = s.CaptureSave(); legacy.schemaVersion = 1; legacy.randomState = 0;
            legacy.checkpoints[0].schemaVersion = 1; legacy.checkpoints[0].randomState = 0;
            var restored = New(); restored.RestoreSave(legacy); Assert.That(restored.RandomState, Is.Not.Zero);
            var future = s.CaptureSave(); future.schemaVersion = 200;
            Assert.Throws<FormatException>(() => restored.RestoreSave(future));
            var changedStory = s.CaptureSave(); changedStory.contentVersion = "other-story";
            Assert.Throws<FormatException>(() => restored.RestoreSave(changedStory));
        }
        [Test] public void FailedLoadBlocksOverwriteAndRetainsOriginalBytes()
        {
            var store = new MemoryStorage { value = "damaged-user-save" }; var slot = new NarrativeSaveSlot(store, new InvalidCodec());
            var s = New(); Assert.That(slot.LoadInto(s, out _), Is.False);
            Assert.Throws<InvalidOperationException>(() => slot.Save(s));
            Assert.That(store.value, Is.EqualTo("damaged-user-save")); Assert.That(store.writes, Is.Zero);
            Assert.That(slot.ImportInto(s, "also-invalid", out _), Is.False); Assert.That(store.value, Is.EqualTo("damaged-user-save"));
        }
        [Test] public void CorruptWorldNeverPartiallyMutatesLiveSession()
        {
            var s = New(); s.BeginShoreChoice(); s.Choose(0); var invalid = s.CaptureSave();
            invalid.world.inventory.Add("bread_miriam"); Assert.Throws<FormatException>(() => s.RestoreSave(invalid));
            Assert.That(s.World.inventory.Count, Is.EqualTo(1)); Assert.That(s.Trust, Is.EqualTo(2));
            invalid = s.CaptureSave(); invalid.world.actors[0].x = float.NaN;
            Assert.Throws<FormatException>(() => s.RestoreSave(invalid)); Assert.That(float.IsNaN(s.World.actors[0].x), Is.False);
        }
        [Test] public void LogicalCorruptionRejectsUnknownCheckpointAndMixedBranchWorld()
        {
            var s = New(); s.BeginShoreChoice(); s.Choose(0); var invalid = s.CaptureSave();
            invalid.activeCheckpoint = "unknown"; Assert.Throws<FormatException>(() => s.RestoreSave(invalid));
            invalid = s.CaptureSave(); invalid.world.pointId = "ending"; Assert.Throws<FormatException>(() => s.RestoreSave(invalid));
            invalid = s.CaptureSave(); invalid.world.peopleConnected = true; Assert.Throws<FormatException>(() => s.RestoreSave(invalid));
            Assert.That(s.World.pointId, Is.EqualTo("room")); Assert.That(s.World.peopleConnected, Is.False);
        }
        sealed class MemoryStorage : ITextSaveStorage
        { public string value; public int writes; public bool Exists => value != null; public string Read() => value; public void Write(string v) { value = v; writes++; } }
        sealed class InvalidCodec : ISnapshotCodec
        { public string Encode(NarrativeSave value) => "new"; public NarrativeSave Decode(string json) => throw new FormatException("broken"); }
    }
}
