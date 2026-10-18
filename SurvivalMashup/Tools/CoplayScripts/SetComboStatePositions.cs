using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Gives the script-added Combo2/Combo3 states explicit graph positions next to Attack so they are visible
/// (and findable) in the Animator window — states added via AnimatorStateMachine.AddState land at a default
/// coordinate and can sit stacked behind other nodes. Also reports each combo state's current motion so a
/// designer knows exactly which clip to swap. Idempotent.
/// </summary>
public static class SetComboStatePositions
{
    private const string ControllerPath = "Assets/Game/Content/Animation/PlayerAnimator.controller";

    public static string Execute()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        Vector3 attackPos = Vector3.zero;
        ChildAnimatorState[] children = sm.states;
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].state.name == "Attack")
            {
                attackPos = children[i].position;
            }
        }

        string report = "Attack pos=" + attackPos + "\n";
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].state.name == "Combo2")
            {
                report += "Combo2 was at " + children[i].position + ", motion=" +
                          (children[i].state.motion != null ? children[i].state.motion.name : "<none>") + "\n";
                children[i].position = attackPos + new Vector3(300f, 70f, 0f);
            }
            else if (children[i].state.name == "Combo3")
            {
                report += "Combo3 was at " + children[i].position + ", motion=" +
                          (children[i].state.motion != null ? children[i].state.motion.name : "<none>") + "\n";
                children[i].position = attackPos + new Vector3(300f, 190f, 0f);
            }
        }

        sm.states = children; // write positions back
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return report + "Repositioned Combo2/Combo3 beside Attack.";
    }
}
