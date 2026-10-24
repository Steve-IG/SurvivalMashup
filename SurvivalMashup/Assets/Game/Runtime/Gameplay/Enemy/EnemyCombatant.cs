using System;
using System.Collections.Generic;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Combat;
using ToyChest.Gameplay.HitDetection;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.Attributes;
using ToyChest.Systems.GameplayEffects;
using ToyChest.Systems.Resources;
using ToyChest.Systems.StatusEffects;
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
    public sealed class EnemyCombatant : MonoBehaviour, IAttackContactReceiver, IHitReactionReceiver
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

        [Header("Interruptibility (authored per attack phase)")]
        [SerializeField]
        [Tooltip("Whether a hit during the wind-up cancels this attack. When true (the readable default), " +
                 "striking the enemy while it telegraphs cuts the attack short so the pending blow never lands.")]
        private bool _windUpInterruptible = true;

        [SerializeField]
        [Tooltip("Whether a hit during the recovery cancels the follow-through. Off by default: once the blow " +
                 "has landed the enemy commits to its recovery and a hit does not arbitrarily cancel it.")]
        private bool _recoverInterruptible;

        [SerializeField]
        [Min(0f)]
        [Tooltip("The early interrupt window: a flinch-weight hit cancels the wind-up only during its first this-many " +
                 "seconds (once the enemy commits to the swing a light blow can no longer stop it). Stagger-weight hits " +
                 "interrupt the whole wind-up.")]
        private float _interruptWindowSeconds = 0.25f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds an enemy whose attack was interrupted by a flinch stays out of action before it can move or swing " +
                 "again (staggers and knockbacks use their own, longer lock). Without it the enemy would re-start the same " +
                 "swing the very next frame.")]
        private float _interruptRecoverySeconds = 0.4f;

        [Header("Hit reactions")]
        [SerializeField]
        [Tooltip("Attribute read as this body's Stability (weight / poise). A blow's Impact / Stability picks the reaction.")]
        private string _stabilityAttributeId = "attribute.stability";

        [SerializeField]
        [Min(0f)]
        [Tooltip("Stability used when the composed object has no Stability attribute.")]
        private float _fallbackStability = 10f;

        [SerializeField]
        [Tooltip("Ratio thresholds and stagger / knockback amounts that turn a blow's weight into a reaction.")]
        private HitReactionRules _hitReactionRules = new HitReactionRules();

        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Seconds a knockback slide lasts (it decelerates to rest over this time).")]
        private float _knockbackSlideSeconds = 0.3f;

        [Header("Signature attack (optional — elites)")]
        [SerializeField]
        [Tooltip("Definition id of the enemy's signature area attack (e.g. the Grunt Brute's Shockwave Slam), applied to " +
                 "everything its area catches. Empty = no signature attack (an ordinary Grunt).")]
        private string _signatureAbilityId = "";

        [SerializeField]
        [Min(0f)]
        [Tooltip("Radius of the signature attack's area, centred on the enemy. The ground telegraph shows exactly this circle.")]
        private float _signatureRadius = 3.5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Planar distance to the player within which the signature attack begins (when off cooldown).")]
        private float _signatureTriggerRange = 4.5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Telegraph: seconds the enemy is rooted, winding up, before the area lands — the player's window to leave it.")]
        private float _signatureWindUpSeconds = 1.2f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Recovery: seconds the enemy is rooted and vulnerable after the area lands — the player's punish window.")]
        private float _signatureRecoverSeconds = 1.6f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds after a signature attack's recovery before it may be used again; the ordinary strike fills the gap.")]
        private float _signatureCooldownSeconds = 5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds after the enemy appears before its first signature attack.")]
        private float _signatureInitialDelaySeconds = 1.5f;

        [SerializeField]
        [Tooltip("Whether a hit during the signature wind-up cancels it. Off by default: the answer to the slam is to get " +
                 "out of the circle, not to out-trade it.")]
        private bool _signatureWindUpInterruptible;

        [Header("Variant statuses (optional — elites)")]
        [SerializeField]
        [Tooltip("Statuses applied once when the enemy is composed. Every enemy archetype shares the base attribute " +
                 "definitions (Maximum Health, Enemy Speed), so an elite variant layers its authored modifiers and identity " +
                 "tag on top through an infinite status rather than through new attributes.")]
        private List<StatusEffectDefinition> _innateStatuses = new List<StatusEffectDefinition>();

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
        private EnemyAttackCycle _attackCycle;
        private EnemySignatureAttack _signature;
        private HitVolumeEmitter _signatureArea;
        private DefinitionId _signatureAbility;
        private readonly List<EffectTarget> _signatureTargets = new List<EffectTarget>();
        private bool _innateApplied;
        private float _deathTimer;
        private float _lastPlanarSpeed;
        private float _staggerTimer;
        private Vector3 _knockbackVelocity;
        private float _knockbackTimer;

        /// <summary>Raised when the enemy begins telegraphing a strike — presentation reads this to flash the wind-up.</summary>
        public event Action WindUpStarted;

        /// <summary>Raised when a telegraphed strike actually lands — presentation reads this for the impact.</summary>
        public event Action Struck;

        /// <summary>Raised once when the enemy dies, before the corpse lingers — presentation reads this for the death anim / effect.</summary>
        public event Action Died;

        /// <summary>Raised when the enemy takes damage (but survives) — presentation reads this for a hit flash.</summary>
        public event Action Damaged;

        /// <summary>
        /// Raised when a blow's weight produces a reaction on this body (Flinch, Stagger, Knockback - never None).
        /// Presentation plays the matching hit animation; gameplay has already interrupted / staggered / pushed it.
        /// </summary>
        public event Action<HitReaction> HitReacted;

        /// <summary>Whether the ordinary strike is winding up (telegraphing).</summary>
        public bool IsWindingUp => _attackCycle != null && _attackCycle.IsWindingUp;

        /// <summary>Whether the enemy is staggered (locked out of moving and attacking by a heavy hit).</summary>
        public bool IsStaggered => _staggerTimer > 0f;

        /// <summary>This body's current Stability (the authored attribute, or the fallback).</summary>
        public float Stability =>
            IsActiveObject() && _behaviour.Object.TryGet(out AttributeSet attributes) &&
            attributes.TryGetValue(new DefinitionId(_stabilityAttributeId), out float value) ? value : _fallbackStability;

        /// <summary>
        /// Resolves a blow's weight against this body's Stability and applies the reaction: a flinch interrupts the
        /// ordinary attack only inside its early window; a stagger interrupts any interruptible wind-up and locks the
        /// enemy for a moment; a knockback also slides it back along the blow. A signature attack authored
        /// uninterruptible is armour: during its wind-up the blow does damage only. Called by the attacker after the
        /// damage has been applied (so a killing blow finds the enemy already dead and does nothing).
        /// </summary>
        public HitReaction ReceiveImpact(in HitImpact impact)
        {
            if (_dead || !IsActiveObject())
            {
                return default;
            }

            HitReaction reaction = HitReactionResolver.Resolve(impact.Power, Stability, _hitReactionRules);
            if (!reaction.Reacts)
            {
                return reaction;
            }

            if (IsSignatureWindingUp && !_signatureWindUpInterruptible)
            {
                return new HitReaction(HitReactionLevel.None, reaction.Ratio, 0f, 0f);
            }

            bool heavy = reaction.Level >= HitReactionLevel.Stagger;
            bool interrupted = _attackCycle.TryInterrupt(ignoreWindow: heavy);
            interrupted |= _signature != null && _signature.TryInterrupt();

            if (heavy)
            {
                _staggerTimer = Mathf.Max(_staggerTimer, reaction.StaggerSeconds);
            }
            else if (interrupted)
            {
                _staggerTimer = Mathf.Max(_staggerTimer, _interruptRecoverySeconds);
            }

            if (reaction.Level == HitReactionLevel.Knockback && impact.Direction.sqrMagnitude > 0f)
            {
                // Linear deceleration to rest over the slide: distance = v0 * T / 2.
                _knockbackTimer = _knockbackSlideSeconds;
                _knockbackVelocity = impact.Direction * (2f * reaction.KnockbackDistance / _knockbackSlideSeconds);
            }

            HitReacted?.Invoke(reaction);
            return reaction;
        }

        /// <summary>Raised when the enemy begins winding up its signature attack — presentation shows the telegraph.</summary>
        public event Action SignatureWindUpStarted;

        /// <summary>
        /// Raised on the frame the signature attack's area lands, with its ground centre, its radius, and how many
        /// targets it damaged — presentation plays the impact here, hit or miss.
        /// </summary>
        public event Action<Vector3, float, int> SignatureStruck;

        /// <summary>Current planar speed in units per second. Presentation reads this to drive locomotion animation.</summary>
        public float CurrentPlanarSpeed => _lastPlanarSpeed;

        /// <summary>The enemy's top pursuit speed right now (the authored attribute value).</summary>
        public float MaxSpeed => CurrentSpeed();

        /// <summary>Whether this enemy has an authored signature attack.</summary>
        public bool HasSignatureAttack => _signature != null;

        /// <summary>Whether the signature attack is currently winding up (its telegraph is showing).</summary>
        public bool IsSignatureWindingUp => _signature != null && _signature.Cycle.IsWindingUp;

        /// <summary>Whether the signature attack is in its rooted recovery (the punish window).</summary>
        public bool IsSignatureRecovering => _signature != null && _signature.Cycle.Phase == EnemyAttackPhase.Recover;

        /// <summary>Whether the enemy has died (its corpse may still be lingering).</summary>
        public bool IsDead => _dead;

        /// <summary>The composed Gameplay Object this enemy acts for, or null before composition.</summary>
        public GameplayObject ComposedObject => _behaviour != null ? _behaviour.Object : null;

        /// <summary>
        /// The signature attack's live area while it winds up: the point on the ground under its centre, its radius,
        /// and the wind-up progress (0 → 1 at the instant it lands). The ground telegraph reads this — the same centre
        /// and radius the hit query uses, so the circle and the damage can never disagree. False when not winding up.
        /// </summary>
        public bool TryGetSignatureArea(out Vector3 groundCenter, out float radius, out float windUpProgress)
        {
            if (_dead || !IsSignatureWindingUp)
            {
                groundCenter = default;
                radius = 0f;
                windUpProgress = 0f;
                return false;
            }

            groundCenter = SignatureGroundCenter();
            radius = _signatureRadius;
            windUpProgress = _signature.Cycle.WindUpProgress;
            return true;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _motor = new PursuitMotor();
            _detector = new HitDetector();
            _attackCycle = new EnemyAttackCycle(_windUpSeconds, _recoverSeconds, _windUpInterruptible, _recoverInterruptible, _interruptWindowSeconds);

            // The strike uses the identical authoring the player's punch does — a cone anchored to an
            // authored humanoid bone — proving one attack authoring is reused across different characters
            // with no enemy-specific hit code. Only the model, rig, bone, and filter tag differ, so
            // swapping the attack animation to a kick is a matter of pointing _attackBone at a foot.
            var volume = new HitVolume(HitShape.Cone, _attackRange, _attackConeHalfAngle, multiTarget: false, maxTargets: 1);
            var anchor = new HitVolumeAnchor(HitAnchorSpace.HumanoidBone, _attackBone, _attackOffset, HitFacing.Owner);
            _strike = new HitVolumeEmitter(volume, anchor, _playerTag, _targetLayers);
            _strike.Bind(transform, GetComponentInChildren<Animator>());

            // The signature attack is the same vocabulary again: a sphere centred on the enemy, resolved on the frame
            // the wind-up completes. Only the shape and the ability differ from the ordinary strike.
            if (!string.IsNullOrEmpty(_signatureAbilityId))
            {
                _signature = new EnemySignatureAttack(
                    _signatureWindUpSeconds, _signatureRecoverSeconds, _signatureCooldownSeconds,
                    _signatureInitialDelaySeconds, _signatureTriggerRange, _signatureWindUpInterruptible);
                var area = new HitVolume(HitShape.Sphere, _signatureRadius, 180f, multiTarget: true, maxTargets: 8);
                var center = new HitVolumeAnchor(HitAnchorSpace.Owner, HumanBodyBones.Hips, Vector3.zero, HitFacing.Owner);
                _signatureArea = new HitVolumeEmitter(area, center, _playerTag, _targetLayers);
                _signatureArea.Bind(transform, GetComponentInChildren<Animator>());
                _signatureAbility = new DefinitionId(_signatureAbilityId);
            }

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

            if (IsActiveObject() && _staggerTimer > 0f)
            {
                // Staggered by a heavy hit: no pursuit, no attacks (the attack cycles hold), only the knockback slide.
                _staggerTimer -= delta;
                planarVelocity = KnockbackStep(delta);
            }
            else if (IsActiveObject())
            {
                AcquirePlayer();
                bool hasPlayer = _player != null;
                Vector2 selfXZ = new Vector2(position.x, position.z);
                Vector2 targetXZ = hasPlayer ? new Vector2(_player.position.x, _player.position.z) : Vector2.zero;

                PursuitStep step = _motor.Tick(selfXZ, targetXZ, hasPlayer, CurrentSpeed(), _aggroRange, _attackRange);
                planarVelocity = new Vector3(step.PlanarVelocity.x, 0f, step.PlanarVelocity.y);

                bool inRange = step.Phase == PursuitPhase.Attack && hasPlayer;

                // An elite's signature attack takes priority over the ordinary strike whenever it is ready and the
                // player is close, and never overlaps it. Once begun it commits: the area lands where the enemy
                // stands whether or not the player is still inside, so leaving the circle is the dodge.
                bool signatureBusy = TickSignature(delta, hasPlayer, Vector2.Distance(selfXZ, targetXZ));
                if (signatureBusy)
                {
                    planarVelocity = Vector3.zero;
                    inRange = false;
                }

                // Telegraphed attack cycle so the strike is readable and fair: the enemy roots and
                // anticipates (WindUp), the blow lands, then it is briefly committed (Recover). Cadence and
                // the authored interrupt windows live in the pure EnemyAttackCycle, not the ability cooldown
                // or the animator. A hit during an interruptible window cancels the cycle (see OnHealthChanged),
                // so this state machine simply resumes from Ready.
                switch (signatureBusy ? EnemyAttackPhase.Ready : _attackCycle.Phase)
                {
                    case EnemyAttackPhase.Ready:
                        if (inRange)
                        {
                            _attackCycle.BeginWindUp();
                            WindUpStarted?.Invoke();
                        }

                        break;

                    case EnemyAttackPhase.WindUp:
                        planarVelocity = Vector3.zero;
                        if (!inRange)
                        {
                            _attackCycle.Reset(); // player left the telegraph — a fair dodge
                        }
                        else if (_attackCycle.Advance(delta).Struck)
                        {
                            if (TryAttack())
                            {
                                Struck?.Invoke();
                            }
                        }

                        break;

                    case EnemyAttackPhase.Recover:
                        planarVelocity = Vector3.zero;
                        _attackCycle.Advance(delta);
                        break;
                }

                // Face the player through the whole attack cycle; otherwise face the movement direction.
                if ((_attackCycle.IsAttacking || signatureBusy) && hasPlayer)
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

        /// <summary>
        /// Advances the signature attack, if any: begins it when ready and the player is within its trigger range
        /// (and the ordinary strike is idle), and resolves its area on the tick the wind-up completes. Returns whether
        /// the signature attack is in progress, which roots the enemy and suspends the ordinary strike.
        /// </summary>
        private bool TickSignature(float delta, bool hasPlayer, float planarDistance)
        {
            if (_signature == null)
            {
                return false;
            }

            if (!_signature.Cycle.IsAttacking)
            {
                _signature.Advance(delta); // cooldown
                if (!hasPlayer || _attackCycle.IsAttacking || !_signature.ShouldBegin(planarDistance))
                {
                    return false;
                }

                _signature.Begin();
                SignatureWindUpStarted?.Invoke();
                return true;
            }

            if (_signature.Advance(delta).Struck)
            {
                ResolveSignature();
            }

            return _signature.Cycle.IsAttacking;
        }

        private void ResolveSignature()
        {
            GameplayObject enemy = _behaviour.Object;
            int damaged = 0;
            if (enemy != null && enemy.TryGet(out AbilitySet abilities))
            {
                // One commit applies the signature ability to everything inside the circle (the Multiple Targets path
                // Ground Slam uses), so a target is never struck twice by one slam.
                IReadOnlyList<HitResult> hits = _signatureArea.Detect(enemy, _detector);
                _signatureTargets.Clear();
                for (int i = 0; i < hits.Count; i++)
                {
                    _signatureTargets.Add(EffectTarget.From(hits[i].Object));
                }

                if (_signatureTargets.Count > 0 &&
                    abilities.TryActivate(_signatureAbility, _signatureTargets) == AbilityActivationResult.Activated)
                {
                    damaged = _signatureTargets.Count;
                }
            }

            SignatureStruck?.Invoke(SignatureGroundCenter(), _signatureRadius, damaged);
        }

        // The area is centred on the enemy's own transform; its ground point is the bottom of the controller capsule.
        private Vector3 SignatureGroundCenter()
        {
            Vector3 origin = _signatureArea != null && _signatureArea.IsBound ? _signatureArea.Origin : transform.position;
            float groundY = _controller != null && _controller.enabled ? _controller.bounds.min.y : transform.position.y;
            return new Vector3(origin.x, groundY, origin.z);
        }

        private void EnsureSubscribed()
        {
            if (_subscribed || !IsActiveObject())
            {
                return;
            }

            ApplyInnateStatuses();

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
                // Damage alone no longer interrupts: whether a blow flinches, staggers, or knocks the enemy back is
                // decided by its weight against this body's Stability, delivered separately (ReceiveImpact). Damage
                // with no impact (or an impact too light to matter) only raises Damaged (the hit flash).
                Damaged?.Invoke();
            }
        }

        // Once per composed object, before health is watched, so the variant's health boost and refill are not read
        // as damage or healing. Statuses are authoritative state, so a saved elite restores them through the Save System.
        private void ApplyInnateStatuses()
        {
            if (_innateApplied)
            {
                return;
            }

            _innateApplied = true;
            if (_innateStatuses.Count == 0 || !_behaviour.Object.TryGet(out StatusEffectSet statuses))
            {
                return;
            }

            for (int i = 0; i < _innateStatuses.Count; i++)
            {
                StatusEffectDefinition status = _innateStatuses[i];
                if (status != null && !statuses.Has(status.Id))
                {
                    statuses.Apply(status);
                }
            }
        }

        private Vector3 KnockbackStep(float delta)
        {
            if (_knockbackTimer <= 0f)
            {
                return Vector3.zero;
            }

            Vector3 velocity = _knockbackVelocity * (_knockbackTimer / _knockbackSlideSeconds);
            _knockbackTimer -= delta;
            return velocity;
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
            if (_dead)
            {
                return;
            }

            // A contact event in the first half of a wind-up cannot belong to it: it is the previous swing's clip (still
            // playing after an interrupt or a quick re-swing) reaching its contact frame. Accepting it would land the new
            // blow with almost no telegraph, so it is ignored and the wind-up timer (or this swing's own event) lands it.
            if (_attackCycle.WindUpElapsed < _windUpSeconds * 0.5f)
            {
                return;
            }

            // Lands the blow only if still winding up; if the attack was interrupted this frame the cycle is
            // already Ready, so CompleteWindUp returns false and the pending strike never fires.
            if (_attackCycle.CompleteWindUp() && TryAttack())
            {
                Struck?.Invoke();
            }
        }

        /// <summary>
        /// Part of the shared attack-animation contract, but intentionally a no-op for enemies: an enemy's
        /// recovery is a deliberate, timer-based vulnerability window (a fairness telegraph the player reads
        /// and punishes), not something the animation should cut short. The <c>OnAttackEnd</c> event exists
        /// for the player's snappy exit; ending the enemy's recovery early would shorten the very window the
        /// telegraph is meant to expose.
        /// </summary>
        public void NotifyAttackEnd()
        {
        }

        /// <summary>
        /// A windowed clip on an enemy lands its strike once, at the window's start — enemy strikes are single
        /// telegraphed blows, so the window's opening is simply the contact frame.
        /// </summary>
        public void NotifyContactWindowStart()
        {
            NotifyAnimationContact();
        }

        /// <summary>Nothing to close for an enemy: its strike already resolved when the window opened.</summary>
        public void NotifyContactWindowEnd()
        {
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
