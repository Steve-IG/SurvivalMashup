# Core Architecture

**Status:** Living Specification
**Version:** 1.0
**Owner:** Technical Director
**Last Updated:** June 2026

---

# Purpose

This document defines the architectural principles that govern every gameplay system in the project.

The objective is not simply to build a working game, but to build a game that remains maintainable, extensible, and AI-friendly throughout years of development.

Whenever implementation decisions conflict with these principles, these principles take precedence.

Documentation authority, canonical terminology, and AI read order are defined in `Docs/AI_AGENT_INDEX.md`.

---

# Core Philosophy

The architecture should maximize:

- Simplicity
- Consistency
- Extensibility
- Data-driven design
- AI-assisted development

The game should grow by adding new data and systems rather than increasing complexity within existing systems.

---

# The Ten Principles

## 1. Build Systems, Not Exceptions

Gameplay should emerge from reusable systems.

Avoid writing special-case logic for individual weapons, enemies, companions, adventures, or regions whenever possible.

Instead, extend existing systems through data and composition.

---

## 2. Data Over Code

If designers should be able to change it, it should live in data.

Examples include:

- Weapons
- Enemies
- Abilities
- Loot Tables
- Regions
- Crafting Recipes
- Companion Definitions
- Status Effects

Avoid hardcoding gameplay values.

---

## 3. Composition Over Inheritance

Favor small reusable components over deep inheritance hierarchies.

Objects should gain behavior by combining components.

Example:

Enemy

- Current Health Resource
- Maximum Health Attribute
- Damageable
- Status Effects
- Navigation
- AI Brain
- Loot Dropper

rather than inheriting from a massive EnemyBase class.

---

## 4. Single Responsibility

Every system owns one domain.

Examples:

Combat System

Owns combat.

Inventory System

Owns inventory.

Adventure System

Owns region experiences, objectives, and adventure progression.

World Reaction System

Owns world interactions.

Avoid systems that become responsible for unrelated gameplay.

---

## 5. One Source of Truth

Every piece of gameplay data has exactly one authoritative owner.

Example:

Resource System owns Current Health.

Attribute System owns Maximum Health and health regeneration.

No other system stores duplicate health values.

The same rule applies throughout the project.

---

## 6. Event-Driven Communication

Systems communicate through events rather than direct dependencies whenever practical.

Avoid tightly coupling unrelated gameplay systems.

This allows systems to evolve independently.

---

## 7. World Reaction First

Whenever possible, gameplay should be expressed through the World Reaction System rather than actor-specific logic.

Objects react to properties and interactions rather than who caused them.

---

## 8. Prefer Configuration Over Programming

Adding a new weapon, enemy, companion, or ability should primarily involve creating new data rather than writing new code.

If implementing a new content type consistently requires new code, reconsider the architecture.

---

## 9. Shared Systems

Players, companions, NPCs, bosses, and enemies should share gameplay systems whenever practical.

Examples include:

- Current Health
- Maximum Health
- Damage
- Status Effects
- Buffs
- Abilities
- Equipment
- Resistances

Avoid creating parallel implementations of similar mechanics.

---

## 10. Build for Expansion

Every major system should assume that future content will exist.

New:

- Regions
- Weapons
- Abilities
- Damage Types
- Companions
- Adventures
- Seasonal Events

should integrate without requiring major refactoring.

---

# Dependency Rules

Dependencies should flow in one direction.

Higher-level layers may depend on lower-level layers.

Lower-level layers must not depend on higher-level layers.

Layer order:

Presentation / UI

↓

Gameplay Systems

↓

Gameplay Framework

↓

Core Services

↓

Engine

Rules:

- Presentation / UI may observe gameplay state and send player intent, but must not own gameplay rules.
- Gameplay Systems may depend on the Gameplay Framework, Core Services, and Engine capabilities.
- Gameplay Systems must not depend on Presentation / UI.
- Gameplay Framework may depend on Core Services and Engine capabilities.
- Gameplay Framework must not depend on specific Gameplay Systems.
- Core Services may depend on Engine capabilities.
- Core Services must not own gameplay state or gameplay rules.
- Engine code must not depend on project-specific gameplay, UI, or content.

Services expose capabilities.

They do not own gameplay domains.

Examples:

Save System

Serializes and restores runtime state.

It does not own inventory, health, progression, region state, or companion state.

State Ownership vs Serialization Responsibility

Gameplay systems own their own persistent state.

Examples:

Inventory System owns inventory state.

Resource System owns resource state.

Progression systems own progression state.

World and Region systems own world and region state.

Companion systems own companion state.

The Save System coordinates serialization and deserialization.

