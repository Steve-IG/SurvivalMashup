# Data Registry

**Architecture:** ToyChest v1.0
**Status:** Living Specification
**Version:** 1.0
**Owner:** Gameplay Architecture

---

## System Ownership

This system owns:
- Definition registration, definition lookup, stable identifier mapping, and duplicate detection.

This system does NOT own:
- Definition meaning, definition content, gameplay rules, asset loading policy, or gameplay state. The Data Registry provides access to definitions; it does not interpret them.

Primary Responsibilities:
- Serve as the canonical runtime source for immutable gameplay definitions.

Primary Data:
- None of its own. The registry indexes definitions owned by their respective systems.

Primary Runtime Objects:
- The Data Registry instance and its per-type definition buckets.

Published Events:
- None. Registration happens during initialization, before gameplay observes state.

Consumed Events:
- None.

---

# Purpose

The Data Registry is the canonical runtime source for gameplay definitions.

Every immutable definition — Ability Definitions, Attribute Definitions, Resource Definitions, Gameplay Effect Definitions, Status Effect Definitions, Item Definitions, Equipment Definitions, Gameplay Tag Definitions — is registered once at startup and looked up through the registry thereafter.

Runtime systems never load definitions ad hoc, never hold scene references to definition assets, and never duplicate definition data. They ask the registry.

Per `Docs/Architecture/CORE_ARCHITECTURE.md`: the Data Registry provides access to definitions; it does not interpret gameplay meaning. Systems own their own data.

The registry lives in the Framework layer (`ToyChest.Framework.Data`), alongside the Event Bus: shared infrastructure that gameplay systems depend on, which itself knows nothing about any specific system.

---

# Design Goals

1. **One authoritative lookup.** A definition id resolves to exactly one definition. Duplicate registration is an error, detected immediately at startup — never a silent overwrite.
2. **Stable identifiers.** Definitions are addressed by durable string ids that survive renames of assets, classes, and folders. Saves, networking, and tooling all reference definitions through these ids.
3. **Loading-mechanism agnostic.** The registry integrates cleanly with Addressables but is not coupled to it. Definitions arrive through sources; the registry does not know or care how a source acquired them.
4. **Deterministic.** Lookup results and enumeration order are stable: `GetAll` returns definitions in registration order.
5. **Plain C#.** Fully testable outside Unity. ScriptableObjects are the standard authoring format, but the registry only requires the `IDefinition` contract.
6. **Fail early, fail clearly.** Missing and duplicate ids throw descriptive exceptions at the earliest possible moment, naming the id and the type involved.

---

# Stable Identifiers

`DefinitionId` is a validated value type wrapping a non-empty string.

Conventions:

- Lowercase, dot-separated namespacing by definition family: `ability.fireball`, `attribute.max-health`, `item.iron-ore`, `status.burning`.
- Tag Definitions use their tag path as their id (`Element.Fire.Burning`) — the path already is the stable identity.
- Ids never change after content ships. Renaming display names is free; renaming ids is a documented breaking change (see the Event Evolution guidance in `Docs/Architecture/EVENT_SYSTEM.md` for the same philosophy).

Identity comparisons are ordinal; hash codes are cached at construction. Definition lookups occur at spawn and configuration time, not in per-frame gameplay loops, so string-keyed dictionary resolution is deliberately acceptable.

---

# The Definition Contract

```csharp
public interface IDefinition
{
    DefinitionId Id { get; }
}
```

`GameplayDefinition` is the standard ScriptableObject base class implementing this contract with a serialized id field and editor validation. All system definition types (AbilityDefinition, AttributeDefinition, ...) derive from it unless they have a natural id of their own (TagDefinition uses its tag path).

Definitions are immutable at runtime. The registry hands out references; nothing mutates them. Runtime state lives in instances, never in definitions (`Docs/Architecture/ENGINE_PRINCIPLES.md`, Principle 14).

---

# Registry API

```csharp
public interface IDataRegistry
{
    void Register(IDefinition definition);
    TDefinition Get<TDefinition>(DefinitionId id) where TDefinition : class, IDefinition;
    bool TryGet<TDefinition>(DefinitionId id, out TDefinition definition) where TDefinition : class, IDefinition;
    IReadOnlyList<TDefinition> GetAll<TDefinition>() where TDefinition : class, IDefinition;
    bool Contains<TDefinition>(DefinitionId id) where TDefinition : class, IDefinition;
}
```

Rules:

- Definitions are bucketed by their **concrete runtime type**. `Get<TagDefinition>` finds definitions registered as `TagDefinition`. Base-type and interface queries are not supported in Milestone 0; introduce them only with a real need.
- `Register` throws on duplicate `(type, id)` pairs.
- `Get` throws with a descriptive message when the id is unknown; `TryGet` is the tolerant form for genuinely optional lookups.
- The registry is populated during initialization and read-only thereafter by convention. It is not a runtime content-streaming system.

