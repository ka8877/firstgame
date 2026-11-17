using System;
using System.Collections.Generic;
using UnityEngine;

namespace PathOfDawn.Presentation
{
    /// <summary>
    /// Caller-owned illustrated UI portraits and optional presentation-only
    /// model inspection. An illustrated request never creates a world person,
    /// controller, skinned model or render camera.
    /// </summary>
    [DefaultExecutionOrder(90)]
    public sealed class GospelNprPortraitCache : MonoBehaviour
    {
        public const string IllustrationResourceRoot = "Art/GospelIllustrated/Portraits/";
        readonly Dictionary<string, GospelNprCharacterView> views = new Dictionary<string, GospelNprCharacterView>(StringComparer.Ordinal);
        readonly Dictionary<string, Texture2D> illustrations = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        int placement;
        public int Count => views.Count;
        public int IllustrationCount
        {
            get
            {
                int count = 0;
                foreach (var portrait in illustrations.Values) if (portrait != null) count++;
                return count;
            }
        }

        /// <summary>Resolve the authored UI art without requesting any 3D portrait.</summary>
        public Texture2D GetIllustratedPortrait(string identity, bool adult)
        {
            string resource = IllustrationResourceFor(identity, adult);
            if (resource == null) return null;
            if (illustrations.TryGetValue(resource, out var portrait)) return portrait;
            portrait = Resources.Load<Texture2D>(resource);
            illustrations.Add(resource, portrait);
            if (portrait == null)
                Debug.LogWarning("GOSPEL_ILLUSTRATED_PORTRAIT_MISSING resource=" + resource);
            return portrait;
        }

        /// <summary>Unknown or unseen speakers have no substitute human face.</summary>
        public static string IllustrationResourceFor(string identity, bool adult)
        {
            if (string.IsNullOrWhiteSpace(identity)) return null;
            string role;
            switch (identity.Trim().ToLowerInvariant())
            {
                case "jesus": role = adult ? "JesusAdult" : "JesusChild"; break;
                case "jesus-child":
                case "jesuschild": role = "JesusChild"; break;
                case "jesus-adult":
                case "jesusadult": role = "JesusAdult"; break;
                case "mary": role = "Mary"; break;
                case "joseph": role = "Joseph"; break;
                case "teacher":
                case "temple-guide":
                case "teacher-listening":
                case "teacher-reading":
                case "jordan-guide":
                case "john-baptist":
                case "jordan-witness":
                case "journey-narrator": role = "Teacher"; break;
                case "shepherd": role = "Shepherd"; break;
                default: return null;
            }
            return IllustrationResourceRoot + role;
        }

        public GospelNprCharacterView GetOrCreate(string modelIdentity)
        {
            if (views.TryGetValue(modelIdentity, out var view) && view != null) return view;
            float height = GospelNprCharacterView.HeightFor(modelIdentity);
            var owner = new GameObject("Portrait-only NPR model · " + modelIdentity);
            owner.transform.SetParent(transform, false);
            owner.transform.position = new Vector3(4096 + placement++ * 24, -4096, 4096);
            view = owner.AddComponent<GospelNprCharacterView>();
            view.LoadModel(modelIdentity, height, Color.white);
            view.SetMotion(Vector3.zero, true, 0);
            // The real model is required to validate its rig and produce the
            // matching portrait, but its first copy never renders in the field.
            foreach (var renderer in view.SkinnedRenderers) renderer.enabled = false;
            // The hidden validation copy never animates. Its portrait clone is
            // instantiated independently from the authored prefab and keeps its
            // own Animator and actual visible expression updates.
            if (view.Animator != null) view.Animator.enabled = false;
            views[modelIdentity] = view;
            return view;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, Time.maximumDeltaTime);
            foreach (var view in views.Values)
                if (view != null && view.HasVisiblePortrait) view.Tick(dt);
        }

        public void HideAll()
        {
            foreach (var view in views.Values)
                if (view != null) { view.SetSpeaking(false); view.SetPortraitVisible(false); }
        }

        public void ReleaseAll()
        {
            foreach (var view in views.Values)
            {
                if (view == null) continue;
                view.SetSpeaking(false); view.ReleasePortrait();
                view.gameObject.SetActive(false);
                Destroy(view.gameObject);
            }
            views.Clear();
            // Resource textures are imported shared assets. Release only this
            // caller's references; never destroy or modify the authored art.
            illustrations.Clear();
        }

        void OnDisable() => HideAll();
        void OnDestroy() => ReleaseAll();
    }
}
