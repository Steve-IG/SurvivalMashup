# Engine Startup

**Architecture:** ToyChest v1.0
**Status:** Living Specification
**Owner:** Gameplay Architecture

---

## System Ownership

This document owns:
- The canonical order in which engine services, registries, saved state, and gameplay objects come to life for a session.

This document does NOT own:
- The internal rules of any individual system, save file format, or scene-loading mechanics. Those belong to the owning system documents.

---

# Purpose

This is the authoritative startup lifecycle for the engine. It defines the fixed order in which a session is brought up, from process start to the first gameplay tick, so that services exist before their consumers, definitions are registered before they are resolved, and saved state is restored onto objects that have already been reconstructed.

Every entry point that boots a session — the bootstrap scene, an integration test harness, and future dedicated-server or client startup — follows this same order. Startup is a single, well-known sequence, not something each scene improvises.

---

# The Startup Sequence

Startup proceeds in seven phases, strictly in order. A phase may begin only after the previous phase has completed.

```
1. Bootstrap
2. Service Creation
3. Registry Population
4. Save Loading
5. Gameplay Object Reconstruction
6. Runtime Initialization
7. Gameplay Start
```

## 1. Bootstrap

The single entry point that owns the whole sequence. It runs once, before any gameplay scene is interactive. It reads launch configuration (which save slot, which starting scene) and then drives phases 2–7 in order. It holds no gameplay rules; it is the conductor, not a player.

## 2. Service Creation

Construct the game-wide services, in dependency order, and hold them for injection:

- The Core logging service.
- The **Event Bus** (`IEventBus`) — one game-wide instance (`Docs/Architecture/EVENT_SYSTEM.md`).
- The **Data Registry** (`IDataRegistry`) — the authoritative definition lookup (`Docs/Architecture/DATA_REGISTRY.md`).
- The **Gameplay Tag Table** — the session's interned tag hierarchy.
- The **Gameplay Object Registry** (`GameplayObjectRegistry`) — the authoritative live-object set (`Docs/Systems/GAMEPLAY_FRAMEWORK.md`).

Services are created once and provided by constructor injection (or the Core service registry where a Unity lifecycle constraint prevents constructor injection). There is no global/static service access. The `GameplayObjectContext` is assembled here from these services and handed to the `GameplayObjectFactory` (the composition root).

## 3. Registry Population

Load immutable definitions into the Data Registry and intern tag definitions into the Gameplay Tag Table. After this phase every definition is resolvable by stable identifier, and every authored tag path is interned. No runtime gameplay object exists yet. Definitions are immutable and shared; this phase touches no mutable runtime state.

## 4. Save Loading

Read the save payload (or determine that this is a new session) and hold it as data. This phase only *reads and validates* the persisted authoritative state; it does not yet apply it. Per `Docs/Architecture/CORE_ARCHITECTURE.md`, the Save System coordinates serialization — it does not own gameplay state and does not decide gameplay outcomes. A new game supplies starting state through the same shape a save would.

## 5. Gameplay Object Reconstruction

For each persisted object, the composition root rebuilds the object from its definition through the `GameplayObjectFactory` — the same path that spawns a fresh object — then each owning system restores its **authoritative** runtime state onto the composed capabilities through event-quiet restore APIs:

- Resource current values (`ResourceValue.RestoreCurrent`).
- Ability cooldowns (`AbilitySet.RestoreCooldown`).
- Status duration, stacks, and periodic accumulator (`StatusEffectSet.Restore`), which re-applies the status's granted tags and modifiers as part of reconstruction.

Derived state (attribute current values, bound maximums, tag ancestor counts, "is on cooldown") is recomputed, never restored (Engine Principle 25, Persistence Boundary). Reconstruction publishes no gameplay facts — it re-establishes state, it does not replay the gameplay that produced it. This is guaranteed by the lifecycle (Engine Principle 26, Construction Before Participation): each object's event boundary stays closed through construction and restoration, so composition and restore are event-quiet without any per-call flags; it opens only at activation (phase 6). A new game composes its starting objects through the same factory with no restore step.

## 6. Runtime Initialization

Objects are activated. Activation registers each object in the Gameplay Object Registry and publishes `GameplayObjectSpawned`. Presentation, UI, and audio subscribe to the Event Bus here and bind to the now-live objects. This is the first phase in which lifecycle facts are published.

## 7. Gameplay Start

The update loop begins. `Tick(deltaSeconds)` fans out to ticking capabilities with injected time; input is enabled; gameplay is live. From here the engine is in its steady runtime state.

---

# Why This Order

- **Services before consumers.** Nothing can be injected before it exists (phase 2 precedes all use).
- **Definitions before resolution.** Objects reference definitions by id; the registry must be populated before reconstruction resolves them (phase 3 before 5).
- **Reconstruct before restore.** Authoritative values are restored onto already-composed capabilities; the object graph is rebuilt from definitions first, then leaf values are set on top (phase 5 is composition then restore, in that internal order).
- **Restore before activate.** Objects come to life already carrying their restored state, so the first `GameplayObjectSpawned` a listener sees is already correct (phase 5 before 6).
- **Activate before tick.** Ticking a non-active object is a lifecycle error (phase 6 before 7).

---

# Multiplayer Note

This sequence is authority-agnostic. In a future server-authoritative setup the server runs phases 1–7 to bring up the authoritative simulation; a client runs the same phases, replacing Save Loading and Reconstruction with replicated state from the server, and composes through the same factory. The order does not change.

---

# Related Documents

- Docs/Architecture/ENGINE_PRINCIPLES.md (Principle 22 Composition Root, Principle 25 Persistence Boundary)
- Docs/Architecture/CORE_ARCHITECTURE.md
- Docs/Architecture/EVENT_SYSTEM.md
- Docs/Architecture/DATA_REGISTRY.md
- Docs/Systems/GAMEPLAY_FRAMEWORK.md
