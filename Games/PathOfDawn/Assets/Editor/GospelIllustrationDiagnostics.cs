#if UNITY_EDITOR
// Root copies this source into the Editor assembly and invokes Report explicitly.
// It neither imports nor saves assets. Only fresh scene clones are modified.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FirstGame.Editor
{
    public static class GospelIllustrationDiagnostics
    {
        const string Models = "Assets/Resources/Art/GospelNpr/Models/";
        const string Characters = "Assets/Resources/Art/GospelNpr/Characters/";
        const string V1Child = "Assets/Art/GospelIllustration20261117/v1/JesusChild/";
        static readonly string[] Ids = { "Mary", "JesusChild", "JesusAdult", "Joseph", "Teacher" };

        [Serializable] public sealed class BoneMismatch
        {
            public string renderer, bone;
            public int boneIndex, worstElement, positiveInfluences, affectedVertices;
            public float maxIdentityError;
            public double weightSum;
            public float[] rendererLocalRestMatrix;
        }
        [Serializable] public sealed class MeshInfo
        {
            public string renderer, mesh, asset, indexFormat;
            public string[] vertexAttributes;
            public int vertices, bones, bindposes, blendShapes, allWeights;
        }
        [Serializable] public sealed class ActorInfo
        {
            public string id, model, avatar, controller, error;
            public bool freshSourceTransformsMatch;
            public string[] transformMismatches;
            public MeshInfo[] meshes;
            public BoneMismatch[] nonidentityBones;
            public string childApplyDiagnostic;
        }
        [Serializable] public sealed class DiagnosticReport
        {
            public string status = "diagnostic-only-not-preparation-or-visual-approval";
            public string utc;
            public ActorInfo[] actors;
        }

        public static void Report()
        {
            Need(!Application.isPlaying, "Run this diagnostic in the Editor before play mode.");
            string output = Environment.GetEnvironmentVariable("FIRSTGAME_VALIDATION_ROOT");
            Need(!string.IsNullOrEmpty(output), "Root must supply the current owned validation output root.");
            var rows = new List<ActorInfo>();
            foreach (string id in Ids) rows.Add(Inspect(id));
            var report = new DiagnosticReport { utc = DateTime.UtcNow.ToString("O"), actors = rows.ToArray() };
            string folder = Path.Combine(Path.GetFullPath(output), "Diagnostics");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "world-bind-and-v1-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ")
                + "-" + Guid.NewGuid().ToString("N") + ".json");
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(file)) writer.WriteLine(JsonUtility.ToJson(report, true));
            Debug.Log("FIRSTGAME_ILLUSTRATION_DIAGNOSTICS_COMPLETE " + path + " actors=" + rows.Count);
        }

        static ActorInfo Inspect(string id)
        {
            var row = new ActorInfo { id = id, model = Models + id + ".fbx" };
            GameObject clone = null;
            try
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(row.model);
                Need(source, "Exact imported source model is missing: " + row.model);
                var avatars = AssetDatabase.LoadAllAssetsAtPath(row.model).OfType<Avatar>().Where(a => a.isHuman && a.isValid).ToArray();
                Need(avatars.Length == 1, "One exact imported Humanoid Avatar is required: " + id);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Characters + id + ".prefab");
                var savedAnimator = prefab ? prefab.GetComponent<Animator>() : null;
                Need(savedAnimator && savedAnimator.runtimeAnimatorController, "Existing approved locomotion controller is missing: " + id);
                clone = Object.Instantiate(source); clone.name = id;
                var animator = clone.GetComponent<Animator>() ?? clone.AddComponent<Animator>();
                animator.avatar = avatars[0]; animator.runtimeAnimatorController = savedAnimator.runtimeAnimatorController;
                animator.applyRootMotion = false;
                row.avatar = AssetDatabase.GetAssetPath(animator.avatar);
                row.controller = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController);
                row.transformMismatches = CompareSourceTransforms(source.transform, clone.transform);
                row.freshSourceTransformsMatch = row.transformMismatches.Length == 0;
                var meshRows = new List<MeshInfo>(); var bad = new List<BoneMismatch>();
                foreach (var renderer in clone.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    InspectSkin(renderer, meshRows, bad);
                row.meshes = meshRows.ToArray(); row.nonidentityBones = bad.ToArray();
                Debug.Log("FIRSTGAME_ILLUSTRATION_BIND_DIAGNOSTIC " + id + " fresh=" + row.freshSourceTransformsMatch
                    + " nonidentity=" + bad.Count + " weighted=" + bad.Count(b => b.positiveInfluences > 0));
                foreach (var bone in bad)
                    Debug.Log("FIRSTGAME_ILLUSTRATION_BIND_BONE " + id + " " + JsonUtility.ToJson(bone));
                if (id == "JesusChild")
                {
                    // The original one-off V1 comparison is preserved in
                    // Rig01. A newer generator must never write into V1.
                    row.childApplyDiagnostic = "Historical V1 comparison not rerun; source-rest inspection only.";
                    Debug.Log("FIRSTGAME_ILLUSTRATION_CHILD_V1_DIAGNOSTIC " + row.childApplyDiagnostic);
                }
            }
            catch (Exception exception) { row.error = exception.ToString(); Debug.Log("FIRSTGAME_ILLUSTRATION_DIAGNOSTIC_ERROR " + id + " " + row.error); }
            finally { if (clone) Object.DestroyImmediate(clone); }
            return row;
        }

        static void InspectSkin(SkinnedMeshRenderer renderer, List<MeshInfo> rows, List<BoneMismatch> bad)
        {
            var mesh = renderer.sharedMesh;
            Need(mesh && mesh.isReadable, "Readable imported skin required: " + renderer.name);
            var bones = renderer.bones; var bindposes = mesh.bindposes;
            Need(bones.Length > 0 && bindposes.Length == bones.Length && bones.All(b => b), "Exact bone palette required: " + renderer.name);
            var counts = mesh.GetBonesPerVertex(); var weights = mesh.GetAllBoneWeights();
            Need(counts.Length == mesh.vertexCount, "Full per-vertex weights are required.");
            var influences = new int[bones.Length]; var vertices = new int[bones.Length]; var sums = new double[bones.Length];
            int cursor = 0;
            for (int vertex = 0; vertex < counts.Length; vertex++)
            {
                var seen = new HashSet<int>();
                for (int slot = 0; slot < counts[vertex]; slot++)
                {
                    Need(cursor < weights.Length, "Truncated native skin weights.");
                    var weight = weights[cursor++];
                    Need(weight.boneIndex >= 0 && weight.boneIndex < bones.Length && Finite(weight.weight) && weight.weight >= 0,
                        "Invalid source skin influence.");
                    if (weight.weight <= 0) continue;
                    influences[weight.boneIndex]++; sums[weight.boneIndex] += weight.weight;
                    if (seen.Add(weight.boneIndex)) vertices[weight.boneIndex]++;
                }
            }
            Need(cursor == weights.Length, "Unexpected extra native skin weights.");
            rows.Add(new MeshInfo { renderer = renderer.name, mesh = mesh.name, asset = AssetDatabase.GetAssetPath(mesh),
                indexFormat = mesh.indexFormat.ToString(), vertices = mesh.vertexCount, bones = bones.Length,
                bindposes = bindposes.Length, blendShapes = mesh.blendShapeCount, allWeights = weights.Length,
                vertexAttributes = mesh.GetVertexAttributes().Select(a => a.attribute + ":" + a.format + "x" + a.dimension + "@" + a.stream).ToArray() });
            for (int bone = 0; bone < bones.Length; bone++)
            {
                Matrix4x4 matrix = renderer.transform.worldToLocalMatrix * bones[bone].localToWorldMatrix * bindposes[bone];
                float maximum = 0; int element = 0; var values = new float[16];
                for (int i = 0; i < 16; i++)
                {
                    Need(Finite(matrix[i]), "Nonfinite imported rest matrix."); values[i] = matrix[i];
                    float error = Mathf.Abs(matrix[i] - Matrix4x4.identity[i]);
                    if (error > maximum) { maximum = error; element = i; }
                }
                if (maximum < .004f) continue;
                bad.Add(new BoneMismatch { renderer = renderer.name, bone = bones[bone].name, boneIndex = bone,
                    worstElement = element, maxIdentityError = maximum, positiveInfluences = influences[bone],
                    affectedVertices = vertices[bone], weightSum = sums[bone], rendererLocalRestMatrix = values });
            }
        }

        static string[] CompareSourceTransforms(Transform source, Transform clone)
        {
            var originals = source.GetComponentsInChildren<Transform>(true).ToDictionary(t => Relative(t, source));
            var copies = clone.GetComponentsInChildren<Transform>(true).ToDictionary(t => Relative(t, clone));
            var mismatches = new List<string>();
            foreach (var pair in originals)
            {
                if (!copies.TryGetValue(pair.Key, out var copy)) { mismatches.Add("missing:" + pair.Key); continue; }
                var original = pair.Value;
                if (copy.localPosition != original.localPosition || copy.localRotation != original.localRotation || copy.localScale != original.localScale)
                    mismatches.Add("localTRS:" + pair.Key);
            }
            foreach (string path in copies.Keys) if (!originals.ContainsKey(path)) mismatches.Add("extra:" + path);
            return mismatches.ToArray();
        }

        static string Relative(Transform value, Transform root)
        {
            var parts = new List<string>();
            while (value != root) { Need(value && value.parent, "Transform must belong to its exact source hierarchy."); parts.Add(value.name); value = value.parent; }
            parts.Reverse(); return string.Join("/", parts);
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static void Need(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
#endif
