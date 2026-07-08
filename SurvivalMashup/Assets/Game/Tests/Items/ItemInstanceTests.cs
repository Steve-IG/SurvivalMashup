using System;
using NUnit.Framework;
using ToyChest.Systems.Items;

namespace ToyChest.Tests.Items
{
    /// <summary>
    /// Verifies the item runtime model: instance identity, quantity bounds owned by the
    /// definition's stacking rule, and component queries on definitions.
    /// </summary>
    public sealed class ItemInstanceTests
    {
        private ItemTestFactory _items;

        [SetUp]
        public void SetUp()
        {
            _items = new ItemTestFactory();
        }

        [TearDown]
        public void TearDown()
        {
            _items.Cleanup();
        }

        [Test]
        public void Constructor_AssignsFreshValidIdentity()
        {
            ItemDefinition wood = _items.CreateItem("item.wood", maxStackSize: 999);

            var first = new ItemInstance(wood, 10);
            var second = new ItemInstance(wood, 10);

            Assert.IsTrue(first.Id.IsValid);
            Assert.AreNotEqual(first.Id, second.Id, "Every instance gets its own stable identity.");
            Assert.AreSame(wood, first.Definition);
            Assert.AreEqual(10, first.Quantity);
        }

        [Test]
        public void Constructor_NullDefinition_FailsClearly()
        {
            Assert.Throws<ArgumentNullException>(() => new ItemInstance(null));
        }

        [Test]
        public void Constructor_QuantityOutsideStackBounds_FailsClearly()
        {
            ItemDefinition potion = _items.CreateItem("item.potion", maxStackSize: 20);

            Assert.Throws<ArgumentOutOfRangeException>(() => new ItemInstance(potion, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ItemInstance(potion, 21));
        }

        [Test]
        public void SetQuantity_EnforcesStackBounds()
        {
            ItemDefinition potion = _items.CreateItem("item.potion", maxStackSize: 20);
            var stack = new ItemInstance(potion, 5);

            stack.SetQuantity(20);
            Assert.AreEqual(20, stack.Quantity);

            stack.SetQuantity(0);
            Assert.AreEqual(0, stack.Quantity, "Zero marks a fully merged-away stack.");

            Assert.Throws<ArgumentOutOfRangeException>(() => stack.SetQuantity(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => stack.SetQuantity(21));
        }

        [Test]
        public void ItemInstanceId_RoundTripsThroughStringForm()
        {
            ItemInstanceId id = ItemInstanceId.New();

            Assert.IsTrue(ItemInstanceId.TryParse(id.ToString(), out ItemInstanceId parsed));
            Assert.AreEqual(id, parsed);
            Assert.IsFalse(ItemInstanceId.TryParse("not-an-id", out _));
            Assert.IsFalse(default(ItemInstanceId).IsValid);
        }

        [Test]
        public void TryGetComponent_FindsFirstOfType_AndReportsAbsence()
        {
            ItemDefinition sword = _items.CreateItem(
                "item.sword",
                components: new ItemComponentDefinition[] { _items.CreateEquippable(new[] { _items.CreateSlot("slot.weapon") }) });
            ItemDefinition wood = _items.CreateItem("item.wood", maxStackSize: 999);

            Assert.IsTrue(sword.HasComponent<ToyChest.Systems.Equipment.EquippableDefinition>());
            Assert.IsTrue(sword.TryGetComponent(out ToyChest.Systems.Equipment.EquippableDefinition equippable));
            Assert.IsNotNull(equippable);
            Assert.IsFalse(wood.HasComponent<ToyChest.Systems.Equipment.EquippableDefinition>());
        }
    }
}
