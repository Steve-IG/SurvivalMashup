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
- TBD

Consumed Events:
- TBD

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

---

# Save System

Only runtime state should be serialized.

Definitions are referenced through stable identifiers.

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