# Event System

**Architecture:** ToyChest v1.0
**Status:** Living Specification
**Version:** 1.0
**Owner:** Gameplay Architecture

---

## System Ownership

This system owns:
- Event contracts, event publication, event subscription, dispatch order, subscription lifetime, and event debug visibility.

This system does NOT own:
- Event content decisions, gameplay outcomes, gameplay state, or which events other systems publish. The system that owns a gameplay domain decides when and what it publishes.

Primary Responsibilities:
- Transport typed gameplay events between systems with deterministic ordering and zero knowledge of publishers or subscribers.

Primary Data:
- None. The Event System defines contracts, not content.

Primary Runtime Objects:
- The Event Bus, per-event-type channels, subscription tokens, and the diagnostic event trace.

Published Events:
- None. The Event System transports events; it never originates them.

Consumed Events:
- None.

---

# Purpose

The Event System is the low-coupling communication backbone of the ToyChest Architecture.

It allows a system to announce that something has already happened without knowing who is listening, and allows any system to react without referencing the publisher.

Per `Docs/Architecture/CORE_ARCHITECTURE.md`:

> An event represents something that has already happened. Events are notifications, not commands.

The Event Bus is Core-Services-adjacent infrastructure that lives in the Framework layer (`Assets/Game/Runtime/Framework`, assembly `ToyChest.Framework`). It transports events between systems. It does not decide gameplay outcomes.

---

# Event Philosophy

This philosophy is a foundational architectural principle for ToyChest. All future event usage is governed by it.

- Events describe facts that have **already occurred**.
- Events are notifications, **never commands**.
- Systems own facts. The system that owns a gameplay domain is the only source of facts about that domain.
- Systems react to facts. A reaction is the reacting system's own decision, applied within its own domain.
- Systems never coordinate by issuing commands through the Event Bus. The bus carries no requests, no instructions, and no expectations of a response.
- Direct service calls are used whenever a caller requires a result or requests specific behavior, following the dependency rules in `Docs/Architecture/CORE_ARCHITECTURE.md`.

If a proposed event is named in the imperative ("DealDamage", "OpenChest"), it is a command in disguise and must be redesigned — either as a service call or as the past-tense fact that results ("DamageApplied", "ChestOpened").

---

# Design Goals

1. **Typed.** Every event is a distinct C# type. No string event names, no untyped payload dictionaries.
2. **Deterministic.** Given identical publish sequences and identical subscription sequences, dispatch order is identical. Required for testing, replays, and future multiplayer.
3. **Allocation-free publishing.** Publishing an event allocates no managed memory in steady state. Events are structs; dispatch never boxes.
4. **Decoupled.** Publishers never know subscribers. Subscribers never know publishers. Neither references the other's assembly.
5. **Debuggable.** Every dispatched event can be observed by diagnostic tooling without modifying publishers or subscribers.
6. **Testable without Unity.** The bus is a plain C# class with no MonoBehaviour, scene, or engine lifecycle dependency.
7. **Simple.** No priorities, no filtering DSL, no async dispatch, no event inheritance hierarchies. Complexity is added only when a real need appears.

---

# Event Contract

An event is an immutable `readonly struct` implementing the empty marker interface `IGameplayEvent`.

Rules:

- Named in past tense: `ResourceChanged`, `StatusApplied`, `ItemEquipped`, `AbilityActivated`.
- Carries data only: what happened, who or what was involved, and stable context listeners need.
- Events describing Gameplay Object state include the originating `GameplayObjectId` whenever applicable, so listeners can attribute the fact to its object without holding live references.
- Contains no logic, no methods beyond constructors, and no mutable state.
- Never used to tell another system what to do. If a caller needs a specific capability or a return value, it uses a direct service call per the dependency rules.

Example:

```csharp
public readonly struct ResourceChanged : IGameplayEvent
{
    public readonly GameplayObjectId Owner;
    public readonly StableId ResourceId;
    public readonly float PreviousValue;
    public readonly float NewValue;
}
```

