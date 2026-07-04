# Resource System

**Status:** Living Specification
**Version:** 1.0
**Owner:** Gameplay Architecture

---

## System Ownership

This system owns:
- Current Resource values, regeneration, consumption, restoration, transfer, clamping, overflow, and resource change reporting.

This system does NOT own:
- Ability activation, Attribute definitions, Damage resolution, UI bars, or Status Effect rules.

Primary Responsibilities:
- Manage all consumable, rechargeable, spendable, and transferable gameplay resources generically.

Primary Data:
- Resource definitions, min/max values, regeneration rules, overflow rules, tags, visibility, and persistence.

Primary Runtime Objects:
- Resource Components and runtime resource values.

Published Events:
- Resource Depleted, Resource Restored, Resource Overflow, Resource Full, Resource Changed.

Consumed Events:
- TBD

---

# Purpose

The Resource System manages all consumable, rechargeable, and spendable gameplay resources.

Resources represent anything that can be gained, consumed, regenerated, transferred, depleted, or restored during gameplay.

The system should remain completely generic.

It should never assume resources are "Mana" or "Ammo."

Instead, every resource follows the same underlying rules while exposing unique behavior through configuration.

---

# Design Philosophy

Resources are gameplay constraints.

They create meaningful decision making.

Resources should encourage players to think strategically rather than simply limiting ability usage.

Every resource should exist because it creates interesting gameplay.

---

# Core Principles

## Generic

Resources are defined by data.

The engine should not distinguish between:

- Current Health
- Mana
- Energy
- Rage
- Heat
- Ammo
- Arrows
- Charges
- Souls
- Combo Points
- Companion Bond
- Durability

They are all Resources.

---

## Extensible

Adding a new resource should require no engine modifications.

New resources should be created through configuration.

---

## Shared Framework

Every resource supports the same operations.

- Add
- Remove
- Consume
- Regenerate
- Clamp
- Transfer
- Modify Maximum
- Modify Regeneration
- Enable
- Disable

---

# Resource Properties

Every resource defines:

Name

Current Value

Maximum Value

Minimum Value

Regeneration Rate

Regeneration Delay

Maximum Overflow

Recharge Behavior

Tags

Visibility

Persistence

---

# Regeneration Models

Different resources regenerate differently.

Examples:

Continuous

Burst Recharge

Charge Based

Kill Based

Pickup Based

Time Based

Companion Based

Manual

No Regeneration

---

# Resource Costs

Abilities may consume:

Single Resource

Multiple Resources

Scaled Resources

Percentage Resources

Conditional Resources

Examples:

Fireball

Mana

25

---

Bow Shot

Arrow

1

---

Power Slam

Rage

40

---

Teleport

Mana 50

Soul 1

---

# Resource Modifiers

Equipment

Abilities

Relics

Companions

Status Effects

World Conditions

Difficulty

may all modify resource behavior.

Examples:

+20 Maximum Mana

Ammo regenerates over time

Abilities cost 50% less Energy

Fire abilities refund Mana

Companion restores Energy

---

# Events

Resources publish events.

Examples:

Resource Depleted

Resource Restored

Resource Overflow

Resource Full

Resource Changed

Other systems react through subscriptions.

---

# Multiplayer

Resources must synchronize correctly.

Server Authority

Prediction

Replication

Rollback support

Deterministic updates

---

# AI

AI should understand resources through metadata.

Examples:

Resource Scarcity

Resource Priority

Expected Regeneration

Ability Affordability

Conservation Behavior

---

# Success Criteria

The Resource System succeeds when:

- New resource types require no engine changes.
- Every ability can consume any resource.
- Equipment can modify resources consistently.
- AI understands resources without custom code.
- Designers create new resources entirely through data.