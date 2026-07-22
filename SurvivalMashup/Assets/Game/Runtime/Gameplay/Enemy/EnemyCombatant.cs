using System;
using System.Collections.Generic;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.HitDetection;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.Attributes;
using ToyChest.Systems.GameplayEffects;
using ToyChest.Systems.Resources;
using UnityEngine;

namespace ToyChest.Gameplay.Enemy
{
    /// <summary>
    /// The thin Unity adapter that makes an enemy Gameplay Object act: it reads the target and speed
    /// from the composed object and the scene, asks the pure <see cref="PursuitMotor"/> for a step,
    /// drives the <see cref="CharacterController"/>, and — when in striking range — activates the
    /// enemy's authored attack ability against the player. It owns no gameplay rules: pursuit is the
    /// motor's decision, damage is the ability's <c>DamageEffect</c>, cadence is that ability's
    /// cooldown, and death is the Resource System's health-depleted signal. It is the enemy
    /// counterpart to <c>NpcWanderLocomotion</c>, driven by <c>Update</c>, not by any AI/combat
    /// manager.
    ///
    /// The strike resolves <em>which</em> object it lands on through the same canonical hit-detection
    /// vocabulary the player uses (<c>ToyChest.Gameplay.HitDetection</c>): a frontal
    /// <see cref="HitVolume"/> queried at the moment of impact, so an enemy blow is directional and
    /// misses a player who dodged out of the arc. Player and enemy share one hit path, not two.
    ///
    /// On death the adapter drops authored loot (a <see cref="LootPickup"/> the player walks over)
    /// and destroys the enemy through the normal Gameplay Object lifecycle. Death is deferred one
    /// frame because it fires from inside the health resource's change callback (during the player's
    /// attack), so tearing down the object there would run mid-callback.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class EnemyCombatant : MonoBehaviour, IAttackContactReceiver
    {
        [SerializeField]
        [Tooltip("The behaviour whose composed object is this enemy. Defaults to the sibling.")]
        private GameplayObjectBehaviour _behaviour;

        [Header("Target")]
        [SerializeField]
        [Tooltip("Unity tag identifying the player the enemy pursues.")]
        private string _playerTag = "Player";

        [Header("Behaviour")]
        [SerializeField]
        [Tooltip("Definition id of the speed attribute read from the composed object.")]
        private string _speedAttributeId = "attribute.enemy_speed";

        [SerializeField]
        [Tooltip("Speed used before an object is bound or when it declares no speed attribute.")]
        private float _fallbackSpeed = 3.5f;

        [SerializeField]
        [Tooltip("Distance within which the enemy notices and pursues the player.")]
        private float _aggroRange = 12f;

        [SerializeField]
        [Tooltip("Distance within which the enemy stops and attacks.")]
        private float _attackRange = 1.8f;

        [SerializeField]
        [Range(0f, 180f)]
        [Tooltip("Half-angle of the strike's frontal arc, in degrees. The blow only lands on a target the " +
                 "enemy is facing, so a player who slips behind during the telegraph is not hit.")]
        private float _attackConeHalfAngle = 75f;

        [SerializeField]
        [Tooltip("Physics layers the strike's hit query searches for the player.")]
        private LayerMask _targetLayers = ~0;

        [SerializeField]
        [Tooltip("Humanoid bone the strike's hit volume originates from — match it to the attack animation. " +
                 "RightHand for a punch/claw, RightFoot (or LeftFoot) for a kick, Head for a headbutt. " +
                 "Falls back to the body transform if the rig is not humanoid or lacks the bone.")]
        private HumanBodyBones _attackBone = HumanBodyBones.RightHand;

        [SerializeField]
        [Tooltip("Local offset from the anchor bone, in the bone's local space (e.g. +Z to reach past the foot).")]
        private Vector3 _attackOffset;

        [SerializeField]
        [Tooltip("Definition id of the enemy's attack ability, activated against the player when in range.")]
        private string _attackAbilityId = "ability.enemy_strike";

        [Header("Attack timing")]
        [SerializeField]
        [Tooltip("Telegraph: seconds the enemy anticipates (rooted) before the strike lands, so the player can read and dodge it.")]
        private float _windUpSeconds = 0.5f;

        [SerializeField]
        [Tooltip("Recovery: seconds the enemy is committed (rooted, vulnerable) after a strike before it can act again.")]
        private float _recoverSeconds = 0.65f;

        [Header("Movement")]
        [SerializeField]
        [Tooltip("Downward acceleration applied while ungrounded, in units per second squared.")]
        private float _gravity = 20f;

        [SerializeField]
        [Tooltip("How quickly the enemy turns to face the player / its movement, in degrees per second.")]
        private float _turnSpeedDegrees = 540f;

        [Header("Loot")]
        [SerializeField]
        [Tooltip("Optional loot pickup instantiated at the enemy's position on death.")]
        private GameObject _lootPrefab;

        [Header("Death")]
        [SerializeField]
        [Tooltip("Seconds the corpse lingers after death so a death animation / dissolve can play before teardown.")]
        private float _deathDelay = 1.3f;

        private CharacterController _controller;
        private PursuitMotor _motor;
        private HitDetector _detector;
        private HitVolumeEmitter _strike;
        private DefinitionId _speedAttribute;
        private DefinitionId _attackAbility;
        private Transform _player;
        private ResourceValue _health;
        private bool _subscribed;
        private bool _dead;
        private float _verticalVelocity;
        private AttackPhase _attackPhase;
        private float _attackTimer;
        private float _deathTimer;
        private float _lastPlanarSpeed;

        /// <summary>Raised when the enemy begins telegraphing a strike — presentation reads this to flash the wind-up.</summary>
        public event Action WindUpStarted;

        /// <summary>Raised when a telegraphed strike actually lands — presentation reads this for the impact.</summary>
        public event Action Struck;

        /// <summary>Raised once when the enemy dies, before the corpse lingers — presentation reads this for the death anim / effect.</summary>
        public event Action Died;

        /// <summary>Raised when the enemy takes damage (but survives) — presentation reads this for a hit flash.</summary>
        public event Action Damaged;

        /// <summary>Current planar speed in units per second. Presentation reads this to drive locomotion animation.</summary>
        public float CurrentPlanarSpeed => _lastPlanarSpeed;

        /// <summary>The enemy's top pursuit speed right now (the authored attribute value).</summary>
        public float MaxSpeed => CurrentSpeed();

        /// <summary>The self-paced attack cycle that makes the strike readable and fair.</summary>
        private enum AttackPhase
        {
            /// <summary>Not attacking; free to pursue.</summary>
            Ready,

            /// <summary>Anticipating: rooted and telegraphing before the blow lands.</summary>
            WindUp,

            /// <summary>Committed: rooted and vulnerable just after the blow.</summary>
            Recover,
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _motor = new PursuitMotor();
            _detector = new HitDetector();

            // The strike uses the identical authoring the player's punch does — a cone anchored to an
            // authored humanoid bone — proving one attack authoring is reused across different characters
            // with no enemy-specific hit code. Only the model, rig, bone, and filter tag differ, so
            // swapping the attack animation to a kick is a matter of pointing _attackBone at a foot.
            var volume = new HitVolume(HitShape.Cone, _attackRange, _attackConeHalfAngle, multiTarget: false, maxTargets: 1);
            var anchor = new HitVolumeAnchor(HitAnchorSpace.HumanoidBone, _attackBone, _attackOffset, HitFacing.Owner);
            _strike = new HitVolumeEmitter(volume, anchor, _playerTag, _targetLayers);
            _strike.Bind(transform, GetComponentInChildren<Animator>());

            _speedAttribute = new DefinitionId(_speedAttributeId);
            _attackAbility = new DefinitionId(_attackAbilityId);
            if (_behaviour == null)
            {
                _behaviour = GetComponent<GameplayObjectBehaviour>();
            }
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.Depleted -= OnDied;
                _health.ValueChanged -= OnHealthChanged;
                _health = null;
                _subscribed = false;
            }
        }

