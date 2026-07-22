using UnityEngine;

namespace ToyChest.Gameplay.HitDetection
{
    /// <summary>
    /// The character-agnostic receiver for the canonical <c>OnAttackContact</c> animation event. It lives
    /// on the animated model (the <see cref="Animator"/>'s GameObject, where Unity dispatches animation
    /// events) and forwards the contact to whichever <see cref="IAttackContactReceiver"/> owns the
    /// character — <c>PlayerCombat</c>, <c>EnemyCombatant</c>, or any future companion or boss.
    ///
    /// This is what makes "author an attack once and any character can use it" true at the animation
    /// layer: a designer places one event on the clip, and every rig that plays that clip resolves its own
    /// authored hit volume at the right frame. Put this component on the model of every attacking
    /// character. It owns no gameplay — it only bridges Unity's animation-event dispatch to the attacker.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttackContactRelay : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The attacker notified on the contact frame. Defaults to the receiver on a parent.")]
        private MonoBehaviour _receiverBehaviour;

        private IAttackContactReceiver _receiver;

        private void Awake()
        {
            _receiver = _receiverBehaviour as IAttackContactReceiver ?? GetComponentInParent<IAttackContactReceiver>();

            if (_receiver == null)
            {
                Debug.LogWarning(
                    $"AttackContactRelay on '{name}' found no IAttackContactReceiver on a parent, so " +
                    "OnAttackContact events from this model will be ignored.", this);
            }
        }

        /// <summary>
        /// The animation event entry point. Author an event named <c>OnAttackContact</c> on the attack
        /// clip's contact frame; Unity dispatches it here and the owning attacker lands its blow.
        /// </summary>
        public void OnAttackContact()
        {
            _receiver?.NotifyAnimationContact();
        }
    }
}
