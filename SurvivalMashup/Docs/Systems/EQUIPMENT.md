# Equipment System

**Architecture:** ToyChest v1.0
**Status:** Living Specification
**Owner:** Gameplay Systems

---

# Purpose

The Equipment System manages items that are actively equipped by Gameplay Objects.

Equipment enables gameplay by activating capabilities defined on Item Instances.

The Equipment System is responsible for equipping, unequipping, validating, and activating equipment.

It is **not** responsible for implementing gameplay mechanics such as combat, abilities, attributes, or effects.

---

# Design Philosophy

Equipment activates capabilities.

Equipment does not contain gameplay logic.

When an item is equipped, its components contribute to the ToyChest Architecture:

- Attributes
- Resources
- Gameplay Tags
- Abilities
- Gameplay Effects
- World Properties
- Presentation

Equipment is a bridge between the Item System and active gameplay.

---

# Core Principles

## Universal

Any Gameplay Object may equip items.

Examples:

Player

Companion

Friendly NPC

Enemy

Boss

Future Mounts

Equipment behavior remains consistent regardless of owner.

---

## Data Driven

Equipment behavior is authored through Item Definition Components.

Adding new equipment should rarely require programming.

---

## Compositional

Equipment grants capabilities by composing existing systems.

No special-case equipment logic should exist.

---

# Architecture

Gameplay Object

↓

Equipment Component

↓

Equipment Slots

↓

Equipped Item Instances

↓

Item Definition Components

↓

ToyChest Systems

---

# Responsibilities

The Equipment System is responsible for:

- Equipping items
- Unequipping items
- Slot validation
- Requirement validation
- Activating equipment contributions
- Deactivating equipment contributions
- Equipment queries
- Equipment events
- Persistence

The Equipment System is **not** responsible for:

- Combat calculations
- Damage
- Ability execution
- Inventory management
- Crafting
- Loot generation

---

# Equipment Slots

The initial slot layout is:

Primary Weapon

Off-Hand

Helmet

Chest

Gloves

Boots

Ring 1

Ring 2

Amulet

Relic

Future slot types may be added without architectural changes.

---

# Equipment Requirements

Equipment may define requirements.

Examples:

Minimum Level

Required Tags

Quest Completion

Companion Species

Region Unlock

Faction

Requirements are evaluated through data.

---

# Equipment Contributions

Equipped items may contribute:

## Attribute Modifiers

Examples:

+Strength

+Armor

+Critical Chance

+Fire Resistance

---

## Resource Modifiers

Examples:

+Maximum Health

+Maximum Mana

+Energy Regeneration

+Ammo Capacity

---

## Gameplay Tags

Examples:

FireWeapon

Holy

Heavy

Legendary

Flying (future)

These tags participate in gameplay queries.

---

## Abilities

Equipment may grant active or passive abilities.

Examples:

Fire Sword

↓

Flame Slash

Boots

↓

Air Dash

Relic

↓

Summon Companion

Abilities remain part of the Ability System.

---

## Gameplay Effects

Equipment may apply passive Gameplay Effects.

Examples:

Life Regeneration

Movement Speed

Thorns

Increased Loot

Ignite Chance

Gameplay Effects remain owned by the Gameplay Effect System.

---

## World Properties

Equipment may influence world interactions.

Examples:

Heat Source

Water Walking

Lava Immunity

Harvest Bonus

Light Source

These contributions participate in the World Reaction System.

---

## Presentation

Equipment may contribute:

Meshes

Animations

Particles

Audio

Trails

UI

Presentation remains independent of gameplay logic.

---

# Equipment Swapping

Players may change equipment at any time.

Equipment changes do not pause gameplay.

Changing equipment during combat is an intentional risk-versus-reward decision.

The system should support rapid experimentation and build iteration.

---

# Companion Equipment

Companions use the same Equipment System.

Slot layouts may differ by companion type.

Examples:

Wolf

Harness

Charm

Collar

Bird

Beak

Harness

Charm

No specialized companion equipment system is required.

---

# Equipment Queries

Examples:

Equipped Weapon

Has Tag

Has Ability

Has Component

Total Attribute Bonus

Granted Gameplay Effects

These queries should remain efficient.

---

# Equipment Events

The Equipment System publishes events.

Examples:

Item Equipped

Item Unequipped

Equipment Changed

Ability Granted

Ability Removed

Equipment Requirement Failed

Other systems subscribe through Gameplay Events.

---

# Multiplayer

Equipment supports:

Server Authority

Replication

Prediction

Persistence

Runtime state belongs to Item Instances.

Definitions remain immutable.

---

# AI

AI evaluates equipment through metadata.

Examples:

Combat Value

Defense Value

Mobility Value

Healing Value

Elemental Synergy

Build Synergy

AI should reason generically rather than recognizing specific equipment.

---

# Future Expansion

Examples:

Sockets

Runes

Enchantments

Set Bonuses

Evolution

Transmogrification

Artifact Progression

Legendary Traits

None should require redesigning the Equipment System.

---

# Uses ToyChest Systems

Item System

Inventory System

Attribute System

Resource System

Ability System

Gameplay Effect System

Gameplay Tags

Relationship System

World Reaction System

Gameplay Events

Definition Composition

---

# Success Criteria

The Equipment System succeeds when:

- Equipment activates capabilities rather than implementing gameplay.
- New equipment is authored almost entirely through data.
- Players can freely experiment with builds.
- Companions and future actors reuse the same system.
- Equipment integrates cleanly with every major gameplay system.
- Future equipment mechanics require minimal engine changes.

---

# Implementation Notes

- Equip Item Instances, never Item Definitions.
- Validate requirements before activation.
- Activate contributions through the appropriate systems rather than embedding behavior in equipment code.
- Treat equipment changes as transactional so all granted capabilities are applied or removed consistently.
- Keep presentation concerns separate from gameplay activation.