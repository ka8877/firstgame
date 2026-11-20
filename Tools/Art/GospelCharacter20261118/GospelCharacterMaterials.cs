#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Firstgame.GospelNpr.Editor
{
    /// <summary>Derived cast materials; source textures/materials remain intact.</summary>
    public static class GospelCharacterMaterials
    {
        const string Root = "Assets/Art/GospelCharacter20261118/RenderV1";
        [Serializable] public sealed class Surface
        {
            public string renderer, sourceMaterial, derivedMaterial, role, originalPropertiesSha256;
            public bool sourcePropertiesUnchanged;
        }
        [Serializable] public sealed class Report
        {
            public string revision = "gospel-human-surfaces-v1", actor;
            public Surface[] surfaces;
            public bool actualVisualReviewCompleted;
        }

        public static Material LayeredHair(GameObject actor, string id)
        {
            var reference = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r => r.sharedMaterials)
                .FirstOrDefault(m => m && GospelNprMaterialFactory.InferRole(m.name) == NprSurfaceRole.Hair);
            Need(reference, "A retained real hair material is required.");
            return Fiber(reference, id, false);
        }
        public static Material Beard(GameObject actor, string id)
        {
            var reference = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r => r.sharedMaterials)
                .FirstOrDefault(m => m && GospelNprMaterialFactory.InferRole(m.name) == NprSurfaceRole.Hair);
            Need(reference, "A retained real hair material is required.");
            return Fiber(reference, id, true);
        }
        public static Report Apply(GameObject actor, string id)
        {
            var rows = new List<Surface>();
            foreach (var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var source = materials[i]; Need(source, "Every visible surface needs its real material.");
                    string sourcePath = AssetDatabase.GetAssetPath(source);
                    if (sourcePath.StartsWith(Root + "/", StringComparison.Ordinal)) continue;
                    string before = EditorJsonUtility.ToJson(source);
                    var role = GospelNprMaterialFactory.InferRole(source.name);
                    var material = new Material(source) { name = source.name + "__HumanSurfaceV1" };
                    if (role == NprSurfaceRole.Face || role == NprSurfaceRole.Skin)
                    {
                        Color tint = id == "JesusChild" ? new Color(1, .965f, .925f) : id == "Mary" ? new Color(1, .97f, .95f)
                            : id == "Teacher" ? new Color(.94f, .90f, .83f) : new Color(.97f, .93f, .87f);
                        Set(material, "_Color", source.GetColor("_Color") * tint);
                        Set(material, "_AsUnlit", .035f); Set(material, "_LightMinLimit", .20f); Set(material, "_LightMaxLimit", 1f);
                        Set(material, "_UseShadow", 1); Set(material, "_ShadowStrength", role == NprSurfaceRole.Face ? .60f : .66f);
                        Set(material, "_ShadowColor", new Color(.82f, .64f, .57f, 1));
                        Set(material, "_ShadowBlur", role == NprSurfaceRole.Face ? .23f : .20f);
                        Set(material, "_ShadowNormalStrength", role == NprSurfaceRole.Face ? .68f : 1);
                        Set(material, "_ShadowEnvStrength", .08f);
                    }
                    else if (role == NprSurfaceRole.Eyes)
                    {
                        Set(material, "_AsUnlit", .72f); Set(material, "_LightMinLimit", .78f); Set(material, "_LightMaxLimit", 1f);
                    }
                    else if (role == NprSurfaceRole.Hair)
                    {
                        if (id == "JesusAdult") Set(material, "_MainTexHSVG", new Vector4(.025f, .80f, 1.20f, .92f));
                        Set(material, "_AnisotropySpecularStrength", .24f);
                        Set(material, "_ReflectionColor", new Color(.62f, .43f, .28f, .10f));
                        Set(material, "_RimColor", new Color(.67f, .49f, .31f, .10f));
                        Set(material, "_LightMaxLimit", 1f);
                    }
                    else if (role == NprSurfaceRole.Linen || role == NprSurfaceRole.Mantle)
                    {
                        var normal = LinenNormal(); material.SetTexture("_BumpMap", normal);
                        material.SetTextureScale("_BumpMap", new Vector2(13, 13)); Set(material, "_UseBumpMap", 1); Set(material, "_BumpScale", .14f);
                        Set(material, "_ShadowColor", new Color(.62f, .65f, .76f, 1));
                        Set(material, "_ShadowStrength", .83f); Set(material, "_ShadowBlur", .16f); Set(material, "_LightMaxLimit", .98f);
                        Set(material, "_RimColor", new Color(.72f, .78f, .88f, .06f));
                    }
                    string output = Root + "/" + id + "/" + Safe(source.name) + ".mat";
                    materials[i] = Persist(material, output);
                    Need(before == EditorJsonUtility.ToJson(source), "Source material properties changed during derivation.");
                    rows.Add(new Surface { renderer = renderer.name, sourceMaterial = sourcePath, derivedMaterial = output,
                        role = role.ToString(), originalPropertiesSha256 = Hash(before), sourcePropertiesUnchanged = true });
                }
                renderer.sharedMaterials = materials;
            }
            return new Report { actor = id, surfaces = rows.ToArray(), actualVisualReviewCompleted = false };
        }

        static Material Fiber(Material reference, string id, bool beard)
        {
            string name = id + (beard ? "_CharacterBeard_HAIR" : "_LayeredHair_HAIR");
            var shader = Shader.Find("FirstGame/GospelNpr/lilToonOpaque"); Need(shader, "Retained real lilToon opaque shader is required.");
            var material = new Material(reference) { name = name, shader = shader };
            material.SetTexture("_MainTex", FiberTexture(id)); Set(material, "_Color", Color.white);
            Set(material, "_MainTexHSVG", new Vector4(0, 1, 1, 1));
            material.renderQueue = 2000; material.SetOverrideTag("RenderType", "Opaque");
            Set(material, "_SrcBlend", (float)BlendMode.One); Set(material, "_DstBlend", (float)BlendMode.Zero);
            Set(material, "_ZWrite", 1); Set(material, "_Cull", (float)CullMode.Back); Set(material, "_AlphaMaskMode", 0);
            Set(material, "_UseShadow", 1); Set(material, "_ShadowColor", new Color(.57f, .47f, .48f, 1)); Set(material, "_ShadowStrength", .72f);
            Set(material, "_ShadowBlur", .22f); Set(material, "_AsUnlit", .03f); Set(material, "_LightMaxLimit", .98f);
            Set(material, "_UseRim", 0); Set(material, "_UseReflection", beard ? 0 : 1); Set(material, "_ApplySpecular", beard ? 0 : 1);
            Set(material, "_UseAnisotropy", beard ? 0 : 1); Set(material, "_AnisotropySpecularStrength", beard ? 0 : .18f);
            Set(material, "_ReflectionColor", new Color(.62f, .42f, .24f, .08f)); Set(material, "_Smoothness", .22f);
            return Persist(material, Root + "/" + id + "/" + name + ".mat");
        }
        static Texture2D FiberTexture(string id)
        {
            string path = Root + "/Textures/" + id + "_FiberV1.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path); if (existing) return existing;
            Folder(Path.GetDirectoryName(path));
            var texture = new Texture2D(64, 256, TextureFormat.RGBA32, true, false) { name = id + " Fiber V1", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 2 };
            Color color = id == "Teacher" ? new Color(.36f, .34f, .31f) : id == "Joseph" ? new Color(.25f, .13f, .065f) : new Color(.21f, .095f, .045f);
            for (int y = 0; y < 256; y++) for (int x = 0; x < 64; x++)
            {
                float strand = .94f + .06f * Mathf.Sin(x * 2.3f + .12f * Mathf.Sin(y * .045f)) + .025f * Mathf.Sin(x * 5.2f + y * .007f);
                float light = .90f + .10f * Mathf.Sin(Mathf.PI * y / 255f);
                var c = color * strand * light; c.a = 1; texture.SetPixel(x, y, c);
            }
            texture.Apply(true, false); AssetDatabase.CreateAsset(texture, path); return texture;
        }
        static Texture2D LinenNormal()
        {
            string path = Root + "/Textures/LinenNormalV1.png";
            if (!File.Exists(path))
            {
                Folder(Path.GetDirectoryName(path)); var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false, true);
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                {
                    var n = new Vector3(.14f * Mathf.Sin(x * Mathf.PI / 4), .14f * Mathf.Sin(y * Mathf.PI / 4), 1).normalized;
                    texture.SetPixel(x, y, new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1));
                }
                texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(path);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.NormalMap;
            importer.sRGBTexture = false; importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear; importer.mipmapEnabled = true; importer.maxTextureSize = 64;
            importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static Material Persist(Material candidate, string path)
        {
            Folder(Path.GetDirectoryName(path)); var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing)
            {
                Need(EditorJsonUtility.ToJson(existing) == EditorJsonUtility.ToJson(candidate), "Preserve a differing derived material; use a new render revision: " + path);
                UnityEngine.Object.DestroyImmediate(candidate); return existing;
            }
            AssetDatabase.CreateAsset(candidate, path); AssetDatabase.SaveAssetIfDirty(candidate); return candidate;
        }
        static string Safe(string name) => string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_'));
        static string Hash(string value) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant(); }
        static void Set(Material m, string key, float v) { if (m.HasProperty(key)) m.SetFloat(key, v); }
        static void Set(Material m, string key, Color v) { if (m.HasProperty(key)) m.SetColor(key, v); }
        static void Set(Material m, string key, Vector4 v) { if (m.HasProperty(key)) m.SetVector(key, v); }
        static void Need(UnityEngine.Object value, string message) { if (!value) throw new InvalidOperationException(message); }
        static void Need(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        static void Folder(string value)
        {
            string path = value.Replace('\\', '/'); if (AssetDatabase.IsValidFolder(path)) return;
            int cut = path.LastIndexOf('/'); if (cut > 0) Folder(path.Substring(0, cut)); AssetDatabase.CreateFolder(path.Substring(0, cut), path.Substring(cut + 1));
        }
    }
}
#endif
