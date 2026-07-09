# Gameplay Framework

**Status:** Living Specification
**Version:** 1.0
**Owner:** Gameplay Architecture

---

## System Ownership

This system owns:
- Gameplay Object identity, capability component structure, definition-instance separation, shared interfaces, and lifecycle expectations.

This system does NOT own:
- Ability rules, Damage rules, Inventory rules, Equipment rules, Companion rules, Adventure rules, World Reaction rules, UI behavior, or Save ownership.

Primary Responsibilities:
- Provide the integration layer that lets gameplay systems cooperate through shared object and component contracts.

Primary Data:
- Gameplay Object definitions, component schemas, stable identifiers, and definition references.

Primary Runtime Objects:
- Gameplay Object instances and their capability components.

Published Events:
- Gameplay Object Spawned, Gameplay Object Destroyed. Capability-specific facts are published by their owning systems, not by the framework.

Consumed Events:
- None.

---

# Purpose

The Gameplay Framework defines the fundamental architecture used by every interactive object in the game.

It establishes a common language for how gameplay objects are represented, how they participate in gameplay systems, and how reusable engine systems interact with them.

The framework favors composition over inheritance, data over hardcoded logic, and events over direct dependencies.

Its goal is to ensure that every gameplay system can interact with every gameplay object through shared interfaces rather than specialized code.

`Docs/Architecture/GAMEPLAY_OBJECT.md` defines the conceptual design role of Gameplay Objects. This document defines the runtime framework that implements that concept.

---

# Design Philosophy

The engine should know as little as possible about individual gameplay objects.

Instead, gameplay emerges from combining modular components.

Rather than creating specialized object types, the framework assembles gameplay objects from reusable capabilities.

Example:

A tree is not a special Tree class.

It is an object with:

- Attributes
- Gameplay Tags
- Gameplay Effects
- World Properties
- Harvestable Component

Likewise, a player is not special.

The player is an object with:

- Attributes
- Resources
- Abilities
- Inventory
- Equipment
- Companion Manager
- Adventure Tracker

The same philosophy applies throughout the game.

---

# Framework Ownership

The Gameplay Framework owns the shared runtime model that allows gameplay systems to cooperate.

It owns:

- Gameplay Object identity.
- Capability component structure.
- Runtime instance structure.
- Definition-to-instance separation.
- Common interfaces used by gameplay systems.
- Shared lifecycle expectations.
- Event participation rules.

It does not own:

- Ability rules.
- Damage rules.
- Inventory rules.
- Equipment rules.
- Companion rules.
- Adventure rules.
- World Reaction rules.
- UI behavior.
- Save ownership.

Individual gameplay systems own their own rules, data, and runtime state.

The framework provides the integration layer.

It should make systems interoperable without absorbing their responsibilities.

---

# Core Principles

## Composition Over Inheritance

Behavior should be assembled from independent components.

Avoid deep inheritance hierarchies.

Favor reusable modules.

---

## Data Driven

Gameplay definitions belong in immutable data assets.

Runtime objects reference definitions.

Gameplay code should rarely know about specific content.

---

## Shared Vocabulary

Every gameplay system should communicate using common concepts.

Examples:

Abilities

Gameplay Effects

Resources

Attributes

Status Effects

Gameplay Tags

Events

World Properties

No system should invent parallel terminology.

---

## Event Driven

Gameplay systems communicate by publishing and subscribing to events.

Systems should not directly depend on one another whenever practical.

---

# Gameplay Object

Every interactive object in the world is a Gameplay Object.

Examples:

Player

Enemy

Boss

Companion

NPC

Harvestable Resource

Chest

Projectile

Trap

Portal

Crafting Station

Shrine

Destructible Object

Vehicle (future)

Gameplay Objects expose capabilities through components.

---

# Core Components

A Gameplay Object may contain any combination of the following components.

## Attribute Component

Stores runtime attribute values.

Examples:

Maximum Health

Movement Speed

