# Design Pillars

**Status:** Approved  
**Owner:** Creative Director / Lead Designer  
**Last Updated:** June 2026

---

# Purpose

The Design Pillars define the core principles that make this game unique. Every gameplay system, feature, mechanic, and technical decision should reinforce one or more of these pillars.

These are not individual mechanics. They are the guiding philosophies behind the game.

When evaluating a new feature, always ask:

> **Which Design Pillar(s) does this strengthen?**

If the answer is "none," the feature should be reconsidered or removed.

---

# Pillar 1 — The World Changes Because of the Player

## Vision

The player is not simply passing through the world—they are reclaiming it.

Every dangerous region can be liberated through exploration, combat, and progression. As regions are reclaimed, the world visibly transforms and becomes safer, more prosperous, and more alive.

The player's actions have permanent, meaningful impact.

## Design Goals

- Regions have distinct states of progression.
- Defeating major threats permanently changes the environment.
- Friendly NPCs return to liberated regions.
- Merchants, services, companions, and adventures become available.
- Dangerous enemies disappear from reclaimed areas and are replaced by peaceful life.
- Earlier regions remain relevant throughout the game.

## Player Experience

Players should feel:

- Proud of their accomplishments.
- Responsible for improving the world.
- Excited to see the visible results of their actions.

## Engineering Considerations

This pillar requires persistent world-state tracking.

Regions should support progression states (e.g., Occupied → Liberated → Restored) that drive environmental changes, NPC behavior, available services, adventures, and encounter tables.

---

# Pillar 2 — Power Is Earned and Felt

## Vision

The player should experience meaningful growth throughout the game.

Each new region begins as a dangerous challenge. Through exploration, crafting, better equipment, companions, and character progression, the player gradually overcomes those challenges until they feel unquestionably powerful.

The game intentionally creates a repeating "Hero Wave."

## The Hero Wave
New Region
↓
Underpowered
↓
Learning
↓
Capable
↓
Powerful
↓
Dominant
↓
Next Region

This repeating cycle ensures that players continually experience:

- Challenge
- Mastery
- Triumph
- Excitement
- Anticipation

## Design Goals

- Progression should feel significant.
- Old enemies become noticeably easier.
- New regions introduce fresh challenges.
- New abilities should fundamentally change gameplay, not merely increase statistics.

## Engineering Considerations

Enemy scaling should be region-based rather than globally synchronized with the player's level. This preserves the feeling of becoming stronger while ensuring new regions remain challenging.

---

# Pillar 3 — Freedom Through Meaningful Variety

## Vision

Players should feel free to create their own playstyle.

Weapons, companions, abilities, crafting, elemental interactions, and equipment should combine into many viable builds rather than one optimal strategy.

Experimentation is rewarded.

## Design Goals

- Multiple effective combat styles.
- Diverse ability progression paths.
- Distinct companion roles.
- Wide variety of weapons and equipment.
- Elemental strengths and weaknesses.
- Interesting loot with meaningful choices.
- Crafting that expands possibilities rather than replacing exploration.

## Player Experience

Players should frequently think:

> "I wonder what happens if I combine these."

## Engineering Considerations

Systems should be data-driven wherever possible to make new content easy to add without extensive code changes.

---

# Pillar 4 — Adventure Is Better Together

## Vision

The game is designed to be equally enjoyable solo or cooperatively with up to four players.

Cooperative play should create memorable shared experiences without making solo players feel disadvantaged.

Players should naturally support one another through complementary builds, companions, exploration, and combat.

## Design Goals

- Seamless drop-in/drop-out cooperative play.
- Shared victories.
- No competition for loot.
- No class-locking of equipment.
- Builds naturally complement one another without requiring fixed roles.

## Player Experience

Players should feel:

- Cooperative rather than competitive.
- Excited to explore together.
- Proud of overcoming challenges as a team.

## Engineering Considerations

Core gameplay systems should be designed with multiplayer compatibility in mind from the beginning, even if multiplayer features are implemented incrementally.

---

# Pillar 5 — Every Journey Reveals Something New

## Vision

Exploration should consistently reward curiosity.

Procedural generation exists to create fresh adventures, not merely random layouts.

Every expedition should offer new discoveries.

## Design Goals

- Discover new regions.
- Encounter unexpected enemies.
- Find unique resources.
- Unlock rare companions.
- Discover hidden secrets.
- Experience procedural variation that feels intentional.

## Player Experience

Players should frequently wonder:

> "What's over that hill?"

## Engineering Considerations

Procedural systems should combine handcrafted design with procedural variation to maintain both quality and replayability.

---

# Emotional Goals

Throughout the game, players should consistently experience:

- Excitement
- Curiosity
- Surprise
- Challenge
- Joy

These emotions should guide design decisions across all gameplay systems.

---

# Anti-Goals

This game intentionally avoids:

- MMO mechanics.
- Endless grinding.
- Competition over loot.
- Class-locked equipment.
- Punishing survival mechanics that distract from adventure.
- Artificial progression gates that slow player momentum.

---

# Feature Evaluation Checklist

Before approving any major feature, ask:

- Does it reinforce at least one Design Pillar?
- Does it strengthen the player's progression fantasy?
- Does it encourage exploration or experimentation?
- Does it improve cooperative play or remain enjoyable solo?
- Does it help the player reclaim the world?
- Will players remember this experience?

If the answer to most of these questions is "no," reconsider the feature.

---

# Related Documents

- 00_OVERVIEW.md
- 01_CORE_GAMEPLAY.md
- 02_PLAYER.md
- 03_WORLD.md
- 04_COMBAT.md