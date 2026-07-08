using System.Collections.Generic;

namespace ToyChest.Framework.Data
{
    /// <summary>
    /// Canonical runtime source for immutable gameplay definitions.
    /// Populated during initialization from definition sources; read-only thereafter by
    /// convention. Provides access to definitions without interpreting their meaning.
    /// Definitions are bucketed by concrete runtime type: Get&lt;TagDefinition&gt; finds
    /// definitions whose runtime type is TagDefinition. See Docs/Architecture/DATA_REGISTRY.md.
    /// </summary>
    public interface IDataRegistry
    {
        /// <summary>
        /// Registers a definition under its concrete runtime type.
        /// Duplicate (type, id) registration throws: one id resolves to exactly one definition.
        /// </summary>
        void Register(IDefinition definition);

        /// <summary>
        /// Resolves a definition, throwing a descriptive exception when the id is unknown.
        /// Use for lookups that must succeed; a miss is a content error.
        /// </summary>
        TDefinition Get<TDefinition>(DefinitionId id) where TDefinition : class, IDefinition;

        /// <summary>Tolerant lookup for genuinely optional definitions.</summary>
        bool TryGet<TDefinition>(DefinitionId id, out TDefinition definition) where TDefinition : class, IDefinition;

        /// <summary>All registered definitions of the type, in deterministic registration order.</summary>
        IReadOnlyList<TDefinition> GetAll<TDefinition>() where TDefinition : class, IDefinition;

        /// <summary>Whether a definition of the type is registered under the id.</summary>
        bool Contains<TDefinition>(DefinitionId id) where TDefinition : class, IDefinition;
    }
}
