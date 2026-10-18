using System.Collections;
using ToyChest.Gameplay.Combat;
using ToyChest.Gameplay.Enemy;
using ToyChest.Gameplay.Player;
using UnityEngine;

/// <summary>
/// Play-mode verification of the OnAttackEnd exit mechanism, independent of where the clip's event sits. It
/// starts a single attack, waits until it is past its contact frame (recovering), confirms the player is
/// rooted while a movement input is held, then fires PlayerCombat.NotifyAttackEnd() (what the animation event
/// calls) and confirms the attack ends that frame and movement resumes immediately — the snappy exit. Reads
/// public seams only; adds no gameplay.
/// </summary>
public static class VerifyAttackEnd
{
    public static string Execute()
    {
        var combat = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
        var dummy = UnityEngine.Object.FindAnyObjectByType<TrainingDummy>();
        if (combat == null || dummy == null)
        {
            Debug.LogError("[VerifyAttackEnd] Missing PlayerCombat or TrainingDummy.");
            return "missing player/dummy";
        }

        var go = new GameObject("~AttackEndProbe");
        var probe = go.AddComponent<Probe>();
        probe.Combat = combat;
        probe.Dummy = dummy;
        return "attack-end probe started; read the console for [VerifyAttackEnd]";
    }

    public sealed class Probe : MonoBehaviour
    {
        public PlayerCombat Combat;
        public TrainingDummy Dummy;

        private IEnumerator Start()
        {
            var loco = Combat.GetComponent<PlayerLocomotion>();
            var bridge = Combat.GetComponent<PlayerInputBridge>();
            if (bridge != null) bridge.enabled = false;
            for (int i = 0; i < 5; i++) yield return null;

            Transform player = Combat.transform;
            Transform target = Dummy.transform;
            player.position = target.position + new Vector3(0f, 0f, -1.3f);
            player.rotation = Quaternion.LookRotation((target.position - player.position).normalized, Vector3.up);
            Physics.SyncTransforms();
            yield return null;

            var held = new Vector2(1f, 0f);
            loco.SetMoveInput(held);
            yield return null;

            Combat.TryAttack(); // start the attack

            // Wait until past the contact frame (recovering), holding input the whole time.
            float t = 0f;
            while (Combat.Phase == ComboPhase.WindUp && t < 1.5f)
            {
                loco.SetMoveInput(held);
                t += Time.deltaTime;
                yield return null;
            }

            // Confirm rooted during the committed (post-contact) motion.
            float rootedSpeed = 0f;
            for (int i = 0; i < 6; i++)
            {
                loco.SetMoveInput(held);
                rootedSpeed = Mathf.Max(rootedSpeed, loco.CurrentPlanarSpeed);
                yield return null;
            }

            bool wasAttacking = Combat.IsAttacking;

            // Fire the attack-end event (what the animation's OnAttackEnd dispatches).
            Combat.NotifyAttackEnd();
            yield return null; // one frame to apply

            bool endedImmediately = !Combat.IsAttacking;

            // Still holding input: movement should now resume promptly.
            float speedAfter = 0f;
            for (int i = 0; i < 25; i++)
            {
                loco.SetMoveInput(held);
                speedAfter = Mathf.Max(speedAfter, loco.CurrentPlanarSpeed);
                yield return null;
            }

            loco.SetMoveInput(Vector2.zero);
            if (bridge != null) bridge.enabled = true;

            bool pass = wasAttacking && rootedSpeed < 1.0f && endedImmediately && speedAfter > 3.0f;
            string msg = $"[VerifyAttackEnd] rootedWhileAttacking(maxSpeed)={rootedSpeed:0.00} (want ~0) | " +
                         $"endedOnEventFrame={endedImmediately} | speedAfterExit={speedAfter:0.00} (want >3) => {(pass ? "PASS" : "FAIL")}";
            if (pass) Debug.Log(msg); else Debug.LogError(msg);

            Destroy(gameObject);
        }
    }
}
