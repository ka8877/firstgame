using UnityEngine;
using UnityEngine.InputSystem;

namespace PathOfDawn.Presentation
{
    public enum GospelLocomotionState { Idle, Walking, Running, Airborne, Reading }

    /// <summary>Camera-relative physical movement; animation follows measured travel, never moves gameplay state.</summary>
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
        public float MeasuredSpeed { get; private set; }
        public Transform InteractionSocket { get; private set; }
        public Animator Animator { get; private set; }
        Camera view;
        InputActionMap inputs;
        InputAction move, look, orbit, run;
        Vector3 planarVelocity, cameraVelocity;
        float gravityVelocity, yaw, pitch = 24, cameraDistance = 5.8f;
        bool neutralRequired;
        const float WalkSpeed = 1.75f, RunSpeed = 3.7f;

        public void Initialize(Camera camera, Animator animator)
        {
            view = camera; Animator = animator;
            Controller = GetComponent<CharacterController>();
            Controller.height = 1.42f; Controller.center = Vector3.up * .73f;
            Controller.radius = .23f; Controller.stepOffset = .28f; Controller.slopeLimit = 47;
            Controller.skinWidth = .035f; Controller.minMoveDistance = 0;
            if (Animator != null) { Animator.applyRootMotion = false; Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
            InteractionSocket = new GameObject("Interaction socket").transform;
            InteractionSocket.SetParent(transform, false); InteractionSocket.localPosition = new Vector3(0,.9f,.38f);
            inputs = new InputActionMap("Gospel exploration");
            move = inputs.AddAction("Move",InputActionType.Value,expectedControlLayout:"Vector2");
            move.AddCompositeBinding("2DVector").With("Up","<Keyboard>/w").With("Down","<Keyboard>/s").With("Left","<Keyboard>/a").With("Right","<Keyboard>/d");
            move.AddCompositeBinding("2DVector").With("Up","<Keyboard>/upArrow").With("Down","<Keyboard>/downArrow").With("Left","<Keyboard>/leftArrow").With("Right","<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");
            look = inputs.AddAction("Look",InputActionType.Value,"<Pointer>/delta",expectedControlLayout:"Vector2");
            look.AddBinding("<Gamepad>/rightStick",processors:"scaleVector2(x=12,y=12)");
            orbit = inputs.AddAction("Orbit",InputActionType.Button,"<Mouse>/rightButton");
            run = inputs.AddAction("Run",InputActionType.Button,"<Keyboard>/leftShift"); run.AddBinding("<Gamepad>/leftStickPress");
            inputs.Enable(); SnapCamera();
        }
        void Update()
        {
            if (Controller == null || !Controller.enabled) return;
            float dt = Mathf.Min(Time.deltaTime,.05f);
            Vector2 rawDirection = Vector2.ClampMagnitude(move.ReadValue<Vector2>() + TouchMove,1);
            Vector2 direction = InputEnabled ? rawDirection : Vector2.zero;
            if (neutralRequired) { if (rawDirection.sqrMagnitude < .001f) neutralRequired = false; direction = Vector2.zero; }
            if (InputEnabled)
            {
                Vector2 delta = TouchLook;
                if (orbit.IsPressed()) delta += look.ReadValue<Vector2>() * .13f;
                if (Gamepad.current != null) delta += Gamepad.current.rightStick.ReadValue() * (100 * dt);
                yaw += delta.x; pitch = Mathf.Clamp(pitch - delta.y,12,55);
                if (Mouse.current != null) cameraDistance = Mathf.Clamp(cameraDistance - Mouse.current.scroll.ReadValue().y * .002f,2.2f,8);
            }
            TouchLook = Vector2.zero;
            var basis = Quaternion.Euler(0,yaw,0);
            Vector3 wish = basis * new Vector3(direction.x,0,direction.y);
            bool running = InputEnabled && (run.IsPressed() || TouchRun);
            Vector3 desired = wish * (running ? RunSpeed : WalkSpeed);
            float rate = direction.sqrMagnitude > .001f ? 11.5f : 20f;
            planarVelocity = Vector3.MoveTowards(planarVelocity,desired,rate * dt);
            if (!InputEnabled) planarVelocity = Vector3.MoveTowards(planarVelocity,Vector3.zero,30 * dt);
            if (wish.sqrMagnitude > .002f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(wish),620 * dt);
            if (Controller.isGrounded && gravityVelocity < 0) gravityVelocity = -2;
            gravityVelocity = Mathf.Max(-25,gravityVelocity - 21 * dt);
            var before = transform.position;
            Controller.Move((planarVelocity + Vector3.up * gravityVelocity) * dt);
            var travel = transform.position - before; travel.y = 0;
            MeasuredSpeed = dt > 0 ? travel.magnitude / dt : 0;
            State = !InputEnabled ? GospelLocomotionState.Reading : !Controller.isGrounded ? GospelLocomotionState.Airborne : MeasuredSpeed < .08f ? GospelLocomotionState.Idle : MeasuredSpeed > 2.3f ? GospelLocomotionState.Running : GospelLocomotionState.Walking;
            if (Animator != null) Animator.SetFloat("Speed",MeasuredSpeed,.12f,dt);
            if (transform.position.y < -3) SetPose(new Vector3(0,.1f,-9),0);
        }
        void LateUpdate()
        {
            if (view == null || !CameraFollow) return;
            Vector3 target = transform.position + Vector3.up * 1.12f;
            Quaternion rotation = Quaternion.Euler(pitch,yaw,0);
            Vector3 direction = rotation * Vector3.back;
            float distance = cameraDistance;
            if (Physics.SphereCast(target,.22f,direction,out var hit,cameraDistance,~(1 << 8),QueryTriggerInteraction.Ignore)) distance = Mathf.Max(1.2f,hit.distance-.18f);
            Vector3 desired = target + direction * distance;
            view.transform.position = Vector3.SmoothDamp(view.transform.position,desired,ref cameraVelocity,.13f,Mathf.Infinity,Time.deltaTime);
            view.transform.rotation = Quaternion.Slerp(view.transform.rotation,Quaternion.LookRotation(target - view.transform.position),1-Mathf.Exp(-16*Time.deltaTime));
        }
        public void SetPose(Vector3 position, float facing)
        {
            if (Controller == null) Controller = GetComponent<CharacterController>();
            Controller.enabled = false; transform.SetPositionAndRotation(position,Quaternion.Euler(0,facing,0)); Controller.enabled = true;
            planarVelocity = Vector3.zero; gravityVelocity = -2; MeasuredSpeed = 0; yaw = facing; SnapCamera();
        }
        public void SnapCamera()
        {
            if (view == null) return;
            Vector3 target = transform.position + Vector3.up * 1.12f;
            view.transform.position = target + Quaternion.Euler(pitch,yaw,0) * Vector3.back * cameraDistance;
            view.transform.LookAt(target); cameraVelocity = Vector3.zero;
        }
        public void StopInput()
        {
            TouchMove = TouchLook = Vector2.zero; TouchRun = false; planarVelocity = Vector3.zero; neutralRequired = true;
            if (Animator != null) Animator.SetFloat("Speed",0);
        }
        void OnApplicationFocus(bool focused) { if (!focused) StopInput(); }
        void OnDisable() { inputs?.Disable(); StopInput(); }
        void OnEnable() => inputs?.Enable();
        void OnDestroy() => inputs?.Dispose();
    }
}