Events reference gameplay objects and definitions through stable identifiers rather than live object references whenever practical. This keeps events serializable for diagnostics, replays, and future network forwarding.

---

# Ownership of Event Types

Event types are declared by the system that publishes them, in that system's assembly.

**Each gameplay fact has exactly one publishing system.** Duplicate publishers must never exist. This is the repository-wide One Source of Truth principle applied to events: if two systems could publish the same fact, ownership of that fact is ambiguous and the design must be corrected before implementation.

Examples:

- Only the Resource System publishes `ResourceChanged` (declared in `ToyChest.Systems.Resources`).
- Only the Equipment System publishes `ItemEquipped` (declared in `ToyChest.Systems.Equipment`).
- Only the Ability System publishes `AbilityActivated` (declared in `ToyChest.Systems.Abilities`).
- Only the Status Effect System publishes `StatusApplied` (declared in `ToyChest.Systems.StatusEffects`).

A subscriber therefore references the publisher's assembly only to see the event *type* — never the publisher's internals. Cross-cutting framework events (object spawned, object destroyed) are declared in `ToyChest.Framework` and published only by the Gameplay Framework.

Each system document's "Published Events" section is the authoritative list of that system's events. When an event is added, renamed, or removed, the owning system document must be updated.

---

# Event Categories

Every event type declares a logical category for organization and debugging.

Categories include:

- Resource
- Attribute
- Tag
- Ability
- Gameplay Effect
- Status Effect
- Inventory
- Equipment
- Interaction
- World
- Adventure
- Companion
- UI

Categories are **organizational only**. They must not affect dispatch behavior in any way.

Their purpose is tooling: trace filtering, diagnostics grouping, editor utilities, and future analytics. Categories are declared with a `[EventCategory]` attribute using string constants, so future systems introduce new categories without modifying the Framework.

---

# Event Evolution

Event contracts should evolve compatibly whenever practical.

- Prefer adding new fields over changing the meaning of existing ones.
- Prefer introducing a new event type over repurposing an existing one.
- Breaking changes to event payloads must be rare, intentional, and documented in the owning system's document.

This guidance exists to support future replay tools, diagnostics, save compatibility, and long-term maintainability. Events recorded by tooling today should remain interpretable after years of development.

---

# The Event Bus

## Interface

```csharp
public interface IEventBus
{
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct, IGameplayEvent;
    void Publish<TEvent>(in TEvent gameplayEvent) where TEvent : struct, IGameplayEvent;
}
```

## Instances

Milestone 0 uses **one game-wide bus instance**, created during bootstrap and provided through constructor injection (or the Core service registry where Unity lifecycle constraints prevent constructor injection).

There is no static/global bus. This keeps tests isolated, allows parallel test execution, and leaves room for scoped buses (per-region, per-match) later without contract changes.

## Subscription Model

- `Subscribe` returns an `IDisposable` token. Disposing it unsubscribes. Disposing twice is safe.
- Subscribers are responsible for their own lifetime. A component that subscribes must dispose its tokens when it is deactivated or destroyed.
- Dispatch order within an event type is subscription order. This is deterministic and documented; systems must not rely on being "first" — if ordering between two reactions matters, that is a design smell indicating hidden coupling.

## Publication Model

- `Publish` dispatches **synchronously on the main thread**, immediately, to all current subscribers of that exact event type.
- There is no polymorphic dispatch: publishing `ResourceChanged` notifies subscribers of `ResourceChanged` only. Event types do not inherit from one another.
- Publishing an event with zero subscribers is valid and cheap.

## Reentrancy

Handlers may publish further events. Nested publishes dispatch immediately (depth-first), which keeps causality readable in stack traces and deterministic.

Subscribing or unsubscribing **during dispatch of the same event type** is deferred until that dispatch completes. A handler added mid-dispatch does not receive the in-flight event; a handler removed mid-dispatch does not receive further calls after the current dispatch finishes iterating it.

## Error Isolation

A handler that throws does not prevent remaining handlers from receiving the event. The exception is caught, reported through the Core logging service with the event type and handler identity (what happened, what to check), and dispatch continues.

