# Gameplay Object

**Architecture:** ToyChest v1.0  
**Status:** Living Specification  
**Owner:** Core Architecture

---

# Purpose

The Gameplay Object is the fundamental runtime entity of the ToyChest Architecture.

Every interactive entity in the game world is represented as a Gameplay Object.

Gameplay Objects expose capabilities through composable components.

Gameplay Systems operate on those capabilities to produce gameplay.

Gameplay Objects themselves contain little or no gameplay logic.

This is the canonical Gameplay Object concept document.

This document defines the conceptual design role of Gameplay Objects. `Docs/Systems/GAMEPLAY_FRAMEWORK.md` defines the runtime framework that implements this concept.

---

# Design Philosophy

Gameplay Objects describe **what they are.**

Gameplay Systems determine **what happens.**

This separation keeps gameplay modular, data-driven, and extensible.

New gameplay should emerge from composing existing systems rather than creating specialized object classes.

---

# Core Principles

## Universal

Every interactive object is a Gameplay Object.

Examples:

Player

Enemy

Companion

NPC

Tree

Rock

Treasure Chest

Projectile

Trap

Door

Merchant

Crafting Station

Harvest Node

Dropped Item

Region Portal

Future gameplay objects should reuse the same architecture.

---

## Composition Over Inheritance

Gameplay Objects are composed from reusable components.

Examples include:

Attributes

Resources

Gameplay Tags

Relationships

Abilities

Gameplay Effects

Inventory

Equipment

World Properties

Presentation

Interaction

No large inheritance hierarchies should exist.

---

## Data Driven

Gameplay Objects are created from immutable Definitions and mutable runtime Instances.

Behavior is configured through data.

Adding new object types should rarely require new engine code.

---

# Architecture

Gameplay Definition (ScriptableObject)

↓

Gameplay Object Instance

↓

Capability Components

↓

Gameplay Systems

↓

Gameplay Events

---

# Capability Components

Gameplay Objects may expose any combination of the following capabilities.

## Attributes

Examples:

Maximum Health

Strength

Armor

Movement Speed

Critical Chance

Attributes describe persistent gameplay statistics.

---

## Resources

Examples:

Current Health

Mana

Energy

Durability

Ammo

Resources change during gameplay.

---

## Gameplay Tags

Examples:

Player

Enemy

Fire

Organic

Mechanical

Harvestable

Legendary

Tags describe identity and enable generic queries.

---

## Relationships

Examples:

Owner

Ally

Enemy

Neutral

Party Member

Faction

Relationships influence gameplay interactions.

---

## Abilities

Gameplay Objects may own abilities.

Examples:

Sword Slash

Fireball

Leap

Harvest

Heal

Abilities are executed by the Ability System.

---

## Gameplay Effects

Gameplay Objects may receive passive or temporary effects.

Examples:

Burning

Frozen

Regeneration

Shielded

Poisoned

Gameplay Effects are managed independently.

---

## Inventory

Gameplay Objects may own Item Instances.

Examples:

Player

Merchant

Companion

Treasure Chest

Enemy Corpse

Inventory is optional.

---

## Equipment

Gameplay Objects may equip Item Instances.

Examples:

Player

Companion

Enemy

Equipment activates gameplay capabilities.

---

## World Properties

Gameplay Objects expose properties used by the World Reaction System.

Examples:

Flammable

Wet

Frozen

Heat

Structural Integrity

Conductive

---

## Interaction

Gameplay Objects may expose interactions.

Examples:

Open

Harvest

Talk

Trade

Activate

Rescue

Interactable behavior remains data-driven.

---

## Presentation

Presentation includes:

Meshes

Animations

Audio

Particles

UI

Presentation should remain independent of gameplay logic.

---

# Lifecycle

Gameplay Objects typically follow this lifecycle:

Definition Loaded

↓

Instance Created

↓

Components Initialized

↓

Gameplay Activated

↓

Gameplay Updated

↓

Gameplay Events

↓

Destroyed or Persisted

---

# Events

Gameplay Objects publish and receive Gameplay Events.

Examples:

Spawned

Destroyed

Damaged

Healed

Interaction Started

Interaction Completed

Equipment Changed

Inventory Changed

Status Applied

Objects remain loosely coupled through events.

---

# Multiplayer

Gameplay Objects support:

Replication

Authority

Prediction

Persistence

Deterministic behavior

The networking layer should operate generically on Gameplay Objects.

---

# AI

AI reasons about Gameplay Objects through capabilities rather than concrete types.

Examples:

Has Fire Tag

Low Health Resource

Hostile Relationship

Carries Valuable Loot

Near Water

AI should query capabilities rather than recognize subclasses.

---

# Future Expansion

Examples:

Vehicles

Mounts

Housing

Pets

Siege Weapons

Factories

Machines

Interactive Puzzles

New gameplay should emerge through composition rather than architectural changes.

---

# Uses ToyChest Systems

Ability System

Attribute System

Resource System

Gameplay Effect System

Gameplay Tags

Relationship System

Damage System

World Reaction System

Item System

Inventory System

Equipment System

Gameplay Events

Definition Composition

---

# Success Criteria

The Gameplay Object architecture succeeds when:

- Every gameplay entity follows the same architectural model.
- New gameplay objects are created primarily through composition.
- Gameplay systems remain independent and reusable.
- AI, networking, save/load, and tools operate on Gameplay Objects generically.
- Designers can create new content largely through data.

---

# Implementation Notes

- Represent Gameplay Objects as lightweight runtime containers of capabilities.
- Favor optional components over mandatory ones.
- Avoid object-specific gameplay logic whenever possible.
- Route behavior through specialized Gameplay Systems.
- Treat Gameplay Objects as the common language shared by every system in the engine.