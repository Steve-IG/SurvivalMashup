# MILESTONE 0 — FOUNDATION

## Purpose

Milestone 0 establishes the foundational gameplay architecture that all future ToyChest systems will be built upon.

The objective is not to create a playable game.

The objective is to create a robust, extensible, data-driven gameplay framework capable of supporting the full ToyChest vision without requiring architectural rewrites.

This milestone ends when the foundation is proven stable enough to begin Vertical Slice development.

---

# Goals

Milestone 0 should:

* Establish core gameplay architecture.
* Validate project structure.
* Validate AI-assisted development workflows.
* Create reusable gameplay systems.
* Eliminate architectural uncertainty before content creation begins.

Milestone 0 should not:

* Create production content.
* Implement regions.
* Implement adventures.
* Implement progression content.
* Implement story content.
* Build the First Hour experience.
* Balance gameplay.

---

# Success Criteria

At the completion of Milestone 0:

* Core gameplay framework exists.
* Core gameplay systems exist.
* Project architecture is validated.
* Documentation reflects implementation.
* New gameplay features can be added without architectural changes.

The team should be confident beginning Vertical Slice production.

---

# Project Foundation

## Unity Project Setup

### Required

* Unity project created and configured.
* Version control configured.
* Assembly Definitions established.
* Namespace conventions established.
* Package dependencies reviewed and documented.
* Build targets configured.

### Exit Criteria

* Clean project checkout compiles successfully.
* No assembly dependency issues.
* No package conflicts.

---

# Repository Structure

## Required

Project folder structure implemented according to Project Architecture documentation.

Examples include:

* Gameplay
* Characters
* Abilities
* Effects
* Inventory
* Equipment
* Interactions
* Regions
* UI
* Audio
* Tools
* Tests

### Exit Criteria

* Folder structure established.
* Assembly boundaries enforced.
* New systems have clear ownership locations.

---

# Gameplay Framework

## Goal

Implement the Gameplay Object architecture defined in documentation.

### Required

* Gameplay Object base architecture.
* Gameplay Component architecture.
* Gameplay Tags.
* Event system.
* Runtime lifecycle.
* Data-driven configuration model.

### Exit Criteria

The framework supports:

* Player characters.
* Enemies.
* Companions.
* NPCs.
* Interactive objects.

Without requiring specialized architecture.

---

# Gameplay Tags

## Goal

Provide a unified tagging system for gameplay logic.

### Required

* Tag definitions.
* Runtime queries.
* Hierarchical tag support.
* Serialization support.

### Exit Criteria

Systems communicate through tags rather than type checks whenever appropriate.

---

# Event Framework

## Goal

Establish low-coupling communication between systems.

### Required

* Event publishing.
* Event subscription.
* Typed events.
* Debug visibility.

### Exit Criteria

Core systems can communicate without direct references.

---

# Attribute Framework

## Goal

Provide reusable attribute support.

### Required

Examples include:

* Health
* Energy
* Mana
* Shields
* Armor
* Harvest Power

The system must remain generic.

### Exit Criteria

New attributes can be added entirely through data.

---

# Gameplay Effect Framework

## Goal

Implement reusable modifications to gameplay state.

### Required

Support:

* Instant effects
* Duration effects
* Periodic effects
* Stackable effects
* Conditional effects

Examples:

* Damage
* Healing
* Buffs
* Debuffs
* Resource generation

### Exit Criteria

Effects are reusable across all actor types.

---

# Ability Framework

## Goal

Implement the foundation for all active gameplay abilities.

### Required

Support:

* Activation requirements
* Cooldowns
* Costs
* Targeting
* Effect application
* Animation hooks
* VFX hooks

### Exit Criteria

Abilities are completely data-driven.

---

# Interaction Framework

## Goal

Support all world interactions.

### Required

Examples:

* Pick up item
* Harvest resource
* Activate object
* Open container
* Talk to NPC

### Exit Criteria

Interactions use a common architecture.

---

# Inventory Foundation

## Goal

Establish inventory architecture.

### Required

* Inventory interfaces
* Item ownership
* Item transfer
* Item stacking
* Serialization support

### Not Required

* Full item database
* Crafting content
* Loot balancing

### Exit Criteria

Inventory architecture exists and is validated.

---

# Equipment Foundation

## Goal

Establish equipment architecture.

### Required

* Equipment slots
* Equip/unequip
* Stat modification hooks
* Ability modification hooks

### Exit Criteria

Equipment integrates cleanly with attributes and abilities.

---

# Save Framework

## Goal

Establish persistence architecture.

### Required

Support saving:

* Player state
* Inventory
* Equipment
* Progression
* World state

### Exit Criteria

Core systems can serialize and restore state.

---

# Developer Tooling

## Required

### Debug Tools

* Gameplay Tag viewer
* Active Effects viewer
* Attribute viewer
* Ability viewer

### Validation Tools

* Data validation
* Missing reference detection
* Tag validation

### Exit Criteria

Engineers and designers can inspect runtime systems efficiently.

---

# Automated Testing

## Required

Coverage for:

* Tags
* Attributes
* Effects
* Abilities
* Inventory
* Save system

### Exit Criteria

Core architecture has automated validation.

---

# Documentation

## Required

Architecture documentation updated to reflect implementation.

Every foundational system must include:

* Purpose
* Responsibilities
* Dependencies
* Extension points

### Exit Criteria

Documentation and implementation remain synchronized.

---

# AI Development Workflow

## Required

All implementation work must:

* Follow Engineering Principles.
* Follow AI Coding Standards.
* Follow AI Playbook.
* Update documentation when architecture changes.

### Exit Criteria

AI-generated code integrates consistently with project architecture.

---

# Multiplayer Scope

Milestone 0 does **not** implement multiplayer.

Milestone 0 implements **multiplayer-compatible architecture**:

* Deterministic gameplay calculations.
* Strict separation of immutable definitions from mutable runtime state.
* Stable identifiers for all definitions and gameplay objects.
* All state mutation flowing through owning systems (compatible with future server authority).
* No wall-clock or presentation dependencies inside gameplay logic.

No transport, replication, prediction, or rollback code is written during Milestone 0. Networking is layered on later without redesigning gameplay systems, per `Docs/Architecture/ENGINE_PRINCIPLES.md` Principle 11.

---

# Explicitly Out of Scope

The following belong to Milestone 1 (Vertical Slice):

* First Hour implementation
* Hub World
* Regions
* Adventures
* Enemies
* Bosses
* Merchants
* Companions
* Story content
* Progression content
* Crafting content
* Loot balancing
* Buildcraft balancing

Milestone 0 builds the engine.

Milestone 1 builds the game.

---

# Milestone 0 Exit Review

Before Milestone 0 is considered complete, verify:

* Architecture is stable.
* Systems are reusable.
* Systems are data-driven.
* Documentation is current.
* Tests pass.
* No known architectural blockers remain.

Only after passing this review should Vertical Slice production begin.
