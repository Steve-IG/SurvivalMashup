using System;
using System.Collections.Generic;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Combat;
using ToyChest.Gameplay.HitDetection;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.GameplayEffects;
using UnityEngine;

namespace ToyChest.Gameplay.Player
{
    /// <summary>
    /// The thin Unity adapter for the player's melee attacks. A press drives an ordered combo (the pure
    /// <see cref="ComboSequencer"/>): each step is a phased attack — <c>wind-up → contact → recovery</c> —
    /// so the blow lands on the animation's contact frame, and a continuation press chains to the next step
    /// with no dead time. When no <see cref="ComboDefinition"/> is authored the adapter degrades to a single
    /// swing built from its inline fields, so existing content keeps working unchanged.
    ///
    /// <para>Every step is <b>authored data</b>, never attack-specific code: which ability it activates,
    /// which reusable <see cref="HitVolume"/>(s) it sweeps, which bone they anchor to, and which animation
    /// trigger it plays all come from the <see cref="ComboStep"/>. Because a step is just data, a punch, a
    /// kick, or an area-of-effect slam occupy combo slots identically — this adapter never branches on
    /// attack type. Which GameplayObjects a step hits is decided by the canonical hit-detection vocabulary
    /// (a <see cref="HitVolumeEmitter"/> resolved by the shared <see cref="HitDetector"/> at the contact
    /// frame); a single step can hit several targets and applies its ability to all of them through the
    /// Ability System's Multiple Targets path. Hit detection answers <em>what was hit</em>; the ability's
    /// Gameplay Effects answer <em>what happens</em>; the two never mix.</para>
    ///
    /// It owns no gameplay rules: damage and effects are the abilities', combo cadence is the sequencer's
    /// step timing, and whether a step lands is the Ability System's decision. The input adapter calls
    /// <see cref="TryAttack"/>; this component never reads the input device. Presentation (animation, VFX,
    /// camera) hangs off the <see cref="Attacked"/> and <see cref="Impacted"/> events.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameplayObjectBehaviour))]
    public sealed class PlayerCombat : MonoBehaviour, IAttackContactReceiver, IAbilityAttackPerformer
    {
        [SerializeField]
        [Tooltip("The behaviour whose composed object is the attacker. Defaults to the sibling.")]
        private GameplayObjectBehaviour _behaviour;

        [Header("Combo")]
        [SerializeField]
        [Tooltip("Authored attack combo. When set, a press drives this ordered sequence; when empty, the " +
                 "adapter falls back to a single swing built from the fields below.")]
        private ComboDefinition _combo;

        [Header("Single-swing fallback (used only when no Combo is assigned)")]
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

        [Header("Swing phases (fallback feel; tuned to the attack clip's contact frame)")]
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
        private StepRuntime[] _steps;
        private ComboSequencer _sequencer;
        private int _activeStep = -1;
        private float _bufferTimer;
        private int _lastTargetsHit;
        private Vector3 _lastContactPoint;
        private bool _hasContactPoint;

        // A loadout swap requested while a combo is in progress, applied the moment that combo ends or is
        // cancelled so the strike in flight is never cut short (see SetCombo).
        private ComboDefinition _pendingCombo;
        private bool _hasPendingCombo;

        // Reused across steps so repeated hit resolution stays allocation-free (the emitters' detector is
        // pooled; these hold the deduped, ordered target set and its capability views for one contact frame).
        private readonly List<HitResult> _resolvedHits = new List<HitResult>();
        private readonly List<EffectTarget> _targetBuffer = new List<EffectTarget>();

        // Targets already struck by the current contact — an instant OnAttackContact, or a whole
        // OnAttackContactStart..End window — so a sweeping attack hits each target once, not once per frame.
        private readonly HashSet<GameplayObject> _struckThisContact = new HashSet<GameplayObject>();
        private bool _windowOpen;
        private int _windowStep = -1;

        // Attack-asset steps: the character's limb strike shapes, the animator their windows are timed against,
        // the per-window hit-instance record, and a scratch list of this frame's open windows.
        private CharacterStrikeProfile _strikeProfile;
        private Animator _animator;
        private readonly HitWindowLedger _ledger = new HitWindowLedger();
        private readonly List<int> _openWindows = new List<int>();

        // An ability attack (a one-off authored attack started by an ability activation, see StartAbilityAttack)
        // borrows the step pipeline: its runtime replaces the combo's while it plays, and the combo's is parked
        // here and restored the moment the ability attack ends or is cancelled. Each attack is bound once.
        private ComboDefinition _abilityAttack;
        private StepRuntime[] _parkedSteps;
        private ComboSequencer _parkedSequencer;
        private readonly Dictionary<ComboDefinition, StepRuntime[]> _abilityAttackSteps =
            new Dictionary<ComboDefinition, StepRuntime[]>();

        /// <summary>
        /// Stage 1 — raised when a step starts, carrying that step's animator trigger so presentation plays
        /// the right attack clip. It is <b>not</b> impact feedback: it fires on every step start, hit or miss.
        /// </summary>
        public event Action<string> Attacked;

        /// <summary>
        /// Stage 2 — raised when the active step reaches its contact frame, whether or not anything was hit.
        /// For debug tooling and readability only; never wire impact juice to this.
        /// </summary>
        public event Action Contacted;

        /// <summary>
        /// Stage 3 — raised <b>once per confirmed target</b> (the ability committed and damage landed),
        /// carrying that target's real world-space contact point. All impact presentation — camera impulse,
        /// impact VFX, hit-stop, impact sound — hangs off this and this alone, so a swing into empty air
        /// produces no impact feedback and a two-target blow produces feedback on both bodies.
        /// </summary>
        public event Action<Vector3> Impacted;

