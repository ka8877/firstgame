#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Firstgame.GospelNpr.Editor
{
    /// <summary>Root-only candidate mesh derivation; it never edits the imported hair asset.</summary>
    public static class GospelIllustrationHairline
    {
        public static Mesh BuildEyeClearance(GameObject actor, Animator animator, SkinnedMeshRenderer hair,
            float height, out int adjustedVertices)
        {
            if (Application.isPlaying || !actor || !animator || !hair || !hair.sharedMesh || height <= 0)
                throw new InvalidOperationException("Hair tailoring requires the actual Editor bind-pose actor");
            Mesh original = hair.sharedMesh;
            if (!original.isReadable || original.blendShapeCount != 0)
                throw new InvalidOperationException("This bounded fringe candidate requires the retained readable non-morph hair");
            var left = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            var right = animator.GetBoneTransform(HumanBodyBones.RightEye);
            if (!left || !right) throw new InvalidOperationException("Actual source eye bones are required for fringe clearance");
            Vector3 eyeLeft = actor.transform.InverseTransformPoint(left.position);
            Vector3 eyeRight = actor.transform.InverseTransformPoint(right.position);
            Vector3 eye = (eyeLeft + eyeRight) * .5f;
            float halfSpan = Mathf.Abs(eyeLeft.x - eyeRight.x) * .5f;
            if (halfSpan < .005f || halfSpan > height * .08f)
                throw new InvalidOperationException("Unexpected actual eye span; do not guess a fringe region");
            Matrix4x4 toActor = actor.transform.worldToLocalMatrix * hair.transform.localToWorldMatrix;
            Matrix4x4 fromActor = toActor.inverse;
            Vector3[] vertices = original.vertices;
            var moved = new bool[vertices.Length];
            adjustedVertices = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 point = toActor.MultiplyPoint3x4(vertices[i]);
                float front = Smooth(eye.z - height * .02f, eye.z + height * .02f, point.z);
                float centre = 1 - Smooth(halfSpan * .65f, halfSpan * 1.8f + height * .012f, Mathf.Abs(point.x - eye.x));
                float lower = 1 - Smooth(eye.y + height * .01f, eye.y + height * .065f, point.y);
                float aboveCheek = Smooth(eye.y - height * .06f, eye.y - height * .025f, point.y);
                float lift = Mathf.Clamp(eye.y + height * .022f - point.y, 0, height * .035f)
                    * front * centre * lower * aboveCheek;
                if (lift <= .000001f) continue;
                point.y += lift;
                vertices[i] = fromActor.MultiplyPoint3x4(point);
                moved[i] = true;
                adjustedVertices++;
            }
            var result = UnityEngine.Object.Instantiate(original);
            result.name = actor.name + "__IllustrationEyeClearanceHair";
            result.vertices = vertices;
            // Topology, UVs, bone weights, bindposes and material submeshes are
            // copied intact. Preserve authored shading outside triangles that
            // actually touch the bounded fringe change.
            if (adjustedVertices > 0)
            {
                var affected = (bool[])moved.Clone();
                int[] triangles = original.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                    if (moved[triangles[i]] || moved[triangles[i + 1]] || moved[triangles[i + 2]])
                        affected[triangles[i]] = affected[triangles[i + 1]] = affected[triangles[i + 2]] = true;
                result.RecalculateNormals(); result.RecalculateTangents();
                var normals = result.normals; var originalNormals = original.normals;
                var tangents = result.tangents; var originalTangents = original.tangents;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (affected[i]) continue;
                    if (originalNormals.Length == normals.Length) normals[i] = originalNormals[i];
                    if (originalTangents.Length == tangents.Length) tangents[i] = originalTangents[i];
                }
                result.normals = normals; result.tangents = tangents;
            }
            result.RecalculateBounds();
            return result;
        }

        static float Smooth(float from, float to, float value) =>
            Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from, to, value));
    }
}
#endif
