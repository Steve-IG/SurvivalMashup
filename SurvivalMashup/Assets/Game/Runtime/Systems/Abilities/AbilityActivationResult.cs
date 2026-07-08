namespace ToyChest.Systems.Abilities
{
    /// <summary>
    /// Outcome of an ability activation attempt or validation query. Checks run in the
    /// declared order — granted, target validity, owner tag gates, cooldown, costs — and the
    /// first failing check is the reported reason, deterministically.
    /// </summary>
    public enum AbilityActivationResult
    {
        /// <summary>Every check passed; the activation was committed.</summary>
        Activated,

        /// <summary>The owner has not been granted the ability.</summary>
        NotGranted,

        /// <summary>The supplied target does not match the ability's target mode.</summary>
        InvalidTarget,

        /// <summary>The owner lacks a tag the ability requires.</summary>
        MissingRequiredTag,

        /// <summary>The owner carries a tag that blocks the ability (stun, silence).</summary>
        BlockedByTag,

        /// <summary>The ability is cooling down.</summary>
        OnCooldown,

        /// <summary>At least one resource cost cannot be paid in full.</summary>
        CannotAfford,
    }
}
