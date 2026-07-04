# AI Agent Documentation Index

**Status:** Living Specification
**Owner:** Technical Creative Director
**Purpose:** Source-of-truth map for AI coding agents

---

# Purpose

This document tells AI coding agents which project documents are authoritative, what order to read them in, and how to resolve conflicts.

If another document contradicts this index about documentation authority, terminology, or read order, this index wins until it is intentionally updated.

---

# Required Read Order

Every AI implementation session should begin with:

1. `Docs/AI_AGENT_INDEX.md`
2. `Docs/Development/DEVELOPMENT_PLAN.md`
3. `Docs/Architecture/CORE_ARCHITECTURE.md`
4. `Docs/Architecture/ENGINE_PRINCIPLES.md`
5. `Docs/Architecture/AI_CODING_STANDARDS.md`
6. `Docs/Architecture/PROJECT_ARCHITECTURE.md`
7. Task-specific `Docs/Systems/*.md`
8. Relevant `Docs/Foundations/*.md`

Do not begin implementation from chat history, older design drafts, or inferred intent.

---

# Authority Hierarchy

When documents disagree, use this precedence order:

1. `AI_AGENT_INDEX.md` for documentation authority, terminology, and read order.
2. `DEVELOPMENT_PLAN.md` for current work, sprint priority, and next task.
3. Architecture docs for engineering rules, folder layout, and code organization.
4. Systems docs for runtime behavior and implementation boundaries.
5. Foundations docs for creative vision, design pillars, setting, and player experience.
6. Design docs for legacy reference only unless explicitly promoted.
7. `AI_CHAT_HISTORY.md` for archived rationale only.

Architecture docs may constrain implementation details, but they should not override Foundations docs on creative intent or Systems docs on specific system ownership unless the conflict is documented.

---

# Canonical Terminology

Use the following terms consistently:

- **World Reaction System**, not Simulation System.
- **World Properties**, not Simulation Properties.
- **Adventure System**, not Quest System.
- **Abilities**, not Skills, for runtime actions, unlockable actions, and build options.
- **Current Health** is a Resource.
- **Maximum Health** and health regeneration are Attributes.
- **Health System** should not exist as a separate owning system unless a future architecture decision explicitly adds it.

If older docs use deprecated terms, interpret them as follows:

- Simulation System -> World Reaction System
- Simulation Properties -> World Properties
- Quest System -> Adventure System
- Quest Tracker -> Adventure Tracker
- Skills / Skill Trees -> Abilities / Ability progression
- `SKILLS.md` -> `ABILITY_SYSTEM.md` and progression-specific docs
- `SIMULATION.md` -> `WORLD_REACTION_SYSTEM.md`

---

# Canonical Document Roles

## Foundations

`Docs/Foundations` is the canonical home for creative and design foundation documents.

Use it for:

- Game vision
- Design pillars
- Narrative pillars
- Setting
- First hour experience
- Core gameplay loop
- Buildcraft philosophy

## Systems

`Docs/Systems` is the canonical home for feature and runtime system specifications.

Use it for:

- Ability behavior
- Resources and Attributes
- Damage and Combat
- Status Effects and Gameplay Effects
- World Reaction rules
- Adventure, Region, Player, Enemy, Item, Inventory, Crafting, and related systems

## Architecture

`Docs/Architecture` is the canonical home for engineering rules, folder structure, and implementation constraints.

Use it for:

- Core architecture principles
- Engine principles
- AI coding standards
- Project structure
- Event/save/addressable architecture

## Development

`Docs/Development/DEVELOPMENT_PLAN.md` is the operational task list. It answers what should be worked on next.

It does not override canonical terminology in this index.

## Design

`Docs/Design` contains older design material. Treat it as legacy reference unless a file explicitly says it has been promoted to canonical status.

If `Docs/Design` conflicts with `Docs/Foundations`, `Docs/Foundations` wins.

## AI Chat History

`Docs/AI_CHAT_HISTORY.md` is archived rationale only.

It is useful for understanding why decisions changed, but it must not be used as current implementation authority.

---

# Gameplay Object And Gameplay Framework

`Docs/Foundations/GAMEPLAY_OBJECT.md` defines the concept and design role of Gameplay Objects.

`Docs/Systems/GAMEPLAY_FRAMEWORK.md` defines the implementation architecture that supports Gameplay Objects.

Keep both documents, but do not let them redefine each other. If they disagree:

- Use `GAMEPLAY_OBJECT.md` for conceptual intent.
- Use `GAMEPLAY_FRAMEWORK.md` for runtime implementation structure.
- Update the stale document rather than layering compatibility around both meanings.

---

# Health Ownership

Health follows the generic Resource and Attribute model:

- Current Health is a Resource.
- Maximum Health is an Attribute.
- Health regeneration is an Attribute or Resource configuration, depending on the implementation detail.
- Damage and healing modify the Current Health resource through Gameplay Effects.

No separate Health System should own health values.

---

# Implementation Rule

Before implementing a feature, an AI agent should state:

1. The current task from `DEVELOPMENT_PLAN.md`.
2. The authoritative docs it read.
3. The system that owns the behavior.
4. Any detected doc conflict.
5. Whether the conflict blocks implementation.

If a conflict exists, stop and ask for clarification unless this index already resolves it.

---

# Maintenance Rule

When a system is renamed, merged, or deprecated, update:

1. This index.
2. `DEVELOPMENT_PLAN.md`.
3. The relevant Architecture docs.
4. The relevant Systems docs.
5. Any Related Documents sections.

Do not leave old names in high-authority docs unless they are explicitly marked as deprecated aliases.
