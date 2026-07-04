# Engine Principles

**Status:** Living Specification  
**Version:** 1.0  
**Priority:** Highest

---

# Purpose

This document defines the architectural principles that govern every engineering decision in the project.

It exists to ensure the engine remains scalable, modular, data-driven, AI-friendly, multiplayer-ready, and maintainable over many years of development.

Whenever there is uncertainty about an implementation, these principles take precedence over convenience.

Documentation authority, canonical terminology, and AI read order are defined in `Docs/AI_AGENT_INDEX.md`.

---

# Guiding Philosophy

We are not building a collection of gameplay features.

We are building a reusable gameplay engine capable of supporting a continually expanding game.

Content should grow exponentially while engine complexity grows slowly.

Whenever possible:

**Generalize systems.**

**Specialize content.**

---

# Principle 1

## Solve Engine Problems Once

If multiple gameplay features require similar logic, build a reusable engine system instead of duplicating code.

Avoid solving the same problem twice.

---

# Principle 2

## Never Solve Content Problems With Engine Code

Engine code defines capabilities.

Content defines behavior.

When adding a new weapon, ability, companion, item, region, enemy, or adventure, prefer creating or configuring data rather than modifying engine code.

If engine code changes every time content is added, the architecture has failed.

---

# Principle 3

## Composition Over Inheritance

Prefer assembling gameplay from small reusable systems.

Avoid deep inheritance hierarchies.

Gameplay emerges from combining:

- Components
- Gameplay Effects
- Resources
- Attributes
- Tags
- Events
- World Properties

---

# Principle 4

## Data Over Code

Immutable gameplay definitions belong in data assets.

Runtime systems interpret those definitions.

The engine should know how abilities work.

The engine should not know what Fireball is.

---

# Principle 5

## Everything Is Reusable

Every new system should be evaluated by asking:

Can this solve more than one problem?

If yes, generalize it.

---

# Principle 6

## Prefer Gameplay Effects

Gameplay logic should rarely exist inside:

- Abilities
- Weapons
- Companions
- Enemies
- Items

Instead, gameplay should be composed from reusable Gameplay Effects.

---

# Principle 7

## Prefer Events Over Direct References

Systems should communicate through events whenever practical.

Examples:

Ability Activated

↓

Gameplay Effects

↓

Gameplay Events

↓

UI reacts

↓

Audio reacts

↓

Adventure System reacts

↓

Achievements react

↓

Analytics react

Adding a new system should rarely require modifying existing systems.

---

# Principle 8

## Build Generic Systems

Do not create:

Mana System

Gun System

Magic System

Sword System

Harvest System

Instead create:

Resource System

Ability System

Gameplay Effect System

World Reaction System

Attribute System

Inventory System

Equipment System

---

# Principle 9

## Everything Is Metadata

Gameplay decisions should be driven by metadata whenever possible.

Examples:

Tags

Attributes

Resources

Categories

World Properties

AI Metadata

The engine should avoid checking for specific content names.

---

# Principle 10

## AI Is A First-Class Developer

Every architecture decision should make it easier for AI to:

Understand the system.

Generate new content.

Extend existing systems.

Avoid architectural drift.

If an implementation is difficult for AI to understand, it is probably too complex.

---

# Principle 11

## Multiplayer Is Not An Afterthought

Every gameplay system should assume multiplayer support.

Authority

Prediction

Replication

Synchronization

Determinism

should influence design from the beginning.

---

# Principle 12

## World Reaction Is Universal

The world reacts consistently.

Fire behaves like fire.

Ice behaves like ice.

Electricity behaves like electricity.

Gameplay objects respond according to their properties rather than through hardcoded interactions.

---

# Principle 13

## Consistency Beats Cleverness

Players should learn universal rules.

Universal rules create emergent gameplay.

Avoid one-off exceptions.

---

# Principle 14

## Runtime State Is Separate From Definitions

Definitions are immutable.

Runtime state changes.

Never mix the two.

Examples:

Ability Definition

↓

Ability Instance

Weapon Definition

↓

Equipped Weapon

Companion Definition

↓

Companion Instance

---

# Principle 15

## Systems Own Behavior

Systems own logic.

Content owns configuration.

Gameplay objects own state.

Maintain this separation.

---

# Principle 16

## One Source Of Truth

Avoid duplicated gameplay information.

Every concept should have one authoritative owner.

Examples:

Attributes

↓

Attribute System

Resources

↓

Resource System

Cooldowns

↓

Ability System

Inventory

↓

Inventory System

Status Effects

↓

Status Effect System

---

# Principle 17

## Favor Deterministic Behavior

Given identical inputs, gameplay systems should produce identical outputs.

Determinism improves:

Networking

Testing

Replay Systems

Debugging

AI

---

# Principle 18

## Design For Expansion

Assume the game will eventually contain:

Hundreds of abilities

Thousands of items

Hundreds of companions

Thousands of enemies

Dozens of regions

Years of seasonal content

Architecture should become more valuable as content grows.

---

# Principle 19

## Minimize Special Cases

If a feature requires special-case code, first ask whether the framework should be expanded instead.

Exceptions should be rare, documented, and intentional.

---

# Principle 20

## Optimize For Maintainability

Readable systems outperform clever systems.

Future developers—including AI—should understand the architecture quickly.

The engine should become easier to extend over time, not harder.

---

# Architectural Test

Before implementing any feature, ask:

1. Can this be solved using existing systems?
2. Is this a reusable capability or a one-off feature?
3. Should this be data instead of code?
4. Can Gameplay Effects compose this behavior?
5. Does this introduce unnecessary coupling?
6. Does this work in multiplayer?
7. Will AI understand and extend this system?
8. Is there a simpler abstraction?
9. Does this align with the Gameplay Framework?
10. Will this still make sense three years from now?

If the answer to any question is "no," reconsider the design before implementation.

---

# Success Criteria

The engine succeeds when:

- Content creation accelerates over time.
- New gameplay rarely requires architectural changes.
- AI agents consistently extend systems without degrading the architecture.
- Designers spend more time creating content than requesting new engine features.
- The codebase remains understandable after years of development.