Armor

Mining Speed

---

## Resource Component

Stores runtime resource values.

Examples:

Current Health

Mana

Energy

Ammo

Heat

---

## Ability Component

Owns:

Ability Library

Equipped Loadout

Cooldowns

Activation State

---

## Status Effect Component

Tracks active buffs, debuffs, and conditions.

---

## Gameplay Tag Component

Stores descriptive gameplay tags.

Examples:

Fire

Frozen

Boss

Mechanical

Flying

Harvestable

Friendly

Hostile

---

## Inventory Component

Owns item storage.

Optional.

---

## Equipment Component

Owns equipped items.

Optional.

---

## Companion Component

Owns active and collected companions.

Optional.

---

## Adventure Component

Tracks adventure progress, active objectives, and region experience state.

Player-only.

---

## Interaction Component

Defines available interactions.

Examples:

Talk

Harvest

Open

Activate

Craft

Rescue

---

## World Properties Component

Stores world properties used by the World Reaction System.

Examples:

Wet

Burning

Frozen

Electrified

Corrupted

---

# Component Independence

Components should not directly reference one another.

Instead:

Components request information through interfaces.

or

Components publish gameplay events.

Example:

An Ability does not modify Current Health directly.

Instead:

Ability

↓

Gameplay Effect

↓

Resource Component

↓

Resource Changed Event

↓

UI updates

↓

Audio reacts

↓

Achievements update

---

# System Cooperation

Gameplay systems cooperate through the framework.

They should interact with Gameplay Objects through capabilities, interfaces, tags, resources, attributes, effects, and events.

Systems should not reach into another system's internal data structures.

Systems should not duplicate another system's runtime state.

Systems should not require UI, presentation, or content-specific classes to execute gameplay logic.

When one system needs another system to react, it should prefer:

- Gameplay Events.
- Stable interfaces.
- Shared component capabilities.
- Data-driven definitions.

Direct dependencies are allowed only when ownership is clear and the dependency direction follows the architecture rules.

Each system remains responsible for its own domain.

Examples:

Ability System

Owns activation, targeting, costs, cooldowns, and execution flow.

Damage System

Owns damage requests, modifiers, resistance checks, and damage resolution.

Resource System

Owns current resource values and resource changes.

Status Effect System

Owns active conditions, durations, stacks, and status-driven effects.

World Reaction System

Owns world property evaluation and environmental reactions.

The Gameplay Framework coordinates how these systems see and address the same Gameplay Object.

It does not decide the outcome for those systems.

---

# Runtime vs Definitions

Every gameplay object consists of:

Definition

+

Runtime State

Definitions are immutable.

Runtime state changes continuously.

Examples:

Ability Definition

↓

Ability Instance

Weapon Definition

↓

Equipped Weapon Instance

Companion Definition

↓

Companion Instance

This separation simplifies saving, networking, and AI.

---

# Object Lifecycle

Objects generally follow this lifecycle:

Definition Loaded

↓

Runtime Object Created

↓

Components Initialized

↓

Gameplay Begins

↓

Events Published

↓

State Saved

↓

Object Destroyed or Persisted

---

# Ownership

Gameplay Objects may own other Gameplay Objects.

Examples:

Player

↓

Companion

Projectile

↓

Explosion

Chest

↓

Loot

Boss

↓

Summoned Minions

Ownership should remain hierarchical and explicit.

---

# Networking

Gameplay Objects should support:

Server Authority

Replication

Prediction

Rollback where appropriate

Persistent IDs

Runtime state synchronization

---

# AI

AI interacts with gameplay objects through the same interfaces as the player systems.

AI should not receive privileged access to gameplay internals.