        /// <summary>
        /// Raised alongside <see cref="Impacted"/> when an attack-asset hit window strikes a target, carrying the
        /// window's id ("Left kick", "Slam"). For debug tooling and verification — which window landed the hit.
        /// </summary>
        public event Action<string> WindowStruck;

        /// <summary>The authored combo the attack input drives (null = the single-swing fallback).</summary>
        public ComboDefinition Combo => _combo;

        /// <summary>Whether a <see cref="SetCombo"/> made mid-attack is waiting for the current combo to end.</summary>
        public bool HasPendingCombo => _hasPendingCombo;

        /// <summary>The combo that becomes active when the current one ends (meaningful only with <see cref="HasPendingCombo"/>).</summary>
        public ComboDefinition PendingCombo => _pendingCombo;

        /// <summary>Whether a combo/swing is currently in progress.</summary>
        public bool IsAttacking => _sequencer != null && _sequencer.IsActive;

        /// <summary>
        /// Whether the active attack currently owns the character's movement (wind-up through its
        /// <c>OnAttackEnd</c>). Root motion is consumed and the Animator holds the attack clip only while this
        /// is true; in the Release grace the player is free even though the combo can still continue.
        /// </summary>
        public bool IsMovementCommitted => _sequencer != null && _sequencer.IsCommitted;

        /// <summary>Whether the attack in progress is an ability attack (see <see cref="StartAbilityAttack"/>), not the combo.</summary>
        public bool IsPerformingAbilityAttack => _abilityAttack != null;

        /// <summary>The active step's phase (Idle / WindUp / Recovery). Debug tooling reads this.</summary>
        public ComboPhase Phase => _sequencer != null ? _sequencer.Phase : ComboPhase.Idle;

        /// <summary>The active combo step index, or -1 when idle. Debug only.</summary>
        public int ComboStepIndex => _activeStep;

        /// <summary>Targets confirmed hit by the most recent contact frame (0 on a whiff). Debug only.</summary>
        public int LastTargetsHit => _lastTargetsHit;

        /// <summary>The most recent confirmed contact point, and whether one has been recorded. Debug only.</summary>
        public bool TryGetLastContactPoint(out Vector3 point)
        {
            point = _lastContactPoint;
            return _hasContactPoint;
        }

        /// <summary>The active step's primary hit region (step 0's when idle), for debug visualization. Never gameplay.</summary>
        public HitVolumeEmitter PrimaryAttackVolume
        {
            get
            {
                if (_steps == null || _steps.Length == 0)
                {
                    return null;
                }

                int index = _activeStep >= 0 && _activeStep < _steps.Length ? _activeStep : 0;
                return _steps[index].Primary;
            }
        }

        /// <summary>Remaining cooldown on the fallback attack ability, or 0 when ready. Debug read-only.</summary>
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

            Animator animator = GetComponentInChildren<Animator>();
            _animator = animator;

            // Limb strike shapes for attack-asset windows. A character without a profile gets one generated from
            // its humanoid avatar, so any humanoid can use limb-based attacks with zero setup.
            _strikeProfile = GetComponent<CharacterStrikeProfile>();
            if (_strikeProfile == null)
            {
                _strikeProfile = gameObject.AddComponent<CharacterStrikeProfile>();
            }

            _steps = BuildSteps(_combo, animator);
            _sequencer = new ComboSequencer(BuildTimings());
        }

        // Attack-asset windows are swept after animation has posed the limbs this frame.
        private void LateUpdate()
        {
            if (_sequencer != null && _sequencer.IsActive)
            {
                TickAttackAsset();
            }
        }

        private void OnEnable()
        {
            if (_locomotion != null)
            {
                _locomotion.Jumped += OnMovementAction;
                _locomotion.Rolled += OnMovementAction;
            }
        }

        private void OnDisable()
        {
            if (_locomotion != null)
            {
                _locomotion.Jumped -= OnMovementAction;
                _locomotion.Rolled -= OnMovementAction;
                _locomotion.SetMovementCommitted(false, 1f);
                _locomotion.SetAerialHold(false);
            }
        }

        /// <summary>
        /// Builds the per-step runtime — either from an authored combo (each step's ability, hit volume(s),
        /// anchor, and animation trigger) or, with no combo assigned, a single synthetic step from the
        /// inline fallback fields. This is the whole authoring workflow resolved to runtime form: the
        /// adapter holds no attack-specific data of its own beyond the fallback.
        /// </summary>
        private StepRuntime[] BuildSteps(ComboDefinition combo, Animator animator)
        {
            if (combo != null && combo.StepCount > 0)
            {
                IReadOnlyList<ComboStep> authored = combo.Steps;
                var steps = new StepRuntime[authored.Count];
                for (int i = 0; i < authored.Count; i++)
                {
                    ComboStep step = authored[i];
                    HitVolumeEmitter primary = step.BuildPrimaryEmitter(_enemyTag, _targetLayers);
                    primary.Bind(transform, animator);
                    HitVolumeEmitter[] extras = BindExtras(step.ExtraVolumes, animator);
                    steps[i] = new StepRuntime(
                        new DefinitionId(step.AttackAbilityId), primary, extras,
                        step.AnimatorTrigger, step.WindUpDuration, step.Timing.Recovery);
                    BindAttack(steps[i], step, animator);
                }

                return steps;
            }

            // Fallback: a single swing authored inline, anchored to the hand bone (falls back to the body
            // for an unrigged object), preserving the pre-combo behaviour so existing prefabs are unchanged.
            HitVolume volume = _attackVolumePreset != null
                ? _attackVolumePreset.Volume
                : new HitVolume(HitShape.Cone, _attackRange, _attackConeHalfAngle, multiTarget: false, maxTargets: 1);
            var anchor = new HitVolumeAnchor(HitAnchorSpace.HumanoidBone, _attackBone, _attackOffset, HitFacing.Owner);
            var fallbackPrimary = new HitVolumeEmitter(volume, anchor, _enemyTag, _targetLayers);
            fallbackPrimary.Bind(transform, animator);
            if (_extraContactVolumes != null)
            {
                for (int i = 0; i < _extraContactVolumes.Length; i++)
                {
                    _extraContactVolumes[i]?.Bind(transform, animator);
                }
            }

            return new[]
            {
                new StepRuntime(_attackAbility, fallbackPrimary, _extraContactVolumes, "Attack", _windUpDuration, _recoveryDuration),
            };
        }

