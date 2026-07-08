namespace ToyChest.Systems.Interactions
{
    /// <summary>
    /// Outcome of an interaction attempt or availability query. Checks run in the declared
    /// order — interactable, advertised, interactor tag gates, ability validation — and the
    /// first failing check is the reported reason, deterministically.
    /// </summary>
    public enum InteractionResult
    {
        /// <summary>Every check passed; the interaction's ability was activated.</summary>
        Executed,

        /// <summary>The target object advertises no interactions.</summary>
        NotInteractable,

        /// <summary>The target object does not advertise this interaction.</summary>
        UnknownInteraction,

        /// <summary>The interactor lacks a tag the interaction requires.</summary>
        MissingInteractorTag,

        /// <summary>The interactor carries a tag that blocks the interaction.</summary>
        BlockedByInteractorTag,

        /// <summary>The interaction's ability rejected activation (cooldown, cost, tag gate). The Ability System publishes the detailed reason.</summary>
        AbilityRejected,
    }
}
