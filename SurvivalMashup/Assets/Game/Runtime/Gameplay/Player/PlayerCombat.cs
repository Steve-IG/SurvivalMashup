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
    public sealed class PlayerCombat : MonoBehaviour
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
        [Tooltip("Follow-through after the blow before control returns to locomotion, in seconds. " +
                 "Wind-up + recovery is how long the player is committed to the swing.")]
        private float _recoveryDuration = 0.35f;

        [Header("Impact point (where the hit VFX spawns)")]
        [SerializeField]
        [Tooltip("Height above the target's base at which the blow reads, in world units (≈ chest).")]
        private float _hitHeight = 1.1f;

        [SerializeField]
        [Tooltip("How far back toward the player from the target's centre the hit point sits (the near surface).")]
        private float _hitInset = 0.45f;

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
        private readonly MeleeSwing _swing = new MeleeSwing();

        /// <summary>Raised when a swing starts — presentation reads this to play the swing animation.</summary>
        public event Action Attacked;

        /// <summary>Raised at the contact frame when a swing lands a hit, with the world-space hit point —
        /// impact presentation (VFX, camera impulse, hit pause) hangs off this.</summary>
        public event Action<Vector3> Impacted;

        /// <summary>Whether a swing is currently in progress.</summary>
        public bool IsAttacking => _swing.IsActive;

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

                FaceToward(hit.ContactPoint);
            }

            _swing.Begin(_windUpDuration, _recoveryDuration);
            _locomotion?.ApplyMovementLock(_windUpDuration + _recoveryDuration, _commitMoveScale);
            Attacked?.Invoke();
            return true;
        }

        /// <summary>
        /// Lands the blow at the swing's contact frame: re-acquires the nearest enemy (it may have moved
        /// or died during the wind-up), and activates the attack ability against it — the ability applies
        /// its authored damage and starts its cooldown. On a landed hit, raises <see cref="Impacted"/> with
        /// the hit point for presentation. If nothing is in range now, the swing simply whiffs (no damage,
        /// no cooldown), which is fine.
        /// </summary>
        private void ResolveImpact()
        {
            GameplayObject attacker = _behaviour != null ? _behaviour.Object : null;
            if (attacker == null || !attacker.IsActive || !attacker.TryGet(out AbilitySet abilities))
            {
                return;
            }

            if (!TryDetectTarget(attacker, out HitResult hit))
            {
                return;
            }

            FaceToward(hit.ContactPoint);
            if (abilities.TryActivate(_attackAbility, EffectTarget.From(hit.Object)) == AbilityActivationResult.Activated)
            {
                Impacted?.Invoke(HitPoint(hit.ContactPoint));
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

        // The world point where the blow reads: chest height on the near surface of the target, toward
        // the player — so the impact VFX lands on the enemy's body where the punch connects, not at its feet.
        private Vector3 HitPoint(Vector3 targetBase)
        {
            Vector3 toTarget = targetBase - transform.position;
            toTarget.y = 0f;
            Vector3 dir = toTarget.sqrMagnitude > 1e-4f ? toTarget.normalized : transform.forward;
            return targetBase + Vector3.up * _hitHeight - dir * _hitInset;
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
