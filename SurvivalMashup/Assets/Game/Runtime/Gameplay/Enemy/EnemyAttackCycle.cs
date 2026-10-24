using UnityEngine;

namespace ToyChest.Gameplay.Enemy
{
    /// <summary>The phase an enemy's telegraphed attack is in.</summary>
    public enum EnemyAttackPhase
    {
        /// <summary>Not attacking; free to pursue.</summary>
        Ready,

        /// <summary>Anticipating: rooted and telegraphing before the blow lands.</summary>
        WindUp,

        /// <summary>Committed: rooted just after the blow.</summary>
        Recover
    }

    /// <summary>What happened to the attack during one <see cref="EnemyAttackCycle.Advance"/> tick.</summary>
    public readonly struct EnemyAttackTick
    {
        /// <summary>The blow lands this tick (the wind-up completed) — the moment to resolve the strike.</summary>
        public bool Struck { get; }

        /// <summary>The recovery finished this tick and the enemy is free to act again.</summary>
        public bool Recovered { get; }

        internal EnemyAttackTick(bool struck, bool recovered)
        {
            Struck = struck;
            Recovered = recovered;
        }
    }

    /// <summary>
    /// Pure, deterministic timing for one enemy's telegraphed attack — <c>Ready → Wind-up → (strike) →
    /// Recover</c> — separated from the MonoBehaviour so it is testable without a scene (Engine Principle
    /// 17). It reads no clock and applies no damage; it only reports when the strike should land and when
    /// recovery ends, and — the RG5D addition — whether a hit right now falls in an <b>authored interrupt
    /// window</b>.
    ///
    /// <para>Interruptibility is data, not a hardcoded time threshold: each phase carries its own
    /// interruptible flag, so different enemy attacks can eventually make different portions cancelable
    /// without any code change. Interrupting during the wind-up cancels the attack <em>before</em> the
    /// strike resolves, so the pending blow never lands; interrupting during recovery (when authored
    /// interruptible) staggers the enemy out of its follow-through. A hit in a non-interruptible phase
    /// leaves the attack running. This is deliberately not a stun/poise system — it is only the readable
    /// "hit it while it is winding up and you cut the attack short" rule.</para>
    /// </summary>
    public sealed class EnemyAttackCycle
    {
        private readonly float _windUpSeconds;
        private readonly float _recoverSeconds;
        private readonly bool _windUpInterruptible;
        private readonly bool _recoverInterruptible;
        private readonly float _interruptWindowSeconds;

        private EnemyAttackPhase _phase = EnemyAttackPhase.Ready;
        private float _timer;

        /// <summary>
        /// Builds the cycle from authored attack data: the wind-up and recovery durations (seconds) and
        /// which phases a hit may interrupt.
        /// </summary>
        public EnemyAttackCycle(float windUpSeconds, float recoverSeconds, bool windUpInterruptible, bool recoverInterruptible)
            : this(windUpSeconds, recoverSeconds, windUpInterruptible, recoverInterruptible, float.PositiveInfinity)
        {
        }

        /// <summary>
        /// As above, plus the <b>early interrupt window</b>: an ordinary (flinch-weight) hit interrupts the wind-up only
        /// during its first <paramref name="interruptWindowSeconds"/> — the moment the enemy commits to its swing, a
        /// light blow can no longer stop it. Heavier hits pass <c>ignoreWindow</c> to <see cref="TryInterrupt(bool)"/>
        /// and interrupt the whole interruptible wind-up.
        /// </summary>
        public EnemyAttackCycle(float windUpSeconds, float recoverSeconds, bool windUpInterruptible, bool recoverInterruptible, float interruptWindowSeconds)
        {
            _windUpSeconds = Mathf.Max(0f, windUpSeconds);
            _recoverSeconds = Mathf.Max(0f, recoverSeconds);
            _windUpInterruptible = windUpInterruptible;
            _recoverInterruptible = recoverInterruptible;
            _interruptWindowSeconds = Mathf.Max(0f, interruptWindowSeconds);
        }

        /// <summary>The phase the attack is in.</summary>
        public EnemyAttackPhase Phase => _phase;

        /// <summary>Whether an attack is in progress (winding up or recovering).</summary>
        public bool IsAttacking => _phase != EnemyAttackPhase.Ready;

        /// <summary>Whether the attack is still anticipating (before the blow lands).</summary>
        public bool IsWindingUp => _phase == EnemyAttackPhase.WindUp;

