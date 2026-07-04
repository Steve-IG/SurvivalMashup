# World Reaction System

**Architecture:** ToyChest v1.0
**Status:** Living Specification
**Owner:** Gameplay Systems

---

# Purpose

The World Reaction System governs how the game world responds to gameplay.

Rather than implementing isolated interactions, the system models a consistent set of world properties and reaction rules.

Gameplay changes the world.

The world reacts according to its current state.

This enables emergent gameplay while minimizing hardcoded interactions.

---

# Design Philosophy

The world is an active participant in gameplay.

Objects do not contain special-case interaction logic.

Instead:

Gameplay modifies world properties.

World properties drive reactions.

Every reaction should follow universal rules.

---

# Core Principles

## Universal

Every Gameplay Object may participate.

Examples:

Player

Enemy

Companion

Tree

Grass

Ore

River

Torch

Crate

Bridge

Projectile

Building

---

## Property Driven

Objects expose world properties.

Examples:

Flammable

Wet

Frozen

Conductive

Explosive

Heavy

Fragile

Organic

Metal

Wood

Stone

Living

The World Reaction System evaluates properties.

---

## Consistent

Fire behaves like fire.

Water behaves like water.

Ice behaves like ice.

Rules remain consistent throughout the game.

---

## Emergent

Complex gameplay should emerge naturally from combining simple reactions.

Avoid one-off scripted interactions whenever possible.

---

# World Properties

Gameplay Objects may expose properties such as:

Heat

Cold

Moisture

Electric Charge

Corruption

Pressure

Structural Integrity

Light Level

Visibility

Wind Influence

These properties may change continuously.

---

# Reaction Pipeline

Gameplay Effect

↓

World Property Changes

↓

Reaction Evaluation

↓

Additional Gameplay Effects

↓

World Reaction Updates

↓

Gameplay Events

↓

Presentation

---

# Examples

## Fire

Fire increases Heat.

When Heat exceeds ignition thresholds:

Objects may ignite.

Nearby flammable objects gain Heat.

Smoke may appear.

Burning Status Effects may be applied.

---

## Water

Water increases Moisture.

Moisture reduces Heat.

Water conducts Electricity.

Water extinguishes Burning.

---

## Ice

Ice reduces Heat.

Frozen objects become brittle.

Cold slows Heat accumulation.

Ice melts when Heat rises.

---

## Electricity

Electricity propagates through conductive materials.

Examples:

Water

Metal

Certain enemies

Electrical reactions should be property driven.

---

## Wind

Wind influences:

Projectiles

Fire spread

Gliding

Particles

Weather

Future systems may extend wind interactions.

---

## Corruption

Corruption transforms affected objects over time.

Examples:

Plants mutate.

Creatures become hostile.

Resources change type.

Regions evolve.

Corruption should remain generic.

---

# Environmental Objects

Objects respond through properties.

Examples:

Tree

Flammable

Organic

Wood

Grass

Flammable

Lightweight

River

Wet

Conductive

Rock

Heavy

Stone

Torch

Fire Source

These definitions determine reactions.

---

# Status Effect Integration

Status Effects modify world properties.

Examples:

Burning

↓

Heat

Wet

↓

Moisture

Frozen

↓

Cold

World reactions occur automatically.

---

# Damage Integration

Damage may alter world properties.

Examples:

Fire Damage

↓

Heat

Lightning

↓

Charge

Harvest Damage

↓

Structural Integrity

Damage and world reactions remain separate systems.

---

# AI Integration

AI evaluates world state.

Examples:

Avoid Fire

Seek Water

Exploit Conductive Surfaces

Avoid Poison Cloud

Use High Ground

No handcrafted AI behavior should be required for individual reactions.

---

# Weather

Weather modifies world properties globally.

Examples:

Rain

↓

Increase Moisture

↓

Reduce Fire Spread

Snow

↓

Increase Cold

↓

Freeze Water

Wind

↓

Influence Fire

↓

Influence Projectiles

---

# Regions

Regions define baseline world conditions.

Examples:

Volcano

High Heat

Frozen Peaks

Extreme Cold

Swamp

High Moisture

Poison

Desert

Dry

Hot

Regions influence reactions without overriding universal rules.

---

# Events

World reactions publish gameplay events.

Examples:

Object Ignited

Ice Melted

Tree Fell

Bridge Collapsed

Water Frozen

Explosion Triggered

Other systems subscribe.

---

# Multiplayer

World reactions must remain:

Deterministic

Replicated

Predictable

Authoritative

Persistent where appropriate.

---

# Future Expansion

Examples:

Acid

Radiation

Gravity

Time Distortion

Darkness

Sound Propagation

Terraforming

Seasonal Effects

No architectural redesign should be required.

---

# Success Criteria

The World Reaction System succeeds when:

- Gameplay naturally creates unexpected situations.
- World rules remain consistent.
- New interactions are created by combining properties rather than writing custom code.
- Designers expand gameplay primarily through data.
- AI understands world state generically.
- Multiplayer remains deterministic.

---

# Implementation Notes

- Store world properties as composable runtime state on Gameplay Objects.
- Drive reactions through configurable rules rather than object-specific scripts.
- Keep reaction evaluation deterministic and independent of presentation.
- Favor broad reusable properties over narrowly defined interaction flags.