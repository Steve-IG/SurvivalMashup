using System;
using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Modifiers;
using ToyChest.Framework.Objects;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.Attributes;
using ToyChest.Systems.Equipment;
using ToyChest.Systems.GameplayEffects;
using ToyChest.Systems.Items;
using ToyChest.Systems.StatusEffects;
using ToyChest.Systems.Tags;
using ToyChest.Tests.Abilities;
using ToyChest.Tests.Attributes;
using ToyChest.Tests.Effects;
using ToyChest.Tests.Events;
using ToyChest.Tests.Items;

namespace ToyChest.Tests.Equipment
{
    /// <summary>
    /// Verifies equipment orchestration: the deterministic equip validation order, activation
    /// and exact revocation of every contribution kind (tags, attribute modifiers, ability
    /// grants, statuses) through the systems that own them, transactional rejection, and the
    /// equipment event facts.
    /// </summary>
    public sealed class EquipmentSetTests
    {
        private const float Tolerance = 1e-4f;
        private static readonly DefinitionId StrengthId = new DefinitionId("attribute.strength");
        private static readonly DefinitionId WeaponSlotId = new DefinitionId("slot.weapon");

        private ItemTestFactory _items;
        private AttributeTestFactory _attributes;
        private EffectTestFactory _effects;
        private AbilityTestFactory _abilities;

        private GameplayTagTable _tagTable;
        private EventBus _bus;
        private GameplayObjectId _owner;

        private AttributeSet _ownerAttributes;
        private GameplayTagContainer _ownerTags;
        private AbilitySet _ownerAbilities;
        private StatusEffectSet _ownerStatuses;
        private EquipmentSlotDefinition _weaponSlot;
        private EquipmentSet _equipment;

        [SetUp]
        public void SetUp()
        {
            _items = new ItemTestFactory();
            _attributes = new AttributeTestFactory();
            _effects = new EffectTestFactory();
            _abilities = new AbilityTestFactory();

            _tagTable = new GameplayTagTable();
            _bus = new EventBus(new RecordingLogger());
            _owner = GameplayObjectId.New();

            _ownerAttributes = new AttributeSet();
            _ownerAttributes.AddAttribute(_attributes.Create("attribute.strength", 10f));
            _ownerTags = new GameplayTagContainer(_tagTable);
            var ownerTarget = new EffectTarget(null, _ownerAttributes, _ownerTags);
            var runner = new GameplayEffectRunner();
            _ownerAbilities = new AbilitySet(_owner, _bus, _tagTable, runner, ownerTarget);
            _ownerStatuses = new StatusEffectSet(_owner, _bus, _tagTable, runner, ownerTarget);

            _weaponSlot = _items.CreateSlot("slot.weapon");
            _equipment = new EquipmentSet(
                _owner, _bus, _tagTable, new[] { _weaponSlot }, ownerTarget, _ownerAbilities, _ownerStatuses);
        }

        [TearDown]
        public void TearDown()
        {
            _items.Cleanup();
            _attributes.Cleanup();
            _effects.Cleanup();
            _abilities.Cleanup();
        }

        private ItemInstance CreateSword(
            TagDefinition[] requiredOwnerTags = null,
            TagDefinition[] grantedTags = null,
            AttributeModifierConfig[] modifiers = null,
            AbilityDefinition[] grantedAbilities = null,
            StatusEffectDefinition[] statuses = null,
            EquipmentSlotDefinition[] allowedSlots = null)
        {
            EquippableDefinition equippable = _items.CreateEquippable(
                allowedSlots ?? new[] { _weaponSlot },
                requiredOwnerTags: requiredOwnerTags,
                grantedTags: grantedTags,
                attributeModifiers: modifiers,
                grantedAbilities: grantedAbilities,
                appliedStatusEffects: statuses);
            ItemDefinition sword = _items.CreateItem(
                "item.sword", components: new ItemComponentDefinition[] { equippable });
            return new ItemInstance(sword);
        }

        // ---------------------------------------------------------------- Validation

