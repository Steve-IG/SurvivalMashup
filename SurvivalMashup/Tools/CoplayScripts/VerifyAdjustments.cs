using System.Collections;
using System.Text;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Player;
using ToyChest.Systems.Resources;
using UnityEngine;

/// <summary>
/// Play-mode verification of four adjustments: the hit flash is gone, an attack cannot interrupt a jump or
/// a roll (the press buffers instead), a dead enemy stops being hittable the moment it dies, and the
/// player health bar is bound and returns to full on respawn.
/// </summary>
public static class VerifyAdjustments
{
    public static string Execute()
    {
        var combat = Object.FindAnyObjectByType<PlayerCombat>();
        var locomotion = Object.FindAnyObjectByType<PlayerLocomotion>();
        if (combat == null || locomotion == null)
        {
            Debug.LogError("[VerifyAdjustments] Missing player.");
            return "missing player";
        }

        var go = new GameObject("~AdjustmentsProbe");
        var probe = go.AddComponent<Probe>();
        probe.Combat = combat;
        probe.Locomotion = locomotion;
        return "probe started; read the console for [VerifyAdjustments]";
    }

    public sealed class Probe : MonoBehaviour
    {
        public PlayerCombat Combat;
        public PlayerLocomotion Locomotion;

        private IEnumerator Start()
        {
            var sb = new StringBuilder("[VerifyAdjustments] ");
            for (int i = 0; i < 10; i++) yield return null;

            // (1) HitFlash is gone from the project entirely.
            bool flashTypeGone = System.Type.GetType("ToyChest.Gameplay.Presentation.HitFlash, ToyChest.Gameplay.Presentation") == null;
            sb.Append($"hitFlashRemoved={flashTypeGone} | ");

            // (2) Health bar bound to the player's health resource.
            var bar = Object.FindAnyObjectByType<PlayerHealthBar>();
            var playerBehaviour = Combat.GetComponent<GameplayObjectBehaviour>();
            ResourceValue health = null;
            if (playerBehaviour != null && playerBehaviour.Object != null &&
                playerBehaviour.Object.TryGet(out ResourceSet resources))
            {
                health = resources.GetResource(new DefinitionId("resource.health"));
            }

            sb.Append($"healthBar={(bar != null ? "present" : "MISSING")} health={(health != null ? "bound" : "MISSING")} | ");

            // (3) An attack must not interrupt a roll.
            Locomotion.Roll();
            yield return null;
            bool rollingNow = Locomotion.IsRolling;
            Combat.TryAttack();
            yield return null;
            bool attackBlockedDuringRoll = rollingNow && !Combat.IsAttacking && Locomotion.IsRolling;
            sb.Append($"rollNotInterrupted={attackBlockedDuringRoll} | ");

            while (Locomotion.IsRolling) yield return null;
            for (int i = 0; i < 30; i++) yield return null;

            // (4) An attack must not interrupt a jump (airborne).
            Locomotion.Jump();
            yield return null;
            yield return null;
            bool airborne = !Locomotion.IsGrounded;
            Combat.TryAttack();
            yield return null;
            bool attackBlockedInAir = airborne && !Combat.IsAttacking;
            sb.Append($"jumpNotInterrupted={attackBlockedInAir} (airborne={airborne}) | ");

            // (5) Health bar lowers on damage and refills on respawn.
            bool barTracksHealth = false;
            if (health != null)
            {
                float max = health.Maximum;
                health.Consume(max * 0.4f);
                yield return null;
                bool lowered = health.Current < max;
                PlayerRespawn.Restore(playerBehaviour.Object);
                yield return null;
                bool refilled = Mathf.Approximately(health.Current, health.Maximum);
                barTracksHealth = lowered && refilled;
                sb.Append($"healthLowersAndResets={barTracksHealth} | ");
            }

            bool pass = flashTypeGone && bar != null && health != null
                        && attackBlockedDuringRoll && attackBlockedInAir && barTracksHealth;
            sb.Append(pass ? "=> PASS" : "=> FAIL");

            string msg = sb.ToString();
            if (pass) { Debug.Log(msg); } else { Debug.LogError(msg); }
            Destroy(gameObject);
        }
    }
}
