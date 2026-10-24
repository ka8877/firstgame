using UnityEngine;

namespace PathOfDawn.Presentation
{
    /// <summary>Screen-relative intent, independent of the actor's cosmetic facing.</summary>
    public sealed class DawnMovement
    {
        public const float Speed = 2.15f;
        public const float DepthScale = 1.65f;
        public Vector2 Velocity { get; private set; }
        Vector2 lastTarget;
        float acceleration = 24;

        public Vector3 Step(Vector2 input, float deltaTime)
        {
            if (deltaTime <= 0) return Vector3.zero;
            Vector2 target = Vector2.ClampMagnitude(input, 1) * Speed;
            Vector2 difference = target - Velocity;
            float distance = difference.magnitude;
            // Short ease-in, decisive braking and immediate response to a reverse.
            if (target != lastTarget)
            {
                // Keep this ramp's rate until the next intent change. Switching
                // rates at the first frame after velocity crosses zero otherwise
                // makes a reversal depend on the rendering frame rate.
                acceleration = target.sqrMagnitude < .0001f ? 34 : Vector2.Dot(Velocity, target) < 0 ? 38 : 24;
                lastTarget = target;
            }
            float acceleratingTime = Mathf.Min(deltaTime, distance / acceleration);
            Vector2 next = Vector2.MoveTowards(Velocity, target, acceleration * deltaTime);
            // Integrate the ramp and its constant-speed remainder exactly, so low
            // frame rates do not change travel distance or leave extra stopping drift.
            Vector2 travel = (Velocity + next) * (.5f * acceleratingTime) + next * (deltaTime - acceleratingTime);
            Velocity = next;
            return new Vector3(travel.x, 0, travel.y * DepthScale);
        }

        public void Reset() { Velocity = Vector2.zero; lastTarget = Vector2.zero; acceleration = 24; }

        public static Vector2 StickValue(Vector2 normalized)
        {
            float length = normalized.magnitude;
            if (length <= .12f) return Vector2.zero;
            // Full speed slightly before the rim; fine control remains proportional.
            return normalized / length * Mathf.Clamp01((length - .12f) / .80f);
        }
    }
}
