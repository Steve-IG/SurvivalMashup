# Inventory System

**Architecture:** ToyChest v1.0
**Status:** Living Specification
**Owner:** Gameplay Systems

---

## System Ownership

This system owns:
- Item storage, stacking, transfer, sorting queries, filtering queries, capacity checks, and ownership transfer.

This system does NOT own:
- Item behavior, Equipment bonuses, Crafting logic, consumable effects, or Adventure progression.

Primary Responsibilities:
- Manage collections of Item Instances owned by Gameplay Objects.

Primary Data:
- Inventory capacity rules, slot rules, filter data, and storage configuration.

Primary Runtime Objects:
- Inventory Components and Item Instance collections.

Published Events:
- Item Added, Item Removed, Stack Changed, Inventory Full, Item Transferred.

Consumed Events:
- None.

---

# Milestone 0 Implementation (approved decisions)

The implemented Milestone 0 subset (`ToyChest.Systems.Inventory`):

- **InventorySet capability:** composed by the factory when the object definition declares an `InventoryDefinition` (slot capacity as data). Any Gameplay Object may own one.
- **Slot-based capacity** per the approved initial model. Capacity math counts merge space in existing stacks plus free slots; alternative capacity models are future `InventoryDefinition` configuration.
- **Deterministic, all-or-nothing operations:** stacks keep insertion order; adds merge into existing stacks of the same definition in that order, then place the remainder as a new stack; removals consume stacks in that order. An add or removal that cannot complete in full mutates nothing.
- **Stack management:** `TrySplit` and `TryMerge` reorganize stacks without crossing the inventory boundary; `TryTakeStack` hands a whole stack out (equipping, dropping); `TryTransferTo` moves a stack between inventories transactionally.
- **Event facts:** `ItemAdded` / `ItemRemoved` report quantities crossing the inventory boundary; `StackChanged` reports quantity changes of stacks that remain; `InventoryFull` reports rejected adds; `ItemTransferred` reports ownership transfer between inventories. Emptied stacks leave silently — the boundary event carries the fact.
- **Ownership transfer** is inventory-to-inventory in Milestone 0; routing through the Relationship System arrives with that system.

Sorting, filtering, and search are presentation-side queries over `Stacks` and remain specified below. World items (dropped Gameplay Objects) arrive with world systems.

---

# Purpose

The Inventory System manages collections of Item Instances owned by Gameplay Objects.

The Inventory System is responsible for storing, organizing, transferring, and querying items.

It does **not** determine what items do.

Item behavior belongs to the Item System.

---

# Design Philosophy

Inventory is ownership.

Inventory is not presentation.

The Inventory System manages collections of Item Instances.

User interface concerns (slots, grids, icons, sorting layouts) are handled independently.

---

# Core Principles

## Universal

Any Gameplay Object may own an inventory.

Examples:

Player

Companion

Merchant

Chest

Enemy Corpse

Crafting Station

Storage Chest

Mailbox (future)

Vehicle (future)

---

## Data Driven

Inventory stores Item Instances.

Inventory never contains gameplay logic specific to individual items.

---

## UI Independent

Inventory stores collections.

UI determines presentation.

Possible presentations include:

Grid

List

Radial Menu

Quick Bar

Equipment Screen

Search

Filters

These are all views of the same inventory.

---

# Architecture

Gameplay Object

↓

Inventory Component

↓

Item Instances

↓

Item Definitions

---

# Responsibilities

The Inventory System is responsible for:

Adding items

Removing items

Moving items

Splitting stacks

Merging stacks

Sorting

Filtering

Searching

Ownership transfer

Capacity validation

Querying

Persistence

The Inventory System is **not** responsible for:

Combat

Equipment bonuses

Crafting logic

Consumable behavior

Adventure progression

These belong to other systems.

---

# Capacity

Capacity should be configurable.

Examples:

Unlimited (debug)

Weight-based

Slot-based

Volume-based

Hybrid

The capacity model should be interchangeable.

The initial implementation will use a generous slot-based system, but the architecture should not assume one capacity model forever.

---

# Stacking

Stacking behavior comes from Item Definitions.

Inventory respects those rules.

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

# Item Queries

Inventory supports queries such as:

Contains Item

Contains Tag

Contains Category

Contains Component

Quantity

First Match

All Matches

Empty Slots (if relevant)

These queries should remain efficient.

---

# Sorting

Sorting is configurable.

Examples:

Name

Type

Rarity

Value

Recently Acquired

Weight

Custom

Sorting affects presentation only.

---

# Filtering

Filtering is data-driven.

Examples:

Weapons

Consumables

Resources

Crafting Materials

Adventure Items

Legendary

Fire

Equipment

Companion Items

Filtering uses Item metadata.

---

# Ownership

Items belong to inventories.

Ownership transfers through the Relationship System.

Examples:

Loot Pickup

Trading

Crafting

Dropping

Companion Storage

Death

---

# World Items

Dropped items exist as Gameplay Objects.

Picking them up transfers ownership into an Inventory.

Dropping reverses this process.

---

# Events

Inventory publishes events.

Examples:

Item Added

Item Removed

Stack Changed

Inventory Full

Inventory Cleared

Ownership Changed

Other systems subscribe through Gameplay Events.

---

# Multiplayer

Inventory supports:

Server Authority

Replication

Trading

Prediction where appropriate

Persistence

Definitions remain immutable.

Instances replicate mutable state.

---

# AI

AI inventories operate identically.

Examples:

Merchant

Companion

Enemy

NPC

AI queries inventory through the same interfaces as players.

---

# Future Expansion

Examples:

Shared Storage

Guild Storage

Companion Bags

Auto-Sorting

Favorites

Lock Items

Crafting Queues

Mail

Auction House

Loadouts

No architectural redesign should be required.

---

# Uses ToyChest Systems

Item System

Relationship System

Gameplay Tags

Gameplay Events

Save System

Definition Composition

---

# Persistence Boundary

Per Engine Principle 25:

- **Authoritative:** the slot contents — for each occupied slot, the item instance (its `ItemInstanceId`, its definition id, and its stack count).
- **Derived:** occupancy counts, capacity availability, and stack-merge results.
- **Serialized:** per occupied slot — item definition id, item instance id, and stack count. Slot capacity is definition data, not saved.
- **Reconstructed:** the `InventorySet` is rebuilt from the `InventoryDefinition` (capacity), and item instances are recreated from their definitions and placed back into their slots. Item identity persists through `ItemInstanceId` (Principle 21). The event-quiet restore hook is added with the Save Framework, which owns the serialized shape.

---

# Success Criteria

The Inventory System succeeds when:

- It stores any Item Instance without knowing its gameplay behavior.
- UI can change without modifying gameplay code.
- Any Gameplay Object can own an inventory.
- New item types require no inventory changes.
- Inventory remains deterministic and multiplayer-safe.

---

# Implementation Notes

- Store runtime data as collections of Item Instances.
- Treat inventory presentation as a UI concern.
- Expose efficient query APIs.
- Publish events rather than tightly coupling dependent systems.
- Avoid embedding item-specific behavior in inventory logic.