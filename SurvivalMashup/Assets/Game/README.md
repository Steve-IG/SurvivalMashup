# Game

**Purpose:** All first-party ToyChest gameplay code and content.

**Owner:** ToyChest team.

**Structure:**

- `Runtime/` — production gameplay code (Core, Framework, Systems, Gameplay).
- `Content/` — immutable gameplay definitions (ScriptableObject assets).
- `Editor/` — Unity editor extensions; never included in runtime builds.
- `Tests/` — automated tests, organized by system ownership.

**Dependency rule:** Gameplay → Systems → Framework → Core. Lower layers must never reference higher layers.

See `Docs/Architecture/PROJECT_ARCHITECTURE.md` for the canonical layout.
