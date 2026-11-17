#if UNITY_EDITOR
// Root copies this reviewed source into the Unity Editor assembly and invokes
// Apply after assigning the original Avatar/controller, before saving the prefab.
// This file does not run by import, install models, alter source FBX data or hide skin.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Firstgame.GospelNpr.Editor;
using Object = UnityEngine.Object;

namespace FirstGame.Editor
{
    public static class GospelIllustrationTailoring
    {
        const string DefaultRoot = "Assets/Art/GospelIllustration20261117/v5";
        const string Revision = "illustration-tailoring-20261117-v5";
        static readonly string[] Identities = { "JesusChild", "JesusAdult", "Mary", "Joseph", "Teacher" };

        [Serializable] public sealed class MeshReport
        {
            public string renderer, asset, originalMesh, geometrySha256;
            public int vertices, triangles, bones;
            public int adjustedVertices;
            public int nativeWeights, maximumInfluences;
            public string skinQuality;
            public bool reused, originalWeightsRetained;
            public bool sourceGeometryRetained;
        }

        [Serializable] public sealed class Report
        {
            public string revision = Revision, identity, avatar, controller;
            public bool originalMeshDataUnchanged, originalRigAndMorphsUnchanged;
            public bool freshImportedSourceRestVerified, sampledSkinnedEnvelope;
            public bool childOriginalLowerGeometryRetained;
            public string childLowerGarmentPolicy;
            public string sourceRestModel;
            public int sourceRestTransforms, sourceRestRenderers, originalNonidentityBindEntries, originalWeightedNonidentityBindEntries;
            public float originalMaxBindIdentityError;
            public EnvelopeReport[] sampledEnvelopes;
            public GospelIllustrationFittedCloth.Diagnostics fittedCloth;
            public MeshReport[] meshes;
            public string[] derivedMaterials;
            public string scope = "Derived garments and child fringe only; visual, motion, Unity/Web and player approval require separate actual checks.";
        }

        [Serializable] public sealed class EnvelopeReport
        {
            public string renderer;
            public int vertices;
            public float maxRawToSkinnedDistance;
            public Vector3 minimum, maximum;
        }

        /// <summary>Only invoke on a fresh, unanimated instance of the approved actor.</summary>
        public static Report Apply(GameObject instance, string actorIdentity, string outputRoot = DefaultRoot)
        {
            Need(!Application.isPlaying, "Tailoring is a Root-owned Editor preparation step, never a player operation.");
            Need(instance && Identities.Contains(actorIdentity), "An exact approved actor identity is required.");
            Need(!EditorUtility.IsPersistent(instance), "Tailoring requires an instance, never an imported model/prefab asset.");
            Need(outputRoot.StartsWith("Assets/Art/GospelIllustration20261117/", StringComparison.Ordinal)
                 && !outputRoot.Contains("..") && !outputRoot.Contains('\\') && !outputRoot.Contains("/Resources/"), "Use a new versioned derived Art directory.");
            var c = new Context(instance, actorIdentity, outputRoot.TrimEnd('/'));
            var protection = new Protection(c);
            var reports = new List<MeshReport>();
            if (actorIdentity == "JesusChild")
            {
                RetainChildSourceTunic(c, reports);
                reports.Add(OpenChildFringe(c));
            }
            if (actorIdentity != "Mary")
                InstallFittedSourceCloth(c, reports);
            if (actorIdentity != "JesusChild")
                reports.Add(Install(c, c.Existing("ShoulderMantle"), ShoulderDrape(c), c.Mantle, false));
            if (actorIdentity == "Mary" || actorIdentity == "Teacher")
                reports.Add(Install(c, c.Existing("OpenVeil"), RoundedHood(c), c.Mantle, false));
            protection.Verify(c);
            return new Report { identity = actorIdentity, avatar = AssetDatabase.GetAssetPath(c.Animator.avatar),
                controller = AssetDatabase.GetAssetPath(c.Animator.runtimeAnimatorController), meshes = reports.ToArray(),
                freshImportedSourceRestVerified = c.FreshImportedSourceRestVerified, sourceRestModel = c.SourceRestModel,
                sourceRestTransforms = c.SourceRestTransforms, sourceRestRenderers = c.Originals.Length,
                originalNonidentityBindEntries = c.OriginalNonidentityBindEntries,
                originalWeightedNonidentityBindEntries = c.OriginalWeightedNonidentityBindEntries,
                originalMaxBindIdentityError = c.OriginalMaxBindIdentityError,
                sampledSkinnedEnvelope = c.SampledSkinEnvelope, sampledEnvelopes = c.Envelopes.ToArray(),
                fittedCloth = c.FittedCloth?.Report,
                childOriginalLowerGeometryRetained = c.Id == "JesusChild",
                childLowerGarmentPolicy = c.Id == "JesusChild"
                    ? "Original source knee-free TailoredTunic and WovenHem mesh, hem, width, bindposes and weights retained; derived Linen on tunic only."
                    : "Unchanged original lower garment geometry.",
                derivedMaterials = c.MaterialPaths.ToArray(), originalMeshDataUnchanged = true, originalRigAndMorphsUnchanged = true };
        }

        sealed class Context
        {
            public readonly GameObject Instance;
            public readonly string Id, Root;
            public readonly Animator Animator;
            public readonly SkinnedMeshRenderer[] Originals;
            public readonly List<Transform> Bones = new List<Transform>();
            public readonly List<string> MaterialPaths = new List<string>();
            public readonly Dictionary<HumanBodyBones, Transform> Human = new Dictionary<HumanBodyBones, Transform>();
            public readonly Vector3[] Body, Hair, DrapeEnvelope;
            public readonly float Height, ShoulderWidth;
            public readonly Vector3 Hips, Chest, UpperChest, Neck, Head, LeftArm, RightArm;
            public readonly bool FreshImportedSourceRestVerified, SampledSkinEnvelope;
            public readonly string SourceRestModel;
            public readonly int SourceRestTransforms;
            public int OriginalNonidentityBindEntries, OriginalWeightedNonidentityBindEntries;
            public float OriginalMaxBindIdentityError;
            public readonly List<EnvelopeReport> Envelopes = new List<EnvelopeReport>();
            public GospelIllustrationFittedCloth.Package FittedCloth;
            public float FittedClearance;
            Material linen, fittedLinen, mantle;
            public Material Linen => linen ? linen : linen = DerivedMaterial(this, "UndyedLinen", "Linen");
            public Material FittedLinen => fittedLinen ? fittedLinen : fittedLinen = DerivedMaterial(this, "UndyedLinen", "FittedLinen", true);
            public Material Mantle => mantle ? mantle : mantle = DerivedMaterial(this, "DyedMantle", "Mantle");

