#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Firstgame.GospelNpr.Editor
{
    /// <summary>Read-only measurements of the saved child yoke against actual drawn linen.</summary>
    public static class GospelNecklineDiagnostic
    {
        [Serializable] sealed class Weight { public int index; public string bone, path; public float weight, percent; public bool withinFour; }
        [Serializable] sealed class Vertex { public int index; public Weight[] weights; }
        [Serializable] sealed class Triangle { public int slot, triangle, a, b, c; }
        [Serializable] sealed class Anchor { public int column, topVertex, bottomVertex; public Triangle sourceTriangle; public Vector3 bary, neutralCloth, neutralTop, neutralBottom; public Vertex[] sourceVertices; public Weight[] combined, yokeTopWeights, yokeBottomWeights; public float outsideFourPercent; }
        [Serializable] sealed class Bone { public int index; public string name, path; public bool withinFour; public float[] bindpose, boneToActor, skinToActor; }
        [Serializable] sealed class Frame { public string renderer, mesh, rootBone, quality; public Vector3 lossyScale; public float[] rendererToActor, bakeToActor; public Bone[] bones; }
        [Serializable] sealed class Row { public int column, yokeVertex; public string side; public Vector3 actualYoke, mappedNeutralAnchor, normal, gap, tangent; public float distance, signedNormal, tangentDistance, distanceChangeFromNeutral; }
        [Serializable] sealed class Stats { public float maximumDistance, meanDistance, maximumAbsoluteNormal, meanAbsoluteNormal, maximumTangent, meanTangent; }
        [Serializable] sealed class Clip { public string name, asset; public float lengthSeconds, weight; }
        [Serializable] sealed class Pose { public string label; public float secondsAfterRebind, normalizedStateTime; public int stateHash; public Clip[] clips; public Frame clothFrame, yokeFrame; public Row[] rows, worstTwelve; public Stats top, bottom; }
        [Serializable] sealed class Report { public string actor, scope, controller, qualitySkinWeights; public int clothNativeMaximumInfluences, yokeNativeMaximumInfluences; public string[] linenMaterials; public Anchor[] anchors; public Pose[] poses; public bool originalPrefabAndMeshesUnchanged; }
        public static void Capture()
        {
            Need(!Application.isPlaying && !UnityEditor.BuildPipeline.isBuildingPlayer, "Use an idle Editor; this is not a renderer/preparation step.");
            string attempt = Environment.GetEnvironmentVariable("FIRSTGAME_NECKLINE_DIAGNOSTIC_ATTEMPT"), configured = Environment.GetEnvironmentVariable("FIRSTGAME_VALIDATION_ROOT");
            Need(!string.IsNullOrEmpty(attempt) && Regex.IsMatch(attempt, "^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$") && !string.IsNullOrEmpty(configured) && Path.IsPathRooted(configured), "Explicit safe attempt and absolute output required.");
            string output = Path.Combine(Path.GetFullPath(configured), "NecklineDiagnostics", attempt), repository = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            Need(output.StartsWith(Path.Combine(repository, "Artifacts", "Validation") + Path.DirectorySeparatorChar, StringComparison.Ordinal), "Repository validation output required.");
            for (var dir = new DirectoryInfo(output); dir != null; dir = dir.Parent) if (dir.Exists) Need((dir.Attributes & FileAttributes.ReparsePoint) == 0, "Linked output ancestor is forbidden.");
            Need(!Directory.Exists(output) && !File.Exists(output), "Preserve an existing/partial diagnostic.");
            var prefab = Resources.Load<GameObject>("Art/GospelNpr/Characters/JesusChild"); Need(prefab, "Current saved child prefab required.");
            string originalPrefab = EditorJsonUtility.ToJson(prefab); var originalMeshes = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => r.sharedMesh).Distinct().ToDictionary(m => m, GospelCharacterRefinement.MeshHash);
            GameObject actor = null;
            try
            {
                actor = Object.Instantiate(prefab); actor.name = "JesusChild"; actor.hideFlags = HideFlags.HideAndDontSave;
                var animator = actor.GetComponent<Animator>(); Need(animator && animator.avatar && animator.avatar.isHuman && animator.avatar.isValid && animator.runtimeAnimatorController, "Saved real Humanoid/controller required."); animator.applyRootMotion = false;
                var skins = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true); var cloth = skins.Single(r => r.name == "JesusChild__IllustrationFittedShirt"); var yoke = skins.Single(r => r.name == "JesusChild__HumanNecklineV1");
                Need(cloth.enabled && cloth.gameObject.activeInHierarchy && !cloth.forceRenderingOff && yoke.enabled && yoke.gameObject.activeInHierarchy && !yoke.forceRenderingOff && yoke.sharedMesh.vertexCount == 600, "Actual drawn FittedShirt and exact V9 576+24-vertex yoke required; do not guess another layout.");
                var triangles = new List<Triangle>(); var slots = cloth.sharedMaterials;
                for (int slot = 0; slot < slots.Length; slot++) if (slots[slot] && slots[slot].name.IndexOf("Linen", StringComparison.OrdinalIgnoreCase) >= 0)
                { var indices = cloth.sharedMesh.GetTriangles(slot); for (int i = 0; i < indices.Length; i += 3) triangles.Add(new Triangle { slot = slot, triangle = i / 3, a = indices[i], b = indices[i + 1], c = indices[i + 2] }); }
                Need(triangles.Count > 0, "Actual FittedShirt Linen draw triangles required.");
                var allowed = new HashSet<Transform>(new[] { HumanBodyBones.Chest, HumanBodyBones.Neck, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm }.Select(animator.GetBoneTransform)); Need(!allowed.Contains(null), "Four actual human transforms required.");
                var clothWeights = GospelCharacterRefinement.VertexWeights(cloth.sharedMesh); var yokeWeights = GospelCharacterRefinement.VertexWeights(yoke.sharedMesh);
                Vector3[] neutralCloth = Bake(actor, cloth), neutralYoke = Bake(actor, yoke); var anchors = new Anchor[48];
                for (int column = 0; column < 48; column++)
                {
                    int top = 240 + column, bottom = 528 + column; float best = float.PositiveInfinity; Triangle chosen = null; Vector3 bary = default;
                    foreach (var tri in triangles) { var a = neutralCloth[tri.a]; var b = neutralCloth[tri.b]; var c = neutralCloth[tri.c]; if (Vector3.Cross(b - a, c - a).sqrMagnitude < 1e-18f) continue; var candidate = ClosestBary(neutralYoke[top], a, b, c); float distance = (a * candidate.x + b * candidate.y + c * candidate.z - neutralYoke[top]).sqrMagnitude; if (distance < best) { best = distance; chosen = tri; bary = candidate; } }
                    Need(chosen != null, "No nondegenerate actual neutral Linen triangle found."); var native = new Dictionary<Transform, float>(); int[] ids = { chosen.a, chosen.b, chosen.c }; float[] factors = { bary.x, bary.y, bary.z };
                    for (int corner = 0; corner < 3; corner++) foreach (var w in clothWeights[ids[corner]]) { Transform bone = cloth.bones[w.boneIndex]; native.TryGetValue(bone, out float prior); native[bone] = prior + w.weight * factors[corner]; }
                    var combined = native.Where(p => p.Value > 0).Select(p => Influence(actor, cloth, Array.IndexOf(cloth.bones, p.Key), p.Value, allowed)).OrderBy(w => w.index).ToArray();
                    anchors[column] = new Anchor { column = column, topVertex = top, bottomVertex = bottom, sourceTriangle = chosen, bary = bary, neutralCloth = neutralCloth[chosen.a] * bary.x + neutralCloth[chosen.b] * bary.y + neutralCloth[chosen.c] * bary.z, neutralTop = neutralYoke[top], neutralBottom = neutralYoke[bottom], combined = combined, outsideFourPercent = combined.Where(w => !w.withinFour).Sum(w => w.percent), sourceVertices = ids.Select(i => new Vertex { index = i, weights = clothWeights[i].Select(w => Influence(actor, cloth, w.boneIndex, w.weight, allowed)).ToArray() }).ToArray(), yokeTopWeights = yokeWeights[top].Select(w => Influence(actor, yoke, w.boneIndex, w.weight, allowed)).ToArray(), yokeBottomWeights = yokeWeights[bottom].Select(w => Influence(actor, yoke, w.boneIndex, w.weight, allowed)).ToArray() };
                }
                var poses = new List<Pose>(); float[] times = { -1, 0, .5f, .25f }; float[] neutralDistances = null;
                for (int poseIndex = 0; poseIndex < times.Length; poseIndex++)
                {
                    float seconds = times[poseIndex]; bool walk = poseIndex == 3;
                    if (seconds >= 0) { animator.Rebind(); animator.SetFloat("MoveSpeed", walk ? 1.2f : 0); animator.SetBool("Grounded", true); animator.SetFloat("VerticalSpeed", 0); animator.Update(0); if (seconds > 0) animator.Update(seconds); }
                    var cp = seconds < 0 ? neutralCloth : Bake(actor, cloth); var yp = seconds < 0 ? neutralYoke : Bake(actor, yoke); var rows = new List<Row>();
                    foreach (var anchor in anchors)
                    {
                        var tri = anchor.sourceTriangle; var p = cp[tri.a] * anchor.bary.x + cp[tri.b] * anchor.bary.y + cp[tri.c] * anchor.bary.z; var cross = Vector3.Cross(cp[tri.b] - cp[tri.a], cp[tri.c] - cp[tri.a]); Need(cross.sqrMagnitude > 1e-18f, "Fixed source triangle collapsed in sampled pose."); var normal = cross.normalized;
                        foreach (bool top in new[] { true, false }) { int vertex = top ? anchor.topVertex : anchor.bottomVertex; var gap = yp[vertex] - p; float signed = Vector3.Dot(gap, normal); var tangent = gap - normal * signed; int row = rows.Count; rows.Add(new Row { column = anchor.column, side = top ? "outer-top" : "outer-bottom", yokeVertex = vertex, actualYoke = yp[vertex], mappedNeutralAnchor = p, normal = normal, gap = gap, tangent = tangent, distance = gap.magnitude, signedNormal = signed, tangentDistance = tangent.magnitude, distanceChangeFromNeutral = neutralDistances == null ? 0 : gap.magnitude - neutralDistances[row] }); }
                    }
                    if (neutralDistances == null) neutralDistances = rows.Select(r => r.distance).ToArray();
                    var state = seconds >= 0 ? animator.GetCurrentAnimatorStateInfo(0) : default;
                    poses.Add(new Pose { label = seconds < 0 ? "saved-clone-before-explicit-Animator-evaluation" : (walk ? "Walk-MoveSpeed1.2-controller-seconds-" : "Idle-controller-seconds-") + seconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), secondsAfterRebind = seconds, normalizedStateTime = state.normalizedTime, stateHash = state.fullPathHash, clips = seconds < 0 ? Array.Empty<Clip>() : animator.GetCurrentAnimatorClipInfo(0).Select(c => new Clip { name = c.clip.name, asset = AssetDatabase.GetAssetPath(c.clip), lengthSeconds = c.clip.length, weight = c.weight }).ToArray(), clothFrame = RendererFrame(actor, cloth, allowed), yokeFrame = RendererFrame(actor, yoke, allowed), rows = rows.ToArray(), worstTwelve = rows.OrderByDescending(r => r.distance).Take(12).ToArray(), top = Summarize(rows.Where(r => r.side == "outer-top").ToArray()), bottom = Summarize(rows.Where(r => r.side == "outer-bottom").ToArray()) });
                }
                foreach (var entry in originalMeshes) Need(GospelCharacterRefinement.MeshHash(entry.Key) == entry.Value, "An actual saved source mesh changed."); Need(EditorJsonUtility.ToJson(prefab) == originalPrefab, "Actual saved prefab changed.");
                var report = new Report { actor = "JesusChild", controller = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController), qualitySkinWeights = QualitySettings.skinWeights.ToString(), clothNativeMaximumInfluences = clothWeights.Max(w => w.Length), yokeNativeMaximumInfluences = yokeWeights.Max(w => w.Length), linenMaterials = slots.Where(m => m && m.name.IndexOf("Linen", StringComparison.OrdinalIgnoreCase) >= 0).Select(m => AssetDatabase.GetAssetPath(m) + " | " + m.name).ToArray(), anchors = anchors, poses = poses.ToArray(), originalPrefabAndMeshesUnchanged = true, scope = "Read-only actual saved V9 child: top 240..287 and bottom 528..575. Exactly 48 neutral-top closest Linen triangles/barycentrics are reconstructed without old preparation reports and held fixed across all poses; bottom uses the same source anchor. No animated nearest reselection. Seconds are actual controller evaluation seconds after Rebind, not normalized time. Normal sign follows source triangle winding. Native barycentric weight aggregation is diagnostic and does not prove a single LBS vertex reproduces a triangle trajectory. No generation/render/new scene/save or fit/Native/motion/product approval." };
                Directory.CreateDirectory(output); using (var file = new FileStream(Path.Combine(output, "diagnostic.json"), FileMode.CreateNew, FileAccess.Write)) { byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(report, true) + "\n"); file.Write(bytes, 0, bytes.Length); file.Flush(true); } Debug.Log("FIRSTGAME_NECKLINE_DIAGNOSTIC " + output);
            }
            finally { if (actor) Object.DestroyImmediate(actor); }
        }
        static Weight Influence(GameObject actor, SkinnedMeshRenderer renderer, int index, float weight, HashSet<Transform> allowed) => new Weight { index = index, bone = renderer.bones[index].name, path = AnimationUtility.CalculateTransformPath(renderer.bones[index], actor.transform), weight = weight, percent = weight * 100, withinFour = allowed.Contains(renderer.bones[index]) };
        static float[] Matrix(Matrix4x4 m) => Enumerable.Range(0, 16).Select(i => m[i / 4, i % 4]).ToArray();
        static Matrix4x4 BakeMap(GameObject actor, SkinnedMeshRenderer renderer)
        { var s = renderer.transform.lossyScale; Need(Finite(s) && s.x > 0 && s.y > 0 && s.z > 0, "Finite positive renderer scale required."); return actor.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix * Matrix4x4.Scale(new Vector3(1 / s.x, 1 / s.y, 1 / s.z)); }
        static Vector3[] Bake(GameObject actor, SkinnedMeshRenderer renderer)
        { var mesh = new Mesh(); try { renderer.BakeMesh(mesh, false); var map = BakeMap(actor, renderer); var points = mesh.vertices.Select(map.MultiplyPoint3x4).ToArray(); Need(points.Length == renderer.sharedMesh.vertexCount && points.All(Finite), "Finite actual baked vertex correspondence required."); return points; } finally { Object.DestroyImmediate(mesh); } }
        static Frame RendererFrame(GameObject actor, SkinnedMeshRenderer renderer, HashSet<Transform> allowed)
        { var bind = renderer.sharedMesh.bindposes; Need(bind.Length == renderer.bones.Length, "Exact actual native bind palette required."); return new Frame { renderer = renderer.name, mesh = AssetDatabase.GetAssetPath(renderer.sharedMesh), rootBone = renderer.rootBone ? AnimationUtility.CalculateTransformPath(renderer.rootBone, actor.transform) : "", quality = renderer.quality.ToString(), lossyScale = renderer.transform.lossyScale, rendererToActor = Matrix(actor.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix), bakeToActor = Matrix(BakeMap(actor, renderer)), bones = renderer.bones.Select((b, i) => new Bone { index = i, name = b.name, path = AnimationUtility.CalculateTransformPath(b, actor.transform), withinFour = allowed.Contains(b), bindpose = Matrix(bind[i]), boneToActor = Matrix(actor.transform.worldToLocalMatrix * b.localToWorldMatrix), skinToActor = Matrix(actor.transform.worldToLocalMatrix * b.localToWorldMatrix * bind[i]) }).ToArray() }; }
        static Stats Summarize(Row[] rows) => new Stats { maximumDistance = rows.Max(r => r.distance), meanDistance = rows.Average(r => r.distance), maximumAbsoluteNormal = rows.Max(r => Mathf.Abs(r.signedNormal)), meanAbsoluteNormal = rows.Average(r => Mathf.Abs(r.signedNormal)), maximumTangent = rows.Max(r => r.tangentDistance), meanTangent = rows.Average(r => r.tangentDistance) };
        static Vector3 ClosestBary(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            var ab = b - a; var ac = c - a; var ap = p - a; float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap); if (d1 <= 0 && d2 <= 0) return new Vector3(1, 0, 0);
            var bp = p - b; float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp); if (d3 >= 0 && d4 <= d3) return new Vector3(0, 1, 0);
            float vc = d1 * d4 - d3 * d2; if (vc <= 0 && d1 >= 0 && d3 <= 0) { float v = d1 / (d1 - d3); return new Vector3(1 - v, v, 0); }
            var cp = p - c; float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp); if (d6 >= 0 && d5 <= d6) return new Vector3(0, 0, 1);
            float vb = d5 * d2 - d1 * d6; if (vb <= 0 && d2 >= 0 && d6 <= 0) { float w = d2 / (d2 - d6); return new Vector3(1 - w, 0, w); }
            float va = d3 * d6 - d5 * d4; if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) { float w = (d4 - d3) / ((d4 - d3) + (d5 - d6)); return new Vector3(0, 1 - w, w); }
            float sum = va + vb + vc; Need(Mathf.Abs(sum) > 1e-20f, "Degenerate closest triangle."); float y = vb / sum, z = vc / sum; return new Vector3(1 - y - z, y, z);
        }
        static bool Finite(Vector3 v) => !(float.IsNaN(v.x) || float.IsInfinity(v.x) || float.IsNaN(v.y) || float.IsInfinity(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.z));
        static void Need(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
#endif
