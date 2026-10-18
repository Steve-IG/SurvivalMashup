using ToyChest.Gameplay.Combat;
using ToyChest.Gameplay.HitDetection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gives the leaping finisher (Combo3) a hit volume that suits an aerial contact window: a new shared preset,
/// HV_LeapSlam (frontal cone, 3.0m reach, ±75°, multi-target), anchored at the Hips. A forearm-anchored punch
/// volume rises ~2.5m during the flip — out of its own 2.2m reach — so it missed the airborne kicks entirely
/// and caught the slam for ~2 frames; a body-anchored volume follows the whole leap and still reaches ground
/// level for the slam. Only step 3 changes. Idempotent. Editor-only tooling.
/// </summary>
public static class AuthorLeapSlamVolume
{
    private const string PresetPath = "Assets/Game/Content/HitVolumes/HV_LeapSlam.asset";
    private const string ComboPath = "Assets/Game/Content/Definitions/Combo_PlayerMelee.asset";

    public static string Execute()
    {
        var preset = AssetDatabase.LoadAssetAtPath<HitVolumeAsset>(PresetPath);
        if (preset == null)
        {
            preset = ScriptableObject.CreateInstance<HitVolumeAsset>();
            AssetDatabase.CreateAsset(preset, PresetPath);
        }

        var pso = new SerializedObject(preset);
        SerializedProperty v = pso.FindProperty("_volume");
        v.FindPropertyRelative("_shape").enumValueIndex = (int)HitShape.Cone;
        v.FindPropertyRelative("_radius").floatValue = 3.0f;
        v.FindPropertyRelative("_coneHalfAngleDegrees").floatValue = 75f;
        v.FindPropertyRelative("_multiTarget").boolValue = true;
        v.FindPropertyRelative("_maxTargets").intValue = 6;
        pso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(preset);

        var combo = AssetDatabase.LoadAssetAtPath<ComboDefinition>(ComboPath);
        var cso = new SerializedObject(combo);
        SerializedProperty steps = cso.FindProperty("_steps");
        string report = "";
        for (int i = 0; i < steps.arraySize; i++)
        {
            SerializedProperty step = steps.GetArrayElementAtIndex(i);
            if (step.FindPropertyRelative("_animatorTrigger").stringValue != "Combo3") continue;

            SerializedProperty bone = step.FindPropertyRelative("_anchorBone");
            string before = ((HumanBodyBones)bone.intValue).ToString();
            step.FindPropertyRelative("_volumePreset").objectReferenceValue = preset;
            bone.intValue = (int)HumanBodyBones.Hips;
            step.FindPropertyRelative("_anchorOffset").vector3Value = Vector3.zero;
            report = $"Combo3: volume -> HV_LeapSlam (cone 3.0m ±75°, multi 6), anchor {before} -> Hips.";
        }

        cso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(combo);
        AssetDatabase.SaveAssets();
        return report;
    }
}