            public Context(GameObject instance, string id, string root)
            {
                Instance = instance; Id = id; Root = root;
                Animator = instance.GetComponent<Animator>();
                Need(Animator && Animator.avatar && Animator.avatar.isHuman && Animator.avatar.isValid && Animator.runtimeAnimatorController,
                    "The original valid Avatar/controller must be assigned before tailoring: " + id);
                Originals = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                SourceRestModel = "Assets/Resources/Art/GospelNpr/Models/" + id + ".fbx";
                SourceRestTransforms = RequireFreshImportedSource(this);
                FreshImportedSourceRestVerified = true;
                SampledSkinEnvelope = id == "Mary";
                foreach (var renderer in Originals)
                {
                    Need(renderer.sharedMesh && renderer.sharedMesh.isReadable, "Readable original meshes are required.");
                    var bindposes = renderer.sharedMesh.bindposes; var bones = renderer.bones;
                    Need(bindposes.Length == bones.Length && bones.Length > 0 && bones.All(b => b), "Exact original skin palette is required.");
                    var positiveInfluences = new int[bones.Length];
                    var weightSums = new float[bones.Length];
                    var counts = renderer.sharedMesh.GetBonesPerVertex();
                    var allWeights = renderer.sharedMesh.GetAllBoneWeights();
                    int influenceTotal = 0; foreach (byte count in counts) influenceTotal += count;
                    Need(counts.Length == renderer.sharedMesh.vertexCount && influenceTotal == allWeights.Length,
                        "Complete original variable skin weights are required: " + renderer.name);
                    foreach (var influence in allWeights)
                    {
                        Need(influence.boneIndex >= 0 && influence.boneIndex < bones.Length && Finite(influence.weight) && influence.weight >= 0,
                            "Invalid original source skin influence: " + renderer.name);
                        if (influence.weight > 0) { positiveInfluences[influence.boneIndex]++; weightSums[influence.boneIndex] += influence.weight; }
                    }
                    for (int i = 0; i < bones.Length; i++)
                    {
                        Matrix4x4 rest = renderer.transform.worldToLocalMatrix * bones[i].localToWorldMatrix * bindposes[i];
                        float maximum = 0;
                        for (int j = 0; j < 16; j++)
                        {
                            Need(Finite(rest[j]), "Original source bind matrix must be finite: " + renderer.name);
                            maximum = Mathf.Max(maximum, Mathf.Abs(rest[j] - Matrix4x4.identity[j]));
                        }
                        OriginalMaxBindIdentityError = Mathf.Max(OriginalMaxBindIdentityError, maximum);
                        if (maximum >= .004f)
                        {
                            OriginalNonidentityBindEntries++;
                            if (positiveInfluences[i] > 0) OriginalWeightedNonidentityBindEntries++;
                        }
                        // Rig01 establishes Mary's exact imported source-rest
                        // skin is nonidentity, including weighted entries. Read
                        // its actual skinned envelope; never rebind that skin.
                        if (!SampledSkinEnvelope)
                            Need(maximum < .004f,
                                "Tailor the imported bind pose before animation, not a sampled pose: " + renderer.name
                                + " bone=" + i + ":" + bones[i].name + " maxIdentityError=" + maximum
                                + " positiveInfluences=" + positiveInfluences[i] + " weightSum=" + weightSums[i] + " matrix=" + rest);
                    }
                }
                foreach (var kind in new[] { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest,
                    HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head, HumanBodyBones.LeftShoulder,
                    HumanBodyBones.RightShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm,
                    HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg })
                {
                    var bone = Animator.GetBoneTransform(kind);
                    if (!bone && kind == HumanBodyBones.UpperChest) bone = Animator.GetBoneTransform(HumanBodyBones.Chest);
                    if (!bone && kind == HumanBodyBones.LeftShoulder) bone = Animator.GetBoneTransform(HumanBodyBones.Chest);
                    if (!bone && kind == HumanBodyBones.RightShoulder) bone = Animator.GetBoneTransform(HumanBodyBones.Chest);
                    Need(bone, "Expected original humanoid bone missing: " + id + "/" + kind);
                    Human.Add(kind, bone);
                    if (!Bones.Contains(bone)) Bones.Add(bone);
                }
                Need(Bones.All(b => b.IsChildOf(instance.transform)), "Every weight must reference the original actor hierarchy.");
                var sourceMeshProof = Originals.Select(r => r.sharedMesh).Distinct().ToDictionary(m => m, MeshHash);
                Body = Points(Existing("Body")); Hair = Points(Existing("Hair"));
                DrapeEnvelope = SampledSkinEnvelope ? Body.Concat(Points(Existing("TailoredTunic"))).ToArray() : Body;
                var face = Existing("Face");
                Need(face.sharedMesh && face.sharedMesh.blendShapeCount > 0, "The original facial morph mesh must remain available.");
                Hips = Point(HumanBodyBones.Hips); Chest = Point(HumanBodyBones.Chest);
                UpperChest = Point(HumanBodyBones.UpperChest); Neck = Point(HumanBodyBones.Neck); Head = Point(HumanBodyBones.Head);
                LeftArm = Point(HumanBodyBones.LeftUpperArm); RightArm = Point(HumanBodyBones.RightUpperArm);
                ShoulderWidth = Mathf.Max(Mathf.Abs(LeftArm.x - Hips.x), Mathf.Abs(RightArm.x - Hips.x));
                Height = Points(face).Concat(Body).Concat(Hair).Max(p => p.y);
                Need(Height > 1.2f && Height < 2f && ShoulderWidth > .06f && ShoulderWidth < .35f,
                    "Unexpected bind-space actor dimensions; do not tailor a sampled animated pose.");
                Need(RequireFreshImportedSource(this) == SourceRestTransforms, "Envelope sampling must preserve the exact imported source rest.");
                foreach (var pair in sourceMeshProof)
                    Need(MeshHash(pair.Key) == pair.Value, "Envelope sampling changed an original source mesh: " + pair.Key.name);
                Need(!SampledSkinEnvelope || Envelopes.Count == 4, "Mary requires actual Body/Hair/Face and TailoredTunic skin envelopes.");
            }