        // Resolve a step's authored attack asset (if any) to runtime form: the animator state its time is read
        // from, the ability each window applies, and a bound emitter for any window that carries an area.
        private void BindAttack(StepRuntime runtime, ComboStep step, Animator animator)
        {
            AttackDefinition attack = step.Attack;
            if (attack == null)
            {
                return;
            }

            runtime.Attack = attack;
            runtime.Timeline = new AttackTimeline(attack);
            runtime.StateHash = Animator.StringToHash(step.AnimatorState);
            if (animator != null && animator.runtimeAnimatorController != null && !animator.HasState(0, runtime.StateHash))
            {
                Debug.LogWarning(
                    $"[PlayerCombat] Attack '{attack.name}' reads time from animator state '{step.AnimatorState}', " +
                    "which the controller does not have — its hit windows will not fire.", this);
            }

            IReadOnlyList<HitWindow> windows = attack.Windows;
            runtime.WindowAbilities = new DefinitionId[windows.Count];
            runtime.WindowAreas = new HitVolumeEmitter[windows.Count];
            for (int w = 0; w < windows.Count; w++)
            {
                HitWindow window = windows[w];
                string abilityId = !string.IsNullOrEmpty(window.AbilityId) ? window.AbilityId
                    : !string.IsNullOrEmpty(attack.AbilityId) ? attack.AbilityId
                    : step.AttackAbilityId;
                runtime.WindowAbilities[w] = new DefinitionId(abilityId);

                if (window.Area != null)
                {
                    var anchor = new HitVolumeAnchor(HitAnchorSpace.HumanoidBone, window.AreaBone, Vector3.zero, HitFacing.Owner);
                    var area = new HitVolumeEmitter(window.Area.Volume, anchor, _enemyTag, _targetLayers);
                    area.Bind(transform, animator);
                    runtime.WindowAreas[w] = area;
                }
            }
        }

        private HitVolumeEmitter[] BindExtras(IReadOnlyList<HitVolumeEmitter> extras, Animator animator)
        {
            if (extras == null || extras.Count == 0)
            {
                return Array.Empty<HitVolumeEmitter>();
            }

            var bound = new HitVolumeEmitter[extras.Count];
            for (int i = 0; i < extras.Count; i++)
            {
                bound[i] = extras[i];
                bound[i]?.Bind(transform, animator);
            }

            return bound;
        }

        private List<ComboStepTiming> BuildTimings()
        {
            if (_combo != null && _combo.StepCount > 0)
            {
                return _combo.BuildTimings();
            }

            // The fallback single swing has no continuation window — it is one step that recovers and ends.
            return new List<ComboStepTiming> { new ComboStepTiming(_windUpDuration, _recoveryDuration, 0f) };
        }

        private void Update()
        {
            float delta = Time.deltaTime;

            if (_sequencer.IsActive)
            {
                // A combo owns the frame: advance its timing and act on the one event this tick produced, then
                // sweep any open contact window (closed automatically if that tick changed or ended the step).
                Handle(_sequencer.Advance(delta));
                TickContactWindow();

                // Past the attack's cancel point the recovery tail is optional: steering away ends it and the
                // animator blends straight into locomotion.
                if (InCancelableRecovery() && _locomotion != null && _locomotion.MoveIntentDirection.sqrMagnitude > 1e-4f)
                {
                    CancelCombo();
                }

                // A press the live combo could not take yet (the finisher, before its cancel point) fires as
                // soon as it can.
                if (_bufferTimer > 0f)
                {
                    _bufferTimer -= delta;
                    if (TryPress())
                    {
                        _bufferTimer = 0f;
                    }
                }

                return;
            }

            _windowOpen = false;
            ApplyPendingCombo();
            SyncCommitment();

            if (_bufferTimer > 0f)
            {
                _bufferTimer -= delta;
                if (TryPress())
                {
                    _bufferTimer = 0f;
                }
            }
        }

        /// <summary>
        /// Swaps the authored combo the attack input drives (a loadout change — e.g. the dojo's test-combo
        /// selector). When idle it applies at once; while a combo is in progress the swap is <b>deferred</b> until
        /// that combo finishes or is cancelled, so the strike in flight completes intact (the last request wins;
        /// re-selecting the current combo clears a pending swap). The new combo's steps are bound exactly as at
        /// startup, so it plays through the same pipeline. Null restores the single-swing fallback.
        /// </summary>
        public void SetCombo(ComboDefinition combo)
        {
            if (_sequencer != null && _sequencer.IsActive)
            {
                _pendingCombo = combo;
                _hasPendingCombo = combo != _combo;
                return;
            }

            ApplyCombo(combo);
        }

