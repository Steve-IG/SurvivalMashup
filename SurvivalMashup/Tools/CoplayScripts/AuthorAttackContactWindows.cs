using System.Collections.Generic;
using System.Text;
using ToyChest.Gameplay.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// For every combo step whose clip is a <em>leaping</em> attack, replaces its single <c>OnAttackContact</c>
/// with a measured contact window: <c>OnAttackContactStart</c> at takeoff and <c>OnAttackContactEnd</c> at the
/// landing slam. Both are found from the clip's own humanoid root height (RootT.y): takeoff is when the root
/// rises back above standing height after the crouch, landing is when it returns to (or below) standing
/// height after the apex — plus one short beat so the slam's impact frame is included. A clip counts as a leap
/// only if its apex clears <see cref="MinApex"/>; ordinary strikes are left alone. Run after importing or
/// swapping clips; a designer can still nudge either event in the FBX import settings. Idempotent.
/// </summary>
public static class AuthorAttackContactWindows
{
    private const string ControllerPath = "Assets/Game/Content/Animation/PlayerAnimator.controller";
    private const string ComboPath = "Assets/Game/Content/Definitions/Combo_PlayerMelee.asset";
    private const float MinApex = 0.5f;       // metres above standing height for a clip to count as a leap
    private const float LiftOff = 0.05f;      // metres above standing height that counts as airborne
    private const float SlamBeat = 0.05f;     // seconds kept open past touchdown for the slam's impact frame
    private const float Step = 1f / 120f;

    public static string Execute()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var combo = AssetDatabase.LoadAssetAtPath<ComboDefinition>(ComboPath);
        if (controller == null || combo == null)
        {
            return "ABORT: controller or Combo_PlayerMelee not found.";
        }

        var report = new StringBuilder();
        foreach (ComboStep step in combo.Steps)
        {
            AnimatorState state = FindState(controller.layers[0].stateMachine, step.AnimatorTrigger);
            var clip = state != null ? state.motion as AnimationClip : null;
            if (clip == null)
            {
                report.AppendLine($"{step.AnimatorTrigger}: no clip — skipped.");
                continue;
            }

            if (!TryMeasureLeap(clip, out float takeoff, out float landing, out float apex))
            {
                report.AppendLine($"{step.AnimatorTrigger}: not a leap (apex {apex:0.00}m) — left as a single contact.");
                continue;
            }

            float end = Mathf.Min(landing + SlamBeat, clip.length);
            string fbxPath = AssetDatabase.GetAssetPath(clip);
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                report.AppendLine($"{step.AnimatorTrigger}: '{fbxPath}' is not a model — skipped.");
                continue;
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            int idx = 0;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i].name == clip.name) { idx = i; break; }
            }

            var events = new List<AnimationEvent>(clips[idx].events ?? new AnimationEvent[0]);
            events.RemoveAll(e => e.functionName == "OnAttackContact" ||
                                  e.functionName == "OnAttackContactStart" ||
                                  e.functionName == "OnAttackContactEnd");
            // ModelImporter clip events are NORMALIZED (0-1), unlike AnimationClip events (seconds).
            events.Add(new AnimationEvent { functionName = "OnAttackContactStart", time = takeoff / clip.length });
            events.Add(new AnimationEvent { functionName = "OnAttackContactEnd", time = end / clip.length });
            events.Sort((a, b) => a.time.CompareTo(b.time));
            clips[idx].events = events.ToArray();
            importer.clipAnimations = clips;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            report.AppendLine($"{step.AnimatorTrigger}: leap apex {apex:0.00}m — contact window {takeoff:0.00}s (takeoff) → {end:0.00}s (slam) " +
                              $"[{System.IO.Path.GetFileName(fbxPath)}]; replaced the single OnAttackContact.");
        }

        AssetDatabase.Refresh();
        return report.ToString();
    }

    private static bool TryMeasureLeap(AnimationClip clip, out float takeoff, out float landing, out float apex)
    {
        takeoff = landing = apex = 0f;
        AnimationCurve y = null;
        foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(clip))
        {
            if (b.propertyName == "RootT.y") y = AnimationUtility.GetEditorCurve(clip, b);
        }

        if (y == null)
        {
            return false;
        }

        float baseY = y.Evaluate(0f);
        float apexTime = 0f;
        for (float t = 0f; t <= clip.length; t += Step)
        {
            float dy = y.Evaluate(t) - baseY;
            if (dy > apex) { apex = dy; apexTime = t; }
        }

        if (apex < MinApex)
        {
            return false;
        }

        // Takeoff: walking back from the apex, the last moment the root was still at/below lift-off height.
        takeoff = 0f;
        for (float t = apexTime; t >= 0f; t -= Step)
        {
            if (y.Evaluate(t) - baseY <= LiftOff) { takeoff = t; break; }
        }

        // Landing: the first moment after the apex the root is back at/below standing height.
        landing = clip.length;
        for (float t = apexTime; t <= clip.length; t += Step)
        {
            if (y.Evaluate(t) - baseY <= 0f) { landing = t; break; }
        }

        return true;
    }

    private static AnimatorState FindState(AnimatorStateMachine sm, string name)
    {
        foreach (ChildAnimatorState c in sm.states)
        {
            if (c.state.name == name) return c.state;
        }

        return null;
    }
}