            public Vector3 Point(HumanBodyBones bone) => Instance.transform.InverseTransformPoint(Human[bone].position);
            public Vector3[] Points(SkinnedMeshRenderer r)
            {
                Need(r.sharedMesh && r.sharedMesh.isReadable && r.bones.Length > 0, "Readable original bound mesh required: " + r.name);
                Matrix4x4 frame = Instance.transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
                Vector3[] raw = r.sharedMesh.vertices;
                if (!SampledSkinEnvelope) return raw.Select(p => frame.MultiplyPoint3x4(p)).ToArray();
                Need(FreshImportedSourceRestVerified && new[] { "Mary__Body", "Mary__Hair", "Mary__Face", "Mary__TailoredTunic" }.Contains(r.name),
                    "Only Mary's protected source skins may supply sampled envelopes.");
                var baked = new Mesh();
                try
                {
                    r.BakeMesh(baked, false); // Transform scale is applied once by frame, below.
                    var sampled = baked.vertices;
                    Need(sampled.Length == raw.Length && sampled.Length > 0, "Skinned envelope vertex correspondence changed: " + r.name);
                    var points = new Vector3[sampled.Length]; float distance = 0;
                    Vector3 low = Vector3.positiveInfinity, high = Vector3.negativeInfinity;
                    for (int i = 0; i < sampled.Length; i++)
                    {
                        points[i] = frame.MultiplyPoint3x4(sampled[i]);
                        Need(Finite(points[i].x) && Finite(points[i].y) && Finite(points[i].z), "Nonfinite actual skinned envelope: " + r.name);
                        low = Vector3.Min(low, points[i]); high = Vector3.Max(high, points[i]);
                        distance = Mathf.Max(distance, Vector3.Distance(points[i], frame.MultiplyPoint3x4(raw[i])));
                    }
                    Need(Finite(distance) && (high - low).sqrMagnitude > .000001f, "Degenerate actual skinned envelope: " + r.name);
                    Envelopes.Add(new EnvelopeReport { renderer = r.name, vertices = points.Length,
                        maxRawToSkinnedDistance = distance, minimum = low, maximum = high });
                    return points;
                }
                finally { Object.DestroyImmediate(baked); }
            }
            public SkinnedMeshRenderer Existing(string suffix)
            {
                var matches = Originals.Where(r => r.name == Id + "__" + suffix).ToArray();
                Need(matches.Length == 1, "Expected one exact original mesh: " + Id + "__" + suffix);
                return matches[0];
            }
            public SkinnedMeshRenderer Added(string suffix)
            {
                string name = Id + "__Illustration" + suffix;
                var old = Instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name == name).ToArray();
                Need(old.Length <= 1, "Duplicate derived renderer: " + name);
                if (old.Length == 1) return old[0];
                var child = new GameObject(name); child.transform.SetParent(Instance.transform, false);
                return child.AddComponent<SkinnedMeshRenderer>();
            }
            public BoneWeight Weight(params KeyValuePair<HumanBodyBones, float>[] items)
            {
                var values = new Dictionary<int, float>();
                foreach (var item in items)
                {
                    Need(Finite(item.Value) && item.Value >= 0, "Invalid garment influence.");
                    int index = Bones.IndexOf(Human[item.Key]);
                    if (item.Value > .000001f) values[index] = (values.TryGetValue(index, out float old) ? old : 0) + item.Value;
                }
                var ordered = values.OrderByDescending(p => p.Value).Take(4).ToArray();
                float sum = ordered.Sum(p => p.Value); Need(sum > .000001f, "Every generated vertex needs original-bone skinning.");
                var w = new BoneWeight();
                for (int i = 0; i < ordered.Length; i++) SetWeight(ref w, i, ordered[i].Key, ordered[i].Value / sum);
                return w;
            }
            public BoneWeight Torso(Vector3 p, float head = 0, float arm = 0)
            {
                var levels = new[] { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest }
                    .Distinct().OrderBy(k => Point(k).y).ToArray();
                HumanBodyBones a = levels[0], b = levels[0]; float t = 0;
                for (int i = 1; i < levels.Length; i++)
                {
                    a = levels[i - 1]; b = levels[i]; t = Smooth(Point(a).y, Point(b).y, p.y);
                    if (p.y <= Point(b).y) break;
                }
                float remaining = 1f - head - arm; Need(remaining >= -.00001f, "Garment blend exceeds one.");
                return Weight(Pair(a, (1 - t) * remaining), Pair(b, t * remaining), Pair(HumanBodyBones.Head, head),
                    Pair(p.x >= Hips.x ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm, arm));
            }
        }

        static int RequireFreshImportedSource(Context c)
        {
            Need(AssetDatabase.GetAssetPath(c.Animator.avatar) == c.SourceRestModel,
                "Use the Avatar embedded in this exact retained source model: " + c.Id);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(c.SourceRestModel);
            Need(source && EditorUtility.IsPersistent(source), "The retained imported source model must exist: " + c.SourceRestModel);
            var sourceTransforms = source.GetComponentsInChildren<Transform>(true).ToDictionary(t => Relative(t, source.transform));
            var instanceTransforms = c.Instance.GetComponentsInChildren<Transform>(true).ToDictionary(t => Relative(t, c.Instance.transform));
            Need(sourceTransforms.Count == instanceTransforms.Count, "A fresh source instance cannot add/remove original hierarchy entries.");
            foreach (var pair in sourceTransforms)
            {
                Need(instanceTransforms.TryGetValue(pair.Key, out var actual), "Missing exact original hierarchy path: " + pair.Key);
                var original = pair.Value;
                // Equals compares every float component; this is deliberately
                // stricter than Unity's approximate Vector3/Quaternion ==.
                Need(actual.localPosition.Equals(original.localPosition) && actual.localRotation.Equals(original.localRotation)
                     && actual.localScale.Equals(original.localScale), "Animated/modified source local TRS: " + c.Id + "/" + pair.Key);
            }
            var sourceRenderers = source.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .ToDictionary(r => Relative(r.transform, source.transform));
            Need(sourceRenderers.Count == c.Originals.Length, "A fresh source instance must retain every original skin renderer.");
            foreach (var actual in c.Originals)
            {
                string path = Relative(actual.transform, c.Instance.transform);
                Need(sourceRenderers.TryGetValue(path, out var original) && actual.sharedMesh == original.sharedMesh
                     && actual.sharedMaterials.SequenceEqual(original.sharedMaterials), "Original skin mesh/material reference changed: " + path);
                var expectedBones = original.bones; var actualBones = actual.bones;
                Need(expectedBones.Length == actualBones.Length && expectedBones.All(b => b) && actualBones.All(b => b),
                    "Original source skin bone references are incomplete: " + path);
                for (int i = 0; i < expectedBones.Length; i++)
                    Need(Relative(expectedBones[i], source.transform) == Relative(actualBones[i], c.Instance.transform),
                        "Original source bone palette order changed: " + path);
                Need((original.rootBone == null && actual.rootBone == null)
                     || (original.rootBone && actual.rootBone && Relative(original.rootBone, source.transform) == Relative(actual.rootBone, c.Instance.transform)),
                    "Original source skin root bone changed: " + path);
                for (int i = 0; i < actual.sharedMesh.blendShapeCount; i++)
                    Need(actual.GetBlendShapeWeight(i).Equals(original.GetBlendShapeWeight(i)), "Original source default morph changed: " + path);
            }
            return sourceTransforms.Count;
        }

        static string Relative(Transform value, Transform root)
        {
            var parts = new List<string>();
            while (value != root)
            {
                Need(value && value.parent, "A source bone/renderer must belong to its exact actor hierarchy.");
                parts.Add(value.name); value = value.parent;
            }
            parts.Reverse(); return string.Join("/", parts);
        }

