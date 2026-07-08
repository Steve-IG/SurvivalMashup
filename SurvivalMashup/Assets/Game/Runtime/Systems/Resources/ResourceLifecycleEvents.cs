using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Objects;

namespace ToyChest.Systems.Resources
{
    /// <summary>
    /// Fact: a resource reached zero. Published once on the transition to empty, not on every
    /// change while empty. Owned solely by the Resource System.
    /// </summary>
    [EventCategory(EventCategories.Resource)]
    public readonly struct ResourceDepleted : IGameplayEvent
    {
        /// <summary>The object whose resource emptied.</summary>
        public readonly GameplayObjectId Owner;

        /// <summary>The resource that reached zero.</summary>
        public readonly DefinitionId Resource;

        public ResourceDepleted(GameplayObjectId owner, DefinitionId resource)
        {
            Owner = owner;
            Resource = resource;
        }

        /// <inheritdoc />
        public override string ToString() => $"ResourceDepleted({Resource} on {Owner})";
    }

    /// <summary>
    /// Fact: a resource reached its maximum. Published once on the transition to full.
    /// Owned solely by the Resource System.
    /// </summary>
    [EventCategory(EventCategories.Resource)]
    public readonly struct ResourceFull : IGameplayEvent
    {
        /// <summary>The object whose resource filled.</summary>
        public readonly GameplayObjectId Owner;

        /// <summary>The resource that reached its maximum.</summary>
        public readonly DefinitionId Resource;

        /// <summary>The maximum it reached.</summary>
        public readonly float Maximum;

        public ResourceFull(GameplayObjectId owner, DefinitionId resource, float maximum)
        {
            Owner = owner;
            Resource = resource;
            Maximum = maximum;
        }

        /// <inheritdoc />
        public override string ToString() => $"ResourceFull({Resource} on {Owner} @ {Maximum})";
    }
}
