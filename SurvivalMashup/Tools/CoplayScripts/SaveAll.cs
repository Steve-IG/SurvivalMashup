using ToyChest.Editor;

/// <summary>
/// Explicit flush for the automated Unity workflow: saves every dirty on-disk open scene and every
/// dirty asset through <see cref="SceneAutoSave.SaveAllDirty"/>. Idempotent. Call from Coplay MCP
/// after scene/prefab mutations and before compile-heavy <c>execute_script</c>, <c>open_scene</c>,
/// or <c>play_game</c> when you need an immediate save rather than waiting for the debounce hook.
/// </summary>
public static class SaveAll
{
    public static string Execute()
    {
        return SceneAutoSave.SaveAllDirty();
    }
}
