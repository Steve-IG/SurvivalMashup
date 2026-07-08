using System.Collections.Generic;
using ToyChest.Framework.Data;
using UnityEngine;

namespace ToyChest.Systems.GameplayEffects
{
    /// <summary>
    /// Base of every atomic Gameplay Effect: one deterministic, instantaneous gameplay action
    /// (deal damage, add a tag, apply a modifier). Effects own no duration, no scheduling, and
    /// no targeting — the Status Effect System schedules, the Ability System targets. Complex
    /// gameplay is a sequence of atomic effects, never a monolithic one (Engine Principle 24).
    /// Concrete effect types are engine code; their assets are configuration.
    /// See Docs/Systems/GAMEPLAY_EFFECT_SYSTEM.md.
    /// </summary>
    public abstract class GameplayEffectDefinition : GameplayDefinition
    {
        [SerializeField]
        [Tooltip("All conditions must be satisfied for the effect to execute. Empty = unconditional.")]
        private List<EffectCondition> _conditions = new List<EffectCondition>();

        /// <summary>Conditions gating execution. All must pass.</summary>
        public IReadOnlyList<EffectCondition> Conditions => _conditions;

        /// <summary>
        /// Executes the effect if every condition is satisfied.
        /// Returns true when the effect ran, false when a condition blocked it.
        /// </summary>
        public bool TryExecute(in EffectContext context)
        {
            for (int i = 0; i < _conditions.Count; i++)
            {
                if (!_conditions[i].IsSatisfied(in context))
                {
                    return false;
                }
            }

            Execute(in context);
            return true;
        }

        /// <summary>
        /// The single deterministic action this effect performs. Implementations mutate the
        /// context's capabilities through their owning systems and throw a descriptive
        /// exception when a required capability is absent.
        /// </summary>
        protected abstract void Execute(in EffectContext context);

        /// <summary>Fails clearly when a capability this effect requires is missing.</summary>
        protected TCapability Require<TCapability>(TCapability capability, string role) where TCapability : class
        {
            if (capability == null)
            {
                throw new System.InvalidOperationException(
                    $"Effect '{Id}' requires the {role} to have a {typeof(TCapability).Name}, but it is absent. " +
                    "Check the target object's definition or the effect's usage.");
            }

            return capability;
        }
    }
}
