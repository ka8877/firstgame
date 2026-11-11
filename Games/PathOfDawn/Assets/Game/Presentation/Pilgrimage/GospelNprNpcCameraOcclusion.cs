using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PathOfDawn.Presentation
{
    /// <summary>Temporarily mask only field NPC draws that cover the traveler.
    /// Physical actors, animation, materials and saved state remain untouched.</summary>
    [DisallowMultipleComponent]
    public sealed class GospelNprNpcCameraOcclusion : MonoBehaviour
    {
        const float EnterMargin = .055f, ReleaseMargin = .12f;
        const float NearPlanePadding = .035f;
        readonly Dictionary<Renderer, bool> previousRendering = new Dictionary<Renderer, bool>();
        readonly List<GospelPilgrimageActor> candidates = new List<GospelPilgrimageActor>();
        readonly Plane[] frustumPlanes = new Plane[6];
        readonly Vector3[] nearCorners = new Vector3[4];
        readonly Vector3[] bodyPoints = new Vector3[5];
        HashSet<GospelPilgrimageActor> previousOccluders = new HashSet<GospelPilgrimageActor>();
        HashSet<GospelPilgrimageActor> nextOccluders = new HashSet<GospelPilgrimageActor>();
        GospelPilgrimageApp app;
        GospelPilgrimageCamera rig;
        GospelPilgrimagePeople observedPeople;
        IReadOnlyList<GospelPersonMarker> observedMarkers;
        int observedChildCount = -1;
        Camera appliedCamera;
        Bounds nearPlaneBounds;
        Vector3 cameraPosition;
        float sightlineEndPadding;

        // Observations of the last SceneCamera render, not a control surface.
        public int LastOccludingNpcCount { get; private set; }
        public int LastMaskedRendererCount { get; private set; }
        public Camera LastViewedCamera { get; private set; }
        public Transform LastObservedTarget { get; private set; }
        public int OwnedRendererCount => previousRendering.Count;

        public void Initialize(GospelPilgrimageApp owner, GospelPilgrimageCamera cameraRig)
        {
            RestoreRendering();
            ClearHysteresis();
            app = owner; rig = cameraRig;
            observedPeople = null; observedMarkers = null; observedChildCount = -1;
            candidates.Clear();
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            RestoreRendering();
            ClearHysteresis();
        }

        void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            RestoreRendering();
        }

        void LateUpdate()
        {
            // Recover even if a previous render exited without its end callback.
            // Normal renders already restored every flag in EndCamera.
            if (previousRendering.Count != 0) RestoreRendering();
            if (app == null || !FieldIsActive(app.SceneCamera)) ClearHysteresis();
        }

        bool FieldIsActive(Camera camera)
        {
            return app != null && app.isActiveAndEnabled && app.InGame && app.People != null
                && app.People.gameObject.activeInHierarchy && app.Player != null && app.Actor != null
                && app.Actor.HasSkinnedModel && app.Actor.isActiveAndEnabled
                && app.Actor.CharacterView != null && app.Actor.CharacterView.isActiveAndEnabled
                && app.Actor.CharacterView.VisualRoot != null && app.Actor.CharacterView.VisualRoot.gameObject.activeInHierarchy
                && rig != null && rig.isActiveAndEnabled
                && app.CameraRig == rig && app.SceneCamera == camera && rig.View == camera
                && camera != null && camera.isActiveAndEnabled && camera.cameraType == CameraType.Game
                && !camera.orthographic && rig.Target == app.Player.transform
                && (app.OpeningStage == null || !app.OpeningStage.IsVisible);
        }

        void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            // forceRenderingOff is renderer-global. Never carry our scope into
            // a portrait, another camera, another target or an exceptional frame.
            RestoreRendering();
            if (app == null || camera != app.SceneCamera) return;
            LastViewedCamera = camera; LastObservedTarget = rig != null ? rig.Target : null;
            LastOccludingNpcCount = LastMaskedRendererCount = 0;
            if (!FieldIsActive(camera)) { ClearHysteresis(); return; }

            RefreshCandidates();
            PrepareView(camera);
            nextOccluders.Clear();
            appliedCamera = camera;
            try
            {
                foreach (var actor in candidates)
                {
                    if (!EligibleNpc(actor)) continue;
                    float margin = previousOccluders.Contains(actor) ? ReleaseMargin : EnterMargin;
                    if (!OccludesTraveler(actor, camera, margin)) continue;
                    nextOccluders.Add(actor); LastOccludingNpcCount++;
                    foreach (var renderer in actor.SkinnedRenderers)
                    {
                        if (!EligibleRenderer(renderer, camera) || previousRendering.ContainsKey(renderer)) continue;
                        bool original = renderer.forceRenderingOff;
                        previousRendering.Add(renderer, original);
                        if (!original) { renderer.forceRenderingOff = true; LastMaskedRendererCount++; }
                    }
                }
                var swap = previousOccluders; previousOccluders = nextOccluders; nextOccluders = swap;
            }
            catch
            {
                RestoreRendering(); ClearHysteresis();
                throw;
            }
        }

        void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == appliedCamera) RestoreRendering();
        }

        void RefreshCandidates()
        {
            var people = app.People;
            if (observedPeople == people && ReferenceEquals(observedMarkers, people.Markers)
                && observedChildCount == people.transform.childCount) return;
            observedPeople = people; observedMarkers = people.Markers;
            observedChildCount = people.transform.childCount;
            candidates.Clear();
            // Cached, ownership-local discovery; no per-frame world-wide scan.
            people.GetComponentsInChildren(true, candidates);
            ClearHysteresis();
        }

        bool EligibleNpc(GospelPilgrimageActor actor)
        {
            return actor != null && actor != app.Actor && actor.IsNpc && actor.HasSkinnedModel
                && actor.isActiveAndEnabled && actor.transform.IsChildOf(app.People.transform)
                && !actor.transform.IsChildOf(rig.Target);
        }

        static bool EligibleRenderer(SkinnedMeshRenderer renderer, Camera camera)
        {
            return renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy
                && renderer.gameObject.layer != GospelNprCharacterView.PortraitLayer
                && (camera.cullingMask & (1 << renderer.gameObject.layer)) != 0;
        }

        void PrepareView(Camera camera)
        {
            cameraPosition = camera.transform.position;
            GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);
            camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), camera.nearClipPlane,
                Camera.MonoOrStereoscopicEye.Mono, nearCorners);
            nearPlaneBounds = new Bounds(camera.transform.TransformPoint(nearCorners[0]), Vector3.zero);
            for (int i = 1; i < nearCorners.Length; i++) nearPlaneBounds.Encapsulate(camera.transform.TransformPoint(nearCorners[i]));
            nearPlaneBounds.Expand(NearPlanePadding * 2);

            float height = Mathf.Clamp(app.Actor.VisualHeight, 1.2f, 2.2f);
            Vector3 foot = rig.Target.position;
            bodyPoints[0] = foot + Vector3.up * (height * .28f);
            bodyPoints[1] = foot + Vector3.up * (height * .58f);
            bodyPoints[2] = foot + Vector3.up * (height * .88f);
            Vector3 shoulder = camera.transform.right * (height * .12f);
            bodyPoints[3] = bodyPoints[1] + shoulder;
            bodyPoints[4] = bodyPoints[1] - shoulder;
            sightlineEndPadding = Mathf.Max(.14f, height * .12f);
        }

        bool OccludesTraveler(GospelPilgrimageActor actor, Camera camera, float margin)
        {
            // Use each actual mesh bound, not a union spanning empty space
            // between an outstretched hand, hair and separate garment pieces.
            foreach (var renderer in actor.SkinnedRenderers)
            {
                if (!EligibleRenderer(renderer, camera) || renderer.forceRenderingOff) continue;
                Bounds bounds = renderer.bounds;
                if (!Finite(bounds.center) || !Finite(bounds.extents)) continue;
                bounds.Expand(margin * 2);
                if (!GeometryUtility.TestPlanesAABB(frustumPlanes, bounds)) continue;
                if (bounds.Contains(cameraPosition) || bounds.Intersects(nearPlaneBounds)) return true;
                foreach (var point in bodyPoints)
                {
                    Vector3 delta = point - cameraPosition;
                    float length = delta.magnitude;
                    if (length <= sightlineEndPadding) continue;
                    if (bounds.IntersectRay(new Ray(cameraPosition, delta / length), out float distance)
                        && distance >= 0 && distance < length - sightlineEndPadding) return true;
                }
            }
            return false;
        }

        void RestoreRendering()
        {
            foreach (var pair in previousRendering)
                if (pair.Key != null) pair.Key.forceRenderingOff = pair.Value;
            previousRendering.Clear(); appliedCamera = null;
        }

        void ClearHysteresis()
        {
            previousOccluders.Clear(); nextOccluders.Clear();
        }

        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
