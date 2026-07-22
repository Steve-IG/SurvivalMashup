using ToyChest.Gameplay.HitDetection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Puts the character-agnostic AttackContactRelay on every attacking character's model (the Animator's
/// GameObject, where Unity dispatches animation events), so one authored OnAttackContact event works on
/// the player, the Grunt, and the Training Dummy alike instead of only the player. Idempotent.
/// Editor-only tooling.
/// </summary>
public static class AddAttackContactRelays
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
                var animator = root.GetComponentInChildren<Animator>(true);
                if (animator == null)
                {
                    report += $"{root.name}: no Animator; ";
                    continue;
                }

                var receiver = root.GetComponentInChildren<IAttackContactReceiver>(true);
                if (receiver == null)
                {
                    report += $"{root.name}: no IAttackContactReceiver (skipped); ";
                    continue;
                }

                if (animator.GetComponent<AttackContactRelay>() == null)
                {
                    animator.gameObject.AddComponent<AttackContactRelay>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    report += $"{root.name}: relay added to '{animator.name}'; ";
                }
                else
                {
                    report += $"{root.name}: already had relay; ";
                }
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