        static void InstallFittedSourceCloth(Context c, List<MeshReport> reports)
        {
            Need(c.Id != "Mary", "The fitted source-cloth split is limited to the four identity-bound source actors.");
            var body = c.Existing("Body"); Mesh original = body.sharedMesh;
            Matrix4x4 actorFromRenderer = c.Instance.transform.worldToLocalMatrix * body.transform.localToWorldMatrix;
            float x = actorFromRenderer.MultiplyVector(Vector3.right).magnitude;
            float y = actorFromRenderer.MultiplyVector(Vector3.up).magnitude;
            float z = actorFromRenderer.MultiplyVector(Vector3.forward).magnitude;
            Need(x > .000001f && Mathf.Abs(x - y) < x * .0001f && Mathf.Abs(x - z) < x * .0001f,
                "Source-cloth clearance requires the retained uniform renderer scale.");
            c.FittedClearance = .0018f * c.Height / 1.7f / x;
            var package = GospelIllustrationFittedCloth.Build(body, c.Id, c.FittedClearance);
            string prefix = c.Root + "/" + c.Id + "/";
            package.BodyRemainder = Persist(package.BodyRemainder, prefix + c.Id + "__IllustrationBodyRemainder.asset", out bool bodyReused);
            package.Shirt = Persist(package.Shirt, prefix + c.Id + "__IllustrationFittedShirt.asset", out bool shirtReused);
            GospelIllustrationFittedCloth.Verify(body, package, c.Id, c.FittedClearance);
            var shirt = c.Added("FittedShirt");
            shirt.transform.SetParent(body.transform.parent, false);
            shirt.transform.localPosition = body.transform.localPosition;
            shirt.transform.localRotation = body.transform.localRotation;
            shirt.transform.localScale = body.transform.localScale;
            shirt.sharedMesh = package.Shirt; shirt.bones = body.bones; shirt.rootBone = body.rootBone;
            shirt.sharedMaterials = new[] { c.FittedLinen }; shirt.quality = body.quality;
            shirt.updateWhenOffscreen = true; shirt.shadowCastingMode = ShadowCastingMode.On; shirt.receiveShadows = true;
            shirt.localBounds = Expanded(package.Shirt.bounds, c.Height * .055f);
            ValidateOwnBindPose(package.Shirt, body.bones.ToList(), shirt);
            body.sharedMesh = package.BodyRemainder;
            c.FittedCloth = package;
            GospelIllustrationFittedCloth.Verify(body, package, c.Id, c.FittedClearance);
            reports.Add(new MeshReport { renderer = body.name, asset = prefix + c.Id + "__IllustrationBodyRemainder.asset",
                originalMesh = Reference(original), geometrySha256 = MeshHash(body.sharedMesh), vertices = body.sharedMesh.vertexCount,
                triangles = body.sharedMesh.triangles.Length / 3, bones = body.bones.Length, reused = bodyReused,
                originalWeightsRetained = true, nativeWeights = body.sharedMesh.GetAllBoneWeights().Length, skinQuality = body.quality.ToString() });
            int maximumInfluences = 0; foreach (byte count in package.Shirt.GetBonesPerVertex()) maximumInfluences = Math.Max(maximumInfluences, count);
            reports.Add(new MeshReport { renderer = shirt.name, asset = prefix + c.Id + "__IllustrationFittedShirt.asset",
                originalMesh = Reference(original), geometrySha256 = MeshHash(shirt.sharedMesh), vertices = shirt.sharedMesh.vertexCount,
                triangles = shirt.sharedMesh.triangles.Length / 3, bones = shirt.bones.Length, reused = shirtReused,
                originalWeightsRetained = true, nativeWeights = shirt.sharedMesh.GetAllBoneWeights().Length,
                maximumInfluences = maximumInfluences, skinQuality = shirt.quality.ToString() });
        }

        static Surface ShoulderDrape(Context c)
        {
            float scale = c.Height / 1.7f;
            // Leave the front open for every adult: a shoulder/back shawl
            // frames the original tunic instead of covering it with a bib.
            return Sheet(33, 64, false, (t, u) =>
            {
                float a = Mathf.Lerp(-1.97f, 1.97f, u), side = Mathf.Abs(Mathf.Sin(a));
                Vector3 direction = new Vector3(Mathf.Sin(a), 0, -Mathf.Cos(a));
                float frontBack = c.Chest.y - (direction.z >= 0 ? .070f : .025f) * scale;
                float hem = Mathf.Lerp(frontBack, (c.LeftArm.y + c.RightArm.y) * .5f - .13f * scale, side * side);
                float y = Mathf.Lerp(c.Neck.y - .022f * scale, hem, Smooth(0, 1, t));
                float radius = OuterTorsoRadius(c, y, direction, .018f * scale);
                float fold = .0012f * scale * Mathf.Sin(a * 8 + t * 1.2f) * t;
                return new Vector3(c.Hips.x, y, c.Neck.z) + direction * (radius + fold);
            }, p => new Vector3(p.x - c.Hips.x, .07f * scale, p.z - c.Neck.z), p => c.Torso(p), .0035f * scale);
        }

        static float OuterTorsoRadius(Context c, float y, Vector3 direction, float clearance)
        {
            float scale = c.Height / 1.7f, nearest = float.PositiveInfinity;
            foreach (var p in c.DrapeEnvelope)
                if (Mathf.Abs(p.x - c.Hips.x) <= c.ShoulderWidth * 1.04f)
                    nearest = Mathf.Min(nearest, Mathf.Abs(p.y - y));
            Need(Finite(nearest), "Actual torso/garment points must fit the original shoulder span.");
            float radius = 0, slab = Mathf.Max(.035f * scale, nearest + .012f * scale);
            foreach (var p in c.DrapeEnvelope)
            {
                if (Mathf.Abs(p.x - c.Hips.x) > c.ShoulderWidth * 1.04f || Mathf.Abs(p.y - y) > slab) continue;
                radius = Mathf.Max(radius, Vector3.Dot(p - new Vector3(c.Hips.x, y, c.Neck.z), direction));
            }
            // The support distance conservatively encloses the actual torso
            // slice. Thickness/folds remain outside the original white cloth.
            return Mathf.Max(.035f * scale, radius) + clearance;
        }