Validated (Milestone 1, Review Group 5): the first autonomous world actor — a Villager NPC — is an ordinary Gameplay Object composed from authored data, with no privileged access. Its simple autonomous movement is a thin Unity adapter (`NpcWanderLocomotion`, `ToyChest.Gameplay.Npc`) over a pure, deterministic planner (`WanderMotor`), the NPC counterpart to the player's locomotion adapter; it reads movement speed from the same authored attribute and is driven by `Update`, not by any manager, scheduler, or global update service. The player interacts with the NPC through the ordinary Interaction → Ability → Gameplay Effect path, and the NPC's authoritative state persists through the ordinary Save System. This confirms autonomous behaviour attaches at the thin-adapter seam (and, in future, as a capability) without new framework concepts — consistent with the AI extension point below.

---

# Save System

Only runtime state should be serialized.

Definitions are referenced through stable identifiers.

---

# Runtime Framework Architecture

This section is the authoritative runtime design that implements the Gameplay Object concept defined in `Docs/Architecture/GAMEPLAY_OBJECT.md`. It defines how runtime entities are assembled, initialized, ticked, and torn down. The conceptual document describes intent; this section describes structure.

## Gameplay Object Responsibilities

A Gameplay Object is a lightweight plain-C# runtime container. It:

- Holds a stable runtime identity (`GameplayObjectId`).
- References its immutable `GameplayObjectDefinition`.
- Owns a set of capability components, at most one instance per capability type.
- Exposes capabilities for generic query rather than concrete-type branching.
- Drives its capabilities' lifecycle: initialize, tick, dispose.
- Publishes framework lifecycle events.

A Gameplay Object contains no gameplay rules. Behavior lives in capabilities and in the systems that operate on them.

## Composition Rules

**Capabilities own behavior. Gameplay Objects own composition.**

A Gameplay Object decides *which* capabilities exist together and manages their shared lifecycle; it never implements what any capability does. A capability implements its domain's behavior; it never decides what else the object is composed of. Logic drifting into the object, or composition decisions drifting into a capability, are both architecture violations.

Two supporting rules:

- **Depend on capability interfaces, not implementations.** Whenever practical, a capability (or system) that needs a sibling depends on that sibling's public interface — `IAttributeProvider`, not `AttributeSet`. Concrete types are for composition roots; interfaces are for consumers. This keeps capabilities substitutable and testable with fakes.
- **Gameplay code consumes composed objects.** Nothing outside the composition root constructs capabilities. If code is newing up an `AttributeSet` or `ResourceSet` at a call site, it is bypassing the composition root and must be corrected (see `Docs/Architecture/ENGINE_PRINCIPLES.md`, Principle 22).

## Capability Independence

Capabilities are designed to stand alone:

- **No sibling references at runtime.** A capability never holds or reaches for another capability during play. Cross-capability relationships are wired once at composition time (a resource binding an `IAttributeProvider` maximum) or flow through systems and events.
- **Function or fail clearly without siblings.** A capability must either work when a sibling is absent or fail with a descriptive error at composition/apply time — never silently misbehave mid-session.
- **No ordering assumptions.** A capability may rely only on what the factory guarantees (dependencies constructed before dependents), never on incidental ordering.
- **Testable in isolation.** Every capability has EditMode tests that construct it alone, with fakes for any interface it consumes.

## Architecture Diagram

```
GameplayObjectDefinition                (immutable data asset)
          |
          v
GameplayObjectFactory                   (composition root — ToyChest.Gameplay)
          |   builds capabilities in dependency order:
          |   AttributeSet -> ResourceSet(binds IAttributeProvider) -> GameplayTagContainer
          v
GameplayObject                          (plain C# runtime container — ToyChest.Framework)
   +-- AttributeSet          : IGameplayCapability
   +-- ResourceSet           : IGameplayCapability, ITickingCapability, IDisposable
   +-- GameplayTagContainer  : IGameplayCapability
          ^
          |   Bind / Activate / Tick(deltaTime) / Destroy
          |
GameplayObjectBehaviour                 (thin MonoBehaviour adapter — Unity scene)
```

## Layer Placement

