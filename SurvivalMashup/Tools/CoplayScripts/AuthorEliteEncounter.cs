using System.Linq;
using ToyChest.Gameplay.Enemy;
using ToyChest.Gameplay.Objects;
using ToyChest.Framework.Objects;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Authors Review Group C — the Grunt Brute elite encounter. Idempotent; run with the Editor in edit mode
/// (run_script --entry AuthorEliteEncounter.Execute). Editor-only tooling: it authors content, adds no runtime code.
/// <list type="bullet">
/// <item><b>Definitions</b> (each copied from the nearest existing asset, then edited, and given the Addressables
/// "definitions" label): the elite (<c>object.grunt_brute</c> — the Grunt composition plus <c>ability.brute_slam</c>),
/// its innate <c>status.elite_brute</c> (+250 Maximum Health, −0.3 Enemy Speed, +30 Stability, <c>Actor.Elite</c>, refill on apply),
/// the slam's damage, and the encounter object (<c>object.elite_encounter_brute</c>: a one-shot
/// <c>resource.encounter_charge</c> plus <c>ability.elite_reward_brute</c>, whose cost is that charge and whose effect
/// adds <c>item.brute_horn</c> — the Supply Crate's loot pattern).</item>
/// <item><b>Prefab</b> <c>GruntBrute.prefab</c>, a variant of <c>Grunt.prefab</c>: bigger, slower to turn, with the
/// signature-attack fields, the innate status, and an <see cref="EnemyAreaTelegraph"/>.</item>
/// <item><b>Animator</b>: a <c>Slam</c> trigger and state on the shared EnemyAnimator, playing the Combo02_3 hammer slam
/// slowed so its fist-landing frame (nt 0.396) coincides with the end of the 1.2 s wind-up. Grunts never fire it.</item>
/// <item><b>Scene</b>: the encounter in the empty west of VerticalSlice's dojo — a challenge pad, an arena floor ring,
/// and the elite / escort / reinforcement spawn points.</item>
/// </list>
/// </summary>
public static class AuthorEliteEncounter
{
    private const string Label = "definitions";
    private const string Defs = "Assets/Game/Content/Definitions/";
    private const string GruntPrefabPath = "Assets/Game/Content/Prefabs/Grunt.prefab";
    private const string BrutePrefabPath = "Assets/Game/Content/Prefabs/GruntBrute.prefab";
    private const string ControllerPath = "Assets/Game/Content/Animation/EnemyAnimator.controller";
    private const string SlamClipPath = "Assets/Frank_Fighter/Assets/Animations/FBX Animation/Root_Motion/Frank_RPG_Fighter_Combo02_3.FBX";
    private const string BurstPath = "Assets/Epic Toon FX/Prefabs/Combat/Brawling/Toon/ToonBodySlam.prefab";
    private const string PadMaterialSource = "Assets/Game/Content/Materials/M_SpawnPad.mat";
    private const string PadMaterialPath = "Assets/Game/Content/Materials/M_ElitePad.mat";
    private const string RingMaterialPath = "Assets/Game/Content/Materials/M_EliteArena.mat";
    private const string ScenePath = "Assets/Game/Scenes/VerticalSlice.unity";

    private const float SlamWindUp = 1.2f;
    private const float SlamLandingTime = 0.396f; // normalized: the fist is on the ground (measured in RG B)

