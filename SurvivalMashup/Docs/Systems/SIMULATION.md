# Simulation

**Status:** Living Specification  
**Version:** 1.0  
**Owner:** Lead Systems Designer  
**Last Updated:** June 2026

---

# Purpose

The Simulation System defines the universal rules that govern how the game world behaves.

Rather than implementing isolated gameplay mechanics, the game is built from a consistent set of interacting systems.

Every object, creature, ability, material, and environmental feature follows the same simulation rules.

The goal is to create a world that feels alive, predictable, and capable of generating emergent gameplay.

---

# Core Philosophy

> Build systems, not exceptions.

The world responds to interactions rather than actors.

Objects do not care whether an effect came from:

- the player
- a companion
- an enemy
- a trap
- the environment
- another object

They only react to the properties that affect them.

This consistency creates player intuition and encourages experimentation.

---

# Design Goals

The simulation should:

- Reward curiosity.
- Encourage experimentation.
- Produce emergent gameplay.
- Minimize special-case logic.
- Be understandable through play.
- Apply consistently across every gameplay system.

Players should gradually learn the world's rules and creatively combine them to solve problems.

---

# Universal Properties

Gameplay interactions are driven by properties rather than individual abilities.

Examples include:

## Fire

Properties:

- Heat
- Burning
- Light

Possible interactions:

- Ignite flammable objects.
- Burn vegetation.
- Melt wax.
- Cook food.
- Produce smoke.
- Light torches.
- Spread to nearby materials.

---

## Water

Properties:

- Wet
- Conductive
- Flowing

Possible interactions:

- Extinguish fire.
- Conduct electricity.
- Create mud.
- Fill containers.
- Freeze.
- Water crops.

---

## Ice

Properties:

- Cold
- Slippery
- Solid

Possible interactions:

- Freeze water.
- Slow enemies.
- Create bridges.
- Preserve food.
- Reduce fire spread.

---

## Electricity

Properties:

- Conductive
- Chain
- Stun

Possible interactions:

- Travel through water.
- Power machinery.
- Stun enemies.
- Overload ancient devices.

---

## Poison

Properties:

- Toxic
- Persistent

Possible interactions:

- Damage living creatures over time.
- Contaminate water.
- Kill plants.
- Combine with fire to create hazardous gas (example).

---

Additional properties will be added throughout development.

---

# Materials

Objects are defined by materials.

Examples:

- Wood
- Stone
- Metal
- Ice
- Water
- Glass
- Cloth
- Crystal
- Organic
- Corrupted

Materials define how objects respond to universal properties.

---

# World Interactions

The world should react consistently regardless of the interaction source.

Examples:

Fire + Wood → Burning

Fire + Grass → Wildfire

Fire + Ice → Water

Electricity + Water → Conduct

Ice + Water → Frozen Surface

Poison + Water → Contaminated Water

These interactions apply equally to players, companions, enemies, and the environment.

---

# Emergent Gameplay

Interesting gameplay should emerge naturally from combining systems.

Examples:

- Burn vegetation to expose hidden paths.
- Freeze rivers to create shortcuts.
- Electrify flooded areas.
- Use companions to trigger environmental interactions.
- Combine weather with elemental abilities.

The game should reward creative thinking without requiring scripted solutions.

---

# Combat Integration

Combat is one expression of the simulation.

Abilities primarily introduce properties into the world.

The resulting interactions are determined by simulation rules rather than ability-specific logic.

This allows the same elemental systems to affect:

- Combat
- Exploration
- Puzzles
- Crafting
- Traversal
- Environmental storytelling

---

# Companion Integration

Companions participate in the same simulation.

Their abilities introduce properties that interact with the world using identical rules.

A Fire Companion and a Fire Spell should ignite the same objects and produce the same environmental effects.

---

# Future Systems

The simulation is designed to support additional systems including:

- Weather
- Seasons
- Farming
- Building
- Traps
- Vehicles
- Fluids
- Light
- Temperature
- Corruption

New systems should extend the simulation rather than bypass it.

---

# Engineering Philosophy

The simulation should be:

- Modular.
- Data-driven.
- Extensible.
- Deterministic where practical.
- Network-friendly.
- AI-friendly.

Avoid actor-specific logic whenever possible.

Favor declarative data over procedural code.

---

# Design Principles

Every new gameplay feature should ask:

- Does it follow existing simulation rules?
- Can it be expressed through universal properties?
- Does it create new opportunities for emergent gameplay?
- Can another system reuse this behavior?

If not, reconsider the design.

---

# Related Documents

- COMBAT.md
- COMPANIONS.md
- WORLD.md
- CRAFTING.md
- BUILDING.md
- ENEMIES.md