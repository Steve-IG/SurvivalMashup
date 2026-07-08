# Framework

**Purpose:** The foundational gameplay architecture: Gameplay Object model, capability component contracts, Event Bus, shared modifier stack, lifecycle, and runtime context.

**Owner:** Gameplay Architecture.

**Assembly:** `ToyChest.Framework`

**May reference:** `ToyChest.Core`, Unity engine modules.

**Must never reference:** specific Gameplay Systems (Abilities, Inventory, ...), Gameplay implementations, UI, or content. The Framework must not know about Player, Enemy, Weapon, Adventure, or Companion.

Framework provides the integration layer that lets gameplay systems cooperate through shared object and component contracts. It should rarely change.

See `Docs/Systems/GAMEPLAY_FRAMEWORK.md` and `Docs/Architecture/GAMEPLAY_OBJECT.md`.
