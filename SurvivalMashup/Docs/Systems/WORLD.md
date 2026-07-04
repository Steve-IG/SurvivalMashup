# World

**Status:** Living Specification  
**Version:** 1.0  
**Owner:** Lead World Designer  
**Last Updated:** June 2026

---

## System Ownership

This system owns:
- World structure, Hub World role, region state philosophy, campaign structure, Frontier structure, and liberation/restoration design.

This system does NOT own:
- Region streaming, Adventure objectives, Loot generation, enemy spawning, or procedural generation implementation.

Primary Responsibilities:
- Define the world as a progression system made of evolving regions connected through the Hub World.

Primary Data:
- Region state design, Hub World growth expectations, campaign region structure, and Frontier rules.

Primary Runtime Objects:
- Region state and Hub World persistence state.

Published Events:
- TBD

Consumed Events:
- TBD

---

# Purpose

The world is one of the game's primary progression systems.

Rather than serving as a backdrop for combat, the world is designed to evolve alongside the player. As players become stronger, they reclaim dangerous regions, restore civilization, unlock new opportunities, and expand the frontier.

The world should encourage curiosity, reward exploration, and visibly reflect the player's accomplishments.

---

# World Philosophy

The world is effectively infinite.

Rather than existing as one continuous map, the game is built around a network of individual regions connected through a central Hub World.

This architecture provides:

- Strong pacing
- Meaningful progression
- High replayability
- Excellent cooperative gameplay
- Efficient streaming and loading
- Endless opportunities for future expansion

Each region should feel like a meaningful destination rather than simply another biome.

---

# World Structure

```
Hub World
        │
 ┌──────┼──────┐
 │      │      │
Region Region Region
 │      │      │
More Regions...
```

The Hub World acts as the player's permanent home and gateway to adventure.

Players travel from the Hub World into regions through magical portals (working concept) or another lore-appropriate travel system.

Every region is a self-contained adventure.

---

# Hub World

The Hub World is persistent throughout the game.

It serves as the player's home base and grows as the player progresses.

Potential features include:

- Player housing
- Crafting stations
- Merchants
- Companion management
- Ability upgrades
- Storage
- NPCs rescued from liberated regions
- Seasonal events
- Multiplayer gathering space

The Hub World should become increasingly vibrant as players liberate more regions.

---

# Campaign Structure

The core campaign consists of approximately ten handcrafted regions.

Each region introduces:

- New enemies
- New mechanics
- New resources
- New environmental storytelling
- New progression opportunities
- A distinct Regional Threat

These regions form the primary progression path of the game.

Additional handcrafted regions may be added through future content updates.

---

# The Frontier

Completing the campaign unlocks The Frontier.

The Frontier contains an effectively limitless number of procedurally generated regions.

These regions provide:

- Endless replayability
- High-level progression
- Seasonal content
- Rare rewards
- Experimental encounters
- Community events
- New combinations of enemies, resources, and objectives

The Frontier should feel like a continuation of the adventure rather than an endless grind.

---

# Region Structure

Every region follows a common design philosophy while maintaining its own identity.

## Safe Entry Area

Provides an opportunity to prepare before venturing deeper.

Typical features include:

- Friendly NPCs (when appropriate)
- Crafting
- Merchants
- Adventure givers
- Fast travel point

---

## Frontier

Introduces players to the region.

Contains:

- Common resources
- Lower-risk encounters
- Exploration opportunities
- Environmental storytelling

---

## Wilderness

The heart of the region.

Contains:

- Stronger enemies
- Rare resources
- Hidden locations
- Dynamic encounters
- Optional objectives
- Mini-dungeons

---

## Points of Interest

Each region contains handcrafted and procedural points of interest such as:

- Caves
- Ruins
- Ancient temples
- Villages
- Shrines
- Hidden groves
- Enemy camps
- World events

Exploration should consistently reward curiosity.

---

## Regional Strongholds

Enemy-controlled locations that act as major objectives.

Capturing strongholds weakens the region's overall threat and often unlocks new opportunities.

---

## Regional Threat

Every region contains a central threat.

Examples include:

- Powerful monsters
- Enemy commanders
- Corrupted guardians
- Ancient machines
- Magical anomalies

Defeating the Regional Threat is usually the final required objective, but regions are intentionally designed so that other objective structures are also possible.

---

# Completing a Region

Regions are completed by fulfilling their required objectives.

This usually culminates in defeating the Regional Threat.

Completing a region should always feel like a meaningful accomplishment.

---

# Liberating a Region

Liberation permanently changes the world.

Examples include:

- Dangerous enemies retreat.
- Friendly NPCs return.
- Roads become safer.
- Merchants establish shops.
- New adventures become available.
- New companions become recruitable.
- Additional crafting options unlock.
- Music becomes more hopeful.
- Wildlife returns.
- Environmental corruption fades.

The player should immediately recognize that the world has changed because of their actions.

---

# Procedural Generation Philosophy

Procedural generation exists to create meaningful variety rather than randomness.

Every region is built using handcrafted design rules combined with procedural generation.

This hybrid approach allows:

- Memorable locations
- High replayability
- Efficient content creation
- Endless combinations
- Strong environmental storytelling

Quality should always take priority over randomness.

---

# Region Progression

Each region progresses through distinct world states.

Occupied

↓

Contested

↓

Liberated

↓

Restored

These states affect:

- Enemy populations
- NPC behavior
- Available merchants
- Adventures
- Resources
- Visual presentation
- Music
- Fast travel
- Services

---

# Design Goals

Every region should answer "yes" to the following questions:

- Does this place have its own identity?
- Does exploration feel rewarding?
- Does the player become stronger here?
- Does the player permanently improve the world?
- Will players remember this region?
- Does this region introduce something new?

---

# Engineering Notes

The region is the fundamental unit of world architecture.

Regions should be designed as self-contained content packages to simplify:

- Streaming
- Save data
- Multiplayer synchronization
- AI spawning
- Procedural generation
- Content updates
- Seasonal events
- Testing
- Asset management

This architectural decision should be preserved throughout development.

---

# Related Documents

- Docs/Foundations/GAME_VISION.md
- Docs/Foundations/DESIGN_PILLARS.md
- Docs/Foundations/CORE_GAMEPLAY_LOOP.md
- Docs/Systems/PROGRESSION.md
- Docs/Systems/COMBAT.md

Future related topics:

- Hub World
- Procedural Generation