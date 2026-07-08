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
- None directly in Milestone 0. Atomic effects mutate owning systems, whose own events (Resource Changed, Attribute Changed, tag transitions) carry the facts. Domain outcome events (Damage Applied, Enemy Defeated, Item Collected, ...) are published by their owning systems as those systems arrive (Damage System, Loot System, ...).

Consumed Events:
- None.

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

## Atomic Effects

Approved Milestone 0 decision: **Gameplay Effects are atomic.** Each effect performs exactly one deterministic gameplay action — Damage, Heal, Add Resource, Remove Resource, Add Tag, Remove Tag, Apply Modifier. Complex gameplay is produced by composing sequences of atomic effects, never by creating monolithic effects (see `Docs/Architecture/ENGINE_PRINCIPLES.md`, Principle 24, Composition Over Specialization).

Effects own no duration, no scheduling, and no targeting: the Status Effect System schedules, the Ability System targets. Effect sequences are executed through the single Gameplay Effect Runner, in list order, each effect gated by its own reusable conditions.

## Context over Ownership

Effects never receive unrestricted access to gameplay objects. The execution context carries **capability views** — the participants' resource, attribute, and tag capabilities — not live object references. An effect can mutate exactly what the invoker wired into the context, and nothing else.

This keeps effects executable against anything that exposes the right capabilities (a composed Gameplay Object, sibling capabilities wired at composition time, or bare capabilities in a test), keeps invokers in control of what an effect may touch, and prevents effects from growing hidden dependencies on object internals.

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

## Planned Extension Points

These extensions are anticipated and require no contract changes; each arrives only with a documented need:

- **Conditional effects:** richer reusable condition modules (attribute thresholds, resource percentages, world state) composed onto any effect, extending the existing per-effect condition list.
- **Scalable effects:** effect magnitudes derived from context — caster attributes, stack counts, charge time — instead of fixed authored values. Scaling inputs travel in the execution context; effects stay deterministic.
- **Probabilistic effects:** chance-gated execution (ignite chance on hit) driven by an injected deterministic random source, so outcomes remain reproducible for testing, replays, and networking.

---

# Persistence Boundary

Per Engine Principle 25:

- **Authoritative:** none. Gameplay Effects are instantaneous, stateless, deterministic operations; they hold no runtime state between executions.
- **Derived:** nothing persistent — an effect's outcome lives in the resource, attribute, or tag it mutated, owned by that system.
- **Serialized:** nothing.
- **Reconstructed:** nothing to reconstruct. Effects are pure functions of their definition and the target state at execution time. The scheduling that repeatedly runs periodic effects is authoritative to the Status Effect System (its periodic accumulator), not here.

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