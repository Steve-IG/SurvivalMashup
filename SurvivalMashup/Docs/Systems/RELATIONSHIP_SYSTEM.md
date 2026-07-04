# Relationship System

**Architecture:** ToyChest v1.0
**Status:** Living Specification
**Owner:** Gameplay Systems

---

## System Ownership

This system owns:
- Relationship types, relationship state, ownership attribution, source attribution, team relationships, faction relationships, and relationship queries.

This system does NOT own:
- Gameplay decisions, Damage math, AI behavior trees, Save format, or UI presentation.

Primary Responsibilities:
- Provide a shared model for ownership, attribution, allegiance, and interaction eligibility.

Primary Data:
- Relationship definitions, relationship type metadata, stable identifiers, and persistence flags.

Primary Runtime Objects:
- Relationship state on Gameplay Objects and transient relationship context.

Published Events:
- TBD

Consumed Events:
- TBD

---

# Purpose

The Relationship System defines how Gameplay Objects relate to one another.

Rather than relying on direct object references or specialized gameplay code, relationships provide a consistent framework for ownership, attribution, allegiance, and interaction.

Many gameplay systems depend on understanding **who caused an action**, **who owns an object**, or **how two objects should interact**.

The Relationship System provides this shared model.

---

# Design Philosophy

Relationships describe connections.

They do not implement gameplay.

Gameplay systems consume relationship information to make decisions.

Every gameplay object should expose relationship information through a common interface.

---

# Core Principles

## Universal

Any Gameplay Object may participate in relationships.

Examples include:

- Players
- Companions
- Enemies
- NPCs
- Projectiles
- Summons
- Traps
- Loot
- Harvestable Objects
- Regions

---

## Explicit

Relationships should be intentionally defined.

Avoid hidden ownership or implicit assumptions.

Gameplay systems should always be able to determine the source of an action.

---

## Data-Oriented

Relationships should be lightweight and queryable.

Avoid deeply nested ownership hierarchies.

---

# Relationship Types

## Owner

Who currently owns this object?

Examples:

Player owns Inventory

Player owns Companion

Chest owns Loot until opened

---

## Instigator

Who initiated the action?

Examples:

Player casts Fireball

↓

Fireball Instigator = Player

Player deploys Trap

↓

Trap Instigator = Player

Enemy summons Wolf

↓

Wolf Instigator = Enemy

---

## Source

What directly produced this effect?

Example:

Player

↓

Fireball Ability

↓

Projectile

↓

Explosion

↓

Burning

The Explosion's Source is the Projectile.

The Instigator remains the Player.

---

## Target

Who is currently receiving the effect?

Examples:

Enemy

Tree

Ore Node

Companion

NPC

---

## Parent / Child

Defines hierarchical gameplay ownership.

Examples:

Boss

↓

Summoned Minions

Player

↓

Pet

Projectile

↓

Explosion

---

## Team

Determines friendly and hostile interactions.

Examples:

Players

Companions

Friendly NPCs

Enemies

Neutral Wildlife

---

## Faction

Represents long-term allegiance.

Examples:

Kingdom

Bandits

Undead

Forest Spirits

Merchants

Relationships may change through gameplay.

---

## Region

Associates objects with a world region.

Useful for:

- Streaming
- Persistence
- Save/Load
- Population Management
- AI

---

# Relationship Queries

Gameplay systems should answer questions such as:

Who owns this object?

Who caused this damage?

Who should receive XP?

Who should receive loot?

Are these actors allies?

Can these actors damage each other?

Which region spawned this object?

Who summoned this companion?

---

# Gameplay Examples

## Fireball

Player

↓

Ability

↓

Projectile

↓

Enemy

Instigator:

Player

Source:

Projectile

Target:

Enemy

Damage credit belongs to the Player.

---

## Harvesting

Player

↓

Harvest Ability

↓

Tree

↓

Wood

Wood ownership transfers to the Player.

Experience is awarded to the Player.

Adventure progress is credited to the Player.

---

## Companion

Player

↓

Wolf Companion

↓

Enemy

Companion deals damage.

Instigator remains Companion.

Owner remains Player.

Player receives experience and adventure credit.

---

# Friendly Fire

Relationship rules determine:

Can damage occur?

Should healing apply?

Should buffs apply?

Should AI assist?

Avoid hardcoded Player vs Enemy logic.

---

# Multiplayer

Relationships must support:

Server authority

Replication

Prediction

Persistent ownership

Reconnect scenarios

---

# AI

AI reasons about relationships rather than object types.

Examples:

Ally

Enemy

Neutral

Owned

Summoned

Leader

Follower

This enables generalized decision making.

---

# Save System

Persistent relationships should survive saving.

Examples:

Companion ownership

Faction reputation

Region ownership

Adventure associations

Temporary combat relationships should not persist.

---

# Future Expansion

Relationships may later support:

Guilds

Player housing

Vehicles

Towns

Kingdom control

Construction ownership

Economy systems

No architectural changes should be required.

---

# Success Criteria

The Relationship System succeeds when:

- Gameplay attribution is always clear.
- Systems share a common ownership model.
- AI reasons about affiliation generically.
- Multiplayer attribution remains deterministic.
- New gameplay features reuse existing relationships.

---

# Implementation Notes

- Relationships should be represented through stable identifiers rather than fragile object references where practical.
- Distinguish between persistent relationships (Owner, Faction) and transient relationships (Target, Source).
- Systems should query relationships through common interfaces.
- Keep relationship evaluation lightweight, as it will occur frequently.