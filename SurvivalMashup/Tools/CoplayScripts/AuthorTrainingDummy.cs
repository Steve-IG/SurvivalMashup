using System.Linq;
using ToyChest.Gameplay.Enemy;
using ToyChest.Gameplay.Objects;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Authors the Training Dummy content: a Gameplay Object definition (a Grunt composition with no attack
/// ability), the Addressables "definitions" label so the registry loads it, a prefab built from the Grunt
/// prefab but authored inert (zero aggro/attack range, no loot) with the TrainingDummy keep-alive adapter,
/// and one instance placed in the VerticalSlice scene near the player spawn. Idempotent. Editor-only
/// tooling — it authors content, it adds no runtime code and no special-case combat path.
/// </summary>
public static class AuthorTrainingDummy
{
    private const string Label = "definitions";
    private const string DefinitionPath = "Assets/Game/Content/Definitions/Obj_TrainingDummy.asset";
    private const string GruntDefinitionPath = "Assets/Game/Content/Definitions/Obj_Grunt.asset";
    private const string PrefabPath = "Assets/Game/Content/Prefabs/TrainingDummy.prefab";
    private const string GruntPrefabPath = "Assets/Game/Content/Prefabs/Grunt.prefab";
    private const string ScenePath = "Assets/Game/Scenes/VerticalSlice.unity";

    public static string Execute()
    {
        string definitionResult = AuthorDefinition();
        string prefabResult = AuthorPrefab();
        string sceneResult = PlaceInScene();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return $"{definitionResult} | {prefabResult} | {sceneResult}";
    }

    private static string AuthorDefinition()
    {
        if (AssetDatabase.LoadAssetAtPath<GameplayObjectDefinition>(DefinitionPath) == null)
        {
            if (!AssetDatabase.CopyAsset(GruntDefinitionPath, DefinitionPath))
            {
                return "ERROR: could not copy Grunt definition";
            }

            AssetDatabase.ImportAsset(DefinitionPath);
        }

        var definition = AssetDatabase.LoadAssetAtPath<GameplayObjectDefinition>(DefinitionPath);
        var so = new SerializedObject(definition);
        so.FindProperty("_definitionId").stringValue = "object.training_dummy";
        so.FindProperty("_displayName").stringValue = "Training Dummy";
        so.FindProperty("_abilities").ClearArray(); // never attacks: it is authored with no abilities
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            return "ERROR: no AddressableAssetSettings";
        }

        if (!settings.GetLabels().Contains(Label))
        {
            settings.AddLabel(Label);
        }

        string guid = AssetDatabase.AssetPathToGUID(DefinitionPath);
        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
        entry.SetLabel(Label, true, true);
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, null, true, true);

        return "definition object.training_dummy authored + labeled";
    }

    private static string AuthorPrefab()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(GruntPrefabPath);
        if (source == null)
        {
            return "ERROR: missing Grunt prefab";
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null && !AssetDatabase.CopyAsset(GruntPrefabPath, PrefabPath))
        {
            return "ERROR: could not copy Grunt prefab";
        }

        AssetDatabase.ImportAsset(PrefabPath);

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            root.name = "TrainingDummy";

            var spawner = root.GetComponentInChildren<GameplayObjectSpawner>();
            if (spawner != null)
            {
                var definition = AssetDatabase.LoadAssetAtPath<GameplayObjectDefinition>(DefinitionPath);
                var so = new SerializedObject(spawner);
                so.FindProperty("_definition").objectReferenceValue = definition;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Authored inert: it never notices the player, so it never pursues and never strikes. The
            // component stays so the dummy keeps every ordinary enemy reaction (hit flash, hit animation).
            var combatant = root.GetComponentInChildren<EnemyCombatant>();
            if (combatant != null)
            {
                var so = new SerializedObject(combatant);
                so.FindProperty("_aggroRange").floatValue = 0f;
                so.FindProperty("_attackRange").floatValue = 0f;
                so.FindProperty("_lootPrefab").objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (root.GetComponentInChildren<TrainingDummy>() == null)
            {
                root.AddComponent<TrainingDummy>();
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return "prefab TrainingDummy authored (inert + keep-alive)";
    }

    private static string PlaceInScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var existing = Object.FindObjectsByType<TrainingDummy>(FindObjectsSortMode.None);
        if (existing.Length > 0)
        {
            return "scene already contains a Training Dummy";
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            return "ERROR: missing TrainingDummy prefab";
        }

        // Place it a few metres in front of the player's spawn so it is the first thing to punch.
        Vector3 position = new Vector3(0f, 0f, 6f);
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            position = player.transform.position + player.transform.forward * 4f;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.transform.position = position;
        instance.name = "TrainingDummy";

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"placed Training Dummy at {position}";
    }
}
