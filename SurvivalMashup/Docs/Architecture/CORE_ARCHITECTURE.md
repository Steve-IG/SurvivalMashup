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

Avoid writing special-case logic for individual weapons, enemies, companions, quests, or regions whenever possible.

Instead, extend existing systems through data and composition.

---

## 2. Data Over Code

If designers should be able to change it, it should live in data.

Examples include:

- Weapons
- Enemies
- Skills
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

- Health
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

Quest System

Owns quests.

Simulation System

Owns world interactions.

Avoid systems that become responsible for unrelated gameplay.

---

## 5. One Source of Truth

Every piece of gameplay data has exactly one authoritative owner.

Example:

HealthComponent owns Health.

No other system stores duplicate health values.

The same rule applies throughout the project.

---

## 6. Event-Driven Communication

Systems communicate through events rather than direct dependencies whenever practical.

Avoid tightly coupling unrelated gameplay systems.

This allows systems to evolve independently.

---

## 7. Simulation First

Whenever possible, gameplay should be expressed through the Simulation System rather than actor-specific logic.

Objects react to properties and interactions rather than who caused them.

---

## 8. Prefer Configuration Over Programming

Adding a new weapon, enemy, companion, or skill should primarily involve creating new data rather than writing new code.

If implementing a new content type consistently requires new code, reconsider the architecture.

---

## 9. Shared Systems

Players, companions, NPCs, bosses, and enemies should share gameplay systems whenever practical.

Examples include:

- Health
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
- Skills
- Damage Types
- Companions
- Quests
- Seasonal Events

should integrate without requiring major refactoring.

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
- Does it follow the Simulation rules?
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

- AI_PLAYBOOK.md
- AI_CODING_STANDARDS.md
- SIMULATION.md
- COMBAT.md
- PROGRESSION.md