# Debugging

**Purpose:** Dev-only runtime tooling for inspecting the live gameplay world.

**Assembly:** `ToyChest.Debugging` — a top-of-stack assembly (like `ToyChest.Boot`) referenced by nothing. It sits above every layer so it may inspect all systems, and is kept separate from `ToyChest.Boot` so the production startup assembly carries no debug UI.

**Contents:**
- `GameplayDebugOverlay` — an IMGUI overlay that reads the bootstrapped `RuntimeServices` and walks the `GameplayObjectRegistry`, showing each live object's identity, tags, attributes, resources, abilities, inventory, and interactions. Toggle with F1. It is strictly read-only — it never mutates gameplay — so it is safe to leave enabled.

**Rule:** Everything here observes; nothing here changes gameplay state.
