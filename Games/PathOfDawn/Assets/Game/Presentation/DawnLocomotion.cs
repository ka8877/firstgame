using UnityEngine;

namespace PathOfDawn.Presentation
{
    /// <summary>Drives authored in-place animation from collision-resolved world movement.</summary>
    [DisallowMultipleComponent]
    public sealed class DawnLocomotion : MonoBehaviour
    {
        public const float WalkingSpeed = 1.45f;
        static readonly int SpeedParameter = Animator.StringToHash("Speed");
        Animator animator;
        bool hasSpeed;
        public float NormalizedSpeed { get; private set; }
        public bool HasLocomotionController => animator != null && hasSpeed;
        public Animator CharacterAnimator => animator;

        public void Initialize(Animator characterAnimator)
        {
            animator = characterAnimator;
            if (animator == null) return;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var parameter in animator.parameters)
                if (parameter.nameHash == SpeedParameter && parameter.type == AnimatorControllerParameterType.Float)
                    hasSpeed = true;
            StopImmediately();
        }

        public void ObserveMovement(Vector3 displacement, float deltaTime)
        {
            displacement.y = 0;
            float actualSpeed = deltaTime > .0001f ? displacement.magnitude / deltaTime : 0;
            NormalizedSpeed = Mathf.Clamp01(actualSpeed / WalkingSpeed);
            if (NormalizedSpeed < .015f) NormalizedSpeed = 0;
            if (animator != null && hasSpeed)
                animator.SetFloat(SpeedParameter, NormalizedSpeed, .09f, Mathf.Max(0, deltaTime));
        }

        public void StopImmediately()
        {
            NormalizedSpeed = 0;
            if (animator != null && hasSpeed) animator.SetFloat(SpeedParameter, 0);
        }

        void OnDisable() => StopImmediately();
    }
}
