using System.Collections.Generic;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Systems.Resources;
using ToyChest.Systems.StatusEffects;

namespace ToyChest.Gameplay.Player
{
    /// <summary>
    /// The pure, engine-agnostic core of respawn: it restores a downed Gameplay Object to a clean
    /// spawn state using only existing capability operations, so respawn is composed from the
    /// engine rather than owned by a new system. It refills every resource to its maximum (through
    /// the Resource System) and clears every active status (through the Status Effect System);
    /// it never touches the Unity transform — repositioning belongs to the scene-side controller.
    ///
    /// This is a caller helper, the respawn counterpart to <see cref="InventoryEquip"/>: no
    /// respawn/checkpoint manager exists, because reconstituting authoritative state is just a few
    /// existing operations applied in order. Fully unit-testable against a composed object.
    /// </summary>
    public static class PlayerRespawn
    {
        /// <summary>
        /// Restores <paramref name="subject"/> to full health and clears its status effects.
        /// A no-op for capabilities the object lacks. Safe to call outside a capability tick —
        /// the scene controller defers it to avoid mutating the status set mid-tick.
        /// </summary>
        public static void Restore(GameplayObject subject)
        {
            if (subject == null)
            {
                return;
            }

            if (subject.TryGet(out StatusEffectSet statuses))
            {
                ClearStatuses(statuses);
            }

            if (subject.TryGet(out ResourceSet resources))
            {
                RefillResources(resources);
            }
        }

        // Copy the active ids first: Remove mutates the live status list, so iterating it directly
        // while removing would skip entries.
        private static void ClearStatuses(StatusEffectSet statuses)
        {
            IReadOnlyList<StatusEffectInstance> active = statuses.ActiveStatuses;
            if (active.Count == 0)
            {
                return;
            }

            var ids = new List<DefinitionId>(active.Count);
            for (int i = 0; i < active.Count; i++)
            {
                ids.Add(active[i].Definition.Id);
            }

            for (int i = 0; i < ids.Count; i++)
            {
                statuses.Remove(ids[i]);
            }
        }

        private static void RefillResources(ResourceSet resources)
        {
            IReadOnlyList<ResourceValue> values = resources.Resources;
            for (int i = 0; i < values.Count; i++)
            {
                ResourceValue resource = values[i];
                resource.Restore(resource.Maximum);
            }
        }
    }
}
