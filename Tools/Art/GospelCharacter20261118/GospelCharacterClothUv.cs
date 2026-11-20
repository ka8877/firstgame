#if UNITY_EDITOR
// Optional source draft only. Root installs/invokes this explicitly after V5
// tailoring; import alone does nothing. No source asset is edited in place.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Firstgame.GospelNpr.Editor
{
    /// <summary>UV0/tangent-only cloth derivation, preserving original mesh data.</summary>
    public static class GospelCharacterClothUv
    {
        public const string Revision = "gospel-cloth-uv-20261118-v1-draft";
        public const string OutputRoot = "Assets/Art/GospelCharacter20261118/RenderUvV1";
        [Serializable] public sealed class Surface
        {
            public string renderer, sourceAsset, sourceGuid, sourceLocalId, sourceHash, asset, derivedHash, retainedBuffersHash;
            public string reason, projection, existingUvFormat;
            public int vertices, triangles, existingUvCount, existingUvDimension, existingTangentCount;
            public int existingUvNondegenerateTriangles, existingUvDegenerateTriangles, seamCrossingTriangles;
            public int generatedUvNondegenerateTriangles, generatedUvDegenerateTriangles;
            public float maximumTriangleUSpan, circumference, axialSpan;
            public Vector3 actorAxis, actorOrigin;
            public bool applied, existingUvUsable, tangentsRegenerated, sourceUnchanged, retainedBuffersExact;
            public bool originalRendererBindingsRetained, originalMeshPointerRetained, unsplitBackSeamDistortionRemains;
        }
        [Serializable] public sealed class Report
        {
            public string revision = Revision, actor;
            public Surface[] surfaces;
            public bool avatarControllerAndHierarchyRetained, actualVisualReviewCompleted;
            public string scope = "UV0 and tangent-only derived mesh pointers after V5; original source assets retained. No seam split or motion/visual approval.";
        }
        sealed class UvState
        {
            public List<Vector4> values = new List<Vector4>();
            public bool usable, finite = true, constant;
            public int goodTriangles, badTriangles;
        }

        public static Report Apply(GameObject actor, string id)
        {
            Need(!Application.isPlaying && actor && !EditorUtility.IsPersistent(actor), "Use an explicitly prepared fresh neutral Editor instance.");
            Need(new[] { "JesusChild", "JesusAdult", "Mary", "Joseph", "Teacher" }.Contains(id), "An exact retained actor identity is required.");
            var animator = actor.GetComponent<Animator>();
            Need(animator && animator.avatar && animator.avatar.isHuman && animator.avatar.isValid, "A real retained Humanoid is required.");
            var avatar = animator.avatar; var controller = animator.runtimeAnimatorController;
            string hierarchy = HierarchyHash(actor);
            var rows = new List<Surface>();
            var renderers = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Need(renderers.Select(r => r.name).Distinct().Count() == renderers.Length, "Unique renderer names are required for deterministic outputs.");
            Need(renderers.Select(r => Safe(r.name)).Distinct().Count() == renderers.Length, "Sanitized renderer output stems must also remain unique.");
            foreach (var renderer in renderers)
            {
                Mesh source = renderer.sharedMesh;
                if (!source || !source.isReadable) continue;
                var triangles = Triangles(source); var referenced = triangles.Distinct().OrderBy(i => i).ToArray();
                if (referenced.Length == 0) continue;
                string before = FullHash(source), retained = RetainedHash(source);
                var uv = InspectUv(source, triangles, referenced);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long localId);
                var row = new Surface { renderer = renderer.name, sourceAsset = AssetDatabase.GetAssetPath(source), sourceGuid = guid,
                    sourceLocalId = localId.ToString(System.Globalization.CultureInfo.InvariantCulture), sourceHash = before,
                    vertices = source.vertexCount, triangles = triangles.Length / 3, existingUvCount = uv.values.Count,
                    existingUvDimension = source.HasVertexAttribute(VertexAttribute.TexCoord0) ? source.GetVertexAttributeDimension(VertexAttribute.TexCoord0) : 0,
                    existingUvFormat = source.HasVertexAttribute(VertexAttribute.TexCoord0) ? source.GetVertexAttributeFormat(VertexAttribute.TexCoord0).ToString() : "absent",
                    existingTangentCount = source.tangents.Length, existingUvUsable = uv.usable,
                    existingUvNondegenerateTriangles = uv.goodTriangles, existingUvDegenerateTriangles = uv.badTriangles,
                    retainedBuffersHash = retained, originalMeshPointerRetained = true };
                rows.Add(row);
                string excluded = Exclusion(renderer, id);
                if (excluded != null) row.reason = excluded;
                else if (uv.usable) row.reason = "Existing usable UV0 retained exactly; no tangent repair attempted.";
                else if (source.blendShapeCount != 0)
                    row.reason = "Morph-bearing cloth retained unchanged; regenerating its base tangent space needs a separate morph-tangent review.";
                else if (uv.values.Count != 0 && (uv.values.Count != source.vertexCount || !uv.finite || !uv.constant))
                    row.reason = "Existing nonconstant/partial/invalid UV requires separate review; do not overwrite authored coordinates.";
                else
                {
                    Need(source.normals.Length == source.vertexCount, "New cloth tangents require retained authored normals: " + renderer.name);
                    Need(Enumerable.Range(0, source.blendShapeCount).All(i => Mathf.Abs(renderer.GetBlendShapeWeight(i)) < .00001f),
                        "Map only a fresh neutral cloth source; never reset a sampled expression.");
                    var bones = renderer.bones.ToArray(); var rootBone = renderer.rootBone; var materials = renderer.sharedMaterials.ToArray();
                    var bounds = renderer.localBounds;
                    var defaults = Enumerable.Range(0, source.blendShapeCount).Select(renderer.GetBlendShapeWeight).ToArray();
                    Vector3[] points = ActorPoints(actor, renderer);
                    Frame(actor, animator, renderer, id, points, referenced, out Vector3 origin, out Vector3 axis, out string projection);
                    Vector3 front = Vector3.ProjectOnPlane(Vector3.forward, axis).normalized;
                    if (front.sqrMagnitude < .5f) front = Vector3.ProjectOnPlane(Vector3.up, axis).normalized;
                    Need(front.sqrMagnitude > .5f, "A finite cylindrical frame is required.");
                    Vector3 side = Vector3.Cross(axis, front).normalized;
                    float min = referenced.Min(i => Vector3.Dot(points[i] - origin, axis));
                    float max = referenced.Max(i => Vector3.Dot(points[i] - origin, axis));
                    float radius = referenced.Average(i => Vector3.ProjectOnPlane(points[i] - origin, axis).magnitude);
                    float circumference = 2 * Mathf.PI * radius;
                    Need(Finite(circumference) && circumference > .0001f && max - min > .0001f, "Actual drawn cloth needs positive cylindrical radius and length.");
                    var mapped = new List<Vector2>(points.Length);
                    foreach (var p in points)
                    {
                        Vector3 q = p - origin;
                        var value = new Vector2(Mathf.Atan2(Vector3.Dot(q, side), Vector3.Dot(q, front)) / (2 * Mathf.PI) + .5f,
                            (Vector3.Dot(q, axis) - min) / circumference);
                        Need(Finite(value.x) && Finite(value.y), "Every generated coordinate must remain finite."); mapped.Add(value);
                    }
                    Mesh candidate = Object.Instantiate(source); candidate.name = renderer.name + "__ClothUvV1";
                    try
                    {
                        candidate.SetUVs(0, mapped);
                        var generatedUv = InspectUv(candidate, triangles, referenced);
                        Need(generatedUv.usable && generatedUv.badTriangles <= .05f * (generatedUv.goodTriangles + generatedUv.badTriangles),
                            "New cylindrical cloth UVs must map at least 95 percent of actual nondegenerate draw triangles without collapse.");
                        candidate.SetTangents(Tangents(source, mapped, triangles));
                        Need(RetainedHash(candidate) == retained, "UV derivation changed a protected position/normal/topology/UV1-7/color/skin/morph buffer.");
                        Need(FullHash(source) == before, "The source mesh changed during UV derivation.");
                        string path = OutputRoot + "/" + id + "/" + Safe(renderer.name) + "__ClothUvV1.asset";
                        Mesh saved = Persist(candidate, path); candidate = null;
                        renderer.sharedMesh = saved;
                        row.asset = path; row.derivedHash = FullHash(saved); row.applied = true;
                        row.originalMeshPointerRetained = false; row.tangentsRegenerated = true;
                        row.actorAxis = axis; row.actorOrigin = origin; row.projection = projection;
                        row.circumference = circumference; row.axialSpan = max - min;
                        row.generatedUvNondegenerateTriangles = generatedUv.goodTriangles; row.generatedUvDegenerateTriangles = generatedUv.badTriangles;
                        for (int t = 0; t < triangles.Length; t += 3)
                        {
                            float lo = Mathf.Min(mapped[triangles[t]].x, Mathf.Min(mapped[triangles[t + 1]].x, mapped[triangles[t + 2]].x));
                            float hi = Mathf.Max(mapped[triangles[t]].x, Mathf.Max(mapped[triangles[t + 1]].x, mapped[triangles[t + 2]].x));
                            row.maximumTriangleUSpan = Mathf.Max(row.maximumTriangleUSpan, hi - lo);
                            if (hi - lo > .5f) row.seamCrossingTriangles++;
                        }
                        row.unsplitBackSeamDistortionRemains = row.seamCrossingTriangles > 0;
                        row.reason = "New UV0 and CPU tangent channel only; retained original mesh pointer replaced explicitly after V5. No vertex split; back seam interpolation is not repaired.";
                    }
                    finally { if (candidate) Object.DestroyImmediate(candidate); }
                    Need(renderer.bones.SequenceEqual(bones) && renderer.rootBone == rootBone && renderer.sharedMaterials.SequenceEqual(materials)
                        && renderer.localBounds == bounds && defaults.SequenceEqual(Enumerable.Range(0, source.blendShapeCount).Select(renderer.GetBlendShapeWeight)),
                        "UV mapping must retain actual renderer bindings, materials, bounds and morph defaults.");
                }
                row.sourceUnchanged = FullHash(source) == before;
                row.retainedBuffersExact = RetainedHash(renderer.sharedMesh) == retained;
                row.originalRendererBindingsRetained = true;
                Need(row.sourceUnchanged && row.retainedBuffersExact, "Source and protected buffers must remain exact.");
            }
            Need(animator.avatar == avatar && animator.runtimeAnimatorController == controller && HierarchyHash(actor) == hierarchy,
                "UV derivation must preserve the actual Avatar, controller and all hierarchy transforms.");
            return new Report { actor = id, surfaces = rows.ToArray(), avatarControllerAndHierarchyRetained = true };
        }

        static string Exclusion(SkinnedMeshRenderer renderer, string id)
        {
            if (renderer.name == id + "__Body" || renderer.name == id + "__Face" || renderer.name == id + "__Hair"
                || renderer.name.IndexOf("FittedShirt", StringComparison.Ordinal) >= 0)
                return "Protected Body/Face/Hair/fitted-source-shirt renderer; no UV exception permitted.";
            var materials = renderer.sharedMaterials;
            if (materials.Length != renderer.sharedMesh.subMeshCount || materials.Length == 0 || materials.Any(m => !m))
                return "Incomplete actual material/submesh mapping; unchanged.";
            if (materials.Any(m => { var role = GospelNprMaterialFactory.InferRole(m.name); return role != NprSurfaceRole.Linen && role != NprSurfaceRole.Mantle; }))
                return "Not exclusively semantic Linen/Mantle; mixed/neutral trim including WovenHem/WovenSash unchanged.";
            return null;
        }
        static UvState InspectUv(Mesh mesh, int[] triangles, int[] referenced)
        {
            var result = new UvState(); mesh.GetUVs(0, result.values);
            if (result.values.Count != mesh.vertexCount) return result;
            result.finite = referenced.All(i => Finite(result.values[i].x) && Finite(result.values[i].y));
            if (!result.finite) return result;
            Vector2 first = result.values[referenced[0]];
            result.constant = referenced.All(i => ((Vector2)result.values[i] - first).sqrMagnitude <= 1e-16f);
            var positions = mesh.vertices;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                if (Vector3.Cross(positions[triangles[i + 1]] - positions[triangles[i]], positions[triangles[i + 2]] - positions[triangles[i]]).sqrMagnitude <= 1e-20f) continue;
                Vector2 a = result.values[triangles[i]], b = result.values[triangles[i + 1]], c = result.values[triangles[i + 2]];
                float area = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                if (Mathf.Abs(area) > 1e-12f) result.goodTriangles++; else result.badTriangles++;
            }
            // Preserve an authored useful map even if some deliberate UV
            // degeneracies exist. Partial meaningful maps require manual review.
            result.usable = result.goodTriangles > 0 && !result.constant; return result;
        }
        static int[] Triangles(Mesh mesh)
        {
            var values = new List<int>();
            for (int s = 0; s < mesh.subMeshCount; s++)
            { Need(mesh.GetTopology(s) == MeshTopology.Triangles, "Actual cloth draw topology must be triangles."); values.AddRange(mesh.GetIndices(s)); }
            Need(values.All(i => i >= 0 && i < mesh.vertexCount), "Draw indices must reference actual mesh vertices."); return values.ToArray();
        }
        static Vector3[] ActorPoints(GameObject actor, SkinnedMeshRenderer renderer)
        {
            Mesh mesh = renderer.sharedMesh; var bones = renderer.bones; var bind = mesh.bindposes;
            Need(bones.Length > 0 && bind.Length == bones.Length && bones.All(b => b && (b == actor.transform || b.IsChildOf(actor.transform))), "Retain the complete actual skin palette.");
            var palette = bones.Select((bone, i) => actor.transform.worldToLocalMatrix * bone.localToWorldMatrix * bind[i]).ToArray();
            var raw = mesh.vertices; var result = new Vector3[raw.Length];
            using (var counts = mesh.GetBonesPerVertex()) using (var weights = mesh.GetAllBoneWeights())
            {
                Need(counts.Length == raw.Length, "Complete native counts required."); int offset = 0;
                for (int i = 0; i < raw.Length; i++)
                {
                    float sum = 0; Need(counts[i] > 0, "Every retained cloth vertex needs actual skin weights.");
                    for (int j = 0; j < counts[i]; j++)
                    {
                        Need(offset < weights.Length, "Native influence buffers must agree."); var weight = weights[offset++];
                        Need(weight.boneIndex >= 0 && weight.boneIndex < bones.Length && Finite(weight.weight) && weight.weight > 0, "Actual finite positive native influence required.");
                        sum += weight.weight; result[i] += palette[weight.boneIndex].MultiplyPoint3x4(raw[i]) * weight.weight;
                    }
                    Need(Mathf.Abs(sum - 1) <= .001f && Finite(result[i]), "Actual complete native actor-space mapping must remain normalized and finite.");
                }
                Need(offset == weights.Length, "Do not truncate native cloth influences.");
            }
            return result;
        }
        static void Frame(GameObject actor, Animator animator, SkinnedMeshRenderer renderer, string id, Vector3[] points, int[] referenced,
            out Vector3 origin, out Vector3 axis, out string projection)
        {
            var bounds = new Bounds(points[referenced[0]], Vector3.zero); foreach (int i in referenced) bounds.Encapsulate(points[i]);
            origin = bounds.center; axis = Vector3.up; projection = "actor_y_cylinder_back_seam";
            if (renderer.name == id + "__TailoredTunic")
            { var hips = animator.GetBoneTransform(HumanBodyBones.Hips); Need(hips, "The actual robe hips anchor is required."); var p = actor.transform.InverseTransformPoint(hips.position); origin.x = p.x; origin.z = p.z; }
            else if (renderer.name == id + "__LeftTailoredSleeve" || renderer.name == id + "__RightTailoredSleeve")
            {
                bool left = renderer.name == id + "__LeftTailoredSleeve";
                var upper = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
                var hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                Need(upper && hand, "Actual sleeve limb anchors are required."); origin = actor.transform.InverseTransformPoint(upper.position);
                axis = (actor.transform.InverseTransformPoint(hand.position) - origin).normalized; projection = "actual_arm_axis_cylinder_back_seam";
            }
            else
            { var s = bounds.size; axis = s.y >= s.x && s.y >= s.z ? Vector3.up : s.x >= s.z ? Vector3.right : Vector3.forward; projection = "drawn_bounds_principal_axis_cylinder"; }
            Need(Finite(origin) && Finite(axis) && axis.sqrMagnitude > .5f, "A measured finite cloth frame is required.");
        }
        static List<Vector4> Tangents(Mesh source, List<Vector2> uv, int[] triangles)
        {
            var p = source.vertices; var n = source.normals; var u = new Vector3[p.Length]; var v = new Vector3[p.Length];
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2]; Vector3 e1 = p[b] - p[a], e2 = p[c] - p[a];
                Vector2 d1 = uv[b] - uv[a], d2 = uv[c] - uv[a]; float det = d1.x * d2.y - d1.y * d2.x;
                if (Mathf.Abs(det) <= 1e-12f) continue;
                Vector3 du = (e1 * d2.y - e2 * d1.y) / det, dv = (e2 * d1.x - e1 * d2.x) / det;
                Need(Finite(du) && Finite(dv), "New cloth tangent derivatives must remain finite.");
                foreach (int i in new[] { a, b, c }) { u[i] += du; v[i] += dv; }
            }
            var result = new List<Vector4>(p.Length);
            for (int i = 0; i < p.Length; i++)
            {
                Need(Finite(n[i]) && n[i].sqrMagnitude > 1e-12f, "Retained normals must be finite and nonzero; never recalculate them silently.");
                Vector3 normal = n[i].normalized, tangent = Vector3.ProjectOnPlane(u[i], normal);
                if (tangent.sqrMagnitude <= 1e-12f) tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) < .9f ? Vector3.up : Vector3.right);
                tangent.Normalize(); Need(Finite(tangent) && tangent.sqrMagnitude > .5f, "New CPU tangent must be finite and nonzero.");
                result.Add(new Vector4(tangent.x, tangent.y, tangent.z, Vector3.Dot(Vector3.Cross(normal, tangent), v[i]) < 0 ? -1 : 1));
            }
            return result;
        }
        static Mesh Persist(Mesh candidate, string path)
        {
            Folder(Path.GetDirectoryName(path).Replace('\\', '/'));
            string absolute = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing)
            { Need(FullHash(existing) == FullHash(candidate), "Preserve a differing existing UV derivative; use a new revision: " + path); Object.DestroyImmediate(candidate); return existing; }
            Need(!File.Exists(absolute) && !Directory.Exists(absolute), "Preserve an existing non-mesh output: " + path);
            AssetDatabase.CreateAsset(candidate, path); AssetDatabase.SaveAssetIfDirty(candidate); return candidate;
        }
        static string FullHash(Mesh mesh) => Hash(writer =>
        {
            writer.Write(RetainedHash(mesh)); WriteUv(writer, mesh, 0);
            var descriptors = mesh.GetVertexAttributes().Where(a => a.attribute == VertexAttribute.TexCoord0 || a.attribute == VertexAttribute.Tangent).ToArray();
            writer.Write(descriptors.Length); foreach (var a in descriptors) { writer.Write((int)a.attribute); writer.Write((int)a.format); writer.Write(a.dimension); }
            var tangents = mesh.tangents; writer.Write(tangents.Length); foreach (var t in tangents) { writer.Write(t.x); writer.Write(t.y); writer.Write(t.z); writer.Write(t.w); }
        });
        static string RetainedHash(Mesh mesh) => Hash(writer =>
        {
            writer.Write(mesh.vertexCount); writer.Write((int)mesh.indexFormat); writer.Write(mesh.subMeshCount);
            WriteVectors(writer, mesh.vertices); WriteVectors(writer, mesh.normals); WriteBounds(writer, mesh.bounds);
            var descriptors = mesh.GetVertexAttributes().Where(a => a.attribute != VertexAttribute.TexCoord0 && a.attribute != VertexAttribute.Tangent).ToArray();
            writer.Write(descriptors.Length); foreach (var a in descriptors) { writer.Write((int)a.attribute); writer.Write((int)a.format); writer.Write(a.dimension); }
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var desc = mesh.GetSubMesh(s); writer.Write((int)desc.topology); writer.Write(desc.baseVertex); writer.Write(desc.firstVertex); writer.Write(desc.vertexCount); WriteBounds(writer, desc.bounds);
                var indices = mesh.GetIndices(s, false); writer.Write(indices.Length); foreach (int i in indices) writer.Write(i);
            }
            for (int channel = 1; channel < 8; channel++) WriteUv(writer, mesh, channel);
            var colors = mesh.colors; writer.Write(colors.Length); foreach (var c in colors) { writer.Write(c.r); writer.Write(c.g); writer.Write(c.b); writer.Write(c.a); }
            var colors32 = mesh.colors32; writer.Write(colors32.Length); foreach (var c in colors32) { writer.Write(c.r); writer.Write(c.g); writer.Write(c.b); writer.Write(c.a); }
            var bind = mesh.bindposes; writer.Write(bind.Length); foreach (var m in bind) for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) writer.Write(m[r, c]);
            using (var counts = mesh.GetBonesPerVertex()) using (var weights = mesh.GetAllBoneWeights())
            { writer.Write(counts.Length); foreach (byte count in counts) writer.Write(count); writer.Write(weights.Length); foreach (var weight in weights) { writer.Write(weight.boneIndex); writer.Write(weight.weight); } }
            writer.Write(mesh.blendShapeCount); var dv = new Vector3[mesh.vertexCount]; var dn = new Vector3[mesh.vertexCount]; var dt = new Vector3[mesh.vertexCount];
            for (int shape = 0; shape < mesh.blendShapeCount; shape++)
            {
                writer.Write(mesh.GetBlendShapeName(shape)); writer.Write(mesh.GetBlendShapeFrameCount(shape));
                for (int frame = 0; frame < mesh.GetBlendShapeFrameCount(shape); frame++)
                { writer.Write(mesh.GetBlendShapeFrameWeight(shape, frame)); mesh.GetBlendShapeFrameVertices(shape, frame, dv, dn, dt); WriteVectors(writer, dv); WriteVectors(writer, dn); WriteVectors(writer, dt); }
            }
        });
        static void WriteUv(BinaryWriter writer, Mesh mesh, int channel)
        { var uv = new List<Vector4>(); mesh.GetUVs(channel, uv); writer.Write(uv.Count); foreach (var p in uv) { writer.Write(p.x); writer.Write(p.y); writer.Write(p.z); writer.Write(p.w); } }
        static void WriteVectors(BinaryWriter writer, Vector3[] values)
        { writer.Write(values.Length); foreach (var p in values) { writer.Write(p.x); writer.Write(p.y); writer.Write(p.z); } }
        static void WriteBounds(BinaryWriter writer, Bounds bounds) => WriteVectors(writer, new[] { bounds.center, bounds.size });
        static string HierarchyHash(GameObject actor) => Hash(writer =>
        { foreach (var t in actor.GetComponentsInChildren<Transform>(true)) { writer.Write(AnimationUtility.CalculateTransformPath(t, actor.transform)); WriteVectors(writer, new[] { t.localPosition, t.localScale }); var q = t.localRotation; writer.Write(q.x); writer.Write(q.y); writer.Write(q.z); writer.Write(q.w); } });
        static string Hash(Action<BinaryWriter> write)
        { using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream)) { write(writer); writer.Flush(); using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant(); } }
        static void Folder(string path)
        {
            Need(path == "Assets" || OutputRoot.StartsWith(path + "/", StringComparison.Ordinal)
                || path == OutputRoot || path.StartsWith(OutputRoot + "/", StringComparison.Ordinal), "UV assets require the fixed derived output root.");
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/'); Need(slash > 0, "The existing Unity Assets root is required.");
            string absolute = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
            Need(!File.Exists(absolute) && !Directory.Exists(absolute), "Preserve an existing unrecognized UV output directory.");
            Folder(path.Substring(0, slash)); AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
        static string Safe(string name) => string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_'));
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static void Need(Object value, string message) { if (!value) throw new InvalidOperationException(message); }
        static void Need(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
#endif
