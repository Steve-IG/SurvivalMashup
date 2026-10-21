using System.Collections;
using System.Text;
using ToyChest.Gameplay.Combat;
using ToyChest.Gameplay.Player;
using UnityEngine;

/// <summary>
/// Play-mode verification of two input-spam regressions:
/// <list type="bullet">
/// <item><b>Attack mash</b> — mashing a single-step combo (Combo_Test_WeaponProxy, Combo_Test_Sword) restarts the same
///   animator state. If the Animator cannot re-enter that state, the clip runs out and holds its last pose while the
///   step stays committed (rooted, no hit windows) until the fallback timers expire. Measures the longest stretch the
///   player is committed while the step's clip is NOT advancing, and how many steps complete in the mash.</item>
/// <item><b>Standing roll</b> — a roll from rest must travel a real distance and keep moving through the tumble, and
///   a chained roll must restart the roll clip instead of playing over the previous roll's get-up.</item>
/// </list>
/// Reads public seams only; adds no gameplay. Output: console [VerifyMashAndRoll] + Temp/VerifyMashAndRoll.txt.
/// </summary>
public static class VerifyMashAndRoll
{
    private static readonly string[] MashCombos = { "Combo_Test_WeaponProxy", "Combo_Test_Sword" };

    // A committed step whose clip stalls longer than this is a freeze (a blend is 0.06s; a few frames of slack).
    private const float MaxStall = 0.15f;

    public static string Execute()
    {
        var combat = Object.FindAnyObjectByType<PlayerCombat>();
        if (combat == null)
        {
            Debug.LogError("[VerifyMashAndRoll] Missing PlayerCombat (enter play from Bootstrap).");
            return "missing player";
        }

        var probe = new GameObject("~MashAndRollProbe").AddComponent<Probe>();
        probe.Combat = combat;
        return "mash/roll probe started; read the console for [VerifyMashAndRoll]";
    }

    public sealed class Probe : MonoBehaviour
    {
        public PlayerCombat Combat;

        private readonly StringBuilder _log = new StringBuilder();
        private PlayerLocomotion _loco;
        private Animator _animator;
        private bool _allPass = true;
        private int _steps;
        private int _rolls;
        private Vector3 _home;

        private IEnumerator Start()
        {
            _loco = Combat.GetComponent<PlayerLocomotion>();
            _home = _loco.transform.position;
            _animator = Combat.GetComponentInChildren<Animator>();
            var bridge = Combat.GetComponent<PlayerInputBridge>();
            if (bridge != null) bridge.enabled = false;
            ComboDefinition original = Combat.Combo;
            Combat.Attacked += _ => _steps++;
            _loco.Rolled += () => _rolls++;

            foreach (string name in MashCombos)
            {
                yield return Mash(name);
            }

            yield return StandingRoll();
            yield return ChainedRolls();

            yield return Settle();
            Combat.SetCombo(original);
            if (bridge != null) bridge.enabled = true;

            string verdict = _allPass ? "PASS" : "FAIL";
            _log.Insert(0, $"[VerifyMashAndRoll] => {verdict}\n");
            System.IO.File.WriteAllText("Temp/VerifyMashAndRoll.txt", _log.ToString());
            if (_allPass) Debug.Log(_log.ToString()); else Debug.LogError(_log.ToString());
            Destroy(gameObject);
        }

        private IEnumerator Mash(string name)
        {
            ComboDefinition combo = FindCombo(name);
            if (combo == null) { Fail($"{name}: combo asset not found"); yield break; }

            yield return Settle();
            Combat.SetCombo(combo);
            yield return null;

            int stateHash = Animator.StringToHash(combo.Steps[0].AnimatorState);
            float elapsed = 0f, stall = 0f, worstStall = 0f, lastTime = -1f, pressClock = 0f;
            int startSteps = _steps;

            // Mash at ~12 presses/second for 4 seconds.
            while (elapsed < 4f)
            {
                pressClock -= Time.deltaTime;
                if (pressClock <= 0f)
                {
                    Combat.TryAttack();
                    pressClock = 1f / 12f;
                }

                yield return null;
                elapsed += Time.deltaTime;

                float t = StepTime(stateHash);
                bool advancing = t >= 0f && t < 1f && (lastTime < 0f || Mathf.Abs(t - lastTime) > 1e-5f);
                if (Combat.IsMovementCommitted && !advancing)
                {
                    stall += Time.deltaTime;
                    worstStall = Mathf.Max(worstStall, stall);
                }
                else
                {
                    stall = 0f;
                }

                lastTime = t;
            }

            int started = _steps - startSteps;
            bool ok = worstStall <= MaxStall && started >= 3;
            Report(ok, $"{name}: mash 4s -> {started} steps started, worst committed stall {worstStall:0.000}s (limit {MaxStall}s)");
        }

