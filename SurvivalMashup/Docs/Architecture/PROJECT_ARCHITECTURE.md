# Project Architecture

**Architecture:** ToyChest v1.0
**Status:** Living Specification
**Priority:** Highest

---

# Purpose

This document defines the physical organization of the Unity project.

Its purpose is to ensure that every file has a clear home, every system has a clear owner, and the repository remains understandable as the project grows.

A consistent repository structure is essential for long-term maintainability, AI-assisted development, multiplayer support, and onboarding new developers.

When adding new code or content, follow this document before creating new folders or files.

Documentation authority, canonical terminology, and AI read order are defined in `Docs/AI_AGENT_INDEX.md`.

---

# Design Philosophy

The project is organized by **ownership**, not by file type.

Every folder should answer one question:

**Who owns this?**

Avoid generic folders such as:

- Scripts
- Utilities
- Managers
- Helpers
- Misc
- New
- Prototype
- Test

Every folder should represent a coherent gameplay system or domain.

---

# High-Level Repository Layout

Assets/

```
Game/
ThirdParty/
Generated/
Documentation/
Art/
Audio/
```

Each top-level folder has a distinct responsibility.

---

# Game/

Contains all first-party gameplay code and content.

```
Game/

    Runtime/

    Content/

    Scenes/

    Editor/

    Tests/

    UI/

    Art/

    Audio/
```

---

# Runtime/

Contains production gameplay code.

Nothing in this folder should contain game-specific content.

Runtime implements the ToyChest Architecture.

```
Runtime/

    Core/

    Framework/

    Systems/

    Gameplay/

    Networking/

    Save/

    AI/

    Utilities/
```

---

# Core/

Contains project-wide infrastructure.

Examples:

- Dependency Injection
- Service Registration
- Configuration
- Logging

Core should remain small and stable.

**Bootstrap placement.** The *application bootstrap* — the composition entry point that wires the whole stack (Gameplay, Save, Addressables) and drives the startup sequence in `Docs/Architecture/ENGINE_STARTUP.md` — lives in a dedicated top-of-stack assembly (`ToyChest.Boot`, `Runtime/Boot/`), not in Core. It sits *above* every layer because it must reference all of them, so placing it in Core would invert the one-directional dependency rule below. Core holds only the small, stable bootstrap *infrastructure* that lower layers may depend on (configuration, logging). Nothing references `ToyChest.Boot`.

**Dev tooling placement.** Runtime debugging tools (currently the read-only `GameplayDebugOverlay`) live in a separate top-of-stack assembly `ToyChest.Debugging` (`Runtime/Debugging/`), also referenced by nothing. It sits above the stack so it may inspect every system, and is kept out of `ToyChest.Boot` so the production startup assembly carries no debug UI. Debug tools only read live state; they never mutate gameplay.

---

# Framework/

Contains the foundational architecture.

Examples:

- Gameplay Object
- Components
- Event Bus
- Lifecycle
- Runtime Context

Framework should rarely change.

---

# Systems/

Contains reusable engine systems.

Each system owns its own folder.

Example:

```
Systems/

    Ability/

    Attribute/

    Resource/

    GameplayEffects/

    StatusEffects/

    Tags/

    WorldReaction/

    Inventory/

    Equipment/

    Loot/

    Crafting/

    Dialogue/

    Adventure/

    Save/

    Audio/

    Input/
```

Each system owns:

- Runtime code
- Data definitions
- Internal utilities
- Tests

A system should not expose unnecessary implementation details.

---

# Gameplay/

Contains gameplay-specific implementations built on top of engine systems.

Examples:

```
Gameplay/

    Player/

    Companion/

    Enemy/

    NPC/

    Region/

    World/

    Camera/

    Interaction/
```

Gameplay composes systems.

Gameplay should rarely implement low-level functionality.

---

# Content/

Contains immutable gameplay definitions.

Examples:

```
Content/

    Abilities/

    Weapons/

    Armor/

    Items/

    Companions/

    Enemies/

    Bosses/

    Regions/

    LootTables/

    Adventures/

    Dialogue/

    StatusEffects/

    Attributes/

    Resources/

    Tags/
```

Content should primarily consist of ScriptableObject assets and other authoring data.

Definitions loaded at runtime carry the Addressables `definitions` label; `AddressablesDefinitionSource` (`ToyChest.Boot`) loads every labelled asset into the Data Registry at startup, so authoring a new definition is creating the asset and labelling it — no code change. The first authored content, `Content/Definitions/` (the Wooden Crate smoke set), demonstrates the full pipeline.

---

# Scenes/

`Assets/Game/Scenes` is the canonical home for first-party ToyChest scenes, separate from any third-party sample scenes.

```
Scenes/

    Bootstrap.unity          (application entry; ENGINE_STARTUP.md phase 1; first in Build Settings)

    VerticalSlice.unity      (Milestone 1 gameplay scene; player, camera, interactables)

    Hub.unity                (placeholder)

    MissionPrototype.unity   (placeholder)
```

`Bootstrap.unity` holds the `GameBootstrap` entry point (`ToyChest.Boot`) and performs engine initialization only. After startup it transitions into `VerticalSlice.unity`, the dedicated gameplay scene where all Milestone 1 gameplay lives (`Docs/Development/MILESTONE_1_VERTICAL_SLICE.md`). Bootstrap is ordered first in Build Settings, VerticalSlice second, so a build launches through the startup path and then into gameplay. Hub and MissionPrototype are placeholders until their gameplay systems exist.

