using System.Text;
using ToyChest.Gameplay.Combat;
using ToyChest.Gameplay.HitDetection;
using ToyChest.Gameplay.Player;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.GameplayEffects;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Authors the player's 3-hit melee combo content (RG5D): sets the base Strike ability to zero cooldown so
/// combo cadence is owned by the sequencer's step timing, creates two zero-cooldown continuation abilities,
/// builds the Combo_PlayerMelee definition (three punch steps, distinct animator triggers), grants the new
/// abilities to the player object, and assigns the combo to the Player prefab. Idempotent — re-running
/// overwrites the same assets. Editor-only tooling; adds no runtime code.
/// </summary>
public static class AuthorComboContent
{
    private const string Defs = "Assets/Game/Content/Definitions";
    private const string PunchPath = "Assets/Game/Content/HitVolumes/HV_Punch.asset";
    private const string PlayerPrefab = "Assets/Game/Content/Prefabs/Player.prefab";

    public static string Execute()
    {
        var log = new StringBuilder();

        var damage = AssetDatabase.LoadAssetAtPath<GameplayEffectDefinition>(Defs + "/Fx_Damage_PlayerStrike.asset");
        var punch = AssetDatabase.LoadAssetAtPath<HitVolumeAsset>(PunchPath);
        if (damage == null || punch == null)
        {
            return "ABORT: missing Fx_Damage_PlayerStrike or HV_Punch.";
        }

        // 1. Base Strike cooldown -> 0. Combo cadence now lives in the sequencer's step timing (Steve approved).
        var strike = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(Defs + "/Ability_PlayerStrike.asset");
        SetFloat(strike, "_cooldownSeconds", 0f);
        log.AppendLine("Ability_PlayerStrike cooldown -> 0");

        // 2. Two new continuation abilities: Provided, zero cooldown, reusing the existing strike damage.
        AbilityDefinition combo2 = CreateAbility("Ability_PlayerCombo2", "ability.player_combo2", "Combo Strike 2", damage);
        AbilityDefinition combo3 = CreateAbility("Ability_PlayerCombo3", "ability.player_combo3", "Combo Strike 3", damage);
        log.AppendLine("Authored Ability_PlayerCombo2 / Ability_PlayerCombo3 (Provided, 0 cooldown)");

        // 3. The 3-step combo definition. Each step keeps its own ability + animator trigger; all share the
        //    (now multi-target) Punch hit volume anchored to the right hand.
        var combo = LoadOrCreate<ComboDefinition>(Defs + "/Combo_PlayerMelee.asset");
        var cso = new SerializedObject(combo);
        SerializedProperty steps = cso.FindProperty("_steps");
        steps.arraySize = 3;
        ConfigureStep(steps.GetArrayElementAtIndex(0), "ability.player_strike", punch, "Attack", 0.28f, 0.22f, 0.32f);
        ConfigureStep(steps.GetArrayElementAtIndex(1), "ability.player_combo2", punch, "Combo2", 0.26f, 0.22f, 0.32f);
        ConfigureStep(steps.GetArrayElementAtIndex(2), "ability.player_combo3", punch, "Combo3", 0.34f, 0.30f, 0.00f);
        cso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(combo);
        log.AppendLine("Combo_PlayerMelee authored with 3 steps (Attack -> Combo2 -> Combo3)");

        // 4. Grant the new abilities to the player object definition so the AbilitySet can activate them.
        var player = AssetDatabase.LoadAssetAtPath<ScriptableObject>(Defs + "/Obj_Player.asset");
        var pso = new SerializedObject(player);
        SerializedProperty abilities = pso.FindProperty("_abilities");
        AddUnique(abilities, combo2);
        AddUnique(abilities, combo3);
        pso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(player);
        log.AppendLine("Obj_Player granted combo2/combo3 (total abilities=" + abilities.arraySize + ")");

        // 5. Assign the combo to the Player prefab's PlayerCombat.
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
        PlayerCombat pc = prefab != null ? prefab.GetComponentInChildren<PlayerCombat>(true) : null;
        if (pc != null)
        {
            var so = new SerializedObject(pc);
            so.FindProperty("_combo").objectReferenceValue = combo;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pc);
            PrefabUtility.SavePrefabAsset(prefab);
            log.AppendLine("Player.prefab PlayerCombat._combo assigned");
        }
        else
        {
            log.AppendLine("WARN: PlayerCombat not found on Player.prefab");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return log.ToString();
    }

    private static AbilityDefinition CreateAbility(string file, string id, string displayName, GameplayEffectDefinition damage)
    {
        var ability = LoadOrCreate<AbilityDefinition>(Defs + "/" + file + ".asset");
        var so = new SerializedObject(ability);
        so.FindProperty("_definitionId").stringValue = id;
        so.FindProperty("_displayName").stringValue = displayName;
        so.FindProperty("_category").stringValue = "Combat";
        so.FindProperty("_targetMode").enumValueIndex = (int)AbilityTargetMode.Provided;
        so.FindProperty("_cooldownSeconds").floatValue = 0f;
        SerializedProperty effects = so.FindProperty("_effects");
        effects.arraySize = 1;
        effects.GetArrayElementAtIndex(0).objectReferenceValue = damage;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ability);
        return ability;
    }

    private static void ConfigureStep(
        SerializedProperty step, string abilityId, HitVolumeAsset preset, string trigger,
        float windUp, float recovery, float continuation)
    {
        step.FindPropertyRelative("_attackAbilityId").stringValue = abilityId;
        step.FindPropertyRelative("_volumePreset").objectReferenceValue = preset;
        step.FindPropertyRelative("_anchorBone").enumValueIndex = (int)HumanBodyBones.RightHand;
        step.FindPropertyRelative("_anchorOffset").vector3Value = new Vector3(0f, 0f, 0.1f);
        step.FindPropertyRelative("_animatorTrigger").stringValue = trigger;
        step.FindPropertyRelative("_windUpDuration").floatValue = windUp;
        step.FindPropertyRelative("_recoveryDuration").floatValue = recovery;
        step.FindPropertyRelative("_continuationWindow").floatValue = continuation;
        step.FindPropertyRelative("_postImpactCommit").floatValue = 0.05f;
        SerializedProperty extras = step.FindPropertyRelative("_extraVolumes");
        if (extras != null)
        {
            extras.arraySize = 0;
        }
    }

    private static void AddUnique(SerializedProperty array, Object value)
    {
        for (int i = 0; i < array.arraySize; i++)
        {
            if (array.GetArrayElementAtIndex(i).objectReferenceValue == value)
            {
                return;
            }
        }

        array.arraySize++;
        array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = value;
    }

    private static void SetFloat(Object asset, string prop, float value)
    {
        var so = new SerializedObject(asset);
        so.FindProperty(prop).floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }

        return asset;
    }
}
