using System.Collections.Generic;
using ToyChest.Framework.Data;
using ToyChest.Systems.Tags;
using UnityEngine;

namespace ToyChest.Systems.Items
{
    /// <summary>
    /// Immutable authoring definition of one item type (Iron Ore, Health Potion, Steel Sword).
    /// Pure configuration (Engine Principle 24): identity, stacking rule, descriptive tags,
    /// and the Definition Components that describe what the item is capable of. The engine
    /// never needs a SteelSword.cs — a sword is one of these assets composed with an
    /// equippable component. Gameplay systems (Inventory, Equipment, Crafting, Loot) decide
    /// how those capabilities are used. See Docs/Systems/ITEM_SYSTEM.md.
    /// </summary>
    [CreateAssetMenu(menuName = "ToyChest/Items/Item Definition", fileName = "Item_")]
    public sealed class ItemDefinition : GameplayDefinition
    {
        [SerializeField]
        private string _displayName;

        [SerializeField]
        [Tooltip("Organizational category for UI and filtering (Weapons, Consumables, Resources). Never affects behavior.")]
        private string _category;

        [SerializeField]
        [Tooltip("Descriptive tags (Fire, Legendary, Sword). Tags drive interactions with other systems.")]
        private List<TagDefinition> _tags = new List<TagDefinition>();

        [SerializeField]
        [Min(1)]
        [Tooltip("Maximum quantity of one stack. 1 = unstackable (equipment, companion eggs).")]
        private int _maxStackSize = 1;

        [SerializeField]
        [Tooltip("Definition Components composing the item's capabilities (equippable, consumable, ...).")]
        private List<ItemComponentDefinition> _components = new List<ItemComponentDefinition>();

        /// <summary>Designer-facing display name.</summary>
        public string DisplayName => _displayName;

        /// <summary>Organizational category. Organizational only; never affects behavior.</summary>
        public string Category => _category;

        /// <summary>Descriptive tags driving interactions and queries.</summary>
        public IReadOnlyList<TagDefinition> Tags => _tags;

        /// <summary>Maximum quantity of one stack; 1 means unstackable.</summary>
        public int MaxStackSize => _maxStackSize;

        /// <summary>The item's Definition Components.</summary>
        public IReadOnlyList<ItemComponentDefinition> Components => _components;

        /// <summary>Whether the item carries a component of the exact type.</summary>
        public bool HasComponent<TComponent>() where TComponent : ItemComponentDefinition
        {
            return TryGetComponent(out TComponent _);
        }

        /// <summary>
        /// The first component of the exact type, for capability probing by downstream
        /// systems (the Equipment System asks for the equippable component).
        /// </summary>
        public bool TryGetComponent<TComponent>(out TComponent component) where TComponent : ItemComponentDefinition
        {
            for (int i = 0; i < _components.Count; i++)
            {
                if (_components[i] is TComponent match)
                {
                    component = match;
                    return true;
                }
            }

            component = null;
            return false;
        }
    }
}
