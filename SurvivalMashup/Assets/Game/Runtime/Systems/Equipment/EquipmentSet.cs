using System;
using System.Collections.Generic;
using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Modifiers;
using ToyChest.Framework.Objects;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.Attributes;
using ToyChest.Systems.GameplayEffects;
using ToyChest.Systems.Items;
using ToyChest.Systems.StatusEffects;
using ToyChest.Systems.Tags;

namespace ToyChest.Systems.Equipment
{
    /// <summary>
    /// The equipment capability of one gameplay object: its slot layout, the Item Instances
    /// equipped in them, and the activation of their contributions. Pure orchestration —
    /// contributions are activated through the systems that own them (tags through the tag
    /// container, attribute modifiers through the Modifier Framework with the equipped
    /// instance as the revocable source, abilities through the Ability System, statuses
    /// through the Status Effect System); no gameplay logic lives here. Equip changes are
    /// transactional: validation completes before any contribution activates, so a rejected
    /// equip mutates nothing. Composed by the factory with its sibling capabilities wired at
    /// construction (Capability Independence: no runtime sibling lookup).
    /// See Docs/Systems/EQUIPMENT.md.
    /// </summary>
    public sealed class EquipmentSet : IGameplayCapability
    {
        private sealed class EquippedEntry
        {
            public ItemInstance Item;
            public EquippableDefinition Equippable;

            /// <summary>Abilities this equip actually granted (already-granted abilities are skipped and never revoked).</summary>
            public readonly List<DefinitionId> GrantedAbilities = new List<DefinitionId>();

            /// <summary>Statuses this equip actually applied (already-active statuses are skipped and never removed).</summary>
            public readonly List<DefinitionId> AppliedStatuses = new List<DefinitionId>();
        }

        private readonly List<EquipmentSlotDefinition> _slots = new List<EquipmentSlotDefinition>();
        private readonly Dictionary<DefinitionId, EquipmentSlotDefinition> _slotsById =
            new Dictionary<DefinitionId, EquipmentSlotDefinition>();

        private readonly Dictionary<DefinitionId, EquippedEntry> _equipped =
            new Dictionary<DefinitionId, EquippedEntry>();

        private readonly GameplayObjectId _owner;
        private readonly IEventBus _eventBus;
        private readonly GameplayTagTable _tagTable;
        private readonly EffectTarget _ownerTarget;
        private readonly AbilitySet _abilities;
        private readonly StatusEffectSet _statuses;

        /// <summary>
        /// Creates the capability. Sibling capabilities the contributions activate against are
        /// wired here, at composition time, per the Capability Independence guideline.
        /// </summary>
        /// <param name="owner">Identity used in published events. May be default in isolated tests.</param>
        /// <param name="eventBus">Publishes equipment events; null runs silently.</param>
        /// <param name="tagTable">Resolves authored tag paths.</param>
        /// <param name="slots">The owner's slot layout. Slot ids must be distinct.</param>
        /// <param name="ownerTarget">The owner's capabilities that contributions activate against.</param>
        /// <param name="abilities">The owner's ability capability, receiving ability grants.</param>
        /// <param name="statuses">The owner's status capability, receiving applied statuses.</param>
        public EquipmentSet(
            GameplayObjectId owner,
            IEventBus eventBus,
            GameplayTagTable tagTable,
            IReadOnlyList<EquipmentSlotDefinition> slots,
            EffectTarget ownerTarget,
            AbilitySet abilities,
            StatusEffectSet statuses)
        {
            if (slots == null)
            {
                throw new ArgumentNullException(nameof(slots));
            }

            _owner = owner;
            _eventBus = eventBus;
            _tagTable = tagTable ?? throw new ArgumentNullException(nameof(tagTable));
            _ownerTarget = ownerTarget;
            _abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));
            _statuses = statuses ?? throw new ArgumentNullException(nameof(statuses));