        static Surface RoundedHood(Context c)
        {
            float scale = c.Height / 1.7f;
            float top = c.Hair.Max(p => p.y) + .010f * scale;
            float bottom = c.Chest.y + .035f * scale;
            float crownBase = Mathf.Max(c.Head.y + .035f * scale, top - .14f * scale);
            var scalp = c.Hair.Where(p => p.y >= crownBase - .020f * scale).ToArray();
            Need(scalp.Length > 8 && crownBase < top - .04f * scale, "Actual scalp vertices are required for a rounded hood crown.");
            float centreZ = (scalp.Min(p => p.z) + scalp.Max(p => p.z)) * .5f;
            float crownX = scalp.Max(p => Mathf.Abs(p.x - c.Head.x)) + .012f * scale;
            float crownZ = scalp.Max(p => Mathf.Abs(p.z - centreZ)) + .012f * scale;
            // Forty-eight intervals reserve twenty-four intervals for the dome
            // irrespective of the skull/neck height ratio, and twenty-four for
            // the fall. The rejected profile spent only five rows on the dome.
            return Sheet(49, 64, false, (t, u) =>
            {
                float a = Mathf.Lerp(-1.97f, 1.97f, u);
                Vector3 direction = new Vector3(Mathf.Sin(a), 0, -Mathf.Cos(a));
                float y, rx, rz, z;
                if (t <= .5f)
                {
                    float theta = Mathf.Lerp(.015f, Mathf.PI * .5f, t * 2);
                    y = crownBase + (top - crownBase) * Mathf.Cos(theta);
                    rx = crownX * Mathf.Sin(theta); rz = crownZ * Mathf.Sin(theta); z = centreZ;
                }
                else
                {
                    float q = (t - .5f) * 2;
                    y = Mathf.Lerp(crownBase, bottom, q);
                    Vector3 ellipse = new Vector3(direction.x * crownX, 0, direction.z * crownZ);
                    float baseRadius = ellipse.magnitude;
                    float outside = OuterTorsoRadius(c, y, ellipse.normalized, .024f * scale);
                    float radius = Mathf.Lerp(baseRadius, Mathf.Max(baseRadius, outside), Smooth(.10f, 1, q));
                    float expansion = radius / baseRadius;
                    rx = crownX * expansion; rz = crownZ * expansion;
                    z = Mathf.Lerp(centreZ, c.Neck.z, Smooth(0, 1, q));
                }
                float fold = .0012f * scale * Mathf.Sin(a * 7 + t * 1.4f) * Smooth(.5f, 1, t);
                return new Vector3(c.Head.x + Mathf.Sin(a) * (rx + fold), y,
                    z - Mathf.Cos(a) * (rz + fold));
            }, p => new Vector3(p.x - c.Head.x, Mathf.Max(.025f * scale, p.y - c.Head.y), p.z - c.Head.z),
                p => c.Torso(p, Smooth(c.Neck.y + .005f * scale, c.Head.y + .018f * scale, p.y)), .0030f * scale);
        }

        static void RetainChildSourceTunic(Context c, List<MeshReport> reports)
        {
            Need(c.Id == "JesusChild", "Only the child source tunic policy is handled here.");
            var tunic = c.Existing("TailoredTunic"); var hem = c.Existing("WovenHem");
            Need(hem.sharedMaterials.Length == 1 && hem.sharedMaterial, "Keep the actual single woven-hem palette material.");
            tunic.sharedMaterials = new[] { c.Linen };
            foreach (var renderer in new[] { tunic, hem })
            {
                var mesh = renderer.sharedMesh;
                Need(mesh && mesh.blendShapeCount == 0 && AssetDatabase.GetAssetPath(mesh) == c.SourceRestModel,
                    "Retain the exact original source lower-garment mesh pointer: " + renderer.name);
                int maximumInfluences = 0; foreach (byte count in mesh.GetBonesPerVertex()) maximumInfluences = Math.Max(maximumInfluences, count);
                reports.Add(new MeshReport { renderer = renderer.name, asset = AssetDatabase.GetAssetPath(mesh),
                    originalMesh = Reference(mesh), geometrySha256 = MeshHash(mesh), vertices = mesh.vertexCount,
                    triangles = mesh.triangles.Length / 3, bones = renderer.bones.Length, reused = true,
                    originalWeightsRetained = true, sourceGeometryRetained = true, nativeWeights = mesh.GetAllBoneWeights().Length,
                    maximumInfluences = maximumInfluences, skinQuality = renderer.quality.ToString() });
            }
        }

        static MeshReport OpenChildFringe(Context c)
        {
            Need(c.Id == "JesusChild", "Fringe adaptation is explicitly limited to the child.");
            var renderer = c.Existing("Hair"); var original = renderer.sharedMesh;
            Mesh candidate = GospelIllustrationHairline.BuildEyeClearance(c.Instance, c.Animator, renderer, c.Height, out int adjusted);
            string path = c.Root + "/" + c.Id + "/" + c.Id + "__IllustrationEyeClearanceHair.asset";
            Mesh saved = Persist(candidate, path, out bool reused);
            renderer.sharedMesh = saved; renderer.localBounds = Expanded(saved.bounds, c.Height * .03f);
            return new MeshReport { renderer = renderer.name, asset = path, originalMesh = Reference(original), geometrySha256 = MeshHash(saved),
                vertices = saved.vertexCount, triangles = saved.triangles.Length / 3, bones = renderer.bones.Length,
                reused = reused, originalWeightsRetained = true, adjustedVertices = adjusted };
        }

        static MeshReport Install(Context c, SkinnedMeshRenderer renderer, Surface surface, Material material, bool retainedWeights)
        {
            Mesh original = renderer.sharedMesh;
            Need(!original || original.blendShapeCount == 0, "Only a generated non-morph garment can be replaced: " + renderer.name);
            Matrix4x4 fromRoot = renderer.transform.worldToLocalMatrix * c.Instance.transform.localToWorldMatrix;
            // Unity's imported .asset name is its file stem. Use that exact
            // name before fingerprinting so a later preparation is identical.
            var mesh = new Mesh { name = renderer.name, indexFormat = IndexFormat.UInt32 };
            mesh.vertices = surface.Positions.Select(p => fromRoot.MultiplyPoint3x4(p)).ToArray();
            mesh.uv = surface.Uv.ToArray(); mesh.boneWeights = surface.Weights.ToArray();
            mesh.bindposes = c.Bones.Select(b => b.worldToLocalMatrix * renderer.transform.localToWorldMatrix).ToArray();
            mesh.SetTriangles(surface.Triangles, 0); mesh.RecalculateNormals();
            var normals = mesh.normals;
            foreach (var seam in surface.Seams)
            {
                Vector3 normal = (normals[seam.x] + normals[seam.y]).normalized;
                normals[seam.x] = normals[seam.y] = normal;
            }
            mesh.normals = normals; mesh.RecalculateTangents(); mesh.RecalculateBounds();
            ValidateSkin(mesh, c.Bones.Count);
            ValidateOwnBindPose(mesh, c.Bones, renderer);
            string path = c.Root + "/" + c.Id + "/" + renderer.name + ".asset";
            Mesh saved = Persist(mesh, path, out bool reused);
            ValidateSkin(saved, c.Bones.Count);
            ValidateOwnBindPose(saved, c.Bones, renderer);
            renderer.sharedMesh = saved; renderer.bones = c.Bones.ToArray(); renderer.rootBone = c.Human[HumanBodyBones.Hips];
            renderer.sharedMaterials = new[] { material }; renderer.quality = SkinQuality.Bone4;
            renderer.updateWhenOffscreen = true; renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            renderer.localBounds = Expanded(saved.bounds, c.Height * .10f);
            return new MeshReport { renderer = renderer.name, asset = path, originalMesh = Reference(original), geometrySha256 = MeshHash(saved),
                vertices = saved.vertexCount, triangles = saved.triangles.Length / 3, bones = renderer.bones.Length, reused = reused, originalWeightsRetained = retainedWeights };
        }

