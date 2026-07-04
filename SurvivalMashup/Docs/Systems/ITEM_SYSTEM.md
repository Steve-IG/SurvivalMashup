# Item System

**Architecture:** ToyChest v1.0
**Status:** Living Specification
**Owner:** Gameplay Systems

---

## System Ownership

This system owns:
- Item definitions, definition components, item instance model, stacking rules, durability rules, quality rules, and affix structure.

This system does NOT own:
- Inventory storage, Equipment slot logic, Loot table generation, or Crafting validation.

Primary Responsibilities:
- Define the reusable model for collectible, craftable, equippable, consumable, and tradable objects.

Primary Data:
- ItemDefinition assets and item definition components.

Primary Runtime Objects:
- ItemInstance data, durability, charges, affixes, quality, ownership, and item relationships.

Published Events:
- TBD

Consumed Events:
- TBD

---

# Purpose

The Item System defines every collectible, craftable, equippable, consumable, or tradable object in the game.

Items are immutable definitions authored through data.

Runtime inventories store Item Instances, not Item Definitions.

The Item System provides the foundation for Inventory, Equipment, Loot, Crafting, Merchants, Adventures, and Companion progression.

---

# Design Philosophy

Everything is an Item.

Rather than creating specialized item classes, items are composed from reusable definition components.

Items describe what they are capable of.

Gameplay systems determine how those capabilities are used.

---

# Core Principles

## Data Driven

Items should be authored entirely through data.

Adding a new item should rarely require writing code.

---

## Immutable Definitions

Item Definitions never change during gameplay.

Examples:

Iron Ore

Health Potion

Steel Sword

Fire Rune

Companion Egg

---

## Mutable Instances

Players interact with Item Instances.

Instances contain runtime state.

Examples:

Current Durability

Current Charges

Affixes

Quality

Owner

Crafting History

Custom Name (future)

---

# Architecture

ItemDefinition (ScriptableObject)

↓

Definition Components

↓

ItemInstance

↓

Inventory

↓

Equipment / Crafting / Merchant / Loot

---

# Definition Components

An Item Definition is composed of reusable components.

Examples include:

Equipment Component

Weapon Component

Armor Component

Consumable Component

Crafting Material Component

Adventure Component

Placeable Component

Companion Component

Ability Unlock Component

Value Component

Durability Component

Icon Component

Mesh Component

Audio Component

Visual Effects Component

Interaction Component

Future systems should extend items through new components rather than subclasses.

---

# Item Categories

Categories exist primarily for organization and UI.

Examples:

Weapons

Armor

Consumables

Resources

Crafting Materials

Companion Items

Adventure Items

Relics

Tools

Cosmetics

Categories should not determine gameplay behavior.

---

# Item Tags

Items may expose Gameplay Tags.

Examples:

Fire

Legendary

Sword

Heavy

Magic

Food

Plant

Rare

Mechanical

Tags enable interaction with other gameplay systems.

---

# Item Attributes

Items may contribute Attribute Modifiers.

Examples:

+20 Strength

+10 Fire Resistance

+5% Critical Chance

Attribute contributions become active when appropriate (such as when equipped).

---

# Item Resources

Some items contain runtime resources.

Examples:

Durability

Charges

Ammo

Energy

Fuel

These are stored on the Item Instance.

---

# Item Abilities

Items may grant Abilities.

Examples:

Fire Sword

↓

Flame Slash Ability

Boots

↓

Dash Ability

Fishing Rod

↓

Fishing Ability

Abilities are granted while the item is active.

---

# Item Gameplay Effects

Items may apply Gameplay Effects.

Examples:

Potion

↓

Restore Health

Bomb

↓

Explosion

Food

↓

Regeneration

Equipment

↓

Passive Bonuses

---

# Item Relationships

Item Instances may maintain relationships.

Examples:

Owner

Crafter

Bound Player

Companion Owner

Adventure Association

These are runtime properties.

---

# Stacking

Stacking behavior is defined per Item Definition.

Examples:

Wood

Stack: 999

Potion

Stack: 20

Sword

Stack: 1

Companion Egg

Stack: 1

---

# Durability

Durability is optional.

Items requiring durability expose a Durability Component.

Items without durability incur no runtime overhead.

---

# Quality

Items may define quality tiers.

Examples:

Common

Uncommon

Rare

Epic

Legendary

Mythic

Quality modifies presentation and gameplay through data.

---

# Affixes

Item Instances may contain affixes.

Examples:

Flaming

Swift

Heavy

Lucky

Vampiric

Affixes modify existing item capabilities rather than replacing them.

---

# Crafting

Crafting consumes Item Instances and produces new Item Instances.

Crafting operates entirely on the Item System.

---

# Loot

Loot Tables generate Item Instances.

The Item System remains independent of loot generation.

---

# Merchants

Merchants buy and sell Item Instances.

Pricing is determined through Value Components and economy systems.

---

# Multiplayer

Item Instances support:

Replication

Ownership

Persistence

Trading

Synchronization

Definitions remain shared immutable data.

---

# AI

AI reasons about items through metadata.

Examples:

Combat Value

Healing Value

Crafting Value

Trade Value

Adventure Value

AI should not require handcrafted logic for individual items.

---

# Future Expansion

Examples:

Sockets

Runes

Enchantments

Evolution

Item Experience

Set Bonuses

Transmogrification

Housing Decoration

Pet Equipment

No architectural redesign should be required.

---

# Success Criteria

The Item System succeeds when:

- Nearly all new items are created entirely through data.
- New gameplay is added through Definition Components rather than inheritance.
- Inventory, Equipment, Loot, Crafting, and Merchants all operate on the same Item model.
- Runtime state is isolated from immutable definitions.
- The system scales to thousands of unique items.

---

# Implementation Notes

- Store authoring data in immutable `ItemDefinition` ScriptableObjects.
- Represent runtime ownership and mutable state with `ItemInstance`.
- Prefer composition via Definition Components over specialized subclasses.
- Keep item definitions lightweight and reusable.