using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FirstGame.Editor
{
    /// <summary>Installs the fitted sewing patterns with their original UV seams, skin weights and mantle motion.</summary>
    public static class GospelPrincipalCostumes
    {
        const string Art = "Assets/Art/GalileeAtelier/";
        static readonly string[] Cast = { "JesusChild", "Mary", "Joseph", "JesusAdult", "Teacher" };

        static string ReadText(BinaryReader input)
        {
            int length = input.ReadInt32();
            if (length < 1 || length > 100) throw new InvalidDataException("Invalid garment label");
            return System.Text.Encoding.UTF8.GetString(input.ReadBytes(length));
        }

        static Material Surface(string id, string kind)
        {
            bool mantle = kind == "mantle", gown = kind == "underdress", exposed = kind == "exposed";
            string label = mantle ? id + " woven mantle" : gown ? id + " sewn tunic" : kind == "collar" ? id + " folded collar" : exposed ? id + " exposed skin" : "Galilee " + kind;
            string path = Art + label + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(exposed ? "FirstGame/Galilee/Portrait surface" : "FirstGame/Galilee/Loom");
            if (!shader) throw new InvalidOperationException("Missing woven surface shader");
            if (!mat)
            {
                mat = new Material(shader) { name = label };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            string texture = mantle || gown || exposed || kind == "collar" ? id + "-" + kind + ".jpg" : kind == "sandals" ? "brown_leather-color.jpg" : kind == "apron" || kind == "leather" ? "brown_leather-color.jpg" : null;
            if (texture != null)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(Art + texture);
                if (!importer) throw new InvalidDataException(texture);
                if (importer.maxTextureSize != 1024 || importer.wrapMode != TextureWrapMode.Repeat || importer.anisoLevel != 4)
                {
                    importer.maxTextureSize = 1024;
                    importer.wrapMode = TextureWrapMode.Repeat;
                    importer.anisoLevel = 4;
                    importer.filterMode = FilterMode.Trilinear;
                    importer.SaveAndReimport();
                }
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + texture));
            }
            if (exposed)
            {
                mat.SetFloat("_SurfaceKind", 0);
                mat.SetColor("_Color", Color.white);
                EditorUtility.SetDirty(mat);
                return mat;
            }
            bool leather = kind == "sandals" || kind == "apron" || kind == "leather";
            bool cloth = mantle || gown || kind == "undertunic" || kind == "collar";
            if (cloth || leather)
            {
                string stem = cloth ? "rough_linen" : "brown_leather";
                string mapPath = Art + stem + "-normal.jpg";
                var normal = (TextureImporter)AssetImporter.GetAtPath(mapPath);
                if (normal.textureType != TextureImporterType.NormalMap || normal.maxTextureSize != 512 || normal.wrapMode != TextureWrapMode.Repeat)
                {
                    normal.textureType = TextureImporterType.NormalMap;
                    normal.sRGBTexture = false; normal.wrapMode = TextureWrapMode.Repeat;
                    normal.maxTextureSize = 512; normal.anisoLevel = 4; normal.SaveAndReimport();
                }
                mat.SetTexture("_TextileNormal", AssetDatabase.LoadAssetAtPath<Texture2D>(mapPath));
                mat.SetTexture("_TextileGrain", AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"rough_linen-color.jpg"));
                mat.SetFloat("_YarnStrength", cloth ? .55f : .44f);
                mat.SetFloat("_GrainStrength", cloth ? .28f : .04f);
                mat.SetFloat("_ThreadCount", mantle ? 4.4f : gown ? 6.8f : 2.4f);
            }
            ColorUtility.TryParseHtmlString(kind == "bronze" ? "#a27d47" : kind == "stone" ? "#4c807f" :
                kind == "sandals" ? "#744d37" : kind == "undertunic" ? "#b28e65" : kind == "leather" ? "#c5a389" : ColorCode(kind), out var dye);
            mat.SetColor("_Color", dye);
            mat.SetFloat("_Lining", 0);
            mat.SetFloat("_Sheen", kind == "bronze" || kind == "stone" ? .86f : kind == "apron" || kind == "leather" ? .32f : .17f);
            mat.SetFloat("_Metal", kind == "bronze" ? .75f : kind == "stone" ? .30f : 0);
            EditorUtility.SetDirty(mat);
            return mat;
        }
        static string ColorCode(string kind) => kind == "apron" ? "#a27558" : "#ffffff";

        static void Install(GameObject root, string id, string inputPath)
        {
            using var input = new BinaryReader(File.OpenRead(inputPath));
            if (System.Text.Encoding.ASCII.GetString(input.ReadBytes(4)) != "GW01")
                throw new InvalidDataException(inputPath);
            var hierarchy = root.GetComponentsInChildren<Transform>(true);
            var bones = new Transform[input.ReadInt32()];
            for (int i = 0; i < bones.Length; i++)
            {
                string name = ReadText(input);
                bones[i] = hierarchy.Single(t => t.name == name);
            }
            var p = new List<Vector3>();
            var uv = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var motion = new List<Vector2>();
            var names = new List<string>();
            var triangles = new List<List<int>>();
            int count = input.ReadInt32();
            for (int n = 0; n < count; n++)
            {
                string key = ReadText(input);
                int section = names.IndexOf(key);
                if (section < 0)
                {
                    section = names.Count;
                    names.Add(key);
                    triangles.Add(new List<int>());
                }
                int vertices = input.ReadInt32(), indices = input.ReadInt32(), first = p.Count;
                for (int i = 0; i < vertices; i++)
                {
                    p.Add(new Vector3(input.ReadSingle(), input.ReadSingle(), input.ReadSingle()));
                    uv.Add(new Vector2(input.ReadSingle(), input.ReadSingle()));
                    weights.Add(new BoneWeight {
                        boneIndex0 = input.ReadInt32(), boneIndex1 = input.ReadInt32(),
                        boneIndex2 = input.ReadInt32(), boneIndex3 = input.ReadInt32(),
                        weight0 = input.ReadSingle(), weight1 = input.ReadSingle(),
                        weight2 = input.ReadSingle(), weight3 = input.ReadSingle()
                    });
                    motion.Add(new Vector2(input.ReadSingle(), input.ReadSingle()));
                }
                for (int i = 0; i < indices; i++)
                {
                    int local = input.ReadInt32();
                    if (local < 0 || local >= vertices) throw new InvalidDataException(key);
                    triangles[section].Add(first + local);
                }
            }
            if (input.BaseStream.Position != input.BaseStream.Length) throw new InvalidDataException(inputPath);
            string path = Art + id + " travel costume.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool fresh = !mesh;
            if (fresh) mesh = new Mesh();
            else { mesh.ClearBlendShapes(); mesh.Clear(false); }
            mesh.name = id + " fitted travelling layers";
            mesh.SetVertices(p); mesh.SetUVs(0, uv);
            mesh.subMeshCount = triangles.Count;
            for (int i = 0; i < triangles.Count; i++) mesh.SetTriangles(triangles[i], i);
            mesh.boneWeights = weights.ToArray();
            mesh.bindposes = bones.Select(t => t.worldToLocalMatrix * root.transform.localToWorldMatrix).ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            float stature = root.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Neck).position.y;
            foreach (int side in new[] { -1, 1 })
            {
                var deltas = new Vector3[p.Count];
                for (int i = 0; i < p.Count; i++)
                {
                    float amplitude = motion[i].x, lateral = motion[i].y;
                    deltas[i] = stature * amplitude * new Vector3(.032f * side, .008f * (1 + side * lateral),
                        -.034f * (.62f + .38f * side * lateral));
                }
                mesh.AddBlendShapeFrame(side < 0 ? "MantleStepLeft" : "MantleStepRight", 100, deltas, null, null);
            }
            mesh.RecalculateBounds();
            if (fresh) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            string nodeName = id + "__TravelCostume";
            var node = root.transform.Find(nodeName);
            if (!node) { node = new GameObject(nodeName).transform; node.SetParent(root.transform, false); }
            var renderer = node.GetComponent<SkinnedMeshRenderer>();
            if (!renderer) renderer = node.gameObject.AddComponent<SkinnedMeshRenderer>();
            renderer.bones = bones; renderer.rootBone = root.transform;
            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = names.Select(name => Surface(id, name)).ToArray();
            renderer.localBounds = mesh.bounds; renderer.updateWhenOffscreen = true;
            foreach (var previous in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (previous == renderer || previous.name.EndsWith("__SculptedPortrait")) continue;
                if (previous.name == "Body" || previous.name.EndsWith("__Body"))
                    previous.enabled = false;
                else if (previous.name.EndsWith("__OpenVeil") && id == "Mary")
                    previous.sharedMaterial = Surface(id, "mantle");
                // The source face and beard still supply the original lip and emotion bindings.
                else if (id != "JesusChild" && previous.name != id + "__Face" && previous.name != id + "__CharacterBeard")
                    UnityEngine.Object.DestroyImmediate(previous);
                else previous.enabled = false;
            }
        }

        public static void Bake()
        {
            string input = Environment.GetEnvironmentVariable("FIRSTGAME_ATELIER_INPUT");
            if (string.IsNullOrWhiteSpace(input)) throw new InvalidOperationException("Provide the fitted garment exports.");
            foreach (string id in Cast)
            {
                string folder = id == "JesusChild" ? "GospelHero20261119" : "GospelNpr";
                string path = "Assets/Resources/Art/" + folder + "/Characters/" + id + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Install(root, id, Path.Combine(input, id + ".garment"));
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
        }
    }
}
