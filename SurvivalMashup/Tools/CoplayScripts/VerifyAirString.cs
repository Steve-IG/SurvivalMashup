using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ToyChest.Gameplay.Combat;
using ToyChest.Gameplay.Enemy;
using ToyChest.Gameplay.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Play-mode validation of aerial combos, stage 1 (Combo_Test_AirString: AirCombo_Launcher → 1 → 2 → 3, _0H clips).
/// Enter play from Bootstrap.unity, then run. The combo is selected by walking onto the AirString pad and every press
/// is a real keyboard event (Q attack, Space jump), so it runs the normal input → combo → Animator → height profile →
/// PlayerLocomotion path. Checks:
/// <list type="bullet">
/// <item>the controller really leaves the ground (altitude, not just the pose) and is airborne whenever a step chains —
///   no snap to the floor between steps (largest single-frame drop at a step change);</item>
/// <item>gravity is held exactly while an aerial step is committed, and the finisher lands the player, who then moves;</item>
/// <item>air strikes connect at altitude: a target held level with the player, just ahead (as a launched enemy would be),
///   is struck by the air windows; ground dummies take the launcher's kick and the finisher's shockwave;</item>
/// <item>stopping after the launcher lands the player (nothing strands in the air);</item>
/// <item>a jump opens the air string mid-air, while a ground combo still refuses to start in the air (it buffers and
///   fires on landing).</item>
/// </list>
/// Output: console [VerifyAirString] + Temp/VerifyAirString.txt. Reads public seams only; test scaffolding (placing
/// dummies, holding the "air target") is the only direct manipulation.
/// </summary>
public static class VerifyAirString
{
    public static string Execute()
    {
        var combat = Object.FindAnyObjectByType<PlayerCombat>();
        if (combat == null) return "no PlayerCombat (enter play from Bootstrap)";
        new GameObject("~AirStringProbe").AddComponent<Probe>().Combat = combat;
        return "probe started; read [VerifyAirString]";
    }

    public sealed class Probe : MonoBehaviour
    {
        public PlayerCombat Combat;
        private readonly StringBuilder _log = new StringBuilder();
        private readonly List<(int step, string window, string target)> _hits = new List<(int, string, string)>();
        private readonly List<Transform> _dummies = new List<Transform>();
        private PlayerLocomotion _loco;
        private CharacterController _cc;
        private string _pendingWindow;
        private Transform _airTarget;
        private bool _allPass = true;
        private float _floor;
        private Key _north = Key.W, _south = Key.S;

        private void Check(bool ok, string line)
        {
            if (!ok) _allPass = false;
            _log.AppendLine((ok ? "  ok   " : "  FAIL ") + line);
        }

        private static void Keys(params Key[] k) => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(k));

        private float Altitude => Combat.transform.position.y - _cc.height * 0.5f - _cc.skinWidth - _floor;

        private void OnWindow(string id) => _pendingWindow = id;

        private void OnImpacted(Vector3 p)
        {
            Transform nearest = null;
            float best = float.MaxValue;
            foreach (Transform d in _dummies)
            {
                float dist = (d.position - p).sqrMagnitude;
                if (dist < best) { best = dist; nearest = d; }
            }

            _hits.Add((Combat.ComboStepIndex + 1, _pendingWindow, nearest == _airTarget ? "AIR" : nearest != null ? "ground" : "?"));
        }

        private IEnumerator Start()
        {
            _loco = Combat.GetComponent<PlayerLocomotion>();
            _cc = Combat.GetComponent<CharacterController>();
            _floor = 0f;
            ComboDefinition original = Combat.Combo;
            Vector3 cf = Camera.main.transform.forward; cf.y = 0f; cf.Normalize();
            if (Vector3.Dot(cf, Vector3.back) > 0.7f) { _north = Key.S; _south = Key.W; }
            _dummies.AddRange(Object.FindObjectsByType<TrainingDummy>().Select(d => d.transform));
            Combat.WindowStruck += OnWindow;
            Combat.Impacted += OnImpacted;

            // --- 1. Select through the pad ------------------------------------------------------------
            ComboLoadoutPad pad = Object.FindObjectsByType<ComboLoadoutPad>().FirstOrDefault(p => p.name == "ComboPad_AirString");
            Check(pad != null, "AirString pad present");
            if (pad == null) { Finish(original); yield break; }
            yield return Teleport(pad.transform.position + new Vector3(0f, 0f, 2.0f));
            float t0 = 0f;
            while (Combat.transform.position.z > pad.transform.position.z && t0 < 3f) { Keys(_south); t0 += Time.deltaTime; yield return null; }
            Keys();
            for (int i = 0; i < 10; i++) yield return null;
            ComboDefinition air = pad.Combo;
            Check(Combat.Combo == air, $"walked onto the AirString pad → {Combat.Combo?.name}");

            // --- 2. The full string from the ground, pressing ahead ----------------------------------
            Vector3 origin = new Vector3(-12f, 0f, -14f);
            _airTarget = _dummies[0];
            Place(new[] { new Vector3(0f, 0f, 1.0f) }, origin, skip: 1);           // ground dummy for the launcher kick
            yield return Face(origin);
            yield return RunString(air, air.StepCount, holdAirTarget: true, "full string");

            // --- 3. Stop after the launcher: the player must come back down ------------------------
            Place(new Vector3[0], origin);
            yield return Face(origin);
            yield return RunString(air, 1, holdAirTarget: false, "launcher only");

            // --- 4. Ground dummies around the landing take the finisher's shockwave ------------------
            yield return Face(origin);
            yield return GroundFinisherRing(air, origin);

            // --- 5. A jump opens the air string; a ground combo still waits for the floor ------------
            yield return JumpOpen(air, origin, original);

            Finish(original);
        }

