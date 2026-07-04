# Damage System

**Architecture:** ToyChest v1.0
**Status:** Living Specification
**Owner:** Gameplay Systems

---

# Purpose

The Damage System evaluates and resolves all forms of gameplay damage.

Damage represents an attempt to change gameplay state.

The target determines how that attempt is resolved.

The system supports combat, harvesting, environmental interactions, companion attacks, abilities, traps, and future gameplay without requiring specialized implementations.

---

# Design Philosophy

Damage is a request.

Damage is not guaranteed.

Every Gameplay Object evaluates incoming damage according to its own properties, attributes, resources, tags, relationships, status effects, and simulation state.

This creates consistent, extensible, and emergent gameplay.

---

# Core Principles

## Universal

Damage is used throughout the game.

Examples:

- Sword attacks
- Fireballs
- Falling rocks
- Explosions
- Harvesting trees
- Mining ore
- Environmental hazards
- Companion attacks
- Traps

---

## Data Driven

Damage behavior should emerge from data.

Adding a new damage type should not require engine changes.

---

## Target Driven

The source proposes damage.

The target resolves it.

---

# Damage Pipeline

Incoming Damage Request

↓

Relationship Evaluation

↓

Target Validation

↓

Damage Modifiers

↓

Resistances

↓

Weaknesses

↓

Critical Resolution

↓

Simulation Reactions

↓

Status Effect Reactions

↓

Resource Changes

↓

Gameplay Events

↓

Presentation

---

# Damage Request

Every damage request contains:

Instigator

Source

Target

Base Amount

Damage Type

Gameplay Tags

Ability (optional)

Weapon (optional)

Region (optional)

Timestamp

---

# Damage Types

Examples include:

Physical

Fire

Cold

Lightning

Poison

Nature

Arcane

Shadow

Holy

Psychic

True

Harvest

Siege

Damage types are defined through data.

---

# Resistances

Targets may reduce incoming damage.

Examples:

Fire Resistance

Armor

Magic Resistance

Poison Immunity

Projectile Resistance

Harvest Efficiency

---

# Weaknesses

Targets may amplify incoming damage.

Examples:

Plant

↓

Weak to Fire

Mechanical

↓

Weak to Lightning

Frozen

↓

Weak to Blunt

Crystal

↓

Weak to Sonic

Weaknesses are driven by tags and attributes rather than hardcoded logic.

---

# Critical Hits

Critical hits are modifiers applied during damage resolution.

Critical behavior should be configurable.

Examples:

- Increased damage
- Guaranteed Status Effect
- Armor Penetration
- Resource Generation
- Area Explosion

---

# Damage Modifiers

Damage may be modified by:

Equipment

Abilities

Status Effects

Companions

Difficulty

World Conditions

Region Effects

Simulation

Relics

Buffs

Debuffs

Modifiers should compose predictably.

---

# Resource Interaction

Damage may affect any resource.

Examples:

Health

Shield

Mana

Energy

Durability

Ammo

Heat

Stress (future)

Damage is not limited to Health.

---

# Simulation Integration

Damage may trigger simulation reactions.

Examples:

Fire Damage

↓

Apply Heat

↓

Ignite Dry Grass

Lightning

↓

Conduct Through Water

Cold

↓

Freeze Surface

Blunt

↓

Break Weak Wall

Harvest

↓

Damage Tree

↓

Spawn Wood

Simulation determines world behavior.

---

# Status Effect Integration

Damage may:

Apply Status Effects

Refresh Status Effects

Remove Status Effects

Amplify existing Status Effects

Examples:

Fire Damage

↓

Burning

Cold Damage

↓

Frozen

Lightning

↓

Electrified

---

# Harvest Damage

Harvesting is simply another form of damage.

Tree

↓

Harvest Damage

↓

Durability Reduced

↓

Destroyed

↓

Wood Spawned

Ore

↓

Mining Damage

↓

Durability Reduced

↓

Ore Spawned

Combat and harvesting use the same framework.

---

# Friendly Fire

Relationship rules determine whether damage may occur.

The Damage System should not special-case Players, Enemies, or Companions.

---

# Events

Resolved damage publishes events.

Examples:

Damage Applied

Critical Hit

Damage Blocked

Object Destroyed

Resource Depleted

Status Applied

Target Defeated

Other systems subscribe rather than polling.

---

# Multiplayer

Damage resolution must be:

Authoritative

Deterministic

Replicated

Predictable

Replayable

---

# AI

AI evaluates:

Expected Damage

Threat

Elemental Matchups

Resource Cost

Target Resistances

Friendly Fire Risk

Simulation Opportunities

AI reasons through metadata rather than handcrafted rules.

---

# Future Expansion

Examples:

Life Steal

Damage Reflection

Armor Penetration

Chain Damage

Piercing

Splash Damage

Environmental Pressure

Corruption

Decay

Construction Damage

All should integrate without architectural changes.

---

# Success Criteria

The Damage System succeeds when:

- Combat and harvesting share the same framework.
- New damage types require only data.
- Simulation reacts naturally.
- AI understands damage generically.
- Multiplayer remains deterministic.
- The system scales to future gameplay without redesign.

---

# Implementation Notes

- Represent incoming damage as immutable `DamageRequest` data passed through a deterministic resolution pipeline.
- Allow systems to contribute modifiers without tightly coupling them to the Damage System.
- Keep presentation (hit flashes, sounds, floating numbers) outside the damage pipeline; respond through events.
- Favor extensible stages in the pipeline over special-case branches.