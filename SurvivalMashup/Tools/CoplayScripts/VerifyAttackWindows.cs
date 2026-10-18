using System.Collections;
using System.Collections.Generic;
using System.Text;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Enemy;
using ToyChest.Gameplay.Player;
using UnityEngine;

/// <summary>
/// Play-mode verification of the limb-window attack model: plays the full combo against a single training
/// dummy at several distances and records which hit window landed each hit and when (relative to that step's
/// start). Expectation: each window strikes the dummy at most once; the finisher can hit it up to three times
/// (right kick, left kick, slam). Also checks no hit ever comes from the same window twice in one step.
/// Reads public seams only; adds no gameplay.
/// </summary>
public static class VerifyAttackWindows
{
    public static string Execute()
    {
        var probe = new GameObject("~AttackWindowsProbe").AddComponent<Probe>();
        probe.Combat = Object.FindAnyObjectByType<PlayerCombat>();
        probe.Dummy = Object.FindAnyObjectByType<TrainingDummy>();
        return "probe started; read the console for [VerifyAttackWindows]";
    }

    public sealed class Probe : MonoBehaviour
    {
        public PlayerCombat Combat;
        public TrainingDummy Dummy;

        private readonly List<string> _log = new List<string>();
        private readonly Dictionary<string, int> _perStepWindowHits = new Dictionary<string, int>();
        private int _stepAtStart = -1;
        private float _stepStart;
        private bool _duplicate;

        private void OnWindowStruck(string window)
        {
            int step = Combat.ComboStepIndex;
            if (step != _stepAtStart)
            {
                _stepAtStart = step;
                _stepStart = Time.time;
                _perStepWindowHits.Clear();
            }

            _perStepWindowHits.TryGetValue(window, out int n);
            _perStepWindowHits[window] = n + 1;
            if (n + 1 > 1) _duplicate = true;
            _log.Add($"hit{step + 1}:{window}@{Time.time - _stepStart:0.00}s");
        }

        private IEnumerator Start()
        {
            var bridge = Combat.GetComponent<PlayerInputBridge>();
            if (bridge != null) bridge.enabled = false;

            // Isolate one target: park every other enemy far away so hits can only come from this dummy.
            foreach (var behaviour in Object.FindObjectsByType<GameplayObjectBehaviour>())
            {
                if (behaviour.CompareTag("Enemy") && behaviour.gameObject != Dummy.gameObject)
                {
                    behaviour.transform.position += new Vector3(200f, 0f, 200f);
                }
            }

            Physics.SyncTransforms();
            Combat.WindowStruck += OnWindowStruck;
            var summary = new StringBuilder();

            foreach (float distance in new[] { 0.9f, 1.3f, 1.7f })
            {
                yield return new WaitForSeconds(0.4f);
                Transform player = Combat.transform;
                player.position = Dummy.transform.position + new Vector3(0f, 0f, -distance);
                player.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                Physics.SyncTransforms();
                yield return null;

                _log.Clear();
                _stepAtStart = -1;
                float t = 0f;
                Combat.TryAttack();
                while (t < 6f && (Combat.IsAttacking || t < 0.2f))
                {
                    if (Combat.ComboStepIndex < 2) Combat.TryAttack(); // chain through to the finisher
                    t += Time.deltaTime;
                    yield return null;
                }

                summary.Append($" | d={distance:0.0}m: {string.Join(" ", _log)}");
            }

            Combat.WindowStruck -= OnWindowStruck;
            if (bridge != null) bridge.enabled = true;

            bool finisherMultiHit = summary.ToString().Contains("hit3:Right kick") || summary.ToString().Contains("hit3:Left kick");
            bool slam = summary.ToString().Contains("hit3:Slam");
            bool pass = !_duplicate && finisherMultiHit && slam;
            string msg = $"[VerifyAttackWindows] noWindowHitTwice={!_duplicate} finisherKicks={finisherMultiHit} finisherSlam={slam}{summary} => {(pass ? "PASS" : "FAIL")}";
            if (pass) Debug.Log(msg); else Debug.LogError(msg);
            Destroy(gameObject);
        }
    }
}