        private void ApplyCombo(ComboDefinition combo)
        {
            _pendingCombo = null;
            _hasPendingCombo = false;
            if (combo == _combo && _steps != null)
            {
                return;
            }

            _combo = combo;
            _bufferTimer = 0f;
            _steps = BuildSteps(_combo, _animator);
            _sequencer = new ComboSequencer(BuildTimings());
        }

        /// <summary>
        /// Whether <see cref="StartAbilityAttack"/> could start <paramref name="attack"/> right now: grounded and
        /// not rolling (airborne only when the attack's first step is aerial, as for a combo opener), and either
        /// idle or past the current attack's cancel point. A press the caller cannot start now is the caller's to
        /// buffer — exactly as <see cref="TryAttack"/> buffers a melee press.
        /// </summary>
        public bool CanStartAbilityAttack(ComboDefinition attack)
        {
            if (attack == null || attack.StepCount == 0 || _sequencer == null)
            {
                return false;
            }

            AttackDefinition opener = attack.Steps[0].Attack;
            bool aerialOpener = opener != null && opener.IsAerial;
            if (_locomotion != null && (_locomotion.IsRolling || (!_locomotion.IsGrounded && !aerialOpener)))
            {
                return false;
            }

            if (_sequencer.IsCommitted && !InCancelableRecovery())
            {
                return false;
            }

            GameplayObject attacker = _behaviour != null ? _behaviour.Object : null;
            return attacker != null && attacker.IsActive;
        }

        /// <summary>
        /// Plays an ability's authored attack — a <see cref="ComboDefinition"/>, typically one step — through the same
        /// step pipeline as the combo: its animation, its hit windows and areas, and its impact ability applied to every
        /// target through the Multiple Targets path, with <see cref="Attacked"/>/<see cref="Contacted"/>/<see cref="Impacted"/>
        /// raised as for any step. The caller has already committed the ability's activation (cost and cooldown); this
        /// only performs it, so it never consults a cooldown. Any combo in progress is cancelled (the caller checked
        /// <see cref="CanStartAbilityAttack"/>, so it is past its cancel point) and a buffered melee press is dropped.
        /// Returns false, starting nothing, when the attack cannot start.
        /// </summary>
        public bool StartAbilityAttack(ComboDefinition attack)
        {
            if (!CanStartAbilityAttack(attack))
            {
                return false;
            }

            if (_sequencer.IsActive)
            {
                CancelCombo();
            }

            ApplyPendingCombo();

            if (!_abilityAttackSteps.TryGetValue(attack, out StepRuntime[] steps))
            {
                steps = BuildSteps(attack, _animator);
                _abilityAttackSteps.Add(attack, steps);
            }

            _parkedSteps = _steps;
            _parkedSequencer = _sequencer;
            _abilityAttack = attack;
            _steps = steps;
            _sequencer = new ComboSequencer(attack.BuildTimings());
            _bufferTimer = 0f;

            Handle(_sequencer.Press());
            return true;
        }

        // The ability attack is over (finished or cancelled): hand the step pipeline back to the combo.
        private void EndAbilityAttack()
        {
            if (_abilityAttack == null)
            {
                return;
            }

            _steps = _parkedSteps;
            _sequencer = _parkedSequencer;
            _parkedSteps = null;
            _parkedSequencer = null;
            _abilityAttack = null;
        }

        // Called at idle points only (never mid-tick, so no step runtime is rebuilt while it is being swept): a
        // swap requested mid-attack lands once that combo has finished or been cancelled.
        private void ApplyPendingCombo()
        {
            if (_hasPendingCombo)
            {
                ApplyCombo(_pendingCombo);
            }
        }

        /// <summary>
        /// The attack input entrypoint. Starts (or continues) the combo immediately if able; otherwise
        /// remembers the press for a short buffer window (<see cref="_bufferWindow"/>) so a press landing a
        /// hair before the combo can begin (mid-recovery of a previous combo, or just before a cooldown
        /// ends) still connects — the same responsiveness the continuation window gives mid-combo.
        /// </summary>
        public void TryAttack()
        {
            if (!TryPress())
            {
                _bufferTimer = _bufferWindow;
            }
        }

        /// <summary>
        /// Called by the attack animation's contact-frame event (an authored <c>OnAttackContact</c> event,
        /// relayed by <see cref="AttackContactRelay"/>) to land the active step's blow exactly when it
        /// connects rather than on the wind-up timer. Safe to leave unwired — the wind-up timer lands the
        /// blow instead.
        /// </summary>
        public void NotifyAnimationContact()
        {
            // An attack-asset step owns its own timing; a stray clip event must not double it.
            if (_sequencer != null && !ActiveStepUsesAttack())
            {
                Handle(_sequencer.NotifyContact());
            }
        }

        /// <summary>
        /// Called by the attack animation's authored <c>OnAttackEnd</c> event (relayed by
        /// <see cref="AttackContactRelay"/>) when the committed motion is done. It ends the current step at
        /// the animation's own pace — chaining if the player buffered a continuation, otherwise finishing
        /// the combo and releasing the movement commitment now — so the exit into the next action is as
        /// snappy as the clip allows. Safe to leave unwired: without the event the recovery timer ends the
        /// step instead (the conservative fallback).
        /// </summary>
        public void NotifyAttackEnd()
        {
            if (_sequencer != null && !ActiveStepUsesAttack())
            {
                Handle(_sequencer.NotifyAttackEnd());
            }
        }

