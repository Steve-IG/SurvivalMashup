# Data Flow

How information moves between systems, assets, and persistence.

---

## Status

Draft

---

## Data Categories

| Category | Mutable at Runtime | Persisted | Source |
|----------|-------------------|-----------|--------|
| Config | No | No | ScriptableObjects |
| Runtime state | Yes | Partial | TBD |
| Save data | Yes | Yes | TBD |

---

## Read Path

How does gameplay read configuration?

TBD

---

## Write Path

How does gameplay mutate state?

TBD

---

## Key Data Owners

| Data | Owner System | Consumers |
|------|--------------|-----------|
| Player stats | TBD | TBD |
| Inventory | TBD | TBD |
| World state | TBD | TBD |

---

## Serialization Boundaries

What crosses the save/load boundary?

TBD

---

## Validation

Where is data validated?

TBD

---

## Open Questions

- TBD

---

## Related Documents

- [Save System](04_SAVE_SYSTEM.md)
- [System Architecture](02_SYSTEM_ARCHITECTURE.md)
- [Event System](06_EVENT_SYSTEM.md)
