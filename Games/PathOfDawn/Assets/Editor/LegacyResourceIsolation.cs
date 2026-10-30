using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace FirstGame.Editor
{
    /// <summary>Keep the preserved observer art editable, without shipping it in the current Gospel player.</summary>
    [InitializeOnLoad]
    public static class LegacyResourceIsolation
    {
        const string Holding = "Assets/Art/Archive/BuildExcludedLegacy";
        const string Journal = "Library/FirstGameLegacyResourceBuild.json";
        [Serializable] public sealed class FileRecord { public string path, sha256, metaBytes; public long bytes; }
        [Serializable] public sealed class MoveRecord { public string source, destination, guid; }
        [Serializable] public sealed class Record
        {
            public string revision, target, startedUtc, restoredUtc, status, evidencePath;
            public MoveRecord[] moves;
            public FileRecord[] files;
        }

        static LegacyResourceIsolation()
        {
            EditorApplication.delayCall += () =>
            {
                if (!BuildPipeline.isBuildingPlayer && !EditorApplication.isPlayingOrWillChangePlaymode)
                    RecoverInterrupted();
            };
        }

        public static void RecoverInterrupted()
        {
            if (!File.Exists(Journal)) return;
            var record = JsonUtility.FromJson<Record>(File.ReadAllText(Journal));
            if (record == null || (record.revision != "gospel-authored-2026-10-27" && record.revision != "gospel-toon-2026-10-27" && record.revision != "gospel-chronicle-2026-10-29" && record.revision != LuminousProjectSetup.Revision && record.revision != PilgrimageProjectSetup.Revision) || record.moves == null || record.files == null)
                throw new BuildFailedException("Invalid legacy resource recovery journal; retained for inspection.");
            Restore(record, "recovered-after-interruption");
            Debug.Log("FIRSTGAME_LEGACY_RESOURCES_RECOVERED " + record.moves.Length);
        }

        public sealed class Scope : IDisposable
        {
            readonly Record record;
            bool restored;
            public string EvidencePath => record.evidencePath;
            public int ExcludedAssets => record.moves.Length;
            internal Scope(Record value) { record = value; }
            public void Dispose()
            {
                if (restored) return;
                Restore(record, "restored"); restored = true;
            }
        }

        public static Scope Begin(string target)
        {
            RecoverInterrupted();
            var candidates = new List<string> {
                "Assets/Resources/Art/Painted", "Assets/Resources/Voice",
                "Assets/Resources/Art/GospelRemaster", "Assets/Resources/Art/GospelRadiance",
                "Assets/Resources/Art/GospelLuminous/Rendering"
            };
            // The HD-2D field retains authored 3D props/well/temple materials,
            // current pixel sprites, the reading illustrations and SoftGlow.
            // Old full humanoids and the unused lateral Lost Crypt world stay
            // byte-for-byte editable but do not inflate the current Web player.
            AddChildrenExcept(candidates, "Assets/Resources/Art/Gospel3D", "TempleMaterials");
            AddChildrenExcept(candidates, "Assets/Resources/Art/GospelLuminous/World", "SoftGlow.png");
            AddChildrenExcept(candidates, "Assets/Resources/Art/GospelLuminous/Actors", "npcs.png");
            candidates.AddRange(Directory.GetFiles("Assets/Resources/Art", "*", SearchOption.TopDirectoryOnly)
                .Where(path => !path.EndsWith(".meta", StringComparison.Ordinal) &&
                    (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)))
                .Select(path => path.Replace('\\', '/')));
            var moves = candidates.Where(path => File.Exists(path) || Directory.Exists(path)).Select(path => new MoveRecord {
                source = path, destination = Holding + "/" + path.Substring("Assets/Resources/".Length),
                guid = AssetDatabase.AssetPathToGUID(path)
            }).ToArray();
            var dependencies = new HashSet<string>(AssetDatabase.GetDependencies("Assets/Scenes/Boot.unity", true));
            foreach (var move in moves)
            {
                if (string.IsNullOrEmpty(move.guid) || File.Exists(move.destination) || Directory.Exists(move.destination))
                    throw new BuildFailedException("Cannot isolate an unknown or occupied legacy asset: " + move.source);
                if (dependencies.Any(path => path == move.source || path.StartsWith(move.source + "/", StringComparison.Ordinal)))
                    throw new BuildFailedException("Current Boot directly depends on a proposed legacy exclusion: " + move.source);
            }
            var files = new List<FileRecord>();
            foreach (var move in moves)
            {
                var paths = Directory.Exists(move.source) ? Directory.GetFiles(move.source, "*", SearchOption.AllDirectories).ToList()
                    : new List<string> { move.source };
                if (File.Exists(move.source + ".meta")) paths.Add(move.source + ".meta");
                foreach (var path in paths)
                {
                    var bytes = File.ReadAllBytes(path);
                    files.Add(new FileRecord { path = path.Replace('\\', '/'), bytes = bytes.Length,
                        sha256 = Digest(bytes), metaBytes = path.EndsWith(".meta", StringComparison.Ordinal) ? Convert.ToBase64String(bytes) : null });
                }
            }
            string root = Environment.GetEnvironmentVariable("FIRSTGAME_BUILD_ROOT");
            if (string.IsNullOrEmpty(root)) root = Path.GetFullPath("../../Artifacts/Builds");
            var evidence = Path.Combine(root, "PathOfDawn", target, "LegacyResourceExclusions.json");
            var record = new Record { revision = PilgrimageProjectSetup.Revision, target = target,
                startedUtc = DateTime.UtcNow.ToString("o"), status = "journaled-before-mutation", evidencePath = evidence,
                moves = moves, files = files.ToArray() };
            Directory.CreateDirectory("Library");
            // The complete journal is committed before the first move. Recovery
            // inspects actual paths, so a crash at any point can restore them.
            File.WriteAllText(Journal + ".tmp", JsonUtility.ToJson(record, true));
            File.Move(Journal + ".tmp", Journal);
            try
            {
                foreach (var move in moves)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(move.destination));
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    string error = AssetDatabase.MoveAsset(move.source, move.destination);
                    if (!string.IsNullOrEmpty(error)) throw new BuildFailedException(error);
                    if (AssetDatabase.AssetPathToGUID(move.destination) != move.guid)
                        throw new BuildFailedException("GUID changed while isolating " + move.source);
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("FIRSTGAME_LEGACY_RESOURCES_EXCLUDED " + moves.Length);
                return new Scope(record);
            }
            catch { Restore(record, "restored-after-exclusion-error"); throw; }
        }

        static void AddChildrenExcept(List<string> candidates, string directory, params string[] retainedNames)
        {
            if (!Directory.Exists(directory)) return;
            var retained = new HashSet<string>(retainedNames, StringComparer.Ordinal);
            foreach (var entry in Directory.GetFileSystemEntries(directory))
            {
                string name = Path.GetFileName(entry);
                if (name.EndsWith(".meta", StringComparison.Ordinal) || name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || retained.Contains(name)) continue;
                candidates.Add(entry.Replace('\\', '/'));
            }
        }

        static void Restore(Record record, string status)
        {
            foreach (var move in record.moves.Reverse())
            {
                bool source = File.Exists(move.source) || Directory.Exists(move.source);
                bool destination = File.Exists(move.destination) || Directory.Exists(move.destination);
                if (source && destination)
                {
                    if (Directory.Exists(move.source) && !Directory.EnumerateFiles(move.source, "*", SearchOption.AllDirectories).Any())
                        AssetDatabase.DeleteAsset(move.source);
                    else throw new BuildFailedException("Ambiguous recovery paths retained: " + move.source);
                    source = false;
                }
                if (!source && destination)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(move.source));
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    string error = AssetDatabase.MoveAsset(move.destination, move.source);
                    if (!string.IsNullOrEmpty(error)) throw new BuildFailedException(error);
                }
                if (!File.Exists(move.source) && !Directory.Exists(move.source))
                    throw new BuildFailedException("Missing recoverable source retained in journal: " + move.source);
            }
            // Unity can rewrite importer serialization during a move. Restore
            // the exact recorded metadata, then verify every authored byte.
            foreach (var file in record.files)
            {
                if (!File.Exists(file.path)) throw new BuildFailedException("Missing restored resource " + file.path);
                if (Digest(File.ReadAllBytes(file.path)) != file.sha256 && file.path.EndsWith(".meta", StringComparison.Ordinal) && !string.IsNullOrEmpty(file.metaBytes))
                    File.WriteAllBytes(file.path, Convert.FromBase64String(file.metaBytes));
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var file in record.files)
                if (!File.Exists(file.path) || new FileInfo(file.path).Length != file.bytes || Digest(File.ReadAllBytes(file.path)) != file.sha256)
                    throw new BuildFailedException("Legacy source byte restoration differs; journal retained: " + file.path);
            foreach (var move in record.moves)
                if (AssetDatabase.AssetPathToGUID(move.source) != move.guid)
                    throw new BuildFailedException("Legacy GUID restoration differs; journal retained: " + move.source);
            record.restoredUtc = DateTime.UtcNow.ToString("o"); record.status = status;
            Directory.CreateDirectory(Path.GetDirectoryName(record.evidencePath));
            File.WriteAllText(record.evidencePath, JsonUtility.ToJson(record, true));
            // Only empty directories created by this scope are removed.
            if (Directory.Exists(Holding) && Directory.EnumerateFiles(Holding, "*", SearchOption.AllDirectories).All(path =>
                path.EndsWith(".meta", StringComparison.Ordinal) && Directory.Exists(path.Substring(0, path.Length - 5))))
                AssetDatabase.DeleteAsset(Holding);
            File.Delete(Journal);
        }

        // Exercise real GUID-preserving asset moves and both restoration paths
        // before shipping. The interruption case deliberately leaves the journal
        // and invokes the same recovery entry point used after an Editor restart.
        public static void ValidateLifecycle()
        {
            RecoverInterrupted();
            ProjectSetup.Ensure();
            using (Begin("RestorationLifecycleNormal")) { }
            try
            {
                using (Begin("RestorationLifecycleException"))
                    throw new InvalidOperationException("Expected lifecycle verification exception");
            }
            catch (InvalidOperationException error)
            {
                if (error.Message != "Expected lifecycle verification exception") throw;
            }
            Begin("RestorationLifecycleRecovery");
            if (!File.Exists(Journal)) throw new BuildFailedException("Recovery exercise did not retain its journal.");
            RecoverInterrupted();
            if (File.Exists(Journal) || Directory.Exists(Holding))
                throw new BuildFailedException("Recovery exercise left a journal or excluded source assets.");
            Debug.Log("FIRSTGAME_LEGACY_LIFECYCLE_VERIFIED normal exception journal-recovery; every source byte and GUID restored");
        }

        static string Digest(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
