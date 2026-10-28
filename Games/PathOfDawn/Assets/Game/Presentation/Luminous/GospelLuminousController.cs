using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PathOfDawn.Presentation
{
    /// <summary>Physical side-view movement. Rendering never owns the player's position.</summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public sealed class GospelLuminousController : MonoBehaviour
    {
        public GospelLuminousActor Actor { get; set; }
        public Vector2 Position => body ? body.position : (Vector2)transform.position;
        public Vector2 Velocity => body ? body.linearVelocity : Vector2.zero;
        public int Facing { get; private set; } = 1;
        public bool Grounded { get; private set; }
        public bool Enabled
        {
            get => acceptsInput;
            set
            {
                if (acceptsInput == value) return;
                acceptsInput = value;
                if (!value)
                {
                    touchInput = desiredInput = 0; touchRun = false; jumpBuffer = 0;
                    if (body) body.linearVelocity = Vector2.zero;
                }
            }
        }
        public string CurrentState => Actor ? Actor.CurrentState : "Idle";
        public float WalkSpeed = 3.1f;
        public float RunSpeed = 5.6f;
        public float JumpSpeed = 8.2f;
        public float MinX = -33f;
        public float MaxX = 33f;
        public event Action InteractRequested;

        Rigidbody2D body;
        CapsuleCollider2D capsule;
        readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];
        bool acceptsInput = true, touchRun, jumpHeld;
        float touchInput, desiredInput, jumpBuffer, coyote;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            capsule = GetComponent<CapsuleCollider2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 2.85f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            capsule.direction = CapsuleDirection2D.Vertical;
            capsule.size = new Vector2(.4f, 1.65f);
            capsule.offset = new Vector2(0, .825f);
            var frictionless = new PhysicsMaterial2D("LuminousCharacterFriction") { friction = 0, bounciness = 0 };
            capsule.sharedMaterial = frictionless;
        }

        public void SetTouchInput(float horizontal) => touchInput = Mathf.Clamp(horizontal, -1, 1);
        public void SetRun(bool held) => touchRun = held;
        public void RequestJump() { if (acceptsInput) jumpBuffer = .14f; }
        public void RequestInteract() { if (acceptsInput) InteractRequested?.Invoke(); }

        /// <summary>Used only by explicit new-journey and legitimate save/Continue transitions.</summary>
        public void SetPose(Vector2 feet, int facing = 1)
        {
            if (!body) Awake();
            body.position = feet;
            transform.position = new Vector3(feet.x, feet.y, 0);
            body.linearVelocity = Vector2.zero;
            touchInput = desiredInput = jumpBuffer = coyote = 0;
            touchRun = jumpHeld = false;
            Facing = facing < 0 ? -1 : 1;
            Grounded = false;
            Physics2D.SyncTransforms();
            if (Actor) Actor.SetMotion(Vector2.zero, true, Facing);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            float keys = 0;
            if (acceptsInput && keyboard != null)
            {
                keys = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0)
                    - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                if (keyboard.spaceKey.wasPressedThisFrame) RequestJump();
                if (keyboard.eKey.wasPressedThisFrame) RequestInteract();
                bool held = keyboard.spaceKey.isPressed;
                if (jumpHeld && !held && body.linearVelocity.y > 1)
                    body.linearVelocity = new Vector2(body.linearVelocity.x, body.linearVelocity.y * .62f);
                jumpHeld = held;
            }
            desiredInput = acceptsInput ? Mathf.Clamp(keys + touchInput, -1, 1) : 0;
            if (Mathf.Abs(desiredInput) > .08f) Facing = desiredInput < 0 ? -1 : 1;
            if (Actor) Actor.SetMotion(Velocity, Grounded, Facing);
        }

        void FixedUpdate()
        {
            var filter = new ContactFilter2D { useTriggers = false };
            int count = capsule.Cast(Vector2.down, filter, groundHits, .07f);
            Grounded = false;
            for (int i = 0; i < count; i++)
                if (groundHits[i].normal.y > .55f && groundHits[i].collider.attachedRigidbody != body)
                { Grounded = true; break; }
            coyote = Grounded ? .12f : Mathf.Max(0, coyote - Time.fixedDeltaTime);
            jumpBuffer = Mathf.Max(0, jumpBuffer - Time.fixedDeltaTime);
            bool run = touchRun || (Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed));
            float target = desiredInput * (run ? RunSpeed : WalkSpeed);
            var velocity = body.linearVelocity;
            float acceleration = Grounded ? (Mathf.Abs(desiredInput) > .01f ? 25 : 36) : 14;
            velocity.x = Mathf.MoveTowards(velocity.x, target, acceleration * Time.fixedDeltaTime);
            if (!acceptsInput) velocity.x = 0;
            if (acceptsInput && jumpBuffer > 0 && coyote > 0)
            { velocity.y = JumpSpeed; jumpBuffer = coyote = 0; Grounded = false; }
            if (velocity.y < -15) velocity.y = -15;
            if (body.position.x <= MinX && velocity.x < 0 || body.position.x >= MaxX && velocity.x > 0)
                velocity.x = 0;
            body.linearVelocity = velocity;
        }

        void OnDisable()
        {
            touchInput = desiredInput = jumpBuffer = 0;
            if (body) body.linearVelocity = Vector2.zero;
        }
    }
}
