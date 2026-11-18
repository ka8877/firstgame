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
        const string Root = "Assets/Art/GospelCharacter20261118/GarmentsV1/v2";
        const int Segments = 48, Rows = 6, BridgeRows = 4;
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
            public PlaneProvenance chosenPlaneSource;
            public int neckPlaneCandidateCount, neckPlanesEvaluated;
            public float maximumAdjacentOuterDistance;
            public float actualNeckChestSpan, maximumOuterDistanceRatio;
            public int sourcePaletteBones, maximumNativeInfluences, nativeInfluenceCount;
            public float maximumNativeWeightReadbackError, maximumNeutralReconstructionError, minimumOverlapLength, maximumOverlapLength;
            public string sourceRendererFrame, rendererQuality, globalSkinWeights, anchorsAsset;
            public bool fullSourcePaletteAndBindposesRetained;
        }
        [Serializable] public sealed class PlaneProvenance
        {
            public string kind, lowerBoundaryKind, upperBoundaryKind;
            public float y, lowerSourceY, upperSourceY;
            public int[] lowerSourceVertexIds, upperSourceVertexIds;
        }
        [Serializable] public sealed class SeamReport
        {
            public float angleRadians, innerRadius;
            public Vector3 inner, innerNormal, innerBarycentric, outer, outerNormal;
            public int innerSkinTriangle, outerProjectionTriangle;
            public string innerSource, outerSource;
            public Vector3 outerBarycentric;
            public Vector3Int outerSourceVertexIds;
            public int outerSourceTriangle;
            public float outerRadius, outerDistance, outerPlaneResidual, nextOuterDistance;
            public string outerMethod;
            public bool mainRing;
            public float outerMaximumSpan, outerDistanceRatio;
            public Vector3 overlap, overlapBarycentric;
            public string overlapSource;
            public int overlapProjectionTriangle;
            public float overlapLength;
        }
        [Serializable] public sealed class SourceAnchor
        {
            public string meshAsset, source;
            public int triangle;
            public Vector3Int vertices;
            public Vector3 barycentric;
        }
        [Serializable] public sealed class VertexAttachment
        {
            public int vertex, panel, side, row, column;
            public float t, angleRadians, sourceNormalOffset;
            public string region;
            public Vector3 neutralActorPoint, bindPoint;
            public SourceAnchor[] sources;
            public float[] sourceBlend;
        }
        [Serializable] public sealed class AttachmentRecord
        {
            public string id, renderer, meshAsset, sourceBodyAsset, sourceBodyHash;
            public int segments, rows, bridgeRows, paletteBones;
            public string[] palette;
            public VertexAttachment[] vertices;
        }
        sealed class FabricSample
        {
            public Vector3 point, normal;
            public BoneWeight1[] weights;
            public SourceAnchor[] sources;
            public float[] blend;
            public string region;
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
            public ClothSeamDiagnostic clothSeam;
        }
        [Serializable] sealed class SurfaceHit
        {
            public string source;
            public int triangle;
            public float y;
            public Vector3 a, b, c;
            public bool insideMeasuredHeightRange;
            public Vector3 barycentric;
            public Vector3Int sourceVertexIds;
            public int sourceTriangle;
            public float radius, distance, planeResidual;
            public string method;
            public string meshAsset;
        }
        [Serializable] sealed class ClothSeamDiagnostic
        {
            public float angleRadians, innerRadius, minimumSafeRadius, maximumSafeRadius, minY, maxY;
            public Vector3 innerAnchor, radialDirection, planeNormal;
            public int clothTriangles, planeCutTriangles, coplanarTriangles;
            public bool cutsTruncated;
            public ClothCutDiagnostic[] cuts;
            public float maximumSeamDistance, maximumSquaredDistance;
        }
        [Serializable] sealed class ClothCutDiagnostic
        {
            public string source, method, rejection;
            public int projectionTriangle, sourceTriangle;
            public Vector3[] actualCutPoints, barycentricCutPoints;
            public float[] radii;
            public float candidateDistance, candidateDistanceRatio;
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
            public bool[] triangleIsCloth;
            public int[] triangleSourceIndices, triangleVertexOffsets;
            public string[] triangleMeshAssets;
            public int[] triangleComponents;
            public BoneWeight1[][] pointWeights;
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
                    var hit = new SurfaceHit { source = triangleSources[i / 3], triangle = i / 3, y = y, a = a, b = b, c = c, insideMeasuredHeightRange = y >= minY && y <= maxY,
                        barycentric = new Vector3(u, v, w), meshAsset = triangleMeshAssets[i / 3], sourceTriangle = triangleSourceIndices[i / 3], sourceVertexIds = new Vector3Int(ia - triangleVertexOffsets[i / 3], ib - triangleVertexOffsets[i / 3], ic - triangleVertexOffsets[i / 3]) }; hits.Add(hit);
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
            public Vector3 ClosestClothSeam(Vector3 center, CutAnchor inner, float angle, float maximumSeamDistance, float scale, out Vector3 normal, out SurfaceHit chosen,
                int requiredComponent = -1, string requiredSource = null, float? radialMinimum = null, float? radialMaximum = null, Vector3? nearestTarget = null)
            {
                var radial = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)); var planeNormal = new Vector3(Mathf.Cos(angle), 0, -Mathf.Sin(angle));
                float innerRadius = Vector3.Dot(inner.point - center, radial), minimum = radialMinimum ?? innerRadius + Clearance, maximum = radialMaximum ?? innerRadius + maximumSeamDistance, maximumSquaredDistance = maximumSeamDistance * maximumSeamDistance;
                var target = nearestTarget ?? inner.point;
                Need(Finite(maximumSeamDistance) && maximumSeamDistance > 0 && Finite(maximumSquaredDistance) && Finite(maximum) && minimum <= maximum, "The actual actor-local neck/chest span must define a finite physical cloth-seam interval.");
                float tolerance = .0000001f * Mathf.Max(1, scale), best = float.PositiveInfinity; Vector3 bestPoint = Vector3.zero, bestNormal = Vector3.up; SurfaceHit bestHit = null;
                var cutDiagnostics = new List<ClothCutDiagnostic>(); int clothCount = 0, cutCount = 0, coplanarCount = 0;
                CutAnchor Blend(CutAnchor a, CutAnchor b, float t) => new CutAnchor { point = Vector3.Lerp(a.point, b.point, t), barycentric = Vector3.Lerp(a.barycentric, b.barycentric, t) };
                bool ClipRange(float a, float b, float lower, float upper, ref float lo, ref float hi)
                {
                    float delta = b - a; if (delta == 0) return a >= lower && a <= upper;
                    float first = (lower - a) / delta, last = (upper - a) / delta; if (first > last) { float swap = first; first = last; last = swap; }
                    lo = Mathf.Max(lo, first); hi = Mathf.Min(hi, last); return lo <= hi;
                }
                List<CutAnchor> ClipPolygon(List<CutAnchor> input, Func<Vector3, float> signedDistance)
                {
                    var output = new List<CutAnchor>(); if (input.Count == 0) return output;
                    var previous = input[input.Count - 1]; float before = signedDistance(previous.point);
                    foreach (var current in input)
                    {
                        float after = signedDistance(current.point);
                        if ((before >= 0) != (after >= 0)) output.Add(Blend(previous, current, before / (before - after)));
                        if (after >= 0) output.Add(current);
                        previous = current; before = after;
                    }
                    return output;
                }
                CutAnchor ClosestSegment(CutAnchor a, CutAnchor b)
                { var edge = b.point - a.point; float t = edge.sqrMagnitude > 0 ? Mathf.Clamp01(Vector3.Dot(target - a.point, edge) / edge.sqrMagnitude) : 0; return Blend(a, b, t); }
                CutAnchor ClosestTriangle(CutAnchor a, CutAnchor b, CutAnchor c)
                {
                    var ab = b.point - a.point; var ac = c.point - a.point; var ap = target - a.point;
                    float aa = Vector3.Dot(ab, ab), bb = Vector3.Dot(ac, ac), cross = Vector3.Dot(ab, ac), den = aa * bb - cross * cross;
                    if (den > 0)
                    {
                        float u = (Vector3.Dot(ap, ab) * bb - Vector3.Dot(ap, ac) * cross) / den;
                        float v = (Vector3.Dot(ap, ac) * aa - Vector3.Dot(ap, ab) * cross) / den;
                        if (u >= 0 && v >= 0 && u + v <= 1) return new CutAnchor { point = a.point + ab * u + ac * v, barycentric = a.barycentric * (1 - u - v) + b.barycentric * u + c.barycentric * v };
                    }
                    return new[] { ClosestSegment(a, b), ClosestSegment(b, c), ClosestSegment(c, a) }.OrderBy(p => (p.point - target).sqrMagnitude).First();
                }
                for (int tri = 0; tri < triangles.Length / 3; tri++)
                {
                    if (!triangleIsCloth[tri]) continue; clothCount++;
                    if (requiredComponent >= 0 && triangleComponents[tri] != requiredComponent || requiredSource != null && triangleSources[tri] != requiredSource) continue;
                    int ia = triangles[tri * 3], ib = triangles[tri * 3 + 1], ic = triangles[tri * 3 + 2];
                    var vertices = new[] { new CutAnchor { point = points[ia], barycentric = Vector3.right }, new CutAnchor { point = points[ib], barycentric = Vector3.up }, new CutAnchor { point = points[ic], barycentric = Vector3.forward } };
                    var distances = vertices.Select(p => Vector3.Dot(planeNormal, p.point - center)).ToArray(); bool coplanar = distances.All(d => Mathf.Abs(d) <= tolerance);
                    var cuts = new List<CutAnchor>();
                    void Add(CutAnchor p) { if (!cuts.Any(q => (q.point - p.point).sqrMagnitude <= tolerance * tolerance)) cuts.Add(p); }
                    if (coplanar) { cuts.AddRange(vertices); coplanarCount++; }
                    else
                    {
                        for (int edge = 0; edge < 3; edge++)
                        { int next = (edge + 1) % 3; if (distances[edge] * distances[next] < 0) Add(Blend(vertices[edge], vertices[next], distances[edge] / (distances[edge] - distances[next]))); }
                        if (cuts.Count < 2) for (int edge = 0; edge < 3; edge++) if (Mathf.Abs(distances[edge]) <= tolerance) Add(vertices[edge]);
                    }
                    if (cuts.Count == 0) continue; cutCount++;
                    var detail = new ClothCutDiagnostic { source = triangleSources[tri], projectionTriangle = tri, sourceTriangle = triangleSourceIndices[tri], method = coplanar ? "actual-coplanar-triangle-polygon" : "actual-radial-plane-segment",
                        actualCutPoints = cuts.Select(p => p.point).ToArray(), barycentricCutPoints = cuts.Select(p => p.barycentric).ToArray(), radii = cuts.Select(p => Vector3.Dot(p.point - center, radial)).ToArray() };
                    if (cutDiagnostics.Count < 512) cutDiagnostics.Add(detail);
                    CutAnchor candidate = null;
                    if (coplanar)
                    {
                        var polygon = ClipPolygon(cuts, p => p.y - minY); polygon = ClipPolygon(polygon, p => maxY - p.y);
                        polygon = ClipPolygon(polygon, p => Vector3.Dot(p - center, radial) - minimum); polygon = ClipPolygon(polygon, p => maximum - Vector3.Dot(p - center, radial));
                        if (polygon.Count == 1) candidate = polygon[0];
                        else if (polygon.Count == 2) candidate = ClosestSegment(polygon[0], polygon[1]);
                        else if (polygon.Count > 2) candidate = Enumerable.Range(1, polygon.Count - 2).Select(i => ClosestTriangle(polygon[0], polygon[i], polygon[i + 1])).OrderBy(p => (p.point - target).sqrMagnitude).First();
                    }
                    else if (cuts.Count == 1)
                    { float r = Vector3.Dot(cuts[0].point - center, radial); if (cuts[0].point.y >= minY && cuts[0].point.y <= maxY && r >= minimum && r <= maximum) candidate = cuts[0]; }
                    else if (cuts.Count == 2)
                    {
                        float lo = 0, hi = 1;
                        if (ClipRange(cuts[0].point.y, cuts[1].point.y, minY, maxY, ref lo, ref hi) && ClipRange(Vector3.Dot(cuts[0].point - center, radial), Vector3.Dot(cuts[1].point - center, radial), minimum, maximum, ref lo, ref hi))
                            candidate = ClosestSegment(Blend(cuts[0], cuts[1], lo), Blend(cuts[0], cuts[1], hi));
                    }
                    if (candidate == null) { detail.rejection = "no-actual-triangle-point-inside-original-Y-and-physical-radial-bands"; continue; }
                    if (candidate.point.y < minY || candidate.point.y > maxY) { detail.rejection = "candidate-rounding-outside-original-height-band"; continue; }
                    float radius = Vector3.Dot(candidate.point - center, radial), distance = (candidate.point - target).sqrMagnitude, anatomicalDistance = (candidate.point - inner.point).sqrMagnitude;
                    detail.candidateDistance = Mathf.Sqrt(anatomicalDistance); detail.candidateDistanceRatio = detail.candidateDistance / maximumSeamDistance;
                    if (anatomicalDistance > maximumSquaredDistance) { detail.rejection = "actual-cloth-candidate-exceeds-measured-neck-chest-span"; continue; }
                    var n = (normals[ia] * candidate.barycentric.x + normals[ib] * candidate.barycentric.y + normals[ic] * candidate.barycentric.z).normalized;
                    Need(Finite(radius) && Finite(distance) && radius > innerRadius + Thickness && radius <= innerRadius + maximumSeamDistance && n.sqrMagnitude > .5f, "Every cloth seam anchor must be a finite actual normal/point inside the physical gap and anatomical-span-derived radial bound.");
                    if (distance >= best) { detail.rejection = "farther-than-current-actual-cloth-candidate"; continue; }
                    best = distance; bestPoint = candidate.point; bestNormal = n; detail.rejection = "closest-actual-cloth-candidate";
                    bestHit = new SurfaceHit { source = triangleSources[tri], meshAsset = triangleMeshAssets[tri], triangle = tri, sourceTriangle = triangleSourceIndices[tri], sourceVertexIds = new Vector3Int(ia - triangleVertexOffsets[tri], ib - triangleVertexOffsets[tri], ic - triangleVertexOffsets[tri]),
                        a = points[ia], b = points[ib], c = points[ic], y = candidate.point.y, barycentric = candidate.barycentric, radius = radius, distance = Mathf.Sqrt(anatomicalDistance), planeResidual = Mathf.Abs(Vector3.Dot(planeNormal, candidate.point - center)), method = detail.method, insideMeasuredHeightRange = candidate.point.y >= minY && candidate.point.y <= maxY };
                }
                if (bestHit == null)
                {
                    diagnostic.queryKind = "required actual-Linen radial-plane closest seam";
                    diagnostic.clothSeam = new ClothSeamDiagnostic { angleRadians = angle, innerAnchor = inner.point, radialDirection = radial, planeNormal = planeNormal,
                        innerRadius = innerRadius, minimumSafeRadius = minimum, maximumSafeRadius = maximum, maximumSeamDistance = maximumSeamDistance, maximumSquaredDistance = maximumSquaredDistance, minY = minY, maxY = maxY, clothTriangles = clothCount, planeCutTriangles = cutCount, coplanarTriangles = coplanarCount, cutsTruncated = cutCount > 512, cuts = cutDiagnostics.ToArray() };
                    string data = JsonUtility.ToJson(diagnostic, true); Debug.Log("FIRSTGAME_CLOTH_OUTER_SEAM_DIAGNOSTIC " + data);
                    Need(false, "No actual retained Linen point exists inside the measured radial-plane/Y/r bands.\n" + data);
                }
                normal = bestNormal; chosen = bestHit; return bestPoint;
            }
            public void BuildClothComponents(float weld)
            {
                int count = triangles.Length / 3; var roots = Enumerable.Range(0, count).ToArray();
                int Find(int n) { while (roots[n] != n) { roots[n] = roots[roots[n]]; n = roots[n]; } return n; }
                var nodePoints = new List<Vector3>(); var pools = new Dictionary<string, List<int>>(); var vertexNodes = new Dictionary<(string, int), int>();
                int Node(string source, int vertex)
                {
                    if (vertexNodes.TryGetValue((source, vertex), out int known)) return known;
                    if (!pools.TryGetValue(source, out var pool)) { pool = new List<int>(); pools.Add(source, pool); }
                    int at = pool.FindIndex(i => (nodePoints[i] - points[vertex]).sqrMagnitude <= weld * weld), n = at >= 0 ? pool[at] : -1;
                    if (n < 0) { n = nodePoints.Count; nodePoints.Add(points[vertex]); pool.Add(n); }
                    vertexNodes.Add((source, vertex), n); return n;
                }
                var edges = new Dictionary<(string, int, int), int>();
                for (int tri = 0; tri < count; tri++) if (triangleIsCloth[tri]) for (int e = 0; e < 3; e++)
                {
                    int a = Node(triangleSources[tri], triangles[tri * 3 + e]), b = Node(triangleSources[tri], triangles[tri * 3 + (e + 1) % 3]);
                    var key = (triangleSources[tri], Mathf.Min(a, b), Mathf.Max(a, b));
                    if (edges.TryGetValue(key, out int prior)) roots[Find(tri)] = Find(prior); else edges.Add(key, tri);
                }
                triangleComponents = Enumerable.Range(0, count).Select(i => triangleIsCloth[i] ? Find(i) : -1).ToArray();
            }
            public BoneWeight1[] WeightsAt(SurfaceHit hit)
            { int at = hit.triangle * 3; return MixNative(new[] { pointWeights[triangles[at]], pointWeights[triangles[at + 1]], pointWeights[triangles[at + 2]] }, new[] { hit.barycentric.x, hit.barycentric.y, hit.barycentric.z }); }
        }
        static BoneWeight1[] MixNative(BoneWeight1[][] sets, float[] blend)
        {
            var sums = new Dictionary<int, float>();
            for (int i = 0; i < sets.Length; i++) foreach (var w in sets[i]) if (w.weight * blend[i] > 0)
                sums[w.boneIndex] = sums.TryGetValue(w.boneIndex, out float prior) ? prior + w.weight * blend[i] : w.weight * blend[i];
            float total = sums.Values.Sum(); Need(Finite(total) && total > .000001f && sums.Count <= 255, "Complete finite native source influence set required.");
            return sums.OrderByDescending(p => p.Value).Select(p => new BoneWeight1 { boneIndex = p.Key, weight = p.Value / total }).ToArray();
        }
        static Matrix4x4 WeightedSkin(Matrix4x4[] palette, BoneWeight1[] weights)
        {
            var m = Matrix4x4.zero; foreach (var w in weights) for (int i = 0; i < 16; i++) m[i] += palette[w.boneIndex][i] * w.weight;
            Need(Mathf.Abs(m.determinant) > .00001f && Enumerable.Range(0, 16).All(i => Finite(m[i])), "Actual native weighted skin map must remain finite and invertible."); return m;
        }
        sealed class CutAnchor
        {
            public Vector3 point, normal, barycentric;
            public int triangle;
        }
        sealed class CutSegment { public CutAnchor a, b; public int ia, ib; }
        [Serializable] sealed class ContourDiagnostic
        {
            public string actorId, sourceAsset, sourceHash;
            public Vector3 actualNeck;
            public Bounds sourceFullVertexBounds, sourceDrawnSkinBounds;
            public float shoulderHalfWidth, maximumAllowedRadius, actorScale, weldDistance, planeTolerance, sliceMinimumY, sliceMaximumY;
            public int sourceVertices, sourceSkinTriangles;
            public PlaneDiagnostic[] planes;
            public RetainedRendererDiagnostic[] retainedRenderers;
            public PlaneProvenance[] candidates;
            public int candidateCount, originalCandidateCount, noRepresentableMidpointIntervals;
            public bool candidateLimitExceeded, candidateDiagnosticListTruncated;
        }
        [Serializable] sealed class PlaneDiagnostic
        {
            public float planeY;
            public int nodes, edges, coplanarTriangles, recordedSegments, rawSegments, collapsedSegments, duplicateUnorderedEdges, emptyCuts, singleCuts, onPlaneTriangleVertices;
            public bool segmentsTruncated;
            public ComponentDiagnostic[] components;
            public SegmentDiagnostic[] cutSegments;
            public PlaneProvenance source;
            public int candidateIndex;
        }
        [Serializable] sealed class ComponentDiagnostic
        {
            public int index, nodes, edges, cycleNodes;
            public int[] degreeValues, degreeCounts;
            public NodeDiagnostic[] nodeDegrees;
            public bool nodeListTruncated, nodeCountPass, degreeTwoPass, cycleCloses, cycleIncludesAllNodes, properCrossingPass;
            public bool areaMeasured, containmentMeasured, finiteAreaPass, neckCenterContained, minimumRadiusPass, shoulderRadiusPass, smallerAreaPass, selectedCandidate;
            public Bounds actualNodeBounds;
            public Vector3 centroid, minimum, maximum;
            public float area, minimumNodeRadius, maximumNodeRadius, maximumAllowedRadius;
            public string rejectionReason;
        }
        [Serializable] sealed class NodeDiagnostic { public int node, degree; public Vector3 actorPoint; }
        [Serializable] sealed class SegmentDiagnostic
        {
            public int nodeA, nodeB, skinTriangle;
            public Vector3 a, b, normalA, normalB, barycentricA, barycentricB, weldedA, weldedB;
            public bool collapsed, duplicateUnorderedEdge;
        }
        [Serializable] sealed class RetainedRendererDiagnostic
        {
            public string renderer, meshAsset, meshHash, error;
            public string[] materials, materialAssets, shaders;
            public int vertices, triangles, uv0Count, tangentCount, normalCount;
            public bool activeInHierarchy, enabled, readable;
        }
        sealed class NeckContour
        {
            public CutSegment[] segments;
            public Vector3 center;
            public float planeY, minimumRadius, maximumRadius;
            public PlaneProvenance source;
            public int candidateCount, planesEvaluated;
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
        static NeckContour MeasureNeckContour(Vector3[] points, Vector3[] normals, int[] skin, Vector3 neck, Vector3[] neckSlice, int[] neckSliceVertexIds, float half, float scale,
            string actorId, string sourceAsset, string sourceHash, SkinnedMeshRenderer[] retainedRenderers)
        {
            float weld = .00005f * scale, planeTolerance = .0000001f * Mathf.Max(1, scale);
            var diagnostics = new List<string>();
            var planeDiagnostics = new List<PlaneDiagnostic>();
            var heights = neckSliceVertexIds.GroupBy(i => points[i].y).OrderBy(g => g.Key).ToArray();
            int[] AtHeight(float y) => heights.Where(g => g.Key.Equals(y)).SelectMany(g => g).ToArray();
            float sliceMinimum = neckSlice.Min(p => p.y), sliceMaximum = neckSlice.Max(p => p.y);
            PlaneProvenance Candidate(float y, string kind, float lower, float upper) => new PlaneProvenance { y = y, kind = kind, lowerSourceY = lower, upperSourceY = upper,
                lowerSourceVertexIds = AtHeight(lower), upperSourceVertexIds = AtHeight(upper),
                lowerBoundaryKind = AtHeight(lower).Length > 0 ? "actual-WarmSkin-vertex-height" : "actual-neck-bone-bound",
                upperBoundaryKind = AtHeight(upper).Length > 0 ? "actual-WarmSkin-vertex-height" : "actual-neck-bone-bound" };
            var candidates = new List<PlaneProvenance> { Candidate(neck.y, "original-neck-bone", neck.y, neck.y), Candidate((sliceMinimum + sliceMaximum) * .5f, "original-measured-neck-slice-center", sliceMinimum, sliceMaximum) }
                .GroupBy(c => c.y).Select(g => g.First()).ToList();
            int originalCount = candidates.Count, noMidpoint = 0;
            var knots = heights.Select(g => g.Key).Where(y => y >= neck.y && y <= sliceMaximum).Concat(sliceMaximum >= neck.y ? new[] { neck.y, sliceMaximum } : new[] { neck.y }).Distinct().OrderBy(y => y).ToArray();
            Need(knots.All(Finite), "Actual neck-slice candidate heights must be finite.");
            var additional = knots.Select(y => Candidate(y, "actual-WarmSkin-vertex-height", y, y)).ToList();
            for (int i = 0; i + 1 < knots.Length; i++)
            {
                float middle = knots[i] + (knots[i + 1] - knots[i]) * .5f;
                if (middle <= knots[i] || middle >= knots[i + 1]) { noMidpoint++; continue; }
                additional.Add(Candidate(middle, "measured-WarmSkin-height-interval-midpoint", knots[i], knots[i + 1]));
            }
            candidates.AddRange(additional.OrderBy(c => c.y).GroupBy(c => c.y).Select(g => g.First()).Where(c => !candidates.Any(original => original.y.Equals(c.y))).ToArray());
            bool candidateLimitExceeded = false;
            for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
            {
                // Preserve the original success path. Enforce the complete-set
                // limit only after both original planes have actually failed.
                if (candidateIndex == originalCount && candidates.Count > 256) { candidateLimitExceeded = true; diagnostics.Add("complete-candidate-set=" + candidates.Count + " exceeds256; no additional plane was evaluated"); break; }
                var candidate = candidates[candidateIndex]; float plane = candidate.y;
                var nodes = new List<Vector3>(); var edges = new Dictionary<(int, int), CutSegment>();
                var rawSegmentDiagnostics = new List<SegmentDiagnostic>(); int rawSegmentCount = 0, collapsedSegments = 0, duplicateEdges = 0, emptyCuts = 0, singleCuts = 0, onPlaneVertices = 0;
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
                        if (Mathf.Abs(da) <= planeTolerance) { onPlaneVertices++; Add(e, e, 0); }
                        if (da * db < 0) Add(e, next, da / (da - db));
                    }
                    if (cut.Count > 2) { coplanar++; continue; }
                    if (cut.Count != 2) { if (cut.Count == 0) emptyCuts++; else if (cut.Count == 1) singleCuts++; continue; }
                    int ia = Node(cut[0].point), ib = Node(cut[1].point);
                    var key = ia < ib ? (ia, ib) : (ib, ia);
                    rawSegmentCount++;
                    if (rawSegmentDiagnostics.Count < 512) rawSegmentDiagnostics.Add(new SegmentDiagnostic { nodeA = ia, nodeB = ib, skinTriangle = cut[0].triangle,
                        a = cut[0].point, b = cut[1].point, normalA = cut[0].normal, normalB = cut[1].normal, barycentricA = cut[0].barycentric, barycentricB = cut[1].barycentric,
                        weldedA = nodes[ia], weldedB = nodes[ib], collapsed = ia == ib, duplicateUnorderedEdge = edges.ContainsKey(key) });
                    if (ia == ib) { collapsedSegments++; continue; }
                    if (edges.ContainsKey(key)) duplicateEdges++;
                    if (!edges.ContainsKey(key)) edges.Add(key, new CutSegment { a = cut[0], b = cut[1], ia = ia, ib = ib });
                }
                var adjacency = nodes.Select(_ => new List<int>()).ToArray(); var all = edges.Values.ToArray();
                for (int e = 0; e < all.Length; e++) { adjacency[all[e].ia].Add(e); adjacency[all[e].ib].Add(e); }
                var componentDiagnostics = new List<ComponentDiagnostic>();
                var planeDiagnostic = new PlaneDiagnostic { planeY = plane, nodes = nodes.Count, edges = all.Length, coplanarTriangles = coplanar,
                    source = candidate, candidateIndex = candidateIndex,
                    recordedSegments = rawSegmentDiagnostics.Count, segmentsTruncated = rawSegmentCount > 512, rawSegments = rawSegmentCount,
                    collapsedSegments = collapsedSegments, duplicateUnorderedEdges = duplicateEdges, emptyCuts = emptyCuts, singleCuts = singleCuts, onPlaneTriangleVertices = onPlaneVertices,
                    cutSegments = rawSegmentDiagnostics.ToArray() };
                planeDiagnostics.Add(planeDiagnostic);
                var seen = new HashSet<int>(); NeckContour selected = null; float smallestArea = float.PositiveInfinity;
                foreach (int start in Enumerable.Range(0, nodes.Count))
                {
                    if (!seen.Add(start)) continue; var component = new List<int>(); var componentEdges = new HashSet<int>(); var queue = new Queue<int>(); queue.Enqueue(start);
                    while (queue.Count > 0) { int n = queue.Dequeue(); component.Add(n); foreach (int e in adjacency[n]) { componentEdges.Add(e); int next = all[e].ia == n ? all[e].ib : all[e].ia; if (seen.Add(next)) queue.Enqueue(next); } }
                    var componentPoints = component.Select(n => nodes[n]).ToArray(); var nodeBounds = new Bounds(componentPoints[0], Vector3.zero); foreach (var p in componentPoints) nodeBounds.Encapsulate(p);
                    var degreeGroups = component.GroupBy(n => adjacency[n].Count).OrderBy(g => g.Key).ToArray();
                    var detail = new ComponentDiagnostic { index = componentDiagnostics.Count, nodes = component.Count, edges = componentEdges.Count,
                        degreeValues = degreeGroups.Select(g => g.Key).ToArray(), degreeCounts = degreeGroups.Select(g => g.Count()).ToArray(),
                        nodeDegrees = component.Take(512).Select(n => new NodeDiagnostic { node = n, degree = adjacency[n].Count, actorPoint = nodes[n] }).ToArray(), nodeListTruncated = component.Count > 512,
                        actualNodeBounds = nodeBounds, minimum = nodeBounds.min, maximum = nodeBounds.max, centroid = componentPoints.Aggregate(Vector3.zero, (sum, p) => sum + p) / componentPoints.Length,
                        minimumNodeRadius = componentPoints.Min(p => new Vector2(p.x - neck.x, p.z - neck.z).magnitude), maximumNodeRadius = componentPoints.Max(p => new Vector2(p.x - neck.x, p.z - neck.z).magnitude), maximumAllowedRadius = half * .65f,
                        nodeCountPass = component.Count >= 3, degreeTwoPass = component.All(n => adjacency[n].Count == 2) };
                    componentDiagnostics.Add(detail);
                    if (component.Count < 3 || component.Any(n => adjacency[n].Count != 2)) { detail.rejectionReason = component.Count < 3 ? "fewer-than-three-nodes" : "node-degree-is-not-two"; continue; }
                    var cycle = new List<int>(); int previous = -1, current = start;
                    do { cycle.Add(current); int next = adjacency[current].Select(e => all[e].ia == current ? all[e].ib : all[e].ia).First(n => n != previous); previous = current; current = next; } while (current != start && cycle.Count <= component.Count);
                    detail.cycleNodes = cycle.Count; detail.cycleCloses = current == start; detail.cycleIncludesAllNodes = cycle.Count == component.Count;
                    if (current != start || cycle.Count != component.Count) { detail.rejectionReason = current != start ? "cycle-does-not-close" : "cycle-does-not-include-all-component-nodes"; continue; }
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
                    detail.properCrossingPass = simple;
                    if (!simple) { detail.rejectionReason = "proper-segment-crossing"; continue; }
                    float area = 0, minRadius = float.PositiveInfinity, maxRadius = 0; int crossings = 0;
                    for (int i = 0; i < cycle.Count; i++)
                    {
                        var a = nodes[cycle[i]] - neck; var b = nodes[cycle[(i + 1) % cycle.Count]] - neck; area += a.x * b.z - b.x * a.z;
                        float radius = new Vector2(a.x, a.z).magnitude; minRadius = Mathf.Min(minRadius, radius); maxRadius = Mathf.Max(maxRadius, radius);
                        if ((a.z > 0) != (b.z > 0) && a.x + (b.x - a.x) * -a.z / (b.z - a.z) > 0) crossings++;
                    }
                    area = Mathf.Abs(area) * .5f;
                    detail.areaMeasured = true; detail.containmentMeasured = true; detail.area = area; detail.minimumNodeRadius = minRadius; detail.maximumNodeRadius = maxRadius;
                    detail.neckCenterContained = crossings % 2 == 1; detail.finiteAreaPass = Finite(area) && area > weld * weld;
                    detail.minimumRadiusPass = minRadius > weld; detail.shoulderRadiusPass = maxRadius < half * .65f; detail.smallerAreaPass = area < smallestArea;
                    if (crossings % 2 != 1 || area <= weld * weld || minRadius <= weld || maxRadius >= half * .65f || area >= smallestArea)
                    {
                        detail.rejectionReason = crossings % 2 != 1 ? "neck-center-outside-cycle" : area <= weld * weld ? "area-at-or-below-weld-squared" : minRadius <= weld ? "minimum-radius-at-or-below-weld" : maxRadius >= half * .65f ? "maximum-radius-outside-measured-shoulder-bound" : "larger-or-equal-area-than-prior-candidate";
                        continue;
                    }
                    detail.selectedCandidate = true; detail.rejectionReason = "eligible-candidate";
                    smallestArea = area; selected = new NeckContour { center = neck, planeY = plane, minimumRadius = minRadius, maximumRadius = maxRadius, segments = componentEdges.Select(e => all[e]).ToArray(), source = candidate, candidateCount = candidates.Count, planesEvaluated = candidateIndex + 1 };
                }
                planeDiagnostic.components = componentDiagnostics.ToArray();
                diagnostics.Add("plane=" + plane + " segments=" + all.Length + " nodes=" + nodes.Count + " coplanar=" + coplanar);
                if (selected != null) return selected;
            }
            Bounds BoundsOf(IEnumerable<Vector3> values) { var a = values.ToArray(); var bounds = new Bounds(a[0], Vector3.zero); foreach (var p in a) bounds.Encapsulate(p); return bounds; }
            var fullDiagnostic = new ContourDiagnostic { actorId = actorId, sourceAsset = sourceAsset, sourceHash = sourceHash, actualNeck = neck,
                sourceFullVertexBounds = BoundsOf(points), sourceDrawnSkinBounds = BoundsOf(skin.Distinct().Select(i => points[i])), sourceVertices = points.Length, sourceSkinTriangles = skin.Length / 3,
                shoulderHalfWidth = half, maximumAllowedRadius = half * .65f, actorScale = scale, weldDistance = weld, planeTolerance = planeTolerance,
                sliceMinimumY = neckSlice.Min(p => p.y), sliceMaximumY = neckSlice.Max(p => p.y), planes = planeDiagnostics.ToArray(),
                candidates = candidates.Take(256).ToArray(), candidateCount = candidates.Count, originalCandidateCount = originalCount, candidateLimitExceeded = candidateLimitExceeded, candidateDiagnosticListTruncated = candidates.Count > 256, noRepresentableMidpointIntervals = noMidpoint,
                retainedRenderers = retainedRenderers.Where(r => r && r.sharedMesh).Select(r =>
                {
                    var summary = new RetainedRendererDiagnostic { renderer = r.name, meshAsset = AssetDatabase.GetAssetPath(r.sharedMesh), vertices = r.sharedMesh.vertexCount,
                        activeInHierarchy = r.gameObject.activeInHierarchy, enabled = r.enabled, readable = r.sharedMesh.isReadable,
                        materials = r.sharedMaterials.Select(m => m ? m.name : "<null>").ToArray(), materialAssets = r.sharedMaterials.Select(m => m ? AssetDatabase.GetAssetPath(m) : "").ToArray(), shaders = r.sharedMaterials.Select(m => m && m.shader ? m.shader.name : "").ToArray() };
                    try { summary.meshHash = GospelCharacterRefinement.MeshHash(r.sharedMesh); summary.triangles = r.sharedMesh.triangles.Length / 3; summary.uv0Count = r.sharedMesh.uv.Length; summary.tangentCount = r.sharedMesh.tangents.Length; summary.normalCount = r.sharedMesh.normals.Length; }
                    catch (Exception e) { summary.error = e.ToString(); }
                    return summary;
                }).ToArray() };
            Debug.Log("FIRSTGAME_NECK_CONTOUR_DIAGNOSTIC " + JsonUtility.ToJson(fullDiagnostic, true));
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
            var nativeBindposes = source.bindposes.ToArray(); var bodyNativeWeights = GospelCharacterRefinement.VertexWeights(source);
            Need(nativeBindposes.Length == originalBones.Length && originalBones.All(b => b), "Complete original body palette and bindposes required.");
            Need(QualitySettings.skinWeights == SkinWeights.Unlimited, "Full native garment influences require the deliberately configured Unlimited skin-weight level.");
            var actorSkinPalette = originalBones.Select((b, i) => actor.transform.worldToLocalMatrix * b.localToWorldMatrix * nativeBindposes[i]).ToArray();
            Transform Bone(HumanBodyBones name) { var b = animator.GetBoneTransform(name); Need(b && b.IsChildOf(actor.transform), "Actual garment bone missing: " + name); return b; }
            var bones = new[] { Bone(HumanBodyBones.Chest), Bone(HumanBodyBones.Neck), Bone(HumanBodyBones.LeftUpperArm), Bone(HumanBodyBones.RightUpperArm) };
            Vector3 Point(Transform b) => actor.transform.InverseTransformPoint(b.position);
            Vector3 neck = Point(bones[1]), chest = Point(bones[0]); float half = Mathf.Abs(Point(bones[2]).x - Point(bones[3]).x) * .5f;
            Need(half > .06f && half < .30f && neck.y > chest.y, "Measured neck/chest/shoulder frame required.");
            float maximumSeamDistance = Vector3.Distance(neck, chest); Need(Finite(maximumSeamDistance) && maximumSeamDistance > 0, "Actual neck/chest source-bone span required.");
            var bodyBakeSample = new Mesh(); var skin = new List<int>();
            Vector3[] points, normals;
            try
            {
                body.BakeMesh(bodyBakeSample, false); var bakedScale = body.transform.lossyScale;
                Need(Finite(bakedScale.x) && Finite(bakedScale.y) && Finite(bakedScale.z) && bakedScale.x > 0 && bakedScale.y > 0 && bakedScale.z > 0, "BakeMesh scale compensation requires actual finite positive renderer scale.");
                var inverseBakedScale = new Vector3(1 / bakedScale.x, 1 / bakedScale.y, 1 / bakedScale.z);
                Need(Finite(inverseBakedScale.x) && Finite(inverseBakedScale.y) && Finite(inverseBakedScale.z), "Actual renderer scale must have a finite reciprocal.");
                // BakeMesh(false) already carries renderer lossyScale. Remove
                // it once before the full renderer-to-actor transform.
                var map = actor.transform.worldToLocalMatrix * body.transform.localToWorldMatrix * Matrix4x4.Scale(inverseBakedScale);
                points = bodyBakeSample.vertices.Select(v => map.MultiplyPoint3x4(v)).ToArray(); normals = bodyBakeSample.normals.Select(n => map.inverse.transpose.MultiplyVector(n).normalized).ToArray();
                for (int s = 0; s < source.subMeshCount; s++)
                    if (body.sharedMaterials[s] && body.sharedMaterials[s].name == id + "__WarmSkin") skin.AddRange(source.GetTriangles(s));
            }
            finally { Object.DestroyImmediate(bodyBakeSample); }
            Need(skin.Count >= 24 && points.Length == source.vertexCount && normals.Length == points.Length, "Actual drawn anatomical WarmSkin triangles required.");
            var referenced = skin.Distinct().Select(i => points[i]).ToArray(); float scale = half / .18f;
            var neckSliceVertexIds = skin.Distinct().Where(i => Mathf.Abs(points[i].y - neck.y) < .025f * scale && Mathf.Abs(points[i].x - neck.x) < half * .45f).ToArray();
            var neckSlice = neckSliceVertexIds.Select(i => points[i]).ToArray();
            Need(neckSlice.Length >= 4, "Actual neck skin section required.");
            var neckContour = MeasureNeckContour(points, normals, skin.ToArray(), neck, neckSlice, neckSliceVertexIds, half, scale, id, AssetDatabase.GetAssetPath(source), sourceHash, renderers);
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
            var triangleIsCloth = Enumerable.Repeat(false, skin.Count / 3).ToList(); var triangleSourceIndices = Enumerable.Range(0, skin.Count / 3).ToList(); var triangleVertexOffsets = Enumerable.Repeat(0, skin.Count / 3).ToList();
            var pointNativeWeights = bodyNativeWeights.ToList(); var triangleMeshAssets = Enumerable.Repeat(AssetDatabase.GetAssetPath(source), skin.Count / 3).ToList();
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
                    Need(r.bones.SequenceEqual(originalBones) && r.rootBone == body.rootBone && r.sharedMesh.bindposes.SequenceEqual(nativeBindposes)
                        && r.transform.parent == body.transform.parent && r.transform.localPosition.Equals(body.transform.localPosition) && r.transform.localRotation.Equals(body.transform.localRotation) && r.transform.localScale.Equals(body.transform.localScale), "Actual source cloth must share the exact body native palette, bindposes and renderer frame.");
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
                        triangleIsCloth.AddRange(Enumerable.Repeat(true, drawn.Length / 3)); triangleSourceIndices.AddRange(Enumerable.Range(0, drawn.Length / 3)); triangleVertexOffsets.AddRange(Enumerable.Repeat(offset, drawn.Length / 3));
                        pointNativeWeights.AddRange(GospelCharacterRefinement.VertexWeights(r.sharedMesh)); triangleMeshAssets.AddRange(Enumerable.Repeat(AssetDatabase.GetAssetPath(r.sharedMesh), drawn.Length / 3));
                        var refs = drawn.Distinct().Select(i => clothPoints[i]).ToArray(); var clothBounds = BoundsOf(refs); string hash = GospelCharacterRefinement.MeshHash(r.sharedMesh);
                        projectionSources.Add(new ProjectionSourceReport { renderer = r.name, material = slots[slot].name, materialSlot = slot, meshAsset = AssetDatabase.GetAssetPath(r.sharedMesh), meshHash = hash, referencedVertices = refs.Length, triangles = drawn.Length / 3, actualDrawnBounds = clothBounds });
                        projectionMeshes.Add(r.sharedMesh); projectionHashes.Add(hash); clothTriangles += drawn.Length / 3; measuredSurfaceTop = Mathf.Max(measuredSurfaceTop, clothBounds.max.y);
                    }
                    finally { Object.DestroyImmediate(baked); }
                }
            }
            Need(id == "Mary" || clothTriangles > 0, "The actual retained fitted source-shirt draw surface is required.");
            var surface = new Surface { points = projectedPoints.ToArray(), normals = projectedNormals.ToArray(), triangles = projectedTriangles.ToArray(), triangleSources = triangleSources.ToArray(), triangleIsCloth = triangleIsCloth.ToArray(), triangleSourceIndices = triangleSourceIndices.ToArray(), triangleVertexOffsets = triangleVertexOffsets.ToArray(), triangleMeshAssets = triangleMeshAssets.ToArray(), pointWeights = pointNativeWeights.ToArray(), minY = chest.y - .06f * scale, maxY = measuredSurfaceTop };
            surface.BuildClothComponents(.00005f * scale);
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
            var vertices = new List<Vector3>(); var neutralActorPoints = new List<Vector3>(); var uv = new List<Vector2>(); var weights = new List<BoneWeight1[]>(); var panels = new List<int[]>();
            var attachments = new List<VertexAttachment>(); float maximumNeutralError = 0, maximumBindError = 0;
            var measuredSeams = new List<SeamReport>();
            var seamCache = new Dictionary<float, (CutAnchor inner, Vector3 contact, Vector3 contactNormal, SurfaceHit contactHit, Vector3 overlap, Vector3 overlapNormal, SurfaceHit overlapHit)>();
            var fabricCache = new Dictionary<(float, float), FabricSample>();
            int bridgeSamples = 0, liftSamples = 0; float maxBridge = 0, maxLift = 0;
            SourceAnchor Anchor(SurfaceHit hit) => new SourceAnchor { meshAsset = hit.meshAsset, source = hit.source, triangle = hit.sourceTriangle, vertices = hit.sourceVertexIds, barycentric = hit.barycentric };
            SurfaceHit InnerHit(CutAnchor anchor) => new SurfaceHit { meshAsset = AssetDatabase.GetAssetPath(source), source = body.name + "/" + id + "__WarmSkin", triangle = anchor.triangle, sourceTriangle = anchor.triangle,
                sourceVertexIds = new Vector3Int(skin[anchor.triangle * 3], skin[anchor.triangle * 3 + 1], skin[anchor.triangle * 3 + 2]), barycentric = anchor.barycentric };
            FabricSample Place(float t, float angle)
            {
                if (fabricCache.TryGetValue((t, angle), out var cached)) return cached;
                if (!seamCache.TryGetValue(angle, out var seam))
                {
                    var inner = neckContour.Sample(angle);
                    surface.diagnostic.queryKind = "required actual-Linen radial-plane closest seam";
                    var contact = surface.ClosestClothSeam(neck, inner, angle, maximumSeamDistance, scale, out var contactNormal, out var contactHit);
                    var radial = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)); float overlapRadius = contactHit.radius + Thickness * 2;
                    surface.diagnostic.queryKind = "required actual connected-Linen overlap strip";
                    var overlap = surface.ClosestClothSeam(neck, inner, angle, maximumSeamDistance, scale, out var overlapNormal, out var overlapHit,
                        surface.triangleComponents[contactHit.triangle], contactHit.source, overlapRadius, null, contact + radial * Thickness * 2);
                    seam = (inner, contact, contactNormal, contactHit, overlap, overlapNormal, overlapHit); seamCache.Add(angle, seam);
                    measuredSeams.Add(new SeamReport { angleRadians = angle, innerRadius = new Vector2(inner.point.x - neck.x, inner.point.z - neck.z).magnitude,
                        inner = inner.point, innerNormal = inner.normal, innerBarycentric = inner.barycentric, innerSkinTriangle = inner.triangle, innerSource = body.name + "/" + id + "__WarmSkin",
                        outer = contact, outerNormal = contactNormal, outerProjectionTriangle = contactHit.triangle, outerSource = contactHit.source,
                        outerBarycentric = contactHit.barycentric, outerSourceVertexIds = contactHit.sourceVertexIds, outerSourceTriangle = contactHit.sourceTriangle,
                        outerRadius = contactHit.radius, outerDistance = contactHit.distance, outerPlaneResidual = contactHit.planeResidual, outerMethod = contactHit.method, mainRing = surface.diagnostic.panel == 0, outerMaximumSpan = maximumSeamDistance, outerDistanceRatio = contactHit.distance / maximumSeamDistance,
                        overlap = overlap, overlapBarycentric = overlapHit.barycentric, overlapSource = overlapHit.source, overlapProjectionTriangle = overlapHit.triangle, overlapLength = Vector3.Distance(contact, overlap) });
                }
                var innerSource = InnerHit(seam.inner); float bridgeEnd = BridgeRows / (float)Rows, bridgeT = Mathf.Clamp01(t / bridgeEnd);
                var sample = new FabricSample();
                if (t <= 0)
                { sample.point = seam.inner.point; sample.normal = seam.inner.normal; sample.weights = surface.WeightsAt(innerSource); sample.sources = new[] { Anchor(innerSource) }; sample.blend = new[] { 1f }; sample.region = "native-skin-inner-seam"; }
                else if (t >= bridgeEnd)
                {
                    float q = Mathf.InverseLerp(bridgeEnd, 1, t); SurfaceHit hit;
                    if (q <= 0) { sample.point = seam.contact; sample.normal = seam.contactNormal; hit = seam.contactHit; }
                    else if (q >= 1) { sample.point = seam.overlap; sample.normal = seam.overlapNormal; hit = seam.overlapHit; }
                    else
                    {
                        float radius = Mathf.Lerp(seam.contactHit.radius, seam.overlapHit.radius, q);
                        sample.point = surface.ClosestClothSeam(neck, seam.inner, angle, maximumSeamDistance, scale, out sample.normal, out hit,
                            surface.triangleComponents[seam.contactHit.triangle], seam.contactHit.source, radius - .00005f * scale, radius + .00005f * scale, Vector3.Lerp(seam.contact, seam.overlap, q));
                    }
                    sample.weights = surface.WeightsAt(hit); sample.sources = new[] { Anchor(hit) }; sample.blend = new[] { 1f }; sample.region = "native-cloth-overlap-strip";
                }
                else
                {
                    sample.point = Vector3.Lerp(seam.inner.point, seam.contact, bridgeT); sample.normal = Vector3.Lerp(seam.inner.normal, seam.contactNormal, bridgeT).normalized;
                    sample.weights = MixNative(new[] { surface.WeightsAt(innerSource), surface.WeightsAt(seam.contactHit) }, new[] { 1 - bridgeT, bridgeT });
                    sample.sources = new[] { Anchor(innerSource), Anchor(seam.contactHit) }; sample.blend = new[] { 1 - bridgeT, bridgeT }; sample.region = "native-weighted-fabric-bridge";
                    surface.diagnostic.queryKind = "optional surface above source-anchored fabric chord";
                    if (surface.TryProject(sample.point.x, sample.point.z, out var covering, out var coveringNormal, out var coveringHit) && covering.y > sample.point.y)
                    { maxLift = Mathf.Max(maxLift, covering.y - sample.point.y); liftSamples++; sample.point = covering; sample.normal = coveringNormal; sample.weights = surface.WeightsAt(coveringHit); sample.sources = new[] { Anchor(coveringHit) }; sample.blend = new[] { 1f }; sample.region = "native-source-cover"; }
                    else
                    {
                        // This is cloth intentionally spanning its measured
                        // neck and shirt anchors, not an invented skin hit.
                        bridgeSamples++; maxBridge = Mathf.Max(maxBridge, Vector3.Distance(seam.inner.point, seam.contact));
                    }
                }
                Need(Finite(sample.point.x) && Finite(sample.point.y) && Finite(sample.point.z) && sample.normal.sqrMagnitude > .5f, "Every fabric point requires finite measured anchors and a complete normal.");
                fabricCache.Add((t, angle), sample); return sample;
            }
            void AddPanel(int columns, bool wrap, float first, float last, float lift)
            {
                int offset = vertices.Count, count = (Rows + 1) * columns; var faces = new List<int>(); var edge = new Dictionary<(int, int), (int, int, int)>();
                for (int side = 0; side < 2; side++) for (int row = 0; row <= Rows; row++) for (int col = 0; col < columns; col++)
                {
                    float t = row / (float)Rows, a = Mathf.Lerp(first, last, col / (float)(wrap ? columns : columns - 1));
                    surface.diagnostic.panel = panels.Count; surface.diagnostic.side = side; surface.diagnostic.row = row;
                    surface.diagnostic.angularIndex = col; surface.diagnostic.columns = columns; surface.diagnostic.t = wrap ? t : Mathf.Lerp(.15f, .92f, t); surface.diagnostic.angleRadians = a;
                    float actualT = wrap ? t : Mathf.Lerp(.15f, .92f, t); var sample = Place(actualT, a);
                    float contactBlend = Mathf.SmoothStep(0, 1, Mathf.Clamp01(actualT / (BridgeRows / (float)Rows)));
                    float normalOffset = Mathf.Lerp(Thickness, Thickness * .25f, contactBlend) + lift - side * Thickness;
                    Vector3 actorPoint = sample.point + sample.normal * normalOffset; var map = WeightedSkin(actorSkinPalette, sample.weights); var bindPoint = map.inverse.MultiplyPoint3x4(actorPoint);
                    float error = Vector3.Distance(map.MultiplyPoint3x4(bindPoint), actorPoint); maximumNeutralError = Mathf.Max(maximumNeutralError, error);
                    var identity = map.inverse * map; for (int n = 0; n < 16; n++) maximumBindError = Mathf.Max(maximumBindError, Mathf.Abs(identity[n] - Matrix4x4.identity[n]));
                    vertices.Add(bindPoint); neutralActorPoints.Add(actorPoint); weights.Add(sample.weights); uv.Add(new Vector2(col / (float)columns, t));
                    attachments.Add(new VertexAttachment { vertex = vertices.Count - 1, panel = panels.Count, side = side, row = row, column = col, t = actualT, angleRadians = a,
                        sourceNormalOffset = normalOffset, neutralActorPoint = actorPoint, bindPoint = bindPoint, region = sample.region, sources = sample.sources, sourceBlend = sample.blend });
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
            var mesh = new Mesh { name = id + "__HumanNecklineV1", vertices = vertices.ToArray(), uv = uv.ToArray(), bindposes = nativeBindposes, subMeshCount = panels.Count };
            using (var counts = new Unity.Collections.NativeArray<byte>(weights.Select(w => checked((byte)w.Length)).ToArray(), Unity.Collections.Allocator.Temp))
            using (var native = new Unity.Collections.NativeArray<BoneWeight1>(weights.SelectMany(w => w).ToArray(), Unity.Collections.Allocator.Temp)) mesh.SetBoneWeights(counts, native);
            var readback = GospelCharacterRefinement.VertexWeights(mesh); float maximumWeightError = 0;
            for (int v = 0; v < readback.Length; v++)
            {
                var intended = weights[v].ToDictionary(w => w.boneIndex, w => w.weight); var actual = readback[v].ToDictionary(w => w.boneIndex, w => w.weight);
                foreach (int bone in intended.Keys.Concat(actual.Keys).Distinct()) maximumWeightError = Mathf.Max(maximumWeightError, Mathf.Abs((intended.TryGetValue(bone, out float a) ? a : 0) - (actual.TryGetValue(bone, out float b) ? b : 0)));
                maximumNeutralError = Mathf.Max(maximumNeutralError, Vector3.Distance(WeightedSkin(actorSkinPalette, readback[v]).MultiplyPoint3x4(vertices[v]), neutralActorPoints[v]));
            }
            Need(maximumWeightError <= .00005f && maximumNeutralError <= .0005f, "Native weight storage must retain every meaningful source influence and reconstruct the actual neutral garment within 0.5mm.");
            for (int s = 0; s < panels.Count; s++) mesh.SetTriangles(panels[s], s); mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            Need(mesh.uv.Length == vertices.Count && mesh.uv.All(v => Finite(v.x) && Finite(v.y)) && mesh.tangents.Length == vertices.Count && mesh.tangents.All(v => Finite(v.x) && Finite(v.y) && Finite(v.z) && Finite(v.w) && new Vector3(v.x, v.y, v.z).sqrMagnitude > .5f), "Every new vertex requires finite UV0 and nonzero recalculated tangents.");
            Need(readback.All(set => set.All(w => Finite(w.weight) && w.weight > 0 && w.boneIndex >= 0 && w.boneIndex < originalBones.Length) && Mathf.Abs(set.Sum(w => w.weight) - 1) < .002f), "Every new vertex requires the complete finite normalized source-native influence set.");
            float roundTrip = maximumBindError;
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
            var go = new GameObject(id + "__HumanNecklineV1"); go.transform.SetParent(body.transform.parent, false); go.transform.localPosition = body.transform.localPosition; go.transform.localRotation = body.transform.localRotation; go.transform.localScale = body.transform.localScale;
            var renderer = go.AddComponent<SkinnedMeshRenderer>(); renderer.sharedMesh = mesh; renderer.bones = originalBones; renderer.rootBone = body.rootBone; renderer.sharedMaterials = mats; renderer.quality = SkinQuality.Auto; renderer.updateWhenOffscreen = true; var bounds = mesh.bounds; bounds.Expand(.06f); renderer.localBounds = bounds;
            Need(body.sharedMesh == source && body.bones.SequenceEqual(originalBones) && GospelCharacterRefinement.MeshHash(source) == sourceHash, "Retained body geometry and skin palette must remain unchanged.");
            Need(renderer.bones.SequenceEqual(body.bones) && renderer.rootBone == body.rootBone && mesh.bindposes.SequenceEqual(source.bindposes)
                && renderer.transform.parent == body.transform.parent && renderer.transform.localPosition.Equals(body.transform.localPosition) && renderer.transform.localRotation.Equals(body.transform.localRotation) && renderer.transform.localScale.Equals(body.transform.localScale), "The authored neckline must retain its actual source-native renderer frame and complete bind palette.");
            Need(projectionMeshes.Select((m, i) => GospelCharacterRefinement.MeshHash(m) == projectionHashes[i]).All(unchanged => unchanged), "Every sampled anatomical/retained-cloth source must remain unchanged.");
            float maxAdjacentOuter = 0; var orderedSeams = measuredSeams.Where(s => s.mainRing).OrderBy(s => Mathf.Repeat(s.angleRadians, Mathf.PI * 2)).ToArray();
            for (int i = 0; i < orderedSeams.Length; i++) { orderedSeams[i].nextOuterDistance = Vector3.Distance(orderedSeams[i].outer, orderedSeams[(i + 1) % orderedSeams.Length].outer); maxAdjacentOuter = Mathf.Max(maxAdjacentOuter, orderedSeams[i].nextOuterDistance); }
            string anchorsPath = folder + "/" + mesh.name + ".anchors.json";
            PersistText(anchorsPath, JsonUtility.ToJson(new AttachmentRecord { id = id, renderer = renderer.name, meshAsset = path, sourceBodyAsset = AssetDatabase.GetAssetPath(source), sourceBodyHash = sourceHash,
                segments = Segments, rows = Rows, bridgeRows = BridgeRows, paletteBones = originalBones.Length, palette = originalBones.Select(b => b.name).ToArray(), vertices = attachments.ToArray() }, true) + "\n");
            return new Report {
                id = id, renderer = go.name, meshAsset = path, meshHash = GospelCharacterRefinement.MeshHash(mesh), materialAsset = AssetDatabase.GetAssetPath(linen),
                sourceBodyAsset = AssetDatabase.GetAssetPath(source), sourceBodyHash = sourceHash, sourceSkinTriangles = skin.Count / 3, sampledVertices = referenced.Length,
                vertices = mesh.vertexCount, triangles = panels.Sum(a => a.Length / 3), placketTriangles = panels.Count > 1 ? panels[1].Length / 3 : 0,
                maximumInfluences = readback.Max(w => w.Length), indexCount = panels.Sum(a => a.Length), boundaryEdges = edges.Count(e => e.Value == 1), nonManifoldEdges = edges.Count(e => e.Value != 2),
                neckRadiusX = rx, neckRadiusZ = rz, shoulderHalfWidth = half, thickness = Thickness, clearance = Thickness, maximumBindRoundTripError = roundTrip, actorBounds = BoundsOf(neutralActorPoints.ToArray()),
                originalBodyUnchanged = true, originalBonesUnchanged = true, closedTwoManifold = true, finiteUvAndTangents = true, nativeMotionAndVisualReviewCompleted = false,
                projectionSources = projectionSources.ToArray(), sourceClothTriangles = clothTriangles, measuredNeckSkinMaximumY = measuredNeckTop, projectionMinimumY = surface.minY, projectionMaximumY = surface.maxY,
                projectionSourceMeshesUnchanged = true, innerPlaneY = neckContour.planeY, innerContourMinimumRadius = neckContour.minimumRadius, innerContourMaximumRadius = neckContour.maximumRadius,
                innerContourSegments = neckContour.segments.Length, maximumBridgeSpan = maxBridge, maximumSurfaceLift = maxLift, fabricBridgeSamples = bridgeSamples, surfaceLiftSamples = liftSamples,
                measuredSeams = measuredSeams.ToArray(), chosenPlaneSource = neckContour.source, neckPlaneCandidateCount = neckContour.candidateCount, neckPlanesEvaluated = neckContour.planesEvaluated,
                maximumAdjacentOuterDistance = maxAdjacentOuter, actualNeckChestSpan = maximumSeamDistance, maximumOuterDistanceRatio = measuredSeams.Max(s => s.outerDistanceRatio),
                sourcePaletteBones = originalBones.Length, maximumNativeInfluences = readback.Max(w => w.Length), nativeInfluenceCount = readback.Sum(w => w.Length),
                maximumNativeWeightReadbackError = maximumWeightError, maximumNeutralReconstructionError = maximumNeutralError,
                minimumOverlapLength = measuredSeams.Min(s => s.overlapLength), maximumOverlapLength = measuredSeams.Max(s => s.overlapLength),
                sourceRendererFrame = body.transform.parent.name + "/" + body.name, rendererQuality = renderer.quality.ToString(), globalSkinWeights = QualitySettings.skinWeights.ToString(), anchorsAsset = anchorsPath,
                fullSourcePaletteAndBindposesRetained = renderer.bones.SequenceEqual(body.bones) && renderer.rootBone == body.rootBone && mesh.bindposes.SequenceEqual(source.bindposes)
            };
        }
        static void PersistText(string path, string data)
        {
            string file = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, path);
            if (System.IO.File.Exists(file)) Need(System.IO.File.ReadAllText(file) == data, "Preserve a differing prior attachment metadata file.");
            else System.IO.File.WriteAllText(file, data);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
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
