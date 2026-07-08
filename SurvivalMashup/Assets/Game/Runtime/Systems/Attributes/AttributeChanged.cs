using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Objects;

namespace ToyChest.Systems.Attributes
{
    /// <summary>
    /// Fact: an attribute's computed value changed. Published by the Attribute System after a
    /// modifier was added or removed. Carries the owning object, the definition id, and both
    /// values so listeners react without querying back. Owned solely by the Attribute System.
    /// </summary>
    [EventCategory(EventCategories.Attribute)]
    public readonly struct AttributeChanged : IGameplayEvent
    {
        /// <summary>The object whose attribute changed. Default when the set is unowned (isolated tests).</summary>
        public readonly GameplayObjectId Owner;

        /// <summary>The attribute definition whose value changed.</summary>
        public readonly DefinitionId Attribute;

        /// <summary>The computed value before the change.</summary>
        public readonly float PreviousValue;

        /// <summary>The computed value after the change.</summary>
        public readonly float NewValue;

        public AttributeChanged(GameplayObjectId owner, DefinitionId attribute, float previousValue, float newValue)
        {
            Owner = owner;
            Attribute = attribute;
            PreviousValue = previousValue;
            NewValue = newValue;
        }

        /// <inheritdoc />
        public override string ToString() => $"AttributeChanged({Attribute} on {Owner}: {PreviousValue} -> {NewValue})";
    }
}
