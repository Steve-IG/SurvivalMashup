namespace ToyChest.Framework.Data
{
    /// <summary>
    /// Contract for every immutable gameplay definition.
    /// Definitions describe what content is; runtime instances hold what it currently does.
    /// Definitions never change during gameplay and never contain runtime state.
    /// </summary>
    public interface IDefinition
    {
        /// <summary>Stable identifier used by the Data Registry, saves, and tooling.</summary>
        DefinitionId Id { get; }
    }
}
