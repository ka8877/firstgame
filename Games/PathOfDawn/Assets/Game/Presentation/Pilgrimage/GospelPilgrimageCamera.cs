using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace PathOfDawn.Presentation
{
    /// <summary>High oblique HD2D framing: no side-scroll constraint and no orbit-induced sprite flip.</summary>
    public sealed class GospelPilgrimageCamera : MonoBehaviour
    {
        public const float MinimumZoom = 5.0f, MaximumZoom = 12f;
        public Camera View { get; private set; }
        public float Zoom => zoom;
        public float Zoom01 => Mathf.InverseLerp(MinimumZoom, MaximumZoom, zoom);
        public bool InputEnabled { get; set; } = true;
        public Transform Target { get; private set; }
        float zoom = 5.7f, zoomVelocity, groundedHeight;
        Vector3 velocity, lookAhead;
        GospelPilgrimageMotor motor;
        public void Initialize(Camera camera, Transform target)
        {
            View = camera; Target = target; motor = target != null ? target.GetComponent<GospelPilgrimageMotor>() : null;
            View.orthographic = true; View.orthographicSize = zoom; View.nearClipPlane = .15f; View.farClipPlane = 130;
            View.allowHDR = true; View.clearFlags = CameraClearFlags.SolidColor; View.backgroundColor = new Color(.41f, .62f, .61f);
            var data = View.GetUniversalAdditionalCameraData(); data.renderPostProcessing = true;
            // Source materials must already be finite. This retained URP guard
            // additionally prevents an unrelated invalid pixel from spreading
            // through bloom on graphics drivers with different NaN handling.
            data.stopNaN = true;
            // Pixel source edges must stay crisp; environment depth and lighting
            // supply the smoothing instead of post-process edge AA on sprites.
            data.antialiasing = AntialiasingMode.None;
            View.transform.rotation = Quaternion.Euler(43f, 0, 0); Snap();
        }
        public void SetTarget(Transform target) { Target = target; motor = target != null ? target.GetComponent<GospelPilgrimageMotor>() : null; Snap(); }
        public void SetZoom01(float value) => SetZoom(Mathf.Lerp(MinimumZoom, MaximumZoom, Mathf.Clamp01(value)));
        public void SetZoom(float value) { if (!float.IsNaN(value) && !float.IsInfinity(value)) zoom = Mathf.Clamp(value, MinimumZoom, MaximumZoom); }
        public void ZoomBy(float delta) => SetZoom(zoom + delta);
        Vector3 DesiredPosition()
        {
            var focus = Target.position;
            // Keep the landscape steady through a jump. The character travels
            // in the composition while the view retains its ground reference.
            focus.y = groundedHeight + .78f + Mathf.Clamp(Target.position.y - groundedHeight, 0, 2) * .10f;
            return focus + lookAhead + View.transform.rotation * (Vector3.back * 18f);
        }
        public void Snap()
        {
            if (View == null || Target == null) return;
            groundedHeight = Target.position.y; lookAhead = Vector3.zero;
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
            if (dt <= 0) return;
            if (motor == null || motor.IsGrounded)
                groundedHeight = Mathf.Lerp(groundedHeight, Target.position.y, 1 - Mathf.Exp(-dt * 12));
            Vector3 travel = motor != null && InputEnabled ? motor.Velocity : Vector3.zero;
            travel.y = 0;
            Vector3 lead = Vector3.ClampMagnitude(travel * .17f, 1.05f);
            lookAhead = Vector3.Lerp(lookAhead, lead, 1 - Mathf.Exp(-dt * 5.4f));
            View.transform.position = Vector3.SmoothDamp(View.transform.position, DesiredPosition(), ref velocity, motor != null && motor.IsDashing ? .105f : .16f, Mathf.Infinity, dt);
            View.orthographicSize = Mathf.SmoothDamp(View.orthographicSize, zoom, ref zoomVelocity, .14f, Mathf.Infinity, dt);
        }
    }
}
