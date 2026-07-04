# Ability System

**Status:** Living Specification  
**Version:** 1.0

---

# Purpose

The Ability System is the universal framework that defines actions performed by actors within the game.

Abilities are not limited to combat.

Every meaningful action an actor performs should be represented as an ability whenever practical.

The objective is to create a highly modular, data-driven system that can support player abilities, companion behaviors, enemy attacks, traversal mechanics, interaction systems, and future gameplay without requiring architectural changes.

---

# Design Philosophy

Abilities define what an actor can do.

Actors define who performs those abilities.

Abilities should never be hardcoded to a specific actor type.

The same Fireball ability should be usable by:

- Players
- Companions
- Enemies
- Bosses
- NPCs

Differences should emerge through configuration rather than duplicated implementations.

---

# Core Principles

## Everything Is An Ability

Examples include:

Combat

- Fireball
- Shield Slam
- Meteor

Movement

- Dash
- Double Jump
- Grapple
- Glide

Utility

- Harvest
- Treasure Detection
- Auto Pickup
- Portable Crafting

Companion

- Return To Hub
- Store Inventory
- Scout Area

Interaction

- Open Portal
- Activate Shrine
- Revive Ally

Future systems should extend this framework rather than introducing parallel ability implementations.

---

## Data Driven

Abilities should be created primarily through data.

Engine code defines behavior.

Content defines configuration.

Adding a new ability should rarely require writing gameplay code.

---

## Composition Over Inheritance

Abilities should be assembled from reusable components.

Examples include:

Activation

+

Cost

+

Cooldown

+

Targeting

+

Effects

+

Animation

+

Audio

+

Visual Effects

+

Simulation Effects

Rather than:

FireballAbility.cs

IceballAbility.cs

LightningBallAbility.cs

---

# Ability Structure

Every ability contains:

Identity

Description

Tags

Category

Activation Model

Resource Cost

Cooldown

Effects

Animation

Audio

Visual Effects

Simulation Interactions

Evolution Tree

AI Usage Rules

Unlock Requirements

---

# Ability Categories

Combat

Movement

Utility

Companion

Ultimate

Interaction

Future categories should be added without architectural changes.

---

# Activation Models

Abilities define how they are activated.

Examples include:

Instant

Charged

Channeled

Projectile

Beam

Ground Target

Area Around Self

Dash

Leap

Summon

Toggle

Passive

Triggered

The activation model should be independent of the ability's gameplay effect.

---

# Resource System

Abilities may consume one or more resources.

The Ability System should not distinguish between Mana, Ammo, Energy, Rage, Charges, or future resource types.

All costs should use the same generic interface.

Examples:

Mana

Energy

Ammo

Arrows

Heat

Charges

Companion Bond

Future resources

Adding a new resource should not require changing the Ability System.

---

# Cooldowns

Every active ability may define its own cooldown behavior.

Cooldowns may include:

Fixed

Charge Based

Recharge Over Time

Conditional

Shared Cooldown Groups

Cooldown behavior should be configurable rather than hardcoded.

---

# Effects

Abilities may contain multiple effects.

Examples include:

Damage

Healing

Movement

Spawn Actor

Apply Status

Remove Status

Grant Buff

Create Hazard

Modify Simulation

Generate Loot

Play Cinematic

Reveal Area

No assumptions should be made about effect order.

Effects should be modular.

---

# Targeting

Abilities may target:

Self

Enemy

Ally

Ground

Object

Region

Direction

Area

Multiple Targets

Targeting should be independent from effects.

---

# Tags

Abilities should expose descriptive tags.

Examples:

Fire

Ice

Lightning

Nature

Projectile

Melee

Movement

Healing

Support

Summon

Explosion

Harvest

Traversal

Companion

Boss

Tags drive interactions rather than inheritance.

---

# Evolution

Every ability owns its own evolution tree.

Players improve individual abilities independently.

Evolution should primarily unlock new mechanics rather than increase numerical values.

Examples:

Fireball

↓

Explosion Radius

↓

Burning Ground

↓

Chain Explosion

↓

Lightning Conversion

↓

Meteor Impact

Different players should evolve identical abilities differently.

---

# AI Compatibility

The system should support AI-controlled actors.

AI should evaluate abilities using metadata such as:

Preferred Range

Priority

Target Type

Threat Value

Cooldown

Resource Cost

Situation Tags

AI behavior should not require ability-specific code.

---

# Multiplayer

The Ability System must support multiplayer from the beginning.

Authority

Prediction

Replication

Synchronization

Interruptions

Cooldown synchronization

Resource synchronization

should all be considered during implementation.

---

# Success Criteria

The Ability System succeeds when:

- New abilities rarely require engine changes.
- New resource types require no architectural changes.
- Players regularly experiment with new abilities.
- Enemy abilities reuse the same framework.
- Companion abilities reuse the same framework.
- Designers can create most new abilities through data.
- AI can understand abilities through metadata.