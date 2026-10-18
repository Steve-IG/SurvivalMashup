using System.Collections;
using System.Collections.Generic;
using System.Text;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Enemy;
using ToyChest.Gameplay.HitDetection;
using ToyChest.Gameplay.Player;
using UnityEngine;

/// <summary>
/// Diagnostic: plays the leaping finisher with a dummy ahead and samples, every frame of the finisher, whether
/// the step's live hit volume (as currently anchored) contains the dummy — ignoring the once-per-target rule —
/// and where the volume's origin is. Prints the time ranges of coverage so the anchor can be judged against
/// the contact window (takeoff → slam). Reads public seams only; adds no gameplay.
/// </summary>
public static class VerifyLeapCoverage
{
    public static string Execute()
    {
        var combat = Object.FindAnyObjectByType<PlayerCombat>();
        var dummy = Object.FindAnyObjectByType<TrainingDummy>();
        var probe = new GameObject("~LeapCoverageProbe").AddComponent<Probe>();
        probe.Combat = combat;
        probe.Dummy = dummy;
        return "probe started; read the console for [VerifyLeapCoverage]";
    }

    public sealed class Probe : MonoBehaviour
    {
        public PlayerCombat Combat;
        public TrainingDummy Dummy;

        private IEnumerator Start()
        {
            var bridge = Combat.GetComponent<PlayerInputBridge>();
            if (bridge != null) bridge.enabled = false;
            GameplayObject self = Combat.GetComponent<GameplayObjectBehaviour>().Object;
            GameplayObject dummyObj = Dummy.GetComponent<GameplayObjectBehaviour>().Object;
            var detector = new HitDetector();

            yield return new WaitForSeconds(0.3f);
            Transform player = Combat.transform;
            player.position = Dummy.transform.position + new Vector3(0f, 0f, -2.0f);
            player.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            Physics.SyncTransforms();
            yield return null;

            Combat.TryAttack();
            float start = -1f;
            float t = 0f;
            while (t < 4f && start < 0f)
            {
                if (Combat.ComboStepIndex < 2) Combat.TryAttack();
                else start = Time.time;
                t += Time.deltaTime;
                yield return null;
            }

            var sb = new StringBuilder();
            bool inside = false;
            float rangeStart = 0f;
            var ranges = new List<string>();
            float maxOriginY = float.MinValue;
            while (Combat.ComboStepIndex == 2 && Time.time - start < 2.2f)
            {
                float now = Time.time - start;
                HitVolumeEmitter emitter = Combat.PrimaryAttackVolume;
                bool contains = false;
                foreach (HitResult r in emitter.Detect(self, detector))
                {
                    if (r.Object == dummyObj) contains = true;
                }

                maxOriginY = Mathf.Max(maxOriginY, emitter.Origin.y - player.position.y);
                if (contains && !inside) { inside = true; rangeStart = now; }
                if (!contains && inside) { inside = false; ranges.Add($"{rangeStart:0.00}-{now:0.00}s"); }
                yield return null;
            }

            if (inside) ranges.Add($"{rangeStart:0.00}-end");
            if (bridge != null) bridge.enabled = true;
            Debug.Log($"[VerifyLeapCoverage] dummy inside finisher volume during: [{string.Join(", ", ranges)}] " +
                      $"(window 0.25-0.95s) | volume origin peaked {maxOriginY:0.00}m above the character root");
            Destroy(gameObject);
        }
    }
}
