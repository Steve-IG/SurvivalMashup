using System.Collections.Generic;
using UnityEngine;

namespace ToyChest.Gameplay.Combat
{
    /// <summary>
    /// The phase the active combo step is in. Wind-up and Recovery are the <em>committed</em> phases (the
    /// attack owns the character); Release is the uncommitted grace after the attack's motion has ended,
    /// during which the player is free to move and a press still continues the combo.
    /// </summary>
    public enum ComboPhase
    {
        Idle,
        WindUp,
        Recovery,
        Release
    }

    /// <summary>
    /// The pure timing of one combo step, separated from any authored asset so the sequencer is testable
    /// without a scene and knows nothing about abilities, animations, or hit volumes — only durations.
    /// This is what keeps the combo "data describing an ordered sequence", not attack-specific code: the
    /// sequencer sees N of these and advances through them identically whether a step is a punch, a kick,
    /// or a ground slam.
    /// </summary>
    public readonly struct ComboStepTiming
    {
        /// <summary>Anticipation before the blow lands (press → contact frame), in seconds. Fallback for <c>OnAttackContact</c>.</summary>
        public readonly float WindUp;

        /// <summary>
        /// Committed follow-through after the blow, in seconds — the fallback for the clip's
        /// <c>OnAttackEnd</c> event. When the event is authored it ends the commitment sooner.
        /// </summary>
        public readonly float Recovery;

        /// <summary>
        /// Seconds after the commitment ends (the Release grace) during which a press still continues to the
        /// next step. Presses during the committed phases always buffer, so the full continuation window is
        /// the whole attack plus this grace.
        /// </summary>
        public readonly float ContinuationWindow;

        public ComboStepTiming(float windUp, float recovery, float continuationWindow)
        {
            WindUp = Mathf.Max(0f, windUp);
            Recovery = Mathf.Max(0f, recovery);
            ContinuationWindow = Mathf.Max(0f, continuationWindow);
        }
    }

    /// <summary>What happened to the combo during one <see cref="ComboSequencer"/> call.</summary>
    public readonly struct ComboTick
    {
        /// <summary>A step became active this call — the driver fires its animation and raises Attacked.</summary>
        public bool StepStarted { get; }

        /// <summary>The active step reached its contact frame — the driver resolves that step's hit.</summary>
        public bool Impacted { get; }

        /// <summary>The active step's commitment ended this call (Release began); the player is free to move.</summary>
        public bool Released { get; }

        /// <summary>The combo ended this call and control returns to ordinary locomotion.</summary>
        public bool Finished { get; }

        /// <summary>A continuation press was buffered this call (no step started yet). Debug/feedback only.</summary>
        public bool Queued { get; }

        /// <summary>Which step <see cref="StepStarted"/> / <see cref="Impacted"/> / <see cref="Released"/> refers to.</summary>
        public int StepIndex { get; }

        private ComboTick(bool stepStarted, bool impacted, bool released, bool finished, bool queued, int stepIndex)
        {
            StepStarted = stepStarted;
            Impacted = impacted;
            Released = released;
            Finished = finished;
            Queued = queued;
            StepIndex = stepIndex;
        }

        internal static ComboTick Started(int index) => new ComboTick(true, false, false, false, false, index);
        internal static ComboTick Hit(int index) => new ComboTick(false, true, false, false, false, index);
        internal static ComboTick ReleasedAt(int index) => new ComboTick(false, false, true, false, false, index);
        internal static readonly ComboTick Ended = new ComboTick(false, false, false, true, false, -1);
        internal static readonly ComboTick Buffered = new ComboTick(false, false, false, false, true, -1);
        internal static readonly ComboTick None = default;
    }

