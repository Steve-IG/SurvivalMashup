using System;
using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Objects;
using ToyChest.Systems.Inventory;
using ToyChest.Systems.Items;
using ToyChest.Tests.Events;
using ToyChest.Tests.Items;

namespace ToyChest.Tests.Inventory
{
    /// <summary>
    /// Verifies inventory ownership rules: deterministic merge-then-place adds, all-or-nothing
    /// capacity validation, ordered consumption, stack splitting and merging, transactional
    /// transfer, and the inventory event facts.
    /// </summary>
    public sealed class InventorySetTests
    {
        private static readonly DefinitionId WoodId = new DefinitionId("item.wood");

        private ItemTestFactory _items;
        private EventBus _bus;
        private GameplayObjectId _owner;
        private InventorySet _inventory;
        private ItemDefinition _wood;
        private ItemDefinition _sword;

        [SetUp]
        public void SetUp()
        {
            _items = new ItemTestFactory();
            _bus = new EventBus(new RecordingLogger());
            _owner = GameplayObjectId.New();
            _inventory = new InventorySet(_owner, _bus, slotCapacity: 3);
            _wood = _items.CreateItem("item.wood", maxStackSize: 10);
            _sword = _items.CreateItem("item.sword", maxStackSize: 1);
        }

        [TearDown]
        public void TearDown()
        {
            _items.Cleanup();
        }

        // ---------------------------------------------------------------- Add

        [Test]
        public void TryAdd_PlacesNewStack_AndPublishesItemAdded()
        {
            var added = new List<ItemAdded>();
            using IDisposable token = _bus.Subscribe<ItemAdded>(added.Add);

            var stack = new ItemInstance(_wood, 7);
            Assert.IsTrue(_inventory.TryAdd(stack));

            Assert.AreEqual(1, _inventory.StackCount);
            Assert.AreSame(stack, _inventory.GetStack(stack.Id));
            Assert.AreEqual(7, _inventory.QuantityOf(WoodId));
            Assert.AreEqual(1, added.Count);
            Assert.AreEqual(_owner, added[0].Owner);
            Assert.AreEqual(7, added[0].Quantity);
        }

        [Test]
        public void TryAdd_MergesIntoExistingStacks_InInsertionOrder()
        {
            var changed = new List<StackChanged>();
            using IDisposable token = _bus.Subscribe<StackChanged>(changed.Add);

            var first = new ItemInstance(_wood, 8);
            _inventory.TryAdd(first);

            var incoming = new ItemInstance(_wood, 5);
            Assert.IsTrue(_inventory.TryAdd(incoming));

            Assert.AreEqual(10, first.Quantity, "The existing stack fills to its max first.");
            Assert.AreEqual(3, incoming.Quantity, "The remainder stays on the incoming instance as a new stack.");
            Assert.AreEqual(2, _inventory.StackCount);
            Assert.AreEqual(13, _inventory.QuantityOf(WoodId));
            Assert.AreEqual(1, changed.Count);
            Assert.AreEqual(first.Id, changed[0].Stack);
            Assert.AreEqual(10, changed[0].NewQuantity);
        }

        [Test]
        public void TryAdd_FullyMergedInstance_IsAbsorbed_NotStored()
        {
            var first = new ItemInstance(_wood, 5);
            _inventory.TryAdd(first);

            var incoming = new ItemInstance(_wood, 3);
            Assert.IsTrue(_inventory.TryAdd(incoming));

            Assert.AreEqual(1, _inventory.StackCount);
            Assert.AreEqual(0, incoming.Quantity, "A fully merged instance is emptied and discarded.");
            Assert.IsNull(_inventory.GetStack(incoming.Id));
        }

        [Test]
        public void TryAdd_BeyondCapacity_IsAllOrNothing_AndPublishesInventoryFull()
        {
            var full = new List<InventoryFull>();
            using IDisposable token = _bus.Subscribe<InventoryFull>(full.Add);

            _inventory.TryAdd(new ItemInstance(_sword));
            _inventory.TryAdd(new ItemInstance(_items.CreateItem("item.shield", maxStackSize: 1)));
            var wood = new ItemInstance(_wood, 8);
            _inventory.TryAdd(wood);

            // One free merge space of 2 remains (8/10) and zero free slots: 5 wood cannot fit in full.
            var overflow = new ItemInstance(_wood, 5);
            Assert.IsFalse(_inventory.TryAdd(overflow));

            Assert.AreEqual(5, overflow.Quantity, "A rejected add mutates nothing.");
            Assert.AreEqual(8, wood.Quantity, "A rejected add merges nothing.");
            Assert.AreEqual(1, full.Count);
            Assert.AreEqual(5, full[0].RejectedQuantity);
        }

