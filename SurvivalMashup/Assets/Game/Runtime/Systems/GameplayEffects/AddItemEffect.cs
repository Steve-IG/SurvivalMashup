using ToyChest.Systems.Inventory;
using ToyChest.Systems.Items;
using UnityEngine;

namespace ToyChest.Systems.GameplayEffects
{
    /// <summary>
    /// Atomic effect: add an authored quantity of one item to the target's inventory
    /// (loot a crate, harvest a resource, receive a quest reward). The item's behavior is
    /// never decided here — the Inventory System stores the stack, and other systems decide
    /// what the item does. Follows the Inventory System's all-or-nothing add: a quantity that
    /// cannot fit in full is rejected and nothing is stored.
    /// </summary>
    [CreateAssetMenu(menuName = "ToyChest/Effects/Add Item", fileName = "Fx_AddItem")]
    public sealed class AddItemEffect : GameplayEffectDefinition
    {
        [SerializeField]
        [Tooltip("The item definition to add to the target's inventory.")]
        private ItemDefinition _item;

        [SerializeField]
        [Min(1)]
        [Tooltip("How many of the item to add. Merged into existing stacks up to the item's max stack size.")]
        private int _quantity = 1;

        /// <inheritdoc />
        protected override void Execute(in EffectContext context)
        {
            InventorySet inventory = Require(context.Target.Inventory, "target");
            ItemDefinition item = Require(_item, "configured item");
            inventory.TryAdd(new ItemInstance(item, _quantity));
        }
    }
}
