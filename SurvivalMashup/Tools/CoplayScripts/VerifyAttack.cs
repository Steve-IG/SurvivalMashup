using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Play-mode runtime check for the melee attack (run from a Bootstrap-composed session). Verifies two
/// things: (1) a press with no enemy in range still starts a swing (whiff — the attack always responds),
/// and (2) next to a Grunt, forcing the contact frame lands the authored damage (Impacted fires only on a
/// committed hit). Public API + reflection only; adds no gameplay.
/// </summary>
public static class VerifyAttack
{
    public static string Execute()
    {
        Type combatType = Type.GetType("ToyChest.Gameplay.Player.PlayerCombat, ToyChest.Gameplay.Player");
        var combat = UnityEngine.Object.FindAnyObjectByType(combatType) as MonoBehaviour;
        if (combat == null) { Debug.LogError("[VerifyAttack] No PlayerCombat."); return "no combat"; }

        GameObject grunt = GameObject.FindGameObjectWithTag("Enemy");
        if (grunt == null) { Debug.LogError("[VerifyAttack] No Enemy."); return "no enemy"; }

        const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var tryAttack = combatType.GetMethod("TryAttack");
        var isAttacking = combatType.GetProperty("IsAttacking");
        var resolve = combatType.GetMethod("ResolveImpact", F);
        var swingField = combatType.GetField("_swing", F);

        // (1) WHIFF: stand far from every enemy and press attack — a swing must still start.
        combat.transform.position = grunt.transform.position + new Vector3(50f, 0f, 50f);
        tryAttack.Invoke(combat, null);
        bool whiffStarted = (bool)isAttacking.GetValue(combat);
        // Reset the swing so the next test can start cleanly.
        swingField.GetValue(combat).GetType().GetMethod("Cancel").Invoke(swingField.GetValue(combat), null);

        // (2) CONNECT: stand next to the Grunt, press, force the contact frame — damage must land.
        bool impacted = false;
        var evt = combatType.GetEvent("Impacted");
        Action<Vector3> handler = _ => impacted = true;
        evt.AddEventHandler(combat, handler);

        combat.transform.position = grunt.transform.position + new Vector3(1.2f, 0f, 0f);
        tryAttack.Invoke(combat, null);
        bool hitSwingStarted = (bool)isAttacking.GetValue(combat);
        resolve.Invoke(combat, null);

        evt.RemoveEventHandler(combat, handler);

        string msg = $"[VerifyAttack] whiffStartsSwing={whiffStarted} connectStartsSwing={hitSwingStarted} damageLandedAtImpact={impacted}";
        Debug.Log(msg);
        return msg;
    }
}