        [Test]
        public void CanAdd_CountsMergeSpacePlusFreeSlots()
        {
            _inventory.TryAdd(new ItemInstance(_wood, 8));
            _inventory.TryAdd(new ItemInstance(_sword));

            Assert.IsTrue(_inventory.CanAdd(_wood, 12), "2 merge space + 1 free slot of 10.");
            Assert.IsFalse(_inventory.CanAdd(_wood, 13));
            Assert.IsFalse(_inventory.CanAdd(_sword, 2), "Unstackable items need one slot each.");
        }

        [Test]
        public void TryAdd_SameInstanceTwice_FailsClearly()
        {
            var stack = new ItemInstance(_sword);
            _inventory.TryAdd(stack);

            Assert.Throws<InvalidOperationException>(() => _inventory.TryAdd(stack));
        }

        // ---------------------------------------------------------------- Remove

        [Test]
        public void TryRemove_ConsumesStacksInInsertionOrder_AndPublishesFacts()
        {
            var removed = new List<ItemRemoved>();
            var changed = new List<StackChanged>();
            using IDisposable removedToken = _bus.Subscribe<ItemRemoved>(removed.Add);
            using IDisposable changedToken = _bus.Subscribe<StackChanged>(changed.Add);

            var first = new ItemInstance(_wood, 10);
            var second = new ItemInstance(_wood, 6);
            _inventory.TryAdd(first);
            _inventory.TryAdd(second);

            Assert.IsTrue(_inventory.TryRemove(WoodId, 12));

            Assert.AreEqual(4, _inventory.QuantityOf(WoodId));
            Assert.AreEqual(1, _inventory.StackCount, "The first stack empties and leaves the inventory.");
            Assert.IsNull(_inventory.GetStack(first.Id));
            Assert.AreEqual(4, second.Quantity);
            Assert.AreEqual(1, removed.Count);
            Assert.AreEqual(12, removed[0].Quantity);
            Assert.AreEqual(1, changed.Count);
            Assert.AreEqual(second.Id, changed[0].Stack);
        }

        [Test]
        public void TryRemove_InsufficientQuantity_IsAllOrNothing()
        {
            _inventory.TryAdd(new ItemInstance(_wood, 5));

            Assert.IsFalse(_inventory.TryRemove(WoodId, 6));
            Assert.AreEqual(5, _inventory.QuantityOf(WoodId), "A rejected removal consumes nothing.");
        }

        [Test]
        public void TryTakeStack_RemovesWholeStack_AndPublishesItemRemoved()
        {
            var removed = new List<ItemRemoved>();
            using IDisposable token = _bus.Subscribe<ItemRemoved>(removed.Add);
            var stack = new ItemInstance(_wood, 5);
            _inventory.TryAdd(stack);

            Assert.IsTrue(_inventory.TryTakeStack(stack.Id, out ItemInstance taken));

            Assert.AreSame(stack, taken);
            Assert.AreEqual(0, _inventory.StackCount);
            Assert.AreEqual(1, removed.Count);
            Assert.AreEqual(5, removed[0].Quantity);
            Assert.IsFalse(_inventory.TryTakeStack(stack.Id, out _));
        }

        // ---------------------------------------------------------------- Split / Merge

        [Test]
        public void TrySplit_CreatesNewStack_NetQuantityUnchanged()
        {
            var added = new List<ItemAdded>();
            using IDisposable token = _bus.Subscribe<ItemAdded>(added.Add);
            var stack = new ItemInstance(_wood, 10);
            _inventory.TryAdd(stack);
            added.Clear();

            Assert.IsTrue(_inventory.TrySplit(stack.Id, 4, out ItemInstance newStack));

            Assert.AreEqual(6, stack.Quantity);
            Assert.AreEqual(4, newStack.Quantity);
            Assert.AreEqual(2, _inventory.StackCount);
            Assert.AreEqual(10, _inventory.QuantityOf(WoodId));
            Assert.AreEqual(0, added.Count, "Splitting moves nothing across the inventory boundary.");
        }

