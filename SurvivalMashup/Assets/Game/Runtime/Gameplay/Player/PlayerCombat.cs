using System;
using System.Collections.Generic;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.HitDetection;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.GameplayEffects;
using UnityEngine;

namespace ToyChest.Gameplay.Player
{
    /// <summary>
    /// The thin Unity adapter for the player's melee attack. A press starts a phased swing —
    /// <c>wind-up → impact → recovery</c> (the pure <see cref="MeleeSwing"/>) — so the blow lands on the
    /// swing's contact frame instead of the input frame, which is what makes the hit feel connected. The
    /// swing is gated at its start by the ability's own validation (<see cref="AbilitySet.CanActivate"/>),
    /// so cadence stays the authored cooldown; the damage itself is applied at impact by activating the
    /// player's authored attack ability (its <c>DamageEffect</c>).
    ///
    /// <para>Which GameplayObject the blow lands on is decided by the canonical hit-detection vocabulary,
    /// not by bespoke code here: a <see cref="HitVolume"/> (a frontal <see cref="HitShape.Cone"/> sized to
    /// the attack's reach) resolved by the shared <see cref="HitDetector"/> at the contact frame. Because
    /// the cone is directional, a swing cannot connect with something behind the player — the hit is
    /// physically tied to facing and to the animation. Hit detection answers <em>what was hit</em>; the
    /// ability's Gameplay Effects answer <em>what happens</em>; the two never mix.</para>
    ///
    /// It owns no gameplay rules: damage and cooldown are the ability's, and whether the swing lands is
    /// the Ability System's decision. The input adapter calls <see cref="TryAttack"/>; this component
    /// never reads the input device. Presentation (animation, VFX, camera) hangs off the
    /// <see cref="Attacked"/> and <see cref="Impacted"/> events.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameplayObjectBehaviour))]
    public sealed class PlayerCombat : MonoBehaviour, IAttackContactReceiver
    {
        [SerializeField]
        [Tooltip("The behaviour whose composed object is the attacker. Defaults to the sibling.")]
        private GameplayObjectBehaviour _behaviour;

        [SerializeField]
        [Tooltip("Definition id of the attack ability activated against the target.")]
        private string _attackAbilityId = "ability.player_strike";

        [SerializeField]
        [Tooltip("Reach of the attack, in world units.")]
        private float _attackRange = 2.5f;

        [SerializeField]
        [Range(0f, 180f)]
        [Tooltip("Half-angle of the swing's frontal arc, in degrees (60 ≈ a 120° swing). The blow cannot " +
                 "land outside this arc, so it never connects with something behind the player.")]
        private float _attackConeHalfAngle = 70f;

        [SerializeField]
        [Tooltip("Unity tag identifying valid attack targets.")]
        private string _enemyTag = "Enemy";

        [SerializeField]
        [Tooltip("Physics layers searched for targets.")]
        private LayerMask _targetLayers = ~0;

        [SerializeField]
        [Tooltip("Maximum colliders inspected per swing. Bounds allocation-free discovery.")]
        private int _maxCandidates = 16;

        [Header("Hit volume anchoring (the punch originates from the hand, not the body centre)")]
        [SerializeField]
        [Tooltip("Shared hit-volume preset for the primary swing. When set, overrides the inline reach/arc above.")]
        private HitVolumeAsset _attackVolumePreset;

        [SerializeField]
        [Tooltip("Humanoid bone the swing originates from (RightHand for a punch). Falls back to the body if unrigged.")]
        private HumanBodyBones _attackBone = HumanBodyBones.RightHand;

        [SerializeField]
        [Tooltip("Local offset from the anchor bone (e.g. +Z to reach past the fist).")]
        private Vector3 _attackOffset = new Vector3(0f, 0f, 0.1f);

        [SerializeField]
        [Tooltip("Additional simultaneous hit regions for multi-volume attacks (e.g. a two-fist smash). Empty for a simple swing.")]
        private HitVolumeEmitter[] _extraContactVolumes;

        [SerializeField]
        [Tooltip("How quickly the player snaps to face the struck target, in degrees per second.")]
        private float _turnSpeedDegrees = 1080f;

        [Header("Swing phases (authored feel; tuned to the attack clip's contact frame)")]
        [SerializeField]
        [Tooltip("Anticipation before the blow lands, in seconds — the time from the press to the punch's " +
                 "contact frame. A fallback: an 'OnAttackContact' animation event lands it exactly if authored.")]
        private float _windUpDuration = 0.4f;

