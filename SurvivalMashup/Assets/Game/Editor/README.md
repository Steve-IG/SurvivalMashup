# Editor

**Purpose:** Unity editor extensions: custom inspectors, validation tools, importers, content generators, build tools, and debug viewers.

**Owner:** Tools.

**Assembly:** `ToyChest.Editor` (editor-only platform).

**May reference:** all runtime assemblies.

**Must never be referenced by:** runtime code. Editor code is never included in runtime builds.

**Contents:**
- `SceneAutoSave` — `[InitializeOnLoad]` auto-save for the MCP-driven workflow. Saves dirty on-disk open scenes and dirty assets (`AssetDatabase.SaveAssets`) before domain reload, before/after Play boundaries, before scene switches, on a 2s debounce while work stays dirty, and when a Cursor hook drops `SurvivalMashup/.cursor/unity-save-requested` (polled every editor frame). Exposes `SaveAllDirty()` for explicit Coplay `Tools/CoplayScripts/SaveAll.cs` calls. Always on by project decision (2026-07-08): replaces Unity's discard-unsaved-changes safety net so autonomous runs need zero user intervention. Never runs during Play; never saves untitled scenes (avoids Save-As).
- `AttackAuthoringWindow` (Tools ▸ ToyChest ▸ Attack Authoring) — authors `AttackDefinition` assets: propose windows from a clip, scrub with Scene-view limb preview, set the strike frame. Proposals land as *suggested* (outlined) until a designer ticks *Verified*. Shows a timing summary and a validation panel covering the asset and every combo step that uses it.
- `AttackWindowProposer` — clip analysis behind **Propose**: forward-drive strikes, sweeps (hooks and spins, keyed to the fastest limb at the strike frame), leaps (takeoff and touchdown), and commitment end.
- `AttackValidation` — shared validation and timing-summary rules for attacks, combo steps and their Animator states. Used by the window and by the EditMode content tests (`ToyChest.Tests` references this assembly).
