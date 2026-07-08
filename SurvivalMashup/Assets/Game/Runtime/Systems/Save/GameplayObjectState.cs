using System;
using System.Collections.Generic;

namespace ToyChest.Systems.Save
{
    /// <summary>
    /// The authoritative persisted state of one Gameplay Object: its stable identity, the
    /// definition it was composed from (by stable id), and the authoritative leaf state each of
    /// its capabilities owns. The object graph itself is not stored — on load the composition
    /// root rebuilds the object from its definition and each system restores its leaf values on
    /// top (Engine Principle 25, Reconstruction Over Serialization).
    ///
    /// Only capabilities with authoritative state appear here. Attributes and tags carry none —
    /// attribute current values are derived from base plus re-applied modifiers, and tags are
    /// re-established by composition and by the systems that grant them — so neither is persisted.
    /// </summary>
    [Serializable]
    public sealed class GameplayObjectState
    {
        /// <summary>The object's stable persistent identity (<c>GameplayObjectId</c> string form).</summary>
        public string ObjectId;

        /// <summary>Stable id of the definition the object was composed from.</summary>
        public string DefinitionId;

        /// <summary>Resource current values. The maximum is derived and never stored.</summary>
        public List<ResourceState> Resources = new List<ResourceState>();

        /// <summary>Remaining cooldowns for abilities that are cooling down. Ready abilities are omitted.</summary>
        public List<AbilityCooldownState> Cooldowns = new List<AbilityCooldownState>();

        /// <summary>Active statuses with their duration, stacks, and periodic accumulator.</summary>
        public List<StatusEffectState> Statuses = new List<StatusEffectState>();

        /// <summary>Owned inventory stacks, in slot order.</summary>
        public List<InventoryStackState> Inventory = new List<InventoryStackState>();

        /// <summary>Equipped items, keyed by slot.</summary>
        public List<EquipmentSlotState> Equipment = new List<EquipmentSlotState>();
    }

    /// <summary>Authoritative state of one resource: its current value (Engine Principle 25).</summary>
    [Serializable]
    public struct ResourceState
    {
        public string ResourceId;
        public float Current;

        public ResourceState(string resourceId, float current)
        {
            ResourceId = resourceId;
            Current = current;
        }
    }

    /// <summary>Authoritative state of one cooling-down ability: its remaining cooldown.</summary>
    [Serializable]
    public struct AbilityCooldownState
    {
        public string AbilityId;
        public float CooldownRemaining;

        public AbilityCooldownState(string abilityId, float cooldownRemaining)
        {
            AbilityId = abilityId;
            CooldownRemaining = cooldownRemaining;
        }
    }

    /// <summary>Authoritative state of one active status: stacks, remaining duration, accumulator.</summary>
    [Serializable]
    public struct StatusEffectState
    {
        public string StatusId;
        public int Stacks;
        public float RemainingSeconds;
        public float PeriodAccumulator;

        public StatusEffectState(string statusId, int stacks, float remainingSeconds, float periodAccumulator)
        {
            StatusId = statusId;
            Stacks = stacks;
            RemainingSeconds = remainingSeconds;
            PeriodAccumulator = periodAccumulator;
        }
    }

    /// <summary>Authoritative state of one inventory stack: item definition, instance id, quantity.</summary>
    [Serializable]
    public struct InventoryStackState
    {
        public string ItemDefinitionId;
        public string InstanceId;
        public int Quantity;

        public InventoryStackState(string itemDefinitionId, string instanceId, int quantity)
        {
            ItemDefinitionId = itemDefinitionId;
            InstanceId = instanceId;
            Quantity = quantity;
        }
    }

    /// <summary>Authoritative state of one occupied equipment slot: which item instance fills it.</summary>
    [Serializable]
    public struct EquipmentSlotState
    {
        public string SlotId;
        public string ItemDefinitionId;
        public string InstanceId;

        public EquipmentSlotState(string slotId, string itemDefinitionId, string instanceId)
        {
            SlotId = slotId;
            ItemDefinitionId = itemDefinitionId;
            InstanceId = instanceId;
        }
    }
}
