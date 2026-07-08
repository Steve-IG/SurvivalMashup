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

- [x] Configure Unity project (Unity 6000.4.9f1, URP, Input System via template)
- [x] Configure URP
- [x] Configure Input System
- [x] Configure Addressables (package 2.6.0; settings at Assets/AddressableAssetsData)
- [x] Configure Assembly Definitions (ToyChest.Core/Framework/Systems.*/Gameplay/Editor/Tests)
- [ ] Configure Git LFS (if required)
- [x] Configure project folders (Assets/Game per PROJECT_ARCHITECTURE.md, ownership READMEs)
- [x] Verify Coplay MCP (compile checks and EditMode test runs working)
- [ ] Configure Cursor Rules

References:

- Docs/Architecture/CORE_ARCHITECTURE.md
- Docs/Architecture/AI_PLAYBOOK.md

---

## Priority 1 — Core Framework

Tasks:

- [x] Event Bus (see Docs/Architecture/EVENT_SYSTEM.md; ToyChest.Framework.Events; 20 EditMode tests)
- [x] Save Framework (see Docs/Architecture/SAVE_SYSTEM.md; ToyChest.Systems.Save; capture/restore through the registry and reconstruction lifecycle)
- [x] Data Registry (see Docs/Architecture/DATA_REGISTRY.md; ToyChest.Framework.Data)
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
- [x] Interaction Framework (ToyChest.Systems.Interactions; interactions route to abilities)
- [x] Resource and Attribute health model (frameworks: ToyChest.Systems.Attributes / .Resources; Current Health binds to Maximum Health)
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

Current Task:

Run the Milestone 0 integration review.

Status: awaiting review approval of the Final Architecture Refinement + Save Framework review group (Construction Before Participation lifecycle + Save Framework).

The next implementer should:

- Read Docs/Development/MILESTONE_0_FOUNDATION.md (Milestone 0 Exit Review section)
- Confirm architecture stability, reusability, data-driven design, documentation currency, and passing tests across every Milestone 0 system before Vertical Slice production begins

Acceptance Criteria:

- Milestone 0 Exit Review checklist satisfied
- No known architectural blockers remain

---

# Recently Completed

- Final Architecture Refinement + Save Framework implemented. Lifecycle refinement: Engine Principle 26 (Construction Before Participation) — gameplay objects are fully constructed and internally consistent before they participate; construction and restoration are event-quiet, activation is the single observable fact. Enforced by a per-object `GameplayObjectEventGate` (closed through construction/restoration, opened at activation, closed again before teardown disposal) rather than "quiet mode" flags, unifying spawning, loading, and future streaming under one lifecycle. The factory gained an `IGameplayObjectReconstructor` (Framework-level) reconstruction path composing persisted objects with their saved id. Save Framework (ToyChest.Systems.Save): `SaveManager` captures authoritative state of every live object through the Gameplay Object Registry and restores by reconstructing through the composition root, restoring authoritative leaf values in dependency order (equipment → statuses → resources → cooldowns → inventory) and activating; only authoritative state is serialized (attributes and tags carry none — derived), definitions referenced by DefinitionId, stable serialization contract with versioning via `UnityEngine.JsonUtility`. New APIs: `ResourceSet.Resources` / `AbilitySet.Abilities` / `StatusEffectSet.ActiveStatuses` enumeration, `InventorySet.RestoreStack`, `ItemInstance.Restore`. Docs/Architecture/SAVE_SYSTEM.md authored and registered in AI_AGENT_INDEX.md; GAMEPLAY_FRAMEWORK.md lifecycle/registry/rehydration sections refined; ENGINE_PRINCIPLES.md Principle 26 added; ENGINE_STARTUP.md updated. Test suite at 257 passing EditMode tests (9 added). No gameplay behavior changed beyond the lifecycle refinement.
- Review Group 7.5 (Architecture Hardening) implemented, ahead of the Save Framework: Persistence Boundary engineering principle (Engine Principle 25: Authoritative / Derived State / Reconstruction Over Serialization) with a Persistence Boundary block added to every core system doc; Gameplay Object Registry (ToyChest.Framework.Objects.GameplayObjectRegistry — plain-C#, deterministic registration-order enumeration, lifecycle-driven membership via Activate/Destroy); event-quiet rehydration APIs (ResourceValue.RestoreCurrent, AbilitySet.RestoreCooldown, StatusEffectSet.Restore + StatusEffectInstance.PeriodAccumulator); deterministic iteration (ResourceSet regeneration and GameplayTagContainer enumeration now registration-ordered); Tag Event Bridge completed (GameplayTagAdded / GameplayTagRemoved published by the Gameplay Tag Container on transitions, category Tag); canonical Engine Startup lifecycle doc (Docs/Architecture/ENGINE_STARTUP.md, registered in AI_AGENT_INDEX.md). No gameplay behavior changed. Test suite at 248 passing EditMode tests (16 added).
- Review Group 7 implemented: Item foundation (ToyChest.Systems.Items: ItemDefinition + Definition Components, ItemInstance with stable ids), Inventory System (ToyChest.Systems.Inventory: slot-based InventorySet, deterministic all-or-nothing stack management, transactional transfer, five inventory events), Equipment System (ToyChest.Systems.Equipment: data-driven slot layouts, EquippableDefinition item component, transactional equip activating tags/attribute modifiers/abilities/statuses through their owning systems), Interaction Framework (ToyChest.Systems.Interactions: interactions route to interactable-owned abilities, priority-based selection, interaction events). AbilityCategory value type introduced; ABILITY_SYSTEM.md refined (deterministic recipes, configuration-only definitions, activation extension points). Milestone 0 sections added to ITEM_SYSTEM.md, INVENTORY_SYSTEM.md, EQUIPMENT.md, INTERACTION_SYSTEM.md.
- Ability Framework implemented (ToyChest.Systems.Abilities): AbilityDefinition/AbilityInstance, AbilitySet capability composed on every object, deterministic activation pipeline (tag gates, all-or-nothing generic resource costs, fixed cooldowns, Self/Provided targeting contract), effects through the Gameplay Effect Runner, six Ability events. ABILITY_SYSTEM.md Milestone 0 section added; Context over Ownership documented in GAMEPLAY_EFFECT_SYSTEM.md; GameplayObjectId event guideline added to EVENT_SYSTEM.md. Test suite at 176 passing EditMode tests.
- Gameplay Object Framework implemented (ToyChest.Framework.Objects + ToyChest.Gameplay.Objects composition root); 128 passing EditMode tests. Runtime Framework Architecture documented in GAMEPLAY_FRAMEWORK.md; Engine Principles 22 (Composition Root) and 23 (Framework System Template) added.
- Shared Modifier Stack (ToyChest.Framework.Modifiers), Attribute System, and Resource System implemented; 105 passing EditMode tests. Resources support attribute-bound maximums with immediate clamp on decrease.
- Hierarchical Gameplay Tags implemented (ToyChest.Systems.Tags); TAG_SYSTEM.md updated to the approved model.
- DATA_REGISTRY.md authored; Data Registry implemented (ToyChest.Framework.Data).
- Addressables settings initialized (Assets/AddressableAssetsData).
- Test suite at 69 passing EditMode tests.
- Milestone 0 Architecture Review completed and approved (July 2026).
- Project structure and assembly definitions established per PROJECT_ARCHITECTURE.md.
- Addressables 2.6.0 installed.
- EVENT_SYSTEM.md authored (canonical event architecture).
- Event System implemented in ToyChest.Framework.Events with 20 passing EditMode tests.
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