# Milestone 1 — Vertical Slice

**Status:** Complete (awaiting certification) — Review Groups 3–8 done. The slice is integrated end-to-end on the canonical scene `Assets/Game/Scenes/VerticalSlice.unity` and validated entirely through the frozen Milestone 0 engine and authored data; **no engine architecture change was required**. Every Success Criterion and Exit Criterion below is met (335 EditMode tests green; clean play-mode boot).

**Purpose**

The purpose of Milestone 1 is to validate the engine, not to perfect the game. If gameplay reveals friction, first solve it through composition, authored data, or existing extension points before proposing engine changes.

Milestone 1 transforms the completed ToyChest engine into the first playable experience.

Milestone 0 established the reusable gameplay framework.

Milestone 1 proves that framework by building a complete gameplay loop entirely through composition, authored content, and existing systems.

No new engine architecture should be introduced during this milestone unless a genuine architectural defect is discovered.

---

# Objectives

Produce a polished, end-to-end playable prototype that demonstrates the core ToyChest experience.

By the end of Milestone 1 the player should be able to:

- Launch the game
- Spawn into a playable world
- Move using responsive controls
- Interact with objects
- Collect items
- Equip items
- Use abilities
- Gain and lose health
- Apply and remove status effects
- Save and reload progress
- Complete a simple gameplay objective

The Vertical Slice is intended to validate both the engine architecture and the gameplay direction.

---

# Philosophy

Milestone 1 is **not** about adding engine features.

Instead it demonstrates that the engine built during Milestone 0 is capable of supporting real gameplay.

Whenever possible:

- extend existing systems
- author data
- compose capabilities

Avoid introducing:

- new framework layers
- parallel systems
- special-case managers
- duplicate gameplay logic

Gameplay should emerge from authored definitions using the existing systems.

---

# Scope

## Player

Implement a fully playable character using the Gameplay Object framework.

The player should be composed entirely through existing capabilities.

Expected capabilities include:

- Attributes
- Resources
- Gameplay Tags
- Status Effects
- Abilities
- Inventory
- Equipment
- Interactions

Player-specific Unity behavior (movement, camera, input) should remain thin adapters over engine systems.

---

## World

Create a small handcrafted environment suitable for demonstrating gameplay.

The world should contain:

- terrain
- obstacles
- interactable objects
- collectible items
- simple hazards

Scene complexity is intentionally limited.

The focus is systems integration, not world size.

---

## Interaction

The player should be able to:

- open containers
- collect loot
- activate interactables
- trigger simple scripted events

Interactions should continue to execute through the Ability System.

---

## Inventory

Demonstrate:

- item collection
- stacking
- inventory capacity
- transferring items
- dropping items (if implemented)

---

## Equipment

Demonstrate:

- equipping
- unequipping
- attribute modification
- gameplay tag grants
- ability grants
- status grants

---

## Abilities

Demonstrate:

- activation
- cooldowns
- resource costs
- gameplay effects
- targeting

At least one player ability and one world interaction ability should exist.

---

## Status Effects

Demonstrate:

- timed effects
- periodic effects
- stacking
- expiration

---

## Saving

Demonstrate:

- save
- load
- reconstruction
- deterministic restoration

The save pipeline created during Milestone 0 should be exercised without modification.

---

# Scene Structure

The project should use the following high-level scene flow:

Bootstrap.unity

↓

VerticalSlice.unity

Bootstrap performs engine initialization only.

Gameplay occurs exclusively inside VerticalSlice.

Future scenes should reuse the same bootstrap path.

---

# Success Criteria

Milestone 1 is complete when a player can:

1. Launch the game.

2. Reach the playable scene.

3. Control the player.

4. Explore the environment.

5. Interact with objects.

6. Pick up items.

7. Equip items.

8. Use abilities.

9. Experience gameplay effects.

10. Save.

11. Quit.

12. Reload into the identical gameplay state.

No engine architecture changes should have been required to achieve this.

---

# Out of Scope

The following belong to later milestones unless required by a demonstrated blocker:

- multiplayer
- procedural generation
- quest systems
- dialogue
- crafting
- building
- skill trees
- advanced AI
- animation systems
- streaming worlds
- live content updates
- online services

---

# AI Implementation Guidance

Milestone 0 is complete.

Treat the engine architecture as stable.

When implementing Milestone 1:

- Prefer composition over inheritance.
- Prefer authored data over code.
- Prefer extending existing systems over creating new ones.
- Do not redesign framework architecture without approval.
- Keep Unity-facing code thin.
- Continue updating documentation as implementation progresses.

When uncertain, assume the existing engine already provides the correct extension point.

---

# Exit Criteria

Milestone 1 is successful if the Vertical Slice demonstrates that ToyChest's gameplay can be built almost entirely through data-driven composition using the Milestone 0 engine.

The milestone is considered an engine validation milestone rather than a content-complete game.