        /// <summary>
        /// Presses the combo. When active, the press feeds the sequencer (buffering a continuation if within
        /// a window) and is always consumed — a live combo never buffers a fresh one. When idle, the first
        /// step is gated exactly as before: never mid-roll or airborne, and — when a live target sits in the
        /// arc — only if the step's ability could actually land, so a fighting player's cadence stays honest
        /// (an off-cadence press buffers instead). A whiff with no target in range always starts, so
        /// attacking in the open never feels dead. Returns whether the press was consumed.
        /// </summary>
        private bool TryPress()
        {
            if (_sequencer.IsActive)
            {
                bool hasNext = _sequencer.StepIndex + 1 < _sequencer.StepCount;
                if (hasNext)
                {
                    // Mid-combo: buffers a continuation while committed, or continues straight to the next step
                    // during the Release grace.
                    Handle(_sequencer.Press());
                    return true;
                }

                // The last step has nothing to continue to. Past its cancel point a press cuts the recovery
                // short and starts the combo over; before it the press is held (TryAttack buffers it).
                if (!InCancelableRecovery())
                {
                    return false;
                }

                CancelCombo();
            }

            // A fresh combo always starts on the latest selection, even when the previous combo ended this frame.
            ApplyPendingCombo();

            if (!CanStartCombo(out GameplayObject attacker, out AbilitySet abilities))
            {
                return false;
            }

            StepRuntime first = _steps[0];
            if (CollectHits(first, attacker) &&
                abilities.CanActivate(first.Ability, EffectTarget.From(_resolvedHits[0].Object)) != AbilityActivationResult.Activated)
            {
                return false;
            }

            Handle(_sequencer.Press());
            return true;
        }

        private bool CanStartCombo(out GameplayObject attacker, out AbilitySet abilities)
        {
            attacker = null;
            abilities = null;

            // A jump or a roll owns the character until it finishes: an attack must never cut one short. The
            // press is not thrown away — TryAttack buffers it, so it fires as soon as the player lands or the
            // roll recovers, which keeps mashing responsive without stealing control from movement.
            // An aerial opener (its first step flies) may also start in the air, so a jump can open an air string.
            bool aerialOpener = _steps != null && _steps.Length > 0 && _steps[0].Attack != null && _steps[0].Attack.IsAerial;
            if (_locomotion != null && (_locomotion.IsRolling || (!_locomotion.IsGrounded && !aerialOpener)))
            {
                return false;
            }

            attacker = _behaviour != null ? _behaviour.Object : null;
            return attacker != null && attacker.IsActive && attacker.TryGet(out abilities);
        }

        // Route one sequencer event to the matching side effect. Exactly one of these fires per tick.
        private void Handle(ComboTick tick)
        {
            if (tick.StepStarted)
            {
                OnStepStarted(tick.StepIndex);
            }
            else if (tick.Impacted)
            {
                OnStepImpact(tick.StepIndex);
            }
            else if (tick.Finished)
            {
                _activeStep = -1;
                EndAbilityAttack();
            }

            SyncCommitment();
        }

        // The attack's movement commitment is a held state mirrored from the sequencer every time it changes
        // and every frame: rooted while the step is committed (wind-up → OnAttackEnd), free in the Release
        // grace and when idle. Because it is never a timer it cannot outlive the attack — no leftover rooting
        // after the leap, a roll, a jump, or a cancelled combo.
        private void SyncCommitment()
        {
            bool committed = _sequencer != null && _sequencer.IsCommitted;
            _locomotion?.SetMovementCommitted(committed, _commitMoveScale);
            _locomotion?.SetAerialHold(committed && ActiveStepIsAerial());
        }

        // Whether the active step is an aerial attack (its asset carries a height profile the controller follows).
        private bool ActiveStepIsAerial()
        {
            int index = _sequencer != null ? _sequencer.StepIndex : -1;
            return _steps != null && index >= 0 && index < _steps.Length && _steps[index].Attack != null && _steps[index].Attack.IsAerial;
        }

        // A roll or jump is a different action: it ends the combo outright (so a later press cannot resume
        // the string mid-air or mid-roll) and drops the commitment so the move owns the character cleanly.
        private void OnMovementAction()
        {
            if (_sequencer == null || !_sequencer.IsActive)
            {
                return;
            }

            CancelCombo();
        }

        private void CancelCombo()
        {
            _sequencer.Cancel();
            _activeStep = -1;
            _windowOpen = false;
            EndAbilityAttack();
            SyncCommitment();
        }

        // Whether the active attack-asset step is in its recovery tail past the attack's cancel point.
        private bool InCancelableRecovery()
        {
            if (_sequencer == null || _sequencer.Phase != ComboPhase.Recovery)
            {
                return false;
            }

            int index = _sequencer.StepIndex;
            StepRuntime step = index >= 0 && index < _steps.Length ? _steps[index] : null;
            return step?.Attack != null && step.Timeline.IsCancelable;
        }

