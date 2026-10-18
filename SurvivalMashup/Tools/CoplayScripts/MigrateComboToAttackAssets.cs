using System.Collections.Generic;
using System.Text;
using ToyChest.Editor;
using ToyChest.Gameplay.Combat;
using ToyChest.Gameplay.HitDetection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Moves the player's combo onto the limb-window attack model: for each combo step it creates
/// Assets/Game/Content/Attacks/Attack_Player_&lt;State&gt;.asset, fills it with the AttackWindowProposer's windows
/// and commitment end (analysed on the Player character), assigns it to the step, and strips the now-unused
/// OnAttack* animation events from that clip so the asset is the single source of attack timing. Run
/// SyncComboTimingToClips afterwards to place the fallback timers. Idempotent. Editor-only tooling.
/// </summary>
public static class MigrateComboToAttackAssets
{
    private const string ComboPath = "Assets/Game/Content/Definitions/Combo_PlayerMelee.asset";
    private const string ControllerPath = "Assets/Game/Content/Animation/PlayerAnimator.controller";
    private const string PlayerPrefab = "Assets/Game/Content/Prefabs/Player.prefab";
    private static readonly HashSet<string> AttackEvents = new HashSet<string>
    {
        "OnAttackContact", "OnAttackEnd", "OnAttackContactStart", "OnAttackContactEnd",
    };

    public static string Execute()
    {
        var combo = AssetDatabase.LoadAssetAtPath<ComboDefinition>(ComboPath);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var preview = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab));
        preview.hideFlags = HideFlags.HideAndDontSave;
        var report = new StringBuilder();
        try
        {
            Animator animator = preview.GetComponentInChildren<Animator>();
            var cso = new SerializedObject(combo);
            SerializedProperty steps = cso.FindProperty("_steps");
            for (int i = 0; i < steps.arraySize; i++)
            {
                SerializedProperty step = steps.GetArrayElementAtIndex(i);
                string stateName = step.FindPropertyRelative("_animatorTrigger").stringValue;
                AnimatorState state = FindState(controller.layers[0].stateMachine, stateName);
                var clip = state != null ? state.motion as AnimationClip : null;
                if (clip == null)
                {
                    report.AppendLine($"{stateName}: no clip — skipped.");
                    continue;
                }

                AttackDefinition attack = AttackAuthoringWindow.CreateAttackAsset(clip, "Player_" + stateName);
                var aso = new SerializedObject(attack);

                // Keep the designer's authored impact: an OnAttackContact on the clip becomes the strike frame
                // before the events are stripped, so the proposer picks the limb striking then.
                SerializedProperty hint = aso.FindProperty("_strikeHint");
                if (hint.floatValue < 0f && TryGetContactTime(clip, out float contact))
                {
                    hint.floatValue = contact / clip.length;
                    report.AppendLine($"   strike frame from the clip's contact event: {contact:0.00}s");
                }

                AttackWindowProposer.Proposal proposal = AttackWindowProposer.Propose(animator, clip, state.speed, hint.floatValue);
                SerializedProperty windows = aso.FindProperty("_windows");
                windows.arraySize = proposal.Windows.Count;
                for (int w = 0; w < proposal.Windows.Count; w++)
                {
                    HitWindow src = proposal.Windows[w];
                    SerializedProperty dst = windows.GetArrayElementAtIndex(w);
                    dst.FindPropertyRelative("_id").stringValue = src.Id;
                    dst.FindPropertyRelative("_start").floatValue = src.Start;
                    dst.FindPropertyRelative("_end").floatValue = src.End;
                    dst.FindPropertyRelative("_limbs").intValue = (int)src.Limbs;
                    dst.FindPropertyRelative("_area").objectReferenceValue = null;
                    dst.FindPropertyRelative("_abilityId").stringValue = string.Empty;
                    dst.FindPropertyRelative("_rehitInterval").floatValue = 0f;
                }

                aso.FindProperty("_commitEnd").floatValue = proposal.CommitEnd;
                aso.FindProperty("_cancelFrom").floatValue = proposal.CancelFrom;
                aso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(attack);

                step.FindPropertyRelative("_attack").objectReferenceValue = attack;
                report.AppendLine($"{stateName}: {AssetDatabase.GetAssetPath(attack)} — {Describe(proposal, clip)}");
                report.AppendLine("   " + StripClipEvents(clip));
            }

            cso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(combo);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            Object.DestroyImmediate(preview);
        }

        return report.ToString();
    }

    private static string Describe(AttackWindowProposer.Proposal p, AnimationClip clip)
    {
        var sb = new StringBuilder();
        foreach (HitWindow w in p.Windows)
        {
            sb.Append($"[{w.Id}: {w.Limbs} {w.Start * clip.length:0.00}-{w.End * clip.length:0.00}s] ");
        }

        return sb.Append($"commit end {p.CommitEnd * clip.length:0.00}s").ToString();
    }

    private static bool TryGetContactTime(AnimationClip clip, out float seconds)
    {
        foreach (AnimationEvent e in AnimationUtility.GetAnimationEvents(clip))
        {
            if (e.functionName == "OnAttackContact" || e.functionName == "OnAttackContactStart")
            {
                seconds = e.time;
                return true;
            }
        }

        seconds = -1f;
        return false;
    }

    private static string StripClipEvents(AnimationClip clip)
    {
        string path = AssetDatabase.GetAssetPath(clip);
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
        {
            return "clip is not an FBX — events left as-is.";
        }

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            return "no clip events to strip.";
        }

        int removed = 0;
        foreach (ModelImporterClipAnimation ca in clips)
        {
            var kept = new List<AnimationEvent>();
            foreach (AnimationEvent e in ca.events ?? new AnimationEvent[0])
            {
                if (AttackEvents.Contains(e.functionName)) removed++;
                else kept.Add(e);
            }

            ca.events = kept.ToArray();
        }

        if (removed == 0)
        {
            return "no OnAttack* events present.";
        }

        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        return $"stripped {removed} OnAttack* event(s) from {System.IO.Path.GetFileName(path)} (timing now lives on the attack asset).";
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