        private void Finish(ComboDefinition original)
        {
            Keys();
            Combat.WindowStruck -= OnWindow;
            Combat.Impacted -= OnImpacted;
            Combat.SetCombo(original);
            Place(new Vector3[0], Vector3.zero);
            string verdict = _allPass ? "PASS" : "FAIL";
            _log.Insert(0, $"[VerifyAirString] => {verdict}\n");
            System.IO.File.WriteAllText("Temp/VerifyAirString.txt", _log.ToString());
            if (_allPass) Debug.Log(_log.ToString()); else Debug.LogError(_log.ToString());
            Destroy(gameObject);
        }

        // Dummies carry CharacterControllers and autoSyncTransforms is off: sync immediately after moving them.
        // Unused dummies are parked on the floor (±25m), far from the origin. `skip` leaves the first N for the probe.
        private void Place(Vector3[] offsets, Vector3 origin, int skip = 0)
        {
            int k = 0;
            for (int i = 0; i < _dummies.Count; i++)
            {
                if (i < skip) { _dummies[i].position = new Vector3(4f + 1.5f * i, 1f, -22f); continue; }
                _dummies[i].position = k < offsets.Length ? origin + offsets[k] + Vector3.up : new Vector3(4f + 1.5f * i, 1f, -22f);
                k++;
            }

            Physics.SyncTransforms();
        }

        private IEnumerator Teleport(Vector3 ground)
        {
            float wait = 0f;
            while ((Combat.IsAttacking || !_loco.IsGrounded) && wait < 5f) { wait += Time.deltaTime; yield return null; }
            _cc.enabled = false; Combat.transform.position = ground + Vector3.up * 1.1f; _cc.enabled = true;
            Physics.SyncTransforms();
            for (int i = 0; i < 10; i++) yield return null;
        }

        // Face +Z with a short real input (PlayerLocomotion keeps turning toward its last move direction).
        private IEnumerator Face(Vector3 origin)
        {
            yield return Teleport(origin + Vector3.back * 4f);
            for (int i = 0; i < 15; i++) { Keys(_north); yield return null; }
            Keys();
            for (int i = 0; i < 20; i++) yield return null;
            yield return Teleport(origin);
        }

        private IEnumerator Press()
        {
            Keys(Key.Q); yield return null; Keys(); yield return null;
        }

        private IEnumerator RunString(ComboDefinition combo, int steps, bool holdAirTarget, string label)
        {
            _hits.Clear();
            yield return Press();
            float t = 0f, peak = 0f, worstChainDrop = 0f, lowestAtChain = float.MaxValue, prevAlt = Altitude;
            int lastStep = 0, pressedFor = 0, heldMismatch = 0;
            var chainAlt = new List<string>();
            while (t < 8f && (Combat.IsAttacking || t < 0.2f))
            {
                int idx = Combat.ComboStepIndex;
                if (idx >= pressedFor && pressedFor + 1 < steps) { pressedFor = idx + 1; yield return Press(); }

                if (holdAirTarget && _airTarget != null)
                {
                    // A target floating level with the player, just ahead — what a launched enemy would be.
                    Vector3 f = Combat.transform.forward; f.y = 0f; f.Normalize();
                    _airTarget.position = Combat.transform.position + f * 1.1f;
                    Physics.SyncTransforms();
                }

                yield return null;
                t += Time.deltaTime;
                float alt = Altitude;
                peak = Mathf.Max(peak, alt);
                bool aerial = Combat.IsMovementCommitted && idx >= 0 && combo.Steps[idx].Attack.IsAerial;
                if (aerial != _loco.IsAerialHeld) heldMismatch++;
                int now = Combat.ComboStepIndex;
                if (now > lastStep)
                {
                    worstChainDrop = Mathf.Max(worstChainDrop, prevAlt - alt);
                    lowestAtChain = Mathf.Min(lowestAtChain, alt);
                    chainAlt.Add($"step{now + 1}@{alt:0.00}m");
                    lastStep = now;
                }

                prevAlt = alt;
            }

            float land = 0f;
            while (!_loco.IsGrounded && land < 3f) { land += Time.deltaTime; yield return null; }
            for (int i = 0; i < 5; i++) yield return null;
            Vector3 before = Combat.transform.position;
            float walk = 0f;
            while (walk < 0.3f) { Keys(_north); walk += Time.deltaTime; yield return null; }
            Keys();
            float moved = Vector3.Distance(before, Combat.transform.position);

            Check(lastStep == steps - 1, $"{label}: ran {lastStep + 1}/{steps} steps");
            Check(peak > 1.0f, $"{label}: the controller left the ground — peak altitude {peak:0.00}m");
            if (steps > 1)
            {
                Check(lowestAtChain > 1.0f && worstChainDrop < 0.3f,
                    $"{label}: airborne at every chain [{string.Join(" ", chainAlt)}], largest drop at a step change {worstChainDrop:0.00}m (no snap)");
            }

            Check(heldMismatch == 0, $"{label}: gravity held exactly while an aerial step is committed ({heldMismatch} mismatched frames)");
            Check(_loco.IsGrounded && !_loco.IsAerialHeld && land < 3f, $"{label}: landed {land:0.00}s after the combo ended; hold released");
            Check(moved > 1.0f, $"{label}: moves after landing ({moved:0.00}m in 0.3s)");
            if (holdAirTarget)
            {
                var air = _hits.Where(h => h.target == "AIR").Select(h => $"s{h.step}:{h.window}").ToList();
                var ground = _hits.Where(h => h.target == "ground").Select(h => $"s{h.step}:{h.window}").ToList();
                Check(air.Count >= 3 && _hits.Where(h => h.target == "AIR").Select(h => h.step).Distinct().Count() >= 3,
                    $"{label}: air windows connect at altitude with a target level with the player [{string.Join(", ", air)}]");
                _log.AppendLine($"       ground dummy: [{string.Join(", ", ground)}]");
            }
        }

