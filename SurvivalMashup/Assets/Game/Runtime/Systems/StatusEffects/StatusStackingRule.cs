namespace ToyChest.Systems.StatusEffects
{
    /// <summary>
    /// How re-applying an already-active status behaves. Milestone 0 implements this subset
    /// of the models in Docs/Systems/STATUS_EFFECT_SYSTEM.md; Independent Instances and
    /// fully custom models are future extensions.
    /// </summary>
    public enum StatusStackingRule
    {
        /// <summary>Re-application resets the remaining duration.</summary>
        RefreshDuration = 0,

        /// <summary>Re-application is ignored entirely.</summary>
        IgnoreDuplicate = 1,

        /// <summary>Re-application removes the existing instance and applies a fresh one.</summary>
        ReplaceExisting = 2,

        /// <summary>
        /// Re-application adds a stack (up to the maximum), re-applies the attribute modifiers
        /// so their magnitude scales with stacks, and resets the remaining duration.
        /// At maximum stacks, only the duration resets.
        /// </summary>
        IncreaseStacks = 3,
    }
}
