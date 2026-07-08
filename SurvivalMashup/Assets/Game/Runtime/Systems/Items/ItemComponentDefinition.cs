using UnityEngine;

namespace ToyChest.Systems.Items
{
    /// <summary>
    /// Base of the item Definition Component model (Docs/Systems/ITEM_SYSTEM.md): an
    /// <see cref="ItemDefinition"/> is composed of reusable component assets rather than
    /// specialized subclasses. Each downstream system defines its own components in its own
    /// assembly (the Equipment System defines the equippable component, a future Consumable
    /// System defines the consumable component), so the Item System stays dependency-light
    /// and items gain capabilities through composition, never inheritance.
    /// Components are immutable authoring data; runtime state belongs to <see cref="ItemInstance"/>.
    /// </summary>
    public abstract class ItemComponentDefinition : ScriptableObject
    {
    }
}
