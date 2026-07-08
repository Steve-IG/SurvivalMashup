# Ability System

**Status:** Living Specification  
**Version:** 1.0

---

## System Ownership

This system owns:
- Ability definitions, activation models, costs, cooldowns, targeting configuration, and ability loadouts.

This system does NOT own:
- Gameplay Effect execution, Resource values, Attribute math, Damage resolution, or actor identity.

Primary Responsibilities:
- Provide the reusable action framework for combat, movement, utility, companion, enemy, and interaction abilities.

Primary Data:
- Ability definitions, tags, categories, activation rules, costs, cooldowns, effects, unlocks, and evolution data.

Primary Runtime Objects:
- Ability instances, equipped loadouts, cooldown state, and activation state.

Published Events:
- Ability Granted, Ability Revoked, Ability Activated, Ability Activation Failed (with reason), Ability Cooldown Started, Ability Cooldown Ended.

Consumed Events:
- None.

---

# Milestone 0 Implementation (approved decisions)

Abilities are **orchestration**: Gameplay Effects answer *what happens*; abilities answer *when and why*. No gameplay mutation is implemented inside the Ability System — effects execute through the Gameplay Effect Runner, and costs mutate through the Resource System.

Abilities are **deterministic recipes**: for identical inputs (owner state, target, definition), an activation attempt produces identical outcomes — the same validation result, the same costs, the same effect sequence in the same order. No randomness, wall-clock time, or presentation state participates in activation; time enters only as injected tick deltas.

Ability Definitions contain **configuration, not gameplay behavior**: gates, costs, a cooldown, a target mode, and references to the Gameplay Effects that own every mutation. Engine code interprets that configuration; authoring a new ability is authoring data.

The implemented Milestone 0 subset (`ToyChest.Systems.Abilities`):

- **Definition/instance split:** `AbilityDefinition` (immutable asset: category, tags, target mode, tag gates, costs, cooldown, effect sequence) and `AbilityInstance` (runtime cooldown state), per Engine Principle 14.
- **AbilitySet capability:** composed onto every Gameplay Object by the factory; abilities declared on the object definition are granted at composition, and runtime grant/revoke supports progression and equipment.
- **Deterministic activation pipeline**, first failing check reported: granted → target validity → owner tag gates (required, then blocking; hierarchical match) → cooldown → costs. `CanActivate` runs the same validation without committing anything, for UI and AI.
- **Costs:** generic resource id + amount pairs, all-or-nothing; validation never throws for a missing resource — an actor lacking the resource simply cannot afford the ability.
- **Cooldowns:** fixed seconds, owned by the Ability System, advanced through the object's `Tick` with injected time. Charge-based, recharge, conditional, and shared-group cooldowns are future extensions.
- **Targeting contract:** the ability declares its target mode (`Self` or `Provided`); the activator (input, AI, interaction) selects and supplies the concrete target. Spatial modes (ground, area, projectile) arrive with the world systems able to answer them.
- **AbilityActivated** is published when the activation is committed (validation passed, costs paid, cooldown started), before effect dispatch, so the event trace reads causally: activation → effect consequences.
- **AbilityCategory value type:** the organizational category is exposed as a lightweight string-backed `AbilityCategory` struct with ordinal equality (`AbilityCategory.None` for uncategorized abilities). Data-driven — a new category is an authored string, not code. Deliberately not an enum (categories are content) and not a Gameplay Tag (categories are organizational and never queried by gameplay logic). Category remains purely organizational and never affects behavior.

## Future activation extension points

The Milestone 0 activation model is Instant: validation and commit happen in one deterministic step. The following extension points are reserved; each extends the pipeline **between validation and commit** without changing the definition/instance split, the targeting contract, or the effect dispatch contract:

- **Cast Time** — a validated activation enters a casting window before committing; commit happens when the window completes.
- **Channeling** — the commit sustains over time, executing effect sequences while the channel holds.
- **Interruptibility** — data-driven rules for what cancels a casting or channeling window (damage, movement, tags), publishing an interruption fact.
- **Multi-stage activation** — activations that progress through authored stages (charge → aim → release), each stage with its own gates and effects.

Evolution trees, unlock requirements, loadouts, AI usage metadata, and the remaining activation models are future work and remain specified below.

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

World Reaction Effects

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

World Reaction Interactions

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

Modify World Properties

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

# Persistence Boundary

Per Engine Principle 25:

- **Authoritative:** which abilities are granted from a persistent source, and each granted ability's remaining cooldown.
- **Derived:** `IsOnCooldown` and every validation result (affordability, tag gates, targeting).
- **Serialized:** per persistently granted ability — definition id plus remaining cooldown. Grants that originate from the object definition, equipment, or progression are reconstructed by those sources rather than saved here.
- **Reconstructed:** abilities are re-granted (through the definition, equipment, or progression), then each cooldown is restored with the event-quiet `AbilitySet.RestoreCooldown`. Reconstruction publishes no ability facts.

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