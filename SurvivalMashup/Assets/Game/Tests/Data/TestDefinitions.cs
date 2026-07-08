using ToyChest.Framework.Data;

namespace ToyChest.Tests.Data
{
    /// <summary>Plain C# definition used to test the registry without ScriptableObjects.</summary>
    internal sealed class FruitDefinition : IDefinition
    {
        public FruitDefinition(string id, string displayName)
        {
            Id = new DefinitionId(id);
            DisplayName = displayName;
        }

        public DefinitionId Id { get; }

        public string DisplayName { get; }
    }

    /// <summary>Second definition type used to verify type bucket isolation.</summary>
    internal sealed class ToolDefinition : IDefinition
    {
        public ToolDefinition(string id)
        {
            Id = new DefinitionId(id);
        }

        public DefinitionId Id { get; }
    }

    /// <summary>Definition whose id is invalid, for registration failure tests.</summary>
    internal sealed class BrokenDefinition : IDefinition
    {
        public DefinitionId Id => default;
    }
}