        /// <summary>
        /// How far through the wind-up the attack is, 0 at its start to 1 at the instant the blow lands; 0 outside the
        /// wind-up. Presentation reads it to show the player when a telegraphed blow will land.
        /// </summary>
        public float WindUpProgress =>
            _phase != EnemyAttackPhase.WindUp ? 0f :
            _windUpSeconds <= 0f ? 1f : Mathf.Clamp01(1f - _timer / _windUpSeconds);

        /// <summary>
        /// Whether a hit right now would interrupt the attack — the authored interrupt window for the
        /// current phase. Ready is never interruptible (there is nothing to interrupt).
        /// </summary>
        public bool IsInterruptible => CanInterrupt(ignoreWindow: false);

        /// <summary>Seconds elapsed in the current wind-up (0 outside it).</summary>
        public float WindUpElapsed => _phase == EnemyAttackPhase.WindUp ? _windUpSeconds - _timer : 0f;

        private bool CanInterrupt(bool ignoreWindow) =>
            _phase == EnemyAttackPhase.WindUp ? _windUpInterruptible && (ignoreWindow || WindUpElapsed <= _interruptWindowSeconds) :
            _phase == EnemyAttackPhase.Recover && _recoverInterruptible;

        /// <summary>Begins the wind-up from <see cref="EnemyAttackPhase.Ready"/>. No-op if already attacking.</summary>
        public void BeginWindUp()
        {
            if (_phase != EnemyAttackPhase.Ready)
            {
                return;
            }

            _phase = EnemyAttackPhase.WindUp;
            _timer = _windUpSeconds;
        }

        /// <summary>
        /// Advances the attack by <paramref name="deltaTime"/> seconds. Reports <see cref="EnemyAttackTick.Struck"/>
        /// on the tick the wind-up completes (then enters recovery) and <see cref="EnemyAttackTick.Recovered"/>
        /// on the tick recovery ends (then returns to Ready). At most one strike per wind-up.
        /// </summary>
        public EnemyAttackTick Advance(float deltaTime)
        {
            if (_phase == EnemyAttackPhase.Ready)
            {
                return default;
            }

            _timer -= Mathf.Max(0f, deltaTime);

            if (_phase == EnemyAttackPhase.WindUp && _timer <= 0f)
            {
                _phase = EnemyAttackPhase.Recover;
                _timer = _recoverSeconds;
                return new EnemyAttackTick(struck: true, recovered: false);
            }

            if (_phase == EnemyAttackPhase.Recover && _timer <= 0f)
            {
                _phase = EnemyAttackPhase.Ready;
                _timer = 0f;
                return new EnemyAttackTick(struck: false, recovered: true);
            }

            return default;
        }

        /// <summary>
        /// Lands the strike immediately because the attack animation reached its contact frame (an authored
        /// <c>OnAttackContact</c> event), moving straight to recovery. Returns true when this call is what
        /// landed the blow (it was winding up); false otherwise, so the caller resolves the strike exactly
        /// once. If the attack was already interrupted this returns false — the pending blow never lands.
        /// </summary>
        public bool CompleteWindUp()
        {
            if (_phase != EnemyAttackPhase.WindUp)
            {
                return false;
            }

            _phase = EnemyAttackPhase.Recover;
            _timer = _recoverSeconds;
            return true;
        }

        /// <summary>
        /// Resets to <see cref="EnemyAttackPhase.Ready"/> with no strike — the player left the telegraph
        /// (a fair dodge), not an interrupt. Distinct from <see cref="TryInterrupt"/>, which is gated by the
        /// authored interrupt window.
        /// </summary>
        public void Reset()
        {
            _phase = EnemyAttackPhase.Ready;
            _timer = 0f;
        }

        /// <summary>
        /// A hit landed on the attacker. If the current phase is within an authored interrupt window, cancels
        /// the attack (returning to Ready — a pending strike during the wind-up therefore never resolves) and
        /// returns true. In a non-interruptible phase the attack is left running and this returns false, so a
        /// blow outside the window does not arbitrarily cancel it.
        /// </summary>
        public bool TryInterrupt() => TryInterrupt(ignoreWindow: false);

        /// <summary>
        /// As <see cref="TryInterrupt()"/>; with <paramref name="ignoreWindow"/> (a stagger-weight hit) the whole
        /// interruptible wind-up can be interrupted, not just its early window. An attack authored uninterruptible
        /// stays uninterruptible either way.
        /// </summary>
        public bool TryInterrupt(bool ignoreWindow)
        {
            if (!CanInterrupt(ignoreWindow))
            {
                return false;
            }

            _phase = EnemyAttackPhase.Ready;
            _timer = 0f;
            return true;
        }
    }
}