        [SerializeField]
        [Tooltip("Follow-through after the blow before another swing may start, in seconds. Kept short so " +
                 "repeat punches feel immediate; this no longer roots the player (see post-impact commit).")]
        private float _recoveryDuration = 0.12f;

        [SerializeField]
        [Tooltip("Seconds the player stays committed AFTER the contact frame before movement returns. " +
                 "This is the knob that makes a punch feel snappy rather than floaty — keep it small.")]
        private float _postImpactCommit = 0.05f;

        [Header("Feel")]
        [SerializeField]
        [Tooltip("Locomotion adapter briefly damped during a swing so the attack commits. Defaults to the sibling.")]
        private PlayerLocomotion _locomotion;

        [SerializeField]
        [Tooltip("Seconds a pressed-but-unready attack is remembered, so a press just before the swing is ready still lands.")]
        private float _bufferWindow = 0.18f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Movement-speed multiplier while a swing is in progress (attack commitment; 0 = rooted, 1 = free). " +
                 "Rooted by default so the punch's own root motion moves the character instead of the feet sliding.")]
        private float _commitMoveScale;

        private DefinitionId _attackAbility;
        private HitDetector _detector;
        private HitVolumeEmitter _primaryAttack;
        private float _bufferTimer;
        private int _lastTargetsHit;
        private Vector3 _lastContactPoint;
        private bool _hasContactPoint;
        private readonly MeleeSwing _swing = new MeleeSwing();

        /// <summary>
        /// Stage 1 — raised when a swing starts. Presentation reads this for the swing animation and a
        /// whoosh. It is <b>not</b> impact feedback: it fires on every press, hit or miss.
        /// </summary>
        public event Action Attacked;

        /// <summary>
        /// Stage 2 — raised when the swing reaches its contact frame, whether or not anything was hit.
        /// For debug tooling and readability only; never wire impact juice to this.
        /// </summary>
        public event Action Contacted;

        /// <summary>
        /// Stage 3 — raised only when a hit is <b>confirmed</b> (the ability committed and damage landed),
        /// carrying the real world-space contact point on the target's surface. All impact presentation —
        /// camera impulse, impact VFX, hit-stop, impact sound — hangs off this and this alone, so a swing
        /// into empty air produces no impact feedback.
        /// </summary>
        public event Action<Vector3> Impacted;

        /// <summary>Whether a swing is currently in progress.</summary>
        public bool IsAttacking => _swing.IsActive;

        /// <summary>The swing's current phase (Idle / WindUp / Recovery). Debug tooling reads this.</summary>
        public MeleePhase Phase => _swing.Phase;

        /// <summary>Targets confirmed hit by the most recent contact frame (0 on a whiff). Debug only.</summary>
        public int LastTargetsHit => _lastTargetsHit;

        /// <summary>The most recent confirmed contact point, and whether one has been recorded. Debug only.</summary>
        public bool TryGetLastContactPoint(out Vector3 point)
        {
            point = _lastContactPoint;
            return _hasContactPoint;
        }

        /// <summary>The primary swing's authored hit region, for debug visualization. Never gameplay.</summary>
        public HitVolumeEmitter PrimaryAttackVolume => _primaryAttack;

        /// <summary>Remaining cooldown on the attack ability, or 0 when ready. Debug read-only.</summary>
        public float AttackCooldownRemaining
        {
            get
            {
                GameplayObject attacker = _behaviour != null ? _behaviour.Object : null;
                if (attacker == null || !attacker.IsActive || !attacker.TryGet(out AbilitySet abilities))
                {
                    return 0f;
                }

                AbilityInstance instance = abilities.GetAbility(_attackAbility);
                return instance != null ? instance.CooldownRemaining : 0f;
            }
        }

        private void Awake()
        {
            if (_behaviour == null)
            {
                _behaviour = GetComponent<GameplayObjectBehaviour>();
            }

            if (_locomotion == null)
            {
                _locomotion = GetComponent<PlayerLocomotion>();
            }

            _attackAbility = new DefinitionId(_attackAbilityId);
            _detector = new HitDetector(_maxCandidates);

            // The primary swing: a shared preset if authored, else a cone from the inline reach/arc,
            // anchored to the hand bone so the punch originates where the fist is (falls back to the body
            // for an unrigged object). This is the whole authoring workflow in code form.
            HitVolume volume = _attackVolumePreset != null
                ? _attackVolumePreset.Volume
                : new HitVolume(HitShape.Cone, _attackRange, _attackConeHalfAngle, multiTarget: false, maxTargets: 1);
            var anchor = new HitVolumeAnchor(HitAnchorSpace.HumanoidBone, _attackBone, _attackOffset, HitFacing.Owner);
            _primaryAttack = new HitVolumeEmitter(volume, anchor, _enemyTag, _targetLayers);

            Animator animator = GetComponentInChildren<Animator>();
            _primaryAttack.Bind(transform, animator);
            if (_extraContactVolumes != null)
            {
                for (int i = 0; i < _extraContactVolumes.Length; i++)
                {
                    _extraContactVolumes[i]?.Bind(transform, animator);
                }
            }
        }

        private void Update()
        {
            float delta = Time.deltaTime;

            if (_swing.IsActive)
            {
                // A swing owns the frame: advance its phases and land the blow on the contact frame.
                MeleeSwingTick tick = _swing.Advance(delta);
                if (tick.Impacted)
                {
                    ResolveImpact();
                }

                return;
            }

            if (_bufferTimer > 0f)
            {
                _bufferTimer -= delta;
                if (TryStartSwing())
                {
                    _bufferTimer = 0f;
                }
            }
        }

        /// <summary>
        /// The attack input entrypoint. Starts a swing immediately if able; otherwise remembers the press
        /// for a short buffer window (<see cref="_bufferWindow"/>) so a press landing a hair before the
        /// swing is ready (mid-recovery, or just before the cooldown ends) still connects, which is what
        /// makes attacking feel responsive under mashing.
        /// </summary>
        public void TryAttack()
        {
            if (!TryStartSwing())
            {
                _bufferTimer = _bufferWindow;
            }
        }

        /// <summary>
        /// Called by the attack animation's contact-frame event (an authored <c>OnAttackContact</c> event
        /// on the swing clip, relayed by <see cref="PlayerModelEvents"/>) to land the blow exactly when the
        /// punch connects rather than on the wind-up timer. Safe to leave unwired — if no event is authored,
        /// the wind-up timer lands the blow instead.
        /// </summary>
        public void NotifyAnimationContact()
        {
            if (_swing.CompleteWindUp())
            {
                ResolveImpact();
            }
        }

        /// <summary>
        /// Starts one swing if able. A swing must not already be in progress. The attack always responds
        /// to input: with no enemy in range the player still swings (a whiff), so pressing attack in the
        /// open never feels dead. When an enemy <em>is</em> in range, the swing is gated on the ability's
        /// own validation (cooldown, gates) against that target — validated without committing — so we do
        /// not play a swing at a live enemy off-cadence; that press buffers instead and fires when ready.
        /// Faces the target (if any), begins the phased swing, commits the player (damps movement for the
        /// swing), and raises <see cref="Attacked"/> for the animation. Damage is not applied here — it
        /// lands at impact. Returns whether a swing began.
        /// </summary>
        private bool TryStartSwing()
        {
            if (_swing.IsActive)
            {
                return false;
            }

            // A jump or a roll owns the character until it finishes: an attack must never cut one short.
            // The press is not thrown away — TryAttack buffers it, so it fires as soon as the player lands
            // or the roll recovers, which keeps mashing responsive without stealing control from movement.
            if (_locomotion != null && (_locomotion.IsRolling || !_locomotion.IsGrounded))
            {
                return false;
            }

            GameplayObject attacker = _behaviour != null ? _behaviour.Object : null;
            if (attacker == null || !attacker.IsActive || !attacker.TryGet(out AbilitySet abilities))
            {
                return false;
            }

            if (TryDetectTarget(attacker, out HitResult hit))
            {
                // A live target in the swing arc: only swing if the ability could actually land, so a
                // fighting player's cadence stays the authored cooldown (an off-cadence press buffers).
                if (abilities.CanActivate(_attackAbility, EffectTarget.From(hit.Object)) != AbilityActivationResult.Activated)
                {
                    return false;
                }
            }

            // Commit the swing's direction *instantly*. The player's stick wins: attacking while holding a
            // direction must strike that way immediately — including a new swing that starts the moment the
            // previous one ends, even a full reversal. Easing this at the turn rate is what made the
            // character barely rotate before swinging. With no movement input we snap to the target
            // instead (aim assist for a standing attack), and with neither we keep the current facing.
            Vector3 intent = _locomotion != null ? _locomotion.MoveIntentDirection : Vector3.zero;
            if (intent.sqrMagnitude > 1e-4f)
            {
                SnapFacing(intent);
            }
            else if (hit.Object != null)
            {
                SnapFacing(hit.ContactPoint - transform.position);
            }

            _swing.Begin(_windUpDuration, _recoveryDuration);

            // Commit only through the wind-up plus a brief beat past contact — not the whole recovery.
            // The blow still needs the player planted while it lands, but holding them rooted through the
            // follow-through is what made the punch feel floaty; movement now returns right after contact.
            _locomotion?.ApplyMovementLock(_windUpDuration + _postImpactCommit, _commitMoveScale);
            Attacked?.Invoke();
            return true;
        }

        /// <summary>
        /// The swing's contact frame — the middle of the three stages this adapter reports:
        /// <c>Attacked</c> (swing started) → <c>Contacted</c> (the contact frame arrived) →
        /// <c>Impacted</c> (a hit was confirmed). It re-acquires the nearest enemy (it may have moved or
        /// died during the wind-up) and activates the attack ability against it — the ability applies its
        /// authored damage and starts its cooldown. <see cref="Impacted"/> is raised <b>only</b> when the
        /// activation actually committed, and carries the real surface contact point, so impact
        /// presentation (camera impulse, VFX, hit-stop, sound) can never fire on a whiff. A swing into
        /// empty air reaches <see cref="Contacted"/> and stops there: no damage, no cooldown, no juice.
        /// </summary>
        private void ResolveImpact()
        {
            _lastTargetsHit = 0;
            Contacted?.Invoke();

            GameplayObject attacker = _behaviour != null ? _behaviour.Object : null;
            if (attacker == null || !attacker.IsActive || !attacker.TryGet(out AbilitySet abilities))
            {
                return;
            }

            if (!TryDetectTarget(attacker, out HitResult hit))
            {
                return;
            }

            // Only re-aim at the target if the player is not steering: while a direction is held, the
            // swing stays committed to the player's intent rather than being pulled toward an enemy.
            if (_locomotion == null || _locomotion.MoveIntentDirection.sqrMagnitude <= 1e-4f)
            {
                FaceToward(hit.ContactPoint);
            }

            if (abilities.TryActivate(_attackAbility, EffectTarget.From(hit.Object)) == AbilityActivationResult.Activated)
            {
                _lastTargetsHit = 1;
                _lastContactPoint = hit.ContactPoint;
                _hasContactPoint = true;
                Impacted?.Invoke(hit.ContactPoint);
            }
        }

        // The canonical hit query: resolve every authored contact region (the primary swing plus any
        // multi-volume extras), each anchored to its socket/bone, and return the nearest live enemy across
        // all of them. The same vocabulary the enemy strike and hazards use — no bespoke overlap loop lives
        // here anymore, and adding a second hit region is authoring an extra emitter, not writing code.
        private bool TryDetectTarget(GameplayObject attacker, out HitResult hit)
        {
            hit = default;
            float bestSqr = float.MaxValue;
            bool found = ConsiderNearest(_primaryAttack, attacker, ref hit, ref bestSqr);

            if (_extraContactVolumes != null)
            {
                for (int i = 0; i < _extraContactVolumes.Length; i++)
                {
                    found |= ConsiderNearest(_extraContactVolumes[i], attacker, ref hit, ref bestSqr);
                }
            }

            return found;
        }

        // Resolve one emitter and fold its nearest result into the running best. The detector's list is
        // reused across calls, so the nearest is captured here before the next emitter overwrites it.
        private bool ConsiderNearest(HitVolumeEmitter emitter, GameplayObject attacker, ref HitResult best, ref float bestSqr)
        {
            if (emitter == null)
            {
                return false;
            }

            IReadOnlyList<HitResult> hits = emitter.Detect(attacker, _detector);
            bool found = false;
            for (int i = 0; i < hits.Count; i++)
            {
                if (hits[i].SqrDistance < bestSqr)
                {
                    bestSqr = hits[i].SqrDistance;
                    best = hits[i];
                }

                found = true;
            }

            return found;
        }

        /// <summary>
        /// Turns to a world direction immediately, with no turn-rate easing — used when a swing commits so
        /// the attack always goes exactly where the player aimed it.
        /// </summary>
        private void SnapFacing(Vector3 direction)
        {
            Vector3 planar = new Vector3(direction.x, 0f, direction.z);
            if (planar.sqrMagnitude < 1e-4f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(planar.normalized, Vector3.up);
        }

        private void FaceToward(Vector3 worldPosition)
        {
            Vector3 planar = worldPosition - transform.position;
            planar.y = 0f;
            if (planar.sqrMagnitude < 1e-4f)
            {
                return;
            }

            Quaternion rotation = Quaternion.LookRotation(planar, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, rotation, _turnSpeedDegrees * Time.deltaTime);
        }
    }
}
