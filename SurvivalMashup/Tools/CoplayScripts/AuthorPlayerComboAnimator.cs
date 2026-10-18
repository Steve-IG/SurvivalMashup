using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Adds the presentation states for combo hits 2 and 3 to the player animator: Combo2/Combo3 trigger
/// parameters, two states driven from AnyState by those triggers, and exit transitions back to Locomotion —
/// mirroring the existing Attack state exactly. The new states reuse the Attack state's own motion (the
/// already-retargeted punch swing), so hits 2 and 3 animate with no retargeting risk; a designer can later
/// point Combo2/Combo3 at distinct combo clips (e.g. Frank_RPG_Fighter_Combo01_2/_3) in the Inspector for
/// variety. Gameplay-side combo sequencing is unchanged — the Animator only presents which step is active.
/// Idempotent. Editor-only tooling; adds no runtime code.
/// </summary>
public static class AuthorPlayerComboAnimator
{
    private const string ControllerPath = "Assets/Game/Content/Animation/PlayerAnimator.controller";

    public static string Execute()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            return "ABORT: PlayerAnimator.controller not found.";
        }

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState attack = FindState(sm, "Attack");
        AnimatorState loco = FindState(sm, "Locomotion");
        if (attack == null || loco == null)
        {
            return "ABORT: Attack or Locomotion state not found.";
        }

        EnsureTrigger(controller, "Combo2");
        EnsureTrigger(controller, "Combo3");

        AnimatorState combo2 = EnsureState(sm, "Combo2", attack);
        AnimatorState combo3 = EnsureState(sm, "Combo3", attack);

        EnsureAnyStateTransition(sm, combo2, "Combo2");
        EnsureAnyStateTransition(sm, combo3, "Combo3");

        EnsureExitTransitions(combo2, loco);
        EnsureExitTransitions(combo3, loco);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return "Authored Combo2/Combo3 states + triggers (reusing Attack motion '" +
               (attack.motion != null ? attack.motion.name : "<none>") + "', speed " + attack.speed + ").";
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

    private static void EnsureTrigger(AnimatorController controller, string name)
    {
        foreach (AnimatorControllerParameter p in controller.parameters)
        {
            if (p.name == name)
            {
                return;
            }
        }

        controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
    }

    private static AnimatorState EnsureState(AnimatorStateMachine sm, string name, AnimatorState template)
    {
        AnimatorState state = FindState(sm, name);
        if (state == null)
        {
            state = sm.AddState(name);
        }

        state.motion = template.motion;
        state.speed = template.speed;
        return state;
    }

    private static void EnsureAnyStateTransition(AnimatorStateMachine sm, AnimatorState destination, string trigger)
    {
        foreach (AnimatorStateTransition t in sm.anyStateTransitions)
        {
            if (t.destinationState == destination)
            {
                return;
            }
        }

        AnimatorStateTransition transition = sm.AddAnyStateTransition(destination);
        transition.hasExitTime = false;
        transition.duration = 0.06f;
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
    }

    private static void EnsureExitTransitions(AnimatorState from, AnimatorState loco)
    {
        // Mirror the Attack state's two exits: leave early once the player is moving again, or after the
        // follow-through. Skip if this state already transitions to Locomotion (idempotent re-run).
        foreach (AnimatorStateTransition t in from.transitions)
        {
            if (t.destinationState == loco)
            {
                return;
            }
        }

        AnimatorStateTransition moving = from.AddTransition(loco);
        moving.hasExitTime = true;
        moving.exitTime = 0.1f;
        moving.duration = 0.15f;
        moving.AddCondition(AnimatorConditionMode.Greater, 0.15f, "Speed");

        AnimatorStateTransition settle = from.AddTransition(loco);
        settle.hasExitTime = true;
        settle.exitTime = 0.8f;
        settle.duration = 0.12f;
    }
}
