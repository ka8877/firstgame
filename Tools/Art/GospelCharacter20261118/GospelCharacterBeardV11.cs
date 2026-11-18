#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Firstgame.GospelNpr.Editor
{
    /// <summary>
    /// Root-only, source-face-bound volumetric beard authoring. No imported
    /// source mesh, bone, expression, material or original texture is edited.
    /// </summary>
    public static class GospelCharacterBeard
    {
        const int Sides = 6, VerticesPerClump = 20, MaximumClumps = 224;
        const string NewAssetRoot = "Assets/Art/GospelCharacter20261118/";
        public const string OutputRoot = NewAssetRoot + "v2";
        const float MinimumNonzeroAnchorDelta = .0000101f, MorphReadbackTolerance = .000002f;
        const string JawFloorBasis = "actual_neck_y_plus_0.055_scale; head_y_observed_only";

        [Serializable] public sealed class AnchorReport
        {
            public int triangle, a, b, c, region;
            public Vector3 barycentric, actorPosition;
            public bool exactSharedPalette;
            public int influences;
            public float length, radius, minimumSingleFrameLipDistance;
        }

        [Serializable] public sealed class Report
        {
            public string actorId, renderer, meshAsset, sourceFaceAsset, sourceFacePayloadSha256, beardPayloadSha256, materialAsset;
            public int sourceSkinTriangles, candidates, clumps, jawClumps, cheekClumps, moustacheClumps, vertices, triangles, sourceMorphs, sourceMorphFrames;
            public int mixedPaletteVertexAnchors, barycentricSharedPaletteAnchors, maximumInfluences;
            public int[] precisionRejectedByRegion;
            public float minimumNonzeroAnchorDelta, morphReadbackTolerance, immediateMorphReadbackMaximumError, persistedMorphReadbackMaximumError;
            public float eyeSpan, actorScale, minimumSingleFrameLipDistance;
            public Vector3 mouth, chin;
            public Bounds actorBounds;
            public bool sourceFaceUnchanged, originalRigUnchanged, allSourceMorphFramesCopied, sourceNeutralDefaultsCopied;
            public bool nativeMotionAndVisualReviewCompleted;
            public string scope, materialUvRequirement, expressionBindingRequirement;
            public string jawFloorBasis;
            public int chinReferenceCandidates;
            public float jawFloor;
            public Vector3 observedHead, observedNeck;
            public AnchorReport[] anchors;
        }

        [Serializable] sealed class ChinPredicateCounts
        {
            public int total, x, lower, upper, depth, normFront, aboveActualNeck, belowMouth, aboveHeadFloor, aboveNeckFloor;
            public int xThenLower, xThenLowerThenUpper, xThenLowerThenUpperThenDepth, all;
            public int exceptX, exceptLower, exceptUpper, exceptDepth, exceptNormal;
        }
        [Serializable] sealed class ChinInfluence
        {
            public int boneIndex;
            public float weight;
        }
        [Serializable] sealed class ChinVertex
        {
            public int vertex, nativeInfluenceCount;
            public Vector3 actorPoint, actorNormal, rawPoint, rawNormal, nativeMappedActorPoint;
            public bool x, lower, upper, depth, normFront, aboveActualNeck, belowMouth, aboveHeadFloor, aboveNeckFloor, all;
            public float nativeWeightSum, rawToNativeDistance;
            public ChinInfluence[] nativeInfluences;
            public bool nativeInfluencesTruncated;
        }
        [Serializable] sealed class ChinNativeFrame
        {
            public int boneIndex;
            public string path;
            public float maximumFaceRestIdentityError;
            public float[] nativeToActor, faceRest;
        }
        [Serializable] sealed class ChinDiagnostic
        {
            public string actorId, sourceFaceAsset, sourceFaceHash, renderer, rendererPath, rootBone, quality, globalSkinWeights, matrixLayout, jawFloorBasis;
            public Vector3 headPoint, neckPoint, eye, mouthCentre;
            public float eyeSpan, scale, mouthHalfWidth, mouthHalfHeight, jawFloor, headJawFloor, neckJawFloor;
            public float maximumAbsX, maximumY, minimumZ, minimumNormalZ;
            public float rawToActorDeterminant, maximumSkinRawToNativeDistance;
            public Bounds sourceSkinBounds, modeledMouthBounds;
            public int sourceSkinTriangles, sourceSkinReferencedVertices, modeledMouthVertices, nativeBones, nativeFramesReported, maximumSkinRawToNativeVertex;
            public Vector3 rendererLocalPosition, rendererLocalScale, rendererLossyScale;
            public Quaternion rendererLocalRotation;
            public float[] rawToActor, normalToActor;
            public ChinPredicateCounts counts;
            public ChinVertex[] representativeVertices, nearestLowerAnteriorAboveActualNeck;
            public ChinNativeFrame[] nativeFrames;
            public string diagnosticFailure;
            public string scope = "Zero-chin diagnostic only. Original strict predicates and rejection remain unchanged; no source mutation or fabricated chin.";
        }
        [Serializable] sealed class QuotaRegion
        {
            public int region, candidates, examined, accepted, quota, precisionRejected, spacingRejected, basisLipRejected, morphLipRejected;
            public float spacing, minimumNormalizedAbsX, maximumNormalizedAbsX, minimumNormalizedMouthY, maximumNormalizedMouthY;
            public bool hasCandidates, quotaReached, globalLimitReached;
            public Bounds candidateBounds;
        }
        [Serializable] sealed class QuotaFrame
        {
            public int region, frameIndex, rejected;
            public string name;
            public float weight, halfWidth, halfHeight;
            public Vector3 mouth;
        }
        [Serializable] sealed class QuotaRejection
        {
            public int region, triangle, a, b, c, frameIndex;
            public Vector3 point, barycentric, frameDelta, frameMouth;
            public string frame;
            public float basisGap, frameGap, frameHalfWidth, frameHalfHeight;
        }
        [Serializable] sealed class QuotaDiagnostic
        {
            public string actorId, sourceFaceAsset, sourceFaceHash, jawFloorBasis;
            public int selected, vertices, maximumClumps, minimumSelected, minimumJaw, minimumCheek, minimumMoustache, maximumVertices, sourceMorphFrames;
            public float eyeSpan, scale, jawFloor, basisLipMinimum, morphLipMinimum, mouthHalfWidth, mouthHalfHeight, minimumNonzeroAnchorDelta;
            public Vector3 mouth, chin, eye, head, neck;
            public QuotaRegion[] regions;
            public QuotaFrame[] firstRejectingFrames;
            public QuotaRejection[] examples;
            public string scope = "Quota failure diagnostic only. Precision rejection excludes actual nonzero per-frame anchor displacements below the native sparse-storage safety floor; source geometry masks, quotas and mouth exclusion are unchanged. Frame counts identify the first lip-rejecting frame, not every rejecting frame.";
        }
        [Serializable] sealed class UpperLipVariant
        {
            public string direction, worstFrame;
            public float basisGap, minimumFrameGap, minimumMargin, worstFrameWeight, worstFrameHalfWidth, worstFrameHalfHeight;
            public int basisMinimumVertex, worstFrameIndex, worstFrameMinimumVertex, checkedFrames;
            public Vector3 worstFrameMouth, worstFrameAnchorDelta;
            public bool allFramesClear;
        }
        [Serializable] sealed class UpperLipCandidate
        {
            public int triangle, a, b, c, influences;
            public Vector3 point, normal, barycentric;
            public float normalizedAbsX, normalizedMouthY;
            public bool exactSharedPalette;
            public UpperLipVariant[] variants;
        }
        [Serializable] sealed class UpperFaceVertex
        {
            public int vertex;
            public Vector3 point, normal;
        }
        [Serializable] sealed class UpperLipDiagnostic
        {
            public string actorId, sourceFaceAsset, sourceFaceHash, diagnosticFailure;
            public Vector3 mouth, eye;
            public float scale, mouthHalfWidth, mouthHalfHeight, oldMaximumY, probeMinimumY, probeMaximumY, minimumAbsX, maximumAbsX, minimumZ, minimumNormalZ;
            public float oldMoustacheMinimumNormalizedY = .005f, oldMoustacheMaximumNormalizedY = .017f, basisRequired = 1.035f, morphRequired = 1.015f;
            public int actualFrames, candidates, safeCandidates, safeLeft, safeRight, anteriorCentralVertices;
            public int[] safeByVariant;
            public bool probeTruncated, hasSafeCandidates, hasAnteriorCentralVertices;
            public Bounds safeBounds, anteriorCentralBounds;
            public UpperLipCandidate[] records;
            public UpperFaceVertex[] anteriorCentralRecords;
            public string scope = "Quota-failure geometry probe only; source anchors stay on actual skin triangles. Direction probes do not move anchors. Upper-face records are observations, not an asserted nose boundary. Generator selection, quotas, successful assets/reports and 1.035/1.015 guards remain unchanged.";
        }

        sealed class Mouth
        {
            public Vector3 centre;
            public float halfWidth, halfHeight;
            public float Distance(Vector3 point)
            {
                float x = (point.x - centre.x) / halfWidth, y = (point.y - centre.y) / halfHeight;
                return Mathf.Sqrt(x * x + y * y);
            }
        }

        sealed class Frame
        {
            public string name;
            public int shape, index;
            public float weight;
            public Vector3[] delta;
            public Mouth mouth;
        }

        sealed class Anchor
        {
            public int triangle, a, b, c, region;
            public Vector3 bary, point, normal;
            public BoneWeight1[] influences;
            public bool shared;
            public uint key;
            public Vector3 Delta(Frame frame) => frame.delta[a] * bary.x + frame.delta[b] * bary.y + frame.delta[c] * bary.z;
        }

        public static Report Apply(GameObject actor, Animator animator, string actorId, Material beardMaterial, string outputRoot)
        {
            Need(!Application.isPlaying && actor && animator && animator.avatar && animator.avatar.isHuman && animator.avatar.isValid,
                "Beard derivation requires the actual neutral Editor Humanoid instance.");
            Need(actorId == "JesusAdult" || actorId == "Joseph" || actorId == "Teacher", "Only the three adult male identities have this beard design.");
            Need(beardMaterial && EditorUtility.IsPersistent(beardMaterial) && beardMaterial.shader && beardMaterial.shader.name.Contains("lilToon"),
                "Supply a new, persistent beard-appropriate lilToon material; source materials are never edited here.");
            Need(!string.IsNullOrEmpty(outputRoot) && outputRoot.TrimEnd('/') == OutputRoot,
                "This source requires GospelCharacterBeard.OutputRoot (v2); preserve earlier beard meshes and anchor sidecars.");
            var faces = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name == actorId + "__Face").ToArray();
            Need(faces.Length == 1, "One exact source-bound face renderer is required.");
            var face = faces[0]; var source = face.sharedMesh;
            Need(source && source.isReadable && source.vertexCount > 0 && face.bones.Length == source.bindposes.Length
                && face.bones.All(b => b), "The complete readable face skin/bind palette is required.");
            string name = actorId + "__CharacterBeard";
            Need(!actor.GetComponentsInChildren<Transform>(true).Any(t => t.name == name), "Derive from a fresh actor; never replace an existing beard object.");
            var avatar = animator.avatar; var controller = animator.runtimeAnimatorController;
            string originalFingerprint = Fingerprint(source);
            float[] defaults = Enumerable.Range(0, source.blendShapeCount).Select(face.GetBlendShapeWeight).ToArray();
            Need(defaults.All(w => Finite(w) && Mathf.Abs(w) < .00001f), "Use the fresh neutral face. Nonzero source expressions are not reset silently.");

            var points = source.vertices; var sourceNormals = source.normals;
            Need(sourceNormals.Length == points.Length, "Authored source-face normals are required for actual front-skin selection.");
            var toActor = actor.transform.worldToLocalMatrix * face.transform.localToWorldMatrix;
            Need(Finite(toActor.determinant) && toActor.determinant > .0000001f,
                "This closed clump basis requires a nonsingular, nonmirrored source face frame.");
            var fromActor = toActor.inverse; var normalToActor = fromActor.transpose;
            var actorPoints = points.Select(toActor.MultiplyPoint3x4).ToArray();
            var actorNormals = sourceNormals.Select(n => normalToActor.MultiplyVector(n).normalized).ToArray();
            Need(actorPoints.All(Finite) && actorNormals.All(Finite), "Source-face coordinates and authored normals must remain finite.");
            var left = animator.GetBoneTransform(HumanBodyBones.LeftEye); var right = animator.GetBoneTransform(HumanBodyBones.RightEye);
            var head = animator.GetBoneTransform(HumanBodyBones.Head); var neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            Need(left && right && head && neck, "Actual eye/head/neck bones are required; never guess their positions.");
            Vector3 eye = actor.transform.InverseTransformPoint((left.position + right.position) * .5f);
            float eyeSpan = Vector3.Distance(actor.transform.InverseTransformPoint(left.position), actor.transform.InverseTransformPoint(right.position));
            Need(eyeSpan > .025f && eyeSpan < .12f, "Unexpected source eye span; review this actor before deriving a beard.");
            float scale = eyeSpan / .061f;
            Vector3 headPoint = actor.transform.InverseTransformPoint(head.position), neckPoint = actor.transform.InverseTransformPoint(neck.position);
            Need(eye.z - headPoint.z > .005f * scale, "Source eye-to-head forward must face actor +Z.");
            Need(face.sharedMaterials.Length == source.subMeshCount, "Exact face submesh/material correspondence required.");
            var skinTriangles = new List<int>(); var mouthIndices = new HashSet<int>();
            for (int slot = 0; slot < source.subMeshCount; slot++)
            {
                var material = face.sharedMaterials[slot]; Need(material, "No missing face material is admitted.");
                if (material.name.IndexOf("Face_", StringComparison.OrdinalIgnoreCase) >= 0 && material.name.IndexOf("SKIN", StringComparison.OrdinalIgnoreCase) >= 0)
                    skinTriangles.AddRange(source.GetTriangles(slot));
                if (material.name.IndexOf("FaceMouth", StringComparison.OrdinalIgnoreCase) >= 0)
                    foreach (int index in source.GetTriangles(slot)) mouthIndices.Add(index);
            }
            Need(skinTriangles.Count > 0 && skinTriangles.Count % 3 == 0 && mouthIndices.Count > 0, "Actual Face_SKIN triangles and modeled mouth are required.");
            int[] oral = mouthIndices.OrderBy(i => i).ToArray();
            Mouth mouth = MouthAt(actorPoints, oral, null, toActor, scale);
            float jawFloor = neckPoint.y + .055f * scale;
            var chinCandidates = skinTriangles.Distinct().Where(i => Mathf.Abs(actorPoints[i].x - mouth.centre.x) < .034f * scale
                && actorPoints[i].y > jawFloor && actorPoints[i].y < mouth.centre.y - .012f * scale
                && actorPoints[i].z > mouth.centre.z - .028f * scale && actorNormals[i].z > .12f).ToArray();
            if (chinCandidates.Length == 0)
                LogChinDiagnostic(actor, face, actorId, originalFingerprint, actorPoints, actorNormals, skinTriangles, oral,
                    headPoint, neckPoint, eye, eyeSpan, scale, mouth, jawFloor, toActor, normalToActor);
            Need(chinCandidates.Length > 0, "An anterior facial chin distinct from the neck is required.");
            Vector3 chin = actorPoints[chinCandidates.OrderBy(i => actorPoints[i].y).First()];
            Need(mouth.centre.y - chin.y > .015f * scale && mouth.centre.y - chin.y < .10f * scale,
                "Actual mouth-to-chin span is outside this bounded beard design.");

            BoneWeight1[][] palettes = ReadPalettes(source, face.bones.Length);
            VerifyBindRest(face, palettes);
            var frames = ReadFrames(source, actorPoints, oral, toActor, scale);
            var candidates = Candidates(skinTriangles, actorPoints, actorNormals, palettes, mouth, chin.y, eye.y, scale);
            if (actorId == "Joseph" || actorId == "Teacher")
            {
                candidates.RemoveAll(anchor => anchor.region == 2);
                candidates.AddRange(LateralMoustacheCandidates(skinTriangles, actorPoints, actorNormals, palettes, mouth, eye.y, scale));
            }
            Need(candidates.Count > 0 && candidates.Count <= 24000, "Empty or excessive actual face-anchor candidates.");
            var selected = new List<Anchor>(); var generated = new List<Vector3>(); var normals = new List<Vector3>();
            var tangents = new List<Vector4>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            var outputAnchors = new List<Anchor>(); var reports = new List<AnchorReport>();
            var quotas = new[] { 128, 60, 36 }; var counts = new int[3]; float minimumGap = float.PositiveInfinity;
            var examined = new int[3]; var spacingRejected = new int[3]; var basisRejected = new int[3]; var morphRejected = new int[3];
            var precisionRejected = new int[3];
            var rejectedFrames = Enumerable.Range(0, 3).Select(_ => new Dictionary<Frame, int>()).ToArray();
            var rejectionExamples = new List<QuotaRejection>();
            foreach (int region in new[] { 0, 1, 2 }) foreach (var anchor in candidates.Where(a => a.region == region).OrderBy(a => a.key))
            {
                if (counts[region] >= quotas[region] || selected.Count >= MaximumClumps) break;
                examined[region]++;
                if (!PrecisionSafe(anchor, frames)) { precisionRejected[region]++; continue; }
                float spacing = (region == 2 ? .0018f : .0032f) * scale;
                if (selected.Any(a => Vector3.SqrMagnitude(a.point - anchor.point) < spacing * spacing)) { spacingRejected[region]++; continue; }
                float random = (anchor.key & 65535) / 65535f;
                float length = region == 2 ? Mathf.Lerp(.003f, .005f, random) : Mathf.Lerp(.004f, .010f, random);
                if (region == 0) length += .004f * (1 - Mathf.InverseLerp(chin.y, mouth.centre.y, anchor.point.y));
                if (actorId == "Joseph") length *= .82f;
                else if (actorId == "Teacher") length *= 1.18f;
                length = Mathf.Clamp(length, .002f, .018f) * scale;
                float radius = (region == 2 ? Mathf.Lerp(.00055f, .0009f, random) : Mathf.Lerp(.00085f, .00135f, random)) * scale;
                var clump = (actorId == "Joseph" || actorId == "Teacher") && region == 2
                    ? UpperLipProbeClump(anchor, mouth.centre.x, length, radius, scale, 1)
                    : Clump(anchor, mouth.centre.x, length, radius, scale);
                float gap = clump.Min(mouth.Distance); bool clear = gap >= 1.035f;
                if (!clear)
                {
                    basisRejected[region]++;
                    if (!rejectionExamples.Any(e => e.region == region && e.frame == "<basis>"))
                        rejectionExamples.Add(new QuotaRejection { region = region, triangle = anchor.triangle, a = anchor.a, b = anchor.b, c = anchor.c,
                            point = anchor.point, barycentric = anchor.bary, frame = "<basis>", frameIndex = -1, basisGap = gap, frameGap = gap });
                }
                if (clear) foreach (var frame in frames)
                {
                    Vector3 delta = toActor.MultiplyVector(anchor.Delta(frame));
                    float frameGap = clump.Min(v => frame.mouth.Distance(v + delta));
                    gap = Mathf.Min(gap, frameGap); if (frameGap < 1.015f)
                    {
                        morphRejected[region]++; rejectedFrames[region].TryGetValue(frame, out int rejected); rejectedFrames[region][frame] = rejected + 1;
                        if (!rejectionExamples.Any(e => e.region == region && e.frame != "<basis>"))
                            rejectionExamples.Add(new QuotaRejection { region = region, triangle = anchor.triangle, a = anchor.a, b = anchor.b, c = anchor.c,
                                point = anchor.point, barycentric = anchor.bary, frame = frame.name, frameIndex = frames.IndexOf(frame),
                                basisGap = clump.Min(mouth.Distance), frameGap = frameGap, frameDelta = delta, frameMouth = frame.mouth.centre,
                                frameHalfWidth = frame.mouth.halfWidth, frameHalfHeight = frame.mouth.halfHeight });
                        clear = false; break;
                    }
                }
                if (!clear) continue;
                int first = generated.Count;
                AppendClump(clump, fromActor, toActor.transpose, generated, normals, tangents, uv, triangles);
                for (int i = 0; i < VerticesPerClump; i++) outputAnchors.Add(anchor);
                Need(generated.Count - first == VerticesPerClump, "A closed clump must retain its fixed topology.");
                selected.Add(anchor); counts[region]++; minimumGap = Mathf.Min(minimumGap, gap);
                reports.Add(new AnchorReport { triangle = anchor.triangle, a = anchor.a, b = anchor.b, c = anchor.c,
                    region = region, barycentric = anchor.bary, actorPosition = anchor.point, exactSharedPalette = anchor.shared,
                    influences = anchor.influences.Length, length = length, radius = radius, minimumSingleFrameLipDistance = gap });
            }
            string quotaDiagnostic = "";
            if (!(selected.Count >= 12 && counts[0] >= 6 && counts[1] >= 4 && counts[2] >= 2 && generated.Count <= 5000))
            {
                var regionReports = Enumerable.Range(0, 3).Select(region =>
                {
                    var actual = candidates.Where(a => a.region == region).ToArray(); var bounds = new Bounds(actual.Length > 0 ? actual[0].point : Vector3.zero, Vector3.zero);
                    foreach (var anchor in actual) bounds.Encapsulate(anchor.point);
                    return new QuotaRegion { region = region, candidates = actual.Length, examined = examined[region], accepted = counts[region], quota = quotas[region],
                        precisionRejected = precisionRejected[region], spacingRejected = spacingRejected[region], basisLipRejected = basisRejected[region], morphLipRejected = morphRejected[region],
                        spacing = (region == 2 ? .0018f : .0032f) * scale, candidateBounds = bounds, hasCandidates = actual.Length > 0,
                        quotaReached = counts[region] >= quotas[region], globalLimitReached = selected.Count >= MaximumClumps,
                        minimumNormalizedAbsX = actual.Length > 0 ? actual.Min(a => Mathf.Abs(a.point.x - mouth.centre.x) / scale) : 0,
                        maximumNormalizedAbsX = actual.Length > 0 ? actual.Max(a => Mathf.Abs(a.point.x - mouth.centre.x) / scale) : 0,
                        minimumNormalizedMouthY = actual.Length > 0 ? actual.Min(a => (a.point.y - mouth.centre.y) / scale) : 0,
                        maximumNormalizedMouthY = actual.Length > 0 ? actual.Max(a => (a.point.y - mouth.centre.y) / scale) : 0 };
                }).ToArray();
                var frameReports = Enumerable.Range(0, 3).SelectMany(region => rejectedFrames[region].Select(pair => new QuotaFrame
                    { region = region, frameIndex = frames.IndexOf(pair.Key), name = pair.Key.name, weight = pair.Key.weight, rejected = pair.Value,
                        mouth = pair.Key.mouth.centre, halfWidth = pair.Key.mouth.halfWidth, halfHeight = pair.Key.mouth.halfHeight }))
                    .OrderByDescending(row => row.rejected).ThenBy(row => row.region).ThenBy(row => row.name, StringComparer.Ordinal).Take(16).ToArray();
                quotaDiagnostic = "\nFIRSTGAME_BEARD_QUOTA_DIAGNOSTIC " + JsonUtility.ToJson(new QuotaDiagnostic
                {
                    actorId = actorId, sourceFaceAsset = AssetDatabase.GetAssetPath(source), sourceFaceHash = originalFingerprint, jawFloorBasis = JawFloorBasis,
                    selected = selected.Count, vertices = generated.Count, maximumClumps = MaximumClumps, minimumSelected = 12, minimumJaw = 6, minimumCheek = 4,
                    minimumMoustache = 2, maximumVertices = 5000, sourceMorphFrames = frames.Count, eyeSpan = eyeSpan, scale = scale, jawFloor = jawFloor,
                    basisLipMinimum = 1.035f, morphLipMinimum = 1.015f, minimumNonzeroAnchorDelta = MinimumNonzeroAnchorDelta,
                    mouth = mouth.centre, mouthHalfWidth = mouth.halfWidth, mouthHalfHeight = mouth.halfHeight,
                    chin = chin, eye = eye, head = headPoint, neck = neckPoint, regions = regionReports, firstRejectingFrames = frameReports, examples = rejectionExamples.ToArray()
                });
                if (actorId == "Joseph" || actorId == "Teacher")
                    quotaDiagnostic += "\nFIRSTGAME_BEARD_UPPERLIP_DIAGNOSTIC " + UpperLipDiagnosticFor(actorId, source, originalFingerprint,
                        skinTriangles, actorPoints, actorNormals, palettes, mouth, eye, scale, frames, toActor);
            }
            Need(selected.Count >= 12 && counts[0] >= 6 && counts[1] >= 4 && counts[2] >= 2 && generated.Count <= 5000,
                "The bounded design did not produce jaw, cheek and moustache clumps; inspect source landmarks instead of weakening masks." + quotaDiagnostic);
            var mesh = new Mesh { name = name };
            mesh.SetVertices(generated); mesh.SetNormals(normals); mesh.SetTangents(tangents); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.bindposes = source.bindposes;
            AssignPalettes(mesh, outputAnchors);
            VerifyOutputPalettes(mesh, outputAnchors);
            float immediateMorphReadbackMaximumError = 0;
            foreach (var frame in frames)
            {
                var delta = new Vector3[generated.Count];
                for (int i = 0; i < delta.Length; i++) delta[i] = outputAnchors[i].Delta(frame);
                mesh.AddBlendShapeFrame(frame.name, frame.weight, delta, new Vector3[delta.Length], new Vector3[delta.Length]);
                immediateMorphReadbackMaximumError = Mathf.Max(immediateMorphReadbackMaximumError,
                    VerifyMorphFrame(mesh, frame, outputAnchors, "Immediately after AddBlendShapeFrame"));
            }
            mesh.RecalculateBounds(); Bounds allMorphBounds = mesh.bounds;
            foreach (var frame in frames) for (int i = 0; i < generated.Count; i++) allMorphBounds.Encapsulate(generated[i] + outputAnchors[i].Delta(frame));
            string meshPath = outputRoot.TrimEnd('/') + "/" + actorId + "/" + name + ".asset";
            EnsureFolder(Path.GetDirectoryName(meshPath).Replace('\\', '/'));
            string fingerprint = Fingerprint(mesh); var existing = AssetDatabase.LoadMainAssetAtPath(meshPath);
            if (existing)
            {
                Need(existing is Mesh && Fingerprint((Mesh)existing) == fingerprint, "Preserve a differing prior derived beard; select a new versioned output root.");
                UnityEngine.Object.DestroyImmediate(mesh); mesh = (Mesh)existing;
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, meshPath); AssetDatabase.SaveAssetIfDirty(mesh);
                Need(Fingerprint(mesh) == fingerprint, "Persisted beard payload differs from the complete candidate.");
            }
            AssetDatabase.ImportAsset(meshPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var persisted = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            Need(persisted && Fingerprint(persisted) == fingerprint, "Reimported beard payload differs from the complete candidate.");
            mesh = persisted;
            Need(mesh.blendShapeCount == source.blendShapeCount, "The persisted beard must retain every source morph shape.");
            for (int shape = 0; shape < source.blendShapeCount; shape++)
                Need(mesh.GetBlendShapeFrameCount(shape) == source.GetBlendShapeFrameCount(shape), "The persisted beard must retain every source morph frame.");
            float persistedMorphReadbackMaximumError = 0;
            foreach (var frame in frames) persistedMorphReadbackMaximumError = Mathf.Max(persistedMorphReadbackMaximumError,
                VerifyMorphFrame(mesh, frame, outputAnchors, "After persistent asset reimport"));
            var obj = new GameObject(name); obj.transform.SetParent(face.transform, false); obj.layer = face.gameObject.layer;
            var beard = obj.AddComponent<SkinnedMeshRenderer>(); beard.sharedMesh = mesh; beard.sharedMaterial = beardMaterial;
            beard.bones = (Transform[])face.bones.Clone(); beard.rootBone = face.rootBone; beard.quality = face.quality;
            beard.localBounds = allMorphBounds; beard.updateWhenOffscreen = face.updateWhenOffscreen;
            beard.shadowCastingMode = face.shadowCastingMode; beard.receiveShadows = face.receiveShadows;
            for (int shape = 0; shape < defaults.Length; shape++) beard.SetBlendShapeWeight(shape, defaults[shape]);
            Need(face.sharedMesh == source && Fingerprint(source) == originalFingerprint && animator.avatar == avatar
                && animator.runtimeAnimatorController == controller && defaults.SequenceEqual(Enumerable.Range(0, source.blendShapeCount).Select(face.GetBlendShapeWeight)),
                "Beard authoring must leave the selected source face, rig/controller and actual morph defaults unchanged.");
            Need(beard.bones.SequenceEqual(face.bones) && beard.rootBone == face.rootBone && beard.sharedMesh.bindposes.SequenceEqual(source.bindposes),
                "The actual source face palette, root and bindposes must be retained exactly.");
            Bounds actorBounds = new Bounds(selected[0].point, Vector3.zero);
            foreach (var v in generated) actorBounds.Encapsulate(toActor.MultiplyPoint3x4(v));
            var report = new Report { actorId = actorId, renderer = name, meshAsset = meshPath, sourceFaceAsset = AssetDatabase.GetAssetPath(source),
                sourceFacePayloadSha256 = originalFingerprint, beardPayloadSha256 = fingerprint, materialAsset = AssetDatabase.GetAssetPath(beardMaterial),
                sourceSkinTriangles = skinTriangles.Count / 3, candidates = candidates.Count, clumps = selected.Count,
                jawClumps = counts[0], cheekClumps = counts[1], moustacheClumps = counts[2], vertices = mesh.vertexCount, triangles = triangles.Count / 3,
                sourceMorphs = source.blendShapeCount, sourceMorphFrames = frames.Count, eyeSpan = eyeSpan, actorScale = scale,
                mouth = mouth.centre, chin = chin, actorBounds = actorBounds, minimumSingleFrameLipDistance = minimumGap,
                jawFloorBasis = JawFloorBasis, jawFloor = jawFloor, chinReferenceCandidates = chinCandidates.Length, observedHead = headPoint, observedNeck = neckPoint,
                mixedPaletteVertexAnchors = selected.Count(a => !a.shared), barycentricSharedPaletteAnchors = selected.Count(a => a.shared),
                maximumInfluences = selected.Max(a => a.influences.Length), sourceFaceUnchanged = true, originalRigUnchanged = true,
                precisionRejectedByRegion = precisionRejected, minimumNonzeroAnchorDelta = MinimumNonzeroAnchorDelta, morphReadbackTolerance = MorphReadbackTolerance,
                immediateMorphReadbackMaximumError = immediateMorphReadbackMaximumError, persistedMorphReadbackMaximumError = persistedMorphReadbackMaximumError,
                allSourceMorphFramesCopied = true, sourceNeutralDefaultsCopied = true, nativeMotionAndVisualReviewCompleted = false,
                scope = "Closed project-authored tapered 3D clumps anchored to actual facial skin. Exact shared-palette barycentric or original-vertex anchors; all source morph frames retained with strict immediate and reimported delta read-back. Candidates with native sparse-storage-unsafe tiny nonzero source anchor motion are rejected, never rounded, amplified or replaced with fabricated channel data. Basis and each individual authored morph frame pass the modeled-mouth exclusion. Combined expressions and animated clearance require actual Root review.",
                materialUvRequirement = "New clump UV0 uses circumference/length. Supply solid or tileable beard fiber albedo; do not reuse an unrelated hair atlas as if its UVs matched.",
                expressionBindingRequirement = "Add source-bound ih/oh/relaxed/sad/happy catalog rows for this unique beard renderer. Never duplicate the face renderer name; the current runtime requires each catalog row to resolve exactly once.",
                anchors = reports.ToArray() };
            if (actorId == "Joseph")
                report.scope += " Joseph moustache uses actual lateral upper-lip skin at mouth-relative Y .021-.025 and absolute X .020-.027 eye-scale units, no farther forward than mouth Z+.004 scale; validated up/out direction with the same complete authored-frame exclusion.";
            if (actorId == "Teacher")
                report.scope += " Teacher moustache uses actual lateral upper-lip skin at mouth-relative Y .021-.025 and absolute X .020-.027 eye-scale units, no farther forward than mouth Z+.004 scale; validated up/out direction at the retained Teacher length with the same complete authored-frame exclusion.";
            WriteAnchorSidecar(meshPath, report);
            return report;
        }

        static void WriteAnchorSidecar(string meshPath, Report report)
        {
            string assetPath = Path.ChangeExtension(meshPath, ".anchors.json").Replace('\\', '/');
            Need(assetPath.StartsWith(NewAssetRoot, StringComparison.Ordinal) && !assetPath.Contains(".."),
                "Anchor provenance must stay beside the new versioned beard mesh.");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string path = Path.GetFullPath(Path.Combine(project, assetPath));
            string allowed = Path.GetFullPath(Path.Combine(project, NewAssetRoot));
            Need(path.StartsWith(allowed, StringComparison.Ordinal), "Anchor provenance must remain inside character-art assets.");
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(report, true) + "\n");
            if (File.Exists(path))
            {
                Need((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0 && File.ReadAllBytes(path).SequenceEqual(bytes),
                    "Preserve differing prior anchor provenance; choose another reviewed output version.");
            }
            else
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            Need(File.ReadAllBytes(path).SequenceEqual(bytes), "Persistent beard anchor provenance differs from its actual report.");
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            Need(text && text.bytes.SequenceEqual(bytes), "Imported anchor provenance must retain the exact authored JSON bytes.");
        }

        static Mouth MouthAt(Vector3[] actorBasis, int[] oral, Vector3[] delta, Matrix4x4 toActor, float scale)
        {
            Vector3 At(int i) => actorBasis[i] + (delta == null ? Vector3.zero : toActor.MultiplyVector(delta[i]));
            float front = oral.Max(i => At(i).z);
            var exposed = oral.Select(At).Where(v => v.z >= front - .012f * scale).ToArray();
            Need(exposed.Length >= 3, "Actual front modeled-mouth vertices are required for lip clearance.");
            float xmin = exposed.Min(v => v.x), xmax = exposed.Max(v => v.x), ymin = exposed.Min(v => v.y), ymax = exposed.Max(v => v.y);
            Need(xmax - xmin > .008f * scale && xmax - xmin < .10f * scale && ymax - ymin < .12f * scale,
                "Modeled-mouth extents exceed the bounded facial design.");
            return new Mouth { centre = new Vector3((xmin + xmax) * .5f, (ymin + ymax) * .5f, front),
                halfWidth = (xmax - xmin) * .5f + .0035f * scale, halfHeight = (ymax - ymin) * .5f + .004f * scale };
        }

        static List<Frame> ReadFrames(Mesh source, Vector3[] actorPoints, int[] oral, Matrix4x4 toActor, float scale)
        {
            var result = new List<Frame>();
            for (int shape = 0; shape < source.blendShapeCount; shape++) for (int frame = 0; frame < source.GetBlendShapeFrameCount(shape); frame++)
            {
                var delta = new Vector3[source.vertexCount]; source.GetBlendShapeFrameVertices(shape, frame, delta, new Vector3[delta.Length], new Vector3[delta.Length]);
                Need(delta.All(Finite), "Every actual facial morph displacement must be finite.");
                float weight = source.GetBlendShapeFrameWeight(shape, frame);
                Need(Finite(weight), "Every actual facial morph frame weight must remain finite.");
                result.Add(new Frame { name = source.GetBlendShapeName(shape), shape = shape, index = frame, weight = weight,
                    delta = delta, mouth = MouthAt(actorPoints, oral, delta, toActor, scale) });
            }
            Need(result.Count > 0, "Actual retained facial morph frames are required."); return result;
        }

        static bool PrecisionSafe(Anchor anchor, List<Frame> frames)
        {
            foreach (var frame in frames)
            {
                Vector3 delta = anchor.Delta(frame);
                Need(Finite(delta), "Actual source anchor morph displacements must remain finite.");
                // Vector3 == is approximate: legitimate component-exact zero frames
                // must be distinguished from nonzero motion native storage can drop.
                if (delta.x == 0f && delta.y == 0f && delta.z == 0f) continue;
                if (delta.sqrMagnitude < MinimumNonzeroAnchorDelta * MinimumNonzeroAnchorDelta) return false;
            }
            return true;
        }

        static float VerifyMorphFrame(Mesh mesh, Frame frame, List<Anchor> anchors, string stage)
        {
            Need(mesh.vertexCount == anchors.Count && frame.shape < mesh.blendShapeCount
                && mesh.GetBlendShapeName(frame.shape) == frame.name
                && frame.index < mesh.GetBlendShapeFrameCount(frame.shape)
                && mesh.GetBlendShapeFrameWeight(frame.shape, frame.index).Equals(frame.weight),
                stage + ": exact source morph name, frame index, weight and vertex count must round-trip.");
            var actual = new Vector3[mesh.vertexCount];
            mesh.GetBlendShapeFrameVertices(frame.shape, frame.index, actual, null, null);
            float maximum = 0;
            for (int i = 0; i < actual.Length; i++)
            {
                Vector3 expected = anchors[i].Delta(frame); float error = Vector3.Distance(actual[i], expected);
                if (!(error < MorphReadbackTolerance))
                    Need(false, stage + ": beard morph delta differs from its actual source anchor at "
                        + frame.name + " frame " + frame.index + " vertex " + i + "; error=" + error.ToString("R")
                        + ", expected=" + expected.ToString("R") + ", actual=" + actual[i].ToString("R") + ".");
                maximum = Mathf.Max(maximum, error);
            }
            return maximum;
        }

        static string UpperLipDiagnosticFor(string actorId, Mesh source, string sourceHash, List<int> skin, Vector3[] points, Vector3[] normals,
            BoneWeight1[][] palettes, Mouth mouth, Vector3 eye, float scale, List<Frame> frames, Matrix4x4 toActor)
        {
            var d = new UpperLipDiagnostic { actorId = actorId, sourceFaceAsset = AssetDatabase.GetAssetPath(source), sourceFaceHash = sourceHash,
                mouth = mouth.centre, eye = eye, scale = scale, mouthHalfWidth = mouth.halfWidth, mouthHalfHeight = mouth.halfHeight,
                oldMaximumY = mouth.centre.y + .020f * scale, probeMinimumY = mouth.centre.y + .005f * scale,
                probeMaximumY = Mathf.Min(eye.y - .020f * scale, mouth.centre.y + .060f * scale), minimumAbsX = .004f * scale,
                maximumAbsX = mouth.halfWidth + .002f * scale, minimumZ = mouth.centre.z - .050f * scale, minimumNormalZ = .08f,
                actualFrames = frames.Count, safeByVariant = new int[3] };
            try
            {
                var anterior = skin.Distinct().Where(i => points[i].y > mouth.centre.y + .005f * scale && points[i].y < d.probeMaximumY
                    && Mathf.Abs(points[i].x - mouth.centre.x) < .018f * scale && points[i].z > mouth.centre.z + .003f * scale && normals[i].z > .08f).ToArray();
                d.anteriorCentralVertices = anterior.Length; d.hasAnteriorCentralVertices = anterior.Length > 0;
                if (anterior.Length > 0)
                { d.anteriorCentralBounds = new Bounds(points[anterior[0]], Vector3.zero); foreach (int i in anterior) d.anteriorCentralBounds.Encapsulate(points[i]); }
                d.anteriorCentralRecords = anterior.OrderBy(i => points[i].y).ThenBy(i => Mathf.Abs(points[i].x - mouth.centre.x)).ThenBy(i => i)
                    .Take(16).Select(i => new UpperFaceVertex { vertex = i, point = points[i], normal = normals[i] }).ToArray();
                var anchors = new List<Anchor>();
                for (int t = 0; t < skin.Count && anchors.Count < 4096; t += 3)
                {
                    int a = skin[t], b = skin[t + 1], c = skin[t + 2];
                    if (Mathf.Min(points[a].y, Mathf.Min(points[b].y, points[c].y)) > d.probeMaximumY
                        || Mathf.Max(points[a].y, Mathf.Max(points[b].y, points[c].y)) < d.probeMinimumY) continue;
                    bool same = SamePalette(palettes[a], palettes[b]) && SamePalette(palettes[a], palettes[c]);
                    float edge = Mathf.Max(Vector3.Distance(points[a], points[b]), Mathf.Max(Vector3.Distance(points[b], points[c]), Vector3.Distance(points[c], points[a])));
                    int steps = same ? Mathf.Clamp(Mathf.CeilToInt(edge / (.0036f * scale)), 2, 12) : 1;
                    for (int i = 0; i <= steps && anchors.Count < 4096; i++) for (int j = 0; j <= steps - i && anchors.Count < 4096; j++)
                    {
                        var bary = new Vector3((float)i / steps, (float)j / steps, (float)(steps - i - j) / steps);
                        int vertex = bary.x == 1 ? a : bary.y == 1 ? b : c;
                        var p = points[a] * bary.x + points[b] * bary.y + points[c] * bary.z;
                        var n = (normals[a] * bary.x + normals[b] * bary.y + normals[c] * bary.z).normalized; float dx = Mathf.Abs(p.x - mouth.centre.x);
                        if (p.y < d.probeMinimumY || p.y > d.probeMaximumY || dx <= d.minimumAbsX || dx > d.maximumAbsX
                            || p.z < d.minimumZ || n.z < d.minimumNormalZ || mouth.Distance(p) < 1.12f) continue;
                        anchors.Add(new Anchor { triangle = t / 3, a = a, b = b, c = c, bary = bary, point = p, normal = n, region = 2,
                            shared = same, influences = same ? palettes[a] : palettes[vertex],
                            key = Hash((uint)(t + 1) * 73856093u ^ (uint)(i + 1) * 19349663u ^ (uint)(j + 1) * 83492791u) });
                    }
                }
                d.candidates = anchors.Count; d.probeTruncated = anchors.Count >= 4096; var rows = new List<UpperLipCandidate>();
                foreach (var anchor in anchors)
                {
                    float random = (anchor.key & 65535) / 65535f;
                    float length = Mathf.Clamp(Mathf.Lerp(.003f, .005f, random) * (actorId == "Teacher" ? 1.18f : .82f), .002f, .018f) * scale;
                    float radius = Mathf.Lerp(.00055f, .0009f, random) * scale;
                    var variants = new UpperLipVariant[3];
                    for (int variant = 0; variant < 3; variant++)
                    {
                        var clump = UpperLipProbeClump(anchor, mouth.centre.x, length, radius, scale, variant);
                        var row = new UpperLipVariant { direction = variant == 0 ? "original" : variant == 1 ? "up_and_out" : "lateral_and_out",
                            basisGap = float.MaxValue, minimumFrameGap = float.MaxValue, worstFrameIndex = -1, checkedFrames = frames.Count };
                        for (int v = 0; v < clump.Length; v++)
                        { float gap = mouth.Distance(clump[v]); if (gap < row.basisGap) { row.basisGap = gap; row.basisMinimumVertex = v; } }
                        for (int f = 0; f < frames.Count; f++)
                        {
                            var frame = frames[f]; Vector3 delta = toActor.MultiplyVector(anchor.Delta(frame));
                            for (int v = 0; v < clump.Length; v++)
                            {
                                float gap = frame.mouth.Distance(clump[v] + delta);
                                if (gap >= row.minimumFrameGap) continue;
                                row.minimumFrameGap = gap; row.worstFrameIndex = f; row.worstFrame = frame.name; row.worstFrameWeight = frame.weight;
                                row.worstFrameMouth = frame.mouth.centre; row.worstFrameHalfWidth = frame.mouth.halfWidth; row.worstFrameHalfHeight = frame.mouth.halfHeight;
                                row.worstFrameAnchorDelta = delta; row.worstFrameMinimumVertex = v;
                            }
                        }
                        row.minimumMargin = Mathf.Min(row.basisGap - 1.035f, row.minimumFrameGap - 1.015f);
                        row.allFramesClear = row.basisGap >= 1.035f && row.minimumFrameGap >= 1.015f;
                        if (row.allFramesClear) d.safeByVariant[variant]++; variants[variant] = row;
                    }
                    rows.Add(new UpperLipCandidate { triangle = anchor.triangle, a = anchor.a, b = anchor.b, c = anchor.c, point = anchor.point, normal = anchor.normal,
                        barycentric = anchor.bary, influences = anchor.influences.Length, exactSharedPalette = anchor.shared,
                        normalizedAbsX = Mathf.Abs(anchor.point.x - mouth.centre.x) / scale, normalizedMouthY = (anchor.point.y - mouth.centre.y) / scale, variants = variants });
                }
                var safe = rows.Where(row => row.variants.Any(v => v.allFramesClear)).ToArray();
                d.safeCandidates = safe.Length; d.hasSafeCandidates = safe.Length > 0; d.safeLeft = safe.Count(row => row.point.x < mouth.centre.x); d.safeRight = safe.Length - d.safeLeft;
                if (safe.Length > 0) { d.safeBounds = new Bounds(safe[0].point, Vector3.zero); foreach (var row in safe) d.safeBounds.Encapsulate(row.point); }
                d.records = rows.OrderByDescending(row => row.variants.Any(v => v.allFramesClear)).ThenBy(row => row.point.y)
                    .ThenByDescending(row => row.variants.Max(v => v.minimumMargin)).ThenBy(row => row.triangle).Take(32).ToArray();
            }
            catch (Exception error) { string text = error.ToString(); d.diagnosticFailure = text.Length <= 4096 ? text : text.Substring(0, 4096); }
            return JsonUtility.ToJson(d);
        }
        static Vector3[] UpperLipProbeClump(Anchor anchor, float mouthX, float length, float radius, float scale, int variant)
        {
            if (variant == 0) return Clump(anchor, mouthX, length, radius, scale);
            float side = Mathf.Sign(anchor.point.x - mouthX);
            Vector3 direction = (Vector3.right * side * (variant == 1 ? .75f : .85f) + Vector3.up * (variant == 1 ? .28f : 0) + anchor.normal * (variant == 1 ? .42f : .30f)).normalized;
            Vector3 u = Vector3.Cross(direction, Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > .8f ? Vector3.right : Vector3.up).normalized;
            Vector3 v = Vector3.Cross(direction, u).normalized, start = anchor.point + anchor.normal * (.0012f * scale);
            var result = new Vector3[VerticesPerClump]; result[0] = start; var times = new[] { 0f, .32f, .72f }; var radii = new[] { .28f, 1f, .54f };
            for (int ring = 0; ring < 3; ring++)
            {
                float t = times[ring]; Vector3 centre = start + direction * (length * t) + anchor.normal * (length * .16f * Mathf.Sin(Mathf.PI * t));
                for (int s = 0; s < Sides; s++) result[1 + ring * Sides + s] = centre + radius * radii[ring] * (u * Mathf.Cos(s * Mathf.PI * 2 / Sides) + v * Mathf.Sin(s * Mathf.PI * 2 / Sides));
            }
            result[19] = start + direction * length; return result;
        }

        static void LogChinDiagnostic(GameObject actor, SkinnedMeshRenderer face, string actorId, string sourceHash,
            Vector3[] points, Vector3[] normals, List<int> skinTriangles, int[] oral, Vector3 head, Vector3 neck,
            Vector3 eye, float eyeSpan, float scale, Mouth mouth, float jawFloor, Matrix4x4 toActor, Matrix4x4 normalToActor)
        {
            var d = new ChinDiagnostic { actorId = actorId, sourceFaceAsset = AssetDatabase.GetAssetPath(face.sharedMesh), sourceFaceHash = sourceHash,
                renderer = face.name, rendererPath = AnimationUtility.CalculateTransformPath(face.transform, actor.transform),
                rootBone = face.rootBone ? AnimationUtility.CalculateTransformPath(face.rootBone, actor.transform) : "<null>",
                quality = face.quality.ToString(), globalSkinWeights = QualitySettings.skinWeights.ToString(), matrixLayout = "row-major 4x4",
                headPoint = head, neckPoint = neck, eye = eye, eyeSpan = eyeSpan, scale = scale,
                mouthCentre = mouth.centre, mouthHalfWidth = mouth.halfWidth, mouthHalfHeight = mouth.halfHeight,
                jawFloor = jawFloor, headJawFloor = head.y - .025f * scale, neckJawFloor = neck.y + .055f * scale, jawFloorBasis = JawFloorBasis,
                maximumAbsX = .034f * scale, maximumY = mouth.centre.y - .012f * scale,
                minimumZ = mouth.centre.z - .028f * scale, minimumNormalZ = .12f,
                rawToActorDeterminant = toActor.determinant,
                rendererLocalPosition = face.transform.localPosition, rendererLocalRotation = face.transform.localRotation,
                rendererLocalScale = face.transform.localScale, rendererLossyScale = face.transform.lossyScale,
                rawToActor = MatrixValues(toActor), normalToActor = MatrixValues(normalToActor) };
            try
            {
                var source = face.sharedMesh; var raw = source.vertices; var rawNormals = source.normals;
                int[] skin = skinTriangles.Distinct().OrderBy(i => i).ToArray();
                Bounds BoundsOf(IEnumerable<int> ids) { var values = ids.ToArray(); var bounds = new Bounds(points[values[0]], Vector3.zero); foreach (int i in values) bounds.Encapsulate(points[i]); return bounds; }
                d.sourceSkinBounds = BoundsOf(skin); d.modeledMouthBounds = BoundsOf(oral);
                d.sourceSkinTriangles = skinTriangles.Count / 3; d.sourceSkinReferencedVertices = skin.Length; d.modeledMouthVertices = oral.Length;
                ChinVertex Sample(int i)
                {
                    Vector3 p = points[i], n = normals[i];
                    var v = new ChinVertex { vertex = i, actorPoint = p, actorNormal = n, rawPoint = raw[i], rawNormal = rawNormals[i],
                        x = Mathf.Abs(p.x - mouth.centre.x) < .034f * scale, lower = p.y > jawFloor,
                        upper = p.y < mouth.centre.y - .012f * scale, depth = p.z > mouth.centre.z - .028f * scale,
                        normFront = n.z > .12f, aboveActualNeck = p.y > neck.y, belowMouth = p.y < mouth.centre.y,
                        aboveHeadFloor = p.y > head.y - .025f * scale, aboveNeckFloor = p.y > neck.y + .055f * scale };
                    v.all = v.x && v.lower && v.upper && v.depth && v.normFront; return v;
                }
                var all = skin.Select(Sample).ToArray();
                d.counts = new ChinPredicateCounts { total = all.Length, x = all.Count(v => v.x), lower = all.Count(v => v.lower),
                    upper = all.Count(v => v.upper), depth = all.Count(v => v.depth), normFront = all.Count(v => v.normFront),
                    aboveActualNeck = all.Count(v => v.aboveActualNeck), belowMouth = all.Count(v => v.belowMouth),
                    aboveHeadFloor = all.Count(v => v.aboveHeadFloor), aboveNeckFloor = all.Count(v => v.aboveNeckFloor),
                    xThenLower = all.Count(v => v.x && v.lower), xThenLowerThenUpper = all.Count(v => v.x && v.lower && v.upper),
                    xThenLowerThenUpperThenDepth = all.Count(v => v.x && v.lower && v.upper && v.depth), all = all.Count(v => v.all),
                    exceptX = all.Count(v => v.lower && v.upper && v.depth && v.normFront), exceptLower = all.Count(v => v.x && v.upper && v.depth && v.normFront),
                    exceptUpper = all.Count(v => v.x && v.lower && v.depth && v.normFront), exceptDepth = all.Count(v => v.x && v.lower && v.upper && v.normFront),
                    exceptNormal = all.Count(v => v.x && v.lower && v.upper && v.depth) };
                Vector3 target = mouth.centre + new Vector3(0, -.04f * scale, -.015f * scale);
                d.representativeVertices = all.OrderBy(v => v.belowMouth && Mathf.Abs(v.actorPoint.x - mouth.centre.x) < .065f * scale ? 0 : 1)
                    .ThenBy(v => (v.x ? 0 : 1) + (v.lower ? 0 : 1) + (v.upper ? 0 : 1) + (v.depth ? 0 : 1) + (v.normFront ? 0 : 1))
                    .ThenBy(v => Vector3.SqrMagnitude(v.actorPoint - target)).ThenBy(v => v.vertex).Take(64).ToArray();
                // This diagnostic deliberately ignores the head-derived floor:
                // it can expose real facial points rejected only by that bound.
                d.nearestLowerAnteriorAboveActualNeck = all.Where(v => v.aboveActualNeck && v.belowMouth && v.x && v.depth && v.normFront)
                    .OrderBy(v => Vector3.SqrMagnitude(v.actorPoint - target)).ThenBy(v => v.vertex).Take(16).ToArray();
                var chosen = d.representativeVertices.Concat(d.nearestLowerAnteriorAboveActualNeck).Distinct().ToArray();
                var bones = face.bones; var bind = source.bindposes; d.nativeBones = bones.Length;
                var palette = bones.Select((bone, i) => bone ? actor.transform.worldToLocalMatrix * bone.localToWorldMatrix * bind[i] : Matrix4x4.zero).ToArray();
                var usedBones = new HashSet<int>();
                using (var counts = source.GetBonesPerVertex()) using (var weights = source.GetAllBoneWeights())
                {
                    var offsets = new int[counts.Length + 1]; for (int i = 0; i < counts.Length; i++) offsets[i + 1] = offsets[i] + counts[i];
                    foreach (var v in all)
                    {
                        int start = offsets[v.vertex], end = offsets[v.vertex + 1]; v.nativeInfluenceCount = end - start;
                        var influences = new List<ChinInfluence>();
                        for (int i = start; i < end; i++)
                        {
                            var weight = weights[i]; v.nativeWeightSum += weight.weight;
                            if (weight.boneIndex >= 0 && weight.boneIndex < palette.Length)
                            { v.nativeMappedActorPoint += palette[weight.boneIndex].MultiplyPoint3x4(v.rawPoint) * weight.weight; usedBones.Add(weight.boneIndex); }
                            if (influences.Count < 16) influences.Add(new ChinInfluence { boneIndex = weight.boneIndex, weight = weight.weight });
                        }
                        if (chosen.Contains(v)) { v.nativeInfluences = influences.ToArray(); v.nativeInfluencesTruncated = end - start > influences.Count; }
                        v.rawToNativeDistance = Vector3.Distance(v.actorPoint, v.nativeMappedActorPoint);
                        if (v.rawToNativeDistance > d.maximumSkinRawToNativeDistance)
                        { d.maximumSkinRawToNativeDistance = v.rawToNativeDistance; d.maximumSkinRawToNativeVertex = v.vertex; }
                    }
                }
                d.nativeFrames = usedBones.OrderBy(i => i).Take(16).Select(i =>
                {
                    Matrix4x4 rest = bones[i] ? face.transform.worldToLocalMatrix * bones[i].localToWorldMatrix * bind[i] : Matrix4x4.zero;
                    float error = 0; for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) error = Mathf.Max(error, Mathf.Abs(rest[r, c] - (r == c ? 1 : 0)));
                    return new ChinNativeFrame { boneIndex = i, path = bones[i] ? AnimationUtility.CalculateTransformPath(bones[i], actor.transform) : "<null>",
                        maximumFaceRestIdentityError = error, nativeToActor = MatrixValues(palette[i]), faceRest = MatrixValues(rest) };
                }).ToArray();
                d.nativeFramesReported = d.nativeFrames.Length;
            }
            catch (Exception error) { string text = error.ToString(); d.diagnosticFailure = text.Length <= 4096 ? text : text.Substring(0, 4096); }
            Debug.Log("FIRSTGAME_BEARD_CHIN_DIAGNOSTIC " + JsonUtility.ToJson(d, true));
        }
        static float[] MatrixValues(Matrix4x4 matrix)
        { var values = new float[16]; for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) values[r * 4 + c] = matrix[r, c]; return values; }

        static BoneWeight1[][] ReadPalettes(Mesh mesh, int bones)
        {
            var result = new BoneWeight1[mesh.vertexCount][];
            using (var counts = mesh.GetBonesPerVertex()) using (var all = mesh.GetAllBoneWeights())
            {
                int offset = 0;
                for (int i = 0; i < counts.Length; i++)
                {
                    result[i] = new BoneWeight1[counts[i]]; float total = 0;
                    for (int j = 0; j < counts[i]; j++)
                    {
                        var w = all[offset++]; Need(w.boneIndex >= 0 && w.boneIndex < bones && Finite(w.weight) && w.weight > 0, "Every actual native skin influence must be valid.");
                        result[i][j] = w; total += w.weight;
                    }
                    Need(result[i].Length > 0 && Mathf.Abs(total - 1) < .0002f, "Every actual facial vertex needs its complete normalized source palette.");
                }
                Need(offset == all.Length, "Native source influence count differs.");
            }
            return result;
        }

        static bool SamePalette(BoneWeight1[] a, BoneWeight1[] b) => a.Length == b.Length
            && a.Zip(b, (x, y) => x.boneIndex == y.boneIndex && x.weight.Equals(y.weight)).All(v => v);

        static void VerifyBindRest(SkinnedMeshRenderer face, BoneWeight1[][] palettes)
        {
            var used = new HashSet<int>(palettes.SelectMany(v => v).Select(v => v.boneIndex));
            foreach (int bone in used)
            {
                var m = face.transform.worldToLocalMatrix * face.bones[bone].localToWorldMatrix * face.sharedMesh.bindposes[bone];
                float error = 0;
                for (int row = 0; row < 4; row++) for (int col = 0; col < 4; col++) error = Mathf.Max(error, Mathf.Abs(m[row, col] - (row == col ? 1 : 0)));
                Need(error < .005f, "Use the source male bind rest, not an animated face or a rebound palette.");
            }
        }

        static List<Anchor> Candidates(List<int> skin, Vector3[] points, Vector3[] normals, BoneWeight1[][] palettes,
            Mouth mouth, float chin, float eyeY, float scale)
        {
            var result = new List<Anchor>();
            for (int t = 0; t < skin.Count; t += 3)
            {
                int a = skin[t], b = skin[t + 1], c = skin[t + 2];
                if (Mathf.Min(points[a].y, Mathf.Min(points[b].y, points[c].y)) > mouth.centre.y + .020f * scale
                    || Mathf.Max(points[a].y, Mathf.Max(points[b].y, points[c].y)) < chin + .001f * scale) continue;
                bool same = SamePalette(palettes[a], palettes[b]) && SamePalette(palettes[a], palettes[c]);
                float edge = Mathf.Max(Vector3.Distance(points[a], points[b]), Mathf.Max(Vector3.Distance(points[b], points[c]), Vector3.Distance(points[c], points[a])));
                int steps = same ? Mathf.Clamp(Mathf.CeilToInt(edge / (.0036f * scale)), 2, 12) : 1;
                for (int i = 0; i <= steps; i++) for (int j = 0; j <= steps - i; j++)
                {
                    var bary = new Vector3((float)i / steps, (float)j / steps, (float)(steps - i - j) / steps);
                    int vertex = bary.x == 1 ? a : bary.y == 1 ? b : c;
                    var p = points[a] * bary.x + points[b] * bary.y + points[c] * bary.z;
                    var n = (normals[a] * bary.x + normals[b] * bary.y + normals[c] * bary.z).normalized;
                    float dx = Mathf.Abs(p.x - mouth.centre.x), dy = p.y - mouth.centre.y;
                    if (p.y < chin + .001f * scale || p.y > Mathf.Min(eyeY - .020f * scale, mouth.centre.y + .020f * scale)
                        || p.z < mouth.centre.z - .050f * scale || n.z < .08f || dx > .090f * scale || mouth.Distance(p) < 1.12f) continue;
                    int region = p.y < mouth.centre.y - .008f * scale ? 0
                        : dx > mouth.halfWidth + .002f * scale ? 1
                        : dy > .005f * scale && dy < .017f * scale && dx > .004f * scale ? 2 : -1;
                    if (region < 0) continue;
                    result.Add(new Anchor { triangle = t / 3, a = a, b = b, c = c, bary = bary, point = p, normal = n,
                        shared = same, influences = same ? palettes[a] : palettes[vertex], region = region,
                        key = Hash((uint)(t + 1) * 73856093u ^ (uint)(i + 1) * 19349663u ^ (uint)(j + 1) * 83492791u) });
                    Need(result.Count <= 24000, "Candidate budget exceeded; review the new source topology explicitly.");
                }
            }
            return result;
        }

        static List<Anchor> LateralMoustacheCandidates(List<int> skin, Vector3[] points, Vector3[] normals, BoneWeight1[][] palettes,
            Mouth mouth, float eyeY, float scale)
        {
            // Root's actual source/morph probes observed symmetric safe skin
            // within this strip for Joseph and Teacher, at their own lengths.
            // Both projecting central noses were materially farther anterior.
            float lower = mouth.centre.y + .021f * scale;
            float upper = Mathf.Min(eyeY - .020f * scale, mouth.centre.y + .025f * scale);
            var result = new List<Anchor>();
            for (int t = 0; t < skin.Count; t += 3)
            {
                int a = skin[t], b = skin[t + 1], c = skin[t + 2];
                if (Mathf.Min(points[a].y, Mathf.Min(points[b].y, points[c].y)) > upper
                    || Mathf.Max(points[a].y, Mathf.Max(points[b].y, points[c].y)) < lower) continue;
                bool same = SamePalette(palettes[a], palettes[b]) && SamePalette(palettes[a], palettes[c]);
                float edge = Mathf.Max(Vector3.Distance(points[a], points[b]), Mathf.Max(Vector3.Distance(points[b], points[c]), Vector3.Distance(points[c], points[a])));
                int steps = same ? Mathf.Clamp(Mathf.CeilToInt(edge / (.0036f * scale)), 2, 12) : 1;
                for (int i = 0; i <= steps; i++) for (int j = 0; j <= steps - i; j++)
                {
                    var bary = new Vector3((float)i / steps, (float)j / steps, (float)(steps - i - j) / steps);
                    int vertex = bary.x == 1 ? a : bary.y == 1 ? b : c;
                    var p = points[a] * bary.x + points[b] * bary.y + points[c] * bary.z;
                    var n = (normals[a] * bary.x + normals[b] * bary.y + normals[c] * bary.z).normalized;
                    float dx = Mathf.Abs(p.x - mouth.centre.x);
                    if (p.y < lower || p.y > upper || dx < .020f * scale || dx > .027f * scale
                        || p.z < mouth.centre.z - .050f * scale || p.z > mouth.centre.z + .004f * scale
                        || n.z < .08f || mouth.Distance(p) < 1.12f) continue;
                    result.Add(new Anchor { triangle = t / 3, a = a, b = b, c = c, bary = bary, point = p, normal = n,
                        shared = same, influences = same ? palettes[a] : palettes[vertex], region = 2,
                        key = Hash((uint)(t + 1) * 73856093u ^ (uint)(i + 1) * 19349663u ^ (uint)(j + 1) * 83492791u) });
                    Need(result.Count <= 24000, "Candidate budget exceeded; review the new source topology explicitly.");
                }
            }
            return result;
        }

        static Vector3[] Clump(Anchor anchor, float mouthX, float length, float radius, float scale)
        {
            float side = Mathf.Sign(anchor.point.x - mouthX);
            Vector3 direction = anchor.region == 2
                ? (Vector3.right * side * .58f + Vector3.down * .18f + anchor.normal * .55f).normalized
                : (Vector3.down * .90f + Vector3.right * side * .08f + anchor.normal * .58f).normalized;
            Vector3 u = Vector3.Cross(direction, Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > .8f ? Vector3.right : Vector3.up).normalized;
            Vector3 v = Vector3.Cross(direction, u).normalized;
            Vector3 start = anchor.point + anchor.normal * (.0012f * scale);
            var result = new Vector3[VerticesPerClump]; result[0] = start;
            var times = new[] { 0f, .32f, .72f }; var radii = new[] { .28f, 1f, .54f };
            for (int ring = 0; ring < 3; ring++)
            {
                float t = times[ring]; Vector3 centre = start + direction * (length * t) + anchor.normal * (length * .16f * Mathf.Sin(Mathf.PI * t));
                for (int s = 0; s < Sides; s++) result[1 + ring * Sides + s] = centre
                    + radius * radii[ring] * (u * Mathf.Cos(s * Mathf.PI * 2 / Sides) + v * Mathf.Sin(s * Mathf.PI * 2 / Sides));
            }
            result[19] = start + direction * length; return result;
        }

        static void AppendClump(Vector3[] p, Matrix4x4 fromActor, Matrix4x4 normalFromActor, List<Vector3> vertices,
            List<Vector3> normals, List<Vector4> tangents, List<Vector2> uv, List<int> triangles)
        {
            int first = vertices.Count; Vector3 axis = (p[19] - p[0]).normalized;
            for (int i = 0; i < p.Length; i++)
            {
                Vector3 normal;
                if (i == 0) normal = -axis; else if (i == 19) normal = axis;
                else
                {
                    int ring = (i - 1) / Sides; Vector3 centre = Vector3.zero;
                    for (int s = 0; s < Sides; s++) centre += p[1 + ring * Sides + s] / Sides;
                    normal = (p[i] - centre).normalized;
                }
                Vector3 localNormal = normalFromActor.MultiplyVector(normal).normalized;
                Vector3 tangent = fromActor.MultiplyVector(Vector3.Cross(axis, normal)).normalized;
                if (tangent.sqrMagnitude < .1f) tangent = Vector3.Cross(localNormal, Mathf.Abs(localNormal.y) > .8f ? Vector3.right : Vector3.up).normalized;
                tangent = (tangent - localNormal * Vector3.Dot(tangent, localNormal)).normalized;
                vertices.Add(fromActor.MultiplyPoint3x4(p[i])); normals.Add(localNormal); tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, 1));
                int ringIndex = i > 0 && i < 19 ? (i - 1) / Sides : 0;
                float along = ringIndex == 0 ? 0 : ringIndex == 1 ? .32f : .72f;
                uv.Add(i == 0 ? new Vector2(.5f, 0) : i == 19 ? new Vector2(.5f, 1) : new Vector2((i - 1) % Sides / (float)Sides, along));
            }
            void Tri(int a, int b, int c) { triangles.Add(first + a); triangles.Add(first + b); triangles.Add(first + c); }
            for (int s = 0; s < Sides; s++)
            {
                int next = (s + 1) % Sides; Tri(0, 1 + next, 1 + s);
                for (int ring = 0; ring < 2; ring++)
                { int a = 1 + ring * Sides + s, b = 1 + ring * Sides + next; Tri(a, b, a + Sides); Tri(b, b + Sides, a + Sides); }
                Tri(13 + s, 13 + next, 19);
            }
        }

        static void AssignPalettes(Mesh mesh, List<Anchor> anchors)
        {
            var counts = new NativeArray<byte>(anchors.Count, Allocator.Temp);
            var weights = default(NativeArray<BoneWeight1>);
            try
            {
                weights = new NativeArray<BoneWeight1>(anchors.Sum(a => a.influences.Length), Allocator.Temp);
                int offset = 0;
                for (int i = 0; i < anchors.Count; i++)
                {
                    Need(anchors[i].influences.Length <= 255, "A full native source palette cannot be truncated.");
                    counts[i] = (byte)anchors[i].influences.Length;
                    foreach (var weight in anchors[i].influences) weights[offset++] = weight;
                }
                mesh.SetBoneWeights(counts, weights);
            }
            finally
            {
                if (weights.IsCreated) weights.Dispose();
                if (counts.IsCreated) counts.Dispose();
            }
        }

        static void VerifyOutputPalettes(Mesh mesh, List<Anchor> anchors)
        {
            using (var counts = mesh.GetBonesPerVertex()) using (var weights = mesh.GetAllBoneWeights())
            {
                Need(counts.Length == anchors.Count, "Persisted native beard influence counts differ.");
                int offset = 0;
                for (int i = 0; i < counts.Length; i++)
                {
                    Need(counts[i] == anchors[i].influences.Length, "A complete source anchor palette was truncated.");
                    foreach (var expected in anchors[i].influences)
                    {
                        var actual = weights[offset++];
                        Need(actual.boneIndex == expected.boneIndex && actual.weight.Equals(expected.weight),
                            "The new beard skin must round-trip every actual source influence exactly.");
                    }
                }
                Need(offset == weights.Length, "Unexpected native beard influences.");
            }
        }

        static string Fingerprint(Mesh mesh)
        {
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
            {
                writer.Write(mesh.vertexCount); writer.Write(mesh.subMeshCount);
                void Vec(Vector3 v) { writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }
                foreach (var v in mesh.vertices) Vec(v); foreach (var n in mesh.normals) Vec(n);
                foreach (var t in mesh.tangents) { writer.Write(t.x); writer.Write(t.y); writer.Write(t.z); writer.Write(t.w); }
                foreach (var v in mesh.uv) { writer.Write(v.x); writer.Write(v.y); }
                for (int sub = 0; sub < mesh.subMeshCount; sub++) foreach (int i in mesh.GetIndices(sub)) writer.Write(i);
                foreach (var m in mesh.bindposes) for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) writer.Write(m[r, c]);
                using (var counts = mesh.GetBonesPerVertex()) using (var weights = mesh.GetAllBoneWeights())
                { foreach (byte count in counts) writer.Write(count); foreach (var w in weights) { writer.Write(w.boneIndex); writer.Write(w.weight); } }
                writer.Write(mesh.blendShapeCount);
                for (int shape = 0; shape < mesh.blendShapeCount; shape++)
                {
                    writer.Write(mesh.GetBlendShapeName(shape)); writer.Write(mesh.GetBlendShapeFrameCount(shape));
                    for (int frame = 0; frame < mesh.GetBlendShapeFrameCount(shape); frame++)
                    {
                        writer.Write(mesh.GetBlendShapeFrameWeight(shape, frame)); var v = new Vector3[mesh.vertexCount]; var n = new Vector3[v.Length]; var t = new Vector3[v.Length];
                        mesh.GetBlendShapeFrameVertices(shape, frame, v, n, t); foreach (var p in v) Vec(p); foreach (var p in n) Vec(p); foreach (var p in t) Vec(p);
                    }
                }
                writer.Flush(); using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/'); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
        static uint Hash(uint value) { value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15; value *= 0x846ca68bu; return value ^ value >> 16; }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static void Need(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
#endif
