namespace ToyChest.Systems.StatusEffects
{
    /// <summary>How a status effect's lifetime is measured.</summary>
    public enum StatusDurationType
    {
        /// <summary>Expires after a configured number of seconds.</summary>
        Timed = 0,

        /// <summary>Persists until explicitly removed.</summary>
        Infinite = 1,
    }
}