The framework's contracts and object model live in `ToyChest.Framework`: `GameplayObject`, `GameplayObjectId`, `IGameplayCapability`, `ITickingCapability`, the lifecycle events, and the `GameplayObjectBehaviour` bridge. They know nothing about specific systems.

The concrete composition pieces — `GameplayObjectDefinition`, `GameplayObjectContext`, and `GameplayObjectFactory` — live in `ToyChest.Gameplay`, because they reference system types (`AttributeDefinition`, `ResourceDefinition`, `TagDefinition`, `GameplayTagTable`) and the dependency rules forbid Framework from depending on systems. This is the literal reading of "Gameplay composes systems" in `Docs/Architecture/PROJECT_ARCHITECTURE.md`.

## Capability Composition Model

Capabilities are plain-C# classes — the already-implemented `AttributeSet`, `ResourceSet`, `GameplayTagContainer`, `StatusEffectSet`, and `AbilitySet`, plus future inventory, equipment, and interaction capabilities. Each implements the marker `IGameplayCapability`.

Capabilities are stored keyed by concrete type. Access is generic:

- `Has<TCapability>()`
- `Get<TCapability>()` (throws when absent)
- `TryGet<TCapability>(out TCapability)`

This mirrors tag queries: systems ask "does this object have a `ResourceSet`?" rather than testing concrete entity types. Adding a capability requires no framework change — implement the marker, list it in a definition, and the composition root attaches it.

Optional capability lifecycle interfaces:

- `ITickingCapability` — `Tick(float deltaSeconds)`, driven each update (for example, resource regeneration). Time is always injected; a capability never reads the engine clock.
- `IDisposable` — released on teardown (for example, `ResourceSet` detaching its attribute bindings).

Capabilities never reference one another through concrete fields. When a capability needs a sibling — a bound resource needs an attribute — the dependency is resolved once during composition, not by reaching into the object at runtime.

## Definition

`GameplayObjectDefinition` is an immutable `GameplayDefinition` (ScriptableObject) that declares, as data, which capabilities an object has and how they are configured: attribute definitions, resource definitions, initial gameplay tags, and — in future — abilities, inventory, equipment, and interaction configuration.

Composing a new entity type (Player, Wolf, Chest) is authoring a definition, not writing a class.

## Object Lifecycle

The runtime lifecycle has five phases, and the object is **fully constructed and internally consistent before it participates in the simulation** (Engine Principle 26, Construction Before Participation). The first three phases are event-quiet; observable gameplay begins at Activation.

1. **Construction.** The composition root receives a `GameplayObjectContext` (injected `IEventBus`, `IDataRegistry`, `GameplayTagTable`, `GameplayObjectRegistry`), assigns a `GameplayObjectId`, constructs every capability from the `GameplayObjectDefinition` in dependency order (attributes before the resources that bind to them), seeds initial authoritative state (initial tags, granted abilities), and creates the object with its full capability set. Composition is sealed at construction: capabilities cannot be added to a live object. There is no separate initialization interface — the factory's construction order is the initialization mechanism (approved Milestone 0 decision). Construction publishes nothing.
2. **Restoration** (load path only). For a persisted object, `Reconstruct(definitionId, id)` runs Construction with the *saved* identity, then each owning system restores its authoritative leaf state onto the composed capabilities through event-quiet restore APIs (see Rehydration). A fresh spawn skips this phase; its initial state came from the definition during Construction. Restoration publishes nothing.
3. **Activation.** The spawner (scene bridge or future spawn system) calls `Activate()` after placement. Activation opens the object's event boundary, registers it in the Gameplay Object Registry, and publishes exactly one `GameplayObjectSpawned` fact announcing the fully-formed object. The factory composes; it does not activate.
4. **Simulation.** While active, `Tick(deltaSeconds)` fans out to `ITickingCapability` components with injected time (nothing reads the engine clock), and capabilities publish their per-change facts as live gameplay occurs.
5. **Destruction.** Teardown publishes exactly one `GameplayObjectDestroyed` fact, unregisters the object, then closes the event boundary and disposes disposable capabilities — so end-of-life cleanup (a status revoking its tags and modifiers) is quiet, mirroring construction on the other end. Destroy is idempotent because engine teardown paths can overlap.

