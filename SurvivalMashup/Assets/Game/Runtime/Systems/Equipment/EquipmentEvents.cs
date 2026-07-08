using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Objects;
using ToyChest.Systems.Items;

namespace ToyChest.Systems.Equipment
{
    /// <summary>Fact: an item was equipped and its contributions activated.</summary>
    [EventCategory(EventCategories.Equipment)]
    public readonly struct ItemEquipped : IGameplayEvent
    {
        public readonly GameplayObjectId Owner;
        public readonly DefinitionId Slot;
        public readonly DefinitionId Item;
        public readonly ItemInstanceId Instance;

        public ItemEquipped(GameplayObjectId owner, DefinitionId slot, DefinitionId item, ItemInstanceId instance)
        {
            Owner = owner;
            Slot = slot;
            Item = item;
            Instance = instance;
        }

        /// <inheritdoc />
        public override string ToString() => $"ItemEquipped({Item} in {Slot} on {Owner})";
    }

    /// <summary>Fact: an item was unequipped and its contributions revoked.</summary>
    [EventCategory(EventCategories.Equipment)]
    public readonly struct ItemUnequipped : IGameplayEvent
    {
        public readonly GameplayObjectId Owner;
        public readonly DefinitionId Slot;
        public readonly DefinitionId Item;
        public readonly ItemInstanceId Instance;

        public ItemUnequipped(GameplayObjectId owner, DefinitionId slot, DefinitionId item, ItemInstanceId instance)
        {
            Owner = owner;
            Slot = slot;
            Item = item;
            Instance = instance;
        }

        /// <inheritdoc />
        public override string ToString() => $"ItemUnequipped({Item} from {Slot} on {Owner})";
    }

    /// <summary>Fact: an equip attempt was rejected, with the first failing check as reason.</summary>
    [EventCategory(EventCategories.Equipment)]
    public readonly struct EquipFailed : IGameplayEvent
    {
        public readonly GameplayObjectId Owner;
        public readonly DefinitionId Slot;
        public readonly DefinitionId Item;
        public readonly EquipResult Reason;

        public EquipFailed(GameplayObjectId owner, DefinitionId slot, DefinitionId item, EquipResult reason)
        {
            Owner = owner;
            Slot = slot;
            Item = item;
            Reason = reason;
        }

        /// <inheritdoc />
        public override string ToString() => $"EquipFailed({Item} in {Slot} on {Owner}: {Reason})";
    }
}
