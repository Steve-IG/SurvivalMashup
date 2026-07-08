using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Objects;

namespace ToyChest.Systems.Resources
{
    /// <summary>
    /// Fact: a resource's current value changed. Published by the Resource System on every
    /// current-value movement — consumption, restoration, regeneration, or a clamp caused by a
    /// lowered maximum. Owned solely by the Resource System.
    /// </summary>
    [EventCategory(EventCategories.Resource)]
    public readonly struct ResourceChanged : IGameplayEvent
    {
        /// <summary>The object whose resource changed. Default when the set is unowned (isolated tests).</summary>
        public readonly GameplayObjectId Owner;

        /// <summary>The resource definition whose current value changed.</summary>
        public readonly DefinitionId Resource;

        /// <summary>Current value before the change.</summary>
        public readonly float PreviousValue;

        /// <summary>Current value after the change.</summary>
        public readonly float NewValue;

        /// <summary>The resource's maximum at the time of the change.</summary>
        public readonly float Maximum;

        public ResourceChanged(GameplayObjectId owner, DefinitionId resource, float previousValue, float newValue, float maximum)
        {
            Owner = owner;
            Resource = resource;
            PreviousValue = previousValue;
            NewValue = newValue;
            Maximum = maximum;
        }

        /// <inheritdoc />
        public override string ToString() =>
            $"ResourceChanged({Resource} on {Owner}: {PreviousValue} -> {NewValue} / {Maximum})";
    }
}
