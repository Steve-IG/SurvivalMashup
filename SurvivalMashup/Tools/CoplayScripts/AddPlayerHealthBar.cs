using ToyChest.Gameplay.Player;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds the PlayerHealthBar presentation adapter to the Player prefab, alongside the other thin player
/// adapters, so it binds the same composed object's health resource. Idempotent. Editor-only tooling.
/// </summary>
public static class AddPlayerHealthBar
{
    private const string PrefabPath = "Assets/Game/Content/Prefabs/Player.prefab";

    public static string Execute()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            return "ERROR: missing Player prefab";
        }

        try
        {
            if (root.GetComponentInChildren<PlayerHealthBar>(true) != null)
            {
                return "Player already has a PlayerHealthBar";
            }

            root.AddComponent<PlayerHealthBar>();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "PlayerHealthBar added to Player prefab";
    }
}
