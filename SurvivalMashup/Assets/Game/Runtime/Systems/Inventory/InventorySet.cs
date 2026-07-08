using System;
using System.Collections.Generic;
using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Objects;
using ToyChest.Systems.Items;

namespace ToyChest.Systems.Inventory
{
    /// <summary>
    /// The inventory capability of one gameplay object: the Item Instances it owns, organized
    /// as stacks under a slot-based capacity. Owns storage, stacking, capacity validation,
    /// movement, and ownership transfer; never implements item gameplay behavior — items
    /// become useful through Abilities, Equipment, or Gameplay Effects.
    /// Deterministic: stacks keep insertion order, merges and removals consume stacks in that
    /// order, and adds are all-or-nothing (a quantity that cannot fit in full is rejected).
    /// Composed by the factory when the object's definition declares an inventory.
    /// See Docs/Systems/INVENTORY_SYSTEM.md.
    /// </summary>
    public sealed class InventorySet : IGameplayCapability
    {
        private readonly List<ItemInstance> _stacks = new List<ItemInstance>();
        private readonly Dictionary<ItemInstanceId, ItemInstance> _byId =
            new Dictionary<ItemInstanceId, ItemInstance>();

        private readonly GameplayObjectId _owner;
        private readonly IEventBus _eventBus;

        /// <summary>Creates the capability.</summary>
        /// <param name="owner">Identity used in published events. May be default in isolated tests.</param>
        /// <param name="eventBus">Publishes inventory events; null runs silently.</param>
        /// <param name="slotCapacity">Number of stack slots. Must be at least 1.</param>
        public InventorySet(GameplayObjectId owner, IEventBus eventBus, int slotCapacity)
        {
            if (slotCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(slotCapacity), slotCapacity, "An inventory needs at least one slot.");
            }

            _owner = owner;
            _eventBus = eventBus;
            SlotCapacity = slotCapacity;
        }

        /// <summary>Number of stack slots.</summary>
        public int SlotCapacity { get; }

        /// <summary>Number of occupied slots.</summary>
        public int StackCount => _stacks.Count;

        /// <summary>Whether every slot is occupied. Stackable items may still merge into full inventories.</summary>
        public bool IsFull => _stacks.Count >= SlotCapacity;

        /// <summary>The owned stacks in insertion order. Presentation (sorting, grids) is a UI concern.</summary>
        public IReadOnlyList<ItemInstance> Stacks => _stacks;

        /// <summary>The stack with this instance id, or null when not owned here.</summary>
        public ItemInstance GetStack(ItemInstanceId stack)
        {
            return _byId.TryGetValue(stack, out ItemInstance instance) ? instance : null;
        }

        /// <summary>
        /// Places a persisted stack directly into the next free slot during load, preserving its
        /// instance id and quantity without merging (rehydration for the Save Framework).
        /// Distinct from <see cref="TryAdd"/>, which merges into like stacks and may discard the
        /// instance: restoration reproduces the saved layout exactly and publishes nothing, since
        /// reconstruction re-establishes state rather than replaying the gameplay that produced it.
        /// Throws on a duplicate id, an empty stack, or more stacks than slots (a corrupt save).
        /// </summary>
        public void RestoreStack(ItemInstance stack)
        {
            if (stack == null)
            {
                throw new ArgumentNullException(nameof(stack));
            }

            if (stack.Quantity < 1)
            {
                throw new ArgumentException(
                    $"Cannot restore an empty stack of '{stack.Definition.Id}'. Saved stacks always carry a positive quantity.",
                    nameof(stack));
            }

            if (_byId.ContainsKey(stack.Id))
            {
                throw new InvalidOperationException(
                    $"Stack '{stack.Id}' is already restored into this inventory. Each saved stack is restored once.");
            }

            if (_stacks.Count >= SlotCapacity)
            {
                throw new InvalidOperationException(
                    $"Cannot restore stack '{stack.Definition.Id}': the inventory already holds {SlotCapacity} stacks, its full slot capacity. " +
                    "The save holds more stacks than the definition's inventory can fit.");
            }

            _stacks.Add(stack);
            _byId.Add(stack.Id, stack);
        }