        // The step clip's normalized time, preferring the state being blended into; -1 when it is not playing.
        private float StepTime(int stateHash)
        {
            if (_animator.IsInTransition(0))
            {
                AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(0);
                if (next.shortNameHash == stateHash) return next.normalizedTime;
            }

            AnimatorStateInfo current = _animator.GetCurrentAnimatorStateInfo(0);
            return current.shortNameHash == stateHash ? current.normalizedTime : -1f;
        }

        private IEnumerator StandingRoll()
        {
            yield return Settle();
            FaceOpenGround();
            yield return null;
            Vector3 start = _loco.transform.position;
            _loco.Roll();
            float elapsed = 0f, movingFor = 0f;
            while (elapsed < 1.2f)
            {
                yield return null;
                elapsed += Time.deltaTime;
                if (_loco.CurrentPlanarSpeed > 0.5f) movingFor = elapsed;
            }

            Vector3 travel = _loco.transform.position - start;
            travel.y = 0f;
            bool ok = travel.magnitude >= 2.5f && movingFor >= 0.45f;
            Report(ok, $"standing roll: travelled {travel.magnitude:0.00}m, kept moving for {movingFor:0.00}s (need >=2.5m, >=0.45s)");
        }

        // Return the player to where it started (the mash's lunges can wedge it between dummies) and turn it toward
        // the direction with the most clear ground, so the roll measures travel, not a collision.
        private void FaceOpenGround()
        {
            Transform player = _loco.transform;
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.position = _home;
            cc.enabled = true;
            Physics.SyncTransforms();
            Vector3 center = player.TransformPoint(cc.center);
            float half = Mathf.Max(0f, cc.height * 0.5f - cc.radius);
            Vector3 bottom = center - Vector3.up * (half - 0.1f), top = center + Vector3.up * half;
            Vector3 best = player.forward;
            float bestClear = -1f;
            for (int i = 0; i < 16; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
                float clear = 10f;
                foreach (RaycastHit hit in Physics.CapsuleCastAll(bottom, top, cc.radius, dir, 10f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform.root != player.root && hit.distance > 0f) clear = Mathf.Min(clear, hit.distance);
                }

                if (clear > bestClear) { bestClear = clear; best = dir; }
            }

            player.rotation = Quaternion.LookRotation(best, Vector3.up);
            _log.AppendLine($"  (rolling toward {best} with {bestClear:0.0}m clear)");
        }

        private IEnumerator ChainedRolls()
        {
            yield return Settle();
            FaceOpenGround();
            int rollHash = Animator.StringToHash("Roll");
            int restarts = 0, rollsBefore = _rolls, seen = _rolls;
            float elapsed = 0f;

            // Press roll every frame for 2.5s: every accepted roll must restart the roll clip near its start.
            while (elapsed < 2.5f)
            {
                _loco.Roll();
                yield return null;
                elapsed += Time.deltaTime;

                if (_rolls != seen)
                {
                    seen = _rolls;
                    float waited = 0f;
                    bool restarted = false;
                    while (waited < 0.12f)
                    {
                        yield return null;
                        waited += Time.deltaTime;
                        elapsed += Time.deltaTime;
                        AnimatorStateInfo info = _animator.IsInTransition(0)
                            ? _animator.GetNextAnimatorStateInfo(0)
                            : _animator.GetCurrentAnimatorStateInfo(0);
                        if (info.shortNameHash == rollHash && info.normalizedTime < 0.2f) { restarted = true; break; }
                    }

                    if (restarted) restarts++;
                }
            }

            int rolls = _rolls - rollsBefore;
            Report(rolls >= 3 && restarts == rolls, $"chained rolls: {rolls} rolls, {restarts} restarted the roll clip");
        }

        private IEnumerator Settle()
        {
            _loco.SetMoveInput(Vector2.zero);
            float t = 0f;
            while (t < 1.5f && (Combat.IsAttacking || _loco.IsRolling || _loco.CurrentPlanarSpeed > 0.05f || t < 0.6f))
            {
                yield return null;
                t += Time.deltaTime;
            }
        }

        private static ComboDefinition FindCombo(string name)
        {
            foreach (ComboDefinition c in Resources.FindObjectsOfTypeAll<ComboDefinition>())
            {
                if (c.name == name) return c;
            }

#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<ComboDefinition>($"Assets/Game/Content/Definitions/{name}.asset");
#else
            return null;
#endif
        }

        private void Report(bool ok, string line)
        {
            if (!ok) _allPass = false;
            _log.AppendLine((ok ? "  PASS " : "  FAIL ") + line);
        }

        private void Fail(string line) => Report(false, line);
    }
}
