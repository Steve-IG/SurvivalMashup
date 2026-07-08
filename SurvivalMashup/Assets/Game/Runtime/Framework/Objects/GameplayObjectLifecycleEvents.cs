using ToyChest.Framework.Data;
using ToyChest.Framework.Events;

namespace ToyChest.Framework.Objects
{
    /// <summary>
    /// Fact: a Gameplay Object became live. Published by the Gameplay Framework when an
    /// object activates, after composition and placement. Owned solely by the framework.
    /// </summary>
    [EventCategory(EventCategories.Framework)]
    public readonly struct GameplayObjectSpawned : IGameplayEvent
    {
        /// <summary>The object's persistent identity.</summary>
        public readonly GameplayObjectId Object;

        /// <summary>The definition the object was composed from.</summary>
        public readonly DefinitionId Definition;

        public GameplayObjectSpawned(GameplayObjectId objectId, DefinitionId definition)
        {
            Object = objectId;
            Definition = definition;
        }

        /// <inheritdoc />
        public override string ToString() => $"GameplayObjectSpawned({Definition} as {Object})";
    }

    /// <summary>
    /// Fact: a Gameplay Object was torn down. Published by the Gameplay Framework during
    /// destroy, before capabilities are disposed. Owned solely by the framework.
    /// </summary>
    [EventCategory(EventCategories.Framework)]
    public readonly struct GameplayObjectDestroyed : IGameplayEvent
    {
        /// <summary>The object's persistent identity.</summary>
        public readonly GameplayObjectId Object;

        /// <summary>The definition the object was composed from.</summary>
        public readonly DefinitionId Definition;

        public GameplayObjectDestroyed(GameplayObjectId objectId, DefinitionId definition)
        {
            Object = objectId;
            Definition = definition;
        }

        /// <inheritdoc />
        public override string ToString() => $"GameplayObjectDestroyed({Definition} as {Object})";
    }
}