        private void Update()
        {
            float delta = Time.deltaTime;

            if (_dead)
            {
                DeathTick(delta);
                return;
            }

            EnsureSubscribed();

            Vector3 position = transform.position;
            Vector3 planarVelocity = Vector3.zero;

            if (IsActiveObject())
            {
                AcquirePlayer();
                bool hasPlayer = _player != null;
                Vector2 selfXZ = new Vector2(position.x, position.z);
                Vector2 targetXZ = hasPlayer ? new Vector2(_player.position.x, _player.position.z) : Vector2.zero;

                PursuitStep step = _motor.Tick(selfXZ, targetXZ, hasPlayer, CurrentSpeed(), _aggroRange, _attackRange);
                planarVelocity = new Vector3(step.PlanarVelocity.x, 0f, step.PlanarVelocity.y);

                bool inRange = step.Phase == PursuitPhase.Attack && hasPlayer;

                // Telegraphed attack cycle so the strike is readable and fair: the enemy roots and
                // anticipates (WindUp), the blow lands, then it is briefly committed (Recover).
                // Cadence lives here, in the enemy's behaviour, not in the ability cooldown.
                switch (_attackPhase)
                {
                    case AttackPhase.Ready:
                        if (inRange)
                        {
                            _attackPhase = AttackPhase.WindUp;
                            _attackTimer = _windUpSeconds;
                            WindUpStarted?.Invoke();
                        }

                        break;

                    case AttackPhase.WindUp:
                        planarVelocity = Vector3.zero;
                        _attackTimer -= delta;
                        if (!inRange)
                        {
                            _attackPhase = AttackPhase.Ready; // player left the telegraph — a fair dodge
                        }
                        else if (_attackTimer <= 0f)
                        {
                            if (TryAttack())
                            {
                                Struck?.Invoke();
                            }

                            _attackPhase = AttackPhase.Recover;
                            _attackTimer = _recoverSeconds;
                        }

                        break;

                    case AttackPhase.Recover:
                        planarVelocity = Vector3.zero;
                        _attackTimer -= delta;
                        if (_attackTimer <= 0f)
                        {
                            _attackPhase = AttackPhase.Ready;
                        }

                        break;
                }

                // Face the player through the whole attack cycle; otherwise face the movement direction.
                if (_attackPhase != AttackPhase.Ready && hasPlayer)
                {
                    FaceTarget(_player.position - position, delta);
                }
                else
                {
                    FaceTarget(planarVelocity, delta);
                }
            }

            _lastPlanarSpeed = new Vector2(planarVelocity.x, planarVelocity.z).magnitude;

            // Grounded gravity so the controller rests on terrain, matching the other locomotion adapters.
            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }
            else
            {
                _verticalVelocity -= _gravity * delta;
            }