It does not decide what gameplay state means.

It does not validate gameplay rules.

It does not repair gameplay state through gameplay logic.

It does not become the owner of any state because that state is saved.

When loading, the Save System restores state back to the system that owns it.

The owning system remains responsible for interpreting, validating, and using that state according to its own rules.

Event Bus

Transports events between systems.

It does not decide gameplay outcomes.

Data Registry

Provides access to definitions.

It does not interpret gameplay meaning.

Systems own their own data.

If a system needs information owned by another system, it should request it through a stable interface or react to events.

Communication should prefer events over direct coupling where practical.

Direct references are allowed only when ownership is clear, dependency direction is valid, and the relationship is stable.

---

# Event Architecture

An event represents something that has already happened.

Events are notifications, not commands.

Use events when:

- A system needs to announce a completed state change.
- Multiple unrelated systems may need to react.
- The publisher should not know who is listening.
- The response is optional, indirect, or cross-cutting.

Prefer direct service calls when:

- A system needs a specific capability immediately.
- The caller requires a return value.
- The relationship is stable and follows dependency rules.
- The operation belongs to a clearly owned service.

Events should remain data-oriented.

They should describe what happened, who or what was involved, and any stable context required by listeners.

Events should not contain business logic.

Events should not decide outcomes.

Events should not be used to tell another system what to do.

Systems publish events rather than directly orchestrating unrelated systems.

Listeners may react to events, but ownership remains with the system that owns the affected gameplay domain.

---

# Architectural Anti-Patterns

The following practices are prohibited:

- Manager-to-manager coupling.
- Circular dependencies between systems.
- Duplicated ownership of the same gameplay state.
- Business logic inside UI.
- UI directly modifying gameplay state.
- Save System owning gameplay state.
- Core Services deciding gameplay outcomes.
- Systems reaching into another system's internals.
- Special-case gameplay branches for individual content unless explicitly approved.
- Parallel implementations of shared mechanics.

If an implementation requires one of these patterns, stop and reconsider the design before coding.

---

# AI Development Principles

AI is a core member of the development team.

Every AI-generated implementation should strive to:

- Extend existing systems.
- Avoid unnecessary duplication.
- Prefer reusable solutions.
- Minimize coupling.
- Produce self-documenting code.
- Follow existing project conventions.

AI should solve the requested problem without redesigning unrelated systems.

---

# Unity Principles

Unity-specific implementation should emphasize:

- Prefab composition
- ScriptableObjects for game data
- Addressables for content management
- Assembly Definitions for modularity
- Dependency Injection where appropriate
- Minimal MonoBehaviour logic

MonoBehaviours should primarily orchestrate Unity lifecycle events.

Gameplay logic should live in reusable C# classes.

---

# Code Quality Standards

Code should be:

- Readable
- Predictable
- Testable
- Modular
- Well documented where necessary

Favor clarity over cleverness.

---

# Refactoring Policy

AI should improve existing systems rather than replacing them.

Large architectural rewrites require explicit approval.

Avoid unnecessary churn.

---

# Performance Philosophy

Optimize when necessary, not prematurely.

However:

Avoid architecture that fundamentally prevents future optimization.

Data-oriented improvements should be possible without redesigning gameplay systems.

---

# Architectural Review Questions

Before introducing a new system, ask:

- Does this duplicate an existing system?
- Can this be data-driven?
- Can this be expressed through composition?
- Does it follow the World Reaction rules?
- Will AI understand this pattern?
- Can designers extend it without engineering?
- Will multiplayer support this architecture?
- Will this still work after 100 new content additions?

---

# Success Criteria

The architecture is successful if:

- Designers create new content without engineers.
- AI consistently extends systems instead of replacing them.
- Features remain modular.
- New gameplay systems integrate naturally.
- The codebase becomes easier to understand over time rather than harder.

---

# Related Documents

- Docs/AI_AGENT_INDEX.md
- Docs/Architecture/AI_PLAYBOOK.md
- Docs/Architecture/AI_CODING_STANDARDS.md
- Docs/Architecture/CODING_PRINCIPLES.md
- Docs/Architecture/ENGINE_PRINCIPLES.md
- Docs/Architecture/PROJECT_ARCHITECTURE.md
- Docs/Architecture/EVENT_SYSTEM.md
- Docs/Architecture/DATA_REGISTRY.md
- Docs/Architecture/GAMEPLAY_OBJECT.md
- Docs/Systems/GAMEPLAY_FRAMEWORK.md
- Docs/Systems/WORLD_REACTION_SYSTEM.md
- Docs/Systems/COMBAT.md
- Docs/Systems/PROGRESSION.md