# Project Roadmap

**Status:** Living Specification
**Version:** 1.0
**Owner:** Creative Director
**Last Updated:** June 2026

---

# Purpose

This document defines the long-term execution strategy for the project.

It answers one question:

> What should we build next?

This roadmap exists to ensure that development proceeds in a deliberate, iterative manner that continuously produces playable, testable, and enjoyable builds.

Every feature, task, and milestone should support the next playable version of the game.

---

# Development Philosophy

We are not building every system independently.

We are continuously building a better game.

Each milestone should result in a playable experience.

The game should remain playable throughout development.

---

# Guiding Principles

- Build vertical slices.
- Validate fun before adding content.
- Complete systems before expanding systems.
- Finish features rather than starting new ones.
- Prefer iteration over perfection.
- Build architecture before complexity.
- Let design drive engineering.

---

# Milestone 0 — Foundation

**Objective**

Create a production-quality technical foundation.

### Deliverables

- Unity project configured
- Git repository
- Cursor configured
- Coplay MCP operational
- Documentation repository
- Core Architecture
- AI Playbook
- Coding Standards
- CI/CD pipeline (basic)
- Assembly Definitions
- Addressables configured
- Save System skeleton
- Event System skeleton
- Data model conventions

### Success Criteria

A clean, scalable project that AI can safely extend.

---

# Milestone 1 — The First Fun

**Objective**

Prove the core gameplay loop is enjoyable.

### Scope

One Hub World.

One handcrafted region.

One weapon.

One companion.

One enemy faction.

One elite encounter.

One regional objective.

Basic gathering.

Basic crafting.

Simple progression.

Return to Hub.

### Success Criteria

Players should enjoy repeating the loop even with minimal content.

If this milestone is not fun, we revisit the design before expanding.

---

# Milestone 2 — Vertical Slice

**Objective**

Create a polished 30–60 minute gameplay experience representing the final game's quality.

### Scope

- Improved combat
- Companion progression
- Region liberation
- NPC restoration
- Multiple enemy types
- Crafting depth
- Better loot
- Save/load
- UI polish
- Audio
- VFX
- Basic multiplayer

### Success Criteria

The slice should convincingly demonstrate the vision of the final game.

---

# Milestone 3 — Core Systems Complete

Complete production-ready versions of all major gameplay systems.

Including:

- Combat
- Progression
- Inventory
- Equipment
- Crafting
- Skills
- Companions
- Regions
- Hub World
- Simulation
- Quests
- Building foundations

---

# Milestone 4 — Content Production

Shift emphasis from engineering toward content creation.

Primary focus:

- New regions
- New companions
- Weapons
- Skills
- Resources
- Enemies
- Bosses
- Quests

The architecture should remain largely stable during this phase.

---

# Milestone 5 — Alpha

The entire gameplay loop exists.

Focus shifts toward:

- Balancing
- Bug fixing
- Performance
- Multiplayer polish
- Content completeness

---

# Milestone 6 — Beta

Content complete.

Primary focus:

- Polish
- Optimization
- Accessibility
- Tutorials
- User experience
- Platform testing

---

# Milestone 7 — Launch

Release Version 1.0.

Post-launch development begins immediately.

---

# Post Launch

The architecture is intentionally designed to support years of expansion.

Potential additions include:

- New handcrafted regions
- Seasonal regions
- New companions
- Additional weapon classes
- New damage properties
- New simulation interactions
- Community events

The game should grow through content rather than architectural rewrites.

---

# Definition of Done

A feature is considered complete when:

- Gameplay is fun.
- Documentation is updated.
- Tests pass.
- AI coding standards are followed.
- Architecture principles are respected.
- Performance targets are met.
- Save/load functions correctly.
- Multiplayer compatibility has been considered.

---

# Development Order

Major systems should generally be implemented in the following order:

1. Core Framework
2. Player
3. Camera
4. Input
5. Interaction System
6. Combat
7. Enemy AI
8. Companion Framework
9. Inventory
10. Equipment
11. Loot
12. Crafting
13. Progression
14. Regions
15. Hub World
16. Quests
17. Multiplayer
18. Procedural Generation

Each system should be production-ready before introducing major dependencies.

---

# AI Development Strategy

AI agents should work in small, focused tasks.

Each task should:

- Have a clear objective.
- Modify as few systems as possible.
- Reference the relevant design documents.
- Include acceptance criteria.
- Preserve architectural integrity.

Avoid large, multi-system implementation requests.

---

# Success Metrics

The project is successful if:

- The game remains playable throughout development.
- New features become easier to add over time.
- Designers create most new content without engineering support.
- AI consistently extends existing systems instead of rewriting them.
- The architecture scales to years of post-launch content.