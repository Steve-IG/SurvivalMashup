# Player

**Status:** Living Specification
**Version:** 1.0
**Owner:** Lead Gameplay Designer
**Last Updated:** June 2026

---

## System Ownership

This system owns:
- Player fantasy, feel goals, player capability categories, control expectations, and build-expression goals.

This system does NOT own:
- Ability implementation, Equipment rules, Companion behavior, Interaction logic, Damage rules, or UI implementation.

Primary Responsibilities:
- Define how controlling the player should feel and how player capabilities should expand over time.

Primary Data:
- The `Obj_Player` `GameplayObjectDefinition` and the attribute/resource/tag definitions it composes (Movement Speed, Maximum Health, Current Health, `Actor.Player`). The player owns no bespoke data type — it is authored composition.

Primary Runtime Objects:
- Player Gameplay Object and its composed capability components. Player-specific Unity behavior lives in thin adapters (`ToyChest.Gameplay.Player`) that own no gameplay state.

Published Events:
- None owned by the Player system directly; player actions publish through the systems that own them (Ability, Interaction, Resource, Framework lifecycle).

Consumed Events:
- None required by the Milestone 1 slice; UI/presentation may observe the systems' events.

---

# Milestone 1 Implementation (approved decisions)

The playable player is built **entirely through composition and authored data on the frozen Milestone 0 engine** — there is no player-specific engine code, no `Player.cs` gameplay class. The player is a `GameplayObjectDefinition` (`Obj_Player`) composed of existing capabilities; Unity-facing behavior is thin adapters that read the composed object.

- **Player as data.** `Obj_Player` composes a Movement Speed and a Maximum Health `AttributeDefinition`, the shared bound Current Health `ResourceDefinition`, and an `Actor.Player` identity `TagDefinition`. Inventory, equipment, abilities, and interactions are added the same way — as authored capabilities — in later review groups.
- **Movement ownership.** Movement *speed* is an authored **Attribute** (one source of truth); *locomotion* is a thin `PlayerLocomotion` adapter that reads that attribute off the composed object and drives a `CharacterController`, with the direction math isolated in the pure, deterministic `LocomotionMotor`. Discrete traversal (jump, dash, dodge) is later, **Ability**-driven work.
- **Input.** `PlayerInputBridge` reads the authored `Player.inputactions` (Unity Input System) and forwards intent: the move vector to `PlayerLocomotion`, the interact button to `PlayerInteractor`. It makes no gameplay decisions.
- **Interaction.** `PlayerInteractor` performs only scene-facing proximity discovery, producing a deterministically ordered candidate list routed to the existing `InteractionSystem`; all validation and execution stay in the engine.
- **Camera.** A minimal `ThirdPersonCameraRig` follow establishes the camera foundation; it can be replaced by a Cinemachine rig with no engine impact, since locomotion reads the active camera's facing.
- **Scene composition.** The player is a scene object composed by `GameplayObjectSpawner` (see `Docs/Architecture/PROJECT_ARCHITECTURE.md`, Scene composition), not by bespoke player spawning.
- **Death and respawn (Review Group 7).** Death is not a system: it is the existing **Resource Depleted** transition on Current Health. Respawn is intentionally minimal and manager-free — a pure, unit-tested `PlayerRespawn` helper (the respawn counterpart to `InventoryEquip`) restores a downed player to a clean state using only existing capability operations (refill resources through the Resource System, clear active statuses through the Status Effect System), and a thin `PlayerRespawnController` listens for the health-depleted signal and, deferred one frame so it never mutates the status set mid-tick, restores state and moves the player to an authored spawn-point Transform. A spawn point is just a scene `Transform`; there is no checkpoint system.

---

# Purpose

The player is the center of every gameplay system.

Every interaction with the world occurs through the player.

The objective of this document is to define what controlling the player should feel like rather than prescribing specific implementation details.

Throughout the game, the player should evolve from a capable adventurer into a legendary hero through progression, buildcraft, and mastery.

---

# Player Fantasy

The player is not defined by a character class.

Instead, the player gradually creates their own identity through equipment, companions, movement, abilities, and experimentation.

By endgame, two experienced players should rarely control the same.

The player's identity should emerge naturally from their build rather than from a predetermined archetype.

---

# Core Design Principles

## Responsive

Player input should always feel immediate and reliable.

The player should never question whether the game received an input.

Controls should feel responsive without appearing unnatural.

---

## Accessible

Basic combat should be easy to understand.

Players should quickly feel competent.

The depth of the game comes from combining systems rather than memorizing complex inputs.

---

## Expressive

Progression should expand what the player is capable of doing.

New movement options, new abilities, new companions, and new synergies should continually increase player expression.

---

## Adaptive

Players are encouraged to experiment.

Builds may be modified at any time.

Gameplay continues while menus are open.

Changing equipment during dangerous situations is a strategic risk rather than a restricted action.

---

## Cooperative

The player should always feel valuable during cooperative play.

Different builds should naturally complement one another.

---

# Player Capabilities

The player gradually gains access to a wide variety of capabilities.

## Movement

Core movement includes:

- Walk
- Run
- Sprint
- Jump
- Dodge
- Fall
- Swim (future)
- Climb / Mantle (where appropriate)

