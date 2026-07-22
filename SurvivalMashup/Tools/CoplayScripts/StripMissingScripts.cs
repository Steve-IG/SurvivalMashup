using UnityEditor;
using UnityEngine;

/// <summary>
/// Removes leftover missing-script components from the character prefabs after the HitFlash component was
/// deleted. Idempotent. Editor-only tooling.
/// </summary>
public static class StripMissingScripts
{
    private static readonly string[] Prefabs =
    {
        "Assets/Game/Content/Prefabs/Player.prefab",
        "Assets/Game/Content/Prefabs/Grunt.prefab",
        "Assets/Game/Content/Prefabs/TrainingDummy.prefab",
    };

    public static string Execute()
    {
        string report = string.Empty;

        foreach (string path in Prefabs)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                report += $"{path}: MISSING; ";
                continue;
            }

            try
            {
                int removed = 0;
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                }

                if (removed > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }

                report += $"{root.name}: removed {removed}; ";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return report;
    }
}
