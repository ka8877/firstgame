using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace PathOfDawn.Presentation
{
    /// <summary>High oblique HD2D framing: no side-scroll constraint and no orbit-induced sprite flip.</summary>
    public sealed class GospelPilgrimageCamera : MonoBehaviour
    {
        public const float MinimumZoom = 5.5f, MaximumZoom = 12f;
        public Camera View { get; private set; }
        public float Zoom => zoom;
        public float Zoom01 => Mathf.InverseLerp(MinimumZoom, MaximumZoom, zoom);
        public bool InputEnabled { get; set; } = true;
        public Transform Target { get; private set; }
        float zoom = 7.2f, zoomVelocity;
        Vector3 velocity;
        public void Initialize(Camera camera, Transform target)
        {
            View = camera; Target = target;
            View.orthographic = true; View.orthographicSize = zoom; View.nearClipPlane = .15f; View.farClipPlane = 130;
            View.allowHDR = true; View.clearFlags = CameraClearFlags.SolidColor; View.backgroundColor = new Color(.67f, .73f, .61f);
            var data = View.GetUniversalAdditionalCameraData(); data.renderPostProcessing = true;
            // Pixel source edges must stay crisp; environment depth and lighting
            // supply the smoothing instead of post-process edge AA on sprites.
            data.antialiasing = AntialiasingMode.None;
            View.transform.rotation = Quaternion.Euler(48f, 0, 0); Snap();
        }
        public void SetTarget(Transform target) { Target = target; Snap(); }
        public void SetZoom01(float value) => SetZoom(Mathf.Lerp(MinimumZoom, MaximumZoom, Mathf.Clamp01(value)));
        public void SetZoom(float value) { if (!float.IsNaN(value) && !float.IsInfinity(value)) zoom = Mathf.Clamp(value, MinimumZoom, MaximumZoom); }
        public void ZoomBy(float delta) => SetZoom(zoom + delta);
        Vector3 DesiredPosition() => Target.position + Vector3.up * .8f + View.transform.rotation * (Vector3.back * 18f);
        public void Snap()
        {
            if (View == null || Target == null) return;
            View.transform.position = DesiredPosition(); View.orthographicSize = zoom;
            velocity = Vector3.zero; zoomVelocity = 0;
        }
        void Update()
        {
            if (!InputEnabled || View == null || Mouse.current == null) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > .001f) ZoomBy(-Mathf.Sign(scroll) * .55f);
        }
        void LateUpdate()
        {
            if (View == null || Target == null) return;
            float dt = Mathf.Min(Time.deltaTime, Time.maximumDeltaTime);
            View.transform.position = Vector3.SmoothDamp(View.transform.position, DesiredPosition(), ref velocity, .13f, Mathf.Infinity, dt);
            View.orthographicSize = Mathf.SmoothDamp(View.orthographicSize, zoom, ref zoomVelocity, .14f, Mathf.Infinity, dt);
        }
    }
}
