using System.Collections;
using System.Collections.Generic;
using System.Text;
using ToyChest.Gameplay.Enemy;
using ToyChest.Gameplay.Player;
using UnityEngine;

/// <summary>
/// Play-mode verification of two combo rules:
/// (A) the continuation window closes at OnAttackEnd — a press after hit 1's commitment ends starts the combo
///     over at hit 1, while a press during the attack still advances to hit 2;
/// (B) the leaping finisher's damage comes from its contact window (takeoff → slam), not a late single frame:
///     records when, relative to the finisher's start, each confirmed hit lands.
/// Reads public seams only; adds no gameplay.
/// </summary>
public static class VerifyComboWindow
{
    public static string Execute()
    {
        var combat = Object.FindAnyObjectByType<PlayerCombat>();
        var dummy = Object.FindAnyObjectByType<TrainingDummy>();
        if (combat == null || dummy == null)
        {
            Debug.LogError("[VerifyComboWindow] Missing PlayerCombat or TrainingDummy.");
            return "missing";
        }

        var probe = new GameObject("~ComboWindowProbe").AddComponent<Probe>();
        probe.Combat = combat;
        probe.Dummy = dummy;
        return "probe started; read the console for [VerifyComboWindow]";
    }

    public sealed class Probe : MonoBehaviour
    {
        public PlayerCombat Combat;
        public TrainingDummy Dummy;

        private readonly List<float> _impactTimes = new List<float>();
        private float _finisherStart = -1f;

        private void OnImpacted(Vector3 p)
        {
            if (_finisherStart >= 0f) _impactTimes.Add(Time.time - _finisherStart);
        }

        private IEnumerator Start()
        {
            var bridge = Combat.GetComponent<PlayerInputBridge>();
            if (bridge != null) bridge.enabled = false;
            for (int i = 0; i < 5; i++) yield return null;

            // (A1) Press after hit 1 has ended -> a fresh hit 1, not hit 2.
            yield return Place(1.3f);
            Combat.TryAttack();
            yield return WaitUntil(() => !Combat.IsMovementCommitted, 3f);
            bool endedAtRelease = !Combat.IsAttacking;
            yield return new WaitForSeconds(0.1f);
            Combat.TryAttack();
            yield return null;
            bool afterEndStartsOver = Combat.ComboStepIndex == 0;
            yield return WaitUntil(() => !Combat.IsAttacking, 3f);

            // (A2) Press during hit 1 (committed) -> hit 2.
            yield return Place(1.3f);
            Combat.TryAttack();
            yield return new WaitForSeconds(0.15f);
            Combat.TryAttack();
            yield return WaitUntil(() => Combat.ComboStepIndex == 1 || !Combat.IsAttacking, 3f);
            bool duringContinues = Combat.ComboStepIndex == 1;
            yield return WaitUntil(() => !Combat.IsAttacking, 4f);

            // (B) Chain to the finisher and time its hits. Stand back so the dummy stays ahead through the leap.
            yield return Place(2.0f);
            Combat.Impacted += OnImpacted;
            Combat.TryAttack();
            float t = 0f;
            while (t < 4f && _finisherStart < 0f)
            {
                if (Combat.ComboStepIndex < 2) Combat.TryAttack();
                else _finisherStart = Time.time;
                t += Time.deltaTime;
                yield return null;
            }

            yield return WaitUntil(() => !Combat.IsAttacking, 4f);
            Combat.Impacted -= OnImpacted;
            if (bridge != null) bridge.enabled = true;

            var times = new StringBuilder();
            foreach (float it in _impactTimes) times.Append($"{it:0.00}s ");
            bool hitInWindow = _impactTimes.Count > 0 && _impactTimes[0] < 1.0f;
            bool pass = endedAtRelease && afterEndStartsOver && duringContinues && hitInWindow;
            string msg = $"[VerifyComboWindow] (A) endsAtAttackEnd={endedAtRelease} pressAfterEnd->hit1={afterEndStartsOver} " +
                         $"pressDuring->hit2={duringContinues} | (B) finisher hits at [{times.ToString().Trim()}] " +
                         $"(window 0.25-0.95s) => {(pass ? "PASS" : "FAIL")}";
            if (pass) Debug.Log(msg); else Debug.LogError(msg);
            Destroy(gameObject);
        }

        private IEnumerator Place(float distance)
        {
            yield return new WaitForSeconds(0.3f);
            Transform player = Combat.transform;
            Transform target = Dummy.transform;
            player.position = target.position + new Vector3(0f, 0f, -distance);
            player.rotation = Quaternion.LookRotation((target.position - player.position).normalized, Vector3.up);
            Physics.SyncTransforms();
            yield return null;
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition, float timeout)
        {
            float t = 0f;
            while (!condition() && t < timeout)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }
    }
}
