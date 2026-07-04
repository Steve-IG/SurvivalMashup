# Interaction System

**Architecture:** ToyChest v1.0
**Status:** Living Specification
**Owner:** Core Gameplay

---

# Purpose

The Interaction System enables Gameplay Objects to interact with one another through a consistent, ability-driven framework.

Interactions are the primary way players and AI engage with the world outside of combat.

Rather than implementing custom interaction logic on individual objects, Gameplay Objects expose one or more Interaction Abilities that can be discovered and executed by the Interaction System.

---

# Design Philosophy

Everything interactive should behave consistently.

Opening a chest, harvesting a flower, talking to an NPC, activating a portal, rescuing a companion, reviving an ally, and using a crafting station are all interactions.

Interactions are implemented as Abilities.

This allows every interaction to benefit from the existing Ability System, Gameplay Tags, Gameplay Events, networking, and validation.

---

# Core Goals

- One interaction framework for the entire game.
- Minimize bespoke object logic.
- Support players and AI equally.
- Integrate naturally with existing gameplay systems.
- Allow future interaction types without architectural changes.

---

# Architecture

Interactor Gameplay Object

↓

Interaction Query

↓

Target Gameplay Object

↓

Interaction Ability

↓

Ability System

↓

Gameplay Systems

↓

Gameplay Events

---

# Interaction Discovery

Gameplay Objects may expose one or more Interaction Abilities.

Examples:

Open Chest

Harvest

Talk

Trade

Craft

Travel

Rescue

Revive

Activate

Inspect

Interactable objects advertise their available interactions through data.

---

# Validation

Before an interaction executes, requirements are validated.

Examples:

Distance

Line of Sight

Gameplay Tags

Quest Progress

Region Unlock

Required Item

Companion Present

Cooldown

Validation is data-driven.

---

# Interaction Examples

## Harvest

Flower

↓

Harvest Ability

↓

Loot System

↓

Inventory

---

## Chest

Chest

↓

Open Ability

↓

Loot System

↓

Inventory

---

## Merchant

Merchant

↓

Trade Ability

↓

Merchant UI

↓

Economy System

---

## Portal

Portal

↓

Travel Ability

↓

Region System

---

## Companion Rescue

Companion Cage

↓

Rescue Ability

↓

Companion System

↓

Gameplay Events

---

## Revive Ally

Downed Player

↓

Revive Ability

↓

Resource System

↓

Gameplay Events

---

# Interaction Types

Examples include:

Conversation

Trading

Harvesting

Crafting

Looting

Travel

Rescue

Puzzle Activation

Quest Interaction

Future interactions should reuse the same framework.

---

# Input

Players initiate interactions through contextual input.

The Interaction System determines the highest-priority valid interaction within range.

Presentation of prompts is handled by the UI layer.

---

# AI

AI uses the same Interaction System.

Examples:

Open doors

Harvest resources

Activate objectives

Revive allies

Operate mechanisms

No separate AI interaction framework is required.

---

# Multiplayer

Interactions support:

Server authority

Validation

Prediction where appropriate

Replication

Deterministic execution

---

# Future Expansion

Examples:

Dialogue trees

Emotes

Housing interactions

Vehicles

Companion bonding

Mini-games

Photo mode interactions

No redesign should be required.

---

# Uses ToyChest Systems

Gameplay Object

Ability System

Gameplay Tags

Gameplay Events

Inventory System

Loot System

Region System

Companion System

Economy System

Crafting System

Adventure System

---

# Success Criteria

The Interaction System succeeds when:

- All world interactions use a common framework.
- Players and AI interact through the same architecture.
- New interactions are authored primarily through data.
- Designers rarely require custom gameplay code for interactive objects.
- Interaction remains consistent across every gameplay system.

---

# Implementation Notes

- Model interactions as specialized abilities.
- Discover interactions through Gameplay Object capabilities.
- Keep validation generic and data-driven.
- Separate interaction execution from UI presentation.
- Favor composition over custom object scripts.