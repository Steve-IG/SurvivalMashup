using System.Collections.Generic;

namespace ToyChest.Framework.Data
{
    /// <summary>
    /// Supplies definitions to the Data Registry during initialization.
    /// Sources own all loading policy (Addressables, direct references, in-memory test data);
    /// the registry never initiates loading and never knows how definitions were acquired.
    /// Any asynchronous preloading completes inside the source before it yields.
    /// </summary>
    public interface IDefinitionSource
    {
        /// <summary>Yields every definition this source provides, in deterministic order.</summary>
        IEnumerable<IDefinition> LoadDefinitions();
    }
}
