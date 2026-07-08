using ToyChest.Framework.Events;
using ToyChest.Framework.Objects;

namespace ToyChest.Systems.Tags
{
    /// <summary>
    /// Fact: a tag became present on an object. Published once on the absent-to-present
    /// transition, not on every source that adds it while it is already present (tags are
    /// reference-counted; intermediate count changes are silent). Owned solely by the Tag
    /// System, published by the object's <see cref="GameplayTagContainer"/> — the Gameplay Tag
    /// capability component — completing the bridge between tag changes and the Event Bus while
    /// keeping tag ownership with the Tag System. See Docs/Systems/TAG_SYSTEM.md.
    /// </summary>
    [EventCategory(EventCategories.Tag)]
    public readonly struct GameplayTagAdded : IGameplayEvent
    {
        /// <summary>The object the tag became present on.</summary>
        public readonly GameplayObjectId Owner;

        /// <summary>The tag that transitioned to present.</summary>
        public readonly GameplayTag Tag;

        public GameplayTagAdded(GameplayObjectId owner, GameplayTag tag)
        {
            Owner = owner;
            Tag = tag;
        }

        /// <inheritdoc />
        public override string ToString() => $"GameplayTagAdded({Tag} on {Owner})";
    }

    /// <summary>
    /// Fact: a tag left an object. Published once on the present-to-absent transition, after
    /// the last contributing source removes it. Owned solely by the Tag System, published by
    /// the object's <see cref="GameplayTagContainer"/>. See Docs/Systems/TAG_SYSTEM.md.
    /// </summary>
    [EventCategory(EventCategories.Tag)]
    public readonly struct GameplayTagRemoved : IGameplayEvent
    {
        /// <summary>The object the tag left.</summary>
        public readonly GameplayObjectId Owner;

        /// <summary>The tag that transitioned to absent.</summary>
        public readonly GameplayTag Tag;

        public GameplayTagRemoved(GameplayObjectId owner, GameplayTag tag)
        {
            Owner = owner;
            Tag = tag;
        }

        /// <inheritdoc />
        public override string ToString() => $"GameplayTagRemoved({Tag} on {Owner})";
    }
}
