using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FirstGame.Editor
{
    // Packed frame coordinates require exact texel dimensions on every target.
    public static class NeutralCharacterImportSetup
    {
        [Serializable] sealed class Layout { public int width, height; }
        [Serializable] sealed class Observation
        {
            public string identity, guid;
            public int expectedWidth, expectedHeight, beforeWidth, beforeHeight,
                beforeDefaultLimit, beforeWebLimit, width, height, defaultLimit, webLimit;
            public bool beforeWebOverride, webOverride, mipmaps;
        }

        public static void Ensure()
        {
            const string folder = "Assets/Resources/Art/GospelMotion/Neutral/";
            if (!Directory.Exists(folder)) return;
            foreach (string identity in new[] { "child", "adult", "mary", "joseph", "teacher" })
            {
                string path = folder + identity + ".png";
                if (!File.Exists(path) || !File.Exists(folder + identity + "-layout.json") ||
                    !File.Exists(folder + identity + "-manifest.json"))
                    throw new InvalidOperationException("Incomplete neutral actor installation: " + identity);
                var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(folder + identity + "-layout.json"));
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing neutral actor importer: " + path);
                var before = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var defaults = importer.GetDefaultPlatformTextureSettings();
                var web = importer.GetPlatformTextureSettings("WebGL");
                var row = new Observation { identity = identity, guid = AssetDatabase.AssetPathToGUID(path),
                    expectedWidth = layout.width, expectedHeight = layout.height,
                    beforeWidth = before == null ? 0 : before.width, beforeHeight = before == null ? 0 : before.height,
                    beforeDefaultLimit = defaults.maxTextureSize, beforeWebLimit = web.maxTextureSize,
                    beforeWebOverride = web.overridden };
                importer.textureType = TextureImporterType.Default;
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 4096;
                importer.mipmapEnabled = false;
                importer.ignoreMipmapLimit = true;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                defaults.maxTextureSize = 4096;
                defaults.format = TextureImporterFormat.RGBA32;
                defaults.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SetPlatformTextureSettings(defaults);
                web.name = "WebGL"; web.overridden = true; web.maxTextureSize = 4096;
                web.format = TextureImporterFormat.RGBA32;
                web.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SetPlatformTextureSettings(web);
                importer.SaveAndReimport();
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                row.width = texture == null ? 0 : texture.width; row.height = texture == null ? 0 : texture.height;
                row.defaultLimit = importer.GetDefaultPlatformTextureSettings().maxTextureSize;
                row.webLimit = importer.GetPlatformTextureSettings("WebGL").maxTextureSize;
                row.webOverride = importer.GetPlatformTextureSettings("WebGL").overridden;
                row.mipmaps = importer.mipmapEnabled;
                Debug.Log("NEUTRAL_EXACT_IMPORT " + JsonUtility.ToJson(row));
                if (row.width != layout.width || row.height != layout.height || row.mipmaps ||
                    row.defaultLimit != 4096 || row.webLimit != 4096 || !row.webOverride)
                    throw new InvalidOperationException("Neutral atlas texel dimensions/settings differ: " + JsonUtility.ToJson(row));
            }
        }
    }
}
