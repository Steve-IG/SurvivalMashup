using System.Collections.Generic;
using ToyChest.Gameplay.Enemy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the RG5D combat dojo inside the canonical VerticalSlice scene: permanent Training Dummies in the
/// formations the multi-target/combo tests need (a lone target, a close pair, an arc, and staggered
/// distances), a few pursuing Grunts, spawn points, and a visible spawn-pad trigger that spawns a wave of
/// Grunts when the player steps on it. Everything is parented under a single "Arena" root so the build is
/// idempotent — re-running clears and rebuilds it. Editor-only tooling; adds no runtime code.
/// </summary>
public static class BuildCombatArena
{
    private const string ScenePath = "Assets/Game/Scenes/VerticalSlice.unity";
    private const string DummyPrefab = "Assets/Game/Content/Prefabs/TrainingDummy.prefab";
    private const string GruntPrefab = "Assets/Game/Content/Prefabs/Grunt.prefab";
    private const string PadMaterial = "Assets/Game/Content/Materials/M_SpawnPad.mat";

    // Dummy formations (y = 1), each named for the test it supports. The lone dummy already in the scene
    // stays as the "single target directly ahead" station; these add the rest.
    private static readonly (string name, Vector3 pos)[] Dummies =
    {
        // A close pair for the two-target punch test (≈1.2 apart, inside one cone).
        ("Dummy_Pair_L", new Vector3(-2.4f, 1f, 5.5f)),
        ("Dummy_Pair_R", new Vector3(-1.2f, 1f, 5.5f)),
        // A shallow arc for the several-adjacent-targets test.
        ("Dummy_Arc_L", new Vector3(2.0f, 1f, 6.0f)),
        ("Dummy_Arc_C", new Vector3(3.2f, 1f, 6.6f)),
        ("Dummy_Arc_R", new Vector3(4.4f, 1f, 6.0f)),
        // Staggered distances for reach/facing testing.
        ("Dummy_Far_Near", new Vector3(-5.5f, 1f, 6.0f)),
        ("Dummy_Far_Back", new Vector3(-5.5f, 1f, 10.0f)),
    };

    // A few Grunts within pursuit range so the player can fight several at once without the spawn trigger.
    private static readonly (string name, Vector3 pos)[] Grunts =
    {
        ("Grunt_Arena_L", new Vector3(-4f, 1f, 12f)),
        ("Grunt_Arena_C", new Vector3(0f, 1f, 13f)),
        ("Grunt_Arena_R", new Vector3(4f, 1f, 12f)),
    };

    // Where a spawned wave appears.
    private static readonly Vector3[] SpawnPoints =
    {
        new Vector3(-3f, 1f, 16f),
        new Vector3(0f, 1f, 17f),
        new Vector3(3f, 1f, 16f),
    };

    public static string Execute()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        var dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefab);
        var gruntPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GruntPrefab);
        if (dummyPrefab == null || gruntPrefab == null)
        {
            return "ABORT: TrainingDummy or Grunt prefab not found.";
        }

        // Idempotent: clear any previous arena build.
        GameObject existing = GameObject.Find("Arena");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        var root = new GameObject("Arena");
        var dummies = new GameObject("Dummies");
        var grunts = new GameObject("Grunts");
        var points = new GameObject("SpawnPoints");
        dummies.transform.SetParent(root.transform);
        grunts.transform.SetParent(root.transform);
        points.transform.SetParent(root.transform);

        Quaternion facePlayer = Quaternion.Euler(0f, 180f, 0f);

        foreach ((string name, Vector3 pos) in Dummies)
        {
            GameObject d = Place(dummyPrefab, name, pos, facePlayer, dummies.transform);
        }

        foreach ((string name, Vector3 pos) in Grunts)
        {
            Place(gruntPrefab, name, pos, facePlayer, grunts.transform);
        }

        var spawnTransforms = new List<Transform>();
        for (int i = 0; i < SpawnPoints.Length; i++)
        {
            var p = new GameObject("SpawnPoint_" + i);
            p.transform.SetParent(points.transform);
            p.transform.position = SpawnPoints[i];
            spawnTransforms.Add(p.transform);
        }

        BuildSpawnPad(root.transform, gruntPrefab, spawnTransforms);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Combat arena built: " + Dummies.Length + " formation dummies, " + Grunts.Length +
               " arena grunts, spawn pad with " + SpawnPoints.Length + " points. Scene saved.";
    }

    private static GameObject Place(GameObject prefab, string name, Vector3 pos, Quaternion rot, Transform parent)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.SetPositionAndRotation(pos, rot);
        return go;
    }

    private static void BuildSpawnPad(Transform parent, GameObject gruntPrefab, List<Transform> spawnPoints)
    {
        // A flat, clearly coloured floor pad the player walks onto. Its collider is a trigger so it never
        // blocks movement; stepping on it fires one wave (the EnemySpawnTrigger re-arms on exit).
        var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = "EnemySpawnPad";
        pad.transform.SetParent(parent);
        pad.transform.position = new Vector3(6f, 0.15f, 3f);
        pad.transform.localScale = new Vector3(2f, 0.3f, 2f);

        var collider = pad.GetComponent<BoxCollider>();
        collider.isTrigger = true;

        var renderer = pad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = LoadOrCreatePadMaterial();

        var trigger = pad.AddComponent<EnemySpawnTrigger>();
        var so = new SerializedObject(trigger);
        so.FindProperty("_playerTag").stringValue = "Player";
        so.FindProperty("_enemyPrefab").objectReferenceValue = gruntPrefab;
        so.FindProperty("_rearmSeconds").floatValue = 1.5f;
        SerializedProperty pts = so.FindProperty("_spawnPoints");
        pts.arraySize = spawnPoints.Count;
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            pts.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Material LoadOrCreatePadMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(PadMaterial);
        if (existing != null)
        {
            return existing;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(shader) { name = "M_SpawnPad" };
        var color = new Color(1f, 0.55f, 0.1f); // a bright orange "spawn here" pad
        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }

        mat.color = color;
        System.IO.Directory.CreateDirectory("Assets/Game/Content/Materials");
        AssetDatabase.CreateAsset(mat, PadMaterial);
        return mat;
    }
}
