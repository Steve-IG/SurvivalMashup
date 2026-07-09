using System.Collections.Generic;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Scene;
using ToyChest.Systems.Equipment;
using ToyChest.Systems.Inventory;
using UnityEngine;

namespace ToyChest.Gameplay.Player
{
    /// <summary>
    /// The thin Unity adapter that lets the player equip and unequip from their own bag. It owns
    /// no gameplay rules; it is the "caller" the Equipment System expects (see
    /// Docs/Systems/EQUIPMENT.md) and delegates every decision to the pure
    /// <see cref="InventoryEquip"/> helper, which moves whole Item Instances between the player's
    /// <see cref="InventorySet"/> and <see cref="EquipmentSet"/>. All validation and contribution
    /// activation (attribute modifiers, granted tags, granted abilities, applied statuses) belong
    /// to the Equipment System; this adapter only turns an input press into a call.
    ///
    /// <see cref="Toggle"/> is a single-button demonstration: when nothing managed is equipped it
    /// equips everything it can from the bag; when anything is equipped it returns all managed
    /// items to the bag. The input adapter calls it; this component never reads the input device.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameplayObjectBehaviour))]
    public sealed class PlayerEquipmentController : MonoBehaviour, IGameplaySceneParticipant
    {
        [SerializeField]
        [Tooltip("The behaviour whose composed object owns the inventory and equipment. Defaults to the sibling.")]
        private GameplayObjectBehaviour _behaviour;

        [SerializeField]
        [Tooltip("Slots this controller equips into and unequips from, in priority order (Boots, Charm).")]
        private List<EquipmentSlotDefinition> _managedSlots = new List<EquipmentSlotDefinition>();

        private void Awake()
        {
            if (_behaviour == null)
            {
                _behaviour = GetComponent<GameplayObjectBehaviour>();
            }
        }

        /// <summary>No scene service is required; equip/unequip act on the composed object directly.</summary>
        public void OnGameplaySceneReady(GameplaySceneContext context)
        {
        }

        /// <summary>
        /// Toggles the managed slots: equips everything possible from the bag when nothing is
        /// equipped, otherwise unequips every managed item back to the bag. A no-op until an
        /// object with both an inventory and an equipment capability is bound and active.
        /// </summary>
        public void Toggle()
        {
            GameplayObject player = _behaviour != null ? _behaviour.Object : null;
            if (player == null || !player.IsActive
                || !player.TryGet(out InventorySet inventory)
                || !player.TryGet(out EquipmentSet equipment))
            {
                return;
            }

            if (AnyEquipped(equipment))
            {
                for (int i = 0; i < _managedSlots.Count; i++)
                {
                    EquipmentSlotDefinition slot = _managedSlots[i];
                    if (slot != null && equipment.HasSlot(slot.Id))
                    {
                        InventoryEquip.TryUnequipToInventory(inventory, equipment, slot.Id, out _);
                    }
                }
            }
            else
            {
                while (InventoryEquip.TryEquipFromInventory(inventory, equipment, _managedSlots, out _, out _))
                {
                }
            }
        }

        private bool AnyEquipped(EquipmentSet equipment)
        {
            for (int i = 0; i < _managedSlots.Count; i++)
            {
                EquipmentSlotDefinition slot = _managedSlots[i];
                if (slot != null && equipment.HasSlot(slot.Id) && equipment.IsEquipped(slot.Id))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
