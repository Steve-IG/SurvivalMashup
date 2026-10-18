using System.Collections;
using System.Collections.Generic;
using System.Text;
using ToyChest.Gameplay.Enemy;
using ToyChest.Gameplay.Player;
using UnityEngine;

/// <summary>
/// Play-mode verification of the RG5D player combo, run from a Bootstrap-composed session. It drives
/// PlayerCombat.TryAttack directly (no input device) while standing next to a Training Dummy and records:
///  - the ordered animator triggers PlayerCombat raises (expect Attack -> Combo2 -> Combo3);
///  - that the live Animator actually has the Combo2/Combo3 parameters (proves the prefab uses the edited
///    controller at runtime, not just on disk);
///  - the number of confirmed Impacted events (one per landed step);
///  - that mashing terminates and restarts the combo (a fresh Attack after the third hit).
/// Reads public seams only; adds no gameplay.
/// </summary>
public static class VerifyCombo
{
    public static string Execute()
    {
        var combat = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
        var dummy = UnityEngine.Object.FindAnyObjectByType<TrainingDummy>();
        if (combat == null || dummy == null)
        {
            Debug.LogError("[VerifyCombo] Missing PlayerCombat or TrainingDummy.");
            return "missing player/dummy";
        }

        var go = new GameObject("~ComboProbe");
        var probe = go.AddComponent<Probe>();
        probe.Combat = combat;
        probe.Dummy = dummy;
        return "combo probe started; read the console for [VerifyCombo]";
    }

    public sealed class Probe : MonoBehaviour
    {
        public PlayerCombat Combat;
        public TrainingDummy Dummy;

        private readonly List<string> _triggers = new List<string>();
        private int _impacted;

        private void OnAttacked(string trigger) => _triggers.Add(trigger);
        private void OnImpacted(Vector3 p) => _impacted++;

        private IEnumerator Start()
        {
            Combat.Attacked += OnAttacked;
            Combat.Impacted += OnImpacted;
            for (int i = 0; i < 10; i++) yield return null;

            // Does the live animator actually carry the combo triggers? (Proves the runtime controller.)
            Animator animator = Combat.GetComponentInChildren<Animator>();
            bool hasCombo2 = HasParam(animator, "Combo2");
            bool hasCombo3 = HasParam(animator, "Combo3");

            // Stand just in front of the dummy, facing it, so every step lands.
            Transform player = Combat.transform;
            Transform target = Dummy.transform;
            player.position = target.position + new Vector3(0f, 0f, -1.3f);
            player.rotation = Quaternion.LookRotation((target.position - player.position).normalized, Vector3.up);
            Physics.SyncTransforms();
            yield return null;

            // Sum across every dummy: a finisher whose hit lands at the end of a leap may strike a neighbour.
            int hitsBefore = TotalDummyHits();
            _triggers.Clear();
            _impacted = 0;

            // Mash attack for ~1.1s: press-ahead buffers each continuation, so the sequencer should chain
            // Attack -> Combo2 -> Combo3, then terminate and start a fresh Attack.
            float t = 0f;
            while (t < 1.15f)
            {
                Combat.TryAttack();
                yield return new WaitForSeconds(0.1f);
                t += 0.1f;
            }

            yield return new WaitForSeconds(0.4f);

            int hitsAfter = TotalDummyHits();
            var sb = new StringBuilder();
            for (int i = 0; i < _triggers.Count; i++)
            {
                sb.Append(_triggers[i]);
                if (i < _triggers.Count - 1) sb.Append(" -> ");
            }

            bool chainedInOrder = _triggers.Count >= 3 &&
                                  _triggers[0] == "Attack" && _triggers[1] == "Combo2" && _triggers[2] == "Combo3";
            bool restarted = _triggers.Count >= 4 && _triggers[3] == "Attack";
            bool landedEachStep = _impacted >= 3 && hitsAfter - hitsBefore >= 3;

            bool pass = chainedInOrder && landedEachStep && hasCombo2 && hasCombo3;
            string msg = $"[VerifyCombo] triggers=[{sb}] | chainedAttack->Combo2->Combo3={chainedInOrder} | " +
                         $"restartedAfter3={restarted} | impacts={_impacted} dummyHits+={hitsAfter - hitsBefore} | " +
                         $"animatorHasCombo2/3={hasCombo2}/{hasCombo3} => {(pass ? "PASS" : "FAIL")}";
            if (pass) Debug.Log(msg); else Debug.LogError(msg);

            Combat.Attacked -= OnAttacked;
            Combat.Impacted -= OnImpacted;
            Destroy(gameObject);
        }

        private static int TotalDummyHits()
        {
            int total = 0;
            foreach (TrainingDummy d in Object.FindObjectsByType<TrainingDummy>())
            {
                total += d.HitsAbsorbed;
            }

            return total;
        }

        private static bool HasParam(Animator animator, string name)
        {
            if (animator == null) return false;
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.name == name) return true;
            }

            return false;
        }
    }
}
