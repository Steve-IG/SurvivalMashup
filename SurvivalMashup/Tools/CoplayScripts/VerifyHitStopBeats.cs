using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ToyChest.Gameplay.Combat;
using ToyChest.Gameplay.Enemy;
using ToyChest.Gameplay.Player;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Play-mode regression for the multi-target hit-stop stutter (RG5F follow-up). Runs the Sweeping combo into one
/// dummy and then into a three-dummy arc, logging real frame time, Time.timeScale and impacts per frame. Swept limbs
/// reach each body on a different frame, so per-impact hit-stop used to chain separate freezes (six in 0.4s on the
/// sweep kick). PASS when the arc produces no more separate freezes than the single target (one per beat = one hit
/// window of one step) and no real frame exceeds 50ms. Output: console [VerifyHitStopBeats] + Temp/VerifyHitStopBeats.txt.
/// </summary>
public static class VerifyHitStopBeats
{
    public static string Execute()
    {
        var p = new GameObject("~HitStopBeatsProbe").AddComponent<Probe>();
        p.Combat = Object.FindAnyObjectByType<PlayerCombat>();
        return "probe started; read [VerifyHitStopBeats]";
    }

    public sealed class Probe : MonoBehaviour
    {
        public PlayerCombat Combat;
        private int _impactsThisFrame;
        private string _window;
        private readonly StringBuilder _log = new StringBuilder();

        private IEnumerator Start()
        {
            var bridge = Combat.GetComponent<PlayerInputBridge>();
            bridge.enabled = false;
            var dummies = Object.FindObjectsByType<TrainingDummy>().Select(d => d.transform).ToList();
            Vector3 origin = new Vector3(-12f, 1f, -12f);
            var cc = Combat.GetComponent<CharacterController>();
            ComboDefinition original = Combat.Combo;
            Combat.SetCombo(AssetDatabase.LoadAssetAtPath<ComboDefinition>("Assets/Game/Content/Definitions/Combo_Test_Sweeping.asset"));

            var freezes = new Dictionary<int, int>();
            float worstFrame = 0f;
            foreach (int count in new[] { 1, 3 })
            {
                Vector3[] arc = count == 1 ? new[] { new Vector3(0f, 0f, 1.3f) }
                    : new[] { new Vector3(-0.9f, 0f, 1.0f), new Vector3(0f, 0f, 1.3f), new Vector3(0.9f, 0f, 1.0f) };
                for (int i = 0; i < dummies.Count; i++)
                    dummies[i].position = i < arc.Length ? origin + arc[i] : new Vector3(4f + 1.5f * i, 1f, -22f); // parked on the floor (±25m), far from the probe origin
                cc.enabled = false; Combat.transform.SetPositionAndRotation(origin, Quaternion.identity); cc.enabled = true;
                Physics.SyncTransforms();
                for (int i = 0; i < 30; i++) yield return null;

                Combat.Impacted += OnImpact; Combat.WindowStruck += OnWindow;
                _log.AppendLine($"=== Sweeping vs {count} dummies  (frame: realDt ms | timeScale | step | impacts)");
                Combat.TryAttack();
                bool pressed = false; int f = 0; float maxDt = 0f; int frozenFrames = 0, freezeRuns = 0; bool prevFrozen = false;
                while (f < 400 && (Combat.IsAttacking || f < 5))
                {
                    if (!pressed && Combat.ComboStepIndex == 0) { Combat.TryAttack(); pressed = true; }
                    yield return null;
                    float dt = Time.unscaledDeltaTime * 1000f;
                    bool frozen = Time.timeScale < 0.5f;
                    if (frozen) frozenFrames++;
                    if (frozen && !prevFrozen) freezeRuns++;
                    prevFrozen = frozen;
                    if (f > 3) maxDt = Mathf.Max(maxDt, dt);
                    if (_impactsThisFrame > 0 || frozen || dt > 25f)
                        _log.AppendLine($"  f{f,3}: {dt,6:0.0}ms | ts {Time.timeScale:0.00} | step {Combat.ComboStepIndex + 1} | impacts {_impactsThisFrame} {_window}");
                    _impactsThisFrame = 0; _window = null;
                    f++;
                }
                Combat.Impacted -= OnImpact; Combat.WindowStruck -= OnWindow;
                _log.AppendLine($"  summary: frames {f}, max real frame {maxDt:0.0}ms, frozen frames {frozenFrames}, separate freezes {freezeRuns}");
                freezes[count] = freezeRuns;
                worstFrame = Mathf.Max(worstFrame, maxDt);
                for (int i = 0; i < 40; i++) yield return null;
            }

            Combat.SetCombo(original);
            bridge.enabled = true;
            bool pass = freezes[3] <= freezes[1] && freezes[1] > 0 && worstFrame < 50f;
            string verdict = pass ? "PASS" : "FAIL";
            _log.Insert(0, $"[VerifyHitStopBeats] freezes 1 dummy={freezes[1]} 3 dummies={freezes[3]} worst real frame={worstFrame:0.0}ms => {verdict}\n");
            System.IO.File.WriteAllText("Temp/VerifyHitStopBeats.txt", _log.ToString());
            if (pass) Debug.Log(_log.ToString()); else Debug.LogError(_log.ToString());
            Destroy(gameObject);
        }

        private void OnImpact(Vector3 p) => _impactsThisFrame++;
        private void OnWindow(string id) => _window = (_window == null ? "" : _window + ",") + id;
    }
}
