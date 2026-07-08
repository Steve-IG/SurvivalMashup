namespace ToyChest.Systems.Abilities
{
    /// <summary>
    /// How an ability resolves the target its effects execute against. Targeting is
    /// independent of effects: the ability declares its mode, the activator (input layer,
    /// AI, interaction) selects concrete targets, and effects never target themselves.
    /// Spatial modes (ground, area, projectile) are future extensions that arrive with the
    /// world systems able to answer them.
    /// </summary>
    public enum AbilityTargetMode
    {
        /// <summary>Effects execute against the owning object. No explicit target is accepted.</summary>
        Self,

        /// <summary>The activator supplies the selected target at activation.</summary>
        Provided,
    }
}
