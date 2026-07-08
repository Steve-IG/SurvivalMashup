using UnityEngine;

namespace ToyChest.Systems.GameplayEffects
{
    /// <summary>
    /// Reusable, data-authored gate on effect execution ("target is burning",
    /// "current health below 30%"). Conditions answer a question about the context;
    /// they never mutate anything and never contain gameplay outcomes.
    /// </summary>
    public abstract class EffectCondition : ScriptableObject
    {
        /// <summary>Whether the effect may execute in this context. Pure; no side effects.</summary>
        public abstract bool IsSatisfied(in EffectContext context);
    }
}
