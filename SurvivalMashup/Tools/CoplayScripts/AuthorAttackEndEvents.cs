using System.Collections.Generic;
using System.Text;
using ToyChest.Gameplay.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Places an <c>OnAttackEnd</c> animation event on each player combo clip at the point the clip's own root
/// motion stops — measured, not guessed. For each combo step it samples the clip's humanoid root curves
/// (RootT.x/y/z), finds the last moment the root is still moving faster than a small threshold, and puts the
/// event just after it (never before the step's contact frame). That is the "committed motion is done"
/// frame: from there the attack releases the character. Clips without root curves fall back to a fraction.
/// This scales to any number of attacks — run it after importing or swapping clips; a designer can still
/// nudge any event in the FBX import settings (Animation ▸ Events). Idempotent. Editor-only tooling.
/// </summary>
public static class AuthorAttackEndEvents
{
    private const string ControllerPath = "Assets/Game/Content/Animation/PlayerAnimator.controller";
    private const string ComboPath = "Assets/Game/Content/Definitions/Combo_PlayerMelee.asset";
    private const float MovingSpeed = 0.25f;    // m/s of root travel below which the attack has "stopped moving"
    private const float Pad = 0.03f;            // seconds after the last moving sample
    private const float AfterContact = 0.06f;   // never end before contact + this (real seconds)
    private const float FallbackFraction = 0.6f;
    private const float SampleStep = 1f / 60f;

    public static string Execute()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var combo = AssetDatabase.LoadAssetAtPath<ComboDefinition>(ComboPath);
        if (controller == null || combo == null)
        {
            return "ABORT: controller or Combo_PlayerMelee not found.";
        }

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        var report = new StringBuilder();
        var done = new HashSet<string>();

        foreach (ComboStep step in combo.Steps)
        {
            AnimatorState state = FindState(sm, step.AnimatorTrigger);
            var clip = state != null ? state.motion as AnimationClip : null;
            if (clip == null)
            {
                report.AppendLine($"{step.AnimatorTrigger}: no clip on state — skipped.");
                continue;
            }

            string fbxPath = AssetDatabase.GetAssetPath(clip);
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null || !done.Add(fbxPath + "::" + clip.name))
            {
                report.AppendLine($"{step.AnimatorTrigger}: '{fbxPath}' skipped (not a model, or already done).");
                continue;
            }

            float speed = state.speed <= 0.01f ? 1f : state.speed;

            // Never end before contact: prefer the clip's own authored OnAttackContact, else the wind-up timer.
            float contactClipTime = step.WindUpDuration * speed;
            foreach (AnimationEvent e in AnimationUtility.GetAnimationEvents(clip))
            {
                // The end must follow the whole contact: an instant contact, or a window's close.
                if (e.functionName == "OnAttackContact" || e.functionName == "OnAttackContactEnd") contactClipTime = e.time;
            }

            contactClipTime += AfterContact * speed;

            float motionEnd = FindRootMotionEnd(clip, out bool measured);
            float t = measured ? motionEnd + Pad : clip.length * FallbackFraction;
            t = Mathf.Clamp(t, Mathf.Min(contactClipTime, clip.length * 0.95f), clip.length * 0.95f);

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            int idx = 0;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i].name == clip.name)
                {
                    idx = i;
                    break;
                }
            }

            var events = new List<AnimationEvent>(clips[idx].events ?? new AnimationEvent[0]);
            events.RemoveAll(e => e.functionName == "OnAttackEnd");
            // ModelImporter clip events are NORMALIZED (0-1 of the clip), unlike AnimationClip events (seconds).
            events.Add(new AnimationEvent { functionName = "OnAttackEnd", time = t / clip.length });
            clips[idx].events = events.ToArray();
            importer.clipAnimations = clips;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            report.AppendLine(
                $"{step.AnimatorTrigger}: clip {clip.length:0.00}s @x{speed:0.0} | " +
                (measured ? $"root motion stops at {motionEnd:0.00}s" : "no root curves (fallback)") +
                $" -> OnAttackEnd @ {t:0.00}s clip = {t / speed:0.00}s on screen ({t / clip.length:P0}) [{System.IO.Path.GetFileName(fbxPath)}]");
        }

        AssetDatabase.Refresh();
        return report.ToString();
    }

    // Last clip time at which the humanoid root is still travelling faster than MovingSpeed.
    private static float FindRootMotionEnd(AnimationClip clip, out bool measured)
    {
        AnimationCurve x = null, y = null, z = null;
        foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(clip))
        {
            if (b.propertyName == "RootT.x") x = AnimationUtility.GetEditorCurve(clip, b);
            else if (b.propertyName == "RootT.y") y = AnimationUtility.GetEditorCurve(clip, b);
            else if (b.propertyName == "RootT.z") z = AnimationUtility.GetEditorCurve(clip, b);
        }

        measured = x != null && z != null;
        if (!measured)
        {
            return 0f;
        }

        float last = 0f;
        Vector3 prev = Sample(x, y, z, 0f);
        for (float t = SampleStep; t <= clip.length; t += SampleStep)
        {
            Vector3 p = Sample(x, y, z, t);
            if ((p - prev).magnitude / SampleStep > MovingSpeed)
            {
                last = t;
            }

            prev = p;
        }

        return last;
    }

    private static Vector3 Sample(AnimationCurve x, AnimationCurve y, AnimationCurve z, float t)
    {
        return new Vector3(x.Evaluate(t), y != null ? y.Evaluate(t) : 0f, z.Evaluate(t));
    }

    private static AnimatorState FindState(AnimatorStateMachine sm, string name)
    {
        foreach (ChildAnimatorState child in sm.states)
        {
            if (child.state.name == name)
            {
                return child.state;
            }
        }

        return null;
    }
}
