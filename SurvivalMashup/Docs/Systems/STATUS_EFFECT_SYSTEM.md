# Status Effect System

**Architecture:** ToyChest v1.0
**Status:** Living Specification
**Owner:** Gameplay Systems

---

## System Ownership

This system owns:
- Active conditions, durations, stacks, status lifecycle, periodic ticks, and status-driven modifiers.

This system does NOT own:
- Gameplay Effect definitions, Attribute storage, Resource storage, World Reaction rules, or presentation.

Primary Responsibilities:
- Apply and manage temporary or persistent gameplay conditions through data.

Primary Data:
- Status Effect definitions, durations, stacking rules, tags, modifiers, periodic effects, and presentation references.

Primary Runtime Objects:
- Status Effect Components and active status instances.

Published Events:
- Status Applied, Status Removed, Status Expired, Status Refreshed, Stack Increased, Periodic Tick.

Consumed Events:
- TBD

---

# Purpose

The Status Effect System manages temporary and persistent gameplay conditions that modify actors over time.

Status Effects represent ongoing gameplay state rather than one-time events.

Examples include:

- Burning
- Frozen
- Poisoned
- Shocked
- Bleeding
- Shielded
- Inspired
- Invisible
- Corrupted

The system should be generic, data-driven, and reusable across all gameplay objects.

---

# Design Philosophy

Status Effects do not contain gameplay logic.

Instead, they describe:

- Tags
- Attribute Modifiers
- Resource Modifiers
- Gameplay Effects
- World Properties
- Duration
- Stacking Rules
- Visual Presentation

The engine executes these definitions.

Status Effects are data.

The Status Effect System provides execution.

---

# Core Principles

## Universal

Any Gameplay Object may receive Status Effects.

Examples:

- Player
- Companion
- Enemy
- NPC
- Boss
- Harvestable Tree
- Resource Node
- Environmental Hazard

---

## Data Driven

New Status Effects should be authored through configuration rather than code.

The engine should never require a custom "Burning.cs" or "Poison.cs" implementation.

---

## Composable

Complex effects emerge from combining:

- Gameplay Effects
- Attribute Modifiers
- Resource Modifiers
- Tags
- World Properties

---

# Lifecycle

A Status Effect follows this lifecycle:

Applied

↓

Activated

↓

Periodic Updates (optional)

↓

Refreshed / Stacked (optional)

↓

Expired

↓

Removed

Each stage may trigger Gameplay Effects or Events.

---

# Components of a Status Effect

Each Status Effect may define:

- Display Name
- Description
- Icon
- Duration
- Refresh Rules
- Stacking Rules
- Gameplay Tags
- Attribute Modifiers
- Resource Modifiers
- Periodic Gameplay Effects
- World Properties
- Visual Effects
- Audio Effects
- Gameplay Events

---

# Duration Types

- Instant
- Timed
- Infinite
- Conditional
- Permanent (until explicitly removed)

---

# Stacking Rules

Supported models include:

- Refresh Duration
- Increase Magnitude
- Independent Instances
- Replace Existing
- Ignore Duplicate
- Custom (through configuration)

---

# Periodic Effects

Status Effects may execute Gameplay Effects at intervals.

Examples:

Burning

Every 1 second:

- Deal Fire Damage
- Apply Heat

Poison

Every 0.5 seconds:

- Deal Poison Damage

Regeneration

Every second:

- Restore Health

No custom code should be required for these behaviors.

---

# Attribute Modifiers

Status Effects may temporarily modify attributes.

Examples:

Burning

-20 Fire Resistance

Frozen

-50% Move Speed

Inspired

+15% Attack Speed

Shielded

+25 Armor

---

# Resource Modifiers

Status Effects may directly modify resources.

Examples:

Mana Drain

-5 Mana per second

Energy Regeneration

+10 Energy per second

Health Degeneration

-2 Health per second

---

# Gameplay Tags

Status Effects add and remove Tags automatically.

Examples:

Burning

Adds:

- Burning
- Fire

Frozen

Adds:

- Frozen
- Ice

These Tags drive interactions throughout the engine.

---

# World Reaction Integration

Status Effects interact with the World Reaction System.

Examples:

Burning

Applies:

Heat

Frozen

Applies:

Cold

Wet

Applies:

Water

The World Reaction System determines emergent interactions.

---

# Gameplay Effects

Status Effects execute Gameplay Effects rather than implementing gameplay directly.

Examples:

Burning

↓

Deal Fire Damage

↓

Spawn Fire VFX

↓

Play Burn Audio

↓

Publish Burn Tick

---

# Events

Status Effects publish events.

Examples:

Status Applied

Status Removed

Status Expired

Status Refreshed

Stack Increased

Periodic Tick

Other systems subscribe to these events.

---

# Multiplayer

Status Effects support:

- Server Authority
- Replication
- Prediction
- Rollback
- Deterministic Timing

---

# AI

AI evaluates Status Effects through metadata.

Examples:

Threat

Crowd Control Value

Damage Potential

Healing Potential

Duration

Stack Count

Immunity

AI should reason generically rather than recognizing individual status names.

---

# Future Expansion

Examples of future Status Effects:

- Radiation
- Fear
- Silence
- Charm
- Gravity Shift
- Time Slow
- Berserk
- Camouflage

All should be implementable without engine changes.

---

# Success Criteria

The Status Effect System succeeds when:

- New Status Effects are created entirely through data.
- Designers rarely require programming support.
- Gameplay remains deterministic.
- Multiplayer behaves consistently.
- AI understands Status Effects generically.
- Status Effects compose naturally with Gameplay Effects, Attributes, Resources, and World Reactions.

---

# Implementation Notes

- Status Effect definitions should be immutable `ScriptableObject` assets.
- Runtime instances should track duration, stacks, and source.
- Avoid embedding gameplay logic directly in status implementations.
- Execute behavior through Gameplay Effects and Modifiers.
- Use Gameplay Tags to enable interactions between Status Effects and other systems.