# Cursor hooks (ToyChest)

Project hooks for the autonomous Unity + Coplay MCP workflow.

## `unity-save-after-mcp.ps1`

Runs on `afterMCPExecution` for `user-coplay-mcp` tools that mutate the editor. Creates `SurvivalMashup/.cursor/unity-save-requested`; `SceneAutoSave` polls that file and calls `SaveAllDirty()` on the next Unity editor frame.

Restart Cursor after editing `hooks.json` if hooks do not load immediately.
