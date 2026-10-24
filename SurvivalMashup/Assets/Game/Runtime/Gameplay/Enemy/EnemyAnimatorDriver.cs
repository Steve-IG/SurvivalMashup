using ToyChest.Gameplay.Combat;
using ToyChest.Gameplay.Presentation;
using UnityEngine;

namespace ToyChest.Gameplay.Enemy
{
    /// <summary>
    /// The thin presentation adapter that drives the Grunt's <see cref="Animator"/> from its
    /// behaviour — it owns no gameplay rules. It reads speed off <see cref="EnemyCombatant"/> to
    /// blend idle/pursue locomotion, and hangs the one-shot states off the combatant's existing
    /// signals: the wind-up telegraph plays the attack animation (so the anticipation the player
    /// reads is the animation itself), a hit reaction (flinch / stagger / knockback) plays the matching hit clip, and death plays the
    /// death animation during the corpse's linger delay. An elite's signature wind-up plays the slam state.
    /// Animation is downstream of behaviour.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyCombatant))]
    public sealed class EnemyAnimatorDriver : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The animator on the enemy model. Defaults to the first one found in children.")]
        private Animator _animator;

        [SerializeField]
        [Tooltip("The combatant whose speed and combat events drive animation. Defaults to the sibling.")]
        private EnemyCombatant _combatant;

        [SerializeField]
        [Tooltip("How quickly the locomotion blend value follows the real speed, in seconds.")]
        private float _speedDamp = 0.1f;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int AttackParam = Animator.StringToHash("Attack");
        private static readonly int HitParam = Animator.StringToHash("Hit");
        private static readonly int DeadParam = Animator.StringToHash("Dead");
        private static readonly int SlamParam = Animator.StringToHash("Slam");
        private static readonly int HitHeavyParam = Animator.StringToHash("HitHeavy");
        private static readonly int KnockbackParam = Animator.StringToHash("Knockback");

        private bool _bound;
        private bool _dead;
        private bool _hasSlam;
        private bool _hasHitHeavy;
        private bool _hasKnockback;

        private void Awake()
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            if (_combatant == null)
            {
                _combatant = GetComponent<EnemyCombatant>();
            }

            if (_animator != null)
            {
                _animator.applyRootMotion = false;
                foreach (AnimatorControllerParameter parameter in _animator.parameters)
                {
                    _hasSlam |= parameter.nameHash == SlamParam;
                    _hasHitHeavy |= parameter.nameHash == HitHeavyParam;
                    _hasKnockback |= parameter.nameHash == KnockbackParam;
                }
            }
        }

        private void OnEnable()
        {
            if (_combatant != null && !_bound)
            {
                _combatant.WindUpStarted += OnWindUp;
                _combatant.SignatureWindUpStarted += OnSignatureWindUp;
                _combatant.HitReacted += OnHitReacted;
                _combatant.Died += OnDied;
                _bound = true;
            }
        }

        private void OnDisable()
        {
            if (_combatant != null && _bound)
            {
                _combatant.WindUpStarted -= OnWindUp;
                _combatant.SignatureWindUpStarted -= OnSignatureWindUp;
                _combatant.HitReacted -= OnHitReacted;
                _combatant.Died -= OnDied;
                _bound = false;
            }
        }

        // The hit animation follows the gameplay reaction, never raw damage: a blow too light for this body's Stability
        // plays nothing (the hit flash still shows it landed), a flinch plays the light hit, a stagger the heavy one, a
        // knockback the knockback clip. Gameplay has already interrupted / staggered / pushed the enemy; the animator only
        // presents it. Controllers without the heavier states fall back to the lighter ones.
        private void OnHitReacted(HitReaction reaction)
        {
            if (_dead || _animator == null || !reaction.Reacts)
            {
                return;
            }

            if (reaction.Level == HitReactionLevel.Knockback && _hasKnockback)
            {
                _animator.SetTrigger(KnockbackParam);
            }
            else if (reaction.Level >= HitReactionLevel.Stagger && _hasHitHeavy)
            {
                _animator.SetTrigger(HitHeavyParam);
            }
            else
            {
                _animator.SetTrigger(HitParam);
            }
        }

        private void Update()
        {
            if (_animator == null || _combatant == null || _dead)
            {
                return;
            }

            float max = _combatant.MaxSpeed;
            float normalized = max > 0.01f ? Mathf.Clamp01(_combatant.CurrentPlanarSpeed / max) : 0f;
            _animator.SetFloat(SpeedParam, normalized, _speedDamp, Time.deltaTime);
        }

        private void OnWindUp()
        {
            if (!_dead && _animator != null)
            {
                _animator.SetTrigger(AttackParam);
            }
        }

        // The signature attack's wind-up plays the slam, slowed in the controller so the clip's impact frame lands
        // on the frame the area resolves. Controllers without a Slam parameter (an ordinary Grunt) ignore it.
        private void OnSignatureWindUp()
        {
            if (!_dead && _hasSlam)
            {
                _animator.SetTrigger(SlamParam);
            }
        }

        private void OnDied()
        {
            _dead = true;
            if (_animator != null)
            {
                _animator.SetBool(DeadParam, true);
            }
        }
    }
}