### Event Boundary

Construction Before Participation is enforced by lifecycle, not by "quiet mode" flags. Each object owns a `GameplayObjectEventGate` — a thin `IEventBus` wrapper the composition root hands to every capability and to the object itself. The gate is **closed** through Construction and Restoration and **opens** at Activation; while closed it drops published events, so capabilities publish exactly as they always do and observability depends only on *when* in the lifecycle a publish happens. Subscriptions always pass through; only publication is gated. Destruction publishes its fact, then closes the gate before disposal.

This one mechanism unifies **spawning** (seed initial state → activate), **loading** (restore authoritative state → activate), and future **streaming** (compose → restore → activate; deactivate re-closes the gate). Initial composition state is not re-announced as per-capability events; the single `GameplayObjectSpawned` fact tells listeners the object exists with its full initial state, and they read it directly.

A note on tags: every composed object receives a `GameplayTagContainer` even when the definition declares no initial tags, because state tags arrive at runtime from other systems (status effects, world reactions) and a universally present container keeps that path branch-free. Attribute and resource capabilities remain strictly optional.

## Dependency Resolution

Two dependency kinds, resolved in two places:

- **Service dependencies** (Event Bus, Data Registry, Tag Table) are injected into the composition root via `GameplayObjectContext` and handed to capabilities that need them. No capability locates services globally; there is no service singleton.
- **Intra-object dependencies** (Resource → Attribute) are resolved by the composition root's construction order. The root is the single place that knows how to wire an object, so ordering never leaks into gameplay code.

## Composition Root

**Principle: Gameplay Objects are assembled in one authoritative location.**

A single `GameplayObjectFactory` (the composition root) turns a `GameplayObjectDefinition` plus a `GameplayObjectContext` into a fully wired `GameplayObject`. Runtime and gameplay code consume composed objects; they never construct `AttributeSet`, `ResourceSet`, or other capabilities ad hoc.

Why:

- One place performs dependency injection and wiring, so it stays consistent and testable.
- Assembly order (attributes before bound resources) lives in exactly one location.
- Pooling, networking, and future capabilities extend the factory without touching call sites.
- Gameplay code depends on capabilities, not on how they were built.

Future consumers of the composition root — all of which reuse the same factory rather than composing objects their own way:

- **Prefab composition:** a prefab carries a `GameplayObjectBehaviour` and a definition reference; instantiation invokes the factory and binds the result. *Realized (Milestone 1) by `GameplayObjectSpawner` (`ToyChest.Gameplay`)* — the canonical scene-composition adapter, which runs the factory and binds to the sibling behaviour when the Boot layer injects the scene's services (`GameplaySceneContext` / `IGameplaySceneParticipant`). See `Docs/Architecture/PROJECT_ARCHITECTURE.md`, Scene composition.
- **Runtime procedural spawning:** spawners and directors (Region Director, encounter systems) request objects from the factory by definition.
- **Save reconstruction:** the load path rebuilds each object from its definition through the factory, then restores capability state into the composed capabilities.
- **Networking:** the server-authoritative spawn path composes through the factory and replicates identity plus capability state; clients reconstruct through the same root.

## Runtime Ownership

- A Gameplay Object owns the lifetime of its capabilities.
- Each capability owns its own mutable state; no capability reads or mutates another's internals.
- A Gameplay Object may own child Gameplay Objects (projectile → explosion, chest → loot). Ownership is hierarchical and explicit; destroying a parent destroys its children.
- The composition root owns creation; the object owns destruction of what it created.

## Gameplay Object Registry

`GameplayObjectRegistry` is the authoritative owner of the set of live Gameplay Objects for a session (or a simulation scope). It answers "which objects currently exist?" — the question Save, AI, Simulation, Debugging, and future Multiplayer all need and that no single object can answer about the whole world.

