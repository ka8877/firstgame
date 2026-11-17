#if UNITY_EDITOR
// Root-only in-memory derivation. No AssetDatabase, importer, save or original
// renderer/mesh mutation. The caller persists a new version and copies the
// original renderer's frame, bones, rootBone and SkinQuality onto the shirt.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace FirstGame.Editor
{
    public static class GospelIllustrationFittedCloth
    {
        static readonly string[] Identities = { "JesusChild", "JesusAdult", "Joseph", "Teacher" };

        public sealed class Package
        {
            public Mesh OriginalMesh, BodyRemainder, Shirt;
            public int ClothSubmesh;
            public int[] SourceVertexForShirtVertex;
            public int[] ReferencedSourceClothVertices;
            public SkinQuality OriginalSkinQuality;
            public Transform[] OriginalBones;
            public Transform OriginalRootBone, OriginalParent;
            public Material[] OriginalMaterials;
            public Vector3 OriginalLocalPosition, OriginalLocalScale;
            public Quaternion OriginalLocalRotation;
            public Diagnostics Report;
        }

        [Serializable] public sealed class Diagnostics
        {
            public string identity, originalMesh, clothMaterial, skinQuality;
            public int sourceVertices, sourceSubmeshes, clothSubmesh, clothTriangles, shirtVertices, remainderTriangles;
            public int referencedClothVertices, unusedRetainedUndrawnVertices;
            public int fullSourceInfluences, fullShirtInfluences, maximumSourceInfluences;
            public float rendererLocalClearance;
            public bool exactTrianglePartition, anatomicalAndOtherTrianglesRetained, remainderVertexPayloadExact;
            public bool identitySourceMappingExact, fullSourceVertexBufferRetained, fullVariableWeightsExact, bindposesExact, originalSkinQualityPreserved;
            public string vertexStrategy = "Full original buffers retained; only original cloth indices are drawn. No vertex compaction or skin-weight reencoding.";
            public string scope = "Exact retained cloth submesh extraction only; no anatomical skin, original model, Avatar or rig was removed or modified. Motion/appearance need actual Root review.";
        }

        public static Package Build(SkinnedMeshRenderer original, string id, float clearance)
        {
            Need(!Application.isPlaying, "Fitted cloth is an Editor preparation step.");
            int slot = RequireSource(original, id, clearance);
            Mesh source = original.sharedMesh, remainder = null, shirt = null;
            try
            {
                int[] triangles = source.GetTriangles(slot);
                int[] selected = triangles.Distinct().OrderBy(i => i).ToArray();
                int[] map = Enumerable.Range(0, source.vertexCount).ToArray();
                remainder = Object.Instantiate(source);
                remainder.name = id + "__IllustrationBodyRemainder";
                // Empty only this draw slot. All original vertex buffers and
                // every other submesh remain in the clone without compaction.
                remainder.SetTriangles(Array.Empty<int>(), slot, false, (int)source.GetBaseVertex(slot));
                remainder.bounds = source.bounds;

                // SetBoneWeights can quantize even copied BoneWeight1 values.
                // Clone the entire original storage instead: unused vertices
                // remain exact but cannot draw without an index reference.
                shirt = Object.Instantiate(source);
                shirt.name = id + "__IllustrationFittedShirt";
                Vector3[] vertices = source.vertices, normals = source.normals;
                foreach (int vertex in selected) vertices[vertex] += normals[vertex] * clearance;
                shirt.vertices = vertices;
                shirt.subMeshCount = 1;
                shirt.SetTriangles(triangles, 0, false, 0);
                shirt.RecalculateBounds();
                var package = new Package { OriginalMesh = source, BodyRemainder = remainder, Shirt = shirt,
                    ClothSubmesh = slot, SourceVertexForShirtVertex = map, ReferencedSourceClothVertices = selected, OriginalSkinQuality = original.quality,
                    OriginalBones = original.bones, OriginalRootBone = original.rootBone, OriginalMaterials = original.sharedMaterials,
                    OriginalParent = original.transform.parent, OriginalLocalPosition = original.transform.localPosition,
                    OriginalLocalRotation = original.transform.localRotation, OriginalLocalScale = original.transform.localScale };
                package.Report = Verify(original, package, id, clearance);
                return package;
            }
            catch
            {
                if (remainder) Object.DestroyImmediate(remainder);
                if (shirt) Object.DestroyImmediate(shirt);
                throw;
            }
        }

        /// <summary>Use before changing original.sharedMesh.</summary>
        public static Diagnostics Verify(SkinnedMeshRenderer original, Mesh bodyRemainder, Mesh shirt, string id, float clearance)
        {
            int slot = RequireSource(original, id, clearance);
            var map = Enumerable.Range(0, original.sharedMesh.vertexCount).ToArray();
            return VerifyCore(original, original.sharedMesh, bodyRemainder, shirt, map, slot, id, clearance, original.quality);
        }

        /// <summary>Also works after the caller assigns package.BodyRemainder to the original renderer.</summary>
        public static Diagnostics Verify(SkinnedMeshRenderer original, Package package, string id, float clearance)
        {
            Need(package != null && package.OriginalMesh && original, "Actual derivation package and original renderer required.");
            Need(original.sharedMesh == package.OriginalMesh || original.sharedMesh == package.BodyRemainder,
                "The original renderer may only retain the original mesh or its verified remainder.");
            Need(original.quality == package.OriginalSkinQuality, "Preserve original SkinQuality; do not truncate variable weights.");
            Need(original.bones.SequenceEqual(package.OriginalBones) && original.rootBone == package.OriginalRootBone
                 && original.sharedMaterials.SequenceEqual(package.OriginalMaterials), "Original body palette/material/rootBone references changed.");
            Need(original.transform.parent == package.OriginalParent && original.transform.localPosition.Equals(package.OriginalLocalPosition)
                 && original.transform.localRotation.Equals(package.OriginalLocalRotation) && original.transform.localScale.Equals(package.OriginalLocalScale),
                "Original renderer frame changed.");
            RequireFrame(original, package.OriginalMesh, id, clearance, package.ClothSubmesh);
            Need(package.ReferencedSourceClothVertices != null && package.ReferencedSourceClothVertices.SequenceEqual(
                package.OriginalMesh.GetTriangles(package.ClothSubmesh).Distinct().OrderBy(i => i)), "Exact referenced cloth vertex set changed.");
            return VerifyCore(original, package.OriginalMesh, package.BodyRemainder, package.Shirt,
                package.SourceVertexForShirtVertex, package.ClothSubmesh, id, clearance, package.OriginalSkinQuality);
        }

        static Diagnostics VerifyCore(SkinnedMeshRenderer renderer, Mesh source, Mesh remainder, Mesh shirt,
            int[] map, int slot, string id, float clearance, SkinQuality quality)
        {
            Need(source && remainder && shirt && source != remainder && source != shirt && remainder != shirt,
                "Derived meshes must be independent objects; never edit the imported source.");
            Need(source.blendShapeCount == 0 && remainder.blendShapeCount == 0 && shirt.blendShapeCount == 0,
                "This bounded extraction excludes every morph mesh.");
            Need(remainder.name == id + "__IllustrationBodyRemainder" && shirt.name == id + "__IllustrationFittedShirt",
                "Stable derived mesh identities required.");
            Need(remainder.vertexCount == source.vertexCount && remainder.subMeshCount == source.subMeshCount
                 && remainder.indexFormat == source.indexFormat && remainder.bounds.Equals(source.bounds), "Remainder buffers/bounds must remain original.");
            int[] cloth = source.GetTriangles(slot);
            int[] expectedMap = Enumerable.Range(0, source.vertexCount).ToArray();
            Need(map != null && map.SequenceEqual(expectedMap) && shirt.vertexCount == map.Length && shirt.subMeshCount == 1,
                "Shirt must retain the complete original vertex storage with an identity map.");
            int remainingTriangles = 0;
            for (int submesh = 0; submesh < source.subMeshCount; submesh++)
            {
                Need(remainder.GetTopology(submesh) == MeshTopology.Triangles, "Retain triangle topology.");
                int[] kept = remainder.GetIndices(submesh, false);
                if (submesh == slot) Need(kept.Length == 0, "Only the exact cloth slot may be emptied.");
                else
                {
                    Need(remainder.GetBaseVertex(submesh) == source.GetBaseVertex(submesh)
                         && kept.SequenceEqual(source.GetIndices(submesh, false)), "Anatomical skin/other submesh triangles changed.");
                    remainingTriangles += kept.Length / 3;
                }
            }
            Need(shirt.GetTopology(0) == MeshTopology.Triangles && shirt.GetBaseVertex(0) == 0
                 && shirt.GetTriangles(0).SequenceEqual(cloth), "Shirt triangle order/winding must be the exact source cloth indices.");
            Need(cloth.Length + remainingTriangles * 3 == Enumerable.Range(0, source.subMeshCount).Sum(i => source.GetIndices(i).Length),
                "Exact triangle partition must retain every original draw triangle once.");

            EqualVectors(source.vertices, remainder.vertices, null, "Original body positions");
            EqualVectors(source.normals, remainder.normals, null, "Original body normals");
            EqualTangents(source.tangents, remainder.tangents, null, "Original body tangents");
            Need(shirt.indexFormat == source.indexFormat, "Keep the original index format.");
            Vector3[] originalVertices = source.vertices, originalNormals = source.normals, positions = shirt.vertices;
            var referenced = new HashSet<int>(cloth);
            for (int i = 0; i < map.Length; i++)
                Need(positions[i].Equals(originalVertices[i] + (referenced.Contains(i) ? originalNormals[i] * clearance : Vector3.zero)),
                    "Only referenced cloth vertices may receive exact normal clearance; undrawn vertices remain original.");
            EqualVectors(originalNormals, shirt.normals, map, "Shirt authored normals");
            EqualTangents(source.tangents, shirt.tangents, map, "Shirt authored tangents");
            VerifyUvs(source, remainder, null); VerifyUvs(source, shirt, map);
            VerifyColors(source, remainder, null); VerifyColors(source, shirt, map);
            Need(source.bindposes.SequenceEqual(remainder.bindposes) && source.bindposes.SequenceEqual(shirt.bindposes), "Original bindposes changed.");
            var sourceWeights = ReadWeights(source, renderer.bones.Length);
            VerifyWeights(sourceWeights, ReadWeights(remainder, renderer.bones.Length), null);
            var shirtWeights = ReadWeights(shirt, renderer.bones.Length);
            VerifyWeights(sourceWeights, shirtWeights, map);
            return new Diagnostics { identity = id, originalMesh = source.name, clothMaterial = renderer.sharedMaterials[slot].name,
                skinQuality = quality.ToString(), sourceVertices = source.vertexCount, sourceSubmeshes = source.subMeshCount,
                clothSubmesh = slot, clothTriangles = cloth.Length / 3, shirtVertices = shirt.vertexCount,
                referencedClothVertices = referenced.Count, unusedRetainedUndrawnVertices = shirt.vertexCount - referenced.Count,
                remainderTriangles = remainingTriangles, fullSourceInfluences = sourceWeights.Values.Length,
                fullShirtInfluences = shirtWeights.Values.Length, maximumSourceInfluences = sourceWeights.Counts.Max(i => (int)i),
                rendererLocalClearance = clearance, exactTrianglePartition = true, anatomicalAndOtherTrianglesRetained = true,
                remainderVertexPayloadExact = true, identitySourceMappingExact = true, fullSourceVertexBufferRetained = true, fullVariableWeightsExact = true,
                bindposesExact = true, originalSkinQualityPreserved = renderer.quality == quality };
        }

        static int RequireSource(SkinnedMeshRenderer renderer, string id, float clearance)
        {
            Need(renderer && renderer.sharedMesh, "Actual original renderer required.");
            int[] slots = renderer.sharedMaterials.Select((m, i) => new { m, i })
                .Where(x => x.m && x.m.name == id + "__UndyedLinen").Select(x => x.i).ToArray();
            Need(slots.Length == 1, "Exactly one source UndyedLinen material slot required.");
            RequireFrame(renderer, renderer.sharedMesh, id, clearance, slots[0]);
            return slots[0];
        }

        static void RequireFrame(SkinnedMeshRenderer renderer, Mesh mesh, string id, float clearance, int slot)
        {
            Need(!Application.isPlaying && Identities.Contains(id) && renderer.name == id + "__Body", "Only the four exact original C-source Body renderers are admitted.");
            Need(mesh && mesh.isReadable && mesh.blendShapeCount == 0 && mesh.vertexCount > 0, "Readable non-morph source body required.");
            Need(Finite(clearance) && clearance > 0, "Explicit positive renderer-local normal clearance required.");
            Need(renderer.sharedMaterials.Length == mesh.subMeshCount && slot >= 0 && slot < mesh.subMeshCount
                 && renderer.sharedMaterials[slot] && renderer.sharedMaterials[slot].name == id + "__UndyedLinen", "Exact source cloth draw slot required.");
            Need(mesh.GetTriangles(slot).Length > 0 && mesh.normals.Length == mesh.vertexCount, "Actual source cloth triangles and authored normals required.");
            Need(Enumerable.Range(0, mesh.subMeshCount).All(i => mesh.GetTopology(i) == MeshTopology.Triangles), "Only actual triangle submeshes are admitted.");
            var bones = renderer.bones; var bindposes = mesh.bindposes;
            Need(bones.Length > 0 && bindposes.Length == bones.Length && bones.All(b => b), "Preserve the full original bone palette.");
            for (int i = 0; i < bones.Length; i++)
            {
                Matrix4x4 matrix = renderer.transform.worldToLocalMatrix * bones[i].localToWorldMatrix * bindposes[i];
                for (int j = 0; j < 16; j++)
                    Need(Finite(matrix[j]) && Mathf.Abs(matrix[j] - Matrix4x4.identity[j]) < .004f, "C-source clothing inherits the original strict .004 bind frame.");
            }
            var vertices = mesh.vertices; var normals = mesh.normals;
            foreach (int i in mesh.GetTriangles(slot))
                Need(i >= 0 && i < mesh.vertexCount && Finite(vertices[i]) && Finite(normals[i]) && normals[i].sqrMagnitude > .1f,
                    "Every selected cloth vertex needs a finite original position/normal.");
        }

        sealed class FullWeights
        {
            public byte[] Counts;
            public int[] Offsets;
            public BoneWeight1[] Values;
        }

        static FullWeights ReadWeights(Mesh mesh, int boneCount)
        {
            var nativeCounts = mesh.GetBonesPerVertex(); var nativeValues = mesh.GetAllBoneWeights();
            var result = new FullWeights { Counts = nativeCounts.ToArray(), Values = nativeValues.ToArray(), Offsets = new int[mesh.vertexCount + 1] };
            Need(result.Counts.Length == mesh.vertexCount, "Full variable influence counts must cover every source vertex.");
            int cursor = 0;
            for (int vertex = 0; vertex < mesh.vertexCount; vertex++)
            {
                result.Offsets[vertex] = cursor; double total = 0;
                Need(result.Counts[vertex] > 0, "An original skinned vertex must retain nonzero influences.");
                for (int slot = 0; slot < result.Counts[vertex]; slot++)
                {
                    Need(cursor < result.Values.Length, "Truncated full source skin weights.");
                    var w = result.Values[cursor++];
                    Need(w.boneIndex >= 0 && w.boneIndex < boneCount && Finite(w.weight) && w.weight >= 0, "Invalid original skin influence.");
                    total += w.weight;
                }
                Need(Math.Abs(total - 1) < .0001, "Each complete original vertex influence set must be normalized.");
            }
            result.Offsets[mesh.vertexCount] = cursor;
            Need(cursor == result.Values.Length, "Unexpected extra full source skin weights.");
            return result;
        }

        static void VerifyWeights(FullWeights source, FullWeights actual, int[] map)
        {
            int count = map == null ? source.Counts.Length : map.Length;
            Need(actual.Counts.Length == count, "Full influence vertex correspondence changed.");
            for (int vertex = 0; vertex < count; vertex++)
            {
                int original = map == null ? vertex : map[vertex];
                Need(actual.Counts[vertex] == source.Counts[original], "Variable influence count was truncated/reencoded.");
                for (int i = 0; i < actual.Counts[vertex]; i++)
                {
                    var a = actual.Values[actual.Offsets[vertex] + i]; var b = source.Values[source.Offsets[original] + i];
                    Need(a.boneIndex == b.boneIndex && a.weight.Equals(b.weight), "Full original BoneWeight1 order/index/value changed.");
                }
            }
        }

        static void VerifyUvs(Mesh source, Mesh actual, int[] map)
        {
            for (int channel = 0; channel < 8; channel++)
            {
                var attribute = (VertexAttribute)((int)VertexAttribute.TexCoord0 + channel);
                bool expected = source.HasVertexAttribute(attribute);
                Need(actual.HasVertexAttribute(attribute) == expected, "UV channel presence changed.");
                if (!expected) continue;
                Need(actual.GetVertexAttributeDimension(attribute) == source.GetVertexAttributeDimension(attribute)
                     && actual.GetVertexAttributeFormat(attribute) == source.GetVertexAttributeFormat(attribute), "UV channel format/dimension changed.");
                var before = new List<Vector4>(); var after = new List<Vector4>(); source.GetUVs(channel, before); actual.GetUVs(channel, after);
                Need(after.Count == (map == null ? before.Count : map.Length), "UV vertex correspondence changed.");
                for (int i = 0; i < after.Count; i++) Need(after[i].Equals(before[map == null ? i : map[i]]), "Original UV coordinate changed.");
            }
        }

        static void VerifyColors(Mesh source, Mesh actual, int[] map)
        {
            bool expected = source.HasVertexAttribute(VertexAttribute.Color);
            Need(actual.HasVertexAttribute(VertexAttribute.Color) == expected, "Vertex color presence changed.");
            if (!expected) return;
            Need(actual.GetVertexAttributeFormat(VertexAttribute.Color) == source.GetVertexAttributeFormat(VertexAttribute.Color)
                 && actual.GetVertexAttributeDimension(VertexAttribute.Color) == source.GetVertexAttributeDimension(VertexAttribute.Color), "Vertex color format changed.");
            var a = source.colors; var b = actual.colors;
            Need(b.Length == (map == null ? a.Length : map.Length), "Color vertex correspondence changed.");
            for (int i = 0; i < b.Length; i++) Need(b[i].Equals(a[map == null ? i : map[i]]), "Original vertex color changed.");
        }

        static void EqualVectors(Vector3[] source, Vector3[] actual, int[] map, string label)
        {
            Need(actual.Length == (map == null ? source.Length : source.Length == 0 ? 0 : map.Length), label + " count changed.");
            for (int i = 0; i < actual.Length; i++) Need(actual[i].Equals(source[map == null ? i : map[i]]), label + " changed.");
        }
        static void EqualTangents(Vector4[] source, Vector4[] actual, int[] map, string label)
        {
            Need(actual.Length == (map == null ? source.Length : source.Length == 0 ? 0 : map.Length), label + " count changed.");
            for (int i = 0; i < actual.Length; i++) Need(actual[i].Equals(source[map == null ? i : map[i]]), label + " changed.");
        }
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static void Need(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
#endif