Like the Event Bus, the registry is created at bootstrap and provided through constructor injection. There is no global singleton.

---

# Definition Sources

Definitions arrive through sources:

```csharp
public interface IDefinitionSource
{
    IEnumerable<IDefinition> LoadDefinitions();
}
```

Bootstrap enumerates its configured sources and registers everything they yield. The registry never initiates loading.

Planned sources:

- **Addressables source** (bootstrap, Milestone 0 scope ends at the interface): loads all assets with a definitions label, then yields them. Addressables coupling lives entirely inside this source — the registry and all gameplay systems remain unaware of it.
- **Direct source** (tests, tooling): yields in-memory or directly referenced definitions.

Asynchronous preloading happens inside a source before it yields; the registry's registration pass itself is synchronous and deterministic.

---

# Registry Lifecycle

The registry has three distinct phases. Keeping them separate is what makes lookups deterministic and content errors surface early.

## 1. Construction

The registry is created empty at bootstrap, before any gameplay system initializes. It is provided to systems through constructor injection. Nothing gameplay-related has run yet.

## 2. Population

Bootstrap enumerates its configured definition sources and registers everything they yield. This phase is synchronous and deterministic: any asynchronous loading (Addressables preloading) completes inside a source before it yields, so registration order depends only on the source order, not on load timing.

Population is the only phase in which `Register` is called. Duplicate or invalid ids throw here, at startup — the earliest possible moment — never mid-session.

Downstream structures are built at the end of this phase from the populated registry: for example, the Gameplay Tag Table interns every `TagDefinition` the registry now holds.

## 3. Read-Only Serving

Once population completes, the registry is read-only by convention for the rest of the session. Systems call `Get`, `TryGet`, `GetAll`, and `Contains`; nothing calls `Register`. Because definitions are immutable and no registration occurs, lookups are free of ordering hazards and safe to call from any gameplay system at spawn or configuration time.

## Teardown

The registry holds only references to immutable definitions and owns no runtime gameplay state, so teardown is simply releasing the instance. It participates in no save data: saves reference definitions by stable id and re-resolve them against a freshly populated registry on load. A definition id present in a save but absent from the registry is a content error, reported clearly, never silently defaulted.

## Editor Iteration (future)

Live re-registration for designer iteration (hot-reload) is an explicit future extension. It would introduce a controlled invalidation step and is deliberately out of Milestone 0 scope; the read-only-after-population rule holds until such a mechanism is designed.

---

# Relationship to Other Systems

- **Tag System:** TagDefinition assets are registered like any other definition; bootstrap then feeds them to the Gameplay Tag Table, which interns the hierarchy for runtime queries. The registry is the source of truth for which tags exist; the table is the runtime query structure.
- **Save System:** saves reference definitions by `DefinitionId` only. On load, ids resolve through the registry; a missing definition is a content error reported clearly, never silently defaulted.
- **Future systems:** any new definition type gains registry support by implementing `IDefinition`. No registry changes required.

---

# Examples

```csharp
// Bootstrap
var registry = new DataRegistry();
foreach (IDefinitionSource source in sources)
{
    foreach (IDefinition definition in source.LoadDefinitions())
    {
        registry.Register(definition);
    }
}

// A system resolving a definition
var fireball = registry.Get<AbilityDefinition>(new DefinitionId("ability.fireball"));

// Enumerating for table building
tagTable.RegisterDefinitions(registry.GetAll<TagDefinition>());
```

---

# Extension Points

- **Addressables-backed source** at bootstrap (planned within Milestone 0's service initialization).
- **Hot-reload for designer iteration** (editor-only re-registration; requires an explicit invalidation contract — not Milestone 0).
- **Validation pass** run by editor tooling: orphaned ids, duplicate ids across sources, naming-convention violations.
- **Base-type queries** if a real consumer appears.

---

# Success Criteria

The Data Registry succeeds when:

- Every definition in the game is reachable through one lookup path.
- Saves and future networking reference content by stable id without breaking across renames.
- Swapping the loading mechanism touches only definition sources, never gameplay systems.
- Duplicate or missing content ids are caught at startup, not mid-session.

---

# Related Documents

- Docs/Architecture/CORE_ARCHITECTURE.md
- Docs/Architecture/EVENT_SYSTEM.md
- Docs/Architecture/PROJECT_ARCHITECTURE.md
- Docs/Systems/TAG_SYSTEM.md
- Docs/AI_AGENT_INDEX.md