            for (int i = 0; i < slots.Count; i++)
            {
                EquipmentSlotDefinition slot = slots[i];
                if (slot == null)
                {
                    throw new ArgumentException("Slot list contains null. Check the object definition's equipment slots.", nameof(slots));
                }

                if (_slotsById.ContainsKey(slot.Id))
                {
                    throw new ArgumentException(
                        $"Duplicate equipment slot '{slot.Id}'. Distinct slots need distinct definitions (Ring 1, Ring 2).",
                        nameof(slots));
                }

                _slots.Add(slot);
                _slotsById.Add(slot.Id, slot);
            }
        }

        /// <summary>The owner's slot layout, in declared order.</summary>
        public IReadOnlyList<EquipmentSlotDefinition> Slots => _slots;

        /// <summary>Number of slots holding an item.</summary>
        public int EquippedCount => _equipped.Count;

        /// <summary>Whether the owner has a slot with this id.</summary>
        public bool HasSlot(DefinitionId slot) => _slotsById.ContainsKey(slot);

        /// <summary>Whether the slot holds an item. Throws for an unknown slot.</summary>
        public bool IsEquipped(DefinitionId slot)
        {
            RequireSlot(slot);
            return _equipped.ContainsKey(slot);
        }

        /// <summary>The item equipped in the slot, or null when the slot is empty. Throws for an unknown slot.</summary>
        public ItemInstance GetEquipped(DefinitionId slot)
        {
            RequireSlot(slot);
            return _equipped.TryGetValue(slot, out EquippedEntry entry) ? entry.Item : null;
        }

        /// <summary>Whether any slot holds an instance of this item definition.</summary>
        public bool HasEquippedItem(DefinitionId item)
        {
            foreach (KeyValuePair<DefinitionId, EquippedEntry> pair in _equipped)
            {
                if (pair.Value.Item.Definition.Id == item)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Validates an equip without committing anything: no contributions, no events.
        /// For UI availability and AI evaluation.
        /// </summary>
        public EquipResult CanEquip(ItemInstance item, DefinitionId slot)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            return Validate(item, slot, out _);
        }

        /// <summary>
        /// Equips an item into a slot. On success activates the equippable component's
        /// contributions in order — tags, attribute modifiers, ability grants, statuses —
        /// and publishes <see cref="ItemEquipped"/>. On rejection publishes
        /// <see cref="EquipFailed"/> with the first failing check and mutates nothing.
        /// The caller moves the instance between inventory and equipment; the Equipment
        /// System never reaches into inventories.
        /// </summary>
        public EquipResult TryEquip(ItemInstance item, DefinitionId slot)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            EquipResult result = Validate(item, slot, out EquippableDefinition equippable);
            if (result != EquipResult.Equipped)
            {
                _eventBus?.Publish(new EquipFailed(_owner, slot, item.Definition.Id, result));
                return result;
            }

            RequireContributionCapabilities(equippable, item);

            var entry = new EquippedEntry { Item = item, Equippable = equippable };

            foreach (TagDefinition granted in equippable.GrantedTags)
            {
                _ownerTarget.Tags.AddTag(_tagTable.GetTag(granted.TagPath));
            }

            foreach (AttributeModifierConfig config in equippable.AttributeModifiers)
            {
                _ownerTarget.Attributes.GetAttribute(new DefinitionId(config.AttributeId))
                    .AddModifier(new Modifier(config.Operation, config.Value, item));
            }

            foreach (AbilityDefinition ability in equippable.GrantedAbilities)
            {
                if (!_abilities.Has(ability.Id))
                {
                    _abilities.Grant(ability);
                    entry.GrantedAbilities.Add(ability.Id);
                }
            }

            foreach (StatusEffectDefinition status in equippable.AppliedStatusEffects)
            {
                if (!_statuses.Has(status.Id))
                {
                    _statuses.Apply(status);
                    entry.AppliedStatuses.Add(status.Id);
                }
            }

            _equipped.Add(slot, entry);
            _eventBus?.Publish(new ItemEquipped(_owner, slot, item.Definition.Id, item.Id));
            return EquipResult.Equipped;
        }

        /// <summary>
        /// Unequips the slot's item, revoking contributions in reverse order — statuses,
        /// ability grants, attribute modifiers, tags — and publishes
        /// <see cref="ItemUnequipped"/>. Returns false when the slot is empty.
        /// Throws for an unknown slot.
        /// </summary>
        public bool TryUnequip(DefinitionId slot, out ItemInstance item)
        {
            RequireSlot(slot);

            if (!_equipped.TryGetValue(slot, out EquippedEntry entry))
            {
                item = null;
                return false;
            }

            for (int i = 0; i < entry.AppliedStatuses.Count; i++)
            {
                _statuses.Remove(entry.AppliedStatuses[i]);
            }

            for (int i = 0; i < entry.GrantedAbilities.Count; i++)
            {
                _abilities.Revoke(entry.GrantedAbilities[i]);
            }

            foreach (AttributeModifierConfig config in entry.Equippable.AttributeModifiers)
            {
                _ownerTarget.Attributes.GetAttribute(new DefinitionId(config.AttributeId))?.RemoveModifiersFrom(entry.Item);
            }

            foreach (TagDefinition granted in entry.Equippable.GrantedTags)
            {
                _ownerTarget.Tags.RemoveTag(_tagTable.GetTag(granted.TagPath));
            }

            _equipped.Remove(slot);
            item = entry.Item;
            _eventBus?.Publish(new ItemUnequipped(_owner, slot, item.Definition.Id, item.Id));
            return true;
        }

        private EquipResult Validate(ItemInstance item, DefinitionId slot, out EquippableDefinition equippable)
        {
            if (!item.Definition.TryGetComponent(out equippable))
            {
                return EquipResult.NotEquippable;
            }

            if (!_slotsById.ContainsKey(slot))
            {
                return EquipResult.UnknownSlot;
            }

            bool slotAllowed = false;
            IReadOnlyList<EquipmentSlotDefinition> allowed = equippable.AllowedSlots;
            for (int i = 0; i < allowed.Count; i++)
            {
                if (allowed[i].Id == slot)
                {
                    slotAllowed = true;
                    break;
                }
            }

            if (!slotAllowed)
            {
                return EquipResult.SlotNotAllowed;
            }

            if (_equipped.ContainsKey(slot))
            {
                return EquipResult.SlotOccupied;
            }

            IReadOnlyList<TagDefinition> required = equippable.RequiredOwnerTags;
            for (int i = 0; i < required.Count; i++)
            {
                if (_ownerTarget.Tags == null || !_ownerTarget.Tags.HasTag(_tagTable.GetTag(required[i].TagPath)))
                {
                    return EquipResult.MissingRequiredTag;
                }
            }

            return EquipResult.Equipped;
        }

        /// <summary>
        /// Fails fast, before any mutation, when the owner lacks a capability the item's
        /// contributions need — a configuration error, kept out of the transactional commit.
        /// </summary>
        private void RequireContributionCapabilities(EquippableDefinition equippable, ItemInstance item)
        {
            if (equippable.GrantedTags.Count > 0 && _ownerTarget.Tags == null)
            {
                throw new InvalidOperationException(
                    $"Item '{item.Definition.Id}' grants tags, but the owner has no tag container. Check the owner's object definition.");
            }

            foreach (AttributeModifierConfig config in equippable.AttributeModifiers)
            {
                AttributeValue attribute = _ownerTarget.Attributes?.GetAttribute(new DefinitionId(config.AttributeId));
                if (attribute == null)
                {
                    throw new InvalidOperationException(
                        $"Item '{item.Definition.Id}' modifies attribute '{config.AttributeId}', which the owner lacks. " +
                        "Check the owner's object definition or the item's modifier list.");
                }
            }
        }

        private void RequireSlot(DefinitionId slot)
        {
            if (!_slotsById.ContainsKey(slot))
            {
                throw new ArgumentException(
                    $"The owner has no equipment slot '{slot}'. Check the object definition's slot layout.", nameof(slot));
            }
        }
    }
}
