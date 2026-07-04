# Attribute System

**Status:** Living Specification
**Version:** 1.0
**Owner:** Gameplay Architecture

---

# Purpose

The Attribute System defines the permanent and temporary characteristics of every actor in the game.

Attributes describe an actor's capabilities.

They influence combat, movement, gathering, crafting, companions, AI behavior, simulation interactions, and progression.

The system must be completely generic and reusable.

Players, companions, enemies, NPCs, bosses, destructible objects, and future actor types should all use the same Attribute framework.

---

# Design Philosophy

Attributes describe capability.

Resources describe current state.

Effects modify resources.

Equipment, progression, companions, status effects, world modifiers, and abilities modify attributes.

No gameplay system should hardcode knowledge of specific attributes.

All attributes should be data-driven.

---

# Core Principles

## Generic

The engine should never hardcode attributes like:

Health

Armor

Strength

Intelligence

Instead, attributes are defined entirely through data.

---

## Universal

Every actor may possess attributes.

Players

Companions

Enemies

Bosses

NPCs

Interactive Objects

Environmental Objects

---

## Data Driven

Adding a new attribute should require no engine changes.

Examples:

Fishing Luck

Harvest Radius

Companion Affinity

Gliding Efficiency

Mining Speed

should all be possible through configuration.

---

## Composable

Attributes should be modified through independent modifier sources rather than custom gameplay code.

---

# Attribute Definition

Each attribute defines:

Identifier

Display Name

Description

Base Value

Minimum Value

Maximum Value

Rounding Rules

Tags

Visibility

Persistence

---

# Attribute Categories

## Vital

Maximum Health

Maximum Energy

Maximum Mana

Maximum Shield

Health Regeneration

Resource Regeneration

---

## Offensive

Attack Power

Ability Power

Critical Chance

Critical Damage

Attack Speed

Projectile Speed

Area Size

Status Chance

Knockback Strength

---

## Defensive

Armor

Block Chance

Dodge Chance

Elemental Resistances

Status Resistance

Crowd Control Resistance

Healing Effectiveness

Damage Reduction

---

## Movement

Movement Speed

Sprint Speed

Jump Height

Air Control

Gravity Scale

Glide Efficiency

Swim Speed

Climb Speed

Pickup Radius

Interaction Radius

---

## Gathering

Harvest Speed

Mining Speed

Woodcutting Speed

Fishing Speed

Gathering Radius

Rare Resource Chance

---

## Companion

Companion Health

Companion Damage

Companion Cooldown Reduction

Companion Utility Speed

Companion Affinity

---

## Utility

Experience Gain

Gold Find

Loot Quality

Crafting Speed

Crafting Efficiency

Vendor Discounts

Inventory Capacity

Durability Efficiency

---

These categories are organizational only.

The engine should not depend on them.

---

# Modifiers

Attributes are modified by independent sources.

Examples:

Equipment

Abilities

Status Effects

Companions

Relics

World Buffs

Difficulty

Temporary Effects

Quest Rewards

Seasonal Events

---

# Modifier Types

Flat

+10 Attack

---

Percentage

+20% Movement Speed

---

Multiplicative

x1.5 Critical Damage

---

Override

Set Gravity Scale

---

Conditional

+50% Fire Damage while Burning

---

Stacking

Each nearby ally grants +5 Armor

---

# Modifier Priority

To ensure deterministic behavior:

1. Base Value

2. Flat Modifiers

3. Additive Percentage

4. Multiplicative Percentage

5. Overrides

This order should remain consistent across all attributes.

---

# Derived Attributes

Some attributes may be calculated.

Examples:

Maximum Carry Weight

Derived from Strength.

Critical Damage

Derived from Weapon + Relics.

Movement Speed

Derived from Base Speed + Equipment + Buffs.

Derived attributes should update automatically.

---

# Events

Attribute changes publish events.

Examples:

Maximum Health Changed

Armor Changed

Move Speed Changed

Critical Chance Changed

Other systems subscribe rather than polling.

---

# Multiplayer

Attributes must support:

Replication

Prediction

Rollback

Authority

Deterministic calculations

---

# AI

AI should evaluate attributes through metadata.

Examples:

Target Armor

Target Threat

Movement Capability

Resistance Profile

Healing Potential

No special AI code should exist for individual attributes.

---

# Future Expansion

New gameplay systems should introduce new attributes rather than new engine logic whenever possible.

Examples:

Sailing

Flying

Magic Schools

Companion Loyalty

Construction Speed

can all be implemented by defining new attributes.

---

# Success Criteria

The Attribute System succeeds when:

- New attributes require no engine changes.
- All actors share the same framework.
- Equipment modifies attributes consistently.
- Effects and abilities can reference attributes generically.
- Designers create new attributes entirely through data.

---

# Implementation Notes

- Attribute definitions should be immutable data assets.
- Runtime attribute values should be stored separately from their definitions.
- Modifiers should be additive and composable rather than embedded in gameplay code.
- Systems should query attributes through interfaces, not direct field access.
- Attribute calculations should be deterministic and independent of presentation systems.