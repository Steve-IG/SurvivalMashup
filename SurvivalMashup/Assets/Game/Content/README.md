# Content

**Purpose:** Immutable gameplay definitions — ScriptableObject assets and other authoring data (Abilities, Items, Tags, Attributes, Resources, StatusEffects, ...).

**Owner:** Design, via the owning system's definition types.

**Rules:**

- Content is data, never code.
- Definitions are immutable at runtime; runtime state never lives in ScriptableObjects.
- Content is loaded through the Data Registry / Addressables, never through `Resources/`.
- Organize by definition type (`Abilities/`, `Items/`, `Tags/`, ...), mirroring `Docs/Architecture/PROJECT_ARCHITECTURE.md`.
