using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using FantasyCards.Core;
using FirstGame.Shared;
using Ink;
using NUnit.Framework;
using PathOfDawn.Core;

namespace FirstGame.Validation
{
    // The same public fields Unity JsonUtility serializes are included here. This exercises
    // actual JSON, but is intentionally NOT a substitute for a Unity JsonUtility test.
    static class FieldJson
    {
        static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };
        public static string Encode<T>(T value) => JsonSerializer.Serialize(value, Options);
        public static T Decode<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
        public static SafeSaveService Service(ISaveBackend backend) =>
            new SafeSaveService(backend, Encode, Decode<PortableSave>);
    }

    [TestFixture]
    public sealed class PortableSaveTests
    {
        string temporaryDirectory;
        FileSaveBackend disk;
        SafeSaveService service;

        [SetUp] public void CreateIsolatedDirectory()
        {
            temporaryDirectory = Path.Combine(Path.GetTempPath(), "firstgame-save-tests-" + Guid.NewGuid().ToString("N"));
            disk = new FileSaveBackend(temporaryDirectory);
            service = FieldJson.Service(disk);
        }
        [TearDown] public void RemoveIsolatedDirectory()
        { if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, true); }

        [Test] public void FileSaveAndPortableTransferPreserveUnicodePayloadAcrossBackends()
        {
            const string payload = "{\"scene\":\"갈릴리 새벽\",\"inventory\":[\"빵\"],\"rng\":4294967295}";
            Assert.That(service.TrySave("slot", "dawn", 2, "story-1", payload, out var error), Is.True, error);
            Assert.That(service.TryLoad("slot", "dawn", 2, "story-1", out var loaded, out error), Is.True, error);
            Assert.That(loaded, Is.EqualTo(payload));
            Assert.That(service.TryExport("slot", out var portable, out error), Is.True, error);
            var memory = new MemoryBackend(); var other = FieldJson.Service(memory);
            Assert.That(other.TryImport("other-device", "dawn", 2, "story-1", portable,
                value => JsonDocument.Parse(value).RootElement.GetProperty("inventory")[0].GetString() == "빵", out error), Is.True, error);
            Assert.That(other.TryLoad("other-device", "dawn", 2, "story-1", out loaded, out error), Is.True, error);
            Assert.That(loaded, Is.EqualTo(payload));
            Assert.That(other.TryExport("other-device", out var returned, out error), Is.True, error);
            Assert.That(service.TryImport("returned", "dawn", 2, "story-1", returned, value => value == payload, out error), Is.True, error);
            Assert.That(disk.Read("returned"), Is.EqualTo(portable));
        }

        [TestCase("other-game", 1, "version-1")]
        [TestCase("cards", 2, "version-1")]
        [TestCase("cards", 1, "version-2")]
        public void IncompatibleLoadSaveAndImportKeepOriginalBytes(string game, int schema, string content)
        {
            Assert.That(service.TrySave("slot", "cards", 1, "version-1", "{\"turn\":4}", out var error), Is.True, error);
            byte[] before = File.ReadAllBytes(Path.Combine(temporaryDirectory, "slot.json"));
            Assert.That(service.TryLoad("slot", game, schema, content, out var loaded, out error), Is.False);
            Assert.That(loaded, Is.Null); Assert.That(error, Is.Not.Empty);
            Assert.That(service.TrySave("slot", game, schema, content, "{\"turn\":5}", out error), Is.False);
            var incoming = FieldJson.Service(new MemoryBackend());
            Assert.That(incoming.TrySave("transfer", game, schema, content, "{\"turn\":6}", out error), Is.True, error);
            Assert.That(incoming.TryExport("transfer", out var portable, out error), Is.True, error);
            Assert.That(service.TryImport("slot", "cards", 1, "version-1", portable, _ => true, out error), Is.False);
            Assert.That(File.ReadAllBytes(Path.Combine(temporaryDirectory, "slot.json")), Is.EqualTo(before));
            Assert.That(File.Exists(Path.Combine(temporaryDirectory, "slot.json.backup")), Is.False);
        }

        [Test] public void ChecksumFailureBlocksOverwriteAndImportWithoutChangingDamagedSource()
        {
            Assert.That(service.TrySave("slot", "cards", 1, "v1", "{\"turn\":4}", out _), Is.True);
            var damaged = FieldJson.Decode<PortableSave>(disk.Read("slot"));
            damaged.payload = "{\"turn\":99}"; // A valid envelope and payload, with deliberately stale digest.
            var path = Path.Combine(temporaryDirectory, "slot.json");
            File.WriteAllText(path, FieldJson.Encode(damaged), new UTF8Encoding(false));
            byte[] before = File.ReadAllBytes(path);
            Assert.That(service.TryLoad("slot", "cards", 1, "v1", out _, out _), Is.False);
            Assert.That(service.TrySave("slot", "cards", 1, "v1", "{\"turn\":5}", out _), Is.False);
            Assert.That(service.TryImport("slot", "cards", 1, "v1", disk.Read("slot"), _ => true, out _), Is.False);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
        }

        [TestCase(false)] [TestCase(true)]
        public void RejectedOrThrowingPayloadValidatorNeverWritesSlot(bool throws)
        {
            Assert.That(service.TrySave("slot", "cards", 1, "v1", "{\"turn\":4}", out _), Is.True);
            byte[] before = File.ReadAllBytes(Path.Combine(temporaryDirectory, "slot.json"));
            var incoming = FieldJson.Service(new MemoryBackend());
            Assert.That(incoming.TrySave("transfer", "cards", 1, "v1", "{\"turn\":-20}", out _), Is.True);
            Assert.That(incoming.TryExport("transfer", out var portable, out _), Is.True);
            Assert.That(service.TryImport("slot", "cards", 1, "v1", portable,
                _ => throws ? throw new FormatException("Invalid turn") : false, out var error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(File.ReadAllBytes(Path.Combine(temporaryDirectory, "slot.json")), Is.EqualTo(before));
        }

        [Test] public void AtomicReplacementKeepsExactlyPreviousBytesAndNoTemporaryFile()
        {
            Assert.That(service.TrySave("slot", "cards", 1, "v1", "{\"turn\":1}", out _), Is.True);
            byte[] first = File.ReadAllBytes(Path.Combine(temporaryDirectory, "slot.json"));
            Assert.That(service.TrySave("slot", "cards", 1, "v1", "{\"turn\":2}", out var error), Is.True, error);
            Assert.That(File.ReadAllBytes(Path.Combine(temporaryDirectory, "slot.json.backup")), Is.EqualTo(first));
            byte[] second = File.ReadAllBytes(Path.Combine(temporaryDirectory, "slot.json"));
            Assert.That(service.TrySave("slot", "cards", 1, "v1", "{\"turn\":3}", out error), Is.True, error);
            Assert.That(File.ReadAllBytes(Path.Combine(temporaryDirectory, "slot.json.backup")), Is.EqualTo(second));
            Assert.That(Directory.GetFiles(temporaryDirectory, "*.tmp-*"), Is.Empty);
            Assert.That(service.TryLoad("slot", "cards", 1, "v1", out var payload, out error), Is.True, error);
            Assert.That(payload, Is.EqualTo("{\"turn\":3}"));
        }

        [Test] public void ExplicitValidImportCanRecoverCorruptSlotAndKeepsCorruptBytesInBackup()
        {
            disk.WriteAtomic("slot", "damaged original — 원본");
            byte[] before = File.ReadAllBytes(Path.Combine(temporaryDirectory, "slot.json"));
            var incoming = FieldJson.Service(new MemoryBackend());
            Assert.That(incoming.TrySave("transfer", "cards", 1, "v1", "{\"turn\":2}", out _), Is.True);
            Assert.That(incoming.TryExport("transfer", out var portable, out _), Is.True);
            Assert.That(service.TryImport("slot", "cards", 1, "v1", portable, p => p == "{\"turn\":2}", out var error), Is.True, error);
            Assert.That(File.ReadAllBytes(Path.Combine(temporaryDirectory, "slot.json.backup")), Is.EqualTo(before));
        }

        [TestCase("../outside")] [TestCase("/absolute")] [TestCase("a/b")] [TestCase("a\\b")] [TestCase("")]
        public void InvalidSlotCannotWriteOutsideItsDirectory(string slot)
        {
            Assert.That(service.TrySave(slot, "cards", 1, "v1", "{}", out _), Is.False);
            Assert.That(Directory.Exists(temporaryDirectory), Is.False);
        }

        [Test] public void OversizedPayloadCannotReplaceValidSlot()
        {
            Assert.That(service.TrySave("slot", "cards", 1, "v1", "{}", out _), Is.True);
            var original = disk.Read("slot");
            Assert.That(service.TrySave("slot", "cards", 1, "v1", new string('한', SafeSaveService.MaximumBytes / 2), out _), Is.False);
            Assert.That(disk.Read("slot"), Is.EqualTo(original));
        }

        sealed class MemoryBackend : ISaveBackend
        {
            readonly Dictionary<string, string> slots = new Dictionary<string, string>();
            public bool Exists(string slot) => slots.ContainsKey(slot);
            public string Read(string slot) => slots[slot];
            public void WriteAtomic(string slot, string contents) => slots[slot] = contents;
        }
    }

    [TestFixture]
    public sealed class SerializedGameStateTests
    {
        [TestCase(1u)] [TestCase(44u)] [TestCase(188u)]
        public void CardSaveThroughJsonAndPortableEnvelopeResumesWholeRemainingBattle(uint seed)
        {
            var source = new BattleEngine(seed);
            int steps = 0;
            while (source.State.phase != BattlePhase.BlockResponse && steps++ < 300)
                Assert.That(BattleAi.Step(source, source.State.activePlayer, out var error), Is.True, error);
            Assert.That(source.State.phase, Is.EqualTo(BattlePhase.BlockResponse));
            var path = Path.Combine(Path.GetTempPath(), "firstgame-card-roundtrip-" + Guid.NewGuid().ToString("N"));
            try
            {
                var saves = FieldJson.Service(new FileSaveBackend(path));
                Assert.That(saves.TrySave("battle", "fantasy-cards", 1, CardCatalog.Version, FieldJson.Encode(source.State), out var error), Is.True, error);
                Assert.That(saves.TryLoad("battle", "fantasy-cards", 1, CardCatalog.Version, out var payload, out error), Is.True, error);
                var restored = new BattleEngine(FieldJson.Decode<BattleState>(payload));
                Assert.That(FieldJson.Encode(restored.State), Is.EqualTo(FieldJson.Encode(source.State)));
                while (source.State.phase != BattlePhase.Complete && steps++ < 2000)
                {
                    int actor = source.State.phase == BattlePhase.BlockResponse ? 1 - source.State.activePlayer : source.State.activePlayer;
                    Assert.That(BattleAi.Step(source, actor, out error), Is.True, error);
                    Assert.That(BattleAi.Step(restored, actor, out error), Is.True, error);
                    BattleEngine.Validate(restored.State);
                    // Compare every persisted field, including next RNG, deck order, pending target,
                    // units/equipment, resource totals and the complete ordered event history.
                    Assert.That(FieldJson.Encode(restored.State), Is.EqualTo(FieldJson.Encode(source.State)), "Resumed step " + steps);
                }
                Assert.That(source.State.phase, Is.EqualTo(BattlePhase.Complete));
                Assert.That(steps, Is.LessThan(2000));
            }
            finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
        }

        [Test] public void NarrativeJsonRestoresChoiceCheckpointsRngAndPersistentMemories()
        {
            string ink = new Compiler(File.ReadAllText(Environment.GetEnvironmentVariable("PATH_OF_DAWN_INK"))).Compile().ToJson();
            var source = new NarrativeSession(ink); source.Examine("stopped_watch");
            source.BeginShoreChoice(); source.Choose(0); source.BeginRoomChoice(); source.Choose(0);
            source.Rewind("room_before_choice"); source.Choose(1); source.Rewind("room_before_choice");
            var restored = new NarrativeSession(ink);
            restored.RestoreSave(FieldJson.Decode<NarrativeSave>(FieldJson.Encode(source.CaptureSave())));
            Assert.That(restored.Choices.Select(c => c.text), Is.EqualTo(source.Choices.Select(c => c.text)));
            Assert.That(restored.Choices.Count, Is.EqualTo(3));
            Assert.That(restored.Meta.routes, Is.EquivalentTo(new[] { "witness", "shelter" }));
            Assert.That(restored.Meta.discoveries, Does.Contain("stopped_watch"));
            Assert.That(restored.NextRandom(), Is.EqualTo(source.NextRandom()));
            restored.Choose(2); source.Choose(2);
            Assert.That(restored.Route, Is.EqualTo("reconcile"));
            Assert.That(FieldJson.Encode(restored.CaptureSave()), Is.EqualTo(FieldJson.Encode(source.CaptureSave())));
            restored.Rewind("shore_before_choice"); source.Rewind("shore_before_choice");
            Assert.That(restored.Checkpoints.Count, Is.EqualTo(1));
            Assert.That(restored.NextRandom(), Is.EqualTo(source.NextRandom()));
            Assert.That(restored.World.inventory, Is.Empty);
            Assert.That(restored.Meta.routes.Count, Is.EqualTo(3));
            Assert.That(FieldJson.Encode(restored.CaptureSave()), Is.EqualTo(FieldJson.Encode(source.CaptureSave())));
        }
    }
}
