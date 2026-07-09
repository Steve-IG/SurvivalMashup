using ToyChest.Framework.Objects;
using ToyChest.Systems.Attributes;
using ToyChest.Systems.Inventory;
using ToyChest.Systems.Resources;
using ToyChest.Systems.Tags;

namespace ToyChest.Systems.GameplayEffects
{
    /// <summary>
    /// The capability view of one participant in an effect execution.
    /// Effects mutate capabilities, never objects directly, so the context carries this view
    /// rather than requiring a live Gameplay Object — which lets the Status Effect System
    /// execute periodic effects against sibling capabilities wired at composition time.
    /// Any capability may be null; each atomic effect fails clearly when a capability it
    /// requires is absent.
    /// </summary>
    public readonly struct EffectTarget
    {
        /// <summary>The participant's resources, or null.</summary>
        public readonly ResourceSet Resources;

        /// <summary>The participant's attributes, or null.</summary>
        public readonly AttributeSet Attributes;

        /// <summary>The participant's tag container, or null.</summary>
        public readonly GameplayTagContainer Tags;

        /// <summary>The participant's inventory, or null.</summary>
        public readonly InventorySet Inventory;

        /// <summary>Builds a target from explicit capabilities (composition-time wiring, tests).</summary>
        public EffectTarget(ResourceSet resources, AttributeSet attributes, GameplayTagContainer tags)
            : this(resources, attributes, tags, null)
        {
        }

        /// <summary>
        /// Builds a target from explicit capabilities including an inventory
        /// (composition-time wiring, tests). An inventory-carrying view lets atomic Inventory
        /// effects (Add Item) mutate exactly the participant the invoker wired in.
        /// </summary>
        public EffectTarget(ResourceSet resources, AttributeSet attributes, GameplayTagContainer tags, InventorySet inventory)
        {
            Resources = resources;
            Attributes = attributes;
            Tags = tags;
            Inventory = inventory;
        }

        /// <summary>Builds a target from a composed Gameplay Object's capabilities.</summary>
        public static EffectTarget From(GameplayObject gameplayObject)
        {
            if (gameplayObject == null)
            {
                return default;
            }

            gameplayObject.TryGet(out ResourceSet resources);
            gameplayObject.TryGet(out AttributeSet attributes);
            gameplayObject.TryGet(out GameplayTagContainer tags);
            gameplayObject.TryGet(out InventorySet inventory);
            return new EffectTarget(resources, attributes, tags, inventory);
        }
    }
}