        /// <summary>
        /// A step became active: commit its facing, root the player for its wind-up plus a brief beat past
        /// contact, and raise <see cref="Attacked"/> with the step's animator trigger so presentation plays
        /// the matching clip. Damage is not applied here — it lands at the step's contact frame.
        /// </summary>
        private void OnStepStarted(int index)
        {
            _activeStep = index;
            StepRuntime step = _steps[index];

            // An attack-asset step starts its clip timeline from zero with a fresh hit-instance record.
            if (step.Attack != null)
            {
                step.Timeline.Restart();
                _ledger.Clear();
                _lastTargetsHit = 0;
                step.StaleTime = -1f;
                if (_animator != null && _animator.isActiveAndEnabled)
                {
                    AnimatorStateInfo current = _animator.GetCurrentAnimatorStateInfo(0);
                    if (current.shortNameHash == step.StateHash)
                    {
                        step.StaleTime = current.normalizedTime;
                    }
                }
            }

            // Commit the step's direction instantly. The player's stick wins: attacking while holding a
            // direction strikes that way immediately (including a reversal), which is what keeps chained
            // steps from barely rotating. With no movement input we snap to the nearest target (aim assist
            // for a standing attack); with neither we keep the current facing.
            Vector3 intent = _locomotion != null ? _locomotion.MoveIntentDirection : Vector3.zero;
            if (intent.sqrMagnitude > 1e-4f)
            {
                SnapFacing(intent);
            }
            else
            {
                GameplayObject attacker = _behaviour != null ? _behaviour.Object : null;
                if (attacker != null && attacker.IsActive && CollectHits(step, attacker))
                {
                    SnapFacing(_resolvedHits[0].ContactPoint - transform.position);
                }
            }

            // Rooting is not set here: it is a held state synced from the sequencer (see SyncCommitment), so
            // the character moves only by the attack's root motion for exactly as long as the step is
            // committed, and never a frame longer.
            Attacked?.Invoke(step.AnimatorTrigger);
        }

        /// <summary>
        /// The active step's contact frame — the middle of the three stages: <c>Attacked</c> (step started)
        /// → <c>Contacted</c> (contact frame arrived) → <c>Impacted</c> (a hit was confirmed). It re-resolves
        /// every valid target in the step's contact volume(s) and activates the step's ability against
        /// <em>all</em> of them in one activation (the Ability System's Multiple Targets path), so one blow
        /// applies its authored damage to every enemy legitimately inside the arc while paying one cost and
        /// one cooldown. <see cref="Impacted"/> is raised once per confirmed target, each with its own
        /// surface contact point; a swing into empty air reaches <see cref="Contacted"/> and stops there.
        /// </summary>
        private void OnStepImpact(int index)
        {
            _activeStep = index;
            _struckThisContact.Clear();
            if (_steps[index].Attack == null)
            {
                // (An attack-asset step reaches "contact" when its windows close; its hits already counted.)
                _lastTargetsHit = 0;
            }

            Contacted?.Invoke();

            // An attack-asset step deals its damage through its hit windows (see TickAttackAsset), not through
            // the step's single hit region.
            if (_steps[index].Attack != null)
            {
                return;
            }

            Strike(index, reaimAtTarget: true);
        }

        // Resolve the step's hit volume(s) now and apply its ability to every target found that has not
        // already been struck during this contact (an instant contact, or the whole of a contact window).
        // Instant contacts strike once; a window calls this every frame, so the "once per target" rule is what
        // turns a sweeping flip kick into one hit per enemy rather than a hit per frame.
        private void Strike(int index, bool reaimAtTarget)
        {
            GameplayObject attacker = _behaviour != null ? _behaviour.Object : null;
            if (attacker == null || !attacker.IsActive || !attacker.TryGet(out AbilitySet abilities))
            {
                return;
            }

            StepRuntime step = _steps[index];
            if (!CollectHits(step, attacker))
            {
                return;
            }

            // Keep only targets this contact has not struck yet (order preserved: nearest first).
            for (int i = _resolvedHits.Count - 1; i >= 0; i--)
            {
                if (_struckThisContact.Contains(_resolvedHits[i].Object))
                {
                    _resolvedHits.RemoveAt(i);
                }
            }

            if (_resolvedHits.Count == 0)
            {
                return;
            }

            // Only re-aim (toward the nearest struck target) if the player is not steering: while a direction
            // is held, the blow stays committed to the player's intent rather than being pulled to an enemy.
            if (reaimAtTarget && (_locomotion == null || _locomotion.MoveIntentDirection.sqrMagnitude <= 1e-4f))
            {
                FaceToward(_resolvedHits[0].ContactPoint);
            }

            _targetBuffer.Clear();
            for (int i = 0; i < _resolvedHits.Count; i++)
            {
                _targetBuffer.Add(EffectTarget.From(_resolvedHits[i].Object));
            }

            // One activation, many receivers: the ability commits once (one cost, one cooldown) and applies
            // its effect sequence to each target. Detection decided who was hit; the ability decides what
            // happens — the two never mix, even for a multi-target blow.
            if (abilities.TryActivate(step.Ability, _targetBuffer) == AbilityActivationResult.Activated)
            {
                _lastTargetsHit += _resolvedHits.Count;
                _hasContactPoint = true;
                for (int i = 0; i < _resolvedHits.Count; i++)
                {
                    _struckThisContact.Add(_resolvedHits[i].Object);

                    // Per-target impact: each confirmed hit reports its own contact point, so VFX and juice
                    // appear on every struck body rather than only the first.
                    _lastContactPoint = _resolvedHits[i].ContactPoint;
                    Impacted?.Invoke(_resolvedHits[i].ContactPoint);
                }
            }
        }

