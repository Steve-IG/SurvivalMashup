using NUnit.Framework;
using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Player;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.Attributes;
using ToyChest.Systems.Equipment;
using ToyChest.Systems.GameplayEffects;
using ToyChest.Systems.Inventory;
using ToyChest.Systems.Items;
using ToyChest.Systems.StatusEffects;
using ToyChest.Systems.Tags;
using ToyChest.Tests.Events;
using ToyChest.Tests.Items;

namespace ToyChest.Tests.Player
{
    /// <summary>
    /// Verifies the player's equip caller (<see cref="InventoryEquip"/>) — the pure logic that
    /// moves whole Item Instances between an inventory and an equipment set. It owns no gameplay
    /// rules; these tests confirm it delegates validation to the Equipment System, respects slot
    /// occupancy, and never destroys an item when there is no room to receive it.
    /// </summary>
    public sealed class InventoryEquipTests
    {
        private ItemTestFactory _items;
        private GameplayTagTable _tagTable;
        private EventBus _bus;
        private GameplayObjectId _owner;

        private InventorySet _inventory;
        private EquipmentSet _equipment;
        private EquipmentSlotDefinition _boots;
        private EquipmentSlotDefinition _charm;

        [SetUp]
        public void SetUp()
        {
            _items = new ItemTestFactory();
            _tagTable = new GameplayTagTable();
            _bus = new EventBus(new RecordingLogger());
            _owner = GameplayObjectId.New();

            var attributes = new AttributeSet();
            var tags = new GameplayTagContainer(_tagTable);
            var ownerTarget = new EffectTarget(null, attributes, tags);
            var runner = new GameplayEffectRunner();
            var abilities = new AbilitySet(_owner, _bus, _tagTable, runner, ownerTarget);
            var statuses = new StatusEffectSet(_owner, _bus, _tagTable, runner, ownerTarget);

            _boots = _items.CreateSlot("slot.boots");
            _charm = _items.CreateSlot("slot.charm");
            _inventory = new InventorySet(_owner, _bus, 8);
            _equipment = new EquipmentSet(
                _owner, _bus, _tagTable, new[] { _boots, _charm }, ownerTarget, abilities, statuses);
        }

        [TearDown]
        public void TearDown() => _items.Cleanup();

        private ItemInstance BootsItem() => Equippable("item.boots", _boots);
        private ItemInstance CharmItem() => Equippable("item.charm", _charm);

        private ItemInstance Equippable(string id, EquipmentSlotDefinition slot)
        {
            EquippableDefinition component = _items.CreateEquippable(new[] { slot });
            ItemDefinition definition = _items.CreateItem(id, components: new ItemComponentDefinition[] { component });
            return new ItemInstance(definition);
        }

        [Test]
        public void Equip_MovesFittingStackFromInventoryIntoItsSlot()
        {
            ItemInstance boots = BootsItem();
            _inventory.TryAdd(boots);

            bool equipped = InventoryEquip.TryEquipFromInventory(
                _inventory, _equipment, new[] { _boots, _charm }, out DefinitionId slot, out ItemInstance moved);

            Assert.IsTrue(equipped);
            Assert.AreEqual(_boots.Id, slot);
            Assert.AreSame(boots, moved);
            Assert.AreSame(boots, _equipment.GetEquipped(_boots.Id), "The instance now lives in the slot.");
            Assert.AreEqual(0, _inventory.StackCount, "The equipped stack left the inventory.");
        }

        [Test]
        public void Equip_SkipsOccupiedSlots_AndItemsThatFitNoManagedSlot()
        {
            _equipment.TryEquip(BootsItem(), _boots.Id);
            _inventory.TryAdd(BootsItem()); // a second boots fits only the already-occupied boots slot

            bool equipped = InventoryEquip.TryEquipFromInventory(
                _inventory, _equipment, new[] { _boots, _charm }, out _, out _);

            Assert.IsFalse(equipped, "The only fitting slot is occupied, so nothing is equipped.");
            Assert.AreEqual(1, _inventory.StackCount, "The un-equippable stack stays in the inventory.");
        }

        [Test]
        public void Equip_FillsEachManagedSlotFromTheBag()
        {
            _inventory.TryAdd(BootsItem());
            _inventory.TryAdd(CharmItem());

            Assert.IsTrue(InventoryEquip.TryEquipFromInventory(_inventory, _equipment, new[] { _boots, _charm }, out _, out _));
            Assert.IsTrue(InventoryEquip.TryEquipFromInventory(_inventory, _equipment, new[] { _boots, _charm }, out _, out _));
            Assert.IsFalse(InventoryEquip.TryEquipFromInventory(_inventory, _equipment, new[] { _boots, _charm }, out _, out _));

            Assert.IsTrue(_equipment.IsEquipped(_boots.Id));
            Assert.IsTrue(_equipment.IsEquipped(_charm.Id));
            Assert.AreEqual(0, _inventory.StackCount);
        }

        [Test]
        public void Unequip_ReturnsTheItemToTheInventory()
        {
            ItemInstance boots = BootsItem();
            _inventory.TryAdd(boots);
            InventoryEquip.TryEquipFromInventory(_inventory, _equipment, new[] { _boots }, out _, out _);

            bool unequipped = InventoryEquip.TryUnequipToInventory(_inventory, _equipment, _boots.Id, out ItemInstance moved);

            Assert.IsTrue(unequipped);
            Assert.AreSame(boots, moved);
            Assert.IsFalse(_equipment.IsEquipped(_boots.Id));
            Assert.AreEqual(1, _inventory.StackCount, "The item is back in the bag.");
        }

        [Test]
        public void Unequip_EmptyOrUnknownSlot_ReturnsFalse()
        {
            Assert.IsFalse(InventoryEquip.TryUnequipToInventory(_inventory, _equipment, _boots.Id, out _),
                "An empty slot has nothing to return.");
            Assert.IsFalse(InventoryEquip.TryUnequipToInventory(_inventory, _equipment, new DefinitionId("slot.tail"), out _),
                "An unknown slot is not managed by this equipment set.");
        }

        [Test]
        public void Unequip_WithNoInventoryRoom_LeavesTheItemEquipped()
        {
            var full = new InventorySet(_owner, _bus, 1);
            full.TryAdd(Equippable("item.filler", _charm)); // one slot, one stack -> full
            _equipment.TryEquip(BootsItem(), _boots.Id);

            bool unequipped = InventoryEquip.TryUnequipToInventory(full, _equipment, _boots.Id, out _);

            Assert.IsFalse(unequipped, "Unequipping never destroys an item: no room means it stays equipped.");
            Assert.IsTrue(_equipment.IsEquipped(_boots.Id));
        }
    }
}
