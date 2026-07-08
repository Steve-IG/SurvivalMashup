using System;

namespace ToyChest.Systems.Items
{
    /// <summary>
    /// Runtime state of one item stack (Engine Principle 14): a stable instance identity,
    /// the immutable definition it was created from, and its current quantity. Durability,
    /// charges, and affixes are future instance state and arrive as data, not subclasses.
    /// Quantity is owned by the Inventory System — stack management (merging, splitting,
    /// consuming) mutates it through <see cref="SetQuantity"/>; all other systems treat
    /// quantity as read-only. See Docs/Systems/ITEM_SYSTEM.md.
    /// </summary>
    public sealed class ItemInstance
    {
        /// <summary>Creates an instance of <paramref name="definition"/> with a fresh identity.</summary>
        /// <param name="definition">The immutable item definition. Required.</param>
        /// <param name="quantity">Initial quantity, from 1 to the definition's max stack size.</param>
        public ItemInstance(ItemDefinition definition, int quantity = 1)
            : this(definition, ItemInstanceId.New(), quantity)
        {
        }

        private ItemInstance(ItemDefinition definition, ItemInstanceId id, int quantity)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));

            if (quantity < 1 || quantity > definition.MaxStackSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantity), quantity,
                    $"Quantity of item '{definition.Id}' must be between 1 and its max stack size ({definition.MaxStackSize}).");
            }

            Id = id;
            Quantity = quantity;
        }

        /// <summary>
        /// Reconstructs a persisted stack with its original <paramref name="id"/> and
        /// <paramref name="quantity"/> (rehydration for the Save Framework). The public
        /// constructor mints a fresh identity; restoration preserves the saved instance id so
        /// references that crossed the save boundary stay stable (Engine Principle 21).
        /// </summary>
        public static ItemInstance Restore(ItemDefinition definition, ItemInstanceId id, int quantity)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException(
                    "Restore requires the persisted ItemInstanceId. Parse it from the save payload.", nameof(id));
            }

            return new ItemInstance(definition, id, quantity);
        }

        /// <summary>The instance's stable persistent identity.</summary>
        public ItemInstanceId Id { get; }

        /// <summary>The immutable item definition.</summary>
        public ItemDefinition Definition { get; }

        /// <summary>Current stack quantity. Zero only transiently, when a stack has been fully merged away.</summary>
        public int Quantity { get; private set; }

        /// <summary>
        /// Sets the stack quantity. Called only by the owning Inventory System during stack
        /// management; other systems never mutate quantity.
        /// </summary>
        public void SetQuantity(int quantity)
        {
            if (quantity < 0 || quantity > Definition.MaxStackSize)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantity), quantity,
                    $"Quantity of item '{Definition.Id}' must be between 0 and its max stack size ({Definition.MaxStackSize}).");
            }

            Quantity = quantity;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"ItemInstance({Definition.Id} x{Quantity}, {Id})";
        }
    }
}
