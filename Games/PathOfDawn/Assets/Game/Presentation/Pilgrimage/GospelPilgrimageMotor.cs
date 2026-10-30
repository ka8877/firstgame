using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PathOfDawn.Presentation
{
    /// <summary>Collision and time-based free XZ movement. Sprite animation never moves this root.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class GospelPilgrimageMotor : MonoBehaviour
    {
        public Vector3 Position => transform.position;
        public Vector3 Velocity { get; private set; }
        public float FacingYaw { get; private set; } = 180;
        public float MeasuredSpeed { get; private set; }
        public bool IsGrounded { get; private set; }
        public CharacterController Controller { get; private set; }
        public Rect Bounds { get; private set; } = new Rect(-34f, -15f, 74f, 35f);
        public event Action InteractRequested;
        public float WalkSpeed = 3.25f, RunSpeed = 5.4f;
        bool acceptsInput = true, touchRun, neutralRequired;
        Vector2 touchInput;
        Vector3 planar, smoothVelocity;
        float vertical, jumpUntil = -1, lastGround = -100, jumpStart = -100;
        int jumpFrame = -100;
        Camera view;
        Vector3 safePosition;
        float lastFacing = 180;
        public bool Enabled
        {
            get => acceptsInput;
            set
            {
                if (acceptsInput == value) return;
                acceptsInput = value;
                if (!value) { touchInput = Vector2.zero; touchRun = false; planar = smoothVelocity = Vector3.zero; jumpUntil = -1; jumpFrame = -100; }
                else neutralRequired = true;
            }
        }
        public void Initialize(Camera camera)
        {
            view = camera; EnsureController(); safePosition = transform.position;
        }
        void Awake() => EnsureController();
        void EnsureController()
        {
            if (Controller != null) return;
            Controller = GetComponent<CharacterController>();
            Controller.height = 1.55f; Controller.radius = .27f; Controller.center = new Vector3(0, .80f, 0);
            Controller.stepOffset = .26f; Controller.slopeLimit = 48; Controller.skinWidth = .025f; Controller.minMoveDistance = 0;
        }
        public void SetBounds(Rect value)
        {
            if (Finite(value.x) && Finite(value.y) && Finite(value.width) && Finite(value.height) && value.width > 1 && value.height > 1) Bounds = value;
        }
        public void SetPose(Vector3 position, float yaw = 180)
        {
            if (!Finite(position.x) || !Finite(position.y) || !Finite(position.z) || !Finite(yaw)) throw new ArgumentOutOfRangeException(nameof(position));
            EnsureController(); bool was = Controller.enabled; Controller.enabled = false;
            transform.position = new Vector3(Mathf.Clamp(position.x, Bounds.xMin + .32f, Bounds.xMax - .32f), position.y + .035f, Mathf.Clamp(position.z, Bounds.yMin + .32f, Bounds.yMax - .32f));
            transform.rotation = Quaternion.identity;
            Controller.enabled = was; Physics.SyncTransforms(); safePosition = transform.position;
            vertical = -1f; planar = smoothVelocity = Velocity = Vector3.zero; touchInput = Vector2.zero; touchRun = false;
            FacingYaw = lastFacing = Mathf.Repeat(yaw, 360); jumpUntil = -1; jumpFrame = -100; lastGround = -100; IsGrounded = false;
        }
        public void SetTouchInput(Vector2 input) => touchInput = Finite(input.x) && Finite(input.y) ? Vector2.ClampMagnitude(input, 1) : Vector2.zero;
        public void SetRun(bool held) => touchRun = held;
        public void RequestJump() { if (acceptsInput && !neutralRequired) { jumpUntil = Time.time + .15f; jumpFrame = Time.frameCount; } }
        public void RequestInteract() { if (acceptsInput && !neutralRequired) InteractRequested?.Invoke(); }

        void Update()
        {
            EnsureController(); if (!Controller.enabled) return;
            float dt = Mathf.Min(Time.deltaTime, Time.maximumDeltaTime); if (dt <= 0) return;
            var keyboard = Keyboard.current;
            Vector2 keys = Vector2.zero;
            if (keyboard != null)
            {
                keys.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                keys.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
            }
            Vector2 input = Vector2.ClampMagnitude(keys + touchInput, 1);
            if (Gamepad.current != null) input = Vector2.ClampMagnitude(input + Gamepad.current.leftStick.ReadValue(), 1);
            if (neutralRequired) { if (input.sqrMagnitude < .001f) neutralRequired = false; input = Vector2.zero; }
            if (!acceptsInput) input = Vector2.zero;
            if (acceptsInput && !neutralRequired)
            {
                if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame || Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame) RequestJump();
                if (keyboard != null && keyboard.eKey.wasPressedThisFrame || Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame) RequestInteract();
            }
            bool run = acceptsInput && (touchRun || keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed) || Gamepad.current != null && Gamepad.current.leftStickButton.isPressed);
            // Fixed isometric camera: screen-right is +X, screen-up is +Z.
            // A future camera yaw still uses flattened camera bases coherently.
            Vector3 right = Vector3.right, forward = Vector3.forward;
            if (view != null)
            {
                right = view.transform.right; right.y = 0; right.Normalize();
                forward = view.transform.forward; forward.y = 0; forward.Normalize();
            }
            Vector3 wish = right * input.x + forward * input.y;
            Vector3 desired = wish * (run ? RunSpeed : WalkSpeed);
            if (wish.sqrMagnitude > .002f) lastFacing = Mathf.Atan2(wish.x, wish.z) * Mathf.Rad2Deg;
            FacingYaw = Mathf.Repeat(lastFacing, 360);
            Vector3 before = transform.position;
            int count = Mathf.Max(1, Mathf.CeilToInt(dt / .04f)); float step = dt / count;
            for (int i = 0; i < count; i++) Step(desired, input.sqrMagnitude > .001f, step, Time.time - dt + (i + 1) * step);
            Velocity = (transform.position - before) / dt;
            MeasuredSpeed = new Vector2(Velocity.x, Velocity.z).magnitude;
            if (IsGrounded && transform.position.y > -1.5f) safePosition = transform.position;
            if (transform.position.y < -5) SetPose(safePosition, FacingYaw);
        }
        void Step(Vector3 desired, bool moving, float dt, float now)
        {
            bool grounded = Controller.isGrounded && vertical <= 0;
            if (grounded) { lastGround = now; vertical = -2f; }
            bool buffered = jumpUntil >= Time.time || jumpFrame >= Time.frameCount - 1;
            if (acceptsInput && buffered && (grounded || now - lastGround <= .12f))
            {
                vertical = Mathf.Sqrt(2 * 23f * 1.05f); lastGround = -100; jumpStart = now; jumpUntil = -1; jumpFrame = -100; grounded = false;
            }
            planar = Vector3.SmoothDamp(planar, desired, ref smoothVelocity, acceptsInput ? moving ? .105f : .075f : .045f, 65, dt);
            if (desired.sqrMagnitude < .001f && planar.sqrMagnitude < .001f) planar = smoothVelocity = Vector3.zero;
            vertical = Mathf.Max(-24, vertical - 23f * dt);
            Controller.stepOffset = grounded && vertical <= 0 ? .26f : 0;
            Vector3 delta = (planar + Vector3.up * vertical) * dt;
            Vector3 p = transform.position;
            delta.x = Mathf.Clamp(p.x + delta.x, Bounds.xMin + .29f, Bounds.xMax - .29f) - p.x;
            delta.z = Mathf.Clamp(p.z + delta.z, Bounds.yMin + .29f, Bounds.yMax - .29f) - p.z;
            CollisionFlags collisions = Controller.Move(delta);
            if ((collisions & CollisionFlags.Above) != 0 && vertical > 0) vertical = 0;
            IsGrounded = (collisions & CollisionFlags.Below) != 0 && vertical <= 0 && now - jumpStart > .07f;
            if (IsGrounded) { lastGround = now; vertical = -2; }
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        void OnDisable() { touchInput = Vector2.zero; planar = smoothVelocity = Velocity = Vector3.zero; touchRun = false; }
    }
}
