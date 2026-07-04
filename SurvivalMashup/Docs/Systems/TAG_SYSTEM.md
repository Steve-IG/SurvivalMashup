# Tag System

**Architecture:** ToyChest v1.0
**Status:** Living Specification
**Owner:** Gameplay Systems

---

# Purpose

The Tag System provides a universal vocabulary for describing gameplay objects, gameplay state, gameplay capabilities, and gameplay relationships.

Tags allow systems to communicate without knowing about specific gameplay objects.

Instead of asking:

"Is this a Fire Enemy?"

systems ask:

"Does this object have the Fire tag?"

This makes gameplay highly composable and dramatically reduces coupling.

---

# Design Philosophy

Tags describe gameplay.

They never implement gameplay.

Tags answer questions.

Systems determine behavior.

The Tag System is intentionally passive.

---

# Core Principles

## Universal

Every Gameplay Object may have tags.

Examples:

Player

Enemy

Boss

Weapon

Projectile

Companion

Item

Tree

Ore

Shrine

Portal

Quest

Ability

Status Effect

Region

---

## Data Driven

Tags should be authored through data.

New gameplay should rarely require adding new engine logic.

Instead:

Create new tags.

Configure systems to react.

---

## Composable

Gameplay complexity emerges by combining tags.

Example:

Mechanical

+

Wet

+

Burning

↓

Lightning Damage

↓

Steam

↓

Explosion

↓

Stunned

No custom gameplay code required.

---

# Tag Categories

## Identity Tags

Identity Tags describe what something fundamentally is.

Examples:

Player

Enemy

Boss

Companion

NPC

Tree

Plant

Ore

Sword

Bow

Staff

Fire

Ice

Mechanical

Undead

Animal

Building

Portal

Merchant

Identity Tags are typically permanent.

---

## State Tags

State Tags describe temporary gameplay conditions.

Examples:

Burning

Frozen

Wet

Poisoned

Electrified

Invisible

Flying

Swimming

Harvesting

Dead

Shielded

Channeling

Rooted

Moving

Most State Tags change continuously during gameplay.

---

## Capability Tags

Capability Tags describe what an object can do or how it may be interacted with.

Examples:

Harvestable

Interactable

Craftable

Upgradeable

Breakable

Flammable

Conductive

Freezable

Rideable

Climbable

Talkable

Tradable

Capability Tags rarely change.

---

# Tag Ownership

Tags may originate from many systems.

Examples:

Gameplay Object

Equipment

Status Effect

Ability

Region

Quest

Simulation

Difficulty

Companion

World Event

The active tag set is the union of all contributing sources.

---

# Queries

Systems should query tags rather than gameplay classes.

Examples:

HasTag()

HasAllTags()

HasAnyTag()

HasNone()

TagCount()

Querying tags should be inexpensive.

---

# Gameplay Usage

Tags may influence:

Ability Activation

Gameplay Effects

Damage

AI

Loot

Dialogue

Quest Progress

Crafting

Simulation

Movement

Animation

Audio

UI

Saving

Networking

The Tag System should be usable everywhere.

---

# Ability Examples

Fireball

Requires:

Magic

Projectile

Deals Bonus Damage To:

Plant

Frozen

Cannot Target:

Friendly

Invisible

---

Harvest

Requires:

Harvestable

Produces Bonus Loot From:

Rare

Magical

---

# Damage Examples

Fire Damage

Bonus vs:

Plant

Weak vs:

Fire Resistant

Ignored by:

Fire Immune

---

Lightning Damage

Bonus vs:

Wet

Mechanical

---

# AI Examples

Enemy evaluates:

Player

Visible

Burning

LowHealth

Flying

Boss

Rather than specific gameplay classes.

---

# Status Effect Examples

Burning adds:

Burning

Fire

Hot

Frozen adds:

Frozen

Cold

Wet removes:

Burning

Applies:

Wet

Cold

The Tag System enables interactions without hardcoded knowledge.

---

# Equipment Examples

Sword grants:

Melee

Steel

Weapon

Legendary Sword grants:

Legendary

Holy

Sword

Weapon

---

# Region Examples

Volcano

Adds:

Hot

Fire

Lava

Mountain

Frozen Tundra

Adds:

Snow

Cold

Ice

These tags influence gameplay systems naturally.

---

# Multiplayer

Tags must support:

Replication

Prediction

Rollback

Deterministic evaluation

Tags should always remain synchronized.

---

# AI

Tags provide semantic understanding.

AI reasons about tags rather than object types.

This enables generic decision making.

---

# Future Expansion

Future systems should prefer adding tags over creating custom logic.

The Tag vocabulary should expand as content expands.

The engine should remain stable.

---

# Success Criteria

The Tag System succeeds when:

- Systems communicate using tags.
- New gameplay requires mostly new tags and data.
- AI understands gameplay semantically.
- Multiplayer remains deterministic.
- Designers create complex interactions without programming.

---

# Implementation Notes

- Tags should be immutable data assets or generated constants with stable IDs.
- Runtime objects should expose efficient tag query APIs.
- Tag lookups must be optimized because they occur frequently.
- Avoid string comparisons at runtime; use IDs or hashed values internally.
- Tags should remain lightweight and never contain gameplay logic.