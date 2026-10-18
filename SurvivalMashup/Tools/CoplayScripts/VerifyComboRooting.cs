using System.Collections;
using ToyChest.Gameplay.Enemy;
using ToyChest.Gameplay.Player;
using UnityEngine;

/// <summary>
/// Play-mode verification that the player moves ONLY by the attack's root motion during a combo — i.e. that
/// holding a movement input while attacking produces no input-driven sliding, and that movement resumes once
/// the combo ends. It disables the input bridge, holds a strong lateral input, mashes the combo, and checks
/// that the character's input velocity (CurrentPlanarSpeed) stays ~0 for the whole time it is attacking, then
/// rises after. Reads public seams only; adds no gameplay.
/// </summary>
public static class VerifyComboRooting
{
    public static string Execute()
    {
        var combat = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
        var dummy = UnityEngine.Object.FindAnyObjectByType<TrainingDummy>();
        if (combat == null || dummy == null)
        {
            Debug.LogError("[VerifyComboRooting] Missing PlayerCombat or TrainingDummy.");
            return "missing player/dummy";
        }

        var go = new GameObject("~ComboRootingProbe");
        var probe = go.AddComponent<Probe>();
        probe.Combat = combat;
        probe.Dummy = dummy;
        return "rooting probe started; read the console for [VerifyComboRooting]";
    }

    public sealed class Probe : MonoBehaviour
    {
        public PlayerCombat Combat;
        public TrainingDummy Dummy;

        private IEnumerator Start()
        {
            var loco = Combat.GetComponent<PlayerLocomotion>();
            var bridge = Combat.GetComponent<PlayerInputBridge>();
            if (loco == null)
            {
                Debug.LogError("[VerifyComboRooting] No PlayerLocomotion.");
                Destroy(gameObject);
                yield break;
            }

            // Silence the device input bridge so our held input is not overwritten each frame.
            if (bridge != null) bridge.enabled = false;
            for (int i = 0; i < 5; i++) yield return null;

            // Stand in front of the dummy, facing it.
            Transform player = Combat.transform;
            Transform target = Dummy.transform;
            player.position = target.position + new Vector3(0f, 0f, -1.3f);
            player.rotation = Quaternion.LookRotation((target.position - player.position).normalized, Vector3.up);
            Physics.SyncTransforms();
            yield return null;

            var held = new Vector2(1f, 0f); // hard lateral input held throughout

            // Kick off the combo and press-ahead a couple of times so it chains through all three steps.
            loco.SetMoveInput(held);
            Combat.TryAttack();
            float maxSpeedWhileAttacking = 0f;
            float elapsed = 0f;

            // Track input velocity for as long as the combo is active (with a safety timeout).
            while ((Combat.IsAttacking || elapsed < 0.2f) && elapsed < 5f)
            {
                loco.SetMoveInput(held);
                if (elapsed < 1.0f) Combat.TryAttack(); // buffer continuations to chain 0->1->2
                if (Combat.IsMovementCommitted)
                {
                    maxSpeedWhileAttacking = Mathf.Max(maxSpeedWhileAttacking, loco.CurrentPlanarSpeed);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // Combo ended: still holding input, movement should now resume.
            float speedAfter = 0f;
            for (int i = 0; i < 30; i++)
            {
                loco.SetMoveInput(held);
                speedAfter = Mathf.Max(speedAfter, loco.CurrentPlanarSpeed);
                yield return null;
            }

            loco.SetMoveInput(Vector2.zero);
            if (bridge != null) bridge.enabled = true;

            bool rootedWhileAttacking = maxSpeedWhileAttacking < 1.0f;
            bool resumedAfter = speedAfter > 3.0f;
            bool pass = rootedWhileAttacking && resumedAfter;
            string msg = $"[VerifyComboRooting] maxInputSpeedWhileAttacking={maxSpeedWhileAttacking:0.00} (want ~0) | " +
                         $"speedAfterCombo={speedAfter:0.00} (want >3) | rooted={rootedWhileAttacking} resumed={resumedAfter} " +
                         $"=> {(pass ? "PASS" : "FAIL")}";
            if (pass) Debug.Log(msg); else Debug.LogError(msg);

            Destroy(gameObject);
        }
    }
}
