# Event System

Decoupled communication between systems.

---

## Status

Draft

---

## Goals

- Reduce direct references between unrelated systems
- Keep event payloads small and explicit
- Make subscribe/unsubscribe lifecycle-safe

---

## Approach

TBD — e.g. C# events, ScriptableObject channels, static bus, UniRx.

---

## Event Categories

| Category | Examples | Publishers | Subscribers |
|----------|----------|------------|-------------|
| Gameplay | TBD | TBD | TBD |
| UI | TBD | TBD | TBD |
| Audio | TBD | TBD | TBD |

---

## Event Naming Convention

TBD — e.g. `OnPlayerDamaged`, `PlayerDamagedEvent`.

---

## Payload Guidelines

- Prefer value types or immutable data
- Avoid passing UnityEngine.Object references where lifecycle is unclear
- Document each event in this file as they are added

---

## Event Catalog

| Event | Payload | Published By | Subscribed By |
|-------|---------|--------------|---------------|
| TBD | TBD | TBD | TBD |

---

## Lifecycle

How are listeners registered and cleaned up?

TBD

---

## Open Questions

- TBD

---

## Related Documents

- [System Architecture](02_SYSTEM_ARCHITECTURE.md)
- [Data Flow](03_DATA_FLOW.md)
