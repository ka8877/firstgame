using System;
using System.Collections.Generic;
using UnityEngine;

namespace PathOfDawn.Presentation
{
    /// <summary>Publishes an animated head frame without moving a bone or owning expressions.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(10000)]
    public sealed class GospelHeroHeadFrame : MonoBehaviour
    {
        [Serializable]
        public struct Surface
        {
            public Renderer renderer;
            public int materialIndex;
        }

        static readonly int ForwardId = Shader.PropertyToID("_GospelHeadForwardWS");
        static readonly int RightId = Shader.PropertyToID("_GospelHeadRightWS");
        static readonly int UpId = Shader.PropertyToID("_GospelHeadUpWS");
        static readonly int ValidId = Shader.PropertyToID("_GospelHeadFrameValid");

        [SerializeField] Transform head;
        [SerializeField] Vector3 localForward = Vector3.forward;
        [SerializeField] Vector3 localUp = Vector3.up;
        [SerializeField] Surface[] surfaces = Array.Empty<Surface>();
        MaterialPropertyBlock block;
        readonly List<Material> materialScratch = new List<Material>(8);

        public Transform Head => head;

        // The author/Editor caller must provide the verified neutral head-local
        // axes. A humanoid head bone's local +Z is not assumed to face forward.
        public void Configure(Transform animatedHead, Vector3 headLocalForward,
            Vector3 headLocalUp, Surface[] targetSurfaces)
        {
            if (!animatedHead) throw new ArgumentNullException(nameof(animatedHead));
            if (!Finite(headLocalForward) || !Finite(headLocalUp)
                || headLocalForward.sqrMagnitude < 1e-8f
                || Vector3.Cross(headLocalUp, headLocalForward).sqrMagnitude < 1e-8f)
                throw new ArgumentException("Head forward/up must be finite and nonparallel.");
            if (targetSurfaces == null || targetSurfaces.Length == 0)
                throw new ArgumentException("Explicit face material slots are required.");
            foreach (var slot in targetSurfaces)
            {
                if (!slot.renderer || slot.materialIndex < 0
                    || slot.materialIndex >= slot.renderer.sharedMaterials.Length)
                    throw new ArgumentException("A head-frame target material slot is invalid.");
                var material = slot.renderer.sharedMaterials[slot.materialIndex];
                if (!material || !material.HasProperty(ForwardId) || !material.HasProperty(ValidId))
                    throw new ArgumentException("Target shader does not expose the hero head frame.");
            }
            head = animatedHead;
            localForward = headLocalForward.normalized;
            localUp = Vector3.ProjectOnPlane(headLocalUp, localForward).normalized;
            surfaces = (Surface[])targetSurfaces.Clone();
            Publish();
        }

        void OnEnable() => Publish();
        void LateUpdate() => Publish();

        void Publish()
        {
            if (!head || surfaces == null || surfaces.Length == 0) return;
            var forward = head.TransformDirection(localForward).normalized;
            var up = Vector3.ProjectOnPlane(head.TransformDirection(localUp), forward).normalized;
            var right = Vector3.Cross(up, forward).normalized;
            up = Vector3.Cross(forward, right).normalized;
            if (!Finite(forward) || !Finite(right) || !Finite(up) || right.sqrMagnitude < .5f) return;
            if (block == null) block = new MaterialPropertyBlock();
            foreach (var slot in surfaces)
            {
                if (!ValidSlot(slot)) continue;
                block.Clear();
                slot.renderer.GetPropertyBlock(block, slot.materialIndex);
                // GospelNprCharacterView already seeds per-material _Color.
                // Keep its tint and any unrelated overrides. Preserve a prior
                // renderer-wide block if this slot has no block of its own.
                if (block.isEmpty) slot.renderer.GetPropertyBlock(block);
                block.SetVector(ForwardId, new Vector4(forward.x, forward.y, forward.z, 0));
                block.SetVector(RightId, new Vector4(right.x, right.y, right.z, 0));
                block.SetVector(UpId, new Vector4(up.x, up.y, up.z, 0));
                block.SetFloat(ValidId, 1);
                slot.renderer.SetPropertyBlock(block, slot.materialIndex);
            }
        }

        void OnDisable()
        {
            if (surfaces == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            foreach (var slot in surfaces)
            {
                if (!ValidSlot(slot)) continue;
                block.Clear(); slot.renderer.GetPropertyBlock(block, slot.materialIndex);
                if (block.isEmpty) slot.renderer.GetPropertyBlock(block);
                block.SetFloat(ValidId, 0);
                slot.renderer.SetPropertyBlock(block, slot.materialIndex);
            }
        }

        bool ValidSlot(Surface slot)
        {
            if (!slot.renderer || slot.materialIndex < 0) return false;
            materialScratch.Clear(); slot.renderer.GetSharedMaterials(materialScratch);
            return slot.materialIndex < materialScratch.Count;
        }

        static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
