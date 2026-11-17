using System;
using UnityEditor;
using UnityEngine;

namespace FirstGame.Editor
{
    public static class GospelIllustratedPortraitPreparation
    {
        public static void Ensure()
        {
            foreach (string role in new[] { "JesusChild", "JesusAdult", "Mary", "Joseph", "Teacher", "Shepherd" })
            {
                string path = "Assets/Resources/Art/GospelIllustrated/Portraits/" + role + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Required illustrated portrait missing: " + role);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.textureType = TextureImporterType.Default;
                settings.textureShape = TextureImporterShape.Texture2D;
                settings.alphaSource = TextureImporterAlphaSource.FromInput;
                settings.alphaIsTransparency = true;
                settings.mipmapEnabled = false;
                settings.sRGBTexture = true;
                settings.filterMode = FilterMode.Bilinear;
                settings.wrapMode = TextureWrapMode.Clamp;
                settings.npotScale = TextureImporterNPOTScale.None;
                importer.SetTextureSettings(settings);
                importer.ignoreMipmapLimit = true;
                importer.maxTextureSize = 1024;
                // Preserve the source aspect and use a deterministic readable
                // graphics format in the Editor; Web ships a bounded DXT5 copy.
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.compressionQuality = 100;
                importer.isReadable = false;
                var defaults = importer.GetDefaultPlatformTextureSettings();
                defaults.maxTextureSize = 1024;
                defaults.format = TextureImporterFormat.RGBA32;
                defaults.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SetPlatformTextureSettings(defaults);
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings {
                    name = "WebGL", overridden = true, maxTextureSize = 1024,
                    format = TextureImporterFormat.DXT5,
                    textureCompression = TextureImporterCompression.CompressedHQ, compressionQuality = 100
                });
                importer.SaveAndReimport();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var image = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (image == null || image.width < 512 || image.height < 640)
                    throw new InvalidOperationException("Illustrated portrait imported below the required UI resolution: " + role
                        + " (" + (image == null ? "missing" : image.width + "x" + image.height) + ")");
                Debug.Log("FIRSTGAME_ILLUSTRATED_PORTRAIT " + role + " " + image.width + "x" + image.height);
            }
        }
    }
}