    public static string Execute()
    {
        var log = new System.Text.StringBuilder();
        AuthorDefinitions(log);
        AuthorAnimator(log);
        AuthorPrefab(log);
        AuthorScene(log);
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    // ------------------------------------------------------------------ definitions

    private static void AuthorDefinitions(System.Text.StringBuilder log)
    {
        var eliteTag = Copy("Tag_Actor_Enemy", "Tag_Actor_Elite", so =>
        {
            so.FindProperty("_tagPath").stringValue = "Actor.Elite";
            so.FindProperty("_description").stringValue = "Marks an enemy as an elite: a distinctive, tougher variant that anchors an encounter.";
        });

        var refill = Copy("Fx_Heal_SecondWind", "Fx_Heal_EliteBrute", so =>
        {
            so.FindProperty("_definitionId").stringValue = "fx.heal.elite_brute";
            so.FindProperty("_amount").floatValue = 1000f; // clamped at the boosted maximum: a full refill
        });

        var status = Copy("Status_LuckyRegen", "Status_EliteBrute", so =>
        {
            so.FindProperty("_definitionId").stringValue = "status.elite_brute";
            so.FindProperty("_displayName").stringValue = "Elite: Grunt Brute";
            so.FindProperty("_durationType").enumValueIndex = 1; // Infinite
            so.FindProperty("_periodSeconds").floatValue = 0f;
            so.FindProperty("_periodicEffects").ClearArray();
            SetList(so.FindProperty("_grantedTags"), eliteTag);
            SetList(so.FindProperty("_onApplyEffects"), refill);
            SerializedProperty modifiers = so.FindProperty("_modifiers");
            modifiers.ClearArray();
            AddModifier(modifiers, "attribute.max_health", 250f);
            AddModifier(modifiers, "attribute.enemy_speed", -0.3f);
            AddModifier(modifiers, "attribute.stability", 30f);
        });

        var slamDamage = Copy("Fx_Damage_EnemyStrike", "Fx_Damage_BruteSlam", so =>
        {
            so.FindProperty("_definitionId").stringValue = "fx.damage.brute_slam";
            so.FindProperty("_amount").floatValue = 22f;
        });

        var slam = Copy("Ability_EnemyStrike", "Ability_BruteSlam", so =>
        {
            so.FindProperty("_definitionId").stringValue = "ability.brute_slam";
            so.FindProperty("_displayName").stringValue = "Shockwave Slam";
            so.FindProperty("_cooldownSeconds").floatValue = 0f; // cadence is the signature attack's own cooldown
            SetList(so.FindProperty("_effects"), slamDamage);
        });

        var strike = AssetDatabase.LoadAssetAtPath<ScriptableObject>(Defs + "Ability_EnemyStrike.asset");
        Copy("Obj_Grunt", "Obj_GruntBrute", so =>
        {
            so.FindProperty("_definitionId").stringValue = "object.grunt_brute";
            so.FindProperty("_displayName").stringValue = "Grunt Brute";
            SetList(so.FindProperty("_abilities"), strike, slam);
        });

        var charge = Copy("Res_LootCharge", "Res_EncounterCharge", so =>
        {
            so.FindProperty("_definitionId").stringValue = "resource.encounter_charge";
            so.FindProperty("_displayName").stringValue = "Encounter Charge";
        });

        var horn = Copy("Item_MonsterFang", "Item_BruteHorn", so =>
        {
            so.FindProperty("_definitionId").stringValue = "item.brute_horn";
            so.FindProperty("_displayName").stringValue = "Brute's Horn";
        });

        var addHorn = Copy("Fx_AddItem_ScrapMetal", "Fx_AddItem_BruteHorn", so =>
        {
            so.FindProperty("_definitionId").stringValue = "effect.reward.brute_horn";
            so.FindProperty("_item").objectReferenceValue = horn;
            so.FindProperty("_quantity").intValue = 1;
        });

        var reward = Copy("Ability_LootSupplyCrate", "Ability_EliteRewardBrute", so =>
        {
            so.FindProperty("_definitionId").stringValue = "ability.elite_reward_brute";
            so.FindProperty("_displayName").stringValue = "Elite Reward: Grunt Brute";
            SerializedProperty costs = so.FindProperty("_costs");
            costs.ClearArray();
            costs.InsertArrayElementAtIndex(0);
            costs.GetArrayElementAtIndex(0).FindPropertyRelative("_resourceId").stringValue = "resource.encounter_charge";
            costs.GetArrayElementAtIndex(0).FindPropertyRelative("_amount").floatValue = 1f;
            SetList(so.FindProperty("_effects"), addHorn);
        });

        Copy("Obj_SupplyCrate", "Obj_EliteEncounter", so =>
        {
            so.FindProperty("_definitionId").stringValue = "object.elite_encounter_brute";
            so.FindProperty("_displayName").stringValue = "Elite Encounter: Grunt Brute";
            SetList(so.FindProperty("_resources"), charge);
            so.FindProperty("_initialTags").ClearArray();
            SetList(so.FindProperty("_abilities"), reward);
            so.FindProperty("_interactions").ClearArray();
        });

        log.Append("definitions authored + labeled (status, slam, brute, charge, horn, reward, encounter) | ");
    }

    private static ScriptableObject Copy(string source, string target, System.Action<SerializedObject> edit)
    {
        string sourcePath = Defs + source + ".asset";
        string targetPath = Defs + target + ".asset";
        if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(targetPath) == null)
        {
            if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
            {
                throw new System.InvalidOperationException($"could not copy {sourcePath} → {targetPath}");
            }

            AssetDatabase.ImportAsset(targetPath);
        }

        var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(targetPath);
        var so = new SerializedObject(asset);
        edit(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        AddDefinitionsLabel(targetPath);
        return asset;
    }

    private static void AddDefinitionsLabel(string path)
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (!settings.GetLabels().Contains(Label))
        {
            settings.AddLabel(Label);
        }

        AddressableAssetEntry entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), settings.DefaultGroup);
        entry.SetLabel(Label, true, true);
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, null, true, true);
    }

    private static void SetList(SerializedProperty list, params Object[] items)
    {
        list.ClearArray();
        for (int i = 0; i < items.Length; i++)
        {
            list.InsertArrayElementAtIndex(i);
            list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }
    }

    private static void AddModifier(SerializedProperty modifiers, string attributeId, float value)
    {
        int i = modifiers.arraySize;
        modifiers.InsertArrayElementAtIndex(i);
        SerializedProperty m = modifiers.GetArrayElementAtIndex(i);
        m.FindPropertyRelative("_attributeId").stringValue = attributeId;
        m.FindPropertyRelative("_operation").enumValueIndex = 0; // Flat
        m.FindPropertyRelative("_value").floatValue = value;
    }

    // ------------------------------------------------------------------ animator

    private static void AuthorAnimator(System.Text.StringBuilder log)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var clip = AssetDatabase.LoadAllAssetsAtPath(SlamClipPath).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
        if (controller.parameters.All(p => p.name != "Slam"))
        {
            controller.AddParameter("Slam", AnimatorControllerParameterType.Trigger);
        }

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState slam = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Slam");
        AnimatorState locomotion = machine.states.Select(s => s.state).First(s => s.name == "Locomotion");
        if (slam == null)
        {
            slam = machine.AddState("Slam", new Vector3(340f, 260f, 0f));
            AnimatorStateTransition enter = machine.AddAnyStateTransition(slam);
            enter.AddCondition(AnimatorConditionMode.If, 0f, "Slam");
            enter.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            enter.hasExitTime = false;
            enter.duration = 0.06f;
            enter.canTransitionToSelf = false;

            AnimatorStateTransition exit = slam.AddTransition(locomotion);
            exit.hasExitTime = true;
            exit.exitTime = 0.95f;
            exit.duration = 0.15f;
        }

        slam.motion = clip;

        // Slowed so the fist lands exactly when the wind-up completes: the slow raise is the telegraph.
        slam.speed = SlamLandingTime * clip.length / SlamWindUp;
        EditorUtility.SetDirty(controller);
        log.Append($"animator Slam state: clip {clip.name} {clip.length:0.000}s, speed {slam.speed:0.000} | ");
    }

    // ------------------------------------------------------------------ prefab

    private static void AuthorPrefab(System.Text.StringBuilder log)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BrutePrefabPath) == null)
        {
            var grunt = AssetDatabase.LoadAssetAtPath<GameObject>(GruntPrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(grunt);
            PrefabUtility.SaveAsPrefabAsset(instance, BrutePrefabPath); // saved from an instance = a prefab variant
            Object.DestroyImmediate(instance);
        }

        GameObject root = PrefabUtility.LoadPrefabContents(BrutePrefabPath);
        try
        {
            root.name = "GruntBrute";
            root.transform.localScale = Vector3.one * 1.3f;

            var spawner = new SerializedObject(root.GetComponent<GameplayObjectSpawner>());
            spawner.FindProperty("_definition").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameplayObjectDefinition>(Defs + "Obj_GruntBrute.asset");
            spawner.ApplyModifiedPropertiesWithoutUndo();

            var combatant = new SerializedObject(root.GetComponent<EnemyCombatant>());
            combatant.FindProperty("_attackRange").floatValue = 2.2f;
            combatant.FindProperty("_windUpSeconds").floatValue = 0.6f;
            combatant.FindProperty("_recoverSeconds").floatValue = 0.8f;
            combatant.FindProperty("_turnSpeedDegrees").floatValue = 300f;
            combatant.FindProperty("_deathDelay").floatValue = 2f;
            combatant.FindProperty("_signatureAbilityId").stringValue = "ability.brute_slam";
            combatant.FindProperty("_signatureRadius").floatValue = 3.5f;
            combatant.FindProperty("_signatureTriggerRange").floatValue = 4.5f;
            combatant.FindProperty("_signatureWindUpSeconds").floatValue = SlamWindUp;
            combatant.FindProperty("_signatureRecoverSeconds").floatValue = 1.6f;
            combatant.FindProperty("_signatureCooldownSeconds").floatValue = 5f;
            combatant.FindProperty("_signatureInitialDelaySeconds").floatValue = 1.5f;
            combatant.FindProperty("_signatureWindUpInterruptible").boolValue = false;
            SetList(combatant.FindProperty("_innateStatuses"), AssetDatabase.LoadAssetAtPath<ScriptableObject>(Defs + "Status_EliteBrute.asset"));
            combatant.ApplyModifiedPropertiesWithoutUndo();

            EnemyAreaTelegraph telegraph = root.GetComponent<EnemyAreaTelegraph>();
            if (telegraph == null)
            {
                telegraph = root.AddComponent<EnemyAreaTelegraph>();
            }

            var telegraphSo = new SerializedObject(telegraph);
            telegraphSo.FindProperty("_impactEffect").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(BurstPath);
            telegraphSo.FindProperty("_impactEffectRadius").floatValue = 1.5f;
            telegraphSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, BrutePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        log.Append("prefab GruntBrute (variant of Grunt) authored | ");
    }

    // ------------------------------------------------------------------ scene

    private static void AuthorScene(System.Text.StringBuilder log)
    {
        Material padMaterial = MaterialCopy(PadMaterialPath, new Color(0.85f, 0.12f, 0.08f));
        Material ringMaterial = MaterialCopy(RingMaterialPath, new Color(0.3f, 0.29f, 0.27f));

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject existing = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "EliteEncounter");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        var root = new GameObject("EliteEncounter");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.position = new Vector3(-11f, 0f, 4f);

        var trigger = root.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 0.75f, 0f);
        trigger.size = new Vector3(3f, 1.5f, 3f);

        var behaviour = root.AddComponent<GameplayObjectBehaviour>();
        var spawner = new SerializedObject(root.AddComponent<GameplayObjectSpawner>());
        spawner.FindProperty("_definition").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameplayObjectDefinition>(Defs + "Obj_EliteEncounter.asset");
        spawner.FindProperty("_behaviour").objectReferenceValue = behaviour;
        spawner.ApplyModifiedPropertiesWithoutUndo();

        GameObject pad = Visual(PrimitiveType.Cube, "Pad", root.transform, new Vector3(-11f, 0.05f, 4f), new Vector3(3f, 0.1f, 3f), padMaterial);
        GameObject ring = Visual(PrimitiveType.Cylinder, "ArenaFloor", root.transform, new Vector3(-18f, 0.01f, 4f), new Vector3(15f, 0.005f, 15f), ringMaterial);

        Quaternion facePad = Quaternion.Euler(0f, 90f, 0f);
        Transform elite = Point("EliteSpawn", root.transform, new Vector3(-18f, 1.3f, 4f), facePad);
        Transform[] escorts = { Point("Escort_0", root.transform, new Vector3(-16.5f, 1f, 6.5f), facePad), Point("Escort_1", root.transform, new Vector3(-16.5f, 1f, 1.5f), facePad) };
        Transform[] reinforcements = { Point("Reinforcement_0", root.transform, new Vector3(-22f, 1f, 7.5f), facePad), Point("Reinforcement_1", root.transform, new Vector3(-22f, 1f, 0.5f), facePad) };

        var encounter = new SerializedObject(root.AddComponent<EliteEncounter>());
        encounter.FindProperty("_behaviour").objectReferenceValue = behaviour;
        encounter.FindProperty("_elitePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(BrutePrefabPath);
        encounter.FindProperty("_eliteSpawnPoint").objectReferenceValue = elite;
        encounter.FindProperty("_escortPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(GruntPrefabPath);
        SetList(encounter.FindProperty("_escortSpawnPoints"), escorts);
        SetList(encounter.FindProperty("_reinforcementSpawnPoints"), reinforcements);
        encounter.ApplyModifiedPropertiesWithoutUndo();
        root.AddComponent<EliteEncounterHud>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.Append($"scene: EliteEncounter pad at {root.transform.position}, elite at {elite.position}, {pad.name}/{ring.name} visuals");
    }

    private static GameObject Visual(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject visual = GameObject.CreatePrimitive(type);
        visual.name = name;
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.transform.SetParent(parent, false);
        visual.transform.position = position;
        visual.transform.localScale = scale;
        visual.GetComponent<MeshRenderer>().sharedMaterial = material;
        return visual;
    }

    private static Transform Point(string name, Transform parent, Vector3 position, Quaternion rotation)
    {
        var point = new GameObject(name).transform;
        point.SetParent(parent, false);
        point.SetPositionAndRotation(position, rotation);
        return point;
    }

    private static Material MaterialCopy(string path, Color color)
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(path) == null)
        {
            AssetDatabase.CopyAsset(PadMaterialSource, path);
            AssetDatabase.ImportAsset(path);
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        material.SetColor("_BaseColor", color);
        material.SetColor("_Color", color);
        EditorUtility.SetDirty(material);
        return material;
    }
}
