using System.IO;
using System.Text;
using ToyChest.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Survey: runs the AttackWindowProposer over every Frank RPG Fighter combo clip on the Player rig and reports,
/// per clip, its length, humanoid/root-motion status, root travel (planar distance, max height, lateral drift),
/// and the proposer's account. Used to pick clips for the attack library (RG5E). Read-only; writes nothing.
/// </summary>
public static class AnalyzeFighterClips
{
    private const string Folder = "Assets/Frank_Fighter/Assets/Animations/FBX Animation/Root_Motion";

    public static string Execute()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Content/Prefabs/Player.prefab");
        GameObject preview = Object.Instantiate(prefab);
        preview.hideFlags = HideFlags.DontSave;
        foreach (MonoBehaviour b in preview.GetComponentsInChildren<MonoBehaviour>()) b.enabled = false;
        Animator animator = preview.GetComponentInChildren<Animator>();
        var report = new StringBuilder();
        try
        {
            foreach (string path in Directory.GetFiles(Folder, "Frank_RPG_Fighter_*.FBX"))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (name.EndsWith("_All") || name.EndsWith("_0H")) continue;
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path.Replace('\\', '/'));
                if (clip == null) { report.AppendLine($"## {name}: NO CLIP"); continue; }

                Vector3 travel = RootTravel(clip, out float maxY, out float lateral);
                report.AppendLine($"## {name}  len {clip.length:0.00}s human {clip.isHumanMotion} " +
                                  $"fwd {travel.z:0.00}m lat {lateral:0.00}m apex {maxY:0.00}m rootStop {AttackWindowProposer.FindRootMotionEnd(clip):0.00}s");
                AttackWindowProposer.Proposal p = AttackWindowProposer.Propose(animator, clip, 1f);
                report.AppendLine(p.Report.TrimEnd());
            }
        }
        finally
        {
            Object.DestroyImmediate(preview);
        }

        File.WriteAllText("Temp/AnalyzeFighterClips.txt", report.ToString());
        return "wrote Temp/AnalyzeFighterClips.txt";
    }

    private static Vector3 RootTravel(AnimationClip clip, out float maxY, out float lateral)
    {
        AnimationCurve x = null, y = null, z = null;
        foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(clip))
        {
            if (b.propertyName == "RootT.x") x = AnimationUtility.GetEditorCurve(clip, b);
            if (b.propertyName == "RootT.y") y = AnimationUtility.GetEditorCurve(clip, b);
            if (b.propertyName == "RootT.z") z = AnimationUtility.GetEditorCurve(clip, b);
        }

        maxY = 0f;
        lateral = 0f;
        if (x == null || z == null) return Vector3.zero;
        float y0 = y != null ? y.Evaluate(0f) : 0f;
        for (float t = 0f; t <= clip.length; t += 1f / 60f)
        {
            if (y != null) maxY = Mathf.Max(maxY, y.Evaluate(t) - y0);
            lateral = Mathf.Max(lateral, Mathf.Abs(x.Evaluate(t) - x.Evaluate(0f)));
        }

        return new Vector3(x.Evaluate(clip.length) - x.Evaluate(0f), 0f, z.Evaluate(clip.length) - z.Evaluate(0f));
    }
}