**Scene composition.** Scene-authored Gameplay Objects (the player, interactables, props) are composed by `GameplayObjectSpawner` (`ToyChest.Gameplay`), the canonical Unity adapter that runs the existing `GameplayObjectFactory` and binds the result to the sibling `GameplayObjectBehaviour`. The Boot layer injects the assembled services into each loaded gameplay scene once through a small `GameplaySceneContext` / `IGameplaySceneParticipant` seam — scene components never fetch a global or a service locator. This is the "prefab composition" extension point named in `Docs/Systems/GAMEPLAY_FRAMEWORK.md`, and it is the standard scene composition path until streaming/scene-loading systems arrive.

---

# Editor/

Contains Unity editor extensions.

Examples:

- Custom inspectors
- Validation tools
- Importers
- Content generators
- Build tools

Editor code should never be included in runtime builds.

---

# Tests/

Contains automated tests.

Organize tests by system ownership rather than by test type.

Example:

```
Tests/

    Ability/

    Inventory/

    Equipment/

    WorldReaction/
```

---

# ThirdParty/

Contains external packages.

Never modify third-party code directly.

If customization is required:

- Wrap it.
- Extend it.
- Document it.

---

# Generated/

Contains machine-generated files.

Examples:

- Addressables
- AI-generated assets
- Localization output
- Build artifacts

Developers should avoid manually editing generated content.

---

# Documentation/

Contains all project documentation.

Suggested structure:

```
Documentation/

    Vision/

    Gameplay/

    Engine/

    AI/

    Decisions/

    Implementation/
```

---

# Folder Ownership

Every major folder should contain a README.md describing:

Purpose

Owner

Dependencies

Public API

Things it may reference

Things it must never reference

This prevents architectural drift over time.

---

# Dependency Rules

Dependencies should always flow downward.

```
Gameplay

↓

Systems

↓

Framework

↓

Core
```

Lower layers must never reference higher layers.

Examples:

Framework should not know about:

Player

Enemy

Weapon

Adventure

Companion

Likewise:

Systems should not depend on gameplay implementations.

---

# Assembly Definitions

Create assembly definitions by ownership.

Example:

ToyChest.Core

ToyChest.Framework

ToyChest.Abilities

ToyChest.Attributes

ToyChest.Inventory

ToyChest.Gameplay

ToyChest.Gameplay.Player

ToyChest.UI

Keep dependencies explicit and minimal.

Player-facing Unity behavior (movement, input, camera, interaction) lives in `ToyChest.Gameplay.Player`, a thin-adapter assembly above `ToyChest.Gameplay`. It composes existing engine capabilities and references Unity's Input System; it holds no gameplay rules.

NPC-facing Unity behavior lives in `ToyChest.Gameplay.Npc` (`Runtime/Gameplay/Npc/`), the parallel thin-adapter assembly for autonomous world actors. Like the player adapters it holds no gameplay rules: `NpcWanderLocomotion` reads the composed object's Movement Speed attribute and drives a `CharacterController` from the pure, deterministic `WanderMotor` (the NPC counterpart to `LocomotionMotor`). An NPC is an ordinary Gameplay Object composed by the same `GameplayObjectSpawner`; its identity, resources, interactions, and abilities are authored data, not code. The assembly references only `ToyChest.Framework` and `ToyChest.Systems.Attributes` — it has no dependency on the player assembly, no input, and introduces no manager, scheduler, or behavior framework.

---

# Naming Conventions

Folders:

PascalCase

Classes:

PascalCase

Interfaces:

IExample

Events:

Past tense

Examples:

HealthChanged

AbilityActivated

ItemCollected

Methods:

VerbNoun

Variables:

camelCase

Constants:

UPPER_CASE only when truly constant

Avoid abbreviations unless universally understood.

---

# ScriptableObject Philosophy

Use ScriptableObjects for immutable definitions.

Examples:

AbilityDefinition

WeaponDefinition

ItemDefinition

CompanionDefinition

StatusEffectDefinition

Runtime state should never be stored in ScriptableObjects.

---

# Runtime Philosophy

Runtime objects should own mutable state.

Examples:

Cooldowns

Current Health

Mana

Inventory

Position

Status Effects

Temporary Buffs

---

# Event Philosophy

Systems communicate through events whenever practical.

Avoid direct references between unrelated systems.

Favor loose coupling.

---

# AI Development

Every system should expose clear boundaries.

AI agents should be able to identify:

- Ownership
- Responsibilities
- Dependencies
- Public interfaces

without scanning the entire project.

Repository organization should reduce ambiguity.

---

# Architectural Review Checklist

Before adding a new file:

1. Does an appropriate system already own this?
2. Can this be implemented by extending an existing system?
3. Is a new folder actually necessary?
4. Will another developer immediately know where to find this file?
5. Does this follow the dependency rules?

---

# Success Criteria

The Project Architecture succeeds when:

- Every file has an obvious location.
- Folder ownership is unambiguous.
- Dependencies remain one-directional.
- AI agents consistently place files in the correct locations.
- New developers can navigate the repository without guidance.
- The repository scales gracefully to thousands of source files.