        [Test]
        public void TryEquip_NotEquippableItem_IsRejected_AndPublishesEquipFailed()
        {
            var failed = new List<EquipFailed>();
            using IDisposable token = _bus.Subscribe<EquipFailed>(failed.Add);
            var wood = new ItemInstance(_items.CreateItem("item.wood", maxStackSize: 10), 5);

            Assert.AreEqual(EquipResult.NotEquippable, _equipment.TryEquip(wood, WeaponSlotId));
            Assert.AreEqual(1, failed.Count);
            Assert.AreEqual(EquipResult.NotEquippable, failed[0].Reason);
        }

        [Test]
        public void TryEquip_UnknownSlot_IsRejected()
        {
            Assert.AreEqual(EquipResult.UnknownSlot, _equipment.TryEquip(CreateSword(), new DefinitionId("slot.tail")));
        }

        [Test]
        public void TryEquip_DisallowedSlot_IsRejected()
        {
            EquipmentSlotDefinition helmet = _items.CreateSlot("slot.helmet");
            var equipment = new EquipmentSet(
                _owner, _bus, _tagTable, new[] { _weaponSlot, helmet },
                new EffectTarget(null, _ownerAttributes, _ownerTags), _ownerAbilities, _ownerStatuses);

            Assert.AreEqual(
                EquipResult.SlotNotAllowed,
                equipment.TryEquip(CreateSword(), helmet.Id),
                "A sword authored for the weapon slot never fits the helmet slot.");
        }

        [Test]
        public void TryEquip_OccupiedSlot_IsRejected()
        {
            Assert.AreEqual(EquipResult.Equipped, _equipment.TryEquip(CreateSword(), WeaponSlotId));
            Assert.AreEqual(EquipResult.SlotOccupied, _equipment.TryEquip(CreateSword(), WeaponSlotId));
        }

        [Test]
        public void TryEquip_MissingRequiredOwnerTag_IsRejected_ThenSucceedsWhenPresent()
        {
            _tagTable.RegisterTag("Class.Warrior");
            TagDefinition warrior = _effects.CreateTag("Class.Warrior");
            ItemInstance sword = CreateSword(requiredOwnerTags: new[] { warrior });

            Assert.AreEqual(EquipResult.MissingRequiredTag, _equipment.TryEquip(sword, WeaponSlotId));

            _ownerTags.AddTag(_tagTable.GetTag("Class.Warrior"));
            Assert.AreEqual(EquipResult.Equipped, _equipment.TryEquip(sword, WeaponSlotId));
        }

        [Test]
        public void CanEquip_Validates_WithoutCommittingOrPublishing()
        {
            var failed = new List<EquipFailed>();
            using IDisposable token = _bus.Subscribe<EquipFailed>(failed.Add);
            ItemInstance sword = CreateSword();

            Assert.AreEqual(EquipResult.Equipped, _equipment.CanEquip(sword, WeaponSlotId));
            Assert.IsFalse(_equipment.IsEquipped(WeaponSlotId), "A query equips nothing.");
            Assert.AreEqual(0, failed.Count, "A query is not an attempt; only TryEquip publishes failures.");
        }

        // ---------------------------------------------------------------- Contributions

        [Test]
        public void TryEquip_ActivatesEveryContribution_AndPublishesItemEquipped()
        {
            var equipped = new List<ItemEquipped>();
            using IDisposable token = _bus.Subscribe<ItemEquipped>(equipped.Add);
            _tagTable.RegisterTag("Item.FireWeapon");
            AbilityDefinition slash = _abilities.CreateAbility("ability.flame-slash");
            StatusEffectDefinition regen = _effects.CreateStatus(
                "status.regeneration", durationType: StatusDurationType.Infinite);

            ItemInstance sword = CreateSword(
                grantedTags: new[] { _effects.CreateTag("Item.FireWeapon") },
                modifiers: new[] { new AttributeModifierConfig("attribute.strength", ModifierOperation.Flat, 5f) },
                grantedAbilities: new[] { slash },
                statuses: new[] { regen });

            Assert.AreEqual(EquipResult.Equipped, _equipment.TryEquip(sword, WeaponSlotId));

            Assert.IsTrue(_ownerTags.HasTagExact(_tagTable.GetTag("Item.FireWeapon")));
            Assert.AreEqual(15f, _ownerAttributes.GetAttribute(StrengthId).CurrentValue, Tolerance);
            Assert.IsTrue(_ownerAttributes.GetAttribute(StrengthId).HasModifierFrom(sword),
                "Modifiers register under the equipped instance as the revocable source.");
            Assert.IsTrue(_ownerAbilities.Has(slash.Id));
            Assert.IsTrue(_ownerStatuses.Has(regen.Id));
            Assert.AreSame(sword, _equipment.GetEquipped(WeaponSlotId));
            Assert.AreEqual(1, equipped.Count);
            Assert.AreEqual(WeaponSlotId, equipped[0].Slot);
            Assert.AreEqual(sword.Id, equipped[0].Instance);
        }