- **Plain C#, Unity-independent.** No scene, no `MonoBehaviour`, no statics. It is testable in isolation and reusable by any consumer that needs to enumerate the world.
- **Lifecycle-driven membership.** An object registers itself on `Activate` and unregisters on `Destroy`, so the registry holds exactly the live objects. An object is a member for exactly the interval `[GameplayObjectSpawned published .. GameplayObjectDestroyed published]`, so a Destroyed listener can still resolve it before it leaves the set. The registry never resurrects a destroyed object.
- **Deterministic enumeration.** `Objects` is exposed in registration (activation) order, preserved across removals — consistent with the ordered iteration used elsewhere in the engine (Engine Principle 17). Save ordering, replay, and multiplayer must not depend on hash layout.
- **Injected, not global.** The registry is created at bootstrap (Service Creation) and flows to objects through `GameplayObjectContext` → `GameplayObjectFactory`. Passing no registry (isolated tests) simply means objects are not tracked; the lifecycle still runs.

The registry owns the *set membership and iteration order*. It does not own object rules, and it is not a spawn authority — the factory composes, the object's lifecycle drives registration. See `Docs/Architecture/ENGINE_STARTUP.md` for where population and activation sit in startup.

**One authoritative registry.** There is exactly one authoritative Gameplay Object Registry for the running simulation — the single answer to "which objects currently exist?" that Save, AI, Simulation, and future Multiplayer share. Future regional registries, streaming partitions, and spatial indices are **filtered views built on top of** this authoritative set (a query over `Objects`, or a projection maintained from the same lifecycle transitions), never parallel authorities that could disagree with it (One Source of Truth, Principle 16). A future partition narrows the set; it does not replace it.

## Rehydration

Reconstruction restores **authoritative** runtime state onto already-composed capabilities through event-quiet restore APIs, never by replaying gameplay (Engine Principle 25, Persistence Boundary). Each owning system exposes exactly the restore surface deterministic reconstruction requires, and no more:

- **Resources** — `ResourceValue.RestoreCurrent(current)` sets the current value directly, clamped, raising no change or transition callbacks.
- **Abilities** — `AbilitySet.RestoreCooldown(ability, remaining)` restores a granted ability's remaining cooldown; it publishes nothing and resumes ticking normally.
- **Status Effects** — `StatusEffectSet.Restore(definition, stacks, remaining, accumulator)` reconstructs an active status: it re-applies the status's contributions (granted tags once, attribute modifiers once per stack) and restores duration, stacks, and the periodic accumulator, running no on-apply effects and publishing no events.

Derived state is recomputed, never restored: attribute current values, attribute-bound resource maximums, tag ancestor counts, and `IsOnCooldown` all fall out of the authoritative values above. Serialization itself is a Save Framework concern; these APIs are the deterministic restoration contract the Save Framework builds on.

Restoration runs during Construction Before Participation (Engine Principle 26), so the object's event boundary is closed throughout. The restore APIs set authoritative values directly without callbacks, and the closed gate additionally suppresses any downstream facts from re-establishing derived contributions — a restored status re-grants its tags and re-applies its per-stack modifiers silently. Reconstruction re-establishes state; it never publishes a gameplay fact. The single `GameplayObjectSpawned` at activation is the first observable event, already carrying fully restored state.

## Save Identity Ownership

- The Gameplay Framework owns each object's persistent identity (`GameplayObjectId`) and its definition reference (by `DefinitionId`).
- Each capability owns its own serializable runtime state; the framework does not interpret it.
- On save, the framework records identity and definition id; each capability contributes its state. On load, the composition root rebuilds the object from the definition, then restores capability state into the owning capability.
- This is consistent with `Docs/Architecture/CORE_ARCHITECTURE.md`: the Save System coordinates serialization; it does not own gameplay state, and the framework does not own capability meaning.

