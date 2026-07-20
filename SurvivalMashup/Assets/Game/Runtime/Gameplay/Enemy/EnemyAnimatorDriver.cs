using ToyChest.Gameplay.Presentation;
using UnityEngine;

namespace ToyChest.Gameplay.Enemy
{
    /// <summary>
    /// The thin presentation adapter that drives the Grunt's <see cref="Animator"/> from its
    /// behaviour — it owns no gameplay rules. It reads speed off <see cref="EnemyCombatant"/> to
    /// blend idle/pursue locomotion, and hangs the one-shot states off the combatant's existing
    /// signals: the wind-up telegraph plays the attack animation (so the anticipation the player
    /// reads is the animation itself), a health decrease plays a hit reaction, and death plays the
    /// death animation during the corpse's linger delay. Animation is downstream of behaviour.
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

        [SerializeField]
        [Tooltip("Hit flash on the model. Defaults to the first one found in children.")]
        private HitFlash _hitFlash;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int AttackParam = Animator.StringToHash("Attack");
        private static readonly int HitParam = Animator.StringToHash("Hit");
        private static readonly int DeadParam = Animator.StringToHash("Dead");

        private bool _bound;
        private bool _dead;

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

            if (_hitFlash == null)
            {
                _hitFlash = GetComponentInChildren<HitFlash>();
            }

            if (_animator != null)
            {
                _animator.applyRootMotion = false;
            }
        }

        private void OnEnable()
        {
            if (_combatant != null && !_bound)
            {
                _combatant.WindUpStarted += OnWindUp;
                _combatant.Damaged += OnDamaged;
                _combatant.Died += OnDied;
                _bound = true;
            }
        }

        private void OnDisable()
        {
            if (_combatant != null && _bound)
            {
                _combatant.WindUpStarted -= OnWindUp;
                _combatant.Damaged -= OnDamaged;
                _combatant.Died -= OnDied;
                _bound = false;
            }
        }

        private void OnDamaged()
        {
            if (_hitFlash != null)
            {
                _hitFlash.Flash();
            }

            // A brief hit reaction so blows read as connecting. It is animation only — the enemy's
            // gameplay attack cadence is driven by EnemyCombatant's own timers, not the animator, so a
            // flinch never stunlocks the encounter; it just makes the strike feel like it lands.
            if (!_dead && _animator != null)
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
