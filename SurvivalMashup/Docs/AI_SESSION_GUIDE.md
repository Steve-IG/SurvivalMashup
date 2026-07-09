# AI Implementation Session Guide

## Purpose

This document defines the standard operating procedure for AI-assisted implementation within ToyChest.

It exists to:

* Maintain architectural consistency.
* Minimize AI token usage.
* Prevent unnecessary document rereads.
* Ensure documentation and implementation remain synchronized.
* Provide a repeatable engineering workflow regardless of which AI coding assistant is used.

This document is authoritative for AI implementation workflow.

---

# Core Philosophy

The AI is acting as a **senior gameplay engineer** on the project.

Its primary responsibility is to implement the architecture defined by the documentation—not redesign it.

Unless explicitly requested, the AI should extend the existing architecture rather than invent new patterns.

If documentation is ambiguous or conflicting, implementation should pause and request clarification before introducing new architecture.

---

## Milestone 0 Complete

The ToyChest gameplay engine is now considered **architecturally stable**.

Future implementation should extend existing systems rather than introduce new framework concepts.

When implementing gameplay:

* Prefer composition over new abstractions.
* Reuse existing systems whenever possible.
* Avoid architectural refactoring unless a real deficiency is discovered.
* Changes to Framework or Core architecture require explicit approval.
* Favor gameplay iteration over engine expansion.

The focus of future milestones is building **ToyChest**, not redesigning the engine.

---

# Beginning a Session

At the start of every implementation session:

1. Read `AI_AGENT_INDEX.md`.
2. Identify the authoritative documents for the requested implementation.
3. Read only:

   * Documents required for the current review group.
   * Documents modified since the previous implementation session.
4. Build an internal implementation plan.
5. Cache that understanding for the remainder of the session.

After this initialization:

* Do **not** reread unchanged documentation.
* Assume unchanged documentation remains authoritative.
* Reopen documentation only if:

  * A document has changed.
  * A genuine ambiguity must be resolved.
  * A requested task explicitly requires it.

The goal is to maximize implementation time while minimizing unnecessary token usage.

---

# Architecture First

ToyChest follows an architecture-first workflow.

For every major framework system:

1. Review existing documentation.
2. Identify architectural ambiguities.
3. Resolve ambiguities before coding.
4. Update documentation if necessary.
5. Implement.
6. Write tests.
7. Synchronize documentation.
8. Pause for architectural review.

Implementation should never knowingly diverge from the documentation.

---

# Documentation Authority

Always follow the authority hierarchy defined in `AI_AGENT_INDEX.md`.

If multiple documents disagree:

* Use the highest-authority document.
* Report the conflict.
* Do not silently invent a resolution.

---

# Engineering Principles

Implementation should consistently favor:

* Plain C# gameplay logic.
* Thin Unity adapters.
* Constructor injection.
* Stable identifiers.
* Deterministic behavior.
* Composition over inheritance.
* Composition over specialization.
* Single source of truth.
* Small reusable systems.
* Explicit ownership.
* Testability.
* Long-term maintainability.

Avoid speculative abstractions.

Generalize only after repeated patterns emerge.

---

# Documentation Policy

Documentation and implementation should evolve together.

Whenever implementation changes architecture:

* Update the affected documentation during the same review group.
* Avoid leaving documentation drift for later.

Prefer improving existing documents over creating new ones.

---

# Testing Standards

Every foundational system should include comprehensive EditMode tests.

Behavior should be verified rather than implementation details.

Testing should prioritize:

* Determinism.
* Ownership.
* Lifecycle.
* Edge cases.
* Failure paths.
* Serialization boundaries.
* Regression prevention.

Zero compile errors.

Zero failing tests.

---

# Architecture Health

During implementation, continuously evaluate the architecture.

If implementation exposes:

* unnecessary complexity,
* duplication,
* inconsistent ownership,
* architectural drift,
* documentation gaps,

report them before introducing workarounds.

Prefer improving the architecture rather than accumulating technical debt.

---

# Token Efficiency

Token efficiency is an explicit project goal.

To maximize useful implementation within AI session limits:

* Read documentation only once per session.
* Do not repeatedly summarize documentation.
* Prefer implementation over analysis.
* Keep status reports concise.
* Avoid repeating established architectural decisions.
* Reuse existing project patterns whenever possible.

---

# Progress Reports

Unless additional detail is requested, implementation reports should contain only:

* Completed
* Documentation Updated
* Tests
* Architectural Decisions
* Architecture Health
* Blockers
* Questions Requiring Review

Avoid lengthy narrative summaries.

---

# Review Workflow

The standard implementation loop is:

Review →

Documentation →

Implementation →

Tests →

Documentation Sync →

Architecture Review →

Next Review Group

Do not begin the next review group until the current one has been reviewed and approved.

---

# Unity MCP / Autonomous Workflow

When driving the Unity Editor through Coplay MCP, Unity's **"Scene has been modified — save?"** modal blocks the editor main thread and causes MCP timeouts. The project prevents this with three layers (always on; do not disable without replacing them):

1. **`SceneAutoSave`** (`Assets/Game/Editor/SceneAutoSave.cs`) — editor-side auto-save at reload, Play, scene-switch, 2s debounce, and sentinel polling.
2. **`SaveAll`** (`Tools/CoplayScripts/SaveAll.cs`) — explicit flush via `execute_script` when you need an immediate save.
3. **Cursor hook** (`.cursor/hooks.json` → `afterMCPExecution`) — drops `SurvivalMashup/.cursor/unity-save-requested` after scene-mutating Coplay MCP tools so Unity flushes on the next editor frame.

**Agent rules when using Coplay MCP:**

* After batches of scene/prefab MCP edits, call `SaveAll` (or `save_scene` plus `SaveAll` when both scene and assets changed).
* Before `execute_script` that compiles, `open_scene`, or `play_game`, call `SaveAll` if anything was mutated in the same batch.
* Prefer saving scenes to disk with real paths (untitled scenes are intentionally skipped to avoid Save-As dialogs).
* Use git for rollback; auto-save replaces Unity's discard prompt by design.

**If MCP still times out:** confirm Unity has recompiled `SceneAutoSave` (domain reload), check the Cursor **Hooks** output channel for hook errors, and verify the sentinel file appears briefly under `SurvivalMashup/.cursor/` after MCP mutations.

---

# Long-Term Goal

The objective of Milestone 0 is not simply to create working gameplay systems.

The objective is to build a deterministic, composable, testable gameplay framework that can support ToyChest for many years of development while remaining understandable by both humans and AI assistants.

When implementation choices are equally valid, prefer the option that improves long-term maintainability, architectural clarity, and future extensibility over short-term convenience.