        /// <summary>
        /// Opens a contact window (the clip's <c>OnAttackContactStart</c>): counts as the step's contact if it
        /// is still winding up, then keeps its hit volume live every frame until <c>OnAttackContactEnd</c>,
        /// striking each target at most once. If the wind-up timer already landed the contact this frame, the
        /// window simply opens for the current step and continues its once-per-target bookkeeping.
        /// </summary>
        public void NotifyContactWindowStart()
        {
            if (_sequencer == null || !_sequencer.IsCommitted || ActiveStepUsesAttack())
            {
                return;
            }

            if (_sequencer.Phase == ComboPhase.WindUp)
            {
                Handle(_sequencer.NotifyContact());
            }

            _windowStep = _sequencer.StepIndex;
            _windowOpen = _windowStep >= 0;
        }

        /// <summary>Closes the contact window (the clip's <c>OnAttackContactEnd</c>).</summary>
        public void NotifyContactWindowEnd()
        {
            _windowOpen = false;
        }

        // Sweep an open contact window. It only lives within its own committed step: a chain to the next step,
        // the commitment ending, or a cancelled combo all close it, so it can never strike from a stale attack.
        private void TickContactWindow()
        {
            if (!_windowOpen)
            {
                return;
            }

            if (_sequencer == null || !_sequencer.IsCommitted || _sequencer.StepIndex != _windowStep)
            {
                _windowOpen = false;
                return;
            }

            Strike(_windowStep, reaimAtTarget: false);
        }

        // Whether the active step is driven by an authored attack asset (its windows and timing), in which case
        // the clip's animation events are ignored for it.
        private bool ActiveStepUsesAttack()
        {
            int index = _sequencer != null ? _sequencer.StepIndex : -1;
            return index >= 0 && index < _steps.Length && _steps[index].Attack != null;
        }

        /// <summary>
        /// Drives an attack-asset step for this frame: reads its clip's normalized time from the Animator, lands
        /// the step's contact when the first window opens, sweeps every window open during the frame (each its
        /// own hit instance), and ends the commitment at the asset's commit end. Runs only while the step is
        /// committed, so nothing strikes from the Release grace or after a cancel.
        /// </summary>
        private void TickAttackAsset()
        {
            if (!_sequencer.IsCommitted)
            {
                return;
            }

            int index = _sequencer.StepIndex;
            StepRuntime step = index >= 0 && index < _steps.Length ? _steps[index] : null;
            if (step?.Attack == null || !TryGetStateTime(step, out float normalizedTime))
            {
                return;
            }

            AttackTimelineTick tick = step.Timeline.Advance(normalizedTime);

            // An aerial step's height comes from its authored profile, applied over exactly the span the clip advanced.
            if (step.Attack.IsAerial)
            {
                _locomotion?.ApplyVerticalMotion(step.Attack.AirHeightDelta(step.Timeline.Previous, step.Timeline.Current));
            }
            if (tick.ContactReached && _sequencer.Phase == ComboPhase.WindUp)
            {
                Handle(_sequencer.NotifyContact());
            }

            step.Timeline.OpenWindows(_openWindows);
            for (int i = 0; i < _openWindows.Count; i++)
            {
                StrikeWindow(step, _openWindows[i]);
            }

            if (tick.CommitEnded)
            {
                Handle(_sequencer.NotifyAttackEnd());
            }
        }

        // The normalized time of the step's animator state, preferring the state being transitioned INTO (a step
        // starting while the previous clip blends out — including a fresh combo re-entering the same state).
        // Readings from a previous instance of the same state that is still playing out (see StaleTime) are
        // rejected until the new instance has begun, so a fresh attack never inherits a finished one's time.
        private bool TryGetStateTime(StepRuntime step, out float normalizedTime)
        {
            normalizedTime = 0f;
            if (_animator == null)
            {
                return false;
            }

            if (_animator.IsInTransition(0))
            {
                AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(0);
                if (next.shortNameHash == step.StateHash)
                {
                    step.StaleTime = -1f;
                    normalizedTime = next.normalizedTime;
                    return true;
                }
            }

            AnimatorStateInfo current = _animator.GetCurrentAnimatorStateInfo(0);
            if (current.shortNameHash != step.StateHash)
            {
                return false;
            }

            if (step.StaleTime >= 0f)
            {
                if (current.normalizedTime >= step.StaleTime)
                {
                    return false; // still the old instance
                }

                step.StaleTime = -1f;
            }

            normalizedTime = current.normalizedTime;
            return true;
        }