        [Test]
        public void TryUnequip_RevokesEveryContribution_AndReturnsTheItem()
        {
            var unequipped = new List<ItemUnequipped>();
            using IDisposable token = _bus.Subscribe<ItemUnequipped>(unequipped.Add);
            _tagTable.RegisterTag("Item.FireWeapon");
            AbilityDefinition slash = _abilities.CreateAbility("ability.flame-slash");
            StatusEffectDefinition regen = _effects.CreateStatus(
                "status.regeneration", durationType: StatusDurationType.Infinite);

            ItemInstance sword = CreateSword(
                grantedTags: new[] { _effects.CreateTag("Item.FireWeapon") },
                modifiers: new[] { new AttributeModifierConfig("attribute.strength", ModifierOperation.Flat, 5f) },
                grantedAbilities: new[] { slash },
                statuses: new[] { regen });
            _equipment.TryEquip(sword, WeaponSlotId);

            Assert.IsTrue(_equipment.TryUnequip(WeaponSlotId, out ItemInstance returned));

            Assert.AreSame(sword, returned);
            Assert.IsFalse(_ownerTags.HasTagExact(_tagTable.GetTag("Item.FireWeapon")));
            Assert.AreEqual(10f, _ownerAttributes.GetAttribute(StrengthId).CurrentValue, Tolerance);
            Assert.IsFalse(_ownerAbilities.Has(slash.Id));
            Assert.IsFalse(_ownerStatuses.Has(regen.Id));
            Assert.IsFalse(_equipment.IsEquipped(WeaponSlotId));
            Assert.AreEqual(1, unequipped.Count);
            Assert.AreEqual(sword.Id, unequipped[0].Instance);
        }

        [Test]
        public void TryEquip_AbilityAlreadyGranted_IsSkipped_AndSurvivesUnequip()
        {
            AbilityDefinition slash = _abilities.CreateAbility("ability.flame-slash");
            _ownerAbilities.Grant(slash);
            ItemInstance sword = CreateSword(grantedAbilities: new[] { slash });

            _equipment.TryEquip(sword, WeaponSlotId);
            _equipment.TryUnequip(WeaponSlotId, out _);

            Assert.IsTrue(_ownerAbilities.Has(slash.Id),
                "An ability the item did not grant (progression granted it) is never revoked by unequip.");
        }

        [Test]
        public void TryEquip_StatusAlreadyActive_IsSkipped_AndSurvivesUnequip()
        {
            StatusEffectDefinition regen = _effects.CreateStatus(
                "status.regeneration", durationType: StatusDurationType.Infinite);
            _ownerStatuses.Apply(regen);
            ItemInstance sword = CreateSword(statuses: new[] { regen });

            _equipment.TryEquip(sword, WeaponSlotId);
            _equipment.TryUnequip(WeaponSlotId, out _);

            Assert.IsTrue(_ownerStatuses.Has(regen.Id),
                "A status the item did not apply is never removed by unequip.");
        }

