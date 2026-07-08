using System;

namespace ToyChest.Systems.Abilities
{
    /// <summary>
    /// Runtime state of one granted ability on one object (Engine Principle 14): its cooldown.
    /// Owned and advanced by the <see cref="AbilitySet"/>; never reads the clock itself, so
    /// cooldown behavior is deterministic and testable with injected time.
    /// </summary>
    public sealed class AbilityInstance
    {
        internal AbilityInstance(AbilityDefinition definition)
        {
            Definition = definition;
        }

        /// <summary>The immutable ability definition.</summary>
        public AbilityDefinition Definition { get; }

        /// <summary>Seconds until the ability may activate again. Zero when ready.</summary>
        public float CooldownRemaining { get; private set; }

        /// <summary>Whether the ability is cooling down and cannot activate.</summary>
        public bool IsOnCooldown => CooldownRemaining > 0f;

        /// <summary>Begins the definition's fixed cooldown. No-op for zero-cooldown abilities.</summary>
        internal void StartCooldown()
        {
            CooldownRemaining = Definition.CooldownSeconds;
        }

        /// <summary>
        /// Restores the remaining cooldown from persisted authoritative state (rehydration).
        /// Sets the value directly without publishing events — reconstruction is not a gameplay
        /// activation. The remaining value must be non-negative.
        /// </summary>
        internal void RestoreCooldown(float remainingSeconds)
        {
            if (remainingSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(remainingSeconds), remainingSeconds, "Restored cooldown must be non-negative.");
            }

            CooldownRemaining = remainingSeconds;
        }

        /// <summary>
        /// Advances the cooldown. Returns true when this advance completed the cooldown
        /// (the ready transition), so the owning set can publish the fact exactly once.
        /// </summary>
        internal bool Advance(float deltaSeconds)
        {
            if (CooldownRemaining <= 0f)
            {
                return false;
            }

            CooldownRemaining = Math.Max(0f, CooldownRemaining - deltaSeconds);
            return CooldownRemaining <= 0f;
        }
    }
}