        /// <summary>
        /// Sweeps one open hit window: every limb it names (as swept capsules from the character's strike
        /// profile) plus its optional area, unioned and deduped, then filtered by the window's own hit-instance
        /// record (once per target, or the re-hit interval). New targets receive the window's ability in one
        /// multi-target activation; <see cref="Impacted"/> fires per target at its real contact point.
        /// </summary>
        private void StrikeWindow(StepRuntime step, int windowIndex)
        {
            GameplayObject attacker = _behaviour != null ? _behaviour.Object : null;
            if (attacker == null || !attacker.IsActive || !attacker.TryGet(out AbilitySet abilities))
            {
                return;
            }

            HitWindow window = step.Attack.Windows[windowIndex];
            var filter = new HitFilter(_enemyTag, _targetLayers);
            _resolvedHits.Clear();

            for (int limb = 0; limb < StrikeLimbs.Count; limb++)
            {
                if (!StrikeLimbs.Contains(window.Limbs, (StrikeLimb)limb) ||
                    !_strikeProfile.TryGetSweep((StrikeLimb)limb, out Vector3 pa, out Vector3 pb, out Vector3 ca, out Vector3 cb, out float radius))
                {
                    continue;
                }

                IReadOnlyList<HitResult> hits = _detector.DetectLimbSweep(pa, pb, ca, cb, radius, filter, attacker);
                for (int h = 0; h < hits.Count; h++)
                {
                    if (!AlreadyResolved(hits[h].Object)) _resolvedHits.Add(hits[h]);
                }
            }

            HitVolumeEmitter area = step.WindowAreas[windowIndex];
            if (area != null)
            {
                IReadOnlyList<HitResult> hits = area.Detect(attacker, _detector);
                for (int h = 0; h < hits.Count; h++)
                {
                    if (!AlreadyResolved(hits[h].Object)) _resolvedHits.Add(hits[h]);
                }
            }

            float now = Time.time;
            for (int i = _resolvedHits.Count - 1; i >= 0; i--)
            {
                if (!_ledger.CanStrike(windowIndex, _resolvedHits[i].Object, now, window.RehitInterval))
                {
                    _resolvedHits.RemoveAt(i);
                }
            }

            if (_resolvedHits.Count == 0)
            {
                return;
            }

            _resolvedHits.Sort(HitResult.Compare);
            _targetBuffer.Clear();
            for (int i = 0; i < _resolvedHits.Count; i++)
            {
                _targetBuffer.Add(EffectTarget.From(_resolvedHits[i].Object));
            }

            if (abilities.TryActivate(step.WindowAbilities[windowIndex], _targetBuffer) == AbilityActivationResult.Activated)
            {
                _lastTargetsHit += _resolvedHits.Count;
                _hasContactPoint = true;
                for (int i = 0; i < _resolvedHits.Count; i++)
                {
                    _ledger.Record(windowIndex, _resolvedHits[i].Object, now);
                    _lastContactPoint = _resolvedHits[i].ContactPoint;
                    WindowStruck?.Invoke(window.Id);
                    Impacted?.Invoke(_resolvedHits[i].ContactPoint);
                }
            }
        }

        // The canonical hit query for one step: resolve its primary contact region plus any multi-volume
        // extras, each anchored to its socket/bone, and gather every live enemy across all of them into a
        // deduped, deterministically ordered set. The same vocabulary the enemy strike and hazards use — no
        // bespoke overlap loop lives here. Returns whether at least one target was found; results are left
        // in <see cref="_resolvedHits"/> (nearest first) for the caller to consume before the next query.
        private bool CollectHits(StepRuntime step, GameplayObject attacker)
        {
            _resolvedHits.Clear();
            AccumulateHits(step.Primary, attacker);

            HitVolumeEmitter[] extras = step.Extras;
            if (extras != null)
            {
                for (int i = 0; i < extras.Length; i++)
                {
                    AccumulateHits(extras[i], attacker);
                }
            }

            // Deterministic nearest-first order across all emitters (the detector orders within one volume;
            // this re-orders the union), so multi-target resolution never depends on emitter or query order.
            _resolvedHits.Sort(HitResult.Compare);
            return _resolvedHits.Count > 0;
        }

        // Copy one emitter's results out of the detector's reused buffer into the running set, skipping any
        // object already found by an earlier emitter — one target is hit once per contact event even when it
        // sits inside two overlapping volumes.
        private void AccumulateHits(HitVolumeEmitter emitter, GameplayObject attacker)
        {
            if (emitter == null)
            {
                return;
            }

            IReadOnlyList<HitResult> hits = emitter.Detect(attacker, _detector);
            for (int i = 0; i < hits.Count; i++)
            {
                HitResult hit = hits[i];
                if (!AlreadyResolved(hit.Object))
                {
                    _resolvedHits.Add(hit);
                }
            }
        }

        private bool AlreadyResolved(GameplayObject candidate)
        {
            for (int i = 0; i < _resolvedHits.Count; i++)
            {
                if (_resolvedHits[i].Object == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Turns to a world direction immediately, with no turn-rate easing — used when a step commits so
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

        // The resolved, bound runtime of one combo step: its ability, its hit regions, its animation trigger,
        // and the timing the adapter needs (the sequencer owns wind-up/recovery/continuation; this keeps the
        // wind-up and recovery, whose sum is the window the player is rooted to the attack's root motion).
        private sealed class StepRuntime
        {
            public StepRuntime(
                DefinitionId ability, HitVolumeEmitter primary, HitVolumeEmitter[] extras,
                string animatorTrigger, float windUp, float recovery)
            {
                Ability = ability;
                Primary = primary;
                Extras = extras;
                AnimatorTrigger = animatorTrigger;
                WindUp = windUp;
                Recovery = recovery;
            }

            public DefinitionId Ability { get; }

            public HitVolumeEmitter Primary { get; }

            public HitVolumeEmitter[] Extras { get; }

            public string AnimatorTrigger { get; }

            public float WindUp { get; }

            public float Recovery { get; }

            // Attack-asset steps only (null otherwise): the authored windows, their timeline, the animator state
            // their time is read from, and each window's resolved ability and optional area emitter.
            public AttackDefinition Attack { get; set; }

            public AttackTimeline Timeline { get; set; }

            public int StateHash { get; set; }

            /// <summary>
            /// When the step starts while a previous instance of its state is still playing out (a fresh combo
            /// pressed right after the last one released), that instance's normalized time at the start; current-
            /// state readings at or past it belong to the old instance and are ignored. -1 = none.
            /// </summary>
            public float StaleTime { get; set; } = -1f;

            public DefinitionId[] WindowAbilities { get; set; }

            public HitVolumeEmitter[] WindowAreas { get; set; }
        }
    }
}
