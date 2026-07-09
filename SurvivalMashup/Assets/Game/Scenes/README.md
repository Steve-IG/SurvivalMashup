# Scenes

**Purpose:** First-party ToyChest Unity scenes.

**Owner:** Gameplay Architecture.

**Contents:**
- `Bootstrap.unity` — the application entry scene (`Docs/Architecture/ENGINE_STARTUP.md`, phase 1). Holds a single `Bootstrap` GameObject carrying the `GameBootstrap` component (`ToyChest.Boot`), which initializes every engine service on launch, and the read-only `GameplayDebugOverlay` (`ToyChest.Debugging`). `GameBootstrap` also spawns the authored startup content (currently the Wooden Crate, `object.wooden_crate`) through the composition root on Play. It is the first scene in Build Settings.
- `Hub.unity` — placeholder for the hub, to be built when its gameplay systems exist.
- `MissionPrototype.unity` — placeholder for the mission prototype, likewise deferred.

Only `Bootstrap.unity` is functional today; the other two exist to formalize the scene layout.

**Note:** First-party scenes live here, under `Assets/Game/`, separate from the third-party sample scenes under `Assets/Scenes/`. `Docs/Architecture/PROJECT_ARCHITECTURE.md` names `Assets/Game/Scenes` as the canonical scenes home.
