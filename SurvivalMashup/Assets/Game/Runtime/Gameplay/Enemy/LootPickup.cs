using ToyChest.Framework.Objects;
using ToyChest.Systems.Inventory;
using ToyChest.Systems.Items;
using UnityEngine;

namespace ToyChest.Gameplay.Enemy
{
    /// <summary>
    /// The thin Unity adapter for a dropped loot pickup: when the player walks into its trigger, it
    /// adds an authored item to the player's inventory through the existing inventory pipeline
    /// (<see cref="InventorySet.TryAdd"/> with an <see cref="ItemInstance"/> — the same add the
    /// loot-crate effect uses) and then removes itself. What the item does is never decided here;
    /// the Inventory System stores the stack. It is the pickup counterpart to <c>HazardVolume</c>:
    /// a trigger bridging a Unity contact to one existing gameplay operation, not a loot framework.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LootPickup : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Unity tag identifying the player who may collect this drop.")]
        private string _playerTag = "Player";

        [SerializeField]
        [Tooltip("The item added to the collector's inventory.")]
        private ItemDefinition _item;

        [SerializeField]
        [Min(1)]
        [Tooltip("How many of the item to add.")]
        private int _quantity = 1;

        private bool _collected;

        private void OnTriggerEnter(Collider other)
        {
            if (_collected || _item == null || other == null || !other.CompareTag(_playerTag))
            {
                return;
            }

            var behaviour = other.GetComponentInParent<GameplayObjectBehaviour>();
            GameplayObject player = behaviour != null ? behaviour.Object : null;
            if (player == null || !player.IsActive || !player.TryGet(out InventorySet inventory))
            {
                return;
            }

            if (inventory.TryAdd(new ItemInstance(_item, _quantity)))
            {
                _collected = true;
                Destroy(gameObject);
            }
        }
    }
}
