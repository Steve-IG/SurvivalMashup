using System.IO;
using ToyChest.Gameplay.HitDetection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Authors the shared HitVolume preset library under Assets/Game/Content/HitVolumes. These reusable "shape"
/// assets are the production authoring unit: a new attack references one instead of duplicating geometry.
/// Idempotent — re-running overwrites the same assets. Editor-only tooling; adds no runtime code.
/// </summary>
public static class AuthorHitVolumeLibrary
{
    private const string Dir = "Assets/Game/Content/HitVolumes";

    public static string Execute()
    {
        if (!Directory.Exists(Dir))
        {
            Directory.CreateDirectory(Dir);
        }

        // name,                shape,            radius, halfAngle, multi, maxTargets
        Create("Punch",         HitShape.Cone,    2.2f,   60f,       false, 1);
        Create("SwordSlashSmall", HitShape.Cone,  2.5f,   55f,       false, 1);
        Create("SwordSlashWide",  HitShape.Cone,  2.8f,  100f,       true,  4);
        Create("Kick",          HitShape.Cone,    2.0f,   45f,       false, 1);
        Create("ClawSwipe",     HitShape.Cone,    2.2f,   80f,       false, 1);
        Create("ConeShort",     HitShape.Cone,    2.5f,   50f,       false, 1);
        Create("ConeWide",      HitShape.Cone,    3.0f,  120f,       true,  6);
        Create("BossSlam",      HitShape.Cone,    5.0f,  120f,       true,  16);
        Create("ExplosionSmall", HitShape.Sphere, 4.0f,    0f,       true,  12);
        Create("ExplosionLarge", HitShape.Sphere, 7.0f,    0f,       true,  24);
        Create("ProjectileImpact", HitShape.Sphere, 0.5f,  0f,       false, 1);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "HitVolume library authored under " + Dir;
    }

    private static void Create(string name, HitShape shape, float radius, float halfAngle, bool multi, int maxTargets)
    {
        string path = Dir + "/HV_" + name + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<HitVolumeAsset>(path);
        bool isNew = asset == null;
        if (isNew)
        {
            asset = ScriptableObject.CreateInstance<HitVolumeAsset>();
        }

        var so = new SerializedObject(asset);
        SerializedProperty v = so.FindProperty("_volume");
        v.FindPropertyRelative("_shape").enumValueIndex = (int)shape;
        v.FindPropertyRelative("_radius").floatValue = radius;
        v.FindPropertyRelative("_coneHalfAngleDegrees").floatValue = halfAngle;
        v.FindPropertyRelative("_multiTarget").boolValue = multi;
        v.FindPropertyRelative("_maxTargets").intValue = maxTargets;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (isNew)
        {
            AssetDatabase.CreateAsset(asset, path);
        }
        else
        {
            EditorUtility.SetDirty(asset);
        }
    }
}
