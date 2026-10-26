using System;
using UnityEngine;

namespace PathOfDawn.Presentation
{
    public enum GospelActionState { Ready, Preparing, Performing, Recovering, Cooldown }

    /// <summary>
    /// Optional movement practice, outside the Scripture progression. It has no damage,
    /// hit detection, NPC target, reward, or effect on a canonical story event.
    /// </summary>
    public sealed class GospelActionController : MonoBehaviour
    {
        public GospelActionState State { get; private set; }
        public bool IsActive => State == GospelActionState.Preparing || State == GospelActionState.Performing || State == GospelActionState.Recovering;
        public bool IsInTrainingArea => areaConfigured && trainingBounds.Contains(transform.position) &&
            (trainingRadius <= 0 || new Vector2(transform.position.x - trainingBounds.center.x,transform.position.z - trainingBounds.center.z).sqrMagnitude <= trainingRadius * trainingRadius);
        public bool CanPerformAction => motor != null && motor.InputEnabled && motor.IsGrounded && IsInTrainingArea && State == GospelActionState.Ready;
        public float MovementMultiplier => IsActive ? .22f : 1;
        public float CooldownRemaining => Mathf.Max(0,readyAt - Time.time);
        public event Action ActionStarted;
        public event Action ActionCompleted;
        GospelThirdPersonMotor motor;
        Animator animator;
        Bounds trainingBounds;
        bool areaConfigured;
        float startedAt, readyAt;
        float trainingRadius;
        const float Preparation = .18f, Performance = .72f, Recovery = .42f, Cooldown = .4f;
        static readonly int PracticeId = Animator.StringToHash("Practice");

        public void Initialize(GospelThirdPersonMotor owner, Animator rig) { motor = owner; animator = rig; }
        public void ConfigureTrainingArea(Vector3 center, float radius)
        {
            // Generous vertical bounds allow normal ground steps; actions still require grounding.
            ConfigureTrainingArea(new Bounds(center + Vector3.up, new Vector3(Mathf.Max(.1f,radius)*2,4,Mathf.Max(.1f,radius)*2)));
            trainingRadius = Mathf.Max(.1f,radius);
        }
        public void ConfigureTrainingArea(Bounds bounds) { trainingBounds = bounds; trainingRadius = 0; areaConfigured = true; }
        public void ClearTrainingArea() { areaConfigured = false; CancelAction(); }
        public bool TryAction()
        {
            if (!CanPerformAction) return false;
            startedAt = Time.time; readyAt = startedAt + Preparation + Performance + Recovery + Cooldown;
            State = GospelActionState.Preparing;
            if (animator != null) { animator.ResetTrigger(PracticeId); animator.SetTrigger(PracticeId); }
            ActionStarted?.Invoke(); return true;
        }
        void Update()
        {
            if (animator != null && animator.layerCount > 1)
                animator.SetLayerWeight(1,Mathf.MoveTowards(animator.GetLayerWeight(1),IsActive ? 1 : 0,Time.deltaTime * 9));
            if (State == GospelActionState.Ready) return;
            if (IsActive && (motor == null || !motor.InputEnabled || !IsInTrainingArea || !motor.IsGrounded)) { CancelAction(); return; }
            float elapsed = Time.time - startedAt;
            if (Time.time >= readyAt) State = GospelActionState.Ready;
            else if (elapsed >= Preparation + Performance + Recovery)
            {
                bool completed = IsActive; State = GospelActionState.Cooldown;
                if (completed) ActionCompleted?.Invoke();
            }
            else if (elapsed >= Preparation + Performance) State = GospelActionState.Recovering;
            else if (elapsed >= Preparation) State = GospelActionState.Performing;
        }
        public void CancelAction()
        {
            State = GospelActionState.Ready; readyAt = 0;
            if (animator == null) return;
            animator.ResetTrigger(PracticeId);
            // The practice layer is independent from the physical movement layer.
            if (animator.layerCount > 1) { animator.SetLayerWeight(1,0); animator.Play("Practice ready",1,0); }
        }
    }
}