        [Test]
        public void SharedGrantedTags_AreReferenceCounted_AcrossItems()
        {
            _tagTable.RegisterTag("Item.FireWeapon");
            EquipmentSlotDefinition offHand = _items.CreateSlot("slot.offhand");
            var equipment = new EquipmentSet(
                _owner, _bus, _tagTable, new[] { _weaponSlot, offHand },
                new EffectTarget(null, _ownerAttributes, _ownerTags), _ownerAbilities, _ownerStatuses);

            ItemInstance sword = CreateSword(grantedTags: new[] { _effects.CreateTag("Item.FireWeapon") });
            ItemInstance dagger = CreateSword(
                grantedTags: new[] { _effects.CreateTag("Item.FireWeapon") },
                allowedSlots: new[] { offHand });

            equipment.TryEquip(sword, WeaponSlotId);
            equipment.TryEquip(dagger, offHand.Id);
            equipment.TryUnequip(WeaponSlotId, out _);

            Assert.IsTrue(_ownerTags.HasTagExact(_tagTable.GetTag("Item.FireWeapon")),
                "The tag persists while any equipped item still grants it.");

            equipment.TryUnequip(offHand.Id, out _);
            Assert.IsFalse(_ownerTags.HasTagExact(_tagTable.GetTag("Item.FireWeapon")));
        }

        [Test]
        public void TryEquip_ModifierForMissingAttribute_FailsFast_BeforeAnyMutation()
        {
            _tagTable.RegisterTag("Item.FireWeapon");
            ItemInstance sword = CreateSword(
                grantedTags: new[] { _effects.CreateTag("Item.FireWeapon") },
                modifiers: new[] { new AttributeModifierConfig("attribute.luck", ModifierOperation.Flat, 5f) });

            Assert.Throws<InvalidOperationException>(() => _equipment.TryEquip(sword, WeaponSlotId));
            Assert.IsFalse(_ownerTags.HasTagExact(_tagTable.GetTag("Item.FireWeapon")),
                "The configuration error is detected before any contribution activates.");
            Assert.IsFalse(_equipment.IsEquipped(WeaponSlotId));
        }

        [Test]
        public void MultiSlotItem_FitsEitherAllowedSlot()
        {
            EquipmentSlotDefinition ring1 = _items.CreateSlot("slot.ring1");
            EquipmentSlotDefinition ring2 = _items.CreateSlot("slot.ring2");
            var equipment = new EquipmentSet(
                _owner, _bus, _tagTable, new[] { ring1, ring2 },
                new EffectTarget(null, _ownerAttributes, _ownerTags), _ownerAbilities, _ownerStatuses);

            EquippableDefinition band = _items.CreateEquippable(
                new[] { ring1, ring2 },
                attributeModifiers: new[] { new AttributeModifierConfig("attribute.strength", ModifierOperation.Flat, 2f) });
            ItemDefinition ring = _items.CreateItem("item.ring", components: new ItemComponentDefinition[] { band });

            Assert.AreEqual(EquipResult.Equipped, equipment.TryEquip(new ItemInstance(ring), ring1.Id));
            Assert.AreEqual(EquipResult.Equipped, equipment.TryEquip(new ItemInstance(ring), ring2.Id));
            Assert.AreEqual(14f, _ownerAttributes.GetAttribute(StrengthId).CurrentValue, Tolerance,
                "Two instances contribute independently: each is its own modifier source.");
            Assert.IsTrue(equipment.HasEquippedItem(ring.Id));
        }

        // ---------------------------------------------------------------- Construction / queries

        [Test]
        public void Constructor_DuplicateSlots_FailClearly()
        {
            Assert.Throws<ArgumentException>(() => new EquipmentSet(
                _owner, _bus, _tagTable, new[] { _weaponSlot, _weaponSlot },
                new EffectTarget(null, _ownerAttributes, _ownerTags), _ownerAbilities, _ownerStatuses));
        }

        [Test]
        public void SlotQueries_ThrowForUnknownSlots()
        {
            var unknown = new DefinitionId("slot.tail");
            Assert.IsFalse(_equipment.HasSlot(unknown));
            Assert.Throws<ArgumentException>(() => _equipment.IsEquipped(unknown));
            Assert.Throws<ArgumentException>(() => _equipment.GetEquipped(unknown));
            Assert.Throws<ArgumentException>(() => _equipment.TryUnequip(unknown, out _));
        }

        [Test]
        public void TryUnequip_EmptySlot_ReturnsFalse()
        {
            Assert.IsFalse(_equipment.TryUnequip(WeaponSlotId, out ItemInstance item));
            Assert.IsNull(item);
        }
    }
}
