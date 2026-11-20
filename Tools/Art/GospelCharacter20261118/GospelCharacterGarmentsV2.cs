#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Firstgame.GospelNpr.Editor
{
    // Root invokes this on the neutral, already tailored/refined actor. It adds
    // one thin closed garment; it never edits an original/V5 mesh or material.
    public static class GospelCharacterGarmentsV2
    {
        const string Root = "Assets/Art/GospelCharacter20261118/GarmentsV1";
        const int Segments = 48, Rows = 5;
        const float Thickness = .0025f, Clearance = .006f;
        [Serializable] public sealed class Report
        {
            public string id, renderer, meshAsset, meshHash, materialAsset, sourceBodyAsset, sourceBodyHash;
            public int sourceSkinTriangles, sampledVertices, vertices, triangles, placketTriangles, maximumInfluences, indexCount, boundaryEdges, nonManifoldEdges;
            public float neckRadiusX, neckRadiusZ, shoulderHalfWidth, thickness, clearance, maximumBindRoundTripError;
            public Bounds actorBounds;
            public bool originalBodyUnchanged, originalBonesUnchanged, closedTwoManifold, finiteUvAndTangents, nativeMotionAndVisualReviewCompleted;
        }
        sealed class Surface
        {
            public Vector3[] points, normals;
            public int[] triangles;
            public float minY, maxY;
            public Vector3 Project(float x, float z, out Vector3 normal)
            {
                float best = float.NegativeInfinity; normal = Vector3.up;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int ia = triangles[i], ib = triangles[i + 1], ic = triangles[i + 2]; var a = points[ia]; var b = points[ib]; var c = points[ic];
                    float den = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                    if (Mathf.Abs(den) < 1e-9f) continue;
                    float u = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / den;
                    float v = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / den, w = 1 - u - v;
                    if (u < -.0001f || v < -.0001f || w < -.0001f) continue;
                    float y = a.y * u + b.y * v + c.y * w;
                    if (y < minY || y > maxY || y <= best) continue;
                    best = y; normal = (normals[ia] * u + normals[ib] * v + normals[ic] * w).normalized;
                }
                Need(!float.IsInfinity(best) && normal.sqrMagnitude > .5f, "Actual upper-body skin intersection is missing; do not invent a floating yoke.");
                return new Vector3(x, best, z);
            }
        }
        public static Report Apply(GameObject actor, string id, Material necklineMaterial = null)
        {
            Need(!Application.isPlaying && actor && !EditorUtility.IsPersistent(actor), "Garment authoring requires a fresh neutral Editor instance.");
            Need(new[] { "JesusChild", "JesusAdult", "Mary", "Joseph", "Teacher" }.Contains(id), "Exact retained actor identity required.");
            var animator = actor.GetComponent<Animator>(); Need(animator && animator.avatar && animator.avatar.isValid && animator.avatar.isHuman, "Actual Humanoid rig required.");
            var renderers = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Need(!renderers.Any(r => r.name == id + "__HumanNecklineV1"), "Do not apply the garment twice to an existing instance.");
            var body = renderers.Single(r => r.name == id + "__Body"); var source = body.sharedMesh;
            Need(source && source.isReadable && source.subMeshCount == body.sharedMaterials.Length, "Actual readable body/material mapping required.");
            string sourceHash = GospelCharacterRefinement.MeshHash(source); var originalBones = body.bones.ToArray();
            Transform Bone(HumanBodyBones name) { var b = animator.GetBoneTransform(name); Need(b && b.IsChildOf(actor.transform), "Actual garment bone missing: " + name); return b; }
            var bones = new[] { Bone(HumanBodyBones.Chest), Bone(HumanBodyBones.Neck), Bone(HumanBodyBones.LeftUpperArm), Bone(HumanBodyBones.RightUpperArm) };
            Vector3 Point(Transform b) => actor.transform.InverseTransformPoint(b.position);
            Vector3 neck = Point(bones[1]), chest = Point(bones[0]); float half = Mathf.Abs(Point(bones[2]).x - Point(bones[3]).x) * .5f;
            Need(half > .06f && half < .30f && neck.y > chest.y, "Measured neck/chest/shoulder frame required.");
            var sample = new Mesh(); var skin = new List<int>();
            Vector3[] points, normals;
            try
            {
                body.BakeMesh(sample, false); var bakedScale = body.transform.lossyScale;
                Need(Finite(bakedScale.x) && Finite(bakedScale.y) && Finite(bakedScale.z) && bakedScale.x > 0 && bakedScale.y > 0 && bakedScale.z > 0, "BakeMesh scale compensation requires actual finite positive renderer scale.");
                var inverseBakedScale = new Vector3(1 / bakedScale.x, 1 / bakedScale.y, 1 / bakedScale.z);
                Need(Finite(inverseBakedScale.x) && Finite(inverseBakedScale.y) && Finite(inverseBakedScale.z), "Actual renderer scale must have a finite reciprocal.");
                // BakeMesh(false) already carries renderer lossyScale. Remove
                // it once before the full renderer-to-actor transform.
                var map = actor.transform.worldToLocalMatrix * body.transform.localToWorldMatrix * Matrix4x4.Scale(inverseBakedScale);
                points = sample.vertices.Select(v => map.MultiplyPoint3x4(v)).ToArray(); normals = sample.normals.Select(n => map.inverse.transpose.MultiplyVector(n).normalized).ToArray();
                for (int s = 0; s < source.subMeshCount; s++)
                    if (body.sharedMaterials[s] && body.sharedMaterials[s].name == id + "__WarmSkin") skin.AddRange(source.GetTriangles(s));
            }
            finally { Object.DestroyImmediate(sample); }
            Need(skin.Count >= 24 && points.Length == source.vertexCount && normals.Length == points.Length, "Actual drawn anatomical WarmSkin triangles required.");
            var referenced = skin.Distinct().Select(i => points[i]).ToArray(); float scale = half / .18f;
            var neckSlice = referenced.Where(p => Mathf.Abs(p.y - neck.y) < .025f * scale && Mathf.Abs(p.x - neck.x) < half * .45f).ToArray();
            Need(neckSlice.Length >= 4, "Actual neck skin section required.");
            float rx = Mathf.Clamp(neckSlice.Max(p => Mathf.Abs(p.x - neck.x)), .022f * scale, .060f * scale) + .002f;
            float rz = Mathf.Clamp(neckSlice.Max(p => Mathf.Abs(p.z - neck.z)), .025f * scale, .060f * scale) + .002f;
            var chestSlice = referenced.Where(p => p.y >= chest.y && p.y <= neck.y && Mathf.Abs(p.x - neck.x) < half * .7f).ToArray();
            Need(chestSlice.Length > 8 && rx < half * .65f, "Actual upper torso envelope required.");
            float outerZ = Mathf.Clamp(chestSlice.Max(p => Mathf.Abs(p.z - neck.z)) * .8f, rz + .008f, half * .72f);
            var surface = new Surface { points = points, normals = normals, triangles = skin.ToArray(), minY = chest.y - .06f * scale, maxY = neck.y + .006f * scale };
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var weights = new List<BoneWeight>(); var panels = new List<int[]>();
            Vector3 Place(float t, float angle, out Vector3 normal) => surface.Project(neck.x + Mathf.Sin(angle) * Mathf.Lerp(rx, half * .82f, t), neck.z + Mathf.Cos(angle) * Mathf.Lerp(rz, outerZ, t), out normal);
            void AddPanel(int columns, bool wrap, float first, float last, float lift)
            {
                int offset = vertices.Count, count = (Rows + 1) * columns; var faces = new List<int>(); var edge = new Dictionary<(int, int), (int, int, int)>();
                for (int side = 0; side < 2; side++) for (int row = 0; row <= Rows; row++) for (int col = 0; col < columns; col++)
                {
                    float t = row / (float)Rows, a = Mathf.Lerp(first, last, col / (float)(wrap ? columns : columns - 1));
                    Vector3 p = Place(wrap ? t : Mathf.Lerp(.15f, .92f, t), a, out var n); vertices.Add(p + n * (Clearance + lift - side * Thickness)); uv.Add(new Vector2(col / (float)columns, t));
                    float nw = (1 - t) * .24f, aw = Mathf.Pow(Mathf.Abs(Mathf.Sin(a)), 4) * t * .16f;
                    int arm = (Mathf.Sin(a) >= 0) == (Point(bones[2]).x >= neck.x) ? 2 : 3;
                    weights.Add(new BoneWeight { boneIndex0 = 0, weight0 = 1 - nw - aw, boneIndex1 = 1, weight1 = nw, boneIndex2 = arm, weight2 = aw });
                }
                void Triangle(int a, int b, int c)
                {
                    faces.AddRange(new[] { offset + a, offset + b, offset + c, offset + a + count, offset + c + count, offset + b + count });
                    foreach (var e in new[] { (a, b), (b, c), (c, a) }) { var key = e.Item1 < e.Item2 ? e : (e.Item2, e.Item1); edge[key] = edge.TryGetValue(key, out var old) ? (old.Item1, old.Item2, old.Item3 + 1) : (e.Item1, e.Item2, 1); }
                }
                for (int row = 0; row < Rows; row++) for (int col = 0; col < (wrap ? columns : columns - 1); col++)
                { int a = row * columns + col, b = a + columns, c = row * columns + (col + 1) % columns, d = c + columns; Triangle(a, b, c); Triangle(c, b, d); }
                foreach (var e in edge.Values.Where(e => e.Item3 == 1)) { int a = offset + e.Item1, b = offset + e.Item2; faces.AddRange(new[] { b, a, a + count, b, a + count, b + count }); }
                panels.Add(faces.ToArray());
            }
            AddPanel(Segments, true, 0, Mathf.PI * 2, 0);
            if (id != "Mary") AddPanel(2, false, -.045f, .045f, Thickness);
            var mesh = new Mesh { name = id + "__HumanNecklineV1", vertices = vertices.ToArray(), uv = uv.ToArray(), boneWeights = weights.ToArray(), bindposes = bones.Select(b => b.worldToLocalMatrix * actor.transform.localToWorldMatrix).ToArray(), subMeshCount = panels.Count };
            for (int s = 0; s < panels.Count; s++) mesh.SetTriangles(panels[s], s); mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            Need(mesh.uv.Length == vertices.Count && mesh.uv.All(v => Finite(v.x) && Finite(v.y)) && mesh.tangents.Length == vertices.Count && mesh.tangents.All(v => Finite(v.x) && Finite(v.y) && Finite(v.z) && Finite(v.w) && new Vector3(v.x, v.y, v.z).sqrMagnitude > .5f), "Every new vertex requires finite UV0 and nonzero recalculated tangents.");
            Need(weights.All(w => Finite(w.weight0) && Finite(w.weight1) && Finite(w.weight2) && w.weight0 > 0 && w.weight1 >= 0 && w.weight2 >= 0 && Mathf.Abs(w.weight0 + w.weight1 + w.weight2 - 1) < .00001f), "Every new vertex requires finite complete normalized weights.");
            float roundTrip = 0;
            for (int b = 0; b < bones.Length; b++) { var m = actor.transform.worldToLocalMatrix * bones[b].localToWorldMatrix * mesh.bindposes[b]; for (int n = 0; n < 16; n++) roundTrip = Mathf.Max(roundTrip, Mathf.Abs(m[n] - Matrix4x4.identity[n])); }
            Need(roundTrip < .0001f && vertices.All(p => Finite(p.x) && Finite(p.y) && Finite(p.z)), "Actual bind round trip/finite garment positions required.");
            var edges = new Dictionary<(int, int), int>(); foreach (var face in panels) for (int i = 0; i < face.Length; i += 3) foreach (var e in new[] { (face[i], face[i + 1]), (face[i + 1], face[i + 2]), (face[i + 2], face[i]) }) { var key = e.Item1 < e.Item2 ? e : (e.Item2, e.Item1); edges[key] = edges.TryGetValue(key, out int n) ? n + 1 : 1; }
            Need(edges.Values.All(n => n == 2), "The thin yoke/placket must be closed two-manifold components.");
            string folder = Root + "/" + id; Folder(folder); string path = folder + "/" + mesh.name + ".asset";
            var prior = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (prior) { Need(GospelCharacterRefinement.MeshHash(prior) == GospelCharacterRefinement.MeshHash(mesh), "Preserve a different earlier garment version."); Object.DestroyImmediate(mesh); mesh = prior; } else { Need(!AssetDatabase.LoadMainAssetAtPath(path), "Never replace another asset type."); AssetDatabase.CreateAsset(mesh, path); }
            var template = necklineMaterial ? necklineMaterial : (renderers.SelectMany(r => r.sharedMaterials).FirstOrDefault(m => m && m.name == id + "__IllustrationFittedLinen")
                ?? renderers.SelectMany(r => r.sharedMaterials).FirstOrDefault(m => m && m.name == id + "__UndyedLinen"));
            Need(template && template.shader && template.HasProperty("_Color"), "Actual retained linen material required.");
            var linen = PersistMaterial(template, folder, id + "__HumanNecklineLinenV1", 1); var mats = id == "Mary" ? new[] { linen } : new[] { linen, PersistMaterial(template, folder, id + "__HumanPlacketLinenV1", .91f) };
            var go = new GameObject(id + "__HumanNecklineV1"); go.transform.SetParent(actor.transform, false); var renderer = go.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh; renderer.bones = bones; renderer.rootBone = bones[0]; renderer.sharedMaterials = mats; renderer.quality = SkinQuality.Bone4; renderer.updateWhenOffscreen = true; var bounds = mesh.bounds; bounds.Expand(.06f); renderer.localBounds = bounds;
            Need(body.sharedMesh == source && body.bones.SequenceEqual(originalBones) && GospelCharacterRefinement.MeshHash(source) == sourceHash, "Retained body geometry and skin palette must remain unchanged.");
            return new Report { id = id, renderer = go.name, meshAsset = path, meshHash = GospelCharacterRefinement.MeshHash(mesh), materialAsset = AssetDatabase.GetAssetPath(linen), sourceBodyAsset = AssetDatabase.GetAssetPath(source), sourceBodyHash = sourceHash, sourceSkinTriangles = skin.Count / 3, sampledVertices = referenced.Length, vertices = mesh.vertexCount, triangles = panels.Sum(a => a.Length / 3), placketTriangles = panels.Count > 1 ? panels[1].Length / 3 : 0, maximumInfluences = weights.Max(w => (w.weight0 > 0 ? 1 : 0) + (w.weight1 > 0 ? 1 : 0) + (w.weight2 > 0 ? 1 : 0)), indexCount = panels.Sum(a => a.Length), boundaryEdges = edges.Count(e => e.Value == 1), nonManifoldEdges = edges.Count(e => e.Value != 2), neckRadiusX = rx, neckRadiusZ = rz, shoulderHalfWidth = half, thickness = Thickness, clearance = Clearance, maximumBindRoundTripError = roundTrip, actorBounds = mesh.bounds, originalBodyUnchanged = true, originalBonesUnchanged = true, closedTwoManifold = true, finiteUvAndTangents = true, nativeMotionAndVisualReviewCompleted = false };
        }
        static Material PersistMaterial(Material source, string folder, string name, float shade)
        {
            string path = folder + "/" + name + ".mat"; var clone = new Material(source) { name = name }; var shader = Shader.Find("FirstGame/GospelNpr/lilToonOpaque"); if (shader) clone.shader = shader;
            if (clone.HasProperty("_Cull")) clone.SetFloat("_Cull", 2); var color = source.GetColor("_Color"); clone.SetColor("_Color", new Color(color.r * shade, color.g * shade, color.b * shade, color.a));
            var prior = AssetDatabase.LoadAssetAtPath<Material>(path); if (prior) { Need(prior.shader == clone.shader && prior.GetColor("_Color") == clone.GetColor("_Color") && prior.mainTexture == clone.mainTexture, "Preserve a differing earlier neckline material."); Object.DestroyImmediate(clone); return prior; }
            Need(!AssetDatabase.LoadMainAssetAtPath(path), "Never replace another material asset type."); AssetDatabase.CreateAsset(clone, path); return clone;
        }
        static void Folder(string path) { var p = path.Split('/'); string current = p[0]; for (int i = 1; i < p.Length; i++) { string next = current + "/" + p[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, p[i]); current = next; } }
        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        static void Need(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
#endif
