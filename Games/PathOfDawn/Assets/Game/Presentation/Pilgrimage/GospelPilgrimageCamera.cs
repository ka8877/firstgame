using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace PathOfDawn.Presentation
{
    /// <summary>
    /// A perspective exploration camera for the real three-dimensional field.
    /// It observes the physical traveler; orbit, collision and framing never move that traveler.
    /// </summary>
    public sealed class GospelPilgrimageCamera : MonoBehaviour
    {
        public const float MinimumZoom = 2.6f, MaximumZoom = 8.6f;
        public const float DefaultZoom = 5.6f, DefaultYaw = 47f, DefaultPitch = 18f;
        const float MinimumPitch = -4f, MaximumPitch = 62f;
        const float CollisionRadius = .22f, CollisionPadding = .08f;
        const int CameraCollisionMask = ~(1 << 2 | 1 << 5);

        public Camera View { get; private set; }
        public float Zoom => zoom;
        public float Zoom01 => Mathf.InverseLerp(MinimumZoom, MaximumZoom, zoom);
        public float Yaw => yaw;
        public float Pitch => pitch;
        public float ResolvedDistance => cameraDistance;
        public bool IsObstructed { get; private set; }
        public int ObstructionCount { get; private set; }
        public Transform Target { get; private set; }
        public Transform ConversationTarget { get; private set; }
        public bool InputEnabled
        {
            get => inputEnabled;
            set
            {
                inputEnabled = value;
                if (!value) ReleasePointer();
            }
        }

        readonly RaycastHit[] collisionHits = new RaycastHit[64];
        readonly List<RaycastResult> pointerHits = new List<RaycastResult>();
        GospelPilgrimageMotor motor;
        SkinnedMeshRenderer[] targetRenderers;
        bool inputEnabled = true, mouseOrbit, uiOwnsTouchOrbit;
        int orbitTouchId = -1;
        Vector2 previousTouchPosition, pendingUiLook;
        Vector3 focus, focusVelocity, lookAhead, lastTargetPosition;
        float zoom = DefaultZoom, yaw = DefaultYaw, pitch = DefaultPitch;
        float currentYaw = DefaultYaw, currentPitch = DefaultPitch, yawVelocity, pitchVelocity;
        float cameraDistance, distanceVelocity, groundedHeight, actorHeight = 1.5f, nextHeightSample;

        public void Initialize(Camera camera, Transform target)
        {
            View = camera;
            SetObservedTarget(target);
            if (View == null) return;
            View.orthographic = false;
            View.fieldOfView = 47f;
            View.nearClipPlane = .10f;
            View.farClipPlane = 180f;
            View.allowHDR = true;
            View.allowMSAA = true;
            View.allowDynamicResolution = false;
            View.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            View.backgroundColor = new Color(.63f, .79f, .86f);
            var data = View.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.stopNaN = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            Snap();
        }

        public void SetTarget(Transform target)
        {
            SetObservedTarget(target);
            ConversationTarget = null;
            Snap();
        }

        void SetObservedTarget(Transform target)
        {
            Target = target;
            motor = Target != null ? Target.GetComponent<GospelPilgrimageMotor>() : null;
            targetRenderers = null;
            nextHeightSample = 0;
        }

        public void SetZoom01(float value) => SetZoom(Mathf.Lerp(MinimumZoom, MaximumZoom, Mathf.Clamp01(value)));
        public void SetZoom(float value)
        {
            if (Finite(value)) zoom = Mathf.Clamp(value, MinimumZoom, MaximumZoom);
        }
        public void ZoomBy(float delta) { if (Finite(delta)) SetZoom(zoom + delta); }

        /// <summary>The UI owns its right-side touch pad; the camera owns pixel-to-angle sensitivity.</summary>
        public void SetUiTouchCapture(bool enabled)
        {
            if (uiOwnsTouchOrbit == enabled) return;
            uiOwnsTouchOrbit = enabled;
            orbitTouchId = -1;
        }

        public void AddLookDelta(Vector2 pixelDelta)
        {
            if (!InputEnabled || !Finite(pixelDelta.x) || !Finite(pixelDelta.y)) return;
            pendingUiLook += pixelDelta;
        }

        /// <summary>Camera-only orbit; arguments are degrees, independent of frame rate.</summary>
        public void OrbitBy(Vector2 degrees)
        {
            if (!Finite(degrees.x) || !Finite(degrees.y)) return;
            yaw = Mathf.Repeat(yaw + degrees.x, 360f);
            pitch = Mathf.Clamp(pitch + degrees.y, MinimumPitch, MaximumPitch);
        }

        public void SetOrbit(float yawDegrees, float pitchDegrees)
        {
            if (!Finite(yawDegrees) || !Finite(pitchDegrees)) return;
            yaw = Mathf.Repeat(yawDegrees, 360f);
            pitch = Mathf.Clamp(pitchDegrees, MinimumPitch, MaximumPitch);
        }

        public void ResetOrbit()
        {
            yaw = DefaultYaw;
            pitch = DefaultPitch;
            SetZoom(DefaultZoom);
        }

        /// <summary>Optional dialogue framing. Clearing the partner restores the exploration composition.</summary>
        public void SetConversationTarget(Transform partner) => ConversationTarget = partner;

        public void Snap()
        {
            if (View == null || Target == null) return;
            SampleActorHeight(true);
            groundedHeight = Target.position.y;
            lookAhead = Vector3.zero;
            currentYaw = yaw;
            currentPitch = pitch;
            focus = ResolveFocus(DesiredFocus());
            cameraDistance = ResolveDistance(focus, Rotation(), DesiredDistance());
            View.transform.SetPositionAndRotation(focus + Rotation() * (Vector3.back * cameraDistance), Rotation());
            focusVelocity = Vector3.zero;
            distanceVelocity = yawVelocity = pitchVelocity = 0;
            lastTargetPosition = Target.position;
        }

        void Update()
        {
            if (!InputEnabled || View == null || Target == null)
            {
                ReleasePointer();
                return;
            }

            Vector2 uiLook = pendingUiLook;
            pendingUiLook = Vector2.zero;
            if (uiLook.sqrMagnitude > .0001f) ApplyTouchDelta(uiLook);
            bool touchPressed = uiOwnsTouchOrbit ? AnyTouchPressed() : HandleTouchOrbit();
            var mouse = Mouse.current;
            if (!touchPressed && mouse != null)
            {
                Vector2 position = mouse.position.ReadValue();
                bool pressed = mouse.rightButton.isPressed || mouse.leftButton.isPressed;
                if (mouse.rightButton.wasPressedThisFrame || mouse.leftButton.wasPressedThisFrame)
                    mouseOrbit = !OverUi(position);
                if (!pressed) mouseOrbit = false;
                if (mouseOrbit && pressed)
                {
                    Vector2 movement = mouse.delta.ReadValue();
                    float scale = 140f / Mathf.Max(360f, Screen.height);
                    OrbitBy(new Vector2(movement.x * scale, -movement.y * scale));
                }
                if (!OverUi(position))
                {
                    float scroll = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(scroll) > .001f) ZoomBy(-Mathf.Sign(scroll) * .45f);
                }
            }
            else mouseOrbit = false;

            if (Gamepad.current != null)
            {
                Vector2 stick = Gamepad.current.rightStick.ReadValue();
                if (stick.sqrMagnitude > .025f)
                    OrbitBy(new Vector2(stick.x * 115f, -stick.y * 80f) * Time.unscaledDeltaTime);
            }
        }

        bool HandleTouchOrbit()
        {
            var screen = Touchscreen.current;
            if (screen == null) { orbitTouchId = -1; return false; }
            bool anyPressed = false, tracked = false;
            foreach (var touch in screen.touches)
            {
                if (!touch.press.isPressed) continue;
                anyPressed = true;
                int id = touch.touchId.ReadValue();
                Vector2 position = touch.position.ReadValue();
                if (orbitTouchId < 0 && touch.press.wasPressedThisFrame
                    && position.x > Screen.width * .45f && position.y > Screen.height * .14f
                    && position.y < Screen.height * .90f && !OverUi(position))
                {
                    orbitTouchId = id;
                    previousTouchPosition = position;
                }
                if (id != orbitTouchId) continue;
                tracked = true;
                Vector2 movement = position - previousTouchPosition;
                previousTouchPosition = position;
                ApplyTouchDelta(movement);
            }
            if (!tracked) orbitTouchId = -1;
            return anyPressed;
        }

        void ApplyTouchDelta(Vector2 movement)
        {
            float scale = 145f / Mathf.Max(360f, Screen.height);
            OrbitBy(new Vector2(movement.x * scale, -movement.y * scale * .78f));
        }

        static bool AnyTouchPressed()
        {
            if (Touchscreen.current == null) return false;
            foreach (var touch in Touchscreen.current.touches)
                if (touch.press.isPressed) return true;
            return false;
        }

        bool OverUi(Vector2 position)
        {
            if (EventSystem.current == null) return false;
            pointerHits.Clear();
            var pointer = new PointerEventData(EventSystem.current) { position = position };
            EventSystem.current.RaycastAll(pointer, pointerHits);
            return pointerHits.Count != 0;
        }

        void LateUpdate()
        {
            if (View == null || Target == null) return;
            float dt = Mathf.Min(Time.deltaTime, Time.maximumDeltaTime);
            if (dt <= 0) return;
            if ((Target.position - lastTargetPosition).sqrMagnitude > 100f)
            {
                Snap();
                return;
            }
            lastTargetPosition = Target.position;
            SampleActorHeight(false);
            if (motor == null || motor.IsGrounded)
                groundedHeight = Mathf.Lerp(groundedHeight, Target.position.y, 1f - Mathf.Exp(-dt * 14f));
            Vector3 travel = motor != null && InputEnabled ? motor.Velocity : Vector3.zero;
            travel.y = 0;
            lookAhead = Vector3.Lerp(lookAhead, Vector3.ClampMagnitude(travel * .09f, .48f), 1f - Mathf.Exp(-dt * 6f));
            currentYaw = Mathf.SmoothDampAngle(currentYaw, yaw, ref yawVelocity, .055f, Mathf.Infinity, dt);
            currentPitch = Mathf.SmoothDampAngle(currentPitch, pitch, ref pitchVelocity, .075f, Mathf.Infinity, dt);
            focus = ResolveFocus(Vector3.SmoothDamp(focus, DesiredFocus(), ref focusVelocity, .095f, Mathf.Infinity, dt));
            Quaternion rotation = Rotation();
            float distance = ResolveDistance(focus, rotation, DesiredDistance());
            // Move inward immediately to respect geometry; recover outward gently after passing a pillar.
            if (distance < cameraDistance)
            {
                cameraDistance = distance;
                distanceVelocity = 0;
            }
            else cameraDistance = Mathf.SmoothDamp(cameraDistance, distance, ref distanceVelocity, .22f, Mathf.Infinity, dt);
            View.transform.SetPositionAndRotation(focus + rotation * (Vector3.back * cameraDistance), rotation);
        }

        Quaternion Rotation() => Quaternion.Euler(currentPitch, currentYaw, 0);
        float DesiredDistance() => zoom * (actorHeight / 1.5f) + (ConversationTarget != null ? .65f : 0f);
        Vector3 PhysicalFocus()
        {
            Vector3 value = Target.position;
            // A small jump remains legible without making the landscape rise and fall with every step.
            value.y = groundedHeight + actorHeight * .61f
                + Mathf.Clamp(Target.position.y - groundedHeight, 0, 2f) * .20f;
            return value;
        }
        Vector3 DesiredFocus()
        {
            Vector3 value = PhysicalFocus();
            value += lookAhead + Quaternion.Euler(0, currentYaw, 0) * (Vector3.right * .22f);
            if (ConversationTarget != null)
            {
                Vector3 partner = ConversationTarget.position + Vector3.up * actorHeight * .61f;
                value = Vector3.Lerp(value, partner, .32f);
            }
            return value;
        }

        Vector3 ResolveFocus(Vector3 desired)
        {
            // The composition's shoulder offset and follow lag must not put the
            // cast origin through a nearby wall while the capsule turns a corner.
            Vector3 origin = PhysicalFocus();
            Vector3 offset = desired - origin;
            float length = offset.magnitude;
            if (length < .001f) return desired;
            float safeLength = length;
            int count = Physics.SphereCastNonAlloc(origin, .10f, offset / length, collisionHits,
                length, CameraCollisionMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                if (!CameraObstacle(collisionHits[i].collider)) continue;
                safeLength = Mathf.Min(safeLength, Mathf.Max(0, collisionHits[i].distance - .03f));
            }
            return origin + offset * (safeLength / length);
        }

        void SampleActorHeight(bool force)
        {
            if (!force && Time.unscaledTime < nextHeightSample) return;
            nextHeightSample = Time.unscaledTime + .6f;
            targetRenderers = Target.GetComponentsInChildren<SkinnedMeshRenderer>(false);
            bool found = false;
            Bounds bounds = default;
            foreach (var renderer in targetRenderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            // Model bounds establish framing only; neither the capsule nor the saved physical pose is changed.
            actorHeight = found ? Mathf.Clamp(bounds.size.y, 1.2f, 2.2f) : 1.5f;
        }

        float ResolveDistance(Vector3 origin, Quaternion rotation, float requested)
        {
            float distance = requested;
            ObstructionCount = 0;
            int count = Physics.SphereCastNonAlloc(origin, CollisionRadius, rotation * Vector3.back,
                collisionHits, requested, CameraCollisionMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var hit = collisionHits[i];
                if (!CameraObstacle(hit.collider)) continue;
                ObstructionCount++;
                distance = Mathf.Min(distance, Mathf.Max(.38f, hit.distance - CollisionPadding));
            }
            IsObstructed = distance < requested - .01f;
            return distance;
        }

        bool CameraObstacle(Collider obstacle)
        {
            if (obstacle == null || obstacle.transform.IsChildOf(Target)) return false;
            if (obstacle.isTrigger && obstacle.GetComponent<GospelNprCameraScenery>() == null) return false;
            if (obstacle.GetComponentInParent<GospelPilgrimageActor>() != null
                || obstacle.GetComponentInParent<GospelPilgrimageMotor>() != null) return false;
            // The invisible movement fence limits the traveler, not a camera looking into the village.
            return obstacle.gameObject.name != "Diorama exploration boundary";
        }

        void ReleasePointer() { mouseOrbit = false; orbitTouchId = -1; pendingUiLook = Vector2.zero; }
        void OnDisable() => ReleasePointer();
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
