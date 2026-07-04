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

- Bootstrap
- Dependency Injection
- Service Registration
- Configuration
- Logging

Core should remain small and stable.

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

ToyChest.UI

Keep dependencies explicit and minimal.

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

Health

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