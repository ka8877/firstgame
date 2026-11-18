#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FirstGame.Shared;
using Firstgame.GospelNpr.Editor;
using PathOfDawn.Presentation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Firstgame.GospelVisualRebuild.Editor
{
    /// <summary>Editor validation helpers only. No runtime/product assembly references this assembly.</summary>
    public static class GospelHeroPreviewSupport
    {
        public const string Boot = "Assets/Scenes/Boot.unity";
        public const string SourceSha = "7526838f4a45086a1d0abb23e9f38ad3f0103889f1efa70d2605ef25e7e6ef99";
        public const string HeroResource = "Art/GospelHero20261119/Characters/JesusChild";
        public const string EnvironmentRoot = "Assets/Resources/Art/GospelVisualRebuild20261119/Environment/Textures/";
        public static string Project => Directory.GetParent(Application.dataPath).FullName;
        public static string Repository => Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        public static readonly string[] EnvironmentStems = { "rough_plaster_03", "stone_floor", "sandstone_blocks_04" };

        [Serializable] public sealed class FilePin { public string file, sha256; public long bytes; }
        [Serializable] public sealed class Config
        {
            public int schema = 1;
            public string attempt, outputDirectory;
            public FilePin preparationSettings;
            public FilePin[] installedEnvironmentTextures;
            public bool prepareChild; // Normally false: run the direct child preparer separately first.
            public bool exitEditorWhenDone = true;
            public float maximumRuntimeSeconds = 240;
        }
        [Serializable] public sealed class ExpressionCatalog { public int schema; public ExpressionActor[] actors; }
        [Serializable] public sealed class ExpressionActor { public string id; public GospelHeroChildPreparation.Preset[] presets; }

        public static Config ReadConfig()
        {
            string path = Environment.GetEnvironmentVariable("FIRSTGAME_HERO_PREVIEW_CONFIG");
            string sha = Environment.GetEnvironmentVariable("FIRSTGAME_HERO_PREVIEW_CONFIG_SHA256");
            Need(!string.IsNullOrEmpty(path) && Path.IsPathRooted(path) && HashFile(path) == sha, "Exact absolute preview-config bytes/SHA required.");
            var config = JsonUtility.FromJson<Config>(File.ReadAllText(path));
            Need(config != null && config.schema == 1 && !string.IsNullOrEmpty(config.attempt)
                && config.attempt.Length <= 80 && config.attempt.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'), "Explicit safe preview attempt required.");
            Need(config.maximumRuntimeSeconds >= 30 && config.maximumRuntimeSeconds <= 600, "Bounded 30–600 second preview required.");
            Need(!string.IsNullOrEmpty(config.outputDirectory) && Path.IsPathRooted(config.outputDirectory), "Explicit new absolute WS output directory required.");
            string output = Path.GetFullPath(config.outputDirectory);
            Need(output.StartsWith(Path.Combine(Repository, "Artifacts", "Validation") + Path.DirectorySeparatorChar, StringComparison.Ordinal), "Output must remain in repository WS validation.");
            NoLinks(output); Pin(config.preparationSettings);
            Need(config.installedEnvironmentTextures != null && config.installedEnvironmentTextures.Length == 6, "Pin all six installed environment originals.");
            var expected = EnvironmentStems.SelectMany(s => new[] { s + "_diff_2k.jpg", s + "_nor_gl_2k.jpg" }).ToArray();
            Need(config.installedEnvironmentTextures.Select(p => Path.GetFullPath(p.file)).Distinct(StringComparer.Ordinal).Count() == 6, "Distinct exact environment files required.");
            foreach (string name in expected)
            {
                string exact = Path.GetFullPath(Path.Combine(Project, EnvironmentRoot, name));
                var pin = config.installedEnvironmentTextures.SingleOrDefault(p => Path.GetFullPath(p.file) == exact);
                Need(pin != null, "Missing exact installed environment source: " + name); Pin(pin);
            }
            return config;
        }

        public static GospelHeroChildPreparation.Settings ReadPreparation(Config config)
        {
            Pin(config.preparationSettings);
            var settings = JsonUtility.FromJson<GospelHeroChildPreparation.Settings>(File.ReadAllText(config.preparationSettings.file));
            Need(settings != null && settings.schema == 1 && settings.actorId == "JesusChild" && settings.sourceSha256 == SourceSha, "Selected Hinzka child settings required.");
            PinPreparationFile(settings.originalVrm); PinPreparationFile(settings.model);
            Need(settings.originalVrm.sha256 == SourceSha, "Selected original VRM SHA required.");
            foreach (var texture in settings.textures ?? Array.Empty<GospelHeroChildPreparation.TextureSource>()) PinPreparationFile(texture.source);
            return settings;
        }
        static void PinPreparationFile(GospelHeroChildPreparation.FilePin pin)
        { Need(pin != null, "Actual preparation file pin required."); Pin(new FilePin { file = pin.file, sha256 = pin.sha256, bytes = pin.bytes }); }

        public static void ConfigureEnvironmentImporters(Config config)
        {
            Need(!Application.isPlaying && !BuildPipeline.isBuildingPlayer, "Import only in idle Gospel Editor before the world exists.");
            foreach (var pin in config.installedEnvironmentTextures)
            {
                Pin(pin);
                string asset = EnvironmentRoot + Path.GetFileName(pin.file);
                AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var importer = AssetImporter.GetAtPath(asset) as TextureImporter;
                Need(importer != null, "Exact installed image TextureImporter required: " + asset);
                bool normal = asset.EndsWith("_nor_gl_2k.jpg", StringComparison.Ordinal);
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = !normal; importer.flipGreenChannel = false;
                importer.maxTextureSize = 2048; importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.compressionQuality = 100; importer.crunchedCompression = false;
                importer.mipmapEnabled = true; importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 4;
                importer.alphaIsTransparency = false; importer.npotScale = TextureImporterNPOTScale.None;
                importer.SaveAndReimport(); Pin(pin);
            }
            Need(QualitySettings.skinWeights == SkinWeights.Unlimited, "Active quality must already be Unlimited; this entry does not lower or rewrite quality settings.");
        }

        public static void AssertEnvironmentImporters(Config config)
        {
            foreach (var pin in config.installedEnvironmentTextures)
            {
                Pin(pin);
                string asset = EnvironmentRoot + Path.GetFileName(pin.file);
                var importer = AssetImporter.GetAtPath(asset) as TextureImporter;
                bool normal = asset.EndsWith("_nor_gl_2k.jpg", StringComparison.Ordinal);
                Need(importer != null && importer.textureType == (normal ? TextureImporterType.NormalMap : TextureImporterType.Default)
                    && importer.sRGBTexture == !normal && !importer.flipGreenChannel && importer.maxTextureSize == 2048
                    && importer.textureCompression == TextureImporterCompression.CompressedHQ && importer.mipmapEnabled,
                    "Environment importer differs from the admitted six-map contract: " + asset);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(asset);
                Need(texture && texture.width >= 1024 && texture.height >= 1024 && texture.width <= 2048 && texture.height <= 2048, "Actual imported 2K source pair required.");
            }
        }

        public static GameObject RequireHero()
        {
            Need(GospelHeroModelProfile.TryResolve("JesusChild", out var prefab, out string error), error ?? "New hero prefab is not installed.");
            var profile = prefab.GetComponent<GospelHeroModelProfile>();
            Need(profile.SourceSha256 == SourceSha && profile.LogicalId == "JesusChild", "Actual admitted child identity required.");
            return prefab;
        }

        public static Dictionary<string, string> SnapshotSources(Config config)
        {
            var settings = ReadPreparation(config);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var prefab = RequireHero();
            foreach (string asset in AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(prefab), true))
            {
                string path = Path.GetFullPath(Path.Combine(Project, asset));
                if (File.Exists(path)) result[path] = HashFile(path);
            }
            foreach (string path in new[] { config.preparationSettings.file, settings.originalVrm.file, settings.model.file }
                .Concat(settings.textures.Select(t => t.source.file)).Concat(config.installedEnvironmentTextures.Select(p => p.file)))
                result[Path.GetFullPath(path)] = HashFile(path);
            return result;
        }
        public static void AssertUnchanged(Dictionary<string, string> before)
        { foreach (var entry in before) Need(HashFile(entry.Key) == entry.Value, "Original source/dependency changed: " + entry.Key); }

        public static void Click(string label)
        {
            var buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Where(b => b.gameObject.activeInHierarchy && b.GetComponentsInChildren<TMP_Text>().Any(t => t.text == label)).ToArray();
            Need(buttons.Length == 1 && buttons[0].IsActive() && buttons[0].IsInteractable(), "One real enabled UI callback required: " + label);
            buttons[0].onClick.Invoke(); // Real callback; this is not pointer hit-testing evidence.
        }
        public static string Safe(string value) => new string((value ?? "").Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray());
        public static bool ShapeMatches(string actual, string expected) => actual == expected || actual.EndsWith("." + expected, StringComparison.Ordinal);
        public static string HashFile(string path)
        { using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
        public static void Pin(FilePin pin)
        { Need(pin != null && !string.IsNullOrEmpty(pin.file) && Path.IsPathRooted(pin.file), "Absolute pinned local file required."); NoLinks(pin.file); Need(new FileInfo(pin.file).Length == pin.bytes && HashFile(pin.file) == pin.sha256, "Pinned file bytes changed: " + pin.file); }
        public static void WriteNew(string path, byte[] bytes)
        { using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); } }
        public static void NoLinks(string path)
        { for (var item = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(path))); item != null; item = item.Parent) if (item.Exists) Need((item.Attributes & FileAttributes.ReparsePoint) == 0, "No linked output/input ancestor."); if (File.Exists(path)) Need((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0, "No linked original."); }
        public static void Need(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }

    /// <summary>Test-only memory storage and exact restoration of touched preferences; never DeleteAll.</summary>
    public sealed class GospelHeroPreviewSession : IDisposable
    {
        const string PreferenceRecoveryKey = "FirstGame.GospelHero20261119.Preview.PreferenceRecovery";
        [Serializable] sealed class Preferences
        {
            public string active;
            public bool hadActive, hadMuted, hadVolume, hadVoice;
            public int muted, voice;
            public float volume;
        }
        sealed class Memory : ISaveBackend
        {
            public readonly Dictionary<string, string> Slots = new Dictionary<string, string>();
            public bool Exists(string slot) => Slots.ContainsKey(slot);
            public string Read(string slot) => Slots[slot];
            public void WriteAtomic(string slot, string value) => Slots[slot] = value;
        }
        readonly string active;
        readonly bool hadActive, hadMuted, hadVolume, hadVoice;
        readonly int muted, voice;
        readonly float volume;
        readonly IDisposable overrideStorage;
        readonly Memory backend = new Memory();
        readonly string untouched;
        bool disposed;
        public GospelHeroPreviewSession(string attempt)
        {
            GospelHeroPreviewSupport.Need(string.IsNullOrEmpty(SessionState.GetString(PreferenceRecoveryKey, "")), "Recover the prior preview preference checkpoint before starting another session.");
            active = PlayerPrefs.GetString("GOSPEL.activeSlot", ""); hadActive = PlayerPrefs.HasKey("GOSPEL.activeSlot");
            hadMuted = PlayerPrefs.HasKey("GOSPEL.music.muted"); muted = PlayerPrefs.GetInt("GOSPEL.music.muted", 0);
            hadVolume = PlayerPrefs.HasKey("GOSPEL.music.volume"); volume = PlayerPrefs.GetFloat("GOSPEL.music.volume", .5f);
            hadVoice = PlayerPrefs.HasKey("GOSPEL.voice.enabled"); voice = PlayerPrefs.GetInt("GOSPEL.voice.enabled", 1);
            SessionState.SetString(PreferenceRecoveryKey, JsonUtility.ToJson(new Preferences { active = active, hadActive = hadActive,
                hadMuted = hadMuted, hadVolume = hadVolume, hadVoice = hadVoice, muted = muted, voice = voice, volume = volume }));
            untouched = "hero-preview-original-" + attempt;
            backend.WriteAtomic(untouched, "unchanged original sentinel bytes");
            overrideStorage = PersistentSaves.UseBackendForTesting(backend);
            PlayerPrefs.SetString("GOSPEL.activeSlot", "hero-preview-new-" + attempt);
            PlayerPrefs.SetInt("GOSPEL.music.muted", 1); PlayerPrefs.SetInt("GOSPEL.voice.enabled", 0);
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            overrideStorage.Dispose();
            if (hadActive) PlayerPrefs.SetString("GOSPEL.activeSlot", active); else PlayerPrefs.DeleteKey("GOSPEL.activeSlot");
            if (hadMuted) PlayerPrefs.SetInt("GOSPEL.music.muted", muted); else PlayerPrefs.DeleteKey("GOSPEL.music.muted");
            if (hadVolume) PlayerPrefs.SetFloat("GOSPEL.music.volume", volume); else PlayerPrefs.DeleteKey("GOSPEL.music.volume");
            if (hadVoice) PlayerPrefs.SetInt("GOSPEL.voice.enabled", voice); else PlayerPrefs.DeleteKey("GOSPEL.voice.enabled");
            PlayerPrefs.Save();
            SessionState.EraseString(PreferenceRecoveryKey);
            GospelHeroPreviewSupport.Need(backend.Read(untouched) == "unchanged original sentinel bytes", "Original in-memory slot bytes changed.");
        }
        // PlayMode domain reload can discard a managed session before Dispose.
        // This Editor-only checkpoint recovers only the four explicitly touched keys.
        public static bool RecoverPreferencesAfterUnexpectedExit()
        {
            string json = SessionState.GetString(PreferenceRecoveryKey, "");
            if (string.IsNullOrEmpty(json)) return false;
            var value = JsonUtility.FromJson<Preferences>(json);
            GospelHeroPreviewSupport.Need(value != null, "Malformed preview preference recovery checkpoint.");
            if (value.hadActive) PlayerPrefs.SetString("GOSPEL.activeSlot", value.active); else PlayerPrefs.DeleteKey("GOSPEL.activeSlot");
            if (value.hadMuted) PlayerPrefs.SetInt("GOSPEL.music.muted", value.muted); else PlayerPrefs.DeleteKey("GOSPEL.music.muted");
            if (value.hadVolume) PlayerPrefs.SetFloat("GOSPEL.music.volume", value.volume); else PlayerPrefs.DeleteKey("GOSPEL.music.volume");
            if (value.hadVoice) PlayerPrefs.SetInt("GOSPEL.voice.enabled", value.voice); else PlayerPrefs.DeleteKey("GOSPEL.voice.enabled");
            PlayerPrefs.Save(); SessionState.EraseString(PreferenceRecoveryKey); return true;
        }
    }
}
#endif
