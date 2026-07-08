namespace ToyChest.Framework.Objects
{
    /// <summary>
    /// A capability that advances with time (resource regeneration, status durations).
    /// The owning Gameplay Object fans out <see cref="Tick"/> while active; time is always
    /// injected so capabilities stay deterministic and testable without the engine clock.
    /// </summary>
    public interface ITickingCapability : IGameplayCapability
    {
        /// <summary>Advances the capability by <paramref name="deltaSeconds"/> (non-negative).</summary>
        void Tick(float deltaSeconds);
    }
}
