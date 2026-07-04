# Adventure System

**Architecture:** ToyChest v1.0
**Status:** Living Specification
**Owner:** Game Design

---

## System Ownership

This system owns:
- Adventure structure, objective assembly, progress state, completion criteria, and restoration cadence.

This system does NOT own:
- Combat rules, loot generation, NPC AI, region streaming, or dialogue implementation.

Primary Responsibilities:
- Create meaningful regional situations that guide discovery, resolution, restoration, and reward.

Primary Data:
- Adventure templates, objective modules, completion rules, and generation parameters.

Primary Runtime Objects:
- Active adventures, objectives, progress state, and Adventure Components.

Published Events:
- TBD

Consumed Events:
- TBD

---

# Purpose

The Adventure System is responsible for creating meaningful experiences within every region of ToyChest.

Unlike traditional RPG quest systems, ToyChest does not primarily generate tasks.

It generates situations.

Players uncover, investigate, and resolve those situations through exploration, combat, discovery, and restoration.

The objective is for every expedition to feel like an adventure rather than a checklist.

---

# Design Philosophy

Players should rarely think:

> "I'm completing quests."

Instead they should think:

> "I'm helping this world."

Every adventure should tell a small story.

Whether handcrafted or procedurally generated, adventures should have a beginning, escalation, climax, and resolution.

---

# Core Principles

## Situation Before Objective

Every adventure begins with a situation.

Examples:

A village has been abandoned.

A Reality Anchor is malfunctioning.

Wildlife has become aggressive.

A merchant has gone missing.

An ancient guardian has awakened.

The player first discovers the situation.

Objectives emerge naturally from understanding it.

---

## Objectives Support the Story

Objectives exist to guide the player.

They should never replace discovery.

Objectives should answer:

"What would my character naturally do next?"

Rather than:

"What does the designer need me to do?"

---

## Restoration Is Success

The player's goal is almost always to improve the state of the region.

Success may include:

Defeating a regional threat.

Rescuing stranded guests.

Saving local inhabitants.

Repairing Reality Anchors.

Purifying corruption.

Restoring trade routes.

Reuniting families.

Protecting wildlife.

Helping communities recover.

Different adventures can accomplish restoration in different ways.

---

# Adventure Structure

Every adventure follows the same high-level rhythm.

```
Discovery

↓

Understanding

↓

Preparation

↓

Action

↓

Resolution

↓

Restoration

↓

Reward
```

The specific gameplay varies, but the emotional cadence remains consistent.

---

# Discovery

The player discovers something unusual.

Examples:

Smoke in the distance.

Distress signal.

Destroyed caravan.

Strange creature behavior.

Reality distortion.

Abandoned settlement.

Discovery should encourage curiosity.

---

# Understanding

Players investigate.

They gather information by:

Exploring

Speaking with NPCs

Following tracks

Scanning anomalies

Observing the environment

The world explains itself through gameplay.

---

# Preparation

Players decide how to approach the situation.

Examples:

Equip different gear.

Adjust abilities.

Craft consumables.

Purchase supplies.

Change tactics.

Preparation should feel meaningful without slowing pacing.

---

# Action

The player attempts to resolve the situation.

This may involve:

Combat

Traversal

Puzzle solving

Escorting

Rescue

Investigation

Defense

Construction

Negotiation

Environmental manipulation

Different realities emphasize different gameplay.

---

# Resolution

A major obstacle is overcome.

Examples:

Boss defeated.

Bridge repaired.

Artifact recovered.

Village defended.

Corruption removed.

Reality stabilized.

This represents the turning point.

---

# Restoration

The world visibly improves.

Examples:

NPCs return.

Wildlife calms.

Plants regrow.

Merchants reopen.

Reality stabilizes.

Music changes.

Lighting improves.

Environmental storytelling should reinforce success.

---

# Reward

Rewards should reinforce the fantasy of helping the world.

Examples:

Equipment

Resources

Crafting recipes

Companions (rare)

New merchants

NPC relationships

Reputation

Story progression

Cosmetics

Regional collectibles

Every reward should feel connected to the adventure.

---

# Objective Types

The Adventure System assembles objectives from modular components.

Examples include:

Explore

Rescue

Investigate

Harvest

Defeat

Defend

Escort

Repair

Activate

Collect

Protect

Escape

Negotiate

Restore

Procedural adventures combine these into unique sequences.

---

# Optional Objectives

Optional objectives expand adventures rather than distract from them.

Examples:

Save additional civilians.

Recover lost heirlooms.

Rescue hidden companions.

Discover lore.

Protect structures.

Complete without civilians being injured.

Find hidden shortcuts.

Players should never feel punished for skipping optional content.

---

# Emergent Adventures

Gameplay systems may create entirely new adventures.

Examples:

A wildfire spreads into a nearby village.

A merchant requests help after losing supplies.

Wildlife migrates because another region changed.

An enemy faction launches an unexpected attack.

A companion discovers hidden ruins.

These events make the world feel alive.

---

# Regional Adventures

Every handcrafted region contains multiple adventures.

Examples:

Main Adventure

Regional Stories

Hidden Adventures

NPC Adventures

Environmental Adventures

Companion Adventures

Players should naturally encounter many adventures while exploring.

---

# Procedural Adventures

Procedural regions generate adventures using templates.

Generation considers:

Biome

Local civilization

Enemy faction

World events

Weather

Season

Difficulty

Regional modifiers

The objective is not infinite variety.

The objective is infinite combinations of meaningful situations.

---

# Adventure Completion

An adventure is complete when the region has been meaningfully improved.

Completion is not measured solely by enemy deaths.

Instead it measures:

Threat removed.

People helped.

Area restored.

Mystery resolved.

Community strengthened.

---

# Integration

The Adventure System integrates with:

Region System

NPC System

Companion System

World Reaction System

Faction System

Loot System

Relationship System

Hub World

Narrative System

Every gameplay system contributes to the player's adventure.

---

# Success Criteria

The Adventure System succeeds when:

- Players remember stories rather than objectives.
- Every region feels worth saving.
- Exploration naturally reveals meaningful situations.
- Restoration creates visible, lasting change.
- Procedural adventures feel authored rather than random.
- Objectives support curiosity instead of replacing it.