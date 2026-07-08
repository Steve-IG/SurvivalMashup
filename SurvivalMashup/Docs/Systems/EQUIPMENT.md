# Equipment System

**Architecture:** ToyChest v1.0
**Status:** Living Specification
**Owner:** Gameplay Systems

---

## System Ownership

This system owns:
- Equip and unequip flow, slot validation, requirement checks, and equipment contribution activation.

This system does NOT own:
- Combat calculations, Damage resolution, Ability execution, Inventory storage, Crafting, or Loot generation.

Primary Responsibilities:
- Bridge Item Instances into active gameplay capabilities on Gameplay Objects.

Primary Data:
- Equipment slot definitions, requirement rules, and equipment contribution references.

Primary Runtime Objects:
- Equipment Components, equipped Item Instances, and active equipment state.

Published Events:
- Item Equipped, Item Unequipped, Equip Failed (with reason).

Consumed Events:
- None.

Ability grant/revoke facts remain published by the Ability System (`AbilityGranted` / `AbilityRevoked`); Equipment does not duplicate them. An aggregate Equipment Changed event is future work if a subscriber needs it.

---

# Milestone 0 Implementation (approved decisions)

The implemented Milestone 0 subset (`ToyChest.Systems.Equipment`):

- **EquipmentSet capability:** composed by the factory when the object definition declares equipment slots. Slot layouts are data: `EquipmentSlotDefinition` assets listed per object type, so companions and players differ only in authoring.
- **EquippableDefinition** is the Item System's equippable Definition Component, owned by this system: allowed slots (a ring lists Ring 1 and Ring 2), required owner tags, and the contributions.
- **Contributions compose existing systems, exactly revocable:** granted tags go through the reference-counted tag container (two items granting the same tag coexist); attribute modifiers register with the equipped `ItemInstance` as the modifier source, so unequip revokes precisely that item's contribution; abilities are granted through the Ability System; statuses are applied through the Status Effect System. No gameplay logic lives in Equipment.
- **Deterministic validation order**, first failing check reported (`EquipResult`): equippable → slot known → slot allowed → slot free → owner requirements. `CanEquip` runs the same validation without committing. Occupied slots reject; swap is an explicit unequip-then-equip by the caller.
- **Transactional equip:** all validation (including contribution capability checks, e.g. a modifier targeting an attribute the owner lacks) completes before any contribution activates, so a rejected equip mutates nothing.
- **Already-active handling:** an ability already granted or a status already active from another source is skipped and never revoked by unequip — each equip entry revokes exactly what it activated. Two equipped items sharing the same granted ability or status is a known Milestone 0 limitation (the first unequip removes it); refcounted grants are a future extension if authoring hits it.
- **Inventory independence:** equip/unequip operate on `ItemInstance` values; the caller (interaction, UI, AI) moves instances between inventory and equipment. Equipment never reaches into inventories (Capability Independence).

Level/adventure/faction requirements, resource modifiers, World Property contributions, and presentation contributions are future work and remain specified below.

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

Adventure Completion

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

# Persistence Boundary

Per Engine Principle 25:

- **Authoritative:** which item instance is equipped in each slot.
- **Derived:** the tags, attribute modifiers, granted abilities, and statuses each equipped item activates — all re-applied on equip through their owning systems.
- **Serialized:** per occupied slot — the equipped item's definition id and instance id. The slot layout is definition data.
- **Reconstructed:** the `EquipmentSet` is rebuilt from its slot layout; re-equipping each restored item re-activates its contributions through the owning systems (tags, attributes, abilities, statuses). Because those contributions are re-applied on equip, no equipment-derived state is serialized — a clean example of Reconstruction Over Serialization.

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