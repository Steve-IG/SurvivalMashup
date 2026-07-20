using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Play-mode runtime check for the momentum-based roll. Finds the live PlayerLocomotion, simulates a
/// running entry by seeding planar velocity, fires Roll()/Jump(), and logs the resulting speeds so we
/// can confirm the roll inherits momentum + impulse without a hardware device. Public API + reflection
/// only; adds no gameplay.
/// </summary>
public static class VerifyRoll
{
    public static string Execute()
    {
        Type locoType = Type.GetType("ToyChest.Gameplay.Player.PlayerLocomotion, ToyChest.Gameplay.Player");
        if (locoType == null) { Debug.LogError("[VerifyRoll] PlayerLocomotion type not found."); return "no type"; }

        var loco = UnityEngine.Object.FindAnyObjectByType(locoType);
        if (loco == null) { Debug.LogError("[VerifyRoll] No PlayerLocomotion in scene."); return "no instance"; }

        const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        // Face + move forward, then seed a "running" entry velocity of 7 u/s.
        locoType.GetMethod("SetMoveInput").Invoke(loco, new object[] { new Vector2(0f, 1f) });
        FieldInfo planar = locoType.GetField("_planarVelocity", F);
        planar.SetValue(loco, new Vector3(0f, 0f, 7f));

        float grounded = (bool)locoType.GetProperty("IsGrounded").GetValue(loco) ? 1f : 0f;
        float entry = (float)locoType.GetProperty("CurrentPlanarSpeed").GetValue(loco);

        // Fire the roll — it should inherit the 7 u/s and add the impulse.
        locoType.GetMethod("Roll").Invoke(loco, null);
        bool rolling = (bool)locoType.GetProperty("IsRolling").GetValue(loco);
        float launch = (float)locoType.GetProperty("CurrentPlanarSpeed").GetValue(loco);

        // Fresh jump from a seeded grounded state (needs to be grounded to launch).
        planar.SetValue(loco, Vector3.zero);
        // Clear the roll timer so Jump is not blocked, mimicking a separate grounded moment.
        locoType.GetField("_rollTimer", F).SetValue(loco, 0f);
        locoType.GetMethod("Jump").Invoke(loco, null);
        float vVel = (float)locoType.GetField("_verticalVelocity", F).GetValue(loco);

        string msg = $"[VerifyRoll] grounded={grounded > 0.5f} entrySpeed={entry:0.0} rolling={rolling} " +
                     $"rollLaunchSpeed={launch:0.0} (expect ~15) jumpVVel={vVel:0.0}";
        Debug.Log(msg);
        return msg;
    }
}
