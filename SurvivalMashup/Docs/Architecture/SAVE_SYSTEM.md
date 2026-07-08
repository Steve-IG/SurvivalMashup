# Save System

**Architecture:** ToyChest v1.0
**Status:** Living Specification
**Owner:** Gameplay Architecture

---

## System Ownership

This document owns:
- How the running simulation's authoritative state is captured into a save payload and restored from one: the save data model, the capture and restore coordination, the restore ordering, the serialization contract, and save versioning.

This document does NOT own:
- The **meaning** of any gameplay state. The Save System coordinates serialization; it never interprets, validates, or repairs gameplay state, and it never decides gameplay outcomes (`Docs/Architecture/CORE_ARCHITECTURE.md`).
- **What is authoritative** in each system. Every system's own Persistence Boundary block defines that (Engine Principle 25). The Save System captures exactly what each system exposes and restores it through that system's restore API.
- **Object reconstruction.** The composition root rebuilds objects (`Docs/Systems/GAMEPLAY_FRAMEWORK.md`); the Save System drives it through `IGameplayObjectReconstructor`.

Primary Runtime Objects:
- `SaveManager` (coordinator), `SaveData` (payload root), and the per-capability state records.

Published Events:
- None. Persistence is not gameplay. Restored objects announce themselves through the ordinary lifecycle: one `GameplayObjectSpawned` per object at activation.

Consumed Events:
- None.

---

# Purpose

The Save System persists and restores the authoritative state of a running simulation. It exists so a session can be written to durable storage and brought back exactly, deterministically, and without the Save System ever becoming an owner of gameplay state.

It is a **coordinator**, not a leaf gameplay system. It reads state only through each system's public surface and writes it back only through each system's event-quiet restore API. It depends on the Framework-level `IGameplayObjectReconstructor` rather than on the Gameplay composition layer, so persistence never reaches up into composition.

---

# Persistence Boundary

Per Engine Principle 25 (Persistence Boundary) and Principle 26 (Construction Before Participation):

- **Authoritative (serialized):** each live object's `GameplayObjectId` and its `DefinitionId`; and, per capability, only the irreducible leaf values — resource current values, ability remaining cooldowns, active-status stacks / remaining duration / periodic accumulator, inventory stacks (item definition id, instance id, quantity), and equipment slot assignments (slot id, item definition id, instance id).
- **Derived (never serialized):** attribute current values, attribute-bound resource maximums, gameplay tags, tag ancestor counts, "is on cooldown", the live capability object graph, ticking-capability lists, and lifecycle flags. All are rebuilt by re-running composition and by each system re-establishing its own contributions.
- **Reconstructed:** the whole object is rebuilt from its definition through the composition root, each owning system restores its authoritative leaf values on top, and the object is activated. Definitions are referenced by stable id (Principle 21), never embedded.

Attributes and tags therefore carry **no** capability state record: an attribute's value is base plus re-applied modifiers, and tags are re-established by composition and by the systems that grant them (equipment on re-equip, statuses on restore).

---

# The Save Data Model

`SaveData` is a flat, plain-data model — no live references, no capability graph, no caches, no lookup structures, no event subscriptions — so it round-trips through any serializer deterministically.

```
SaveData
  Version : int                       // schema version (SaveManager.CurrentVersion)
  Objects : GameplayObjectState[]      // in registry (activation) order

GameplayObjectState
  ObjectId      : string               // GameplayObjectId
  DefinitionId  : string               // definition, by stable id
  Resources     : ResourceState[]           // (ResourceId, Current)
  Cooldowns     : AbilityCooldownState[]     // (AbilityId, CooldownRemaining) — cooling-down only
  Statuses      : StatusEffectState[]        // (StatusId, Stacks, RemainingSeconds, PeriodAccumulator)
  Inventory     : InventoryStackState[]      // (ItemDefinitionId, InstanceId, Quantity)
  Equipment     : EquipmentSlotState[]       // (SlotId, ItemDefinitionId, InstanceId)
```

Only capabilities with authoritative state appear. Ready abilities are omitted (they restore to zero). The model is the stable serialization contract; evolving it is a versioned, intentional act.

