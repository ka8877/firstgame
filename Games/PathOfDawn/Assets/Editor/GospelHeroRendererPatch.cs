#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using PathOfDawn.Presentation;
using UnityEditor;
using UnityEngine;

namespace Firstgame.GospelNpr.Editor
{
    /// <summary>Caller-owned instance integration; explicit surface roles, no model/age conversion.</summary>
    public static class GospelHeroRendererPatch
    {
        public enum Role { Face, Eyes, Hair }
        [Serializable]
        public sealed class Slot
        {
            public Renderer renderer;
            public int materialIndex;
            public Role role;
            public bool alphaClip;
            public float cutoff = .5f;
            public GospelHeroMaterialSetup.EyeOptions eye = new GospelHeroMaterialSetup.EyeOptions();
            public GospelHeroMaterialSetup.HairFlow hairFlow;
            public Vector2 hairUvFlow = Vector2.right;
            public float hairRotationDegrees;
        }
        [Serializable]
        public sealed class Plan
        {
            public string actorId; // Existing logical ID; never written back into Core/save data.
            public string outputVersion;
            public Texture2D authoredFaceSdf;
            public bool enableFaceSdf; // False is an honest normal-shading fallback, not a generated mask.
            public Vector3 verifiedHeadLocalForward, verifiedHeadLocalUp;
            public Slot[] slots;
        }
        public sealed class Result
        {
            public string actorId, materialFolder;
            public Material[] derivedMaterials;
            public GospelHeroHeadFrame headFrame;
            public bool meshAvatarControllerAndLocalTransformsRetained;
            public bool faceSdfEnabled;
            public bool actualVisualReviewCompleted; // Always false here; Root owns actual review.
        }

        public static Result Apply(GameObject workingModel, Animator animator, Plan plan)
        {
            if (!workingModel || EditorUtility.IsPersistent(workingModel) || !animator
                || !animator.avatar || !animator.avatar.isValid || !animator.isHuman
                || animator.transform != workingModel.transform && !animator.transform.IsChildOf(workingModel.transform))
                throw new ArgumentException("Use a new caller-owned model instance/prefab contents with its own Animator.");
            if (plan == null || plan.slots == null || plan.slots.Length == 0
                || !new[] { "JesusChild", "JesusAdult", "Mary", "Joseph", "Teacher" }.Contains(plan.actorId)
                || string.IsNullOrEmpty(plan.outputVersion)
                || plan.outputVersion.Any(c => !(char.IsLetterOrDigit(c) || c == '-' || c == '_')))
                throw new ArgumentException("An explicit current actor ID, new output version and surface slots are required.");
            if (!Finite(plan.verifiedHeadLocalForward) || !Finite(plan.verifiedHeadLocalUp)
                || Vector3.Cross(plan.verifiedHeadLocalForward, plan.verifiedHeadLocalUp).sqrMagnitude < 1e-8f)
                throw new ArgumentException("Provide independently verified neutral head-local axes.");

            var seen = new HashSet<string>();
            var sources = new Material[plan.slots.Length];
            var faceSlots = new List<GospelHeroHeadFrame.Surface>();
            for (int i = 0; i < plan.slots.Length; i++)
            {
                var slot = plan.slots[i];
                if (slot == null || !slot.renderer || !Enum.IsDefined(typeof(Role), slot.role)
                    || slot.renderer.transform != workingModel.transform && !slot.renderer.transform.IsChildOf(workingModel.transform))
                    throw new ArgumentException("Each explicit renderer slot must belong to the working model.");
                var materials = slot.renderer.sharedMaterials;
                if (slot.materialIndex < 0 || slot.materialIndex >= materials.Length || !materials[slot.materialIndex]
                    || !seen.Add(slot.renderer.GetInstanceID() + ":" + slot.materialIndex))
                    throw new ArgumentException("Duplicate/invalid renderer material slot.");
                sources[i] = materials[slot.materialIndex];
                if (slot.role == Role.Face)
                    faceSlots.Add(new GospelHeroHeadFrame.Surface { renderer = slot.renderer, materialIndex = slot.materialIndex });
            }
            if (faceSlots.Count == 0) throw new ArgumentException("At least one authored face-SDF surface is required.");

            var skins = workingModel.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var meshes = skins.Select(r => r.sharedMesh).ToArray();
            var transforms = workingModel.GetComponentsInChildren<Transform>(true);
            var positions = transforms.Select(t => t.localPosition).ToArray();
            var rotations = transforms.Select(t => t.localRotation).ToArray();
            var scales = transforms.Select(t => t.localScale).ToArray();
            Avatar avatar = animator.avatar;
            RuntimeAnimatorController controller = animator.runtimeAnimatorController;
            bool rootMotion = animator.applyRootMotion;
            string folder = GospelHeroMaterialSetup.OutputRoot + "/" + plan.outputVersion + "/" + plan.actorId;
            var derived = new Material[plan.slots.Length];
            // Persist all material copies before changing renderer pointers.
            // On any failure retain outputs; use a fresh version for the retry.
            for (int i = 0; i < plan.slots.Length; i++)
            {
                var slot = plan.slots[i];
                string output = folder + "/Surface" + i.ToString("D3") + "_" + slot.role + ".mat";
                switch (slot.role)
                {
                    case Role.Face:
                        derived[i] = plan.enableFaceSdf
                            ? GospelHeroMaterialSetup.CreateFace(sources[i], plan.authoredFaceSdf, output, slot.alphaClip, slot.cutoff)
                            : GospelHeroMaterialSetup.CreateFaceNormalFallback(sources[i], output, slot.alphaClip, slot.cutoff); break;
                    case Role.Eyes:
                        derived[i] = GospelHeroMaterialSetup.CreateEyes(sources[i], slot.eye, output); break;
                    case Role.Hair:
                        derived[i] = GospelHeroMaterialSetup.CreateHair(sources[i], slot.hairFlow, slot.hairUvFlow,
                            slot.hairRotationDegrees, output, slot.alphaClip, slot.cutoff); break;
                }
            }
            for (int i = 0; i < plan.slots.Length; i++)
                GospelHeroMaterialSetup.Assign(plan.slots[i].renderer, plan.slots[i].materialIndex, derived[i]);
            var headFrame = GospelHeroMaterialSetup.BindHeadFrame(workingModel, animator,
                plan.verifiedHeadLocalForward, plan.verifiedHeadLocalUp, faceSlots.ToArray());

            bool retained = animator.avatar == avatar && animator.runtimeAnimatorController == controller && animator.applyRootMotion == rootMotion
                && skins.Select((r, i) => r.sharedMesh == meshes[i]).All(v => v)
                && transforms.Select((t, i) => t.localPosition.Equals(positions[i]) && t.localRotation.Equals(rotations[i]) && t.localScale.Equals(scales[i])).All(v => v);
            if (!retained) throw new InvalidOperationException("Rendering configuration must not alter mesh pointers, Avatar/controller, motor root-motion ownership or local transforms.");
            return new Result { actorId = plan.actorId, materialFolder = folder, derivedMaterials = derived,
                headFrame = headFrame, meshAvatarControllerAndLocalTransformsRetained = true,
                faceSdfEnabled = plan.enableFaceSdf, actualVisualReviewCompleted = false };
        }

        static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
#endif
