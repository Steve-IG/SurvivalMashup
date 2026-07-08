using ToyChest.Framework.Data;
using UnityEngine;

namespace ToyChest.Systems.Equipment
{
    /// <summary>
    /// Immutable authoring definition of one equipment slot (Primary Weapon, Helmet, Ring 1).
    /// Slot layouts are data: each Gameplay Object definition declares the slot list it
    /// carries, so a wolf's harness/charm layout and the player's ten-slot layout use the
    /// same system. An item declares which slots it fits by referencing these assets from
    /// its equippable component. See Docs/Systems/EQUIPMENT.md.
    /// </summary>
    [CreateAssetMenu(menuName = "ToyChest/Equipment/Equipment Slot Definition", fileName = "Slot_")]
    public sealed class EquipmentSlotDefinition : GameplayDefinition
    {
        [SerializeField]
        private string _displayName;

        /// <summary>Designer-facing display name.</summary>
        public string DisplayName => _displayName;
    }
}
