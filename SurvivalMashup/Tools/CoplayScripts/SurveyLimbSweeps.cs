using System.Collections.Generic;
using System.IO;
using System.Text;
using ToyChest.Gameplay.HitDetection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Survey: for each Frank Fighter combo clip, samples every limb tip relative to the hips (in the character's
/// starting frame) and reports its fast segments — peak speed, the yaw arc swept around the body while fast
/// (a hook/roundhouse sweeps a wide arc; a jab barely turns), height, and timing. Complements the window
/// proposer, which scores forward drive only. Read-only (RG5E clip selection).
/// </summary>
public static class SurveyLimbSweeps
{
    private const string Folder = "Assets/Frank_Fighter/Assets/Animations/FBX Animation/Root_Motion";
    private const float Fast = 7f;

    public static string Execute()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Content/Prefabs/Player.prefab");
        GameObject preview = Object.Instantiate(prefab);
        preview.hideFlags = HideFlags.DontSave;
        foreach (MonoBehaviour b in preview.GetComponentsInChildren<MonoBehaviour>()) b.enabled = false;
        Animator animator = preview.GetComponentInChildren<Animator>();
        Dictionary<StrikeLimb, StrikeShape> shapes = CharacterStrikeProfile.ResolveShapes(animator, null);
        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        var report = new StringBuilder();
        AnimationMode.StartAnimationMode();
        try
        {
            foreach (string file in Directory.GetFiles(Folder, "Frank_RPG_Fighter_*Combo*.FBX"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (name.EndsWith("_All") || name.EndsWith("_0H")) continue;
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(file.Replace('\\', '/'));
                report.AppendLine($"## {name} ({clip.length:0.00}s)");
                const float dt = 1f / 60f;
                var prev = new Dictionary<StrikeLimb, Vector3>();
                var seg = new Dictionary<StrikeLimb, (float t0, float peakV, float peakT, float yawMin, float yawMax, float hMax, float reach)>();
                float ground = float.MaxValue;
                var lines = new List<string>();
                for (float t = 0f; t <= clip.length + 1e-4f; t += dt)
                {
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(animator.gameObject, clip, t);
                    AnimationMode.EndSampling();
                    foreach (KeyValuePair<StrikeLimb, StrikeShape> pair in shapes)
                    {
                        if (pair.Key > StrikeLimb.LeftFoot) continue;
                        CharacterStrikeProfile.TryResolvePose(animator, pair.Value, out _, out Vector3 tip);
                        Vector3 rel = tip - new Vector3(hips.position.x, 0f, hips.position.z);
                        if (pair.Key >= StrikeLimb.RightFoot) ground = Mathf.Min(ground, tip.y);
                        float v = prev.TryGetValue(pair.Key, out Vector3 p) ? (rel - p).magnitude / dt : 0f;
                        prev[pair.Key] = rel;
                        float yaw = Mathf.Atan2(rel.x, rel.z) * Mathf.Rad2Deg;
                        float reach = new Vector2(rel.x, rel.z).magnitude;
                        bool open = seg.TryGetValue(pair.Key, out var s);
                        if (v >= Fast)
                        {
                            if (!open) s = (t, v, t, yaw, yaw, tip.y, reach);
                            if (v > s.peakV) { s.peakV = v; s.peakT = t; }
                            s.yawMin = Mathf.Min(s.yawMin, yaw);
                            s.yawMax = Mathf.Max(s.yawMax, yaw);
                            s.hMax = Mathf.Max(s.hMax, tip.y);
                            s.reach = Mathf.Max(s.reach, reach);
                            seg[pair.Key] = s;
                        }
                        else if (open)
                        {
                            seg.Remove(pair.Key);
                            if (t - s.t0 >= 0.05f)
                                lines.Add($"  {s.t0:0.00}-{t:0.00}s {pair.Key,-9} peak {s.peakV:0} m/s @{s.peakT:0.00} " +
                                          $"arc {s.yawMax - s.yawMin:0}° ({s.yawMin:0}..{s.yawMax:0}) top {s.hMax:0.00}m reach {s.reach:0.00}m");
                        }
                    }
                }

                lines.Sort();
                foreach (string l in lines) report.AppendLine(l);
                report.AppendLine($"  (ground {ground:0.00}m)");
            }
        }
        finally
        {
            AnimationMode.StopAnimationMode();
            Object.DestroyImmediate(preview);
        }

        File.WriteAllText("Temp/SurveyLimbSweeps.txt", report.ToString());
        return "wrote Temp/SurveyLimbSweeps.txt";
    }
}