        private IEnumerator GroundFinisherRing(ComboDefinition combo, Vector3 origin)
        {
            // The whole string travels ~6.8m forward; ring the expected landing spot with ground dummies.
            _hits.Clear();
            yield return Press();
            int pressedFor = 0;
            float t = 0f;
            bool placed = false;
            while (t < 8f && (Combat.IsAttacking || t < 0.2f))
            {
                int idx = Combat.ComboStepIndex;
                if (idx >= pressedFor && pressedFor + 1 < combo.StepCount) { pressedFor = idx + 1; yield return Press(); }
                if (!placed && idx == combo.StepCount - 1)
                {
                    // On the finisher, place three ground dummies around where the dive will come down.
                    Vector3 p = Combat.transform.position; p.y = 0f;
                    Vector3 f = Combat.transform.forward; f.y = 0f; f.Normalize();
                    Vector3 r = Vector3.Cross(Vector3.up, f);
                    Place(new[] { f * 1.6f, f * 1.0f + r * 1.2f, f * 1.0f - r * 1.2f }.Select(o => o + (p - origin)).ToArray(), origin);
                    placed = true;
                }

                yield return null;
                t += Time.deltaTime;
            }

            var slam = _hits.Where(h => h.step == combo.StepCount && h.window == "Landing slam").ToList();
            Check(slam.Count >= 2, $"finisher shockwave: 'Landing slam' struck {slam.Count} ground dummies around the landing (all hits: {string.Join(", ", _hits.Select(h => $"s{h.step}:{h.window}"))})");
            float land = 0f;
            while (!_loco.IsGrounded && land < 3f) { land += Time.deltaTime; yield return null; }
        }

        private IEnumerator JumpOpen(ComboDefinition air, Vector3 origin, ComboDefinition ground)
        {
            Place(new Vector3[0], origin);
            yield return Face(origin);
            Keys(Key.Space); yield return null; Keys();
            float t = 0f;
            while (_loco.IsGrounded && t < 0.5f) { t += Time.deltaTime; yield return null; }
            for (int i = 0; i < 6; i++) yield return null; // rising
            bool airborneAtPress = !_loco.IsGrounded;
            yield return Press();
            bool started = Combat.IsAttacking;
            float peak = 0f;
            float u = 0f;
            while (Combat.IsAttacking && u < 3f) { peak = Mathf.Max(peak, Altitude); u += Time.deltaTime; yield return null; }
            Check(airborneAtPress && started, $"a jump opens the air string: airborne at the press {airborneAtPress}, launcher started {started}; peak {peak:0.00}m");
            float land = 0f;
            while (!_loco.IsGrounded && land < 3f) { land += Time.deltaTime; yield return null; }

            // A ground combo in the air: the press must not start it until the player has landed.
            Combat.SetCombo(ground);
            yield return Face(origin);
            Keys(Key.Space); yield return null; Keys();
            t = 0f;
            while (_loco.IsGrounded && t < 0.5f) { t += Time.deltaTime; yield return null; }
            for (int i = 0; i < 4; i++) yield return null;
            yield return Press();
            bool startedInAir = false;
            float w = 0f;
            while (!_loco.IsGrounded && w < 2f) { startedInAir |= Combat.IsAttacking; w += Time.deltaTime; yield return null; }
            bool firedOnLanding = false;
            for (int i = 0; i < 20; i++) { firedOnLanding |= Combat.IsAttacking; yield return null; }
            Check(!startedInAir, $"a ground combo does not start in the air (started mid-air: {startedInAir}; fired after landing: {firedOnLanding})");
            float e = 0f;
            while (Combat.IsAttacking && e < 4f) { e += Time.deltaTime; yield return null; }
        }
    }
}