    /// <summary>
    /// Pure, deterministic sequencing for an ordered attack combo, separated from any MonoBehaviour so it
    /// is testable without a scene (Engine Principle 17). It owns only <em>which step is active and when</em>
    /// — it reads no clock, drives no animation, applies no damage, and holds no attack-specific branches.
    /// The driver (<c>PlayerCombat</c>) maps each step index to that step's authored ability, hit volume,
    /// and animation; this class only decides the ordering and the responsive continuation timing.
    ///
    /// <para>Each step runs <c>WindUp → (contact) → Recovery → (attack end) → Release</c>. Wind-up and Recovery
    /// are <b>committed</b> (<see cref="IsCommitted"/>): the attack owns the character, so the driver roots it
    /// to the clip's root motion. Recovery ends at the clip's <c>OnAttackEnd</c> event (or the recovery timer
    /// as a fallback). Release is the <b>uncommitted grace</b>: the player may move freely, but a press still
    /// continues to the next step for <see cref="ComboStepTiming.ContinuationWindow"/> seconds — so ending
    /// the commitment never ends the combo. A press during the committed phases buffers and chains on the
    /// first tick after contact (no dead time); a press during Release chains immediately. The final step has
    /// nothing to continue to, so its attack end finishes the combo outright.</para>
    /// </summary>
    public sealed class ComboSequencer
    {
        private readonly List<ComboStepTiming> _steps = new List<ComboStepTiming>();

        private int _index = -1;
        private ComboPhase _phase = ComboPhase.Idle;
        private float _phaseTimer;
        private bool _queuedNext;

        /// <summary>Builds a sequencer over the given ordered step timings. The list is copied.</summary>
        public ComboSequencer(IReadOnlyList<ComboStepTiming> steps)
        {
            SetSteps(steps);
        }

        /// <summary>Whether a combo is in progress (any phase other than Idle, including the Release grace).</summary>
        public bool IsActive => _phase != ComboPhase.Idle;

        /// <summary>
        /// Whether the active step currently owns the character (wind-up or recovery). The driver roots the
        /// player to the attack's root motion exactly while this is true; in Release the player is free.
        /// </summary>
        public bool IsCommitted => _phase == ComboPhase.WindUp || _phase == ComboPhase.Recovery;

        /// <summary>The active step index, or -1 when idle.</summary>
        public int StepIndex => _index;

        /// <summary>The active step's phase.</summary>
        public ComboPhase Phase => _phase;

        /// <summary>Number of steps in the combo.</summary>
        public int StepCount => _steps.Count;

        /// <summary>Whether a continuation press is currently buffered.</summary>
        public bool HasQueuedContinuation => _queuedNext;

        /// <summary>
        /// Replaces the step timings (e.g. the driver rebuilt them from a different authored combo). Resets
        /// the sequencer to idle; only valid while not mid-combo in practice, but always leaves clean state.
        /// </summary>
        public void SetSteps(IReadOnlyList<ComboStepTiming> steps)
        {
            _steps.Clear();
            if (steps != null)
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    _steps.Add(steps[i]);
                }
            }

