# AI System

**Architecture:** ToyChest v1.0
**Status:** Living Specification
**Owner:** Core Gameplay

---

# Purpose

The AI System enables Gameplay Objects to perceive the world, evaluate goals, and select abilities or interactions to achieve those goals.

AI does not execute gameplay directly.

Instead, AI chooses from the same Abilities and Interactions available within the ToyChest Architecture.

This ensures architectural consistency between players, companions, enemies, and NPCs.

---

# Design Philosophy

AI follows the same gameplay rules as players.

AI makes decisions.

Gameplay Systems execute those decisions.

The AI System should produce intelligent behavior through reusable decision-making rather than specialized scripts.

---

# Core Goals

- One AI architecture for every actor.
- Reuse existing gameplay systems.
- Support emergent gameplay.
- Enable cooperative behaviors.
- Scale from simple wildlife to complex bosses.

---

# Architecture

Gameplay Object

↓

Perception

↓

World Knowledge

↓

Goal Evaluation

↓

Decision

↓

Ability or Interaction Selection

↓

Ability System / Interaction System

↓

Gameplay Events

---

# AI Actors

The same AI framework supports:

Enemy

Boss

Companion

NPC

Merchant

Wildlife

Future gameplay actors

Behavior differences emerge from goals, priorities, and available abilities.

---

# Perception

AI gathers information about the world.

Examples:

Nearby Gameplay Objects

Gameplay Tags

Relationships

Distance

Visibility

Noise (future)

Health

Status Effects

Environmental hazards

Objectives

Perception should remain generic and extensible.

---

# World Knowledge

Perceived information is stored as temporary world knowledge.

Examples:

Known enemies

Known allies

Dangerous locations

Harvestable resources

Objectives

Interactive objects

AI reasons using this knowledge rather than querying the world continuously.

---

# Goals

Goals represent desired outcomes.

Examples:

Attack hostile target

Protect ally

Follow player

Harvest resource

Trade

Patrol

Investigate

Escape danger

Revive teammate

Capture objective

Goals are data-driven.

---

# Decision Making

AI evaluates:

Goal priority

Current resources

Cooldowns

Distance

Threat

Opportunity

Environment

Gameplay Tags

Relationships

The highest-value valid decision is selected.

---

# Ability Selection

AI never performs gameplay directly.

Instead, it selects:

Abilities

Interactions

Movement

Target

The Ability and Interaction Systems perform execution.

---

# Movement

Movement decisions include:

Navigate

Chase

Retreat

Flank

Circle

Maintain distance

Seek cover (future)

Traversal abilities integrate naturally.

---

# Teamwork

AI should cooperate.

Examples:

Focus targets

Protect allies

Heal teammates

Spread elemental effects

Create combinations

Cooperative behavior should emerge from shared goals.

---

# Companion AI

Companions prioritize:

Protect player

Assist combat

Avoid hazards

Use abilities intelligently

Interact with world

Companions remain autonomous.

Players influence behavior through build choices rather than direct commands.

---

# Boss AI

Bosses extend the same architecture.

Additional concepts may include:

Phases

Objective changes

Arena interactions

Summons

Environmental hazards

Bosses should not require a separate AI system.

---

# World Awareness

AI understands world properties.

Examples:

Fire spreads

Water conducts electricity

Frozen surfaces reduce movement

Explosive objects

Harvestable resources

AI should react using the World Reaction System.

---

# Multiplayer

AI supports:

Server authority

Replication

Prediction where appropriate

Deterministic decisions

Consistent world knowledge

---

# Future Expansion

Examples:

Learning behaviors

Faction diplomacy

Civilian schedules

Companion personalities

Dynamic ecosystems

Procedural behaviors

Seasonal behaviors

No architectural redesign should be required.

---

# Uses ToyChest Systems

Gameplay Object

Ability System

Interaction System

Gameplay Tags

Relationship System

World Reaction System

Gameplay Effects

Attributes

Resources

Gameplay Events

Navigation

---

# Success Criteria

The AI System succeeds when:

- Every actor uses the same AI architecture.
- AI selects abilities rather than executing gameplay directly.
- New actor types require minimal engine code.
- Cooperative and emergent behaviors arise naturally.
- AI reacts intelligently to both combat and the environment.

---

# Implementation Notes

- Separate perception, decision-making, and execution.
- Represent goals and priorities as data where possible.
- Query Gameplay Tags and capabilities instead of concrete object types.
- Reuse the Ability and Interaction Systems for all gameplay execution.
- Keep AI modular so new behaviors can be composed rather than scripted.