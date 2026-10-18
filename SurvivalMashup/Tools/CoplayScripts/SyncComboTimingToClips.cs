using System.Text;
using ToyChest.Gameplay.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Ties each combo step's authored recovery to the real length of the animation clip its animator state
/// plays, so the player stays rooted (moving only by the attack's root motion) for the whole clip and never
/// slides out from under it. For each step it finds the animator state named by the step's animator trigger,
/// reads that state's clip length ÷ state speed (the on-screen duration), and sets the step's recovery so
/// wind-up + recovery ≈ that duration. Re-run after swapping a combo clip (e.g. a leaping finisher). Reports
/// what it measured and set. Editor-only tooling; adds no runtime code.
/// </summary>
public static class SyncComboTimingToClips
{
    private const string ControllerPath = "Assets/Game/Content/Animation/PlayerAnimator.controller";
    private const string ComboPath = "Assets/Game/Content/Definitions/Combo_PlayerMelee.asset";
    private const float MinRecovery = 0.12f;
    private const float TailTrim = -0.05f; // fallback ends just AFTER the clip so an authored OnAttackEnd always wins

    public static string Execute()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var combo = AssetDatabase.LoadAssetAtPath<ComboDefinition>(ComboPath);
        if (controller == null || combo == null)
        {
            return "ABORT: controller or Combo_PlayerMelee not found.";
        }

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        var so = new SerializedObject(combo);
        SerializedProperty steps = so.FindProperty("_steps");
        var report = new StringBuilder();

        for (int i = 0; i < steps.arraySize; i++)
        {
            SerializedProperty step = steps.GetArrayElementAtIndex(i);
            string trigger = step.FindPropertyRelative("_animatorTrigger").stringValue;
            float windUp = step.FindPropertyRelative("_windUpDuration").floatValue;

            AnimatorState state = FindState(sm, trigger);
            var clip = state != null ? state.motion as AnimationClip : null;
            if (clip == null)
            {
                report.AppendLine($"step {i} ({trigger}): no clip on state — recovery left unchanged.");
                continue;
            }

            float speed = state.speed <= 0.01f ? 1f : state.speed;
            float onScreen = clip.length / speed;

            // A fallback timer must never run ahead of the event it backs up. An attack asset's first window is
            // its contact; otherwise an authored OnAttackContact(Start) on the clip is.
            var attack = step.FindPropertyRelative("_attack").objectReferenceValue as AttackDefinition;
            if (attack != null && attack.LastContactEnd >= 0f)
            {
                // An attack asset's step "contact" is when its strikes complete (its last window closes).
                windUp = attack.LastContactEnd * clip.length / speed + 0.05f;
                step.FindPropertyRelative("_windUpDuration").floatValue = windUp;
            }

            foreach (AnimationEvent e in attack != null ? new AnimationEvent[0] : AnimationUtility.GetAnimationEvents(clip))
            {
                if (e.functionName == "OnAttackContact" || e.functionName == "OnAttackContactStart")
                {
                    windUp = e.time / speed + 0.05f;
                    step.FindPropertyRelative("_windUpDuration").floatValue = windUp;
                }
            }
            float recovery = Mathf.Max(MinRecovery, onScreen - windUp - TailTrim);
            step.FindPropertyRelative("_recoveryDuration").floatValue = recovery;

            report.AppendLine(
                $"step {i} ({trigger}): clip='{clip.name}' len={clip.length:0.00}s speed={speed:0.0} " +
                $"onScreen={onScreen:0.00}s windUp={windUp:0.00}s -> recovery={recovery:0.00}s " +
                $"(rooted {windUp + recovery:0.00}s)");
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(combo);
        AssetDatabase.SaveAssets();
        return report.ToString();
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
