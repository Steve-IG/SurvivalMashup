using System.Collections.Generic;
using ToyChest.Framework.Data;
using ToyChest.Systems.Equipment;
using ToyChest.Systems.Inventory;
using ToyChest.Systems.Items;

namespace ToyChest.Gameplay.Player
{
    /// <summary>
    /// The player's equip "caller": pure, deterministic orchestration that moves whole Item
    /// Instances between an <see cref="InventorySet"/> and an <see cref="EquipmentSet"/>. The
    /// Equipment System never reaches into inventories (Capability Independence, see
    /// Docs/Systems/EQUIPMENT.md), so equipping and unequipping from a bag is a caller concern —
    /// interaction, UI, or, here, the player's input adapter. No gameplay logic lives here:
    /// slot validation and contribution activation belong to the Equipment System, storage and
    /// stacking to the Inventory System. This type only decides which instance moves where.
    /// Kept free of Unity types so it is unit-testable without a scene.
    /// </summary>
    public static class InventoryEquip
    {
        /// <summary>
        /// Equips the first inventory stack that fits the first empty managed slot, moving the
        /// whole stack out of the inventory and into the slot through the Equipment System.
        /// Validates with <see cref="EquipmentSet.CanEquip"/> before removing anything, so a
        /// rejected candidate leaves the inventory untouched. Managed slots are considered in
        /// order; within a slot, inventory stacks are considered in insertion order — fully
        /// deterministic. Returns false when nothing can be equipped.
        /// </summary>
        public static bool TryEquipFromInventory(
            InventorySet inventory,
            EquipmentSet equipment,
            IReadOnlyList<EquipmentSlotDefinition> managedSlots,
            out DefinitionId slot,
            out ItemInstance equipped)
        {
            slot = default;
            equipped = null;
            if (inventory == null || equipment == null || managedSlots == null)
            {
                return false;
            }

            for (int s = 0; s < managedSlots.Count; s++)
            {
                EquipmentSlotDefinition candidateSlot = managedSlots[s];
                if (candidateSlot == null || !equipment.HasSlot(candidateSlot.Id) || equipment.IsEquipped(candidateSlot.Id))
                {
                    continue;
                }

                IReadOnlyList<ItemInstance> stacks = inventory.Stacks;
                for (int i = 0; i < stacks.Count; i++)
                {
                    ItemInstance stack = stacks[i];
                    if (!Fits(stack, candidateSlot) || equipment.CanEquip(stack, candidateSlot.Id) != EquipResult.Equipped)
                    {
                        continue;
                    }

                    // Validation passed: take the exact stack out of the bag, then equip it.
                    if (!inventory.TryTakeStack(stack.Id, out ItemInstance taken))
                    {
                        continue;
                    }

                    if (equipment.TryEquip(taken, candidateSlot.Id) == EquipResult.Equipped)
                    {
                        slot = candidateSlot.Id;
                        equipped = taken;
                        return true;
                    }

                    // Defensive: CanEquip agreed but TryEquip did not (e.g. a contribution
                    // capability check). Return the instance so no item is lost, then move on.
                    inventory.TryAdd(taken);
                }
            }

            return false;
        }

        /// <summary>
        /// Unequips the item in <paramref name="slot"/> and returns it to the inventory, all or
        /// nothing: if the inventory has no room the item stays equipped and this returns false,
        /// so unequipping never destroys an item. Contributions are revoked by the Equipment
        /// System as part of the unequip. Returns false when the slot is empty or unknown.
        /// </summary>
        public static bool TryUnequipToInventory(
            InventorySet inventory, EquipmentSet equipment, DefinitionId slot, out ItemInstance unequipped)
        {
            unequipped = null;
            if (inventory == null || equipment == null || !equipment.HasSlot(slot) || !equipment.IsEquipped(slot))
            {
                return false;
            }

            ItemInstance held = equipment.GetEquipped(slot);
            if (held == null || !inventory.CanAdd(held.Definition, held.Quantity))
            {
                return false;
            }

            if (!equipment.TryUnequip(slot, out ItemInstance item))
            {
                return false;
            }

            if (!inventory.TryAdd(item))
            {
                // CanAdd agreed but TryAdd did not: re-equip so the item is never lost.
                equipment.TryEquip(item, slot);
                return false;
            }

            unequipped = item;
            return true;
        }

        private static bool Fits(ItemInstance stack, EquipmentSlotDefinition slot)
        {
            if (stack == null || slot == null || !stack.Definition.TryGetComponent(out EquippableDefinition equippable))
            {
                return false;
            }

            IReadOnlyList<EquipmentSlotDefinition> allowed = equippable.AllowedSlots;
            for (int i = 0; i < allowed.Count; i++)
            {
                if (allowed[i] != null && allowed[i].Id == slot.Id)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
