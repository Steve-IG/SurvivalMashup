using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gives the RG5E library clips the root-motion import settings the runtime model needs — the same ones the
/// shipped combo clips (Frank_RPG_Fighter_Combo05_*) carry: root ROTATION and root HEIGHT baked into the pose
/// (based on Original orientation / Feet), root XZ left as root motion. PlayerLocomotion applies root motion as
/// planar XZ only, so with Unity's defaults a spin's turn and a leap's height would be silently discarded in play
/// while the Attack Authoring preview (which samples the full motion) still showed them. Preserves every other
/// clip setting (frames, events, names/internal IDs). Idempotent. Editor-only tooling.
/// </summary>
public static class ConfigureAttackClipImport
{
    private const string Folder = "Assets/Frank_Fighter/Assets/Animations/FBX Animation/Root_Motion/Frank_RPG_Fighter_";

    public static readonly string[] Clips =
    {
        "Combo04_1", "Combo04_2", "Combo03_1", "Combo02_1", "Combo02_2", "Combo06_1", "AirCombo_Launcher", "Combo02_3",
    };

    public static string Execute()
    {
        var report = new StringBuilder();
        foreach (string name in Clips)
        {
            string path = $"{Folder}{name}.FBX";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                report.AppendLine($"{name}: no ModelImporter");
                continue;
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            bool changed = false;
            foreach (ModelImporterClipAnimation c in clips)
            {
                changed |= !c.lockRootRotation || !c.lockRootHeightY || c.lockRootPositionXZ ||
                           !c.keepOriginalOrientation || c.keepOriginalPositionY || !c.heightFromFeet;
                c.lockRootRotation = true;        // Root Transform Rotation: Bake Into Pose
                c.keepOriginalOrientation = true; //   based upon Original
                c.lockRootHeightY = true;         // Root Transform Position (Y): Bake Into Pose
                c.keepOriginalPositionY = false;
                c.heightFromFeet = true;          //   based upon Feet
                c.lockRootPositionXZ = false;     // Root Transform Position (XZ): root motion (applied by PlayerLocomotion)
            }

            if (!changed && importer.clipAnimations.Length > 0)
            {
                report.AppendLine($"{name}: already configured");
                continue;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            report.AppendLine($"{name}: baked rotation + Y into pose, XZ root motion");
        }

        return report.ToString();
    }
}
