using UnityEngine;

namespace ToyChest.Gameplay.Player
{
    /// <summary>The phase a melee swing is in. Impact is the instant between wind-up and recovery.</summary>
    public enum MeleePhase
    {
        Idle,
        WindUp,
        Recovery
    }

    /// <summary>What happened to a swing during one <see cref="MeleeSwing.Advance"/> tick.</summary>
    public readonly struct MeleeSwingTick
    {
        /// <summary>The blow connected this tick (wind-up completed) — the moment to apply damage and juice.</summary>
        public bool Impacted { get; }

        /// <summary>The swing fully recovered this tick and control returns to ordinary locomotion.</summary>
        public bool Finished { get; }

        public MeleeSwingTick(bool impacted, bool finished)
        {
            Impacted = impacted;
            Finished = finished;
        }
    }

    /// <summary>
    /// Pure, deterministic timing for one melee swing, separated from any MonoBehaviour so it is testable
    /// without a scene (Engine Principle 17). A swing runs <c>WindUp → (impact) → Recovery → Idle</c>: the
    /// wind-up is the readable anticipation, the impact fires exactly once when the wind-up completes (the
    /// contact frame the damage should land on), and the recovery is the follow-through before control
    /// returns to locomotion. Durations are injected by the caller (authored feel data); this reads no
    /// engine clock and drives no animation — it only reports phase and the impact/finish moments.
    /// </summary>
    public sealed class MeleeSwing
    {
        private float _windUpDuration;
        private float _recoveryDuration;
        private float _phaseTimer;

        /// <summary>The phase the swing is currently in.</summary>
        public MeleePhase Phase { get; private set; } = MeleePhase.Idle;

        /// <summary>Whether a swing is in progress (wind-up or recovery).</summary>
        public bool IsActive => Phase != MeleePhase.Idle;

        /// <summary>Whether the swing is still anticipating (before the blow lands).</summary>
        public bool IsWindingUp => Phase == MeleePhase.WindUp;

        /// <summary>Whether the swing is following through after the blow.</summary>
        public bool IsRecovering => Phase == MeleePhase.Recovery;

        /// <summary>Begins a swing: a wind-up of <paramref name="windUpDuration"/> then a recovery of
        /// <paramref name="recoveryDuration"/> (seconds). Restarts cleanly if already active.</summary>
        public void Begin(float windUpDuration, float recoveryDuration)
        {
            _windUpDuration = Mathf.Max(0f, windUpDuration);
            _recoveryDuration = Mathf.Max(0f, recoveryDuration);
            _phaseTimer = _windUpDuration;
            Phase = MeleePhase.WindUp;
        }

        /// <summary>Abandons the swing immediately (e.g., the player rolled or was interrupted).</summary>
        public void Cancel()
        {
            Phase = MeleePhase.Idle;
            _phaseTimer = 0f;
        }

        /// <summary>
        /// Ends the wind-up immediately because the attack animation reached its contact frame (an
        /// authored animation event), moving straight to recovery. Returns true if this call is what
        /// landed the blow (it was winding up); false if the swing was not winding up, so the caller
        /// applies damage exactly once. Lets impact sync to the animation instead of a fixed timer.
        /// </summary>
        public bool CompleteWindUp()
        {
            if (Phase != MeleePhase.WindUp)
            {
                return false;
            }

            Phase = MeleePhase.Recovery;
            _phaseTimer = _recoveryDuration;
            return true;
        }

        /// <summary>
        /// Advances the swing by <paramref name="deltaTime"/> seconds, reporting whether the blow
        /// connected and/or the swing finished this tick. Impact fires exactly once per swing. A single
        /// large delta can both impact and finish in one tick (leftover time carries into recovery).
        /// </summary>
        public MeleeSwingTick Advance(float deltaTime)
        {
            if (Phase == MeleePhase.Idle)
            {
                return default;
            }

            _phaseTimer -= Mathf.Max(0f, deltaTime);

            bool impacted = false;
            if (Phase == MeleePhase.WindUp && _phaseTimer <= 0f)
            {
                impacted = true;
                Phase = MeleePhase.Recovery;
                _phaseTimer += _recoveryDuration; // carry any overshoot into the recovery window
            }

            bool finished = false;
            if (Phase == MeleePhase.Recovery && _phaseTimer <= 0f)
            {
                finished = true;
                Phase = MeleePhase.Idle;
            }

            return new MeleeSwingTick(impacted, finished);
        }
    }
}
