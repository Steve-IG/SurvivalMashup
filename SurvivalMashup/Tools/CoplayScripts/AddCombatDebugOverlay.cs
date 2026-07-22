using ToyChest.Debugging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Places the CombatDebugOverlay in the gameplay scene (it was authored as a script but never added to a
/// GameObject, so F2 had nothing to toggle). Puts it on the same object as the existing GameplayDebugOverlay
/// when there is one, otherwise a dedicated "Debug" object. Idempotent. Editor-only tooling.
/// </summary>
public static class AddCombatDebugOverlay
{
    private const string ScenePath = "Assets/Game/Scenes/VerticalSlice.unity";

    public static string Execute()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        if (Object.FindAnyObjectByType<CombatDebugOverlay>(FindObjectsInactive.Include) != null)
        {
            return "CombatDebugOverlay already present";
        }

        var existing = Object.FindAnyObjectByType<GameplayDebugOverlay>(FindObjectsInactive.Include);
        GameObject host = existing != null ? existing.gameObject : new GameObject("CombatDebug");
        var overlay = host.AddComponent<CombatDebugOverlay>();

        // Visible from the moment you press Play, so the tooling is obviously there; F2 toggles it off.
        var so = new SerializedObject(overlay);
        so.FindProperty("_enabledOnStart").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"CombatDebugOverlay added to '{host.name}' in {scene.name} (enabled on start, F2 toggles)";
    }
}
