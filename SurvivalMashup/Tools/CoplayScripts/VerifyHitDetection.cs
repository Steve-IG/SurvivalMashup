using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Play-mode runtime check for the canonical hit-detection architecture (run from a Bootstrap-composed
/// session). Proves the core claim the vocabulary exists to guarantee: the player melee is now a
/// directional query, so a blow lands when the player faces the Grunt and MISSES when the player faces
/// away — the exact "connect while facing away" defect, fixed at the root. Public API + reflection only;
/// adds no gameplay. Positions the player at melee range and forces the swing's contact frame in each
/// orientation, watching the Impacted event (which fires only on a committed, landed hit).
/// </summary>
public static class VerifyHitDetection
{
    public static string Execute()
    {
        Type combatType = Type.GetType("ToyChest.Gameplay.Player.PlayerCombat, ToyChest.Gameplay.Player");
        var combat = UnityEngine.Object.FindAnyObjectByType(combatType) as MonoBehaviour;
        if (combat == null) { Debug.LogError("[VerifyHitDetection] No PlayerCombat."); return "no combat"; }

        GameObject grunt = GameObject.FindGameObjectWithTag("Enemy");
        if (grunt == null) { Debug.LogError("[VerifyHitDetection] No Enemy."); return "no enemy"; }

        const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var tryAttack = combatType.GetMethod("TryAttack");
        var resolve = combatType.GetMethod("ResolveImpact", F);
        var swingField = combatType.GetField("_swing", F);

        var evt = combatType.GetEvent("Impacted");
        bool impacted = false;
        Action<Vector3> handler = _ => impacted = true;
        evt.AddEventHandler(combat, handler);

        // Place the player just within melee reach of the Grunt (Grunt sits at +X of the player).
        Vector3 toGrunt = (grunt.transform.position - combat.transform.position);
        Vector3 near = grunt.transform.position - new Vector3(1.2f, 0f, 0f);
        Vector3 dirToGrunt = (grunt.transform.position - near); dirToGrunt.y = 0f;

        // (A) FACING the Grunt: the contact frame must land the blow.
        combat.transform.position = near;
        combat.transform.rotation = Quaternion.LookRotation(dirToGrunt.normalized, Vector3.up);
        impacted = false;
        tryAttack.Invoke(combat, null);
        resolve.Invoke(combat, null);
        bool landsWhenFacing = impacted;
        ResetSwing(swingField, combat);

        // (B) FACING AWAY (180°): the same contact frame must NOT land — the blow cannot reach behind.
        combat.transform.position = near;
        combat.transform.rotation = Quaternion.LookRotation(-dirToGrunt.normalized, Vector3.up);
        impacted = false;
        tryAttack.Invoke(combat, null);
        resolve.Invoke(combat, null);
        bool missesWhenFacingAway = !impacted;
        ResetSwing(swingField, combat);

        evt.RemoveEventHandler(combat, handler);

        bool pass = landsWhenFacing && missesWhenFacingAway;
        string msg = $"[VerifyHitDetection] landsWhenFacing={landsWhenFacing} missesWhenFacingAway={missesWhenFacingAway} => {(pass ? "PASS" : "FAIL")}";
        if (pass) { Debug.Log(msg); } else { Debug.LogError(msg); }
        return msg;
    }

    private static void ResetSwing(FieldInfo swingField, MonoBehaviour combat)
    {
        object swing = swingField.GetValue(combat);
        swing.GetType().GetMethod("Cancel").Invoke(swing, null);
    }
}
