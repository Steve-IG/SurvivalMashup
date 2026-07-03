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

- CORE_ARCHITECTURE.md
- AI_PLAYBOOK.md

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

- CORE_ARCHITECTURE.md

---

## Priority 2 — First Gameplay

Tasks:

- [ ] Player Controller
- [ ] Third Person Camera
- [ ] Interaction System
- [ ] Health System
- [ ] Damage System

References:

- PLAYER.md
- SIMULATION.md
- COMBAT.md

---

# Current AI Task

This section should contain exactly one active implementation task.

Example:

Current Task:

Implement the Player Controller.

Cursor should:

- Read PLAYER.md
- Read PLAYER_PROGRESSION.md
- Read CORE_ARCHITECTURE.md
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
- Simulation specification completed.
- Combat specification completed.
- Companion specification completed.

---

# Upcoming Design Work

Next documents to complete:

1. BUILDCRAFT.md
2. PLAYER.md
3. SKILLS.md
4. EQUIPMENT.md
5. INVENTORY.md

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

1. Read DEVELOPMENT_PLAN.md
2. Read referenced specifications
3. Read CORE_ARCHITECTURE.md
4. Read AI_CODING_STANDARDS.md
5. Implement only the current task
6. Self-review implementation
7. Update DEVELOPMENT_PLAN.md
8. Stop

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