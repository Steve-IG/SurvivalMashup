using System.Text;
using UnityEditor;
using ToyChest.Gameplay.Combat;

/// <summary>
/// Records RG5E's verification of the attack library: ticks Verified on windows that were checked against the
/// animation on a RenderClipContactSheet (the window spans the limb's strike) AND landed on a dummy in
/// VerifyAttackLibrary. The weapon proxy stays a suggestion — the virtual baton has no mesh to check it against.
/// Editor-only tooling.
/// </summary>
public static class MarkLibraryWindowsVerified
{
    private static readonly string[] Verified =
    {
        "Fighter_Combo04_1", "Fighter_Combo04_2", "Fighter_Combo03_1", "Fighter_Combo02_1", "Fighter_Combo02_2",
        "Fighter_Combo06_1", "Fighter_AirCombo_Launcher", "Fighter_Combo02_3",
    };

    public static string Execute()
    {
        var report = new StringBuilder();
        foreach (string name in Verified)
        {
            var attack = AssetDatabase.LoadAssetAtPath<AttackDefinition>($"Assets/Game/Content/Attacks/Attack_{name}.asset");
            var so = new SerializedObject(attack);
            SerializedProperty windows = so.FindProperty("_windows");
            for (int i = 0; i < windows.arraySize; i++) windows.GetArrayElementAtIndex(i).FindPropertyRelative("_verified").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(attack);
            report.AppendLine($"{name}: {windows.arraySize} window(s) verified");
        }

        AssetDatabase.SaveAssets();
        return report.ToString();
    }
}
