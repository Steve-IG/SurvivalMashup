namespace ToyChest.Framework.Modifiers
{
    /// <summary>
    /// How a modifier combines with the value being computed.
    /// The enum order is also the fixed evaluation order enforced by the modifier stack:
    /// Flat, then Additive percent, then Multiplicative percent, then Override.
    /// See Docs/Architecture/ENGINE_PRINCIPLES.md (deterministic behavior) and the
    /// Attribute and Resource system documents.
    /// </summary>
    public enum ModifierOperation
    {
        /// <summary>Added to the base value: +10. Applied first, all flats summed.</summary>
        Flat = 0,

        /// <summary>
        /// Additive percentage of the base value: +20% and +30% combine to +50%.
        /// Applied to the post-flat subtotal.
        /// </summary>
        AdditivePercent = 1,

        /// <summary>
        /// Multiplicative percentage, compounding: x1.5 then x1.2 yields x1.8.
        /// Applied after additive percentages.
        /// </summary>
        MultiplicativePercent = 2,

        /// <summary>
        /// Replaces the computed value outright. When several overrides are present,
        /// the highest-priority source wins deterministically.
        /// </summary>
        Override = 3,
    }
}