        sealed class Surface
        {
            public readonly List<Vector3> Positions = new List<Vector3>();
            public readonly List<Vector2> Uv = new List<Vector2>();
            public readonly List<BoneWeight> Weights = new List<BoneWeight>();
            public readonly List<int> Triangles = new List<int>();
            public readonly List<Vector2Int> Seams = new List<Vector2Int>();
            public void Quad(int a, int b, int c, int d, Vector3 outward)
            {
                if (Vector3.Dot(Vector3.Cross(Positions[b] - Positions[a], Positions[c] - Positions[a]), outward) < 0)
                { int swap = b; b = d; d = swap; }
                Triangles.Add(a); Triangles.Add(b); Triangles.Add(c); Triangles.Add(a); Triangles.Add(c); Triangles.Add(d);
            }
        }

        static Surface Sheet(int rows, int segments, bool closed, Func<float, float, Vector3> position,
            Func<Vector3, Vector3> normal, Func<Vector3, BoneWeight> weight, float thickness)
        {
            var s = new Surface(); int columns = segments + 1, layer = rows * columns;
            for (int shell = 0; shell < 2; shell++)
                for (int row = 0; row < rows; row++)
                    for (int col = 0; col < columns; col++)
                    {
                        float t = row / (float)(rows - 1), u = col / (float)segments;
                        Vector3 outer = position(t, u), n = normal(outer).normalized;
                        Need(Finite(outer.x) && Finite(outer.y) && Finite(outer.z) && n.sqrMagnitude > .9f, "Finite nondegenerate garment surface required.");
                        s.Positions.Add(outer - n * (shell == 0 ? 0 : thickness));
                        s.Uv.Add(new Vector2(u, 1 - t)); s.Weights.Add(weight(outer));
                    }
            for (int row = 0; row < rows - 1; row++)
                for (int col = 0; col < segments; col++)
                {
                    int a = row * columns + col, b = a + columns;
                    Vector3 n = normal((s.Positions[a] + s.Positions[b + 1]) * .5f);
                    s.Quad(a, b, b + 1, a + 1, n);
                    s.Quad(a + layer, a + 1 + layer, b + 1 + layer, b + layer, -n);
                }
            // Real geometry closes both thickness layers at every open boundary.
            for (int col = 0; col < segments; col++)
            {
                float u = (col + .5f) / segments;
                Vector3 firstOut = position(0, u) - position(1f / (rows - 1), u);
                Vector3 lastOut = position(1, u) - position(1 - 1f / (rows - 1), u);
                s.Quad(col, col + 1, col + 1 + layer, col + layer, firstOut);
                int a = (rows - 1) * columns + col;
                s.Quad(a, a + layer, a + 1 + layer, a + 1, lastOut);
            }
            if (!closed)
                for (int row = 0; row < rows - 1; row++)
                {
                    int a = row * columns, b = a + columns, d = a + segments, e = d + columns;
                    float t = (row + .5f) / (rows - 1);
                    s.Quad(a, a + layer, b + layer, b, position(t, 0) - position(t, 1f / segments));
                    s.Quad(d, e, e + layer, d + layer, position(t, 1) - position(t, 1 - 1f / segments));
                }
            else
                for (int row = 0; row < rows; row++)
                    for (int shell = 0; shell < 2; shell++)
                        s.Seams.Add(new Vector2Int(shell * layer + row * columns, shell * layer + row * columns + segments));
            return s;
        }