`GameplayObjectId` is a stable value-type identifier, distinct from `DefinitionId` (which identifies the shared immutable definition). Session-local handles are never persisted; the stable id is (see `Docs/Architecture/ENGINE_PRINCIPLES.md`, Principle 21, Stable Identifiers).

## Persistence Boundary

Per Engine Principle 25:

- **Authoritative:** each object's `GameplayObjectId`, its definition reference (`DefinitionId`), the composition it was built from, and the ownership hierarchy (which objects own which children). Per-capability authoritative state is owned by each capability's system, not the framework.
- **Derived:** the live capability object graph, the ticking-capability list, and lifecycle flags (`IsActive`, `IsDestroyed`) — all reproduced by composition and activation.
- **Serialized:** identity, definition id, and the child-ownership links. The framework records *which* object exists and *what* it was composed from; each capability contributes its own authoritative state.
- **Reconstructed:** the whole object is rebuilt from its definition through the `GameplayObjectFactory` (Reconstruction Over Serialization), then capability state is restored onto it and it is activated. Registry membership follows from activation and is never serialized.

## MonoBehaviour Bridge

`GameplayObjectBehaviour : MonoBehaviour` is the thin adapter between Unity's scene and lifecycle and a plain-C# `GameplayObject`. Its responsibilities are strictly:

- Reference a composed `GameplayObject` obtained from the factory; it does not build capabilities itself.
- Translate Unity lifecycle to object lifecycle: activate on enable, `Tick(Time.deltaTime)` from `Update`, destroy and dispose on `OnDestroy`.
- Bridge scene concerns (transform, physics contacts, presentation) to gameplay through capabilities and events.

It contains no gameplay rules. All business logic lives in the plain-C# object and its capabilities, so it is testable without a scene.

## Events

Published by the framework, declared in `ToyChest.Framework`, category Framework:

- `GameplayObjectSpawned` — an object became live.
- `GameplayObjectDestroyed` — an object was torn down.

Capability-specific facts (attribute changed, resource depleted, tag added) are published by their owning systems, never by the framework.

## Framework System Template

**Principle: foundational systems share a standard shape.** Every foundational system should define, where applicable:

- **Definition** — immutable `ScriptableObject` / `IDefinition` for data-driven configuration.
- **Runtime State** — plain-C# instance, separate from the definition.
- **Registry** — registration through the Data Registry when definitions exist.
- **Events** — past-tense facts published on state change.
- **Tests** — EditMode coverage, engine-independent where practical.
- **Documentation** — Purpose, Responsibilities, Dependencies, Extension points, and a System Ownership block.

The Tag, Attribute, and Resource systems already conform. The Gameplay Object Framework and every future system follow the same template, so AI and engineers can navigate any system by knowing one shape.

## Extension Points

- **New capabilities** — implement `IGameplayCapability` (plus optional ticking/disposable) and add to definitions; no framework change.
- **Networking** — replicate `GameplayObjectId` and per-capability state; the factory becomes the spawn authority.
- **Pooling** — the factory recycles objects and resets capabilities.
- **Hierarchical composition** — parent/child object ownership for compound entities.
- **AI capabilities** — reasoning attaches as a capability that queries siblings generically.

---

# Success Criteria

The Gameplay Framework succeeds when:

- Every gameplay object uses the same architectural model.
- New gameplay features are implemented by composing components.
- Designers create new content without requiring engine changes.
- Systems remain loosely coupled.
- Multiplayer, AI, and save/load all operate on the same object model.
- Adding a new gameplay object rarely requires creating a new class hierarchy.

---

# Implementation Notes

- Favor Unity components for runtime behavior and `ScriptableObject` assets for immutable definitions.
- Keep component responsibilities narrowly focused.
- Communicate between systems using interfaces and gameplay events rather than direct references.
- Store immutable definitions separately from mutable runtime state.
- Prefer adding new components over expanding existing ones with unrelated responsibilities.