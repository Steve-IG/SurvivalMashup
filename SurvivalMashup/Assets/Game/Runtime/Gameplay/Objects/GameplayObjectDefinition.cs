using System.Collections.Generic;
using ToyChest.Framework.Data;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.Attributes;
using ToyChest.Systems.Equipment;
using ToyChest.Systems.Interactions;
using ToyChest.Systems.Inventory;
using ToyChest.Systems.Resources;
using ToyChest.Systems.Tags;
using UnityEngine;

namespace ToyChest.Gameplay.Objects
{
    /// <summary>
    /// Immutable authoring definition of one Gameplay Object type (Player, Wolf, Chest, Tree).
    /// Declares, as data, which capabilities the object is composed of: its attributes,
    /// resources, and initial gameplay tags. Composing a new entity type is authoring one of
    /// these assets, not writing a class. Uses explicit typed capability fields per the
    /// approved Milestone 0 decision; a generic capability-descriptor model is deferred until
    /// real usage justifies it. See Docs/Systems/GAMEPLAY_FRAMEWORK.md.
    /// </summary>
    [CreateAssetMenu(menuName = "ToyChest/Objects/Gameplay Object Definition", fileName = "Obj_")]
    public sealed class GameplayObjectDefinition : GameplayDefinition
    {
        [SerializeField]
        [Tooltip("Designer-facing name, e.g. Forest Wolf.")]
        private string _displayName;

        [SerializeField]
        [Tooltip("Attributes composed onto the object. Order is irrelevant; the factory adds all attributes before any resource.")]
        private List<AttributeDefinition> _attributes = new List<AttributeDefinition>();

        [SerializeField]
        [Tooltip("Resources composed onto the object. Attribute-bound maximums resolve against this object's attributes.")]
        private List<ResourceDefinition> _resources = new List<ResourceDefinition>();

        [SerializeField]
        [Tooltip("Tags present from spawn. State tags added by other systems at runtime do not belong here.")]
        private List<TagDefinition> _initialTags = new List<TagDefinition>();

        [SerializeField]
        [Tooltip("Abilities granted from spawn. Runtime grants (progression, equipment) do not belong here.")]
        private List<AbilityDefinition> _abilities = new List<AbilityDefinition>();

        [SerializeField]
        [Tooltip("Inventory configuration, or none. Only objects that own items (players, chests, merchants) declare one.")]
        private InventoryDefinition _inventory;

        [SerializeField]
        [Tooltip("Equipment slot layout, or empty for objects that equip nothing. Slots must be distinct (Ring 1, Ring 2).")]
        private List<EquipmentSlotDefinition> _equipmentSlots = new List<EquipmentSlotDefinition>();

        [SerializeField]
        [Tooltip("Interactions the object advertises (Open, Harvest, Talk). Their abilities are granted at composition.")]
        private List<InteractionDefinition> _interactions = new List<InteractionDefinition>();

        /// <summary>Designer-facing display name.</summary>
        public string DisplayName => _displayName;

        /// <summary>Attributes composed onto the object.</summary>
        public IReadOnlyList<AttributeDefinition> Attributes => _attributes;

        /// <summary>Resources composed onto the object.</summary>
        public IReadOnlyList<ResourceDefinition> Resources => _resources;

        /// <summary>Tags present from spawn.</summary>
        public IReadOnlyList<TagDefinition> InitialTags => _initialTags;

        /// <summary>Abilities granted from spawn.</summary>
        public IReadOnlyList<AbilityDefinition> Abilities => _abilities;

        /// <summary>Inventory configuration, or null when the object owns no items.</summary>
        public InventoryDefinition Inventory => _inventory;

        /// <summary>Equipment slot layout; empty when the object equips nothing.</summary>
        public IReadOnlyList<EquipmentSlotDefinition> EquipmentSlots => _equipmentSlots;

        /// <summary>Interactions the object advertises; empty when it is not interactable.</summary>
        public IReadOnlyList<InteractionDefinition> Interactions => _interactions;
    }
}
