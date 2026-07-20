using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Presentation;
using ToyChest.Systems.Resources;
using UnityEngine;

namespace ToyChest.Gameplay.Player
{
    /// <summary>
    /// The thin presentation adapter that drives the character's <see cref="Animator"/> from gameplay
    /// state — it owns no gameplay rules. It reads movement-relative-to-facing off
    /// <see cref="PlayerLocomotion"/> to feed the directional (8-way) locomotion blend tree and the
    /// grounded flag, and hangs one-shot states off existing signals: locomotion's jump/roll events,
    /// the <see cref="PlayerCombat.Attacked"/> event, a health decrease (hit reaction), and the
    /// health-depleted signal (death, cleared when respawn refills health). Animation is downstream of
    /// gameplay; it never feeds back into it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAnimatorDriver : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The animator on the character model. Defaults to the first one found in children.")]
        private Animator _animator;

        [SerializeField]
        [Tooltip("The behaviour whose composed object supplies the health resource. Defaults to the sibling.")]
        private GameplayObjectBehaviour _behaviour;

        [SerializeField]
        [Tooltip("Locomotion adapter whose speed drives the blend tree. Defaults to the sibling.")]
        private PlayerLocomotion _locomotion;

        [SerializeField]
        [Tooltip("Combat adapter whose Attacked event fires the attack animation. Defaults to the sibling.")]
        private PlayerCombat _combat;

        [SerializeField]
        [Tooltip("Definition id of the health resource watched for hit reactions and death.")]
        private string _healthResourceId = "resource.health";

        [SerializeField]
        [Tooltip("How quickly the locomotion blend value follows the real speed, in seconds.")]
        private float _speedDamp = 0.1f;

        [Header("Juice")]
        [SerializeField]
        [Tooltip("Hit flash on the model. Defaults to the first one found in children.")]
        private HitFlash _hitFlash;

        [SerializeField]
        [Tooltip("Camera-shake impulse source. Defaults to the sibling.")]
        private CombatImpulse _impulse;

        [SerializeField]
        [Tooltip("Camera-shake force when the player is hit.")]
        private float _hitShake = 0.6f;

        [SerializeField]
        [Tooltip("Camera-shake force when the player swings.")]
        private float _attackShake = 0.18f;

        private static readonly int MoveXParam = Animator.StringToHash("MoveX");
        private static readonly int MoveYParam = Animator.StringToHash("MoveY");
        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int GroundedParam = Animator.StringToHash("Grounded");
        private static readonly int JumpParam = Animator.StringToHash("Jump");
        private static readonly int RollParam = Animator.StringToHash("Roll");
        private static readonly int AttackParam = Animator.StringToHash("Attack");
        private static readonly int HitParam = Animator.StringToHash("Hit");
        private static readonly int DeadParam = Animator.StringToHash("Dead");

        private DefinitionId _healthResource;
        private ResourceValue _health;
        private bool _boundCombat;
        private bool _boundLocomotion;
        private bool _dead;

        private void Awake()
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            if (_behaviour == null)
            {
                _behaviour = GetComponent<GameplayObjectBehaviour>();
            }

            if (_locomotion == null)
            {
                _locomotion = GetComponent<PlayerLocomotion>();
            }

            if (_combat == null)
            {
                _combat = GetComponent<PlayerCombat>();
            }

            if (_hitFlash == null)
            {
                _hitFlash = GetComponentInChildren<HitFlash>();
            }

            if (_impulse == null)
            {
                _impulse = GetComponent<CombatImpulse>();
            }

            if (_animator != null)
            {
                // Root motion is consumed by PlayerModelEvents (attack only) and forwarded to the
                // controller; locomotion stays velocity-driven because that relay ignores it off-attack.
                _animator.applyRootMotion = true;
            }

            _healthResource = new DefinitionId(_healthResourceId);
        }

        private void OnEnable()
        {
            if (_combat != null && !_boundCombat)
            {
                _combat.Attacked += OnAttacked;
                _boundCombat = true;
            }

            if (_locomotion != null && !_boundLocomotion)
            {
                _locomotion.Jumped += OnJumped;
                _locomotion.Rolled += OnRolled;
                _boundLocomotion = true;
            }
        }

        private void OnDisable()
        {
            if (_combat != null && _boundCombat)
            {
                _combat.Attacked -= OnAttacked;
                _boundCombat = false;
            }

            if (_locomotion != null && _boundLocomotion)
            {
                _locomotion.Jumped -= OnJumped;
                _locomotion.Rolled -= OnRolled;
                _boundLocomotion = false;
            }

            if (_health != null)
            {
                _health.ValueChanged -= OnHealthChanged;
                _health.Depleted -= OnDepleted;
                _health = null;
            }
        }

        private void Update()
        {
            EnsureHealthBound();

            if (_animator == null || _locomotion == null)
            {
                return;
            }

            // Movement relative to facing drives the directional (8-way) blend tree; grounded drives air/land.
            Vector2 move = _locomotion.LocalMove;
            _animator.SetFloat(MoveXParam, move.x, _speedDamp, Time.deltaTime);
            _animator.SetFloat(MoveYParam, move.y, _speedDamp, Time.deltaTime);
            // Undamped planar speed so Land/Roll can yield to locomotion the instant movement continues.
            _animator.SetFloat(SpeedParam, _locomotion.NormalizedSpeed);
            _animator.SetBool(GroundedParam, _locomotion.IsGrounded);
        }

        private void OnJumped()
        {
            if (!_dead && _animator != null)
            {
                _animator.SetTrigger(JumpParam);
            }
        }

        private void OnRolled()
        {
            if (!_dead && _animator != null)
            {
                _animator.SetTrigger(RollParam);
            }
        }

        private void EnsureHealthBound()
        {
            if (_health != null || _behaviour == null || _behaviour.Object == null || !_behaviour.Object.IsActive)
            {
                return;
            }

            if (_behaviour.Object.TryGet(out ResourceSet resources))
            {
                _health = resources.GetResource(_healthResource);
                if (_health != null)
                {
                    _health.ValueChanged += OnHealthChanged;
                    _health.Depleted += OnDepleted;
                }
            }
        }

        private void OnAttacked()
        {
            if (_dead)
            {
                return;
            }

            if (_animator != null)
            {
                _animator.SetTrigger(AttackParam);
            }

            if (_impulse != null)
            {
                _impulse.Shake(_attackShake);
            }
        }

        private void OnHealthChanged(float previous, float current, float maximum)
        {
            if (_animator == null)
            {
                return;
            }

            if (current < previous && current > 0f && !_dead)
            {
                _animator.SetTrigger(HitParam);
                if (_hitFlash != null) _hitFlash.Flash();
                if (_impulse != null) _impulse.Shake(_hitShake);
            }
            else if (current > previous && _dead && current > 0f)
            {
                // Respawn refilled health — leave the death state.
                _dead = false;
                _animator.SetBool(DeadParam, false);
            }
        }

        private void OnDepleted()
        {
            _dead = true;
            if (_animator != null)
            {
                _animator.SetBool(DeadParam, true);
            }
        }
    }
}