Handler exceptions are always programming errors. They are surfaced loudly in the editor and development builds; they are never silently swallowed.

---

# Threading Assumptions

- The bus is main-thread only in Milestone 0.
- Development builds assert that `Subscribe`, `Publish`, and token disposal occur on the main thread.
- Background work that needs to raise an event marshals back to the main thread first. A thread-safe publish queue is a documented extension point, not a Milestone 0 feature.

---

# Performance Considerations

- Events are structs passed by `in` reference; dispatch does not box.
- Each event type gets a dedicated generic channel created on first use; channels cache their handler arrays, so steady-state publishing performs zero allocations.
- Subscription and unsubscription may allocate; they are expected at lifecycle boundaries, not per frame.
- The diagnostic trace is compiled into editor and development builds only; release builds pay nothing for it.
- Publishing is O(number of subscribers of that event type).

---

# Debug Visibility

The bus exposes a diagnostics hook (editor and development builds):

- **Event trace:** a fixed-size ring buffer recording event type, frame number, timestamp, payload summary, and subscriber count for the most recent N events.
- **Live subscriptions:** per event type, the current subscriber count and owner descriptions.
- **Logging tap:** an optional verbose mode that logs every dispatched event of selected types.

An editor window (`ToyChest.Editor`) presents the trace and subscription table as part of the Milestone 0 developer tooling deliverable. The hook is a bus-level observer: publishers and subscribers require no modification to be observable.

---

# What the Event System Is Not

- **Not a command system.** "DealDamage" is not an event. `DamageApplied` is.
- **Not a request/response mechanism.** Needing a return value means calling a service.
- **Not a scheduler.** There is no delayed or repeating publication; timing belongs to owning systems.
- **Not a network layer.** Replication is a future concern that may forward events, but the bus itself is transport-agnostic and local.

---

# Examples

Resource change propagation:

```
Resource System changes Current Health
    ↓ publishes ResourceChanged
UI updates the health bar
Audio plays a low-health heartbeat
Status Effect System evaluates "below 30%" conditions
```

The Resource System knows none of these listeners exist.

Test usage:

```csharp
var bus = new EventBus();
var received = new List<ResourceChanged>();
using var token = bus.Subscribe<ResourceChanged>(received.Add);
bus.Publish(new ResourceChanged(owner, healthId, 100f, 70f));
Assert.AreEqual(1, received.Count);
```

---

# Extension Points

Planned or possible extensions that require no contract changes:

- **Scoped buses** (per region, per match) composed alongside the global bus.
- **Thread-safe publish queue** for background producers.
- **Event recording and replay** built on the diagnostic trace.
- **Network forwarding** of selected event types once multiplayer arrives.
- **Editor analytics** (event frequency, subscriber leak detection).
- **Gameplay Timeline** (future, not Milestone 0): the Event Trace evolves into a causality visualization that shows how gameplay facts chain together across systems, e.g.

  ```
  AbilityActivated
      ↓
  ResourceSpent
      ↓
  DamageApplied
      ↓
  StatusApplied
      ↓
  QuestAdvanced
  ```

  Because nested dispatch is depth-first and the trace records events in dispatch order, causality chains are already reconstructable from the trace — the Timeline is a presentation layer, not an architectural change.

Explicitly rejected for Milestone 0 (revisit only with a documented need): handler priorities, event inheritance/polymorphic dispatch, async handlers, weak-reference subscriptions.

---

# Success Criteria

The Event System succeeds when:

- Core systems communicate without direct references.
- Adding a new listener never requires touching the publisher.
- Dispatch order is deterministic and tests rely on it.
- Publishing allocates nothing in steady state.
- Engineers can see recent events and current subscriptions in the editor at any time.

---

# Related Documents

- Docs/Architecture/CORE_ARCHITECTURE.md
- Docs/Architecture/PROJECT_ARCHITECTURE.md
- Docs/Systems/GAMEPLAY_FRAMEWORK.md
- Docs/AI_AGENT_INDEX.md
