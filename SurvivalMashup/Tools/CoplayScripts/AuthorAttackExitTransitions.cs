using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Makes the player's attack states hand off to locomotion the instant gameplay releases the attack and the
/// player is moving. Adds an <c>AttackCommitted</c> bool (driven by PlayerAnimatorDriver from
/// PlayerCombat.IsMovementCommitted) and rewrites each attack state's "moving" exit as
/// <c>AttackCommitted == false &amp;&amp; Speed &gt; 0.15</c> with NO exit time. The old exit had exit time 0.1, and
/// Unity only evaluates exit-time conditions at that moment — when the player is still rooted (Speed 0) — so
/// it never fired and the clip's tail played while the character walked away (the slide). The "settle"
/// exit (no input → finish the clip) is unchanged. Idempotent. Editor-only tooling.
/// </summary>
public static class AuthorAttackExitTransitions
{
    private const string ControllerPath = "Assets/Game/Content/Animation/PlayerAnimator.controller";
    private static readonly string[] AttackStates = { "Attack", "Combo2", "Combo3" };

    public static string Execute()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            return "ABORT: controller not found.";
        }

        bool hasParam = false;
        foreach (AnimatorControllerParameter p in controller.parameters)
        {
            hasParam |= p.name == "AttackCommitted";
        }

        if (!hasParam)
        {
            controller.AddParameter("AttackCommitted", AnimatorControllerParameterType.Bool);
        }

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState loco = null;
        foreach (ChildAnimatorState c in sm.states)
        {
            if (c.state.name == "Locomotion") loco = c.state;
        }

        var report = new StringBuilder();
        foreach (ChildAnimatorState child in sm.states)
        {
            AnimatorState state = child.state;
            if (System.Array.IndexOf(AttackStates, state.name) < 0)
            {
                continue;
            }

            // Remove every existing exit to Locomotion (the exit-time "moving" and "settle" transitions). The
            // settle exit fired at 80% of the clip regardless of gameplay, so the Animator left the attack while
            // the player was still committed — and once the clip stopped evaluating, its late OnAttackEnd event
            // never fired.
            foreach (AnimatorStateTransition t in state.transitions)
            {
                if (t.destinationState == loco)
                {
                    state.RemoveTransition(t);
                }
            }

            // One exit, owned by gameplay: leave the attack the moment the commitment ends (OnAttackEnd, or the
            // fallback). Evaluated every frame (no exit time), so a held stick hands off to walk/run at once and
            // an idle player blends to idle; the clip is never cut while the attack still owns the character.
            AnimatorStateTransition exit = state.AddTransition(loco);
            exit.hasExitTime = false;
            exit.duration = 0.12f;
            exit.interruptionSource = TransitionInterruptionSource.None;
            exit.AddCondition(AnimatorConditionMode.IfNot, 0f, "AttackCommitted");

            report.AppendLine($"{state.name}: single exit -> Locomotion when !AttackCommitted (no exit time).");
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return (hasParam ? "" : "Added AttackCommitted bool.\n") + report;
    }
}