        static Material DerivedMaterial(Context c, string suffix, string role, bool openSourceCloth = false)
        {
            var matches = c.Originals.SelectMany(r => r.sharedMaterials).Where(m => m && m.name == c.Id + "__" + suffix).Distinct().ToArray();
            Need(matches.Length == 1 && matches[0].HasProperty("_Color"), "Exact existing garment palette material required: " + c.Id + "/" + suffix);
            Color color = matches[0].GetColor("_Color");
            string path = c.Root + "/" + c.Id + "/" + c.Id + "__Illustration" + role + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material)
                Need(material.HasProperty("_Color") && Approximately(material.GetColor("_Color"), color)
                     && material.shader && (openSourceCloth ? material.shader.name == "FirstGame/GospelNpr/lilToonOpaque"
                         : material.shader.name.Contains("lilToon")), "Derived material differs; use a new outputRoot version: " + path);
            else
            {
                Need(AssetDatabase.LoadMainAssetAtPath(path) == null, "Never replace another derived asset type: " + path);
                material = GospelNprMaterialFactory.CreateTailoredGarment(matches[0], c.Id + "__Illustration" + role, role == "Mantle", path, openSourceCloth);
            }
            c.MaterialPaths.Add(path); return material;
        }

        static Mesh Persist(Mesh candidate, string path, out bool reused)
        {
            Folder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var prior = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (prior)
            {
                Need(MeshHash(prior) == MeshHash(candidate), "Derived mesh differs; preserve it and choose a new outputRoot version: " + path
                    + " " + MeshDifference(prior, candidate));
                Object.DestroyImmediate(candidate); reused = true; return prior;
            }
            Need(AssetDatabase.LoadMainAssetAtPath(path) == null, "Never replace another asset type: " + path);
            AssetDatabase.CreateAsset(candidate, path); reused = false; return candidate;
        }

        static string MeshDifference(Mesh a, Mesh b)
        {
            float Max(Vector3[] x, Vector3[] y) => x.Length != y.Length ? float.PositiveInfinity
                : x.Zip(y, (p, q) => (p - q).magnitude).DefaultIfEmpty(0).Max();
            var aw = a.boneWeights; var bw = b.boneWeights;
            float weight = aw.Length != bw.Length ? float.PositiveInfinity : aw.Zip(bw, (p, q) =>
                Mathf.Max(Mathf.Abs(p.weight0-q.weight0), Mathf.Abs(p.weight1-q.weight1), Mathf.Abs(p.weight2-q.weight2), Mathf.Abs(p.weight3-q.weight3))).DefaultIfEmpty(0).Max();
            int indices = aw.Length != bw.Length ? -1 : aw.Zip(bw, (p, q) =>
                p.boneIndex0 != q.boneIndex0 || p.boneIndex1 != q.boneIndex1 || p.boneIndex2 != q.boneIndex2 || p.boneIndex3 != q.boneIndex3 ? 1 : 0).Sum();
            var ap = a.bindposes; var bp = b.bindposes;
            float bind = ap.Length != bp.Length ? float.PositiveInfinity : ap.Zip(bp, (p, q) =>
                Enumerable.Range(0, 16).Select(i => Mathf.Abs(p[i] - q[i])).Max()).DefaultIfEmpty(0).Max();
            return "vertices=" + a.vertexCount + "/" + b.vertexCount + " positionMax=" + Max(a.vertices,b.vertices)
                + " normalMax=" + Max(a.normals,b.normals) + " uvEqual=" + a.uv.SequenceEqual(b.uv)
                + " tangentEqual=" + a.tangents.SequenceEqual(b.tangents) + " bounds=" + a.bounds + "/" + b.bounds
                + " weightMax=" + weight + " weightIndicesChanged=" + indices + " bindMax=" + bind
                + " bonesPerVertexEqual=" + a.GetBonesPerVertex().ToArray().SequenceEqual(b.GetBonesPerVertex().ToArray())
                + " allWeights=" + a.GetAllBoneWeights().Length + "/" + b.GetAllBoneWeights().Length;
        }

        sealed class Protection
        {
            readonly Avatar avatar;
            readonly RuntimeAnimatorController controller;
            readonly Dictionary<Mesh, string> meshHashes;
            readonly Dictionary<Transform, (Transform parent, Vector3 position, Quaternion rotation, Vector3 scale)> transforms;
            readonly Dictionary<SkinnedMeshRenderer, (Mesh mesh, Transform[] bones, Material[] materials, Transform rootBone, float[] blendWeights)> renderers;
            public Protection(Context c)
            {
                avatar = c.Animator.avatar; controller = c.Animator.runtimeAnimatorController;
                meshHashes = c.Originals.Select(r => r.sharedMesh).Where(m => m).Distinct().ToDictionary(m => m, MeshHash);
                transforms = c.Instance.GetComponentsInChildren<Transform>(true).ToDictionary(t => t, t => (t.parent, t.localPosition, t.localRotation, t.localScale));
                renderers = c.Originals.ToDictionary(r => r, r => (r.sharedMesh, r.bones, r.sharedMaterials, r.rootBone,
                    Enumerable.Range(0, r.sharedMesh.blendShapeCount).Select(r.GetBlendShapeWeight).ToArray()));
            }
            public void Verify(Context c)
            {
                Need(c.Animator.avatar == avatar && c.Animator.runtimeAnimatorController == controller, "The original Avatar/controller must remain unchanged.");
                foreach (var pair in meshHashes) Need(MeshHash(pair.Key) == pair.Value, "Original FBX mesh data was changed: " + pair.Key.name);
                foreach (var pair in transforms)
                    Need(pair.Key && pair.Key.parent == pair.Value.parent && pair.Key.localPosition == pair.Value.position
                         && pair.Key.localRotation == pair.Value.rotation && pair.Key.localScale == pair.Value.scale,
                        "Original hierarchy/rest transforms changed.");
                foreach (var pair in renderers)
                {
                    string name = pair.Key.name;
                    bool garment = new[] { "TailoredTunic", "WovenHem", "ShoulderMantle", "OpenVeil" }.Any(s => name == c.Id + "__" + s)
                        || name.StartsWith(c.Id + "__Illustration", StringComparison.Ordinal);
                    bool childFringe = c.Id == "JesusChild" && name == "JesusChild__Hair";
                    if (childFringe)
                        Need(pair.Key.sharedMesh && pair.Key.sharedMesh.blendShapeCount == 0
                             && AssetDatabase.GetAssetPath(pair.Key.sharedMesh) == c.Root + "/JesusChild/JesusChild__IllustrationEyeClearanceHair.asset"
                             && pair.Key.bones.SequenceEqual(pair.Value.bones) && pair.Key.sharedMaterials.SequenceEqual(pair.Value.materials)
                             && pair.Key.rootBone == pair.Value.rootBone,
                            "Only the new child fringe mesh pointer may change; original rig and palette must remain.");
                    else if (c.Id != "Mary" && name == c.Id + "__Body")
                    {
                        Need(c.FittedCloth != null && c.FittedCloth.OriginalMesh == pair.Value.mesh
                             && pair.Key.sharedMesh == c.FittedCloth.BodyRemainder
                             && AssetDatabase.GetAssetPath(pair.Key.sharedMesh) == c.Root + "/" + c.Id + "/" + c.Id + "__IllustrationBodyRemainder.asset"
                             && pair.Key.bones.SequenceEqual(pair.Value.bones) && pair.Key.sharedMaterials.SequenceEqual(pair.Value.materials)
                             && pair.Key.rootBone == pair.Value.rootBone,
                            "Only the verified fitted-cloth partition may replace the C4 Body pointer.");
                        GospelIllustrationFittedCloth.Verify(pair.Key, c.FittedCloth, c.Id, c.FittedClearance);
                    }
                    else if (c.Id == "JesusChild" && (name == "JesusChild__TailoredTunic" || name == "JesusChild__WovenHem"))
                    {
                        Need(pair.Key.sharedMesh == pair.Value.mesh && pair.Key.bones.SequenceEqual(pair.Value.bones)
                             && pair.Key.rootBone == pair.Value.rootBone,
                            "V5 retains the exact source child tunic/hem mesh and original binding.");
                        Need(name == "JesusChild__TailoredTunic"
                                ? pair.Key.sharedMaterials.Length == 1 && pair.Key.sharedMaterial == c.Linen
                                : pair.Key.sharedMaterials.SequenceEqual(pair.Value.materials),
                            "Only the child tunic's derived Linen material may differ; preserve the woven hem palette.");
                    }
                    else if (!garment)
                        Need(pair.Key.sharedMesh == pair.Value.mesh && pair.Key.bones.SequenceEqual(pair.Value.bones)
                             && pair.Key.sharedMaterials.SequenceEqual(pair.Value.materials) && pair.Key.rootBone == pair.Value.rootBone,
                            "A protected original renderer was replaced: " + name);
                    Need(pair.Key.sharedMesh.blendShapeCount == pair.Value.blendWeights.Length
                         && Enumerable.Range(0, pair.Value.blendWeights.Length).All(i => pair.Key.GetBlendShapeWeight(i) == pair.Value.blendWeights[i]),
                        "Original facial expression/default blendshape weights changed: " + name);
                }
                Need(c.Bones.All(transforms.ContainsKey), "New skeleton bones are forbidden.");
            }
        }

        static string MeshHash(Mesh mesh)
        {
            using (var data = new MemoryStream())
            using (var writer = new BinaryWriter(data))
            {
                writer.Write(mesh.name); writer.Write(mesh.vertexCount); writer.Write(mesh.subMeshCount); writer.Write((int)mesh.indexFormat);
                Write(writer, mesh.bounds.center); Write(writer, mesh.bounds.size);
                foreach (var p in mesh.vertices) Write(writer, p);
                foreach (var p in mesh.normals) Write(writer, p);
                foreach (var p in mesh.tangents) { writer.Write(p.x); writer.Write(p.y); writer.Write(p.z); writer.Write(p.w); }
                for (int channel = 0; channel < 8; channel++)
                {
                    var attribute = (VertexAttribute)((int)VertexAttribute.TexCoord0 + channel);
                    bool exists = mesh.HasVertexAttribute(attribute); writer.Write(exists);
                    if (!exists) continue;
                    writer.Write(mesh.GetVertexAttributeDimension(attribute)); writer.Write((int)mesh.GetVertexAttributeFormat(attribute));
                    var uv = new List<Vector4>(); mesh.GetUVs(channel, uv); writer.Write(uv.Count);
                    foreach (var p in uv) { writer.Write(p.x); writer.Write(p.y); writer.Write(p.z); writer.Write(p.w); }
                }
                bool colors = mesh.HasVertexAttribute(VertexAttribute.Color); writer.Write(colors);
                if (colors)
                {
                    writer.Write(mesh.GetVertexAttributeDimension(VertexAttribute.Color)); writer.Write((int)mesh.GetVertexAttributeFormat(VertexAttribute.Color));
                    foreach (var p in mesh.colors) { writer.Write(p.r); writer.Write(p.g); writer.Write(p.b); writer.Write(p.a); }
                }
                foreach (var w in mesh.boneWeights)
                {
                    writer.Write(w.boneIndex0); writer.Write(w.boneIndex1); writer.Write(w.boneIndex2); writer.Write(w.boneIndex3);
                    writer.Write(w.weight0); writer.Write(w.weight1); writer.Write(w.weight2); writer.Write(w.weight3);
                }
                var perVertex = mesh.GetBonesPerVertex(); writer.Write(perVertex.Length);
                for (int i = 0; i < perVertex.Length; i++) writer.Write(perVertex[i]);
                var allWeights = mesh.GetAllBoneWeights(); writer.Write(allWeights.Length);
                for (int i = 0; i < allWeights.Length; i++) { writer.Write(allWeights[i].boneIndex); writer.Write(allWeights[i].weight); }
                foreach (var m in mesh.bindposes) for (int i = 0; i < 16; i++) writer.Write(m[i]);
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    writer.Write((int)mesh.GetTopology(i)); writer.Write(mesh.GetBaseVertex(i));
                    int[] indices = mesh.GetIndices(i); writer.Write(indices.Length); foreach (int index in indices) writer.Write(index);
                }
                writer.Write(mesh.blendShapeCount);
                var v = new Vector3[mesh.vertexCount]; var n = new Vector3[v.Length]; var t = new Vector3[v.Length];
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    writer.Write(mesh.GetBlendShapeName(i)); writer.Write(mesh.GetBlendShapeFrameCount(i));
                    for (int frame = 0; frame < mesh.GetBlendShapeFrameCount(i); frame++)
                    {
                        writer.Write(mesh.GetBlendShapeFrameWeight(i, frame)); mesh.GetBlendShapeFrameVertices(i, frame, v, n, t);
                        for (int j = 0; j < v.Length; j++) { Write(writer, v[j]); Write(writer, n[j]); Write(writer, t[j]); }
                    }
                }
                writer.Flush(); using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(data.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }

        static void ValidateSkin(Mesh mesh, int bones)
        {
            Need(mesh.vertexCount == mesh.boneWeights.Length && mesh.bindposes.Length == bones, "Every derived vertex needs its exact original-bone palette.");
            foreach (var w in mesh.boneWeights)
            {
                float sum = w.weight0 + w.weight1 + w.weight2 + w.weight3;
                Need(Finite(sum) && Mathf.Abs(sum - 1) < .0001f && w.weight0 > 0 && w.weight1 >= 0 && w.weight2 >= 0 && w.weight3 >= 0,
                    "Derived weights must be finite, positive and normalized.");
                Need(w.boneIndex0 >= 0 && w.boneIndex0 < bones && w.boneIndex1 >= 0 && w.boneIndex1 < bones
                     && w.boneIndex2 >= 0 && w.boneIndex2 < bones && w.boneIndex3 >= 0 && w.boneIndex3 < bones, "Derived influence index outside original palette.");
            }
        }
        static void ValidateOwnBindPose(Mesh mesh, List<Transform> bones, SkinnedMeshRenderer renderer)
        {
            Need(mesh.bindposes.Length == bones.Count, "Every new garment bind pose must match its original-bone palette.");
            var bindposes = mesh.bindposes;
            for (int bone = 0; bone < bones.Count; bone++)
            {
                Matrix4x4 roundTrip = renderer.transform.worldToLocalMatrix * bones[bone].localToWorldMatrix * bindposes[bone];
                for (int element = 0; element < 16; element++)
                    Need(Finite(roundTrip[element]) && Mathf.Abs(roundTrip[element] - Matrix4x4.identity[element]) < .004f,
                        "A new garment must retain the strict .004 bind-pose round trip: " + renderer.name + "/" + bones[bone].name);
            }
        }
        static float Width(Vector3[] points, float y, float slab, float center, float clip) => points.Where(p => Mathf.Abs(p.y - y) <= slab && Mathf.Abs(p.x - center) < clip)
            .Select(p => Mathf.Abs(p.x - center)).DefaultIfEmpty(0).Max();
        static float Depth(Vector3[] points, float y, float slab, float center, float clip) => points.Where(p => Mathf.Abs(p.y - y) <= slab && Mathf.Abs(p.x) < clip)
            .Select(p => Mathf.Abs(p.z - center)).DefaultIfEmpty(0).Max();
        static Bounds Expanded(Bounds value, float amount) { value.Expand(amount); return value; }
        static string Reference(Mesh mesh) => mesh ? AssetDatabase.GetAssetPath(mesh) + "#" + mesh.name : "new-derived-renderer";
        static KeyValuePair<HumanBodyBones, float> Pair(HumanBodyBones bone, float value) => new KeyValuePair<HumanBodyBones, float>(bone, value);
        static float Smooth(float a, float b, float value) => Mathf.Abs(b - a) < .000001f ? (value >= b ? 1 : 0) : Mathf.SmoothStep(0, 1, Mathf.Clamp01((value - a) / (b - a)));
        static bool Approximately(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) + Mathf.Abs(a.a - b.a) < .0001f;
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static void Write(BinaryWriter writer, Vector3 value) { writer.Write(value.x); writer.Write(value.y); writer.Write(value.z); }
        static void SetWeight(ref BoneWeight w, int slot, int index, float value)
        {
            if (slot == 0) { w.boneIndex0 = index; w.weight0 = value; }
            else if (slot == 1) { w.boneIndex1 = index; w.weight1 = value; }
            else if (slot == 2) { w.boneIndex2 = index; w.weight2 = value; }
            else { w.boneIndex3 = index; w.weight3 = value; }
        }
        static void Folder(string path)
        {
            string current = "Assets";
            foreach (string part in path.Substring("Assets".Length).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                Need(part != "." && part != "..", "Canonical asset folder required.");
                if (!AssetDatabase.IsValidFolder(current + "/" + part)) AssetDatabase.CreateFolder(current, part);
                current += "/" + part;
            }
        }
        static void Need(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
#endif
