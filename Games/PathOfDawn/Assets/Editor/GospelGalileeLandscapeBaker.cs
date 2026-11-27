using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FirstGame.Editor
{
public static class GospelGalileeLandscapeBaker
{
    const string Art = "Assets/Art/GalileeAtelier/";
    const string Resources = "Assets/Resources/Art/GalileeAtelier/";
    public static void Bake()
    {
        Directory.CreateDirectory(Resources);
        var vista = Texture("galilee-terraces.jpg", 2048, false);
        var background = Material(Resources + "Terraces.mat", "FirstGame/Galilee/Limestone terraces");
        background.SetTexture("_BaseMap", vista);
        EditorUtility.SetDirty(background);
        foreach (string group in new[] { "aerial_grass_rock", "stony_dirt_path" })
            foreach (string map in new[] { "-diffuse.jpg", "-normal.jpg" })
            {
                string path = Resources + group + map;
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (!importer)
                    throw new InvalidOperationException(path);
                importer.textureType =
                    map == "-normal.jpg" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = map != "-normal.jpg";
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.maxTextureSize = 1024;
                importer.anisoLevel = 4;
                importer.filterMode = FilterMode.Trilinear;
                importer.SaveAndReimport();
            }
        var slope = Material(Resources + "Orchard slope.mat", "FirstGame/Galilee/Loom");
        slope.SetTexture("_BaseMap",
                         AssetDatabase.LoadAssetAtPath<Texture2D>(Resources + "aerial_grass_rock-diffuse.jpg"));
        slope.SetColor("_Color", new Color(.98f, .97f, .83f));
        slope.SetFloat("_Sheen", .02f);
        EditorUtility.SetDirty(slope);
        var edge = Material(Resources + "Canopy border.mat", "FirstGame/Galilee/Loom");
        edge.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Mary-tabletweave.jpg"));
        edge.SetColor("_Color", new Color(.88f, .80f, .67f));
        edge.SetFloat("_Sheen", .10f);
        EditorUtility.SetDirty(edge);
        var bark = Material(Art + "Twisted olive bark.mat", "FirstGame/Galilee/Olive grove");
        bark.SetColor("_Color", new Color(.89f, .87f, .82f));
        EditorUtility.SetDirty(bark);
        var leaves = Material(Art + "Silver olive leaves.mat", "FirstGame/Galilee/Olive grove");
        leaves.SetTexture("_BaseMap", Texture("olive-spray.png", 256, true));
        leaves.SetFloat("_Leaves", 1);
        leaves.EnableKeyword("_OLIVE_LEAVES");
        EditorUtility.SetDirty(leaves);
        string source = Environment.GetEnvironmentVariable("FIRSTGAME_ATELIER_INPUT");
        if (string.IsNullOrWhiteSpace(source))
            throw new InvalidOperationException("Provide the olive.grove export directory");
        using var f = new BinaryReader(File.OpenRead(Path.Combine(source, "olive.grove")));
        int n = f.ReadInt32(), b = f.ReadInt32(), t = f.ReadInt32();
        var points = new Vector3[n];
        var normals = new Vector3[n];
        var uv = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            points[i] = Vec(f);
            normals[i] = Vec(f);
            uv[i] = new Vector2(f.ReadSingle(), f.ReadSingle());
        }
        var color = new Color32[n];
        for (int i = 0; i < n; i++)
            color[i] = new Color32(f.ReadByte(), f.ReadByte(), f.ReadByte(), f.ReadByte());
        var trunk = Indices(f, b);
        var crown = Indices(f, t);
        if (f.BaseStream.Position != f.BaseStream.Length)
            throw new InvalidDataException("Unexpected olive geometry data");
        string file = Art + "Terrace olive.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(file);
        bool fresh = !mesh;
        if (fresh)
            mesh = new Mesh();
        else
            mesh.Clear(false);
        mesh.name = "Terrace olive";
        mesh.vertices = points;
        mesh.normals = normals;
        mesh.uv = uv;
        mesh.colors32 = color;
        mesh.subMeshCount = 2;
        mesh.SetTriangles(trunk, 0);
        mesh.SetTriangles(crown, 1);
        mesh.RecalculateBounds();
        if (fresh)
            AssetDatabase.CreateAsset(mesh, file);
        else
            EditorUtility.SetDirty(mesh);
        Model("OliveTree_Galilee", mesh, bark, leaves);
        var foliage = Material(Art + "Mastic leaves.mat", "FirstGame/Galilee/Olive grove");
        foliage.SetTexture("_BaseMap", leaves.GetTexture("_BaseMap"));
        foliage.SetColor("_Color", new Color(.79f, .87f, .66f));
        foliage.SetFloat("_Leaves", 1);
        foliage.EnableKeyword("_OLIVE_LEAVES");
        EditorUtility.SetDirty(foliage);
        string bushPath = Art + "Village mastic.asset";
        var bush = AssetDatabase.LoadAssetAtPath<Mesh>(bushPath);
        bool newBush = !bush;
        if (newBush)
            bush = new Mesh();
        else
            bush.Clear(false);
        var low = new Vector3[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            var p = points[i];
            low[i] = new Vector3(p.x * 1.06f + .08f * Mathf.Sin(p.z * 2.4f),
                                 Mathf.Max(0, p.y - .94f) * .54f + Mathf.Min(1, p.y) * .06f, p.z);
        }
        bush.name = "Rounded mastic with individual sprays";
        bush.vertices = low;
        bush.uv = uv;
        bush.colors32 = color;
        bush.subMeshCount = 2;
        bush.SetTriangles(trunk, 0);
        bush.SetTriangles(crown, 1);
        bush.RecalculateNormals();
        bush.RecalculateBounds();
        if (newBush)
            AssetDatabase.CreateAsset(bush, bushPath);
        else
            EditorUtility.SetDirty(bush);
        Model("MasticBush", bush, bark, foliage);
        AssetDatabase.SaveAssets();
    }
    static void Model(string name, Mesh mesh, Material bark, Material leaves)
    {
        var tree = new GameObject(name);
        try
        {
            tree.AddComponent<MeshFilter>().sharedMesh = mesh;
            tree.AddComponent<MeshRenderer>().sharedMaterials = new[] { bark, leaves };
            PrefabUtility.SaveAsPrefabAsset(tree, "Assets/Resources/Art/GospelNpr/Environment/" + name + ".prefab");
        }
        finally
        {
            Object.DestroyImmediate(tree);
        }
    }
    static Vector3 Vec(BinaryReader f) => new Vector3(f.ReadSingle(), f.ReadSingle(), f.ReadSingle());
    static int[] Indices(BinaryReader f, int count)
    {
        var a = new int[count];
        for (int i = 0; i < count; i++)
            a[i] = f.ReadInt32();
        return a;
    }
    static Texture2D Texture(string file, int size, bool transparent)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(Art + file);
        if (!imp)
            throw new InvalidOperationException(file);
        imp.alphaIsTransparency = transparent;
        imp.maxTextureSize = size;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.mipmapEnabled = true;
        imp.sRGBTexture = true;
        imp.filterMode = FilterMode.Trilinear;
        imp.anisoLevel = 2;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(Art + file);
    }
    static Material Material(string path, string shaderName)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find(shaderName);
        if (!shader)
            throw new InvalidOperationException(shaderName);
        if (!m)
        {
            m = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = shader;
        EditorUtility.SetDirty(m);
        return m;
    }
}
}
