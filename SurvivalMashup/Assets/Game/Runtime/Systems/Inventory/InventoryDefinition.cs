using ToyChest.Framework.Data;
using UnityEngine;

namespace ToyChest.Systems.Inventory
{
    /// <summary>
    /// Immutable authoring definition of one inventory configuration (Player Backpack,
    /// Small Chest, Merchant Stock). Milestone 0 uses the approved slot-based capacity
    /// model; weight, volume, and hybrid capacity models are future extensions that add
    /// configuration here without changing the Inventory System's API.
    /// See Docs/Systems/INVENTORY_SYSTEM.md.
    /// </summary>
    [CreateAssetMenu(menuName = "ToyChest/Inventory/Inventory Definition", fileName = "Inventory_")]
    public sealed class InventoryDefinition : GameplayDefinition
    {
        [SerializeField]
        private string _displayName;

        [SerializeField]
        [Min(1)]
        [Tooltip("Number of stack slots the inventory holds.")]
        private int _slotCapacity = 20;

        /// <summary>Designer-facing display name.</summary>
        public string DisplayName => _displayName;

        /// <summary>Number of stack slots.</summary>
        public int SlotCapacity => _slotCapacity;
    }
}
