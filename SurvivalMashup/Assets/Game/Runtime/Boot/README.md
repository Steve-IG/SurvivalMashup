# Boot

**Purpose:** The application entry point. Owns the runtime startup sequence that brings a session up from process start to the first gameplay tick, per `Docs/Architecture/ENGINE_STARTUP.md`. It connects already-existing systems in the one authoritative order; it holds no gameplay rules and introduces no new framework concepts.

**Owner:** Gameplay Architecture.

**Assembly:** `ToyChest.Boot`

**Contents:**
- `RuntimeBootstrap` — engine-agnostic orchestrator driving startup phases 2–6 (Service Creation, Registry Population, Save Loading, Reconstruction, Runtime Initialization).
- `RuntimeServices` — immutable holder of the game-wide services the bootstrap assembles (not a service locator).
- `BootstrapConfiguration` — the launch inputs (logger, definition sources, save payload).
- `AddressablesDefinitionSource` / `DirectDefinitionSource` — definition sources; Addressables coupling is confined to the former.
- `GameBootstrap` — the thin MonoBehaviour scene entry that supplies production configuration and runs `RuntimeBootstrap`.

**May reference:** `ToyChest.Core`, `ToyChest.Framework`, `ToyChest.Systems.Tags`, `ToyChest.Gameplay`, `ToyChest.Systems.Save`, `Unity.Addressables`, `Unity.ResourceManager`.

**Must never reference:** UI assemblies. Nothing else in the project references `ToyChest.Boot` — it sits at the top of the dependency graph as the composition entry point, so no downward dependency inversion occurs.

**Layer note:** The application bootstrap sits *above* `Core`. `Core` holds small, stable infrastructure (logging, configuration) that lower layers may depend on; the bootstrap that wires the whole stack (Gameplay, Save, Addressables) is the top-level composition root, and lives here rather than in `Core` so the one-directional dependency rule in `Docs/Architecture/CORE_ARCHITECTURE.md` holds.
