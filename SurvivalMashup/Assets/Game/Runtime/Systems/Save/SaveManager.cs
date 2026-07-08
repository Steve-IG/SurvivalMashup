using System;
using System.Collections.Generic;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.Equipment;
using ToyChest.Systems.Inventory;
using ToyChest.Systems.Items;
using ToyChest.Systems.Resources;
using ToyChest.Systems.StatusEffects;
using UnityEngine;

namespace ToyChest.Systems.Save
{
    /// <summary>
    /// Coordinates persistence for the running simulation: captures the authoritative state of
    /// every live Gameplay Object into a <see cref="SaveData"/> payload, and restores a payload
    /// by reconstructing each object through the composition root and handing its authoritative
    /// state back to the systems that own it.
    ///
    /// The Save System coordinates; it never owns or interprets gameplay state
    /// (Docs/Architecture/CORE_ARCHITECTURE.md). It reads state only through each system's public
    /// surface and writes it back only through each system's event-quiet restore API — it never
    /// reaches into internals and never decides gameplay outcomes. It persists only authoritative
    /// state (Engine Principle 25): derived state — attribute current values, attribute-bound
    /// resource maxima, tags, "is on cooldown" — is rebuilt by re-running composition, not stored.
    ///
    /// Reconstruction goes through <see cref="IGameplayObjectReconstructor"/>, the Framework-level
    /// view of the composition root, so the Save System does not depend on the Gameplay layer.
    /// Every restore happens while the object's event gate is closed (Engine Principle 26), so load
    /// publishes no gameplay facts; the object then activates, and its <c>GameplayObjectSpawned</c>
    /// fact is the first observable event, already carrying fully restored state.
    /// See Docs/Systems/SAVE_SYSTEM.md and Docs/Architecture/ENGINE_STARTUP.md.
    /// </summary>
    public sealed class SaveManager
    {
        /// <summary>
        /// The schema version this build reads and writes. Bump when the serialization contract
        /// changes; loading any other version fails fast (migration is a future addition).
        /// </summary>
        public const int CurrentVersion = 1;

        // ------------------------------------------------------------------ Capture

        /// <summary>
        /// Captures the authoritative state of every live object in <paramref name="world"/>, in
        /// the registry's deterministic enumeration order, into a fresh payload stamped with
        /// <see cref="CurrentVersion"/>.
        /// </summary>
        public SaveData Capture(GameplayObjectRegistry world)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            var data = new SaveData { Version = CurrentVersion };
            IReadOnlyList<GameplayObject> objects = world.Objects;
            for (int i = 0; i < objects.Count; i++)
            {
                data.Objects.Add(CaptureObject(objects[i]));
            }

            return data;
        }

        private static GameplayObjectState CaptureObject(GameplayObject obj)
        {
            var state = new GameplayObjectState
            {
                ObjectId = obj.Id.ToString(),
                DefinitionId = obj.DefinitionId.Value,
            };

            if (obj.TryGet(out ResourceSet resources))
            {
                IReadOnlyList<ResourceValue> values = resources.Resources;
                for (int i = 0; i < values.Count; i++)
                {
                    state.Resources.Add(new ResourceState(values[i].Definition.Id.Value, values[i].Current));
                }
            }

            if (obj.TryGet(out AbilitySet abilities))
            {
                IReadOnlyList<AbilityInstance> granted = abilities.Abilities;
                for (int i = 0; i < granted.Count; i++)
                {
                    // Only cooling-down abilities need a record; a ready ability restores to zero.
                    if (granted[i].CooldownRemaining > 0f)
                    {
                        state.Cooldowns.Add(new AbilityCooldownState(granted[i].Definition.Id.Value, granted[i].CooldownRemaining));
                    }
                }
            }

            if (obj.TryGet(out StatusEffectSet statuses))
            {
                IReadOnlyList<StatusEffectInstance> active = statuses.ActiveStatuses;
                for (int i = 0; i < active.Count; i++)
                {
                    state.Statuses.Add(new StatusEffectState(
                        active[i].Definition.Id.Value,
                        active[i].Stacks,
                        active[i].RemainingSeconds,
                        active[i].PeriodAccumulator));
                }
            }

            if (obj.TryGet(out InventorySet inventory))
            {
                IReadOnlyList<ItemInstance> stacks = inventory.Stacks;
                for (int i = 0; i < stacks.Count; i++)
                {
                    state.Inventory.Add(new InventoryStackState(
                        stacks[i].Definition.Id.Value, stacks[i].Id.ToString(), stacks[i].Quantity));
                }
            }

            if (obj.TryGet(out EquipmentSet equipment))
            {
                IReadOnlyList<EquipmentSlotDefinition> slots = equipment.Slots;
                for (int i = 0; i < slots.Count; i++)
                {
                    ItemInstance item = equipment.GetEquipped(slots[i].Id);
                    if (item != null)
                    {
                        state.Equipment.Add(new EquipmentSlotState(
                            slots[i].Id.Value, item.Definition.Id.Value, item.Id.ToString()));
                    }
                }
            }

            return state;
        }

