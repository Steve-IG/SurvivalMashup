using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Objects;
using ToyChest.Systems.Items;

namespace ToyChest.Systems.Inventory
{
    /// <summary>Fact: a quantity of an item entered an inventory from outside it.</summary>
    [EventCategory(EventCategories.Inventory)]
    public readonly struct ItemAdded : IGameplayEvent
    {
        public readonly GameplayObjectId Owner;
        public readonly DefinitionId Item;
        public readonly int Quantity;

        public ItemAdded(GameplayObjectId owner, DefinitionId item, int quantity)
        {
            Owner = owner;
            Item = item;
            Quantity = quantity;
        }

        /// <inheritdoc />
        public override string ToString() => $"ItemAdded({Item} x{Quantity} to {Owner})";
    }

    /// <summary>Fact: a quantity of an item left an inventory.</summary>
    [EventCategory(EventCategories.Inventory)]
    public readonly struct ItemRemoved : IGameplayEvent
    {
        public readonly GameplayObjectId Owner;
        public readonly DefinitionId Item;
        public readonly int Quantity;

        public ItemRemoved(GameplayObjectId owner, DefinitionId item, int quantity)
        {
            Owner = owner;
            Item = item;
            Quantity = quantity;
        }

        /// <inheritdoc />
        public override string ToString() => $"ItemRemoved({Item} x{Quantity} from {Owner})";
    }

    /// <summary>Fact: the quantity of a stack that remains in the inventory changed.</summary>
    [EventCategory(EventCategories.Inventory)]
    public readonly struct StackChanged : IGameplayEvent
    {
        public readonly GameplayObjectId Owner;
        public readonly ItemInstanceId Stack;
        public readonly DefinitionId Item;
        public readonly int NewQuantity;

        public StackChanged(GameplayObjectId owner, ItemInstanceId stack, DefinitionId item, int newQuantity)
        {
            Owner = owner;
            Stack = stack;
            Item = item;
            NewQuantity = newQuantity;
        }

        /// <inheritdoc />
        public override string ToString() => $"StackChanged({Item} stack on {Owner} → {NewQuantity})";
    }

    /// <summary>Fact: an add was rejected because the inventory cannot hold the quantity.</summary>
    [EventCategory(EventCategories.Inventory)]
    public readonly struct InventoryFull : IGameplayEvent
    {
        public readonly GameplayObjectId Owner;
        public readonly DefinitionId Item;
        public readonly int RejectedQuantity;

        public InventoryFull(GameplayObjectId owner, DefinitionId item, int rejectedQuantity)
        {
            Owner = owner;
            Item = item;
            RejectedQuantity = rejectedQuantity;
        }

        /// <inheritdoc />
        public override string ToString() => $"InventoryFull({Owner} rejected {Item} x{RejectedQuantity})";
    }

    /// <summary>Fact: item ownership transferred from one inventory to another.</summary>
    [EventCategory(EventCategories.Inventory)]
    public readonly struct ItemTransferred : IGameplayEvent
    {
        public readonly GameplayObjectId FromOwner;
        public readonly GameplayObjectId ToOwner;
        public readonly DefinitionId Item;
        public readonly int Quantity;

        public ItemTransferred(GameplayObjectId fromOwner, GameplayObjectId toOwner, DefinitionId item, int quantity)
        {
            FromOwner = fromOwner;
            ToOwner = toOwner;
            Item = item;
            Quantity = quantity;
        }

        /// <inheritdoc />
        public override string ToString() => $"ItemTransferred({Item} x{Quantity}: {FromOwner} → {ToOwner})";
    }
}
