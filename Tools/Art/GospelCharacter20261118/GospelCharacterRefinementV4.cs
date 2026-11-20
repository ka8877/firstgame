#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Firstgame.GospelNpr.Editor
{
    /// <summary>
    /// Editor-only, source-bound character art derivation. Original FBX meshes,
    /// native skin buffers, skeleton transforms, UVs and expression names remain
    /// retained. Root must review real front/side and animated game captures.
    /// </summary>
    public static class GospelCharacterRefinement
    {
        public const string Revision = "gospel-human-2026-11-18-v4-bake-scale";
        public const string OutputRoot = "Assets/Art/GospelCharacter20261118/v1";

        [Serializable] public sealed class MeshReport
        {
            public string renderer, sourceAsset, sourceGuid, sourceHash, derivedAsset, derivedHash;
            public long sourceLocalId;
            public int vertices, triangles, blendShapes, blendShapeFrames;
            public float maximumRestDisplacement, minimumAffineDeterminant;
            public bool topologyUvNativeSkinAndBindposesRetained, sourceUnchanged, expressionFramesLinear, rendererDefaultShapeWeightsRetained;
        }
        [Serializable] public sealed class HairReport
        {
            public string renderer, meshAsset, meshHash;
            public int strands, vertices, triangles;
            public float longestStrand, maximumAnchorGap, maximumScalpSelectionDistance;
            public bool sourcePaletteAndCompleteAnchorWeights, sourceHairRetained;
        }
        [Serializable] public sealed class Report
        {
            public string revision = Revision, id;
            public MeshReport face, hair;
            public HairReport layeredHair;
            public float sourceEyeSpan, sourceFaceHeight, sourceHeadUnits, derivedHeadUnits, headVerticalScale;
            public bool originalSkeletonAndControllerUnchanged, visualApprovalClaimed;
        }

        sealed class Profile
        {
            public float headWidth = 1, headDepth = 1, jawWidth, chinLength, cheekCut;
            public float eyeWidth = 1, eyeHeight = 1, noseProjection, browLower, hairPart;
            public bool child, longHair, retainHeadProportions;
            public static Profile For(string id)
            {
                switch (id)
                {
                    case "JesusChild": return new Profile { child = true, headWidth = .985f,
                        eyeWidth = .97f, eyeHeight = .96f, jawWidth = .018f, cheekCut = .012f, noseProjection = .007f, hairPart = .014f };
                    case "JesusAdult": return new Profile { headWidth = .97f, headDepth = .985f,
                        jawWidth = .085f, chinLength = .030f, cheekCut = .025f, eyeWidth = .94f,
                        eyeHeight = .88f, noseProjection = .034f, browLower = .012f, hairPart = .023f, longHair = true };
                    case "Joseph": return new Profile { headWidth = 1.015f, jawWidth = .115f,
                        chinLength = .022f, cheekCut = .008f, eyeWidth = .96f, eyeHeight = .90f,
                        noseProjection = .041f, browLower = .018f, hairPart = .010f };
                    case "Teacher": return new Profile { headWidth = .985f, jawWidth = .055f,
                        chinLength = .040f, cheekCut = .035f, eyeWidth = .94f, eyeHeight = .84f,
                        noseProjection = .052f, browLower = .022f, hairPart = .009f };
                    case "Mary": return new Profile { headWidth = .99f, headDepth = .99f, retainHeadProportions = true,
                        jawWidth = .018f, chinLength = .008f, cheekCut = .012f,
                        eyeWidth = .97f, eyeHeight = .93f, noseProjection = .012f, browLower = .002f };
                    default: throw new InvalidOperationException("Unknown retained actor identity: " + id);
                }
            }
        }

        sealed class Cage
        {
            public GameObject actor;
            public Animator animator;
            public Profile profile;
            public Vector3 leftEye, rightEye, eye, neck, head;
            public float height, span, faceHeight, mouthY, sourceHeadUnits, headVerticalScale = 1;
            public Vector3[] posedFace, sourceEnvelope;
        }

        public static Report Apply(GameObject actor, string actorId, Material layeredHairMaterial = null)
        {
            Need(!Application.isPlaying && actor, "Character refinement requires a fresh Editor actor instance.");
            var animator = actor.GetComponent<Animator>();
            Need(animator && animator.avatar && animator.avatar.isHuman && animator.avatar.isValid,
                "The retained valid Humanoid Avatar is required.");
            Need(animator.runtimeAnimatorController, "The retained seven-clip motion controller must already be assigned.");
            var renderers = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var face = Exact(renderers, actorId + "__Face");
            var hair = Exact(renderers, actorId + "__Hair");
            Need(!renderers.Any(r => r.name == actorId + "__LayeredHairV1"), "Use a fresh source instance; do not layer this derivation twice.");
            string hierarchy = HierarchyHash(actor), controller = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController);
            var cage = BuildCage(actor, animator, face, hair, Profile.For(actorId));
            var report = new Report { id = actorId, sourceEyeSpan = cage.span, sourceFaceHeight = cage.faceHeight,
                sourceHeadUnits = cage.sourceHeadUnits, headVerticalScale = cage.headVerticalScale };
            report.face = Sculpt(face, cage, actorId, false);
            report.hair = Sculpt(hair, cage, actorId, true);
            var newFace = PosedPoints(actor, face); var newHair = PosedPoints(actor, hair);
            var faceSkin = MaterialIndices(face, n => n.IndexOf("Face_", StringComparison.OrdinalIgnoreCase) >= 0 && n.IndexOf("SKIN", StringComparison.OrdinalIgnoreCase) >= 0);
            report.derivedHeadUnits = cage.height / (Referenced(hair.sharedMesh).Max(i => newHair[i].y) - faceSkin.Min(i => newFace[i].y));
            if (cage.profile.longHair)
            {
                Need(layeredHairMaterial && layeredHairMaterial.shader && layeredHairMaterial.shader.name.Contains("lilToon"),
                    "Adult Jesus layered hair requires an explicit retained NPR hair material.");
                report.layeredHair = BuildLayeredHair(hair, cage, actorId, layeredHairMaterial);
            }
            Need(HierarchyHash(actor, true) == hierarchy && AssetDatabase.GetAssetPath(animator.runtimeAnimatorController) == controller,
                "The original skeleton, transforms and motion controller must remain unchanged.");
            report.originalSkeletonAndControllerUnchanged = true;
            report.visualApprovalClaimed = false;
            return report;
        }

        static Cage BuildCage(GameObject actor, Animator animator, SkinnedMeshRenderer face, SkinnedMeshRenderer hair, Profile profile)
        {
            Vector3 Point(HumanBodyBones bone)
            {
                var t = animator.GetBoneTransform(bone); Need(t, "Actual Humanoid landmark missing: " + bone);
                return actor.transform.InverseTransformPoint(t.position);
            }
            var c = new Cage { actor = actor, animator = animator, profile = profile,
                leftEye = Point(HumanBodyBones.LeftEye), rightEye = Point(HumanBodyBones.RightEye),
                neck = Point(HumanBodyBones.Neck), head = Point(HumanBodyBones.Head) };
            c.eye = (c.leftEye + c.rightEye) * .5f; c.span = Mathf.Abs(c.leftEye.x - c.rightEye.x);
            Need(c.span > .025f && c.span < .12f && c.eye.z > c.head.z,
                "Actual source eyes must define a plausible +Z-facing face.");
            var all = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.enabled)
                .SelectMany(r => DrawnPoints(actor, r)).ToArray();
            c.sourceEnvelope = all;
            c.height = all.Max(p => p.y) - all.Min(p => p.y);
            Need(c.height > 1.25f && c.height < 1.95f, "Retained actor scale is required.");
            c.posedFace = PosedPoints(actor, face);
            var referencedFace = Referenced(face.sharedMesh);
            c.faceHeight = referencedFace.Max(i => c.posedFace[i].y) - referencedFace.Min(i => c.posedFace[i].y);
            Need(c.faceHeight > c.height * .08f && c.faceHeight < c.height * .23f, "Actual face extents are required.");
            var skin = MaterialIndices(face, n => n.IndexOf("Face_", StringComparison.OrdinalIgnoreCase) >= 0 && n.IndexOf("SKIN", StringComparison.OrdinalIgnoreCase) >= 0);
            Need(skin.Count > 0, "Actual facial-skin triangles are required for head proportion measurement.");
            float scalpTop = DrawnPoints(actor, hair).Max(p => p.y), chin = skin.Min(i => c.posedFace[i].y);
            Need(scalpTop > chin + c.height * .08f, "Actual scalp-to-chin span is required.");
            c.sourceHeadUnits = c.height / (scalpTop - chin);
            // A bounded head-only proportional correction keeps body/rig/motion
            // unchanged. Mary retains the female source proportions beneath her hood.
            float targetUnits = profile.child ? 6.6f : profile.retainHeadProportions ? c.sourceHeadUnits : 7.35f;
            c.headVerticalScale = Mathf.Clamp(c.sourceHeadUnits / targetUnits, .90f, 1.025f);
            var mouth = MaterialIndices(face, n => n.IndexOf("FaceMouth", StringComparison.OrdinalIgnoreCase) >= 0);
            Need(mouth.Count > 0, "Actual authored mouth vertices are required; do not guess a lip position.");
            float front = mouth.Max(i => c.posedFace[i].z);
            var visible = mouth.Where(i => c.posedFace[i].z > front - c.faceHeight * .055f).ToArray();
            c.mouthY = (visible.Min(i => c.posedFace[i].y) + visible.Max(i => c.posedFace[i].y)) * .5f;
            Need(c.mouthY < c.eye.y && c.mouthY > c.neck.y, "Actual mouth/eye/neck order is invalid.");
            return c;
        }

        static Vector3 Shape(Cage c, Vector3 p, bool hair)
        {
            var q = p; var a = c.profile; float h = c.faceHeight;
            float neckFade = Smooth(c.neck.y + h * .025f, c.neck.y + h * .31f, p.y);
            q.x = c.eye.x + (q.x - c.eye.x) * Mathf.Lerp(1, a.headWidth, neckFade);
            q.y = c.eye.y + (q.y - c.eye.y) * Mathf.Lerp(1, c.headVerticalScale, neckFade);
            q.z = c.head.z + (q.z - c.head.z) * Mathf.Lerp(1, a.headDepth, neckFade);
            if (hair)
            {
                float front = Smooth(c.head.z, c.eye.z + h * .03f, p.z);
                float fringe = 1 - Smooth(c.eye.y + h * .14f, c.eye.y + h * .44f, p.y);
                float centre = Bell((p.x - c.eye.x) / (c.span * .70f));
                float notBelowCheek = Smooth(c.mouthY, c.eye.y - h * .03f, p.y);
                float lift = Mathf.Clamp(c.eye.y + h * .12f - p.y, 0, h * .13f);
                q.y += .42f * lift * front * fringe * centre * notBelowCheek;
                float sign = (p.x - c.eye.x) / Mathf.Sqrt((p.x - c.eye.x) * (p.x - c.eye.x) + h * h * .0025f);
                q.x += sign * h * a.hairPart * front * fringe * centre;
                return q;
            }
            float faceFront = Smooth(c.head.z - h * .05f, c.eye.z - h * .02f, p.z);
            float jaw = (1 - Smooth(c.mouthY - h * .03f, c.eye.y - h * .08f, p.y)) * neckFade;
            float cheek = Bell((p.y - (c.eye.y - h * .15f)) / (h * .21f));
            q.x += (p.x - c.eye.x) * (a.jawWidth * jaw - a.cheekCut * cheek) * faceFront;
            q.y -= h * a.chinLength * jaw * faceFront;
            foreach (Vector3 eye in new[] { c.leftEye, c.rightEye })
            {
                float mask = Bell((p.x - eye.x) / (c.span * .70f)) * Bell((p.y - eye.y) / (h * .15f)) * faceFront;
                q.x += (p.x - eye.x) * (a.eyeWidth - 1) * mask;
                q.y += (p.y - eye.y) * (a.eyeHeight - 1) * mask;
            }
            float noseY = Mathf.Lerp(c.mouthY, c.eye.y, .61f);
            float nose = Bell((p.x - c.eye.x) / (c.span * .31f)) * Bell((p.y - noseY) / (h * .18f)) * faceFront;
            q.z += h * a.noseProjection * nose;
            float brow = Bell((p.y - (c.eye.y + h * .11f)) / (h * .10f)) * faceFront;
            q.y -= h * a.browLower * brow;
            return q;
        }

        static MeshReport Sculpt(SkinnedMeshRenderer renderer, Cage c, string id, bool hair)
        {
            Mesh source = renderer.sharedMesh; Need(source && source.isReadable, "Readable retained mesh required.");
            string before = MeshHash(source), asset = AssetDatabase.GetAssetPath(source);
            Need(!string.IsNullOrEmpty(asset), "The original mesh asset identity must be retained.");
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long localId);
            var skin = SkinMatrices(c.actor, renderer); Vector3[] basis = source.vertices;
            Vector3[] normals = source.normals; Vector4[] tangents = source.tangents;
            Need(normals.Length == basis.Length, "Retained authored normals are required.");
            var clone = UnityEngine.Object.Instantiate(source); clone.name = id + (hair ? "__HumanHairV1" : "__HumanFaceV1");
            var vertices = new Vector3[basis.Length]; var outputNormals = new Vector3[basis.Length];
            var outputTangents = tangents.Length == basis.Length ? new Vector4[basis.Length] : Array.Empty<Vector4>();
            var affine = new Matrix4x4[basis.Length]; var normalMaps = new Matrix4x4[basis.Length];
            var normalScale = new float[basis.Length]; var tangentScale = new float[basis.Length];
            float maximum = 0, minimumDet = float.MaxValue, eps = c.faceHeight * .0007f;
            for (int i = 0; i < basis.Length; i++)
            {
                Vector3 p = skin[i].MultiplyPoint3x4(basis[i]), mapped = Shape(c, p, hair);
                var j = Matrix4x4.identity;
                for (int axis = 0; axis < 3; axis++)
                {
                    var delta = Vector3.zero; delta[axis] = eps;
                    Vector3 derivative = (Shape(c, p + delta, hair) - Shape(c, p - delta, hair)) / (2 * eps);
                    j.SetColumn(axis, new Vector4(derivative.x, derivative.y, derivative.z, 0));
                }
                affine[i] = skin[i].inverse * j * skin[i];
                float det = affine[i].determinant; minimumDet = Mathf.Min(minimumDet, det);
                Need(det > .20f && det < 4 && Finite(mapped), "A face/hair sculpt must not invert or collapse its local shape.");
                vertices[i] = skin[i].inverse.MultiplyPoint3x4(mapped);
                maximum = Mathf.Max(maximum, (mapped - p).magnitude);
                normalMaps[i] = affine[i].inverse.transpose;
                Vector3 n = normalMaps[i].MultiplyVector(normals[i]); Need(Finite(n) && n.sqrMagnitude > .000001f, "Authored normals must remain finite and nonzero."); normalScale[i] = 1 / n.magnitude;
                outputNormals[i] = n * normalScale[i];
                if (outputTangents.Length > 0)
                {
                    Vector3 t = affine[i].MultiplyVector(new Vector3(tangents[i].x, tangents[i].y, tangents[i].z));
                    Need(Finite(t) && t.sqrMagnitude > .000001f, "Authored tangents must remain finite and nonzero."); tangentScale[i] = 1 / t.magnitude; t *= tangentScale[i];
                    outputTangents[i] = new Vector4(t.x, t.y, t.z, tangents[i].w);
                }
            }
            clone.vertices = vertices; clone.normals = outputNormals;
            if (outputTangents.Length > 0) clone.tangents = outputTangents;
            clone.ClearBlendShapes(); int frames = 0;
            var dv = new Vector3[basis.Length]; var dn = new Vector3[basis.Length]; var dt = new Vector3[basis.Length];
            for (int shape = 0; shape < source.blendShapeCount; shape++)
                for (int frame = 0; frame < source.GetBlendShapeFrameCount(shape); frame++)
                {
                    Array.Clear(dv, 0, dv.Length); Array.Clear(dn, 0, dn.Length); Array.Clear(dt, 0, dt.Length);
                    source.GetBlendShapeFrameVertices(shape, frame, dv, dn, dt);
                    for (int i = 0; i < basis.Length; i++)
                    {
                        // FIXED per-vertex maps preserve arbitrary simultaneous morph
                        // combinations. Masks are never recalculated from morph frames.
                        dv[i] = affine[i].MultiplyVector(dv[i]);
                        dn[i] = normalMaps[i].MultiplyVector(dn[i]) * normalScale[i];
                        dt[i] = outputTangents.Length > 0 ? affine[i].MultiplyVector(dt[i]) * tangentScale[i] : Vector3.zero;
                    }
                    clone.AddBlendShapeFrame(source.GetBlendShapeName(shape), source.GetBlendShapeFrameWeight(shape, frame), dv, dn, dt); frames++;
                }
            clone.RecalculateBounds(); Need(MeshHash(source) == before, "The retained source mesh changed during derivation.");
            Need(SkinAndTopologyHash(clone) == SkinAndTopologyHash(source), "Complete native skin/bind/UV/topology payload must remain exact.");
            Need(clone.blendShapeCount == source.blendShapeCount, "Every retained expression must survive.");
            var defaults = Enumerable.Range(0, source.blendShapeCount).Select(renderer.GetBlendShapeWeight).ToArray();
            string destination = OutputRoot + "/" + id + "/" + clone.name + ".asset";
            Mesh saved = SaveMesh(clone, destination); renderer.sharedMesh = saved;
            for (int i = 0; i < defaults.Length; i++) renderer.SetBlendShapeWeight(i, defaults[i]);
            Need(Enumerable.Range(0, defaults.Length).All(i => renderer.GetBlendShapeWeight(i) == defaults[i]), "Actual renderer default expression weights changed.");
            return new MeshReport { renderer = renderer.name, sourceAsset = asset, sourceGuid = guid,
                sourceLocalId = localId, sourceHash = before, derivedAsset = destination, derivedHash = MeshHash(saved),
                vertices = saved.vertexCount, triangles = saved.triangles.Length / 3, blendShapes = saved.blendShapeCount,
                blendShapeFrames = frames, maximumRestDisplacement = maximum, minimumAffineDeterminant = minimumDet,
                topologyUvNativeSkinAndBindposesRetained = true, sourceUnchanged = MeshHash(source) == before,
                expressionFramesLinear = true, rendererDefaultShapeWeightsRetained = true };
        }

        static HairReport BuildLayeredHair(SkinnedMeshRenderer source, Cage c, string id, Material material)
        {
            var posed = PosedPoints(c.actor, source); var skin = SkinMatrices(c.actor, source); var scalp = Referenced(source.sharedMesh);
            var weights = VertexWeights(source.sharedMesh); var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var uvs = new List<Vector2>(); var counts = new List<byte>(); var fullWeights = new List<BoneWeight1>(); var triangles = new List<int>();
            float top = scalp.Max(i => posed[i].y), width = scalp.Max(i => Mathf.Abs(posed[i].x - c.eye.x));
            Need(width > c.span * .55f && width < c.height * .12f, "Actual scalp width is required for layered locks.");
            int strands = 0; float longest = 0, maximumSelectionDistance = 0; const int rings = 9, sides = 8;
            void Strand(Vector3 rootTarget, Vector3 middle, Vector3 tip, float radius, float phase)
            {
                int anchor = Nearest(posed, scalp, rootTarget); Vector3 root = posed[anchor];
                Need((root - rootTarget).magnitude < c.faceHeight * .39f, "A layered hair root must attach to actual scalp geometry.");
                maximumSelectionDistance = Mathf.Max(maximumSelectionDistance, (root - rootTarget).magnitude);
                var w = weights[anchor]; Matrix4x4 inverse = skin[anchor].inverse; int start = vertices.Count;
                for (int row = 0; row <= rings; row++)
                {
                    float t = row / (float)rings, u = 1 - t;
                    Vector3 p = u * u * root + 2 * u * t * middle + t * t * tip;
                    p.x += Mathf.Sin(t * Mathf.PI * 2 + phase) * radius * .13f * Mathf.Sin(t * Mathf.PI);
                    // The tips fall outside the actual retained cloth/back envelope,
                    // rather than ending inside the existing shoulder shawl.
                    if (t > .35f && p.y < c.neck.y + c.faceHeight * .28f && p.z < c.head.z)
                    {
                        float back = float.PositiveInfinity;
                        foreach (var sample in c.sourceEnvelope)
                            if (Mathf.Abs(sample.y - p.y) < c.faceHeight * .08f && Mathf.Abs(sample.x - p.x) < c.faceHeight * .12f)
                                back = Mathf.Min(back, sample.z);
                        if (!float.IsInfinity(back)) p.z = Mathf.Min(p.z, back - radius * .40f - c.faceHeight * .012f);
                    }
                    Vector3 tangent = (2 * u * (middle - root) + 2 * t * (tip - middle)).normalized;
                    Vector3 across = Vector3.Cross(tangent, Vector3.forward).normalized;
                    if (across.sqrMagnitude < .1f) across = Vector3.Cross(tangent, Vector3.right).normalized;
                    Vector3 depth = Vector3.Cross(tangent, across).normalized;
                    float r = radius * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Lerp(.055f, .997f, t)), .62f);
                    for (int side = 0; side < sides; side++)
                    {
                        float angle = side * Mathf.PI * 2 / sides; Vector3 n = (across * Mathf.Cos(angle) + depth * (Mathf.Sin(angle) / .36f)).normalized;
                        Vector3 actual = p + across * Mathf.Cos(angle) * r + depth * Mathf.Sin(angle) * r * .36f;
                        vertices.Add(inverse.MultiplyPoint3x4(actual));
                        normals.Add(skin[anchor].transpose.MultiplyVector(n).normalized); uvs.Add(new Vector2(side / (float)sides, t));
                        counts.Add(checked((byte)w.Length)); fullWeights.AddRange(w);
                    }
                }
                for (int row = 0; row < rings; row++)
                    for (int side = 0; side < sides; side++)
                    {
                        int a = start + row * sides + side, b = start + row * sides + (side + 1) % sides;
                        triangles.AddRange(new[] { a, b, b + sides, a, b + sides, a + sides });
                    }
                for (int side = 1; side < sides - 1; side++)
                {
                    triangles.AddRange(new[] { start, start + side + 1, start + side });
                    int end = start + rings * sides; triangles.AddRange(new[] { end, end + side, end + side + 1 });
                }
                longest = Mathf.Max(longest, (middle - root).magnitude + (tip - middle).magnitude); strands++;
            }
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 6; i++)
                {
                    float t = i / 5f;
                    var root = new Vector3(c.eye.x + side * width * Mathf.Lerp(.40f, .83f, t), top - c.faceHeight * Mathf.Lerp(.10f, .29f, t), c.head.z + c.faceHeight * Mathf.Lerp(.10f, -.18f, t));
                    var middle = new Vector3(c.eye.x + side * width * Mathf.Lerp(.99f, 1.09f, t), c.eye.y - c.faceHeight * .20f, c.head.z - c.faceHeight * Mathf.Lerp(.01f, .20f, t));
                    var tip = new Vector3(c.eye.x + side * width * Mathf.Lerp(.77f, .96f, t), c.neck.y - c.faceHeight * Mathf.Lerp(.10f, .22f, t), c.head.z - c.faceHeight * Mathf.Lerp(.08f, .28f, t));
                    Strand(root, middle, tip, c.faceHeight * .047f, i * .83f);
                }
            for (int i = 0; i < 12; i++)
            {
                float t = (i + .5f) / 12f, x = Mathf.Lerp(-.88f, .88f, t) * width;
                var root = new Vector3(c.eye.x + x * .72f, top - c.faceHeight * .15f, c.head.z - c.faceHeight * .24f);
                var middle = new Vector3(c.eye.x + x * 1.05f, c.eye.y - c.faceHeight * .19f, c.head.z - c.faceHeight * .48f);
                var tip = new Vector3(c.eye.x + x * .92f, c.neck.y - c.faceHeight * (.18f + .08f * Mathf.Sin(t * Mathf.PI)), c.head.z - c.faceHeight * .34f);
                Strand(root, middle, tip, c.faceHeight * .052f, i * .61f);
            }
            var mesh = new Mesh { name = id + "__LayeredHairV1Mesh" }; mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0);
            mesh.bindposes = source.sharedMesh.bindposes;
            using (var n = new NativeArray<byte>(counts.ToArray(), Allocator.Temp))
            using (var w = new NativeArray<BoneWeight1>(fullWeights.ToArray(), Allocator.Temp)) mesh.SetBoneWeights(n, w);
            using (var actualCounts = mesh.GetBonesPerVertex())
            using (var actualWeights = mesh.GetAllBoneWeights())
            {
                Need(actualCounts.Length == counts.Count && actualWeights.Length == fullWeights.Count,
                    "The generated hair must retain every complete anchor influence.");
                for (int i = 0; i < counts.Count; i++) Need(actualCounts[i] == counts[i], "A generated hair influence count was reencoded.");
                for (int i = 0; i < fullWeights.Count; i++)
                    Need(actualWeights[i].boneIndex == fullWeights[i].boneIndex && actualWeights[i].weight.Equals(fullWeights[i].weight),
                        "A generated hair native influence changed during SetBoneWeights; preserve the failure and review the source.");
            }
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            string destination = OutputRoot + "/" + id + "/" + mesh.name + ".asset"; Mesh saved = SaveMesh(mesh, destination);
            var go = new GameObject(id + "__LayeredHairV1"); go.transform.SetParent(source.transform, false);
            var renderer = go.AddComponent<SkinnedMeshRenderer>(); renderer.sharedMesh = saved; renderer.sharedMaterial = material;
            renderer.bones = source.bones; renderer.rootBone = source.rootBone; renderer.quality = source.quality; renderer.updateWhenOffscreen = true;
            return new HairReport { renderer = go.name, meshAsset = destination, meshHash = MeshHash(saved),
                strands = strands, vertices = saved.vertexCount, triangles = saved.triangles.Length / 3,
                longestStrand = longest, maximumAnchorGap = 0, maximumScalpSelectionDistance = maximumSelectionDistance,
                sourcePaletteAndCompleteAnchorWeights = true, sourceHairRetained = true };
        }

        static int Nearest(Vector3[] values, int[] referenced, Vector3 point)
        {
            int result = -1; float distance = float.MaxValue;
            foreach (int i in referenced) { float d = (values[i] - point).sqrMagnitude; if (d < distance) { distance = d; result = i; } }
            Need(result >= 0, "Actual source scalp vertices required."); return result;
        }
        static SkinnedMeshRenderer Exact(IEnumerable<SkinnedMeshRenderer> renderers, string name)
        { var result = renderers.Where(r => r.name == name).ToArray(); Need(result.Length == 1, "Exactly one retained renderer required: " + name); return result[0]; }
        static HashSet<int> MaterialIndices(SkinnedMeshRenderer r, Func<string, bool> predicate)
        {
            var result = new HashSet<int>();
            for (int slot = 0; slot < r.sharedMesh.subMeshCount; slot++)
                if (slot < r.sharedMaterials.Length && r.sharedMaterials[slot] && predicate(r.sharedMaterials[slot].name))
                    result.UnionWith(r.sharedMesh.GetIndices(slot));
            return result;
        }
        internal static BoneWeight1[][] VertexWeights(Mesh mesh)
        {
            using (var counts = mesh.GetBonesPerVertex())
            using (var all = mesh.GetAllBoneWeights())
            {
                Need(counts.Length == mesh.vertexCount, "Complete native influence counts required.");
                var result = new BoneWeight1[counts.Length][]; int offset = 0;
                for (int i = 0; i < counts.Length; i++)
                {
                    result[i] = new BoneWeight1[counts[i]]; float sum = 0;
                    for (int j = 0; j < counts[i]; j++) { result[i][j] = all[offset++]; sum += result[i][j].weight; }
                    Need(result[i].Length > 0 && Mathf.Abs(sum - 1) < .002f, "Actual complete normalized skin weights required.");
                }
                Need(offset == all.Length, "Native skin count/data mismatch."); return result;
            }
        }
        internal static Matrix4x4[] SkinMatrices(GameObject actor, SkinnedMeshRenderer renderer)
        {
            Mesh mesh = renderer.sharedMesh; Need(mesh && mesh.isReadable && mesh.bindposes.Length == renderer.bones.Length, "Actual readable renderer/bind palette required.");
            var weights = VertexWeights(mesh); var palette = new Matrix4x4[renderer.bones.Length]; var bind = mesh.bindposes;
            for (int i = 0; i < palette.Length; i++) { Need(renderer.bones[i], "Retained bone palette cannot contain null."); palette[i] = actor.transform.worldToLocalMatrix * renderer.bones[i].localToWorldMatrix * bind[i]; }
            var result = new Matrix4x4[mesh.vertexCount];
            for (int v = 0; v < result.Length; v++)
            {
                var m = Matrix4x4.zero;
                foreach (var w in weights[v])
                {
                    Need(w.boneIndex >= 0 && w.boneIndex < palette.Length, "Actual native palette index required.");
                    for (int row = 0; row < 4; row++) for (int col = 0; col < 4; col++) m[row, col] += palette[w.boneIndex][row, col] * w.weight;
                }
                Need(Mathf.Abs(m.determinant) > .00001f, "Actual skin-to-actor rest mapping must be invertible."); result[v] = m;
            }
            return result;
        }
        static Vector3[] PosedPoints(GameObject actor, SkinnedMeshRenderer renderer)
        {
            Need(Enumerable.Range(0, renderer.sharedMesh.blendShapeCount).All(i => Mathf.Abs(renderer.GetBlendShapeWeight(i)) < .00001f),
                "Art derivation requires the actual fresh neutral source morphs; never reset a sampled expression silently.");
            var rawVertices = renderer.sharedMesh.vertices; var vertices = (Vector3[])rawVertices.Clone(); var matrices = SkinMatrices(actor, renderer);
            for (int i = 0; i < vertices.Length; i++) vertices[i] = matrices[i].MultiplyPoint3x4(vertices[i]);
            var sampled = new Mesh();
            try
            {
                renderer.BakeMesh(sampled, false); var actual = sampled.vertices;
                Need(actual.Length == vertices.Length, "Actual temporary skin sample must retain vertex correspondence.");
                var toActor = actor.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                Vector3 scale = renderer.transform.lossyScale;
                Need(Finite(scale) && scale.x > 0 && scale.y > 0 && scale.z > 0,
                    "Actual BakeMesh comparison requires finite positive renderer lossyScale.");
                var inverseScale = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);
                Need(Finite(inverseScale), "Actual renderer scale reciprocals must remain finite.");
                var bakeToActor = toActor * Matrix4x4.Scale(inverseScale);
                var referenced = Referenced(renderer.sharedMesh); int first = -1, maximumIndex = -1; float maximum = 0;
                foreach (int i in referenced)
                {
                    float distance = (bakeToActor.MultiplyPoint3x4(actual[i]) - vertices[i]).magnitude;
                    if (!(distance < .0005f) && first < 0) first = i;
                    if (float.IsNaN(distance) || float.IsInfinity(distance)) { maximum = float.PositiveInfinity; maximumIndex = i; }
                    else if (distance > maximum) { maximum = distance; maximumIndex = i; }
                }
                if (first >= 0)
                    throw new InvalidOperationException(RestDiagnostic(actor, renderer, first, maximumIndex, maximum,
                        referenced.Length, rawVertices[first], vertices[first], actual[first],
                        bakeToActor.MultiplyPoint3x4(actual[first]), toActor, matrices[first], bakeToActor));
            }
            finally { UnityEngine.Object.DestroyImmediate(sampled); }
            return vertices;
        }
        static string RestDiagnostic(GameObject actor, SkinnedMeshRenderer renderer, int first, int maximumIndex, float maximum,
            int referencedCount, Vector3 meshRaw, Vector3 expected, Vector3 bakeRaw, Vector3 mappedBake, Matrix4x4 toActor, Matrix4x4 skin, Matrix4x4 bakeToActor)
        {
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            string F(float v) => v.ToString("R", culture);
            string V(Vector3 v) => "(" + F(v.x) + "," + F(v.y) + "," + F(v.z) + ")";
            string M(Matrix4x4 m) => "[" + string.Join(";", Enumerable.Range(0, 4)
                .Select(row => string.Join(",", Enumerable.Range(0, 4).Select(col => F(m[row, col]))))) + "]";
            var weights = VertexWeights(renderer.sharedMesh); var influences = weights[first];
            var t = renderer.transform; var q = t.localRotation;
            string rootBone = renderer.rootBone ? AnimationUtility.CalculateTransformPath(renderer.rootBone, actor.transform) : "<null>";
            return "Actual BakeMesh rest differs from complete native skin mapping; tolerance=0.0005 unchanged; actorId=" + actor.name
                + "; renderer=" + renderer.name + "; rendererPath=" + AnimationUtility.CalculateTransformPath(t, actor.transform)
                + "; mesh=" + renderer.sharedMesh.name + "; meshAsset=" + AssetDatabase.GetAssetPath(renderer.sharedMesh)
                + "; firstIndex=" + first + "; firstDeviation=" + F((mappedBake - expected).magnitude)
                + "; maximumIndex=" + maximumIndex + "; maximumDeviation=" + F(maximum) + "; referencedVertices=" + referencedCount
                + "; expectedActor=" + V(expected) + "; mappedBakeActor=" + V(mappedBake)
                + "; meshRaw=" + V(meshRaw) + "; bakeRaw=" + V(bakeRaw)
                + "; rendererLocalPosition=" + V(t.localPosition) + "; rendererLocalRotation=(" + F(q.x) + "," + F(q.y) + "," + F(q.z) + "," + F(q.w) + ")"
                + "; rendererLocalScale=" + V(t.localScale) + "; rendererLossyScale=" + V(t.lossyScale)
                + "; rootBone=" + rootBone + "; quality=" + renderer.quality + "; projectSkinWeights=" + QualitySettings.skinWeights
                + "; nativeInfluences=" + influences.Length + "; nativeWeightSum=" + F(influences.Sum(w => w.weight))
                + "; nativePalette=" + string.Join(",", influences.Select(w => w.boneIndex + ":" + F(w.weight)))
                + "; rendererToActor=" + M(toActor) + "; firstSkinToActor=" + M(skin) + "; bakeToActor=" + M(bakeToActor)
                + "; scope=diagnostic only; no pose reset, skin truncation, geometry change or tolerance relaxation.";
        }
        static int[] Referenced(Mesh mesh)
        {
            var result = new HashSet<int>();
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            { Need(mesh.GetTopology(sub) == MeshTopology.Triangles, "Retained character surfaces must use triangles."); result.UnionWith(mesh.GetIndices(sub)); }
            Need(result.Count > 0, "Actually referenced surface vertices are required."); return result.OrderBy(i => i).ToArray();
        }
        static IEnumerable<Vector3> DrawnPoints(GameObject actor, SkinnedMeshRenderer renderer)
        { var points = PosedPoints(actor, renderer); foreach (int i in Referenced(renderer.sharedMesh)) yield return points[i]; }
        internal static Mesh SaveMesh(Mesh mesh, string path)
        {
            string folder = Path.GetDirectoryName(path).Replace('\\', '/'); var segments = folder.Split('/'); string current = segments[0];
            for (int i = 1; i < segments.Length; i++) { string next = current + "/" + segments[i]; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[i]); current = next; }
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing) { Need(MeshHash(existing) == MeshHash(mesh), "Preserve a differing existing derived mesh: " + path); UnityEngine.Object.DestroyImmediate(mesh); return existing; }
            Need(!File.Exists(path), "Preserve any existing non-mesh candidate: " + path); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        internal static string MeshHash(Mesh mesh)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                void V(Vector3 p) { writer.Write(p.x); writer.Write(p.y); writer.Write(p.z); }
                writer.Write(mesh.vertexCount); foreach (var v in mesh.vertices) V(v); foreach (var n in mesh.normals) V(n);
                foreach (var t in mesh.tangents) { writer.Write(t.x); writer.Write(t.y); writer.Write(t.z); writer.Write(t.w); }
                writer.Write(SkinAndTopologyHash(mesh)); writer.Write(mesh.blendShapeCount);
                var dv = new Vector3[mesh.vertexCount]; var dn = new Vector3[mesh.vertexCount]; var dt = new Vector3[mesh.vertexCount];
                for (int shape = 0; shape < mesh.blendShapeCount; shape++)
                {
                    writer.Write(mesh.GetBlendShapeName(shape)); writer.Write(mesh.GetBlendShapeFrameCount(shape));
                    for (int frame = 0; frame < mesh.GetBlendShapeFrameCount(shape); frame++)
                    { writer.Write(mesh.GetBlendShapeFrameWeight(shape, frame)); mesh.GetBlendShapeFrameVertices(shape, frame, dv, dn, dt); for (int i = 0; i < dv.Length; i++) { V(dv[i]); V(dn[i]); V(dt[i]); } }
                }
                writer.Flush(); using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
        static string SkinAndTopologyHash(Mesh mesh)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(mesh.vertexCount); writer.Write(mesh.subMeshCount);
                for (int i = 0; i < mesh.subMeshCount; i++) { writer.Write((int)mesh.GetTopology(i)); foreach (int v in mesh.GetIndices(i)) writer.Write(v); writer.Write(-1); }
                for (int channel = 0; channel < 8; channel++) { var uv = new List<Vector4>(); mesh.GetUVs(channel, uv); writer.Write(uv.Count); foreach (var p in uv) { writer.Write(p.x); writer.Write(p.y); writer.Write(p.z); writer.Write(p.w); } }
                var attributes = mesh.GetVertexAttributes().Where(a => a.attribute == UnityEngine.Rendering.VertexAttribute.Color ||
                    ((int)a.attribute >= (int)UnityEngine.Rendering.VertexAttribute.TexCoord0 && (int)a.attribute <= (int)UnityEngine.Rendering.VertexAttribute.TexCoord7)).ToArray();
                writer.Write(attributes.Length); foreach (var a in attributes) { writer.Write((int)a.attribute); writer.Write((int)a.format); writer.Write(a.dimension); }
                var colors = mesh.colors32; writer.Write(colors.Length); foreach (var p in colors) { writer.Write(p.r); writer.Write(p.g); writer.Write(p.b); writer.Write(p.a); }
                var floatColors = mesh.colors; writer.Write(floatColors.Length); foreach (var p in floatColors) { writer.Write(p.r); writer.Write(p.g); writer.Write(p.b); writer.Write(p.a); }
                writer.Write(mesh.bindposes.Length); foreach (var matrix in mesh.bindposes) for (int row = 0; row < 4; row++) for (int col = 0; col < 4; col++) writer.Write(matrix[row, col]);
                using (var counts = mesh.GetBonesPerVertex()) using (var all = mesh.GetAllBoneWeights())
                { writer.Write(counts.Length); foreach (byte n in counts) writer.Write(n); writer.Write(all.Length); foreach (var w in all) { writer.Write(w.boneIndex); writer.Write(w.weight); } }
                writer.Flush(); using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
        static string HierarchyHash(GameObject actor, bool ignoreNewRenderers = false)
        {
            var text = new StringBuilder();
            foreach (var t in actor.GetComponentsInChildren<Transform>(true))
            {
                if (ignoreNewRenderers && t.name.EndsWith("__LayeredHairV1", StringComparison.Ordinal)) continue;
                text.Append(AnimationUtility.CalculateTransformPath(t, actor.transform)).Append('|');
                foreach (float v in new[] { t.localPosition.x, t.localPosition.y, t.localPosition.z, t.localRotation.x, t.localRotation.y, t.localRotation.z, t.localRotation.w, t.localScale.x, t.localScale.y, t.localScale.z }) text.Append(v.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',');
                text.Append('\n');
            }
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "").ToLowerInvariant();
        }
        static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, x));
        static float Bell(float x) { if (Mathf.Abs(x) >= 1) return 0; float t = 1 - x * x; return t * t * t; }
        static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.x) && !float.IsInfinity(p.y) && !float.IsInfinity(p.z);
        static void Need(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
#endif
