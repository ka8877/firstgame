using System;
using System.Collections.Generic;
using UnityEngine;

namespace PathOfDawn.Presentation
{
    /// <summary>
    /// Caller-owned presentation-only models for a portrait whose person has not
    /// entered the field yet. No NPC marker, controller, quest or save is created.
    /// </summary>
    [DefaultExecutionOrder(90)]
    public sealed class GospelNprPortraitCache : MonoBehaviour
    {
        readonly Dictionary<string, GospelNprCharacterView> views = new Dictionary<string, GospelNprCharacterView>(StringComparer.Ordinal);
        int placement;
        public int Count => views.Count;

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
        }

        void OnDisable() => HideAll();
        void OnDestroy() => ReleaseAll();
    }
}
