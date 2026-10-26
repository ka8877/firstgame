using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PathOfDawn.Presentation
{
    public enum GospelLocomotionState { Idle, Walking, Running, Airborne, Reading }

    /// <summary>Camera-relative capsule movement. Authored joint animation follows physical travel.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class GospelThirdPersonMotor : MonoBehaviour
    {
        public bool InputEnabled { get; set; }
        public bool CameraFollow { get; set; } = true;
        public Vector2 TouchMove { get; set; }
        public Vector2 TouchLook { get; set; }
        public bool TouchRun { get; set; }
        public GospelLocomotionState State { get; private set; }
        public CharacterController Controller { get; private set; }
        public Vector3 PlanarVelocity => planarVelocity;
        public float CameraYaw => yaw;
        public const float MinimumCameraDistance = 1.8f, MaximumCameraDistance = 8f;
        public float CameraDistance => cameraDistance;
        public float CameraZoom01 => Mathf.InverseLerp(MinimumCameraDistance, MaximumCameraDistance, cameraDistance);
        public float MeasuredSpeed { get; private set; }
        public bool IsGrounded { get; private set; }
        public bool CanJump => CanJumpAt(Time.time);
        public bool CanPerformAction => Actions != null && Actions.CanPerformAction;
        public Transform InteractionSocket { get; private set; }
        public Animator Animator { get; private set; }
        public GospelActionController Actions { get; private set; }
        Camera view;
        InputActionMap inputs;
        InputAction move, look, orbit, run, jump, action;
        Vector3 planarVelocity, accelerationVelocity, cameraVelocity;
        float gravityVelocity, yaw, pitch = 22, cameraDistance = 4.6f, smoothedCameraDistance = 4.6f, zoomVelocity, facingVelocity;
        float lastGroundedTime = -100, jumpBufferedUntil = -1, jumpStartedTime = -100, cameraTargetHeight;
        int jumpRequestedFrame = -100;
        bool neutralRequired, hadGroundContact;
        const float WalkSpeed = 1.75f, RunSpeed = 3.7f, Gravity = 21f, JumpHeight = .96f;
        const float CoyoteTime = .12f, JumpBufferTime = .14f;
        static readonly int SpeedId = Animator.StringToHash("Speed"), GroundedId = Animator.StringToHash("Grounded"), VerticalSpeedId = Animator.StringToHash("VerticalSpeed");
        static readonly int JumpId = Animator.StringToHash("Jump"), LandId = Animator.StringToHash("Land");

        public void Initialize(Camera camera, Animator animator)
        {
            view = camera; Animator = animator;
            SetCameraDistance(PlayerPrefs.GetFloat("GOSPEL.cameraDistance", 4.6f));
            smoothedCameraDistance = cameraDistance;
            Controller = GetComponent<CharacterController>();
            Controller.height = 1.42f; Controller.center = Vector3.up * .73f;
            Controller.radius = .23f; Controller.stepOffset = .28f; Controller.slopeLimit = 47;
            Controller.skinWidth = .025f; Controller.minMoveDistance = 0;
            if (Animator != null) { Animator.applyRootMotion = false; Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
            InteractionSocket = new GameObject("Interaction socket").transform;
            InteractionSocket.SetParent(transform, false); InteractionSocket.localPosition = new Vector3(0,.9f,.38f);
            Actions = GetComponent<GospelActionController>();
            if (Actions == null) Actions = gameObject.AddComponent<GospelActionController>();
            Actions.Initialize(this, Animator);
            inputs = new InputActionMap("Gospel exploration");
            move = inputs.AddAction("Move",InputActionType.Value,expectedControlLayout:"Vector2");
            move.AddCompositeBinding("2DVector").With("Up","<Keyboard>/w").With("Down","<Keyboard>/s").With("Left","<Keyboard>/a").With("Right","<Keyboard>/d");
            move.AddCompositeBinding("2DVector").With("Up","<Keyboard>/upArrow").With("Down","<Keyboard>/downArrow").With("Left","<Keyboard>/leftArrow").With("Right","<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");
            look = inputs.AddAction("Look",InputActionType.Value,"<Pointer>/delta",expectedControlLayout:"Vector2");
            orbit = inputs.AddAction("Orbit",InputActionType.Button,"<Mouse>/rightButton");
            run = inputs.AddAction("Run",InputActionType.Button,"<Keyboard>/leftShift"); run.AddBinding("<Keyboard>/rightShift"); run.AddBinding("<Gamepad>/leftStickPress");
            jump = inputs.AddAction("Jump",InputActionType.Button,"<Keyboard>/space"); jump.AddBinding("<Gamepad>/buttonSouth");
            action = inputs.AddAction("Practice",InputActionType.Button,"<Keyboard>/f"); action.AddBinding("<Gamepad>/buttonWest");
            inputs.Enable(); SnapCamera();
        }

        void Update()
        {
            if (Controller == null || !Controller.enabled || inputs == null) return;
            // Consume the complete Unity frame interval. A slow render must not
            // discard movement time; only individual collision steps are bounded.
            float dt = Mathf.Min(Time.deltaTime,Time.maximumDeltaTime);
            if (dt <= 0) return;
            Vector2 rawDirection = Vector2.ClampMagnitude(move.ReadValue<Vector2>() + TouchMove,1);
            Vector2 direction = InputEnabled ? rawDirection : Vector2.zero;
            if (neutralRequired) { if (rawDirection.sqrMagnitude < .001f) neutralRequired = false; direction = Vector2.zero; }
            if (InputEnabled)
            {
                Vector2 delta = TouchLook;
                bool pointerBlocked = Mouse.current != null && (orbit.IsPressed() || Mouse.current.scroll.ReadValue().sqrMagnitude > 0) && PointerOverCameraBlockingUi();
                if (orbit.IsPressed() && !pointerBlocked) delta += look.ReadValue<Vector2>() * .13f;
                if (Gamepad.current != null) delta += Gamepad.current.rightStick.ReadValue() * (100 * dt);
                yaw += delta.x; pitch = Mathf.Clamp(pitch - delta.y,12,55);
                if (Mouse.current != null && !pointerBlocked)
                    // Input System 1.20 uses normalized +/-1 ticks on every
                    // platform; the legacy pixel/120-scale multiplier is tiny.
                    SetCameraDistance(cameraDistance - Mouse.current.scroll.ReadValue().y * .65f);
                if (jump.WasPressedThisFrame()) JumpPressed();
                bool pointerAction = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
                    (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()) &&
                    (Touchscreen.current == null || !Touchscreen.current.primaryTouch.press.isPressed);
                if (action.WasPressedThisFrame() || pointerAction) TryAction();
            }
            TouchLook = Vector2.zero;
            Vector3 wish = Quaternion.Euler(0,yaw,0) * new Vector3(direction.x,0,direction.y);
            bool running = InputEnabled && (run.IsPressed() || TouchRun);
            float actionScale = Actions != null ? Actions.MovementMultiplier : 1;
            Vector3 desired = wish * (running ? RunSpeed : WalkSpeed) * actionScale;
            if (wish.sqrMagnitude > .002f)
            {
                float targetAngle = Mathf.Atan2(wish.x,wish.z) * Mathf.Rad2Deg;
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y,targetAngle,ref facingVelocity,.09f,900,dt);
                transform.rotation = Quaternion.Euler(0,angle,0);
            }
            int substeps = Mathf.Max(1,Mathf.CeilToInt(dt / .05f));
            float stepDelta = dt / substeps, simulationTime = Time.time - dt, planarTravel = 0;
            for (int step = 0; step < substeps; step++)
            {
                simulationTime += stepDelta;
                planarTravel += StepMovement(desired,direction.sqrMagnitude > .001f,stepDelta,simulationTime);
            }
            MeasuredSpeed = planarTravel / dt;
            State = !InputEnabled ? GospelLocomotionState.Reading : !IsGrounded ? GospelLocomotionState.Airborne : MeasuredSpeed < .08f ? GospelLocomotionState.Idle : MeasuredSpeed > 2.3f ? GospelLocomotionState.Running : GospelLocomotionState.Walking;
            if (Animator != null)
            {
                Animator.SetFloat(SpeedId,MeasuredSpeed,.09f,dt);
                Animator.SetFloat(VerticalSpeedId,gravityVelocity,.05f,dt);
                Animator.SetBool(GroundedId,IsGrounded);
            }
            if (transform.position.y < -3) SetPose(new Vector3(0,.1f,-9),0);
        }

        // UI can query before this frame's motor update. Existing capsule contact
        // remains valid even when the preceding timestamp is over .12 seconds old.
        bool CanJumpAt(float now) => InputEnabled && !neutralRequired && gravityVelocity <= 0 &&
            (Controller != null && Controller.enabled && Controller.isGrounded || now - lastGroundedTime <= CoyoteTime) &&
            (Actions == null || !Actions.IsActive);

        float StepMovement(Vector3 desired, bool hasDirection, float dt, float now)
        {
            bool groundedBeforeMove = gravityVelocity <= 0 && Controller.isGrounded;
            if (groundedBeforeMove) { lastGroundedTime = now; gravityVelocity = -2; }
            bool bufferedJump = jumpBufferedUntil >= Time.time || jumpRequestedFrame >= Time.frameCount - 1;
            if (bufferedJump && CanJumpAt(now))
            {
                gravityVelocity = Mathf.Sqrt(2 * Gravity * JumpHeight);
                jumpBufferedUntil = -1; jumpRequestedFrame = -100; lastGroundedTime = -100; jumpStartedTime = now;
                IsGrounded = false;
                if (Animator != null) { Animator.ResetTrigger(LandId); Animator.SetTrigger(JumpId); }
            }
            // The same smoothing and gravity are integrated over every collision
            // substep, so 12 rendered frames consume the same game time as 30.
            float response = hasDirection ? (IsGrounded ? .14f : .23f) : .105f;
            if (!InputEnabled) response = .065f;
            planarVelocity = Vector3.SmoothDamp(planarVelocity,desired,ref accelerationVelocity,response,40,dt);
            if (desired.sqrMagnitude < .001f && planarVelocity.sqrMagnitude < .0009f) { planarVelocity = Vector3.zero; accelerationVelocity = Vector3.zero; }
            gravityVelocity = Mathf.Max(-25,gravityVelocity - Gravity * dt);
            Controller.stepOffset = gravityVelocity > 0 || !groundedBeforeMove ? 0 : .28f;
            Vector3 before = transform.position;
            CollisionFlags collisions = Controller.Move((planarVelocity + Vector3.up * gravityVelocity) * dt);
            if ((collisions & CollisionFlags.Above) != 0 && gravityVelocity > 0) gravityVelocity = 0;
            bool groundedNow = (collisions & CollisionFlags.Below) != 0 && gravityVelocity <= 0 && now - jumpStartedTime > .08f;
            if (groundedNow) { lastGroundedTime = now; gravityVelocity = -2; }
            // Brief paving contact gaps do not alternate the air and walk poses.
            IsGrounded = groundedNow || gravityVelocity <= 0 && now - lastGroundedTime < .055f;
            if (groundedNow && !hadGroundContact && now - jumpStartedTime > .16f && Animator != null) Animator.SetTrigger(LandId);
            hadGroundContact = IsGrounded;
            Vector3 travel = transform.position - before; travel.y = 0;
            return travel.magnitude;
        }

        public void JumpPressed()
        {
            if (InputEnabled && !neutralRequired && (Actions == null || !Actions.IsActive))
            {
                jumpBufferedUntil = Time.time + JumpBufferTime;
                // A UI click may arrive after Update. Even a slow next frame
                // gets one opportunity to consume it; subsequent frames expire.
                jumpRequestedFrame = Time.frameCount;
            }
        }
        public bool TryAction() => Actions != null && Actions.TryAction();
        public void ToggleTouchRun() => TouchRun = !TouchRun;
        public void SetCameraDistance(float meters)
        {
            if (float.IsNaN(meters) || float.IsInfinity(meters)) return;
            cameraDistance = Mathf.Clamp(meters, MinimumCameraDistance, MaximumCameraDistance);
            PlayerPrefs.SetFloat("GOSPEL.cameraDistance", cameraDistance);
        }
        public void SetCameraZoom01(float value) => SetCameraDistance(Mathf.Lerp(MinimumCameraDistance, MaximumCameraDistance, Mathf.Clamp01(value)));
        public void ZoomBy(float meters) => SetCameraDistance(cameraDistance + meters);
        bool PointerOverCameraBlockingUi()
        {
            if (EventSystem.current == null || Mouse.current == null) return false;
            var data = new PointerEventData(EventSystem.current) { position = Mouse.current.position.ReadValue() };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            foreach (var hit in hits)
            {
                // The mobile camera drag surface is world input, whereas its
                // neighbouring joystick, menus and scroll views own the wheel.
                var pad = hit.gameObject.GetComponentInParent<DawnPointerPad>();
                if (pad != null && !pad.joystick) continue;
                return true;
            }
            return false;
        }

        void LateUpdate()
        {
            if (view == null || !CameraFollow) return;
            float dt = Mathf.Min(Time.deltaTime,Time.maximumDeltaTime);
            cameraTargetHeight = Mathf.Lerp(cameraTargetHeight,transform.position.y + 1.12f,1 - Mathf.Exp(-(IsGrounded ? 14 : 7) * dt));
            Vector3 target = new Vector3(transform.position.x,cameraTargetHeight,transform.position.z);
            Vector3 direction = Quaternion.Euler(pitch,yaw,0) * Vector3.back;
            smoothedCameraDistance = Mathf.SmoothDamp(smoothedCameraDistance,cameraDistance,ref zoomVelocity,.14f,Mathf.Infinity,dt);
            float distance = smoothedCameraDistance;
            if (Physics.SphereCast(target,.22f,direction,out var hit,distance,~(1 << 8),QueryTriggerInteraction.Ignore)) distance = Mathf.Max(1.2f,hit.distance-.18f);
            Vector3 desired = target + direction * distance;
            view.transform.position = Vector3.SmoothDamp(view.transform.position,desired,ref cameraVelocity,.11f,Mathf.Infinity,dt);
            // Resolve a newly encountered wall immediately, then ease back to the normal distance.
            Vector3 cameraRay = view.transform.position - target;
            if (Physics.SphereCast(target,.18f,cameraRay.normalized,out var obstruction,cameraRay.magnitude,~(1 << 8),QueryTriggerInteraction.Ignore))
                view.transform.position = target + cameraRay.normalized * Mathf.Max(.7f,obstruction.distance-.1f);
            view.transform.rotation = Quaternion.Slerp(view.transform.rotation,Quaternion.LookRotation(target - view.transform.position),1-Mathf.Exp(-18*dt));
        }

        public void SetPose(Vector3 position, float facing)
        {
            if (Controller == null) Controller = GetComponent<CharacterController>();
            Controller.enabled = false; transform.SetPositionAndRotation(position,Quaternion.Euler(0,facing,0)); Controller.enabled = true;
            planarVelocity = accelerationVelocity = Vector3.zero; gravityVelocity = -2; MeasuredSpeed = 0; yaw = facing; facingVelocity = 0;
            jumpBufferedUntil = -1; jumpRequestedFrame = -100; lastGroundedTime = -100; jumpStartedTime = -100; IsGrounded = false; hadGroundContact = false;
            Actions?.CancelAction();
            if (Animator != null) { Animator.ResetTrigger(JumpId); Animator.ResetTrigger(LandId); Animator.SetFloat(SpeedId,0); Animator.SetBool(GroundedId,true); Animator.Play("Locomotion",0,0); }
            SnapCamera();
        }
        public void SnapCamera()
        {
            if (view == null) return;
            Vector3 target = transform.position + Vector3.up * 1.12f; cameraTargetHeight = target.y;
            smoothedCameraDistance = cameraDistance; zoomVelocity = 0;
            view.transform.position = target + Quaternion.Euler(pitch,yaw,0) * Vector3.back * cameraDistance;
            view.transform.LookAt(target); cameraVelocity = Vector3.zero;
        }
        public void StopInput()
        {
            TouchMove = TouchLook = Vector2.zero; TouchRun = false; planarVelocity = accelerationVelocity = Vector3.zero; neutralRequired = true; jumpBufferedUntil = -1; jumpRequestedFrame = -100;
            Actions?.CancelAction();
            if (Animator != null) Animator.SetFloat(SpeedId,0);
        }
        void OnApplicationFocus(bool focused) { if (!focused) StopInput(); }
        void OnDisable() { inputs?.Disable(); StopInput(); }
        void OnEnable() => inputs?.Enable();
        void OnDestroy() => inputs?.Dispose();
    }
}
