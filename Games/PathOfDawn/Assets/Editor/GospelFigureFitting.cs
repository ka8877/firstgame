using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FirstGame.Editor
{
    /// <summary>Bind a measured, fully three-dimensional costume to the existing expressive travelling
    /// cast.</summary>
    public static class GospelFigureFitting
    {
        const string Art = "Assets/Art/GalileeFigure/";
        const string PreviousArt = "Assets/Art/GalileeAtelier/";
        static readonly string[] Actors = { "JesusChild", "Mary", "Joseph", "JesusAdult", "Teacher" };

        static Vector2 Read2(BinaryReader s) => new Vector2(s.ReadSingle(), s.ReadSingle());
        static Vector3 Read3(BinaryReader s) => new Vector3(s.ReadSingle(), s.ReadSingle(), s.ReadSingle());
        static string ReadName(BinaryReader s)
        {
            int size = s.ReadInt32();
            if (size < 1 || size > 120)
                throw new InvalidDataException("Invalid fitting bone name");
            return System.Text.Encoding.UTF8.GetString(s.ReadBytes(size));
        }

        static Material Fabric(string id)
        {
            string atlas = Art + id + "-figure.jpg";
            var importer = AssetImporter.GetAtPath(atlas) as TextureImporter;
            if (!importer)
                throw new FileNotFoundException(atlas);
            var web = importer.GetPlatformTextureSettings("WebGL");
            if (importer.maxTextureSize != 2048 || importer.wrapMode != TextureWrapMode.Clamp || !web.overridden ||
                web.format != TextureImporterFormat.DXT1)
            {
                importer.maxTextureSize = 2048;
                importer.sRGBTexture = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 4;
                importer.mipmapEnabled = true;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                web.name = "WebGL";
                web.overridden = true;
                web.maxTextureSize = 2048;
                web.format = TextureImporterFormat.DXT1;
                web.compressionQuality = 85;
                importer.SetPlatformTextureSettings(web);
                importer.SaveAndReimport();
            }
            string path = Art + id + " fitted garment.mat";
            var shader = Shader.Find("FirstGame/Galilee/Fitted figure");
            if (!shader)
                throw new InvalidOperationException("The fitted figure shader did not import");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(shader) { name = id + " fitted garment" };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(atlas));
            mat.SetTexture("_AnatomyMap",
                           AssetDatabase.LoadAssetAtPath<Texture2D>(PreviousArt + id + "-exposed.jpg"));
            mat.SetTexture("_HideMap",
                           AssetDatabase.LoadAssetAtPath<Texture2D>(PreviousArt + "brown_leather-color.jpg"));
            mat.SetTexture("_WeaveNormal",
                           AssetDatabase.LoadAssetAtPath<Texture2D>(PreviousArt + "rough_linen-normal.jpg"));
            if (id == "Mary" || id == "Teacher")
                mat.SetTexture("_BorderMap",
                               AssetDatabase.LoadAssetAtPath<Texture2D>(PreviousArt + id + "-tabletweave.jpg"));
            if (id == "Mary" || id == "Teacher")
            {
                mat.SetTexture("_VeilWeave",
                               AssetDatabase.LoadAssetAtPath<Texture2D>(PreviousArt + "rough_linen-color.jpg"));
                ColorUtility.TryParseHtmlString(id == "Mary" ? "#47615d" : "#74644f", out var dye);
                mat.SetColor("_VeilTint", dye);
            }
            mat.SetColor("_Color", Color.white);
            mat.SetColor("_SkinBalance", Color.white);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void FitNeckOpening(GameObject root, string id, Vector4 registration, Texture fittedPigment)
        {
            var portrait = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(
                r => r.name == id + "__SculptedPortrait");
            var mesh = portrait.sharedMesh;
            var animator = root.GetComponentInChildren<Animator>();
            var neck = animator.GetBoneTransform(HumanBodyBones.Neck).position;
            float stature = animator.GetBoneTransform(HumanBodyBones.Head).position.y;
            var skin = Array.FindIndex(portrait.sharedMaterials, m => m.name == id + " skin");
            var p = GospelSculptedCastBaker.WorldVertices(portrait);
            var source = mesh.GetTriangles(skin);
            var vertices = mesh.vertices;
            // Fit the collar around a continuous throat. Move only the former shoulder
            // fan inward; the jaw and its complete expression topology keep their rest shape.
            foreach (int i in source.Distinct())
            {
                var offset = (p[i] - neck) / stature;
                if (offset.y >= .016f)
                    continue;
                float rx = Mathf.Lerp(.034f, .047f, Mathf.InverseLerp(-.035f, .016f, offset.y));
                float rz = Mathf.Lerp(.038f, .047f, Mathf.InverseLerp(-.035f, .016f, offset.y));
                float radius = new Vector2(offset.x / rx, (offset.z - .006f) / rz).magnitude;
                if (radius <= 1.001f)
                    continue;
                var dest =
                    neck + stature * new Vector3(offset.x / radius, offset.y, .006f + (offset.z - .006f) / radius);
                vertices[i] += portrait.transform.InverseTransformVector(dest - p[i]);
            }
            mesh.vertices = vertices;
            var materials = portrait.sharedMaterials;
            var eyes = Array.FindIndex(materials, m => m.name == id + " eyes");
            var eyeIds = mesh.GetTriangles(eyes).Distinct().ToArray();
            var eyeBounds = new Bounds(p[eyeIds[0]], Vector3.zero);
            foreach (int i in eyeIds)
                eyeBounds.Encapsulate(p[i]);
            var eye = eyeBounds.center;
            var maps = new Vector4[mesh.vertexCount];
            var weights = new Vector2[mesh.vertexCount];
            if (!fittedPigment || !AssetDatabase.Contains(fittedPigment))
                throw new InvalidOperationException("Missing persistent figure texture for " + id);
            for (int slot = 0; slot < materials.Length; slot++)
            {
                var mat = materials[slot];
                bool isSkin = slot == skin;
                if (!isSkin && mat.name != id + " hair" && mat.name != id + " crown" && mat.name != id + " wisps")
                    continue;
                mat.SetTexture("_TailoredPigment", fittedPigment);
                EditorUtility.SetDirty(mat);
                AssetDatabase.SaveAssetIfDirty(mat);
                foreach (int i in mesh.GetTriangles(slot).Distinct())
                {
                    var pos = (p[i] - eye) / registration.x;
                    float y = registration.z + pos.y;
                    float angle = (new Vector2(pos.x, pos.z + .049f)).normalized.y;
                    float side = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.85f, -.08f, angle));
                    float back = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.05f, .85f, angle));
                    float pigment = (isSkin ? .69f : .91f) * back *
                                    Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.115f, -.037f, pos.y));
                    float row = Mathf.Clamp(y, registration.z - .066f, .989f);
                    float rear = (1162.5f + 944 * pos.x) / 2048f;
                    float profile = (1934.5f + .83f * (registration.w - 400 + 944 * pos.z)) / 2048f;
                    if (isSkin && pos.y < -.045f)
                    {
                        // Carry the photographed skin continuously across the physical neck to the collar.
                        float neckMix = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.092f, -.045f, pos.y));
                        row = Mathf.Clamp(y, registration.y + .011f, registration.y + .051f);
                        rear = (387.5f - 944 * Mathf.Clamp(pos.x * .45f, -.021f, .021f)) / 2048f;
                        side = 0;
                        pigment = neckMix * .87f;
                    }
                    float v = 1 - (986.5f - 944 * row) / 1024f;
                    maps[i] = new Vector4(rear, v, profile, v);
                    weights[i] = new Vector2(side, pigment);
                }
            }
            mesh.SetUVs(2, new List<Vector4>(maps));
            mesh.SetUVs(3, new List<Vector2>(weights));
            EditorUtility.SetDirty(mesh);
        }

        static void Fit(GameObject root, string id, string file)
        {
            using var input = new BinaryReader(File.OpenRead(file));
            if (System.Text.Encoding.ASCII.GetString(input.ReadBytes(4)) != "GFS3")
                throw new InvalidDataException(file);
            var registration =
                new Vector4(input.ReadSingle(), input.ReadSingle(), input.ReadSingle(), input.ReadSingle());
            var nodes = root.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.GetInstanceID());
            int boneCount = input.ReadInt32();
            if (boneCount < 20 || boneCount > 100)
                throw new InvalidDataException("Invalid fitting skeleton");
            var bones = new Transform[boneCount];
            for (int b = 0; b < boneCount; b++)
            {
                string name = ReadName(input);
                bones[b] = nodes.Values.Single(t => t.name == name);
            }
            int count = input.ReadInt32(), indices = input.ReadInt32();
            if (count < 1000 || count > 200000 || indices < 3 || indices % 3 != 0)
                throw new InvalidDataException("Invalid fitting topology");
            var points = new Vector3[count];
            var grain = new Vector2[count];
            var frontBack = new List<Vector4>(count);
            var sideWeight = new List<Vector4>(count);
            var surface = new Color32[count];
            var skin = new BoneWeight[count];
            var hems = new Vector2[count];
            var stride = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                points[i] = Read3(input);
                grain[i] = Read2(input);
                var front = Read2(input);
                var back = Read2(input);
                var side = Read2(input);
                var views = Read3(input);
                frontBack.Add(new Vector4(front.x, front.y, back.x, back.y));
                sideWeight.Add(new Vector4(side.x, side.y, views.x, views.y));
                surface[i] =
                    new Color(input.ReadSingle(), input.ReadSingle(), input.ReadSingle(), input.ReadSingle());
                hems[i] = Read2(input);
                stride[i] = Read2(input);
                skin[i] = new BoneWeight { boneIndex0 = input.ReadInt32(), boneIndex1 = input.ReadInt32(),
                                           boneIndex2 = input.ReadInt32(), boneIndex3 = input.ReadInt32(),
                                           weight0 = input.ReadSingle(),   weight1 = input.ReadSingle(),
                                           weight2 = input.ReadSingle(),   weight3 = input.ReadSingle() };
            }
            var triangles = new int[indices];
            for (int i = 0; i < indices; i++)
            {
                triangles[i] = input.ReadInt32();
                if ((uint)triangles[i] >= (uint)count)
                    throw new InvalidDataException("Invalid triangle");
            }
            if (input.BaseStream.Position != input.BaseStream.Length)
                throw new InvalidDataException("Unexpected fitting data");
            string asset = Art + id + " tailored figure.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(asset);
            bool fresh = !mesh;
            if (fresh)
                mesh = new Mesh();
            else
            {
                mesh.ClearBlendShapes();
                mesh.Clear(false);
            }
            mesh.name = id + " sculpted travelling figure";
            mesh.indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = points;
            mesh.uv = grain;
            mesh.SetUVs(1, frontBack);
            mesh.SetUVs(2, sideWeight);
            mesh.colors32 = surface;
            mesh.triangles = triangles;
            mesh.boneWeights = skin;
            mesh.bindposes = bones.Select(b => b.worldToLocalMatrix * root.transform.localToWorldMatrix).ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            float span = root.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Neck).position.y;
            foreach (int side in new[] { -1, 1 })
            {
                var delta = new Vector3[count];
                for (int i = 0; i < count; i++)
                    delta[i] = span * hems[i].x *
                               new Vector3(.027f * side, .009f * (1 + side * hems[i].y),
                                           -.034f * (.64f + .36f * side * hems[i].y));
                mesh.AddBlendShapeFrame(side < 0 ? "MantleStepLeft" : "MantleStepRight", 100, delta, null, null);
                var sweep = new Vector3[count];
                for (int i = 0; i < count; i++)
                {
                    float influence = side < 0 ? stride[i].x : stride[i].y;
                    sweep[i] = new Vector3(.002f * side, .004f, .020f) * influence *span;
                }
                mesh.AddBlendShapeFrame(side < 0 ? "FrontHemLeft" : "FrontHemRight", 100, sweep, null, null);
            }
            mesh.RecalculateBounds();
            MeshUtility.SetMeshCompression(mesh, ModelImporterMeshCompression.Off);
            if (fresh)
                AssetDatabase.CreateAsset(mesh, asset);
            else
                EditorUtility.SetDirty(mesh);
            var renderer = root.transform.Find(id + "__TravelCostume").GetComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = new[] { Fabric(id) };
            renderer.bones = bones;
            renderer.rootBone = root.transform;
            renderer.localBounds = mesh.bounds;
            renderer.updateWhenOffscreen = true;
            renderer.enabled = true;
            FitNeckOpening(root, id, registration, renderer.sharedMaterial.GetTexture("_BaseMap"));
            foreach (var former in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (former.name == id + "__OpenVeil")
                    former.enabled = false;
        }

        public static void Bake()
        {
            string master = Environment.GetEnvironmentVariable("FIRSTGAME_FIGURE_INPUT");
            if (string.IsNullOrWhiteSpace(master))
                throw new InvalidOperationException("Provide the external fitted figure directory");
            foreach (string id in Actors)
            {
                string folder = id == "JesusChild" ? "GospelHero20261119" : "GospelNpr";
                string path = "Assets/Resources/Art/" + folder + "/Characters/" + id + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Fit(root, id, Path.Combine(master, "fitted", id + ".figure"));
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FIRSTGAME_FIGURE_STUDIO")))
                GospelFigureStudio.Capture();
        }
    }
}
