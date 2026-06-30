# Architecture Overview

High-level technical architecture for SurvivalMashup.

---

## Status

Draft

---

## Purpose

This folder documents how the game is structured in code and data — not what the game is (see [Design](../Design/00_OVERVIEW.md)).

---

## Tech Stack

| Layer | Choice |
|-------|--------|
| Engine | Unity 6 |
| Language | C# |
| Rendering | URP |
| Input | Unity Input System |
| Content loading | Addressables (planned) |
| IDE / AI | Cursor + Coplay MCP |

---

## Architectural Goals

1. Small, focused systems with clear boundaries
2. Data-driven configuration via ScriptableObjects
3. Explicit dependencies — no hidden singletons where avoidable
4. Testable logic separated from Unity lifecycle glue
5. Documentation updated alongside implementation

---

## Document Map

| Doc | Topic |
|-----|-------|
| [Game Loop](01_GAME_LOOP.md) | Update order, lifecycle, frame flow |
| [System Architecture](02_SYSTEM_ARCHITECTURE.md) | Major systems and responsibilities |
| [Data Flow](03_DATA_FLOW.md) | How data moves between systems |
| [Save System](04_SAVE_SYSTEM.md) | Persistence and serialization |
| [Scene Management](05_SCENE_MANAGEMENT.md) | Scenes, loading, transitions |
| [Event System](06_EVENT_SYSTEM.md) | Decoupled communication |
| [Addressables](07_ADDRESSABLES.md) | Runtime asset loading strategy |

---

## Related Documents

- [Engineering Principles](../AI/02_ENGINEERING_PRINCIPLES.md)
- [Coding Standards](../AI/03_CODING_STANDARDS.md)
- [Decision Log](../AI/04_DECISION_LOG.md)
