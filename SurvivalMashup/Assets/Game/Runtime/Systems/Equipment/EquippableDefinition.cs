using System.Collections.Generic;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.Items;
using ToyChest.Systems.StatusEffects;
using ToyChest.Systems.Tags;
using UnityEngine;

namespace ToyChest.Systems.Equipment
{
    /// <summary>
    /// The item Definition Component that makes an item equippable: which slots it fits,
    /// what the owner must satisfy, and the contributions activated while equipped.
    /// Contributions are pure composition of existing systems — tags, attribute modifiers,
    /// ability grants, and status effects — the Equipment System activates and revokes them
    /// but implements none of their behavior. See Docs/Systems/EQUIPMENT.md.
    /// </summary>
    [CreateAssetMenu(menuName = "ToyChest/Items/Equippable Component", fileName = "Equippable_")]
    public sealed class EquippableDefinition : ItemComponentDefinition
    {
        [SerializeField]
        [Tooltip("Slots the item fits. A ring lists Ring 1 and Ring 2.")]
        private List<EquipmentSlotDefinition> _allowedSlots = new List<EquipmentSlotDefinition>();

        [SerializeField]
        [Tooltip("Owner tags that must all be present (hierarchical match) to equip. Empty = no requirement.")]
        private List<TagDefinition> _requiredOwnerTags = new List<TagDefinition>();

        [Header("Contributions")]
        [SerializeField]
        [Tooltip("Tags granted to the owner while equipped (FireWeapon, Heavy).")]
        private List<TagDefinition> _grantedTags = new List<TagDefinition>();

        [SerializeField]
        [Tooltip("Attribute modifiers active while equipped, revoked exactly on unequip (+10 Strength).")]
        private List<AttributeModifierConfig> _attributeModifiers = new List<AttributeModifierConfig>();

        [SerializeField]
        [Tooltip("Abilities granted while equipped (Fire Sword grants Flame Slash).")]
        private List<AbilityDefinition> _grantedAbilities = new List<AbilityDefinition>();

        [SerializeField]
        [Tooltip("Status effects applied while equipped (Life Regeneration). Skipped when already active from another source.")]
        private List<StatusEffectDefinition> _appliedStatusEffects = new List<StatusEffectDefinition>();

        /// <summary>Slots the item fits.</summary>
        public IReadOnlyList<EquipmentSlotDefinition> AllowedSlots => _allowedSlots;

        /// <summary>Owner tags that must all be present to equip.</summary>
        public IReadOnlyList<TagDefinition> RequiredOwnerTags => _requiredOwnerTags;

        /// <summary>Tags granted while equipped.</summary>
        public IReadOnlyList<TagDefinition> GrantedTags => _grantedTags;

        /// <summary>Attribute modifiers active while equipped.</summary>
        public IReadOnlyList<AttributeModifierConfig> AttributeModifiers => _attributeModifiers;

        /// <summary>Abilities granted while equipped.</summary>
        public IReadOnlyList<AbilityDefinition> GrantedAbilities => _grantedAbilities;

        /// <summary>Status effects applied while equipped.</summary>
        public IReadOnlyList<StatusEffectDefinition> AppliedStatusEffects => _appliedStatusEffects;
    }
}