            planarVelocity.y = _verticalVelocity;
            _controller.Move(planarVelocity * delta);
        }

        private void EnsureSubscribed()
        {
            if (_subscribed || !IsActiveObject())
            {
                return;
            }

            if (_behaviour.Object.TryGet(out ResourceSet resources))
            {
                _health = resources.GetResource(new DefinitionId("resource.health"));
                if (_health != null)
                {
                    _health.Depleted += OnDied;
                    _health.ValueChanged += OnHealthChanged;
                    _subscribed = true;
                }
            }
        }

        private void OnHealthChanged(float previous, float current, float maximum)
        {
            if (current < previous && current > 0f)
            {
                Damaged?.Invoke();
            }
        }

        private void AcquirePlayer()
        {
            if (_player != null)
            {
                return;
            }

            GameObject found = GameObject.FindGameObjectWithTag(_playerTag);
            if (found != null)
            {
                _player = found.transform;
            }
        }

        /// <summary>
        /// Lands the telegraphed strike on the attack animation's contact frame (the canonical
        /// <c>OnAttackContact</c> event, relayed by <see cref="AttackContactRelay"/>) instead of at the end
        /// of the wind-up timer — the same seam the player uses, so one authored clip serves both. Ignored
        /// unless the enemy is currently winding up, so a stray or repeated event can never add a second
        /// blow; with no event authored the wind-up timer still lands it.
        /// </summary>
        public void NotifyAnimationContact()
        {
            if (_dead || _attackPhase != AttackPhase.WindUp)
            {
                return;
            }

            if (TryAttack())
            {
                Struck?.Invoke();
            }

            _attackPhase = AttackPhase.Recover;
            _attackTimer = _recoverSeconds;
        }

        private bool TryAttack()
        {
            GameplayObject enemy = _behaviour.Object;
            if (enemy == null || !enemy.TryGet(out AbilitySet abilities))
            {
                return false;
            }

            // The strike lands through the same canonical hit-detection vocabulary the player uses: a
            // frontal cone anchored to the hand, resolved at the moment of impact. If the player slipped
            // out of the arc during the telegraph, the query returns nothing and the blow whiffs — the
            // enemy is not privileged.
            IReadOnlyList<HitResult> hits = _strike.Detect(enemy, _detector);
            if (hits.Count == 0)
            {
                return false;
            }

            return abilities.TryActivate(_attackAbility, EffectTarget.From(hits[0].Object)) == AbilityActivationResult.Activated;
        }

        private void OnDied()
        {
            if (_dead)
            {
                return;
            }

            _dead = true;
            _deathTimer = _deathDelay;
            _lastPlanarSpeed = 0f;
            StopBeingHittable();
            Died?.Invoke();
        }

        /// <summary>
        /// Removes the corpse from hit detection the instant it dies. Teardown is deliberately deferred so
        /// the death animation can play, but during that window the enemy must not be hittable — hit
        /// detection resolves colliders, so disabling them is what stops a dead enemy from soaking further
        /// blows. Disabling the <see cref="CharacterController"/> also ends its movement, so the death
        /// tick stops driving it.
        /// </summary>
        private void StopBeingHittable()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }

        private void DeathTick(float delta)
        {
            // Let the death animation / dissolve play, then tear down. The controller is disabled on death
            // (so the corpse cannot be hit), which also means it no longer moves — settling stops here.
            if (_controller.enabled)
            {
                if (_controller.isGrounded && _verticalVelocity < 0f)
                {
                    _verticalVelocity = -2f;
                }
                else
                {
                    _verticalVelocity -= _gravity * delta;
                }

                _controller.Move(new Vector3(0f, _verticalVelocity, 0f) * delta);
            }

            _deathTimer -= delta;
            if (_deathTimer <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            if (_lootPrefab != null)
            {
                Vector3 drop = transform.position;
                Instantiate(_lootPrefab, drop, Quaternion.identity);
            }

            // Destroying the GameObject tears down the Gameplay Object through GameplayObjectBehaviour.
            Destroy(gameObject);
        }

        private bool IsActiveObject()
        {
            return _behaviour != null && _behaviour.Object != null && _behaviour.Object.IsActive;
        }

        private float CurrentSpeed()
        {
            if (IsActiveObject() &&
                _behaviour.Object.TryGet(out AttributeSet attributes) &&
                attributes.TryGetValue(_speedAttribute, out float speed))
            {
                return speed;
            }

            return _fallbackSpeed;
        }

        private void FaceTarget(Vector3 direction, float delta)
        {
            Vector3 planar = new Vector3(direction.x, 0f, direction.z);
            if (planar.sqrMagnitude < 1e-4f)
            {
                return;
            }

            Quaternion target = Quaternion.LookRotation(planar, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _turnSpeedDegrees * delta);
        }
    }
}
