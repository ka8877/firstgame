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
        const float MaximumAvoidancePitch = 80f, ClearanceReleaseSeconds = .28f;
        static readonly float[] AvoidanceYawOffsets = { 30, -30, 60, -60, 90, -90 };
        static readonly float[] SideViewPitches = { 35, 50, 65 };
        const int CameraCollisionMask = ~(1 << 2 | 1 << 5);

        public Camera View { get; private set; }
        public float Zoom => zoom;
        public float Zoom01 => Mathf.InverseLerp(MinimumZoom, MaximumZoom, zoom);
        public float Yaw => yaw;
        public float Pitch => pitch;
        public float ResolvedDistance => cameraDistance;
        public float EffectiveYaw => effectiveYaw;
        public float EffectivePitch => effectivePitch;
        public bool IsAvoidingObstruction { get; private set; }
        public float DesiredPathDistance { get; private set; }
        public float FinalPathDistance { get; private set; }
        public float ComfortableDistance { get; private set; }
        public int FinalObstructionCount { get; private set; }
        public int AvoidanceProbeCount { get; private set; }
        public bool HasReadableView { get; private set; }
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
        readonly Collider[] initialOverlaps = new Collider[64], endpointOverlaps = new Collider[64];
        readonly Vector3[] bodyPoints = new Vector3[5];
        int initialOverlapCount, initialObstacleCount;
        Transform targetHead, targetHips, targetLeftFoot, targetRightFoot;
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
        float effectiveYaw = DefaultYaw, effectivePitch = DefaultPitch, renderYawVelocity, renderPitchVelocity;
        float clearSeconds, avoidanceYawOffset, avoidancePitch = DefaultPitch;

        struct CameraPose
        {
            public float Yaw, Pitch, Distance;
            public int Obstacles;
            public bool Valid;
            public Quaternion Rotation => Quaternion.Euler(Pitch, Yaw, 0);
            public Vector3 Direction => Rotation * Vector3.back;
        }

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
            targetHead = targetHips = targetLeftFoot = targetRightFoot = null;
            ResetAvoidance();
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
            ResetAvoidance();
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
            ResetAvoidance();
            focus = ResolveFocus(DesiredFocus());
            var pose = ResolveCameraPose(focus, DesiredDistance(), 0, true);
            cameraDistance = pose.Distance;
            View.transform.SetPositionAndRotation(focus + pose.Direction * cameraDistance, pose.Rotation);
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
            var pose = ResolveCameraPose(focus, DesiredDistance(), dt, false);
            if (pose.Distance < cameraDistance)
            {
                cameraDistance = pose.Distance; distanceVelocity = 0;
            }
            else
            {
                // A safe escape must not spend recovery frames inside the body.
                if (pose.Distance >= ComfortableDistance && cameraDistance < ComfortableDistance)
                { cameraDistance = ComfortableDistance; distanceVelocity = 0; }
                cameraDistance = Mathf.SmoothDamp(cameraDistance, pose.Distance, ref distanceVelocity, .22f, Mathf.Infinity, dt);
            }
            // Even distance recovery is checked at its actual endpoint. Initial
            // focus overlap can make an earlier, shorter point less safe.
            Vector3 endpoint = focus + pose.Direction * cameraDistance;
            if (Mathf.Abs(cameraDistance - pose.Distance) > .001f &&
                (!PositionClear(endpoint, out _) || !BodySightlinesClear(endpoint, out _)
                || IsAvoidingObstruction && !Readable(endpoint, pose.Rotation)))
            { cameraDistance = pose.Distance; distanceVelocity = 0; }
            HasReadableView = pose.Valid && Readable(focus + pose.Direction * cameraDistance, pose.Rotation);
            View.transform.SetPositionAndRotation(focus + pose.Direction * cameraDistance, pose.Rotation);
        }

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
            int count = Physics.SphereCastNonAlloc(origin, CollisionRadius, offset / length, collisionHits,
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
            // Authored stature stays stable while the idle head/cloth bounds
            // animate. Generic targets without a ready actor retain measured bounds.
            var actor = Target.GetComponentInChildren<GospelPilgrimageActor>(true);
            float stature = actor != null && actor.HasSkinnedModel ? actor.VisualHeight : 0;
            actorHeight = Finite(stature) && stature > 0 ? Mathf.Clamp(stature, 1.2f, 2.2f)
                : found ? Mathf.Clamp(bounds.size.y, 1.2f, 2.2f) : 1.5f;
            var animator = actor != null ? actor.Animator : null;
            targetHead = animator != null ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            targetHips = animator != null ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            targetLeftFoot = animator != null ? animator.GetBoneTransform(HumanBodyBones.LeftFoot) : null;
            targetRightFoot = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightFoot) : null;
        }

        CameraPose ResolveCameraPose(Vector3 origin, float requested, float dt, bool snap)
        {
            AvoidanceProbeCount = 0;
            ComfortableDistance = Mathf.Min(requested, Mathf.Clamp(actorHeight * 1.65f, 2.35f, 2.8f));
            PrepareBodyPoints();
            initialOverlapCount = Physics.OverlapSphereNonAlloc(origin, CollisionRadius, initialOverlaps,
                CameraCollisionMask, QueryTriggerInteraction.Collide);
            initialObstacleCount = 0;
            for (int i = 0; i < initialOverlapCount; i++) if (CameraObstacle(initialOverlaps[i])) initialObstacleCount++;

            CameraPose desired = Probe(origin, currentYaw, currentPitch, requested);
            // An origin already inside the expanded clearance volume is a real
            // requested-path obstruction, even if a candidate can escape it.
            DesiredPathDistance = initialObstacleCount != 0 ? 0 : desired.Distance;
            ObstructionCount = desired.Obstacles + initialObstacleCount;
            bool desiredClear = desired.Valid && desired.Distance >= Mathf.Min(requested, ComfortableDistance + .18f);
            clearSeconds = desiredClear ? clearSeconds + dt : 0;
            bool keepAvoidance = IsAvoidingObstruction && (!desiredClear || clearSeconds < ClearanceReleaseSeconds);
            CameraPose selected = desired;
            if (!desired.Valid || desired.Distance < ComfortableDistance || keepAvoidance)
                selected = FindEscape(origin, requested, desired, keepAvoidance);
            else { avoidanceYawOffset = 0; avoidancePitch = currentPitch; }

            float targetYaw = selected.Yaw, targetPitch = selected.Pitch;
            float smoothTime = desiredClear ? .30f : .08f;
            float nextYaw = snap ? targetYaw : Mathf.SmoothDampAngle(effectiveYaw, targetYaw, ref renderYawVelocity, smoothTime, Mathf.Infinity, dt);
            float nextPitch = snap ? targetPitch : Mathf.SmoothDamp(effectivePitch, targetPitch, ref renderPitchVelocity, smoothTime, Mathf.Infinity, dt);
            if (!IsAvoidingObstruction && Mathf.Abs(Mathf.DeltaAngle(targetYaw, currentYaw)) < .01f && Mathf.Abs(targetPitch - currentPitch) < .01f)
            { nextYaw = currentYaw; nextPitch = currentPitch; }
            CameraPose actual = Mathf.Abs(Mathf.DeltaAngle(nextYaw, desired.Yaw)) < .001f
                && Mathf.Abs(nextPitch - desired.Pitch) < .001f && Mathf.Abs(selected.Distance - desired.Distance) < .001f
                ? desired : Probe(origin, nextYaw, nextPitch, selected.Distance);
            // Do not interpolate through an occluder or an unreadable close-up.
            // Safety can move the camera immediately; it never moves its owner.
            if (selected.Valid && (!actual.Valid || actual.Distance < ComfortableDistance
                || !Readable(origin + actual.Direction * actual.Distance, actual.Rotation)))
            { actual = selected; renderYawVelocity = renderPitchVelocity = 0; }
            effectiveYaw = Mathf.Repeat(actual.Yaw, 360); effectivePitch = actual.Pitch;
            IsAvoidingObstruction = Mathf.Abs(Mathf.DeltaAngle(effectiveYaw, currentYaw)) > .1f
                || Mathf.Abs(effectivePitch - currentPitch) > .1f;
            FinalPathDistance = actual.Distance; FinalObstructionCount = actual.Obstacles;
            IsObstructed = !actual.Valid || DesiredPathDistance < requested - .01f || actual.Distance < requested - .01f || IsAvoidingObstruction;
            HasReadableView = actual.Valid && Readable(origin + actual.Direction * actual.Distance, actual.Rotation);
            return actual;
        }

        CameraPose FindEscape(Vector3 origin, float requested, CameraPose desired, bool retain)
        {
            float range = Mathf.Min(requested, ComfortableDistance + .12f);
            float gentleLimit = Mathf.Min(currentPitch + 30f, Mathf.Max(55f, currentPitch));
            CameraPose best = desired, heldHigh = default;
            bool deferHigh = false;
            if (retain)
            {
                var held = Probe(origin, currentYaw + avoidanceYawOffset, Mathf.Max(currentPitch, avoidancePitch), range);
                bool comfortable = Comfortable(origin, held);
                bool highSameYaw = Mathf.Abs(Mathf.DeltaAngle(held.Yaw, currentYaw)) < .1f && held.Pitch > gentleLimit;
                if (comfortable && !highSameYaw) return held;
                if (comfortable) { heldHigh = held; deferHigh = true; }
                if (held.Valid && (!best.Valid || held.Distance > best.Distance)) best = held;
            }
            // Preserve the existing bounded angle set, but prefer a gentle
            // requested-yaw rise before rotating to a natural side view.
            int steps = Mathf.CeilToInt(Mathf.Max(0, MaximumAvoidancePitch - currentPitch) / 6f);
            for (int i = 1; i <= steps; i++)
            {
                float angle = Mathf.Min(MaximumAvoidancePitch, currentPitch + i * 6f);
                if (angle > gentleLimit) continue;
                var candidate = Probe(origin, currentYaw, angle, range);
                if (Comfortable(origin, candidate)) { avoidanceYawOffset = 0; avoidancePitch = angle; return candidate; }
                if (candidate.Valid && (!best.Valid || candidate.Distance > best.Distance)) best = candidate;
            }
            // Favor the lower side-view pitch across the same existing yaw
            // offsets before accepting a steeper side view near the original yaw.
            float preferredSign = avoidanceYawOffset < 0 ? -1 : 1;
            foreach (float elevation in SideViewPitches)
                foreach (float offset in AvoidanceYawOffsets)
                {
                    float yawOffset = offset * preferredSign;
                    float angle = Mathf.Max(currentPitch, elevation);
                    var candidate = Probe(origin, currentYaw + yawOffset, angle, range);
                    if (Comfortable(origin, candidate))
                    { avoidanceYawOffset = yawOffset; avoidancePitch = angle; return candidate; }
                    if (candidate.Valid && (!best.Valid || candidate.Distance > best.Distance)) best = candidate;
                }
            // A retained overhead view must not preempt a newly available
            // gentle/side view, but stays stable if those alternatives fail.
            if (deferHigh) return heldHigh;
            for (int i = 1; i <= steps; i++)
            {
                float angle = Mathf.Min(MaximumAvoidancePitch, currentPitch + i * 6f);
                if (angle <= gentleLimit) continue;
                var candidate = Probe(origin, currentYaw, angle, range);
                if (Comfortable(origin, candidate)) { avoidanceYawOffset = 0; avoidancePitch = angle; return candidate; }
                if (candidate.Valid && (!best.Valid || candidate.Distance > best.Distance)) best = candidate;
            }
            // Closed geometry can admit no readable camera. Retain the best
            // geometrically validated view; do not claim or fabricate success.
            avoidanceYawOffset = Mathf.DeltaAngle(currentYaw, best.Yaw); avoidancePitch = best.Pitch;
            return best;
        }

        bool Comfortable(Vector3 origin, CameraPose pose) => pose.Valid && pose.Distance >= ComfortableDistance
            && Readable(origin + pose.Direction * pose.Distance, pose.Rotation);

        CameraPose Probe(Vector3 origin, float renderYaw, float renderPitch, float requested)
        {
            AvoidanceProbeCount++;
            var pose = new CameraPose { Yaw = renderYaw, Pitch = Mathf.Clamp(renderPitch, MinimumPitch, MaximumAvoidancePitch), Distance = Mathf.Max(0, requested) };
            int count = Physics.SphereCastNonAlloc(origin, CollisionRadius, pose.Direction, collisionHits,
                pose.Distance, CameraCollisionMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var hit = collisionHits[i];
                if (!CameraObstacle(hit.collider)) continue;
                pose.Obstacles++;
                // The focus is a virtual framing point, not an occupied camera.
                // Escaping an initial expanded overlap requires independent
                // endpoint AND actual head/hips/feet visibility proof below.
                if (InitiallyOverlapping(hit.collider)) continue;
                pose.Distance = Mathf.Min(pose.Distance, Mathf.Max(0, hit.distance - CollisionPadding));
            }
            if (count >= collisionHits.Length) { pose.Distance = 0; return pose; }
            Vector3 endpoint = origin + pose.Direction * pose.Distance;
            int endpointBlockers = 0;
            bool endpointClear = pose.Distance > .01f && PositionClear(endpoint, out endpointBlockers);
            bool sightClear = BodySightlinesClear(endpoint, out int sightBlockers);
            pose.Obstacles += endpointBlockers + sightBlockers;
            pose.Valid = endpointClear && sightClear;
            return pose;
        }

        bool InitiallyOverlapping(Collider collider)
        {
            for (int i = 0; i < initialOverlapCount; i++) if (initialOverlaps[i] == collider) return true;
            return false;
        }

        bool PositionClear(Vector3 endpoint, out int blockers)
        {
            blockers = 0;
            int count = Physics.OverlapSphereNonAlloc(endpoint, CollisionRadius, endpointOverlaps,
                CameraCollisionMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++) if (CameraObstacle(endpointOverlaps[i])) blockers++;
            if (count >= endpointOverlaps.Length) blockers++;
            return blockers == 0;
        }

        bool BodySightlinesClear(Vector3 endpoint, out int blockers)
        {
            blockers = 0;
            foreach (var point in bodyPoints)
            {
                Vector3 delta = point - endpoint; float length = delta.magnitude;
                if (length <= .03f) { blockers++; continue; }
                int count = Physics.RaycastNonAlloc(endpoint, delta / length, collisionHits, length - .025f,
                    CameraCollisionMask, QueryTriggerInteraction.Collide);
                for (int i = 0; i < count; i++) if (CameraObstacle(collisionHits[i].collider)) blockers++;
                if (count >= collisionHits.Length) blockers++;
            }
            return blockers == 0;
        }

        void PrepareBodyPoints()
        {
            Vector3 foot = Target.position;
            bodyPoints[0] = targetHead != null ? targetHead.position : foot + Vector3.up * (actorHeight * .88f);
            bodyPoints[1] = targetHips != null ? targetHips.position : foot + Vector3.up * (actorHeight * .52f);
            bodyPoints[2] = (targetLeftFoot != null ? targetLeftFoot.position : foot) + Vector3.up * .07f;
            bodyPoints[3] = (targetRightFoot != null ? targetRightFoot.position : foot) + Vector3.up * .07f;
            bodyPoints[4] = Vector3.Lerp(bodyPoints[0], bodyPoints[1], .5f);
        }

        bool Readable(Vector3 position, Quaternion rotation)
        {
            Quaternion inverse = Quaternion.Inverse(rotation);
            float tangent = Mathf.Tan(View.fieldOfView * .5f * Mathf.Deg2Rad);
            float minimumY = 1, maximumY = 0;
            foreach (var point in bodyPoints)
            {
                Vector3 local = inverse * (point - position);
                if (local.z <= View.nearClipPlane + .03f) return false;
                float y = .5f + local.y / (2 * local.z * tangent);
                float x = .5f + local.x / (2 * local.z * tangent * View.aspect);
                if (x < .07f || x > .93f || y < .07f || y > .93f) return false;
                minimumY = Mathf.Min(minimumY, y); maximumY = Mathf.Max(maximumY, y);
            }
            float span = maximumY - minimumY;
            return span >= .10f && span <= .75f;
        }

        void ResetAvoidance()
        {
            effectiveYaw = currentYaw; effectivePitch = currentPitch;
            renderYawVelocity = renderPitchVelocity = clearSeconds = avoidanceYawOffset = 0;
            avoidancePitch = currentPitch; IsAvoidingObstruction = false;
        }

        bool CameraObstacle(Collider obstacle)
        {
            if (obstacle == null || obstacle.transform.IsChildOf(Target)) return false;
            if (obstacle.isTrigger && obstacle.GetComponent<GospelNprCameraScenery>() == null) return false;
            // A person owns its controller on the marker parent; the visual
            // Actor is a child, so GetComponentInParent<Actor> cannot find it.
            if (obstacle is CharacterController && obstacle.GetComponentInParent<GospelPilgrimagePeople>() != null) return false;
            if (obstacle.GetComponentInParent<GospelPilgrimageActor>() != null
                || obstacle.GetComponentInParent<GospelPilgrimageMotor>() != null) return false;
            // The invisible movement fence limits the traveler, not a camera looking into the village.
            return obstacle.gameObject.name != "Diorama exploration boundary";
        }

        void ReleasePointer() { mouseOrbit = false; orbitTouchId = -1; pendingUiLook = Vector2.zero; }
        void OnDisable() { ReleasePointer(); ResetAvoidance(); }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
