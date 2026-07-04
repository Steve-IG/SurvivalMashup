# Gameplay Effect System

**Status:** Living Specification
**Version:** 1.0
**Owner:** Gameplay Architecture
**Dependencies:** Ability System, World Reaction System, Resource System, Event System

---

## System Ownership

This system owns:
- Gameplay Effect definitions, effect execution, conditions, sequencing, and deterministic outcome application.

This system does NOT own:
- Ability activation, targeting selection, Resource ownership, World Reaction rule evaluation, or UI.

Primary Responsibilities:
- Execute reusable gameplay outcomes triggered by abilities, equipment, status effects, interactions, and environment.

Primary Data:
- Gameplay Effect definitions, effect categories, conditions, target requirements, and execution rules.

Primary Runtime Objects:
- Effect execution contexts and effect result data.

Published Events:
- Damage Applied, Enemy Defeated, Item Collected, Tree Harvested, Ability Activated, Region Liberated, Adventure Completed.

Consumed Events:
- TBD

---

# Purpose

The Gameplay Effect System is responsible for executing gameplay outcomes.

Abilities, equipment, companions, enemies, adventures, interactions, consumables, and environmental systems should rarely contain gameplay logic directly.

Instead, they execute one or more Gameplay Effects.

The Gameplay Effect System provides a universal, reusable, data-driven framework for describing what happens in the game.

---

# Design Philosophy

Gameplay logic should be composed from small, reusable effects.

Instead of implementing unique code for every ability or interaction, gameplay is built by combining standardized effects.

Examples:

Fireball

↓

- Spawn Projectile
- Deal Damage
- Apply Burning
- Ignite Environment
- Spawn Explosion VFX
- Play Audio

---

Harvest Tree

↓

- Damage Object
- Destroy Object
- Spawn Resources
- Award Experience
- Trigger Adventure Progress

---

Open Treasure Chest

↓

- Play Animation
- Spawn Loot
- Award Gold
- Trigger Dialogue
- Save World State

---

The engine should not distinguish between combat, exploration, crafting, or interaction.

Everything is an Effect.

---

# Core Principles

## Composition Over Custom Code

Effects should be reusable.

Creating a new ability should primarily involve selecting and configuring existing effects.

New code should only be written when introducing entirely new gameplay behavior.

---

## Data Driven

Effects should be configurable through data.

Designers should be able to build complex gameplay sequences without writing code.

---

## Independent

Effects should not directly depend on one another.

Each effect performs a single responsibility.

Complex behavior emerges from combining many simple effects.

---

## Deterministic

Given the same inputs, effects should always produce the same gameplay outcome.

This improves debugging, networking, replay systems, and testing.

---

# Gameplay Flow

Typical execution flow:

Actor activates Ability

↓

Ability validates activation

↓

Ability consumes required resources

↓

Ability enters cooldown

↓

Ability executes Gameplay Effects

↓

Effects modify the game world

↓

Effects publish gameplay events

↓

Interested systems react

Examples:

- UI
- Audio
- Achievements
- Adventures
- Tutorials
- Analytics
- AI

---

# Effect Categories

## Damage

Examples:

- Direct Damage
- Area Damage
- Damage Over Time
- Environmental Damage
- True Damage

---

## Healing

Examples:

- Restore Health
- Regeneration
- Group Heal

---

## Resources

Examples:

- Consume Resource
- Restore Resource
- Generate Resource
- Transfer Resource

---

## Status

Examples:

- Apply Burning
- Freeze
- Poison
- Slow
- Shield
- Invulnerability
- Stun

---

## Movement

Examples:

- Dash
- Pull
- Push
- Knockback
- Teleport
- Launch
- Leap

---

## Spawning

Examples:

- Spawn Projectile
- Spawn Actor
- Spawn Companion
- Spawn Loot
- Spawn Hazard

---

## World

Examples:

- Ignite Object
- Freeze Water
- Grow Plants
- Destroy Object
- Reveal Area
- Open Portal

---

## Inventory

Examples:

- Add Item
- Remove Item
- Equip Item
- Transfer Inventory
- Auto Sort

---

## Progression

Examples:

- Award XP
- Unlock Ability
- Unlock Companion
- Unlock Region
- Grant Currency

---

## Audio / Visual

Examples:

- Play Animation
- Play Sound
- Spawn VFX
- Camera Shake
- Controller Rumble

These effects should communicate gameplay but should not contain gameplay logic themselves.

---

## Adventure

Examples:

- Advance Objective
- Complete Objective
- Trigger Dialogue
- Unlock NPC
- Start Event

---

# Effect Execution

Effects execute sequentially by default.

Certain effects may explicitly execute:

- Parallel
- Delayed
- Conditional
- Repeating

Execution order should be deterministic.

---

# Conditions

Effects may execute only if conditions are satisfied.

Examples:

- Target Burning
- Target Frozen
- Critical Hit
- Current Health Below 30%
- Companion Nearby
- Region Cleared
- Night Time

Conditions should be reusable modules.

---

# Targeting

Effects operate on targets supplied by the Ability System.

Possible targets include:

- Self
- Enemy
- Ally
- Companion
- Object
- Environment
- Region
- Multiple Targets

Effects should not determine targeting themselves.

---

# Tags

Effects use gameplay tags instead of hardcoded type checks.

Examples:

Fire

Ice

Lightning

Plant

Mechanical

Boss

Harvestable

Companion

Projectile

Flying

Tags drive interactions throughout the engine.

---

# World Reaction Integration

Effects communicate with the World Reaction System through properties rather than custom logic.

Examples:

Apply Fire Property

Apply Wet Property

Apply Frozen Property

Apply Electricity Property

Apply Corruption Property

The World Reaction System determines what happens next.

Effects never simulate the world directly.

---

# Event Integration

Every significant effect may publish gameplay events.

Examples:

Damage Applied

Enemy Defeated

Item Collected

Tree Harvested

Ability Activated

Region Liberated

Adventure Completed

Other systems react through subscriptions rather than direct references.

---

# Networking

Effects should support:

- Server authority
- Client prediction
- Rollback where appropriate
- Deterministic replication
- Multiplayer synchronization

Networking concerns should remain separate from gameplay behavior whenever possible.

---

# AI Integration

AI should reason about effects using metadata.

Examples:

Estimated Damage

Threat

Healing Value

Mobility Value

Crowd Control

World Reaction Value

Preferred Range

Target Priority

AI should not require bespoke implementations for individual effects.

---

# Future Extensibility

Adding new gameplay should rarely require modifying existing effects.

Instead:

- Create a new effect type.
- Configure it through data.
- Compose it with existing effects.

The system should scale to thousands of abilities over the lifetime of the project.

---

# Success Criteria

The Gameplay Effect System succeeds when:

- Most gameplay features are created through composition.
- Designers can build new abilities without engine changes.
- Abilities, companions, enemies, items, and interactions share the same effect vocabulary.
- Systems remain loosely coupled.
- AI can understand gameplay through effect metadata.
- New gameplay content is primarily data rather than code.

---

# Related Documents

- Docs/Systems/ABILITY_SYSTEM.md
- Docs/Systems/WORLD_REACTION_SYSTEM.md
- Docs/Systems/STATUS_EFFECT_SYSTEM.md
- Docs/Systems/DAMAGE_SYSTEM.md
- Docs/Systems/RESOURCE_SYSTEM.md
- Docs/Systems/ATTRIBUTE_SYSTEM.md
- Docs/Foundations/BUILDCRAFT.md
- Docs/Systems/PLAYER.md
- Docs/Architecture/CORE_ARCHITECTURE.md