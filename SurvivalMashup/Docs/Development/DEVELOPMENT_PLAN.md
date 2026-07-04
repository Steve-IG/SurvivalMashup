# Development Plan

**Status:** Living Document
**Version:** 1.0
**Owner:** Technical Creative Director
**Last Updated:** June 2026

---

# Purpose

This document is the operational center of the project.

It answers one question:

> What should the team (human and AI) work on next?

Every implementation task should originate from this document.

Every completed task should update this document.

Development should proceed in small, testable, vertical slices that continuously improve a playable build.

Documentation authority, terminology, and AI read order are defined in `Docs/AI_AGENT_INDEX.md`.

---

# Current Phase

## Phase

🟢 Milestone 0 — Foundation

Current Goal:

Establish a production-quality Unity project that AI can safely extend for years.

Current Focus:

Project architecture and core gameplay framework.

Success Criteria:

- Stable architecture
- AI workflow established
- Documentation complete
- Unity project configured
- First playable framework ready

---

# Active Sprint

## Sprint Goal

Complete the technical foundation required to begin gameplay implementation.

---

## Priority 0 — Project Setup

Status: In Progress

Tasks:

- [ ] Configure Unity project
- [ ] Configure URP
- [ ] Configure Input System
- [ ] Configure Addressables
- [ ] Configure Assembly Definitions
- [ ] Configure Git LFS (if required)
- [ ] Configure project folders
- [ ] Verify Coplay MCP
- [ ] Configure Cursor Rules

References:

- Docs/Architecture/CORE_ARCHITECTURE.md
- Docs/Architecture/AI_PLAYBOOK.md

---

## Priority 1 — Core Framework

Tasks:

- [ ] Event Bus
- [ ] Save Framework
- [ ] Data Registry
- [ ] Scene Loading
- [ ] Bootstrap Scene
- [ ] Service Initialization

References:

- Docs/Architecture/CORE_ARCHITECTURE.md

---

## Priority 2 — First Gameplay

Tasks:

- [ ] Player Controller
- [ ] Third Person Camera
- [ ] Interaction System
- [ ] Resource and Attribute health model
- [ ] Damage System

References:

- Docs/Systems/PLAYER.md
- Docs/Systems/RESOURCE_SYSTEM.md
- Docs/Systems/ATTRIBUTE_SYSTEM.md
- Docs/Systems/WORLD_REACTION_SYSTEM.md
- Docs/Systems/COMBAT.md

---

# Current AI Task

This section should contain exactly one active implementation task.

Example:

Current Task:

Implement the Player Controller.

Cursor should:

- Read Docs/Systems/PLAYER.md
- Read Docs/Systems/PLAYER_PROGRESSION.md
- Read Docs/Architecture/CORE_ARCHITECTURE.md
- Implement movement only
- Do not implement combat
- Do not implement inventory
- Do not implement skills

Acceptance Criteria:

- Smooth movement
- Jump
- Dodge
- Sprint
- Interaction raycast
- Unit tested where practical

When complete:

Update this document.

Move the next task into Current AI Task.

---

# Recently Completed

- Documentation repository created.
- Core Architecture completed.
- World Reaction System specification completed.
- Combat specification completed.
- Companion specification completed.

---

# Upcoming Documentation Work

Current documentation focus:

1. Keep `Docs/AI_AGENT_INDEX.md` current.
2. Keep system ownership sections current.
3. Add new documents only when a real ownership gap appears.
4. Prefer updating existing canonical docs over creating parallel docs.

---

# Technical Debt

None.

Future technical debt should be intentionally recorded here rather than forgotten.

---

# Open Design Questions

Maintain a short list of unresolved design decisions.

Examples:

- Final movement slot count
- Number of weapon classes
- Companion evolution model
- Procedural region generation algorithm

---

# Definition of Done

A task is complete only when:

- Architecture guidelines followed.
- Relevant documentation updated.
- Feature tested.
- No unnecessary coupling introduced.
- AI Coding Standards followed.
- Cursor session summarized.
- Next task identified.

---

# AI Workflow

Every AI implementation session should follow this workflow:

1. Read AI_AGENT_INDEX.md
2. Read DEVELOPMENT_PLAN.md
3. Read referenced specifications
4. Read CORE_ARCHITECTURE.md
5. Read ENGINE_PRINCIPLES.md
6. Read CODING_PRINCIPLES.md
7. Read AI_CODING_STANDARDS.md
8. Implement only the current task
9. Self-review implementation
10. Update DEVELOPMENT_PLAN.md
11. Stop

Never begin a second feature without updating the plan.

---

# Project Health

Architecture:
🟢 Excellent

Documentation:
🟢 Excellent

Gameplay:
⚪ Not Started

Technical Debt:
🟢 None

AI Context Quality:
🟢 Excellent

Playable Build:
⚪ Not Started