        // ------------------------------------------------------------------ Restore

        /// <summary>
        /// Reconstructs and restores every object in <paramref name="data"/>, then activates each,
        /// returning the restored objects in payload order. Objects register themselves in the
        /// authoritative registry as they activate (through the reconstructor's context), so after
        /// this call the live world matches the save. Throws on an unsupported version, corrupt
        /// identity, or a save inconsistent with the current definitions.
        /// </summary>
        public IReadOnlyList<GameplayObject> Restore(
            SaveData data, IGameplayObjectReconstructor reconstructor, IDataRegistry definitions)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (reconstructor == null)
            {
                throw new ArgumentNullException(nameof(reconstructor));
            }

            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            RequireSupportedVersion(data.Version);

            var restored = new List<GameplayObject>(data.Objects.Count);
            for (int i = 0; i < data.Objects.Count; i++)
            {
                restored.Add(RestoreObject(data.Objects[i], reconstructor, definitions));
            }

            return restored;
        }

        private static GameplayObject RestoreObject(
            GameplayObjectState state, IGameplayObjectReconstructor reconstructor, IDataRegistry definitions)
        {
            GameplayObjectId id = ParseObjectId(state);
            GameplayObject obj = reconstructor.Reconstruct(new DefinitionId(state.DefinitionId), id);

            // Restore in dependency order, all behind the closed event gate (Construction Before
            // Participation, Engine Principle 26):
            //   1. Equipment re-establishes its contributions — attribute modifiers (which set the
            //      correct resource maxima), granted abilities, and applied statuses.
            //   2. Independent statuses (combat, environment) restore their exact drift; statuses
            //      already re-applied by equipment are left as-is.
            //   3. Resource currents restore against the now-correct maxima.
            //   4. Ability cooldowns restore onto granted abilities (definition- or equipment-granted).
            //   5. Inventory stacks restore in slot order.
            RestoreEquipment(obj, state, definitions);
            RestoreStatuses(obj, state, definitions);
            RestoreResources(obj, state);
            RestoreCooldowns(obj, state);
            RestoreInventory(obj, state, definitions);

            // Activation opens the event boundary and registers the object; its GameplayObjectSpawned
            // fact is the first observable event, already carrying fully restored state.
            obj.Activate();
            return obj;
        }

        private static void RestoreEquipment(GameplayObject obj, GameplayObjectState state, IDataRegistry definitions)
        {
            if (state.Equipment.Count == 0)
            {
                return;
            }

            EquipmentSet equipment = RequireCapability<EquipmentSet>(obj, state, "equipment");
            for (int i = 0; i < state.Equipment.Count; i++)
            {
                EquipmentSlotState slot = state.Equipment[i];
                ItemInstance item = RestoreItem(slot.ItemDefinitionId, slot.InstanceId, 1, definitions);
                EquipResult result = equipment.TryEquip(item, new DefinitionId(slot.SlotId));
                if (result != EquipResult.Equipped)
                {
                    throw new InvalidOperationException(
                        $"Could not restore equipped item '{slot.ItemDefinitionId}' into slot '{slot.SlotId}' on object " +
                        $"'{state.ObjectId}' ({state.DefinitionId}): {result}. The save is inconsistent with the current definitions.");
                }
            }
        }

        private static void RestoreStatuses(GameplayObject obj, GameplayObjectState state, IDataRegistry definitions)
        {
            if (state.Statuses.Count == 0)
            {
                return;
            }

            StatusEffectSet statuses = RequireCapability<StatusEffectSet>(obj, state, "status effect");
            for (int i = 0; i < state.Statuses.Count; i++)
            {
                StatusEffectState s = state.Statuses[i];
                var statusId = new DefinitionId(s.StatusId);

                // A status already re-applied by equipment keeps that instance; its persisted drift
                // is not layered on top, avoiding a double application.
                if (statuses.Has(statusId))
                {
                    continue;
                }

                StatusEffectDefinition definition = definitions.Get<StatusEffectDefinition>(statusId);
                statuses.Restore(definition, s.Stacks, s.RemainingSeconds, s.PeriodAccumulator);
            }
        }

        private static void RestoreResources(GameplayObject obj, GameplayObjectState state)
        {
            if (state.Resources.Count == 0)
            {
                return;
            }

            ResourceSet resources = RequireCapability<ResourceSet>(obj, state, "resource");
            for (int i = 0; i < state.Resources.Count; i++)
            {
                ResourceState r = state.Resources[i];
                ResourceValue value = resources.GetResource(new DefinitionId(r.ResourceId));
                if (value == null)
                {
                    throw new InvalidOperationException(
                        $"Object '{state.ObjectId}' ({state.DefinitionId}) has no resource '{r.ResourceId}' to restore. " +
                        "The save is inconsistent with the definition.");
                }

                value.RestoreCurrent(r.Current);
            }
        }

        private static void RestoreCooldowns(GameplayObject obj, GameplayObjectState state)
        {
            if (state.Cooldowns.Count == 0)
            {
                return;
            }

            AbilitySet abilities = RequireCapability<AbilitySet>(obj, state, "ability");
            for (int i = 0; i < state.Cooldowns.Count; i++)
            {
                AbilityCooldownState cooldown = state.Cooldowns[i];
                abilities.RestoreCooldown(new DefinitionId(cooldown.AbilityId), cooldown.CooldownRemaining);
            }
        }

        private static void RestoreInventory(GameplayObject obj, GameplayObjectState state, IDataRegistry definitions)
        {
            if (state.Inventory.Count == 0)
            {
                return;
            }

            InventorySet inventory = RequireCapability<InventorySet>(obj, state, "inventory");
            for (int i = 0; i < state.Inventory.Count; i++)
            {
                InventoryStackState stack = state.Inventory[i];
                ItemInstance item = RestoreItem(stack.ItemDefinitionId, stack.InstanceId, stack.Quantity, definitions);
                inventory.RestoreStack(item);
            }
        }

        private static ItemInstance RestoreItem(
            string itemDefinitionId, string instanceId, int quantity, IDataRegistry definitions)
        {
            ItemDefinition definition = definitions.Get<ItemDefinition>(new DefinitionId(itemDefinitionId));
            if (!ItemInstanceId.TryParse(instanceId, out ItemInstanceId id))
            {
                throw new InvalidOperationException(
                    $"Corrupt item instance id '{instanceId}' for item '{itemDefinitionId}' in the save payload.");
            }

            return ItemInstance.Restore(definition, id, quantity);
        }

        // ------------------------------------------------------------------ JSON

        /// <summary>
        /// Serializes <paramref name="data"/> to the stable JSON form. Deterministic: fields and
        /// list order are preserved, and capture already enumerates in registration order.
        /// </summary>
        public string ToJson(SaveData data, bool prettyPrint = false)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return JsonUtility.ToJson(data, prettyPrint);
        }

        /// <summary>
        /// Parses a payload from JSON, validating the version. Throws on empty or malformed input
        /// or an unsupported version.
        /// </summary>
        public SaveData FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                throw new ArgumentException("Save JSON is null or empty.", nameof(json));
            }

            SaveData data = JsonUtility.FromJson<SaveData>(json);
            if (data == null)
            {
                throw new InvalidOperationException("Save JSON did not parse into a SaveData payload.");
            }

            RequireSupportedVersion(data.Version);
            return data;
        }

        private static void RequireSupportedVersion(int version)
        {
            if (version != CurrentVersion)
            {
                throw new NotSupportedException(
                    $"Save version {version} is not supported by this build (expected {CurrentVersion}). " +
                    "Migration between save versions is a future addition.");
            }
        }

        private static GameplayObjectId ParseObjectId(GameplayObjectState state)
        {
            if (!GameplayObjectId.TryParse(state.ObjectId, out GameplayObjectId id))
            {
                throw new InvalidOperationException(
                    $"Corrupt GameplayObjectId '{state.ObjectId}' for definition '{state.DefinitionId}' in the save payload.");
            }

            return id;
        }

        private static T RequireCapability<T>(GameplayObject obj, GameplayObjectState state, string what)
            where T : class, IGameplayCapability
        {
            if (obj.TryGet(out T capability))
            {
                return capability;
            }

            throw new InvalidOperationException(
                $"Object '{state.ObjectId}' ({state.DefinitionId}) has no {what} capability, but the save records {what} state. " +
                "The save is inconsistent with the definition.");
        }
    }
}