        /// <summary>Whether any stack of this item definition is owned here.</summary>
        public bool Contains(DefinitionId item)
        {
            for (int i = 0; i < _stacks.Count; i++)
            {
                if (_stacks[i].Definition.Id == item)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Total quantity of this item definition across all stacks.</summary>
        public int QuantityOf(DefinitionId item)
        {
            int total = 0;
            for (int i = 0; i < _stacks.Count; i++)
            {
                if (_stacks[i].Definition.Id == item)
                {
                    total += _stacks[i].Quantity;
                }
            }

            return total;
        }

        /// <summary>
        /// Whether <paramref name="quantity"/> of <paramref name="definition"/> fits in full,
        /// counting merge space in existing stacks plus free slots.
        /// </summary>
        public bool CanAdd(ItemDefinition definition, int quantity)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (quantity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be positive.");
            }

            long space = 0;
            for (int i = 0; i < _stacks.Count; i++)
            {
                if (_stacks[i].Definition.Id == definition.Id)
                {
                    space += definition.MaxStackSize - _stacks[i].Quantity;
                }
            }

            space += (long)(SlotCapacity - _stacks.Count) * definition.MaxStackSize;
            return space >= quantity;
        }

        /// <summary>
        /// Adds a stack, all-or-nothing. Merges into existing stacks of the same definition in
        /// insertion order (publishing <see cref="StackChanged"/> per merge), then places the
        /// remainder as a new stack; publishes one <see cref="ItemAdded"/> for the whole add.
        /// A quantity that cannot fit in full is rejected and publishes <see cref="InventoryFull"/>.
        /// A fully merged instance ends with quantity zero and is discarded by the caller.
        /// </summary>
        public bool TryAdd(ItemInstance stack)
        {
            if (stack == null)
            {
                throw new ArgumentNullException(nameof(stack));
            }

            if (_byId.ContainsKey(stack.Id))
            {
                throw new InvalidOperationException(
                    $"Stack '{stack.Id}' is already in this inventory. Use TrySplit or TryMerge for stack management.");
            }

            if (stack.Quantity < 1)
            {
                throw new ArgumentException(
                    $"Cannot add an empty stack of '{stack.Definition.Id}'. Empty instances are consumed, not stored.",
                    nameof(stack));
            }

            int quantity = stack.Quantity;
            if (!CanAdd(stack.Definition, quantity))
            {
                _eventBus?.Publish(new InventoryFull(_owner, stack.Definition.Id, quantity));
                return false;
            }

            int remaining = quantity;
            for (int i = 0; i < _stacks.Count && remaining > 0; i++)
            {
                ItemInstance existing = _stacks[i];
                if (existing.Definition.Id != stack.Definition.Id)
                {
                    continue;
                }

                int mergeable = Math.Min(existing.Definition.MaxStackSize - existing.Quantity, remaining);
                if (mergeable <= 0)
                {
                    continue;
                }

                existing.SetQuantity(existing.Quantity + mergeable);
                remaining -= mergeable;
                _eventBus?.Publish(new StackChanged(_owner, existing.Id, existing.Definition.Id, existing.Quantity));
            }

            if (remaining > 0)
            {
                stack.SetQuantity(remaining);
                _stacks.Add(stack);
                _byId.Add(stack.Id, stack);
            }
            else
            {
                stack.SetQuantity(0);
            }

            _eventBus?.Publish(new ItemAdded(_owner, stack.Definition.Id, quantity));
            return true;
        }

        /// <summary>
        /// Consumes a quantity of an item definition (crafting costs, ability ammo),
        /// all-or-nothing: fails without mutating when the total owned quantity is short.
        /// Consumes stacks in insertion order; emptied stacks leave the inventory, reduced
        /// stacks publish <see cref="StackChanged"/>, and one <see cref="ItemRemoved"/>
        /// reports the whole removal.
        /// </summary>
        public bool TryRemove(DefinitionId item, int quantity)
        {
            if (quantity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be positive.");
            }

            if (QuantityOf(item) < quantity)
            {
                return false;
            }

            int remaining = quantity;
            for (int i = 0; i < _stacks.Count && remaining > 0;)
            {
                ItemInstance stack = _stacks[i];
                if (stack.Definition.Id != item)
                {
                    i++;
                    continue;
                }

                int consumed = Math.Min(stack.Quantity, remaining);
                remaining -= consumed;

                if (consumed == stack.Quantity)
                {
                    stack.SetQuantity(0);
                    _stacks.RemoveAt(i);
                    _byId.Remove(stack.Id);
                }
                else
                {
                    stack.SetQuantity(stack.Quantity - consumed);
                    _eventBus?.Publish(new StackChanged(_owner, stack.Id, item, stack.Quantity));
                    i++;
                }
            }

            _eventBus?.Publish(new ItemRemoved(_owner, item, quantity));
            return true;
        }

        /// <summary>
        /// Takes a whole stack out of the inventory (equipping, dropping into the world),
        /// publishing <see cref="ItemRemoved"/>. Returns false when the stack is not owned here.
        /// </summary>
        public bool TryTakeStack(ItemInstanceId stack, out ItemInstance taken)
        {
            if (!_byId.TryGetValue(stack, out taken))
            {
                return false;
            }

            _stacks.Remove(taken);
            _byId.Remove(stack);
            _eventBus?.Publish(new ItemRemoved(_owner, taken.Definition.Id, taken.Quantity));
            return true;
        }

        /// <summary>
        /// Splits <paramref name="quantity"/> off a stack into a new stack in this inventory.
        /// Requires a free slot and a quantity strictly inside the stack. Net inventory
        /// quantity is unchanged, so only <see cref="StackChanged"/> facts are published.
        /// </summary>
        public bool TrySplit(ItemInstanceId stack, int quantity, out ItemInstance newStack)
        {
            newStack = null;
            if (!_byId.TryGetValue(stack, out ItemInstance source))
            {
                return false;
            }

            if (quantity < 1 || quantity >= source.Quantity || IsFull)
            {
                return false;
            }

            source.SetQuantity(source.Quantity - quantity);
            newStack = new ItemInstance(source.Definition, quantity);
            _stacks.Add(newStack);
            _byId.Add(newStack.Id, newStack);

            _eventBus?.Publish(new StackChanged(_owner, source.Id, source.Definition.Id, source.Quantity));
            _eventBus?.Publish(new StackChanged(_owner, newStack.Id, newStack.Definition.Id, newStack.Quantity));
            return true;
        }

        /// <summary>
        /// Merges as much of one stack as fits into another stack of the same definition.
        /// A fully merged source stack leaves the inventory. Net inventory quantity is
        /// unchanged, so only <see cref="StackChanged"/> facts are published.
        /// </summary>
        public bool TryMerge(ItemInstanceId from, ItemInstanceId into)
        {
            if (from == into
                || !_byId.TryGetValue(from, out ItemInstance source)
                || !_byId.TryGetValue(into, out ItemInstance target)
                || source.Definition.Id != target.Definition.Id)
            {
                return false;
            }

            int mergeable = Math.Min(target.Definition.MaxStackSize - target.Quantity, source.Quantity);
            if (mergeable <= 0)
            {
                return false;
            }

            target.SetQuantity(target.Quantity + mergeable);
            _eventBus?.Publish(new StackChanged(_owner, target.Id, target.Definition.Id, target.Quantity));

            if (mergeable == source.Quantity)
            {
                source.SetQuantity(0);
                _stacks.Remove(source);
                _byId.Remove(source.Id);
            }
            else
            {
                source.SetQuantity(source.Quantity - mergeable);
                _eventBus?.Publish(new StackChanged(_owner, source.Id, source.Definition.Id, source.Quantity));
            }

            return true;
        }

        /// <summary>
        /// Transfers a whole stack to another inventory, transactionally: the move happens in
        /// full or not at all. Publishes <see cref="ItemRemoved"/> here, <see cref="ItemAdded"/>
        /// there, then the <see cref="ItemTransferred"/> ownership fact. A destination without
        /// room publishes <see cref="InventoryFull"/> and the stack stays here.
        /// </summary>
        public bool TryTransferTo(InventorySet destination, ItemInstanceId stack)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            if (ReferenceEquals(destination, this))
            {
                throw new InvalidOperationException("Cannot transfer a stack to its own inventory. Use TrySplit or TryMerge.");
            }

            if (!_byId.TryGetValue(stack, out ItemInstance instance))
            {
                return false;
            }

            int quantity = instance.Quantity;
            if (!destination.CanAdd(instance.Definition, quantity))
            {
                destination._eventBus?.Publish(new InventoryFull(destination._owner, instance.Definition.Id, quantity));
                return false;
            }

            _stacks.Remove(instance);
            _byId.Remove(stack);
            _eventBus?.Publish(new ItemRemoved(_owner, instance.Definition.Id, quantity));

            destination.TryAdd(instance);
            _eventBus?.Publish(new ItemTransferred(_owner, destination._owner, instance.Definition.Id, quantity));
            return true;
        }
    }
}
