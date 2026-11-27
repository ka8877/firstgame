using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace FirstGame.Editor
{
// Imports the fitted, UV-mapped portrait surfaces exported by the HM08 authoring tool.
public static class GospelSculptedCastBaker
{
    const string Art = "Assets/Art/GalileeAtelier/";
    static readonly string[] Actors = { "JesusChild", "Mary", "Joseph", "JesusAdult", "Teacher" };
    struct Delta
    {
        public int vertex;
        public Vector3 delta;
    }
    sealed class Part
    {
        public string name;
        public Vector3[] vertices, normals;
        public Vector2[] uv;
        public int[] triangles;
        public Delta[][] poses;
    }
    static string String(BinaryReader f) => System.Text.Encoding.UTF8.GetString(f.ReadBytes(f.ReadInt32()));
    static Vector3 V3(BinaryReader f) => new Vector3(f.ReadSingle(), f.ReadSingle(), f.ReadSingle());

    public static void Bake()
    {
        string input = Environment.GetEnvironmentVariable("FIRSTGAME_ATELIER_INPUT");
        if (string.IsNullOrEmpty(input))
            throw new InvalidOperationException(
                "Set FIRSTGAME_ATELIER_INPUT to the directory of fitted head exports.");
        foreach (var id in Actors)
            Install(id, Path.Combine(input, id + ".head"));
        AssetDatabase.SaveAssets();
    }

    static Part[] Read(string file, out string[] names)
    {
        using var f = new BinaryReader(File.OpenRead(file));
        if (System.Text.Encoding.ASCII.GetString(f.ReadBytes(4)) != "GH02")
            throw new InvalidDataException(file);
        int count = f.ReadInt32(), shapeCount = f.ReadInt32();
        names = Enumerable.Range(0, shapeCount).Select(i => String(f)).ToArray();
        var parts = new Part[count];
        for (int pi = 0; pi < count; pi++)
        {
            var p = parts[pi] = new Part { name = String(f) };
            int n = f.ReadInt32(), indices = f.ReadInt32();
            p.vertices = new Vector3[n];
            p.normals = new Vector3[n];
            p.uv = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                p.vertices[i] = V3(f);
                p.normals[i] = V3(f);
                p.uv[i] = new Vector2(f.ReadSingle(), f.ReadSingle());
            }
            p.triangles = Enumerable.Range(0, indices).Select(i => f.ReadInt32()).ToArray();
            p.poses = new Delta[shapeCount][];
            for (int j = 0; j < shapeCount; j++)
            {
                int length = f.ReadInt32();
                p.poses[j] = new Delta[length];
                for (int i = 0; i < length; i++)
                    p.poses[j][i] = new Delta { vertex = f.ReadInt32(), delta = V3(f) };
            }
        }
        if (f.BaseStream.Position != f.BaseStream.Length)
            throw new InvalidDataException("Unexpected trailing portrait data");
        return parts;
    }

    static Texture2D Texture(string file, bool color, bool cutout)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(Art + file);
        if (!importer)
            throw new InvalidOperationException("Missing portrait texture " + file);
        const int max = 1024;
        if (importer.sRGBTexture != color || importer.alphaIsTransparency != cutout ||
            importer.maxTextureSize != max || importer.wrapMode != TextureWrapMode.Clamp)
        {
            importer.sRGBTexture = color;
            importer.alphaIsTransparency = cutout;
            importer.maxTextureSize = max;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(Art + file);
    }
    static Material MaterialFor(string id, string part)
    {
        bool fiber = part == "hair" || part == "brows" || part == "lashes" || part == "beard";
        bool skin = part == "skin", eyes = part == "eyes";
        string matName = eyes ? "Amber irises" : part == "teeth" ? "Ivory enamel" : id + " " + part;
        string path = Art + matName + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("FirstGame/Galilee/Portrait surface");
        if (!shader)
            throw new InvalidOperationException("Portrait shader isn't imported");
        if (!mat)
        {
            mat = new Material(shader) { name = matName };
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        mat.SetColor("_Color", part == "teeth" ? new Color(.77f, .70f, .62f) : Color.white);
        mat.SetFloat("_SurfaceKind", skin ? 0 : fiber ? 1 : eyes ? 2 : 3);
        mat.SetFloat("_Cull", fiber ? 0 : 2);
        mat.SetFloat("_Cutoff", part == "brows" ? .15f : part == "lashes" ? .18f : .25f);
        mat.SetFloat("_ReliefStrength", skin ? .38f : 0f);
        if (skin)
            mat.SetTexture("_Relief", Texture(id + "-relief.jpg", false, false));
        if (part != "teeth")
            mat.SetTexture("_BaseMap", Texture(eyes ? "brown-eye.png" : id + "-" + part + (skin ? ".jpg" : ".png"),
                                               true, fiber));
        mat.SetFloat("_AlphaClip", fiber ? 1 : 0);
        if (fiber)
        {
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = 2450;
            mat.SetOverrideTag("RenderType", "TransparentCutout");
        }
        else
        {
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.renderQueue = 2000;
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Skinning in model space avoids applying a nested FBX's import scale twice.
    internal static Vector3[] WorldVertices(SkinnedMeshRenderer renderer)
    {
        var mesh = renderer.sharedMesh;
        var p = mesh.vertices;
        var w = mesh.boneWeights;
        var b = renderer.bones;
        var bind = mesh.bindposes;
        var matrices = b.Select((t, i) => t.localToWorldMatrix * bind[i]).ToArray();
        var vertices = new Vector3[p.Length];
        for (int i = 0; i < p.Length; i++)
        {
            var weights = w[i];
            vertices[i] = matrices[weights.boneIndex0].MultiplyPoint3x4(p[i]) * weights.weight0 +
                          matrices[weights.boneIndex1].MultiplyPoint3x4(p[i]) * weights.weight1 +
                          matrices[weights.boneIndex2].MultiplyPoint3x4(p[i]) * weights.weight2 +
                          matrices[weights.boneIndex3].MultiplyPoint3x4(p[i]) * weights.weight3;
        }
        return vertices;
    }
    static T Store<T>(T value, string path)
        where T : UnityEngine.Object
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (!existing)
        {
            AssetDatabase.CreateAsset(value, path);
            return value;
        }
        if (existing == value)
        {
            EditorUtility.SetDirty(existing);
            return existing;
        }
        EditorUtility.CopySerialized(value, existing);
        UnityEngine.Object.DestroyImmediate(value);
        EditorUtility.SetDirty(existing);
        return existing;
    }
    static void TrimBase(string id, SkinnedMeshRenderer body, Transform neck)
    {
        string dest = Art + id + " neckline.asset";
        if (AssetDatabase.GetAssetPath(body.sharedMesh) == dest)
            return;
        var mesh = UnityEngine.Object.Instantiate(body.sharedMesh);
        var points = WorldVertices(body);
        for (int i = 0; i < body.sharedMaterials.Length; i++)
        {
            var name = body.sharedMaterials[i].name.ToLowerInvariant();
            if (name.Contains("hair"))
            {
                mesh.SetTriangles(Array.Empty<int>(), i);
                continue;
            }
            if (!name.Contains("skin"))
                continue;
            var src = mesh.GetTriangles(i);
            var visible = new List<int>();
            for (int j = 0; j < src.Length; j += 3)
            {
                float y = (points[src[j]].y + points[src[j + 1]].y + points[src[j + 2]].y) / 3;
                if (y < neck.position.y + .004f)
                {
                    visible.Add(src[j]);
                    visible.Add(src[j + 1]);
                    visible.Add(src[j + 2]);
                }
            }
            mesh.SetTriangles(visible, i);
        }
        mesh.name = id + " torso at collar";
        body.sharedMesh = Store(mesh, dest);
    }

    static void Install(string id, string file)
    {
        var parts = Read(file, out var poseNames);
        string folder = id == "JesusChild" ? "GospelHero20261119" : "GospelNpr";
        string path = "Assets/Resources/Art/" + folder + "/Characters/" + id + ".prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var rig = root.GetComponentInChildren<Animator>();
            var head = rig.GetBoneTransform(HumanBodyBones.Head);
            var neck = rig.GetBoneTransform(HumanBodyBones.Neck);
            var all = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var old = all.Single(r => r.name == (id == "JesusChild" ? "Face" : id + "__Face"));
            var eyeSlot = Array.FindIndex(old.sharedMaterials,
                                          m => m.name.IndexOf("EyeIris", StringComparison.OrdinalIgnoreCase) >= 0);
            var points = WorldVertices(old);
            var eyeIds = old.sharedMesh.GetTriangles(eyeSlot).Distinct().ToArray();
            var eyeBounds = new Bounds(points[eyeIds[0]], Vector3.zero);
            foreach (var i in eyeIds)
                eyeBounds.Encapsulate(points[i]);
            var node = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(
                t => t.name == id + "__SculptedPortrait");
            if (!node)
                node = new GameObject(id + "__SculptedPortrait").transform;
            node.SetParent(null, true);
            node.SetPositionAndRotation(eyeBounds.center - root.transform.forward * .011f, root.transform.rotation);
            node.localScale = Vector3.one;
            node.SetParent(head, true);
            var render = node.GetComponent<SkinnedMeshRenderer>();
            if (!render)
                render = node.gameObject.AddComponent<SkinnedMeshRenderer>();
            string output = Art + id + " portrait.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(output);
            if (mesh)
            {
                mesh.ClearBlendShapes();
                mesh.Clear(false);
            }
            else
                mesh = new Mesh();
            mesh.name = id + " fitted portrait and groom";
            mesh.SetVertices(parts.SelectMany(p => p.vertices).ToList());
            mesh.SetNormals(parts.SelectMany(p => p.normals).ToList());
            mesh.SetUVs(0, parts.SelectMany(p => p.uv).ToList());
            mesh.subMeshCount = parts.Length;
            var weights = new List<BoneWeight>();
            var materials = new List<Material>();
            int offset = 0;
            foreach (var p in parts)
            {
                mesh.SetTriangles(p.triangles.Select(k => k + offset).ToArray(), materials.Count);
                materials.Add(MaterialFor(id, p.name));
                foreach (var v in p.vertices)
                {
                    float y = node.TransformPoint(v).y;
                    float blend =
                        1 - Mathf.SmoothStep(
                                0, 1, Mathf.InverseLerp(neck.position.y - .014f, head.position.y + .010f, y));
                    if (p.name != "skin" && p.name != "hair")
                        blend = 0;
                    if (p.name == "hair")
                        blend *= .40f;
                    weights.Add(
                        new BoneWeight { boneIndex0 = 0, weight0 = 1 - blend, boneIndex1 = 1, weight1 = blend });
                }
                offset += p.vertices.Length;
            }
            mesh.boneWeights = weights.ToArray();
            render.bones = new[] { head, neck };
            render.rootBone = node;
            mesh.bindposes = render.bones.Select(b => b.worldToLocalMatrix * node.localToWorldMatrix).ToArray();
            for (int i = 0; i < poseNames.Length; i++)
            {
                var delta = new Vector3[mesh.vertexCount];
                int start = 0;
                foreach (var part in parts)
                {
                    foreach (var d in part.poses[i])
                        delta[start + d.vertex] = d.delta;
                    start += part.vertices.Length;
                }
                mesh.AddBlendShapeFrame(poseNames[i], 100f, delta, null, null);
            }
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            render.sharedMesh = Store(mesh, output);
            render.localBounds = render.sharedMesh.bounds;
            render.updateWhenOffscreen = true;
            render.sharedMaterials = materials.ToArray();
            foreach (var r in all)
                if (r == old || r.name == "Hair001" || r.name.EndsWith("__Hair") ||
                    r.name.EndsWith("__CharacterBeard") || r.name.EndsWith("__LayeredHairV1"))
                    r.enabled = false;
            TrimBase(id, all.Single(r => r.name == (id == "JesusChild" ? "Body" : id + "__Body")), neck);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log("GALILEE_PRINCIPAL " + id + " vertices=" + render.sharedMesh.vertexCount + " eyes=" +
                      eyeBounds.center.ToString("F3") + " height=" + render.localBounds.size.y.ToString("F3"));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
}
