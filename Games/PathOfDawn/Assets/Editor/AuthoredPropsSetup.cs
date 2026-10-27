using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FirstGame.Editor
{
    /// <summary>Import the selected authored scan/foliage assets with their actual material conventions.</summary>
    public static class AuthoredPropsSetup
    {
        const string Folder = "Assets/Resources/Art/GospelAuthoredProps";
        [Serializable] sealed class Palette { public Entry[] materials; }
        [Serializable] sealed class Entry
        {
            public string name, baseMap, normalMap;
            public float smoothness, bumpScale = .5f, cutoff = .45f;
            public bool alphaClip, doubleSided;
        }

        public static void Prepare()
        {
            string specification = Path.GetFullPath("../../Tools/Art/GospelAuthoredProps/material-spec.json");
            if (!Directory.Exists(Folder) || !File.Exists(specification)) return;
            var palette = JsonUtility.FromJson<Palette>(File.ReadAllText(specification));
            if (palette == null || palette.materials == null || palette.materials.Length == 0)
                throw new InvalidOperationException("Authored prop material specification is empty.");
            Directory.CreateDirectory(Folder + "/Materials");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var entry in palette.materials)
            {
                var albedo = Import(entry.baseMap, false, entry.alphaClip);
                var normal = string.IsNullOrEmpty(entry.normalMap) ? null : Import(entry.normalMap, true, false);
                string path = Folder + "/Materials/" + entry.name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("Authored props require the installed URP Lit shader.");
                if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                material.name = entry.name; material.shader = shader;
                material.SetColor("_BaseColor", Color.white); material.SetFloat("_Metallic", 0);
                material.SetTexture("_BaseMap", albedo); material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", entry.bumpScale); material.SetFloat("_Smoothness", entry.smoothness);
                if (normal != null) material.EnableKeyword("_NORMALMAP"); else material.DisableKeyword("_NORMALMAP");
                material.SetFloat("_Surface", 0); material.SetFloat("_AlphaClip", entry.alphaClip ? 1 : 0);
                material.SetFloat("_Cutoff", entry.cutoff); material.SetFloat("_AlphaToMask", entry.alphaClip ? 1 : 0);
                material.SetFloat("_Cull", (float)(entry.doubleSided ? CullMode.Off : CullMode.Back));
                if (entry.alphaClip) material.EnableKeyword("_ALPHATEST_ON"); else material.DisableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", entry.alphaClip ? "TransparentCutout" : "Opaque");
                material.renderQueue = entry.alphaClip ? (int)RenderQueue.AlphaTest : -1;
                EditorUtility.SetDirty(material);
            }
            // Persisted material assets keep the required normal/cutout shader
            // variants in the player as well as giving Editor previews the same look.
            foreach (string path in Directory.GetFiles(Folder, "*.fbx", SearchOption.AllDirectories))
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) throw new InvalidOperationException("Missing authored prop importer: " + path);
                importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
                importer.importNormals = ModelImporterNormals.Import;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                foreach (var embedded in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                {
                    var entry = palette.materials.FirstOrDefault(item => embedded.name == item.name || embedded.name.EndsWith("_" + item.name, StringComparison.Ordinal));
                    if (entry == null) throw new InvalidOperationException("Unknown authored prop material: " + embedded.name + " in " + path);
                    var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/" + entry.name + ".mat");
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), material);
                }
                importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
        }

        static Texture2D Import(string name, bool normal, bool alpha)
        {
            if (string.IsNullOrEmpty(name) || name.Contains("..") || name.Contains("/"))
                throw new InvalidOperationException("Invalid authored prop texture name: " + name);
            var paths = Directory.GetFiles(Folder, name + ".*").Where(path => !path.EndsWith(".meta", StringComparison.Ordinal)).ToArray();
            if (paths.Length != 1) throw new InvalidOperationException("Missing or ambiguous authored texture: " + name);
            string path = paths[0].Replace('\\', '/');
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing authored texture importer: " + path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D; importer.sRGBTexture = !normal;
            importer.flipGreenChannel = false; // The preserved maps are explicitly NOR_GL, +Y.
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = alpha;
            importer.maxTextureSize = 1024; importer.mipmapEnabled = true; importer.anisoLevel = 4;
            importer.wrapMode = alpha ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.compressionQuality = 100;
            var web = importer.GetPlatformTextureSettings("WebGL");
            web.name = "WebGL"; web.overridden = true; web.maxTextureSize = 1024;
            web.format = TextureImporterFormat.ASTC_4x4; web.textureCompression = TextureImporterCompression.CompressedHQ;
            web.compressionQuality = 100; web.crunchedCompression = false;
            importer.SetPlatformTextureSettings(web); importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) throw new InvalidOperationException("Authored texture did not import: " + path);
            return texture;
        }
    }
}