            Cancel();
        }

        /// <summary>
        /// The attack-press entry point. Idle: begins step 0. Committed (wind-up / recovery) with a next step:
        /// buffers the continuation (<see cref="ComboTick.Queued"/>). Release with a next step: continues to it
        /// immediately (<see cref="ComboTick.StepStarted"/>). Otherwise the press is not consumed (neutral tick).
        /// </summary>
        public ComboTick Press()
        {
            if (_phase == ComboPhase.Idle)
            {
                if (_steps.Count == 0)
                {
                    return ComboTick.None;
                }

                BeginStep(0);
                return ComboTick.Started(0);
            }

            if (!HasNextStep())
            {
                return ComboTick.None;
            }

            if (_phase == ComboPhase.Release)
            {
                int next = _index + 1;
                BeginStep(next);
                return ComboTick.Started(next);
            }

            _queuedNext = true;
            return ComboTick.Buffered;
        }

        /// <summary>
        /// Advances the active step by <paramref name="deltaTime"/> seconds, reporting the one event that
        /// occurred this tick: the contact frame, a chain to the next step, the commitment ending (recovery
        /// timer fallback), or the combo finishing (Release grace expired). Deterministic for a given sequence
        /// of deltas and presses.
        /// </summary>
        public ComboTick Advance(float deltaTime)
        {
            if (_phase == ComboPhase.Idle)
            {
                return ComboTick.None;
            }

            _phaseTimer -= Mathf.Max(0f, deltaTime);

            switch (_phase)
            {
                case ComboPhase.WindUp:
                    return _phaseTimer <= 0f ? EnterRecovery() : ComboTick.None;

                case ComboPhase.Recovery:
                    // A buffered continuation chains on the first tick after contact — no dead time.
                    if (_queuedNext && HasNextStep())
                    {
                        int next = _index + 1;
                        BeginStep(next);
                        return ComboTick.Started(next);
                    }

                    // No OnAttackEnd event arrived in time: the recovery timer ends the commitment instead.
                    return _phaseTimer <= 0f ? EnterRelease() : ComboTick.None;

                default: // Release: the grace ran out with no continuation.
                    if (_phaseTimer <= 0f)
                    {
                        Cancel();
                        return ComboTick.Ended;
                    }

                    return ComboTick.None;
            }
        }

        /// <summary>
        /// Ends the active step's wind-up immediately because its attack animation reached its authored
        /// contact frame (an <c>OnAttackContact</c> event), landing the blow on the animation instead of
        /// the wind-up timer. Returns the impact tick when it was winding up; a neutral tick otherwise, so
        /// a stray or repeated event never lands a second blow. Chaining still happens on the next
        /// <see cref="Advance"/>, exactly as with the timer path.
        /// </summary>
        public ComboTick NotifyContact()
        {
            if (_phase != ComboPhase.WindUp)
            {
                return ComboTick.None;
            }

            return EnterRecovery();
        }

        /// <summary>
        /// Ends the active step's commitment because its animation reached the authored <c>OnAttackEnd</c>
        /// frame. A buffered continuation chains to the next step; otherwise the step enters Release — the
        /// player is free, but the combo stays open for the continuation window (the final step finishes the
        /// combo, having nothing to continue to). Only acts while recovering, so a stray event is harmless.
        /// </summary>
        public ComboTick NotifyAttackEnd()
        {
            if (_phase != ComboPhase.Recovery)
            {
                return ComboTick.None;
            }

            if (_queuedNext && HasNextStep())
            {
                int next = _index + 1;
                BeginStep(next);
                return ComboTick.Started(next);
            }

            return EnterRelease();
        }

        /// <summary>Abandons the combo immediately (the player rolled, jumped, or was interrupted).</summary>
        public void Cancel()
        {
            _index = -1;
            _phase = ComboPhase.Idle;
            _phaseTimer = 0f;
            _queuedNext = false;
        }

        private bool HasNextStep() => _index >= 0 && _index + 1 < _steps.Count;

        private ComboTick EnterRecovery()
        {
            int impacted = _index;
            _phase = ComboPhase.Recovery;
            _phaseTimer = _steps[_index].Recovery + Mathf.Max(0f, _phaseTimer); // carry wind-up overshoot
            return ComboTick.Hit(impacted);
        }

        // The commitment ends. With no grace authored (the default) — or nothing left to continue to — the
        // combo finishes right here, so the continuation window closes exactly at OnAttackEnd. A positive
        // ContinuationWindow keeps the step in an uncommitted Release grace where a press still continues it.
        private ComboTick EnterRelease()
        {
            if (!HasNextStep() || _steps[_index].ContinuationWindow <= 0f)
            {
                Cancel();
                return ComboTick.Ended;
            }

            int released = _index;
            _phase = ComboPhase.Release;
            _phaseTimer = _steps[_index].ContinuationWindow;
            return ComboTick.ReleasedAt(released);
        }

        private void BeginStep(int index)
        {
            _index = index;
            _phase = ComboPhase.WindUp;
            _phaseTimer = _steps[index].WindUp;
            _queuedNext = false;
        }
    }
}