        [Test]
        public void TrySplit_RequiresFreeSlot_AndInteriorQuantity()
        {
            var stack = new ItemInstance(_wood, 10);
            _inventory.TryAdd(stack);

            Assert.IsFalse(_inventory.TrySplit(stack.Id, 10, out _), "The whole stack is not a split.");
            Assert.IsFalse(_inventory.TrySplit(stack.Id, 0, out _));

            _inventory.TryAdd(new ItemInstance(_sword));
            _inventory.TryAdd(new ItemInstance(_items.CreateItem("item.shield", maxStackSize: 1)));
            Assert.IsFalse(_inventory.TrySplit(stack.Id, 4, out _), "A full inventory has no slot for the new stack.");
        }

        [Test]
        public void TryMerge_CombinesStacks_AndDropsEmptiedSource()
        {
            var stack = new ItemInstance(_wood, 10);
            _inventory.TryAdd(stack);
            _inventory.TrySplit(stack.Id, 4, out ItemInstance split);

            Assert.IsTrue(_inventory.TryMerge(split.Id, stack.Id));

            Assert.AreEqual(10, stack.Quantity);
            Assert.AreEqual(1, _inventory.StackCount);
            Assert.IsNull(_inventory.GetStack(split.Id));
        }

        [Test]
        public void TryMerge_RejectsMismatchedOrFullTargets()
        {
            var wood = new ItemInstance(_wood, 10);
            var sword = new ItemInstance(_sword);
            _inventory.TryAdd(wood);
            _inventory.TryAdd(sword);

            Assert.IsFalse(_inventory.TryMerge(sword.Id, wood.Id), "Different definitions never merge.");
            Assert.IsFalse(_inventory.TryMerge(wood.Id, wood.Id), "A stack cannot merge into itself.");

            _inventory.TrySplit(wood.Id, 4, out ItemInstance split);
            wood.SetQuantity(10);
            Assert.IsFalse(_inventory.TryMerge(split.Id, wood.Id), "A full target has no merge space.");
        }

        // ---------------------------------------------------------------- Transfer

        [Test]
        public void TryTransferTo_MovesStack_AndPublishesOwnershipFacts()
        {
            GameplayObjectId chestOwner = GameplayObjectId.New();
            var chest = new InventorySet(chestOwner, _bus, slotCapacity: 2);
            var transferred = new List<ItemTransferred>();
            using IDisposable token = _bus.Subscribe<ItemTransferred>(transferred.Add);

            var stack = new ItemInstance(_wood, 7);
            _inventory.TryAdd(stack);

            Assert.IsTrue(_inventory.TryTransferTo(chest, stack.Id));

            Assert.AreEqual(0, _inventory.QuantityOf(WoodId));
            Assert.AreEqual(7, chest.QuantityOf(WoodId));
            Assert.AreEqual(1, transferred.Count);
            Assert.AreEqual(_owner, transferred[0].FromOwner);
            Assert.AreEqual(chestOwner, transferred[0].ToOwner);
            Assert.AreEqual(7, transferred[0].Quantity);
        }

        [Test]
        public void TryTransferTo_FullDestination_IsTransactional()
        {
            var pouch = new InventorySet(GameplayObjectId.New(), _bus, slotCapacity: 1);
            pouch.TryAdd(new ItemInstance(_sword));

            var stack = new ItemInstance(_wood, 7);
            _inventory.TryAdd(stack);

            Assert.IsFalse(_inventory.TryTransferTo(pouch, stack.Id));
            Assert.AreEqual(7, _inventory.QuantityOf(WoodId), "A rejected transfer leaves the stack with its owner.");
        }

        [Test]
        public void TryTransferTo_SelfOrNull_FailsClearly()
        {
            var stack = new ItemInstance(_wood, 7);
            _inventory.TryAdd(stack);

            Assert.Throws<ArgumentNullException>(() => _inventory.TryTransferTo(null, stack.Id));
            Assert.Throws<InvalidOperationException>(() => _inventory.TryTransferTo(_inventory, stack.Id));
        }

        // ---------------------------------------------------------------- Construction / queries

        [Test]
        public void Constructor_RequiresAtLeastOneSlot()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new InventorySet(_owner, _bus, 0));
        }

        [Test]
        public void Queries_ReportContentsAcrossStacks()
        {
            _inventory.TryAdd(new ItemInstance(_wood, 10));
            _inventory.TryAdd(new ItemInstance(_wood, 3));

            Assert.IsTrue(_inventory.Contains(WoodId));
            Assert.IsFalse(_inventory.Contains(_sword.Id));
            Assert.AreEqual(13, _inventory.QuantityOf(WoodId));
            Assert.AreEqual(0, _inventory.QuantityOf(_sword.Id));
            Assert.AreEqual(2, _inventory.Stacks.Count);
            Assert.IsFalse(_inventory.IsFull);
        }
    }
}
