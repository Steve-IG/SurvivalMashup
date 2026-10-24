using UnityEngine;

namespace ToyChest.Gameplay.Enemy
{
    /// <summary>
    /// Pure, deterministic timing for an enemy's optional <b>signature attack</b> — the second, heavier attack an
    /// elite layers over its ordinary strike (the Grunt Brute's Shockwave Slam). It is one more
    /// <see cref="EnemyAttackCycle"/> (Ready → Wind-up → strike → Recover) plus the two rules that make it a
    /// signature rather than a second ordinary swing: it only begins when the target is within its trigger range,
    /// and after each use it waits out an authored cooldown (and an initial delay on first engagement) so the
    /// ordinary strike still fills the gaps. Like the cycle, it reads no clock and applies no damage; the adapter
    /// resolves the strike when <see cref="Advance"/> reports it. Separated from the MonoBehaviour so it is testable
    /// without a scene (Engine Principle 17).
    /// </summary>
    public sealed class EnemySignatureAttack
    {
        private readonly float _cooldownSeconds;
        private readonly float _triggerRange;
        private float _cooldownRemaining;

        /// <summary>
        /// Builds the signature attack from authored data: wind-up and recovery durations, the cooldown between uses,
        /// the delay before the first use, the planar distance within which it may begin, and whether a hit during
        /// its wind-up interrupts it (a signature attack is usually not interruptible — that is what makes it a
        /// "get out of the way" telegraph rather than a "hit it first" one).
        /// </summary>
        public EnemySignatureAttack(
            float windUpSeconds,
            float recoverSeconds,
            float cooldownSeconds,
            float initialDelaySeconds,
            float triggerRange,
            bool windUpInterruptible)
        {
            Cycle = new EnemyAttackCycle(windUpSeconds, recoverSeconds, windUpInterruptible, recoverInterruptible: false);
            _cooldownSeconds = Mathf.Max(0f, cooldownSeconds);
            _cooldownRemaining = Mathf.Max(0f, initialDelaySeconds);
            _triggerRange = Mathf.Max(0f, triggerRange);
        }

        /// <summary>The underlying telegraphed attack cycle.</summary>
        public EnemyAttackCycle Cycle { get; }

        /// <summary>Seconds until the signature attack may begin again (counts down only while it is not attacking).</summary>
        public float CooldownRemaining => _cooldownRemaining;

        /// <summary>Whether the signature attack is idle and off cooldown.</summary>
        public bool IsReady => !Cycle.IsAttacking && _cooldownRemaining <= 0f;

        /// <summary>Whether the signature attack should begin now against a target <paramref name="distance"/> away.</summary>
        public bool ShouldBegin(float distance) => IsReady && distance <= _triggerRange;

        /// <summary>Begins the wind-up. No-op unless <see cref="IsReady"/>.</summary>
        public bool Begin()
        {
            if (!IsReady)
            {
                return false;
            }

            Cycle.BeginWindUp();
            return true;
        }

        /// <summary>
        /// Advances by <paramref name="deltaTime"/> seconds: counts the cooldown down while idle, otherwise advances
        /// the attack cycle. The cooldown restarts the tick recovery ends, so the next signature attack is measured
        /// from the moment the enemy is free to act again.
        /// </summary>
        public EnemyAttackTick Advance(float deltaTime)
        {
            if (!Cycle.IsAttacking)
            {
                _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - Mathf.Max(0f, deltaTime));
                return default;
            }

            EnemyAttackTick tick = Cycle.Advance(deltaTime);
            if (tick.Recovered)
            {
                _cooldownRemaining = _cooldownSeconds;
            }

            return tick;
        }

        /// <summary>
        /// A hit landed on the attacker: interrupts the signature attack only within its authored interrupt window,
        /// and, if it does, restarts the cooldown so an interrupted signature attack is not immediately retried.
        /// </summary>
        public bool TryInterrupt()
        {
            if (!Cycle.TryInterrupt())
            {
                return false;
            }

            _cooldownRemaining = _cooldownSeconds;
            return true;
        }
    }
}
