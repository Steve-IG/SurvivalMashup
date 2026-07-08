# Systems

**Purpose:** Reusable engine systems. Each system owns its own folder, assembly, runtime code, data definitions, and internal utilities.

**Owner:** Gameplay Systems (per-system ownership documented in `Docs/Systems/*.md`).

**Assemblies:** `ToyChest.Systems.<SystemName>` (one per system).

**May reference:** `ToyChest.Core`, `ToyChest.Framework`, and other systems only when the owning system document permits it and the dependency direction is valid.

**Must never reference:** `ToyChest.Gameplay`, UI, or content.

A system owns one domain (Single Responsibility, One Source of Truth). A system should not expose unnecessary implementation details or reach into another system's internals.
