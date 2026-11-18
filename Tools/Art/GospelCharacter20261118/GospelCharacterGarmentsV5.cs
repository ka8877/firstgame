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
            public ProjectionSourceReport[] projectionSources;
            public int sourceClothTriangles;
            public float measuredNeckSkinMaximumY, projectionMinimumY, projectionMaximumY;
            public bool projectionSourceMeshesUnchanged;
            public float innerPlaneY, innerContourMinimumRadius, innerContourMaximumRadius, maximumBridgeSpan, maximumSurfaceLift;
            public int innerContourSegments, fabricBridgeSamples, surfaceLiftSamples;
            public SeamReport[] measuredSeams;
        }
        [Serializable] public sealed class SeamReport
        {
            public float angleRadians, innerRadius;
            public Vector3 inner, innerNormal, innerBarycentric, outer, outerNormal;
            public int innerSkinTriangle, outerProjectionTriangle;
            public string innerSource, outerSource;
        }
        [Serializable] public sealed class ProjectionSourceReport
        {
            public string renderer, material, meshAsset, meshHash;
            public int materialSlot, referencedVertices, triangles;
            public Bounds actualDrawnBounds;
        }
        [Serializable] sealed class SurfaceFailure
        {
            public string id, renderer, meshAsset;
            public string[] materialSlots, actorRendererSlots;
            public Bounds actualDrawnSkinBounds, actualUpperSkinBounds;
            public Vector3 neck, chest, sampledNormal;
            public Vector2 requestedXZ;
            public float minY, maxY, neckRadiusX, neckRadiusZ, shoulderHalfWidth, outerZ, actorScale;
            public float t, angleRadians, lowestIntersectedY, highestIntersectedY, chosenY;
            public int sourceTriangles, sourceReferencedVertices, upperSkinVertices, panel, side, row, angularIndex, columns;
            public int projectedDegenerateTriangles, xzIntersectedTriangles, belowRangeTriangles, aboveRangeTriangles;
            public bool hadIntersectionInXZ, hadChosenY;
            public DrawnClothSurface[] drawnLinenSurfaces;
            public string additionalSurfaceReadError;
            public ProjectionSourceReport[] projectionSources;
            public SurfaceHit[] intersectedSurfaces;
            public string queryKind;
        }
        [Serializable] sealed class SurfaceHit
        {
            public string source;
            public int triangle;
            public float y;
            public Vector3 a, b, c;
            public bool insideMeasuredHeightRange;
        }
        [Serializable] sealed class DrawnClothSurface
        {
            public string renderer, meshAsset;
            public string[] materialSlots;
            public Bounds actualActorBounds;
            public int referencedVertices, triangles;
        }
        sealed class Surface
        {
            public Vector3[] points, normals;
            public int[] triangles;
            public string[] triangleSources;
            public float minY, maxY;
            public SurfaceFailure diagnostic;
            public Action<SurfaceFailure> completeDiagnostic;
            public bool TryProject(float x, float z, out Vector3 point, out Vector3 normal, out SurfaceHit chosen, bool required = false)
            {
                point = Vector3.zero; chosen = null;
                float best = float.NegativeInfinity; normal = Vector3.up;
                int degenerate = 0, intersections = 0, below = 0, above = 0;
                float lowest = float.PositiveInfinity, highest = float.NegativeInfinity;
                var hits = new List<SurfaceHit>();
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int ia = triangles[i], ib = triangles[i + 1], ic = triangles[i + 2]; var a = points[ia]; var b = points[ib]; var c = points[ic];
                    float den = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                    if (Mathf.Abs(den) < 1e-9f) { degenerate++; continue; }
                    float u = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / den;
                    float v = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / den, w = 1 - u - v;
                    if (u < -.0001f || v < -.0001f || w < -.0001f) continue;
                    float y = a.y * u + b.y * v + c.y * w;
                    var hit = new SurfaceHit { source = triangleSources[i / 3], triangle = i / 3, y = y, a = a, b = b, c = c, insideMeasuredHeightRange = y >= minY && y <= maxY }; hits.Add(hit);
                    intersections++; lowest = Mathf.Min(lowest, y); highest = Mathf.Max(highest, y);
                    if (y < minY) below++; else if (y > maxY) above++;
                    if (y < minY || y > maxY || y <= best) continue;
                    best = y; normal = (normals[ia] * u + normals[ib] * v + normals[ic] * w).normalized; chosen = hit;
                }
                if (float.IsInfinity(best) || !(normal.sqrMagnitude > .5f))
                {
                    if (!required) return false;
                    diagnostic.requestedXZ = new Vector2(x, z); diagnostic.minY = minY; diagnostic.maxY = maxY; diagnostic.sampledNormal = normal;
                    diagnostic.projectedDegenerateTriangles = degenerate; diagnostic.xzIntersectedTriangles = intersections;
                    diagnostic.belowRangeTriangles = below; diagnostic.aboveRangeTriangles = above;
                    diagnostic.hadIntersectionInXZ = intersections > 0; diagnostic.lowestIntersectedY = intersections > 0 ? lowest : 0; diagnostic.highestIntersectedY = intersections > 0 ? highest : 0;
                    diagnostic.hadChosenY = !float.IsInfinity(best); diagnostic.chosenY = diagnostic.hadChosenY ? best : 0;
                    diagnostic.intersectedSurfaces = hits.ToArray();
                    try { completeDiagnostic?.Invoke(diagnostic); } catch (Exception e) { diagnostic.additionalSurfaceReadError = e.ToString(); }
                    string data = JsonUtility.ToJson(diagnostic, true);
                    Debug.Log("FIRSTGAME_GARMENT_SURFACE_DIAGNOSTIC " + data);
                    Need(false, "Actual upper-body skin/retained-cloth intersection is missing; do not invent a floating yoke.\n" + data);
                }
                point = new Vector3(x, best, z); return true;
            }
        }
        sealed class CutAnchor
        {
            public Vector3 point, normal, barycentric;
            public int triangle;
        }
        sealed class CutSegment { public CutAnchor a, b; public int ia, ib; }
        sealed class NeckContour
        {
            public CutSegment[] segments;
            public Vector3 center;
            public float planeY, minimumRadius, maximumRadius;
            public CutAnchor Sample(float angle)
            {
                var direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)); float best = float.PositiveInfinity; CutAnchor result = null;
                foreach (var s in segments)
                {
                    var a = new Vector2(s.a.point.x - center.x, s.a.point.z - center.z); var e = new Vector2(s.b.point.x - s.a.point.x, s.b.point.z - s.a.point.z);
                    float den = Cross(direction, e); if (Mathf.Abs(den) < 1e-10f) continue;
                    float radius = Cross(a, e) / den, t = Cross(a, direction) / den;
                    if (radius <= 0 || radius >= best || t < -.00001f || t > 1.00001f) continue;
                    t = Mathf.Clamp01(t); best = radius;
                    result = new CutAnchor { point = Vector3.Lerp(s.a.point, s.b.point, t), normal = Vector3.Lerp(s.a.normal, s.b.normal, t).normalized, barycentric = Vector3.Lerp(s.a.barycentric, s.b.barycentric, t), triangle = s.a.triangle };
                }
                Need(result != null && Finite(best) && result.normal.sqrMagnitude > .5f, "The selected closed neck contour must supply an actual first positive radial seam intersection at angle " + angle);
                return result;
            }
        }
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        static NeckContour MeasureNeckContour(Vector3[] points, Vector3[] normals, int[] skin, Vector3 neck, Vector3[] neckSlice, float half, float scale)
        {
            float weld = .00005f * scale, planeTolerance = .0000001f * Mathf.Max(1, scale);
            var diagnostics = new List<string>();
            foreach (float plane in new[] { neck.y, (neckSlice.Min(p => p.y) + neckSlice.Max(p => p.y)) * .5f }.Distinct())
            {
                var nodes = new List<Vector3>(); var edges = new Dictionary<(int, int), CutSegment>();
                int Node(Vector3 p) { int n = nodes.FindIndex(q => (p - q).sqrMagnitude <= weld * weld); if (n >= 0) return n; nodes.Add(p); return nodes.Count - 1; }
                int coplanar = 0;
                for (int tri = 0; tri < skin.Length; tri += 3)
                {
                    int[] index = { skin[tri], skin[tri + 1], skin[tri + 2] }; var cut = new List<CutAnchor>();
                    void Add(int a, int b, float t)
                    {
                        var bary = Vector3.Lerp(a == 0 ? Vector3.right : a == 1 ? Vector3.up : Vector3.forward, b == 0 ? Vector3.right : b == 1 ? Vector3.up : Vector3.forward, t);
                        var p = Vector3.Lerp(points[index[a]], points[index[b]], t);
                        if (cut.Any(v => (v.point - p).sqrMagnitude <= weld * weld)) return;
                        cut.Add(new CutAnchor { point = p, normal = Vector3.Lerp(normals[index[a]], normals[index[b]], t).normalized, barycentric = bary, triangle = tri / 3 });
                    }
                    for (int e = 0; e < 3; e++)
                    {
                        int next = (e + 1) % 3; float da = points[index[e]].y - plane, db = points[index[next]].y - plane;
                        if (Mathf.Abs(da) <= planeTolerance) Add(e, e, 0);
                        if (da * db < 0) Add(e, next, da / (da - db));
                    }
                    if (cut.Count > 2) { coplanar++; continue; }
                    if (cut.Count != 2) continue;
                    int ia = Node(cut[0].point), ib = Node(cut[1].point); if (ia == ib) continue;
                    var key = ia < ib ? (ia, ib) : (ib, ia);
                    if (!edges.ContainsKey(key)) edges.Add(key, new CutSegment { a = cut[0], b = cut[1], ia = ia, ib = ib });
                }
                var adjacency = nodes.Select(_ => new List<int>()).ToArray(); var all = edges.Values.ToArray();
                for (int e = 0; e < all.Length; e++) { adjacency[all[e].ia].Add(e); adjacency[all[e].ib].Add(e); }
                var seen = new HashSet<int>(); NeckContour selected = null; float smallestArea = float.PositiveInfinity;
                foreach (int start in Enumerable.Range(0, nodes.Count))
                {
                    if (!seen.Add(start)) continue; var component = new List<int>(); var componentEdges = new HashSet<int>(); var queue = new Queue<int>(); queue.Enqueue(start);
                    while (queue.Count > 0) { int n = queue.Dequeue(); component.Add(n); foreach (int e in adjacency[n]) { componentEdges.Add(e); int next = all[e].ia == n ? all[e].ib : all[e].ia; if (seen.Add(next)) queue.Enqueue(next); } }
                    if (component.Count < 3 || component.Any(n => adjacency[n].Count != 2)) continue;
                    var cycle = new List<int>(); int previous = -1, current = start;
                    do { cycle.Add(current); int next = adjacency[current].Select(e => all[e].ia == current ? all[e].ib : all[e].ia).First(n => n != previous); previous = current; current = next; } while (current != start && cycle.Count <= component.Count);
                    if (current != start || cycle.Count != component.Count) continue;
                    bool simple = true;
                    for (int i = 0; i < cycle.Count && simple; i++) for (int j = i + 2; j < cycle.Count; j++)
                    {
                        if (i == 0 && j == cycle.Count - 1) continue;
                        var a = nodes[cycle[i]]; var b = nodes[cycle[(i + 1) % cycle.Count]]; var c = nodes[cycle[j]]; var d = nodes[cycle[(j + 1) % cycle.Count]];
                        var ab = new Vector2(b.x - a.x, b.z - a.z); var cd = new Vector2(d.x - c.x, d.z - c.z); var ac = new Vector2(c.x - a.x, c.z - a.z);
                        float den = Cross(ab, cd); if (Mathf.Abs(den) < 1e-12f) continue;
                        float t = Cross(ac, cd) / den, u = Cross(ac, ab) / den;
                        if (t > 0 && t < 1 && u > 0 && u < 1) { simple = false; break; }
                    }
                    if (!simple) continue;
                    float area = 0, minRadius = float.PositiveInfinity, maxRadius = 0; int crossings = 0;
                    for (int i = 0; i < cycle.Count; i++)
                    {
                        var a = nodes[cycle[i]] - neck; var b = nodes[cycle[(i + 1) % cycle.Count]] - neck; area += a.x * b.z - b.x * a.z;
                        float radius = new Vector2(a.x, a.z).magnitude; minRadius = Mathf.Min(minRadius, radius); maxRadius = Mathf.Max(maxRadius, radius);
                        if ((a.z > 0) != (b.z > 0) && a.x + (b.x - a.x) * -a.z / (b.z - a.z) > 0) crossings++;
                    }
                    area = Mathf.Abs(area) * .5f;
                    if (crossings % 2 != 1 || area <= weld * weld || minRadius <= weld || maxRadius >= half * .65f || area >= smallestArea) continue;
                    smallestArea = area; selected = new NeckContour { center = neck, planeY = plane, minimumRadius = minRadius, maximumRadius = maxRadius, segments = componentEdges.Select(e => all[e]).ToArray() };
                }
                diagnostics.Add("plane=" + plane + " segments=" + all.Length + " nodes=" + nodes.Count + " coplanar=" + coplanar);
                if (selected != null) return selected;
            }
            throw new InvalidOperationException("No closed finite WarmSkin neck cross-section containing the actual neck center within the measured shoulders: " + string.Join("; ", diagnostics));
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
            var neckContour = MeasureNeckContour(points, normals, skin.ToArray(), neck, neckSlice, half, scale);
            var contourPoints = neckContour.segments.SelectMany(s => new[] { s.a.point, s.b.point }).ToArray();
            float rx = contourPoints.Max(p => Mathf.Abs(p.x - neck.x)), rz = contourPoints.Max(p => Mathf.Abs(p.z - neck.z));
            var chestSlice = referenced.Where(p => p.y >= chest.y && p.y <= neck.y && Mathf.Abs(p.x - neck.x) < half * .7f).ToArray();
            Need(chestSlice.Length > 8 && rx < half * .65f, "Actual upper torso envelope required.");
            float outerZ = Mathf.Clamp(chestSlice.Max(p => Mathf.Abs(p.z - neck.z)) * .8f, rz + .008f, half * .72f);
            Bounds BoundsOf(Vector3[] values) { var b = new Bounds(values.Length > 0 ? values[0] : Vector3.zero, Vector3.zero); foreach (var p in values) b.Encapsulate(p); return b; }
            // Neck measurements stay anatomical. Covered shoulders must also
            // sample the actual drawn source-shirt mesh, including its own
            // frame, skinning and V5 clearance, never its unused raw buffers.
            var projectedPoints = points.ToList(); var projectedNormals = normals.ToList(); var projectedTriangles = skin.ToList();
            var triangleSources = Enumerable.Repeat(body.name + "/" + id + "__WarmSkin", skin.Count / 3).ToList();
            var projectionSources = new List<ProjectionSourceReport> { new ProjectionSourceReport { renderer = body.name, material = id + "__WarmSkin", materialSlot = Array.FindIndex(body.sharedMaterials, m => m && m.name == id + "__WarmSkin"), meshAsset = AssetDatabase.GetAssetPath(source), meshHash = sourceHash, referencedVertices = referenced.Length, triangles = skin.Count / 3, actualDrawnBounds = BoundsOf(referenced) } };
            var projectionMeshes = new List<Mesh> { source }; var projectionHashes = new List<string> { sourceHash };
            int clothTriangles = 0; float measuredNeckTop = neckSlice.Max(p => p.y), measuredSurfaceTop = measuredNeckTop;
            foreach (var r in renderers.Where(r => r.enabled && r.gameObject.activeInHierarchy && (r == body || r.name == id + "__IllustrationFittedShirt")))
            {
                var slots = r.sharedMaterials;
                for (int slot = 0; slot < Mathf.Min(slots.Length, r.sharedMesh.subMeshCount); slot++)
                {
                    if (!slots[slot] || (slots[slot].name != id + "__UndyedLinen" && slots[slot].name != id + "__IllustrationFittedLinen")) continue;
                    var drawn = r.sharedMesh.GetTriangles(slot); if (drawn.Length == 0) continue;
                    var baked = new Mesh();
                    try
                    {
                        r.BakeMesh(baked, false); var s = r.transform.lossyScale;
                        Need(Finite(s.x) && Finite(s.y) && Finite(s.z) && s.x > 0 && s.y > 0 && s.z > 0, "Retained source-cloth scale must be finite and positive.");
                        var reciprocal = new Vector3(1 / s.x, 1 / s.y, 1 / s.z); Need(Finite(reciprocal.x) && Finite(reciprocal.y) && Finite(reciprocal.z), "Retained source-cloth reciprocal must be finite.");
                        var map = actor.transform.worldToLocalMatrix * r.transform.localToWorldMatrix * Matrix4x4.Scale(reciprocal);
                        var clothPoints = baked.vertices.Select(p => map.MultiplyPoint3x4(p)).ToArray(); var clothNormals = baked.normals.Select(n => map.inverse.transpose.MultiplyVector(n).normalized).ToArray();
                        Need(clothPoints.Length == r.sharedMesh.vertexCount && clothNormals.Length == clothPoints.Length && clothPoints.All(p => Finite(p.x) && Finite(p.y) && Finite(p.z)), "Actual complete finite source-cloth sample required.");
                        int offset = projectedPoints.Count; projectedPoints.AddRange(clothPoints); projectedNormals.AddRange(clothNormals); projectedTriangles.AddRange(drawn.Select(i => i + offset));
                        triangleSources.AddRange(Enumerable.Repeat(r.name + "/" + slot + ":" + slots[slot].name, drawn.Length / 3));
                        var refs = drawn.Distinct().Select(i => clothPoints[i]).ToArray(); var clothBounds = BoundsOf(refs); string hash = GospelCharacterRefinement.MeshHash(r.sharedMesh);
                        projectionSources.Add(new ProjectionSourceReport { renderer = r.name, material = slots[slot].name, materialSlot = slot, meshAsset = AssetDatabase.GetAssetPath(r.sharedMesh), meshHash = hash, referencedVertices = refs.Length, triangles = drawn.Length / 3, actualDrawnBounds = clothBounds });
                        projectionMeshes.Add(r.sharedMesh); projectionHashes.Add(hash); clothTriangles += drawn.Length / 3; measuredSurfaceTop = Mathf.Max(measuredSurfaceTop, clothBounds.max.y);
                    }
                    finally { Object.DestroyImmediate(baked); }
                }
            }
            Need(id == "Mary" || clothTriangles > 0, "The actual retained fitted source-shirt draw surface is required.");
            var surface = new Surface { points = projectedPoints.ToArray(), normals = projectedNormals.ToArray(), triangles = projectedTriangles.ToArray(), triangleSources = triangleSources.ToArray(), minY = chest.y - .06f * scale, maxY = measuredSurfaceTop };
            var upper = referenced.Where(p => p.y >= surface.minY && p.y <= surface.maxY).ToArray();
            surface.diagnostic = new SurfaceFailure { id = id, renderer = body.name, meshAsset = AssetDatabase.GetAssetPath(source),
                materialSlots = body.sharedMaterials.Select((m, i) => i + ":" + (m ? m.name : "<null>")).ToArray(),
                actorRendererSlots = renderers.Select(r => r.name + ":" + string.Join(",", r.sharedMaterials.Select((m, i) => i + "=" + (m ? m.name : "<null>")))).ToArray(),
                actualDrawnSkinBounds = BoundsOf(referenced), actualUpperSkinBounds = BoundsOf(upper), neck = neck, chest = chest,
                neckRadiusX = rx, neckRadiusZ = rz, shoulderHalfWidth = half, outerZ = outerZ, actorScale = scale,
                sourceTriangles = skin.Count / 3, sourceReferencedVertices = referenced.Length, upperSkinVertices = upper.Length, projectionSources = projectionSources.ToArray() };
            surface.completeDiagnostic = diagnostic =>
            {
                var cloth = new List<DrawnClothSurface>();
                foreach (var r in renderers.Where(r => r.enabled && r.sharedMesh))
                {
                    var slots = r.sharedMaterials; var drawn = new List<int>();
                    for (int s = 0; s < Mathf.Min(slots.Length, r.sharedMesh.subMeshCount); s++)
                        if (slots[s] && slots[s].name.IndexOf("Linen", StringComparison.OrdinalIgnoreCase) >= 0) drawn.AddRange(r.sharedMesh.GetTriangles(s));
                    if (drawn.Count == 0) continue;
                    var baked = new Mesh();
                    try
                    {
                        r.BakeMesh(baked, false); var s = r.transform.lossyScale;
                        Need(Finite(s.x) && Finite(s.y) && Finite(s.z) && s.x > 0 && s.y > 0 && s.z > 0, "Diagnostic cloth scale must be finite and positive.");
                        var map = actor.transform.worldToLocalMatrix * r.transform.localToWorldMatrix * Matrix4x4.Scale(new Vector3(1 / s.x, 1 / s.y, 1 / s.z));
                        var local = baked.vertices; var refs = drawn.Distinct().ToArray();
                        cloth.Add(new DrawnClothSurface { renderer = r.name, meshAsset = AssetDatabase.GetAssetPath(r.sharedMesh),
                            materialSlots = slots.Select((m, i) => i + ":" + (m ? m.name : "<null>")).ToArray(),
                            actualActorBounds = BoundsOf(refs.Select(i => map.MultiplyPoint3x4(local[i])).ToArray()), referencedVertices = refs.Length, triangles = drawn.Count / 3 });
                    }
                    finally { Object.DestroyImmediate(baked); }
                }
                diagnostic.drawnLinenSurfaces = cloth.ToArray();
            };
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var weights = new List<BoneWeight>(); var panels = new List<int[]>();
            var measuredSeams = new List<SeamReport>();
            var seamCache = new Dictionary<float, (CutAnchor inner, Vector3 outer, Vector3 normal, SurfaceHit hit)>();
            var fabricCache = new Dictionary<(float, float), (Vector3 point, Vector3 normal)>();
            int bridgeSamples = 0, liftSamples = 0; float maxBridge = 0, maxLift = 0;
            Vector3 Place(float t, float angle, out Vector3 normal)
            {
                if (fabricCache.TryGetValue((t, angle), out var cached)) { normal = cached.normal; return cached.point; }
                if (!seamCache.TryGetValue(angle, out var seam))
                {
                    var inner = neckContour.Sample(angle);
                    float x = neck.x + Mathf.Sin(angle) * half * .82f, z = neck.z + Mathf.Cos(angle) * outerZ;
                    Need(new Vector2(x - neck.x, z - neck.z).magnitude > new Vector2(inner.point.x - neck.x, inner.point.z - neck.z).magnitude, "The measured outer seam must lie outside the actual neck section.");
                    surface.diagnostic.queryKind = "required outer seam";
                    surface.TryProject(x, z, out var outer, out var outerNormal, out var outerHit, true);
                    seam = (inner, outer, outerNormal, outerHit); seamCache.Add(angle, seam);
                    measuredSeams.Add(new SeamReport { angleRadians = angle, innerRadius = new Vector2(inner.point.x - neck.x, inner.point.z - neck.z).magnitude,
                        inner = inner.point, innerNormal = inner.normal, innerBarycentric = inner.barycentric, innerSkinTriangle = inner.triangle, innerSource = body.name + "/" + id + "__WarmSkin",
                        outer = outer, outerNormal = outerNormal, outerProjectionTriangle = outerHit.triangle, outerSource = outerHit.source });
                }
                Vector3 point;
                if (t <= 0) { point = seam.inner.point; normal = seam.inner.normal; }
                else if (t >= 1) { point = seam.outer; normal = seam.normal; }
                else
                {
                    point = Vector3.Lerp(seam.inner.point, seam.outer, t); normal = Vector3.Lerp(seam.inner.normal, seam.normal, t).normalized;
                    surface.diagnostic.queryKind = "optional surface above source-anchored fabric chord";
                    if (surface.TryProject(point.x, point.z, out var covering, out var coveringNormal, out _) && covering.y > point.y)
                    { maxLift = Mathf.Max(maxLift, covering.y - point.y); liftSamples++; point = covering; normal = coveringNormal; }
                    else
                    {
                        // This is cloth intentionally spanning its measured
                        // neck and shirt anchors, not an invented skin hit.
                        bridgeSamples++; maxBridge = Mathf.Max(maxBridge, Vector3.Distance(seam.inner.point, seam.outer));
                    }
                }
                Need(Finite(point.x) && Finite(point.y) && Finite(point.z) && normal.sqrMagnitude > .5f, "Every fabric point requires finite measured anchors and a complete normal.");
                fabricCache.Add((t, angle), (point, normal)); return point;
            }
            void AddPanel(int columns, bool wrap, float first, float last, float lift)
            {
                int offset = vertices.Count, count = (Rows + 1) * columns; var faces = new List<int>(); var edge = new Dictionary<(int, int), (int, int, int)>();
                for (int side = 0; side < 2; side++) for (int row = 0; row <= Rows; row++) for (int col = 0; col < columns; col++)
                {
                    float t = row / (float)Rows, a = Mathf.Lerp(first, last, col / (float)(wrap ? columns : columns - 1));
                    surface.diagnostic.panel = panels.Count; surface.diagnostic.side = side; surface.diagnostic.row = row;
                    surface.diagnostic.angularIndex = col; surface.diagnostic.columns = columns; surface.diagnostic.t = wrap ? t : Mathf.Lerp(.15f, .92f, t); surface.diagnostic.angleRadians = a;
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
            Need(panels.All(face => Enumerable.Range(0, face.Length / 3).All(i => Vector3.Cross(vertices[face[i * 3 + 1]] - vertices[face[i * 3]], vertices[face[i * 3 + 2]] - vertices[face[i * 3]]).sqrMagnitude > 1e-16f)), "Source-anchored fabric triangles must have finite nonzero area.");
            string folder = Root + "/" + id; Folder(folder); string path = folder + "/" + mesh.name + ".asset";
            var prior = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (prior) { Need(GospelCharacterRefinement.MeshHash(prior) == GospelCharacterRefinement.MeshHash(mesh), "Preserve a different earlier garment version."); Object.DestroyImmediate(mesh); mesh = prior; } else { Need(!AssetDatabase.LoadMainAssetAtPath(path), "Never replace another asset type."); AssetDatabase.CreateAsset(mesh, path); }
            var template = necklineMaterial ? necklineMaterial : (renderers.SelectMany(r => r.sharedMaterials).FirstOrDefault(m => m && m.name == id + "__IllustrationFittedLinen")
                ?? renderers.SelectMany(r => r.sharedMaterials).FirstOrDefault(m => m && m.name == id + "__UndyedLinen"));
            Need(template && template.shader && template.HasProperty("_Color"), "Actual retained linen material required.");
            var linen = PersistMaterial(template, folder, id + "__HumanNecklineLinenV1", 1); var mats = id == "Mary" ? new[] { linen } : new[] { linen, PersistMaterial(template, folder, id + "__HumanPlacketLinenV1", .91f) };
            var go = new GameObject(id + "__HumanNecklineV1"); go.transform.SetParent(actor.transform, false); var renderer = go.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh; renderer.bones = bones; renderer.rootBone = bones[0]; renderer.sharedMaterials = mats; renderer.quality = SkinQuality.Bone4; renderer.updateWhenOffscreen = true; var bounds = mesh.bounds; bounds.Expand(.06f); renderer.localBounds = bounds;
            Need(body.sharedMesh == source && body.bones.SequenceEqual(originalBones) && GospelCharacterRefinement.MeshHash(source) == sourceHash, "Retained body geometry and skin palette must remain unchanged.");
            Need(projectionMeshes.Select((m, i) => GospelCharacterRefinement.MeshHash(m) == projectionHashes[i]).All(unchanged => unchanged), "Every sampled anatomical/retained-cloth source must remain unchanged.");
            return new Report { id = id, renderer = go.name, meshAsset = path, meshHash = GospelCharacterRefinement.MeshHash(mesh), materialAsset = AssetDatabase.GetAssetPath(linen), sourceBodyAsset = AssetDatabase.GetAssetPath(source), sourceBodyHash = sourceHash, sourceSkinTriangles = skin.Count / 3, sampledVertices = referenced.Length, vertices = mesh.vertexCount, triangles = panels.Sum(a => a.Length / 3), placketTriangles = panels.Count > 1 ? panels[1].Length / 3 : 0, maximumInfluences = weights.Max(w => (w.weight0 > 0 ? 1 : 0) + (w.weight1 > 0 ? 1 : 0) + (w.weight2 > 0 ? 1 : 0)), indexCount = panels.Sum(a => a.Length), boundaryEdges = edges.Count(e => e.Value == 1), nonManifoldEdges = edges.Count(e => e.Value != 2), neckRadiusX = rx, neckRadiusZ = rz, shoulderHalfWidth = half, thickness = Thickness, clearance = Clearance, maximumBindRoundTripError = roundTrip, actorBounds = mesh.bounds, originalBodyUnchanged = true, originalBonesUnchanged = true, closedTwoManifold = true, finiteUvAndTangents = true, nativeMotionAndVisualReviewCompleted = false, projectionSources = projectionSources.ToArray(), sourceClothTriangles = clothTriangles, measuredNeckSkinMaximumY = measuredNeckTop, projectionMinimumY = surface.minY, projectionMaximumY = surface.maxY, projectionSourceMeshesUnchanged = true, innerPlaneY = neckContour.planeY, innerContourMinimumRadius = neckContour.minimumRadius, innerContourMaximumRadius = neckContour.maximumRadius, innerContourSegments = neckContour.segments.Length, maximumBridgeSpan = maxBridge, maximumSurfaceLift = maxLift, fabricBridgeSamples = bridgeSamples, surfaceLiftSamples = liftSamples, measuredSeams = measuredSeams.ToArray() };
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