Movement should feel responsive and athletic.

The baseline movement should sit between the responsiveness of Fortnite and the weight of modern God of War.

Movement should always prioritize player intent.

---

## Combat

Combat combines two complementary systems.

### Weapon Combat

Weapons provide the player's primary moment-to-moment gameplay.

Combat should emphasize:

- Light attacks
- Heavy attacks
- Simple combo chains
- Sprint attacks
- Dodge attacks
- Air attacks
- Launchers
- Finishers

Execution should be straightforward while allowing room for mastery.

Weapon combat should feel somewhere between God of War and Diablo, emphasizing fluid hack-and-slash action with simple to moderately complex combo chains.

---

### Active Abilities

Abilities provide impactful tactical options.

Abilities should resemble hero abilities found in games such as Marvel Rivals or Overwatch:

- High impact
- Easy to activate
- Visually satisfying
- Distinct tactical purpose

Abilities may include:

- Burst damage
- Area control
- Movement
- Crowd control
- Defense
- Healing
- Utility
- World reactions

The complexity comes from deciding when to use abilities rather than how to execute them.

---

## Exploration

Players explore dangerous regions to:

- Gather resources
- Discover secrets
- Rescue companions
- Complete objectives
- Defeat regional threats
- Liberate regions

Exploration should reward curiosity.

---

## Gathering

Resources should feel integrated into gameplay.

Different resource types support different interaction styles.

Examples:

Small plants:

- Single interaction.

Trees:

- Multiple strikes.
- Fall apart physically.
- Drop resources.

Late-game upgrades may introduce:

- Automatic harvesting.
- Instant harvesting.
- Companion harvesting.
- Larger pickup radius.

Convenience should be earned through progression.

---

## Interaction

The player interacts with the world through a context-sensitive interaction system.

Interactions should always feel predictable.

Examples include:

- Gathering
- Talking
- Opening
- Crafting
- Activating
- Reviving
- Rescuing
- Entering portals

Interaction prompts should be clear without becoming intrusive.

---

# Progression

The player continually unlocks new capabilities.

Examples include:

- New weapons
- New companions
- New movement modules
- Passive ability progression
- Active abilities
- Relics
- Utility modules
- Equipment
- World reactions

Progression should primarily increase player expression rather than simply increasing numerical power.

---

# Movement Progression

Movement evolves throughout the game.

Potential unlocks include:

- Double Jump
- Air Dash
- Grappling Hook
- Glide
- Blink
- Charge Leap
- Ground Slam

Movement upgrades are build choices rather than mandatory progression.

Different players should move through the world differently.

---

# Combat Progression

Combat evolves in several dimensions.

Players gain access to:

- Additional weapon classes
- New combo opportunities
- Stronger abilities
- Better equipment
- Companion synergies
- Relics
- Elemental interactions

The player's increasing effectiveness should come from both stronger equipment and greater system mastery.

---

# Companion Relationship

Players begin the game without companions.

The first companion is earned early through gameplay.

Players gradually collect many companions.

Normally, only one companion accompanies the player.

A second active companion may become available as a late-game progression reward.

Companions fight autonomously, support exploration, and contribute unique synergies to the player's build.

Outside of combat, companions may perform utility actions such as transporting items back to the Hub World for storage, selling unwanted goods, or processing materials.

---

# Build Expression

A player's identity emerges from the combination of:

- Weapon
- Armor
- Companion
- Active Abilities
- Passive Ability Progression
- Movement Modules
- Utility Modules
- Relics
- Elemental Affinity

No single system should define the player's identity.

The combination of systems creates unique playstyles.

---

# Camera

The camera should prioritize readability, situational awareness, and cooperative gameplay.

Inspirations include:

- God of War
- LEGO action games
- Modern third-person action adventures

Lock-on targeting should remain optional and may evolve based on playtesting.

The camera should support both solo and cooperative play without sacrificing clarity.

---

# Input Philosophy

Every action should be intentional.

The player should never feel that controls are fighting their intentions.

The input system should support:

- Input buffering
- Responsive action queues where appropriate
- Predictable action priority
- Forgiving interaction detection

Player agency should always take priority over animation rigidity.

---

# Success Criteria

The Player system is successful when:

- Movement feels satisfying before any progression unlocks.
- Every progression reward expands player possibilities.
- Combat is immediately enjoyable but continues to deepen over time.
- Two endgame players rarely share identical builds.
- Cooperative play naturally rewards complementary builds.
- Players regularly experiment with new combinations.
- The player feels noticeably more capable after each region is liberated.
- By the end of the game, the player feels like a legendary hero while still looking forward to discovering new builds and combinations.

---

# Related Documents

- Docs/Foundations/GAME_VISION.md
- Docs/Foundations/DESIGN_PILLARS.md
- Docs/Foundations/BUILDCRAFT.md
- Docs/Systems/PLAYER_PROGRESSION.md
- Docs/Systems/COMBAT.md
- Docs/Systems/COMPANIONS.md
- Docs/Systems/WORLD_REACTION_SYSTEM.md
- Docs/Systems/ABILITY_SYSTEM.md
- Docs/Systems/EQUIPMENT.md