---

# Capture

`SaveManager.Capture(registry)` enumerates the authoritative Gameplay Object Registry in its deterministic registration order and records each object's identity, definition id, and the authoritative leaf state each of its capabilities exposes. Capture reads only public enumeration surfaces; it never reaches into internals. Capturing the same world twice yields byte-identical output.

---

# Restore

`SaveManager.Restore(data, reconstructor, definitions)` rebuilds each object and hands its authoritative state back to the systems that own it, then activates it. Every step runs while the object's event boundary is closed (Construction Before Participation), so load publishes no gameplay facts; activation is the first observable event.

Per object:

1. **Reconstruct** through `IGameplayObjectReconstructor` with the persisted id: the composition root rebuilds the object from its definition exactly as a fresh spawn would.
2. **Restore in dependency order**, because contributions must exist before the values that depend on them:
   1. **Equipment** — re-equip persisted items, re-establishing attribute modifiers (which set the correct resource maxima), granted abilities, and applied statuses.
   2. **Statuses** — restore each persisted status's exact stacks / duration / accumulator, re-applying its tags and modifiers. A status already re-applied by equipment is left as-is (no double application).
   3. **Resources** — restore current values against the now-correct maxima.
   4. **Ability cooldowns** — restore onto granted abilities (definition- or equipment-granted).
   5. **Inventory** — restore stacks in slot order, preserving instance ids.
3. **Activate** — open the event boundary, register in the world, publish `GameplayObjectSpawned` carrying fully restored state.

The critical ordering constraint is equipment before resources: an equipment attribute modifier raises a bound resource's maximum, and the current value must be restored against the reconstructed maximum, not the base one.

---

# Serialization Contract and Versioning

`SaveManager` serializes `SaveData` to JSON (`UnityEngine.JsonUtility`) and parses it back. Serialization is deterministic: field and list order are preserved, and capture already enumerates in registration order.

`SaveData.Version` is stamped on capture and checked on load. A version this build does not recognize fails fast with a clear error rather than a silent misread. Migration between versions is a future addition layered at this seam; it does not change the runtime contract.

---

# Addressables

The Save System stores no asset paths. Definitions are referenced by stable `DefinitionId` and resolved through the Data Registry (`Docs/Architecture/DATA_REGISTRY.md`), which is where Addressables-backed loading integrates. A save is therefore independent of asset GUIDs, load order, and content packaging: it references *what* content, and the registry owns *how* that content is loaded.

---

# Milestone 0 Scope and Known Limitations

- **Ownership hierarchy** is not yet persisted, because child object ownership is not yet implemented. When it exists, the child-ownership links become authoritative and are added to `GameplayObjectState` (`Docs/Systems/GAMEPLAY_FRAMEWORK.md`, Persistence Boundary).
- **Equipment-applied statuses** are re-applied fresh by re-equip on load; their post-equip runtime drift is not separately layered back on (the status is already present, so its persisted record is skipped to avoid double application). This is immaterial for the permanent/aura statuses equipment typically applies and is revisited if timed equipment procs need exact drift.
- **Storage** is the serialized payload; slot management, file IO, and cloud sync are session/platform concerns layered above this contract, not part of it.

---

# Multiplayer Note

This design is authority-agnostic. A server-authoritative future reuses the same capture (authoritative state only) and the same reconstruct-then-restore-then-activate path a client uses for replicated state; the Save System's boundaries do not change (Engine Principle 11).

---

# Related Documents

- Docs/Architecture/ENGINE_PRINCIPLES.md (Principle 21 Stable Identifiers, Principle 25 Persistence Boundary, Principle 26 Construction Before Participation)
- Docs/Architecture/ENGINE_STARTUP.md (Save Loading and Gameplay Object Reconstruction phases)
- Docs/Architecture/CORE_ARCHITECTURE.md (Save System coordinates, does not own gameplay state)
- Docs/Architecture/DATA_REGISTRY.md (definition resolution by stable id)
- Docs/Systems/GAMEPLAY_FRAMEWORK.md (Object Lifecycle, Rehydration, Save Identity Ownership)
