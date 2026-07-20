using UnityEngine;

namespace ToyChest.Gameplay.Player
{
    /// <summary>
    /// The thin bridge that lives on the animated model (the <see cref="Animator"/>'s GameObject) and
    /// forwards two things up to the gameplay adapters on the parent: the attack animation's root motion
    /// (so the punch's step is real movement, not a snap-back) and its contact-frame animation event (so
    /// the blow lands exactly when the fist connects). It owns no gameplay. Root motion is applied only
    /// while attacking, so ordinary locomotion stays velocity-driven and the two never fight; when not
    /// attacking the animator's root motion is simply discarded here.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerModelEvents : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The model's animator. Defaults to this GameObject's Animator.")]
        private Animator _animator;

        [SerializeField]
        [Tooltip("Locomotion adapter that receives attack root motion. Defaults to the one on a parent.")]
        private PlayerLocomotion _locomotion;

        [SerializeField]
        [Tooltip("Combat adapter that gates root motion and receives the contact event. Defaults to the one on a parent.")]
        private PlayerCombat _combat;

        private void Awake()
        {
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }

            if (_locomotion == null)
            {
                _locomotion = GetComponentInParent<PlayerLocomotion>();
            }

            if (_combat == null)
            {
                _combat = GetComponentInParent<PlayerCombat>();
            }
        }

        // Unity routes all root motion here because this component exists on the animator's GameObject.
        // We forward it to the controller only during an attack; otherwise it is discarded so locomotion
        // stays purely velocity-driven.
        private void OnAnimatorMove()
        {
            if (_animator == null || _locomotion == null || _combat == null)
            {
                return;
            }

            if (_combat.IsAttacking)
            {
                _locomotion.ApplyRootMotion(_animator.deltaPosition);
            }
        }

        /// <summary>
        /// Animation event hook. Author an event named <c>OnAttackContact</c> on the attack clip at the
        /// exact frame the fist connects; it lands the blow in sync with the animation. Optional — the
        /// wind-up timer lands the blow if the event is absent.
        /// </summary>
        public void OnAttackContact()
        {
            if (_combat != null)
            {
                _combat.NotifyAnimationContact();
            }
        }
    }
}
