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
/// Play-mode acceptance probe for the dojo loadout pads (RG5F). Enter play from <c>Bootstrap.unity</c>, then run.
///
/// <para>Everything goes through the normal gameplay path: keyboard state is queued into the Input System (W/A/S/D
/// to walk, Q to attack), so <c>PlayerInputBridge</c> → <c>PlayerLocomotion</c> moves the CharacterController onto a
/// pad, the pad's physics trigger fires, and <c>ComboLoadoutPad</c> → <c>PlayerCombat.SetCombo</c> equips the combo;
/// the attack presses then run input → combo → Animator → limb windows → HitDetector → ability against a Training
/// Dummy. (If the editor routes no injected device input to the game, it falls back to the bridge's own targets —
/// <c>SetMoveInput</c>/<c>TryAttack</c> — and says so.) Only the start position before each walk is placed directly.</para>
///
/// <para>Per pad: start from a known combo, walk on from a given direction, check the equipped combo, walk off, run the
/// full combo by pressing ahead, and check that the step triggers <see cref="PlayerCombat.Attacked"/> raised are this
/// combo's, in order, and that the windows that struck the dummy (<see cref="PlayerCombat.WindowStruck"/>) belong to
/// this combo's attacks. Also: re-entering the same pad, crossing two pads in one walk, a pad touched mid-attack
/// (deferred, the strike completes), and that a fresh session starts on the authored combo.
/// Output: console [VerifyComboPads] + Temp/VerifyComboPads.txt.</para>
/// </summary>
public static class VerifyComboPads
{
    public static string Execute()
    {
        var combat = Object.FindAnyObjectByType<PlayerCombat>();
        if (combat == null) return "no PlayerCombat (enter play from Bootstrap)";
        var probe = new GameObject("~ComboPadProbe").AddComponent<Probe>();
        probe.Combat = combat;
        return "pad probe started; read [VerifyComboPads]";
    }

    public sealed class Probe : MonoBehaviour
    {
        public PlayerCombat Combat;

        private readonly StringBuilder _log = new StringBuilder();
        private readonly List<string> _triggers = new List<string>();
        private readonly List<string> _windows = new List<string>();
        private readonly Dictionary<string, ComboLoadoutPad> _pads = new Dictionary<string, ComboLoadoutPad>();
        private PlayerLocomotion _loco;
        private PlayerInputBridge _bridge;
        private CharacterController _cc;
        private Transform _dummy;
        private bool _deviceInput = true;
        private bool _allPass = true;

        private void OnAttacked(string trigger) => _triggers.Add(trigger);
        private void OnWindow(string id) => _windows.Add(id);

        private void Check(bool ok, string line)
        {
            if (!ok) _allPass = false;
            _log.AppendLine((ok ? "  ok   " : "  FAIL ") + line);
        }

        private IEnumerator Start()
        {
            _loco = Combat.GetComponent<PlayerLocomotion>();
            _bridge = Combat.GetComponent<PlayerInputBridge>();
            _cc = Combat.GetComponent<CharacterController>();
            foreach (ComboLoadoutPad pad in Object.FindObjectsByType<ComboLoadoutPad>())
            {
                _pads[pad.name.Replace("ComboPad_", string.Empty)] = pad;
            }

            TrainingDummy lone = Object.FindObjectsByType<TrainingDummy>()
                .OrderBy(d => Mathf.Abs(d.transform.position.x) + Mathf.Abs(d.transform.position.z - 4f)).FirstOrDefault();
            _dummy = lone != null ? lone.transform : null;
            Combat.Attacked += OnAttacked;
            Combat.WindowStruck += OnWindow;
            ComboDefinition original = Combat.Combo;

            _log.AppendLine($"pads: {string.Join(", ", _pads.Select(p => $"{p.Key}→{(p.Value.Combo != null ? p.Value.Combo.name : "null")}"))}");
            _log.AppendLine($"combo at probe start: {Name(Combat.Combo)}");

            // Probe whether queued keyboard state reaches the game; otherwise drive the bridge's own targets.
            yield return PlaceAt(new Vector3(-9f, 0f, 2f));
            Vector3 before = Combat.transform.position;
            for (int i = 0; i < 20; i++) { HoldKeys(Key.D); yield return null; }
            ReleaseKeys();
            _deviceInput = (Combat.transform.position - before).magnitude > 0.3f;
            _log.AppendLine(_deviceInput
                ? "input: queued Keyboard state → PlayerInputBridge (device path)"
                : "input: injected device state not routed to the game view — FALLBACK to PlayerLocomotion.SetMoveInput / PlayerCombat.TryAttack (the bridge's targets)");
            if (!_deviceInput && _bridge != null) _bridge.enabled = false;

            // 1. Each pad, from a different direction, starting from a known (different) combo.
            yield return PadScenario("QuickStrikes", Vector3.forward, "north");
            yield return PadScenario("Sweeping", Vector3.right, "east");
            yield return PadScenario("AerialHeavy", Vector3.left, "west");
            yield return PadScenario("WeaponProxy", Vector3.forward, "north");
            yield return PadScenario("Sword", Vector3.left, "east");
            yield return PadScenario("AirString", Vector3.left, "east");
            yield return PadScenario("ShippedMelee", Vector3.back, "south");

            // 2. Re-entering the pad of the active combo: no change, no rebuild.
            yield return Reenter("ShippedMelee");

            // 3. Two pads in one walk (QuickStrikes → Sweeping, walking +x along the row): the last pad wins.
            yield return TwoPads();

            // 4. A pad touched mid-attack: the Sweeping leg sweep's root travel carries the player onto the
            //    AerialHeavy pad; the swap is deferred and the strike in flight completes.
            yield return MidAttack();

            // Leave the player on the combo it started with, so later shipped-combo probes measure what they expect.
            yield return Settle();
            Combat.SetCombo(original);
            ReleaseKeys();
            _loco.SetMoveInput(Vector2.zero);
            if (_bridge != null) _bridge.enabled = true;
            Combat.Attacked -= OnAttacked;
            Combat.WindowStruck -= OnWindow;

            string verdict = _allPass ? "PASS" : "FAIL";
            _log.Insert(0, $"[VerifyComboPads] => {verdict}\n");
            System.IO.File.WriteAllText("Temp/VerifyComboPads.txt", _log.ToString());
            if (_allPass) Debug.Log(_log.ToString()); else Debug.LogError(_log.ToString());
            Destroy(gameObject);
        }

        private static string Name(ComboDefinition c) => c != null ? c.name : "(single swing)";

        // ---- input -----------------------------------------------------------------------------------------

        private void HoldKeys(params Key[] keys)
        {
            if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
        }

        private void ReleaseKeys()
        {
            if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        }

        // The camera-relative key that best walks the given world direction (W = camera forward).
        private Key KeyFor(Vector3 world, out float alignment)
        {
            Transform cam = Camera.main.transform;
            Vector3 f = cam.forward; f.y = 0f; f.Normalize();
            Vector3 r = Vector3.Cross(Vector3.up, f);
            var options = new[] { (Key.W, f), (Key.S, -f), (Key.D, r), (Key.A, -r) };
            var best = options.OrderByDescending(o => Vector3.Dot(o.Item2, world)).First();
            alignment = Vector3.Dot(best.Item2, world);
            return best.Item1;
        }

        private void Steer(Vector3 world)
        {
            if (_deviceInput)
            {
                HoldKeys(KeyFor(world, out _));
                return;
            }

            Transform cam = Camera.main.transform;
            Vector3 f = cam.forward; f.y = 0f; f.Normalize();
            Vector3 r = Vector3.Cross(Vector3.up, f);
            _loco.SetMoveInput(new Vector2(Vector3.Dot(world, r), Vector3.Dot(world, f)));
        }

        private void StopSteer()
        {
            ReleaseKeys();
            if (!_deviceInput) _loco.SetMoveInput(Vector2.zero);
        }

        // One attack press (Q). With a direction, that key is held on the press frame so the step faces it (the
        // stick wins over aim assist) and released right after, so it cannot cancel a recovery.
        private IEnumerator PressAttack(Vector3? aim = null)
        {
            if (_deviceInput)
            {
                if (aim.HasValue)
                {
                    // Hold the direction first (the bridge applies Move in its Update, after the attack callback).
                    Key dir = KeyFor(aim.Value, out _);
                    for (int i = 0; i < 3; i++) { HoldKeys(dir); yield return null; }
                    HoldKeys(Key.Q, dir);
                }
                else
                {
                    HoldKeys(Key.Q);
                }

                yield return null;
                ReleaseKeys();
                yield return null;
            }
            else
            {
                if (aim.HasValue) Steer(aim.Value);
                Combat.TryAttack();
                yield return null;
                StopSteer();
            }
        }

        // ---- movement --------------------------------------------------------------------------------------

        private IEnumerator Settle()
        {
            StopSteer();
            float t = 0f;
            while ((Combat.IsAttacking || !_loco.IsGrounded) && t < 5f) { t += Time.deltaTime; yield return null; }
            for (int i = 0; i < 10; i++) yield return null;
        }

        // Places the player (only ever OFF the pads) as the start of a walk.
        private IEnumerator PlaceAt(Vector3 ground, Vector3? facing = null)
        {
            yield return Settle();
            _cc.enabled = false;
            Combat.transform.position = new Vector3(ground.x, 1.1f, ground.z);
            if (facing.HasValue) Combat.transform.rotation = Quaternion.LookRotation(facing.Value, Vector3.up);
            _cc.enabled = true;
            Physics.SyncTransforms();
            for (int i = 0; i < 15; i++) yield return null;
        }

        // Walks (real locomotion) in a straight world direction until past `through` by `beyond` metres.
        private IEnumerator WalkThrough(Vector3 through, Vector3 dir, float beyond)
        {
            float t = 0f;
            while (t < 4f)
            {
                Vector3 p = Combat.transform.position; p.y = 0f;
                Vector3 offset = p - new Vector3(through.x, 0f, through.z);
                if (Vector3.Dot(offset, dir) > beyond) break;
                Steer(dir);
                t += Time.deltaTime;
                yield return null;
            }

            StopSteer();
            for (int i = 0; i < 10; i++) yield return null;
        }

        // ---- scenarios -------------------------------------------------------------------------------------

        private IEnumerator PadScenario(string padKey, Vector3 approach, string from)
        {
            if (!_pads.TryGetValue(padKey, out ComboLoadoutPad pad)) { Check(false, $"pad {padKey} missing"); yield break; }
            ComboDefinition expected = pad.Combo;
            yield return Settle();
            Check(Combat.Combo != expected, $"{padKey}: starts on a different combo ({Name(Combat.Combo)})");

            Vector3 centre = pad.transform.position;
            Key key = KeyFor(approach, out float align);
            // Start in the gap beside the pad (pads sit 3m apart, 1.6m wide), never on a neighbour.
            yield return PlaceAt(centre - approach * 1.5f);
            // Walk onto the pad and stop on it, then confirm the selection while standing there.
            yield return WalkThrough(centre, approach, -0.1f);
            bool selected = Combat.Combo == expected;
            Check(selected, $"{padKey}: walked on from the {from} (key {key}, align {align:0.00}) → {Name(Combat.Combo)} (want {expected.name})");

            // Walk off and away from the row (north, toward the arena; along z approaches continue), then confirm it stuck.
            Vector3 exit = Mathf.Abs(approach.x) > 0.5f ? Vector3.forward : approach;
            yield return WalkThrough(centre, exit, 2.2f);
            Check(Combat.Combo == expected, $"{padKey}: still {Name(Combat.Combo)} after stepping off");

            yield return RunCombo(expected, padKey);
        }

        // Places the player in front of the lone dummy (off the pads) and presses ahead through the whole combo.
        private IEnumerator RunCombo(ComboDefinition expected, string label)
        {
            if (_dummy == null) { Check(false, "no training dummy"); yield break; }
            Vector3 d = _dummy.position;
            // 0.9m, as VerifyAttackWindows/VerifyAttackLibrary: within reach of every library opener.
            yield return PlaceAt(new Vector3(d.x, 0f, d.z - 0.9f), Vector3.forward);

            _triggers.Clear();
            _windows.Clear();
            int steps = expected.StepCount;
            yield return PressAttack();
            float t = 0f;
            int pressedFor = 0;
            while (t < 6f && (Combat.IsAttacking || t < 0.2f))
            {
                int idx = Combat.ComboStepIndex;
                if (idx >= pressedFor && pressedFor + 1 < steps) { pressedFor = idx + 1; yield return PressAttack(); }
                t += Time.deltaTime;
                yield return null;
            }

            List<string> want = expected.Steps.Select(s => s.AnimatorTrigger).ToList();
            var ownWindows = new HashSet<string>(expected.Steps.Where(s => s.Attack != null).SelectMany(s => s.Attack.Windows.Select(w => w.Id)));
            bool order = _triggers.SequenceEqual(want);
            bool hit = _windows.Count > 0;
            bool own = _windows.All(ownWindows.Contains);
            Check(order, $"{label}: steps played [{string.Join(" → ", _triggers)}] (want [{string.Join(" → ", want)}])");
            Check(hit && own, $"{label}: windows struck the dummy [{string.Join(", ", _windows)}] — all from {expected.name}: {own}");
        }

        private IEnumerator Reenter(string padKey)
        {
            ComboLoadoutPad pad = _pads[padKey];
            Check(Combat.Combo == pad.Combo, $"re-enter {padKey}: currently {Name(Combat.Combo)}");
            yield return PlaceAt(pad.transform.position + Vector3.back * 2.0f);
            yield return WalkThrough(pad.transform.position, Vector3.forward, 2.2f); // across it, south → north
            yield return WalkThrough(pad.transform.position, Vector3.back, 2.2f);    // and back again
            Check(Combat.Combo == pad.Combo && !Combat.HasPendingCombo, $"re-enter {padKey} twice: still {Name(Combat.Combo)}, nothing pending");
            yield return RunCombo(pad.Combo, $"re-enter {padKey}");
        }

        private IEnumerator TwoPads()
        {
            ComboLoadoutPad first = _pads["QuickStrikes"], second = _pads["Sweeping"];
            Vector3 a = first.transform.position;
            yield return PlaceAt(a + Vector3.left * 1.5f);
            bool sawFirst = false;
            float t = 0f;
            while (t < 4f && Combat.transform.position.x < second.transform.position.x + 0.3f)
            {
                Steer(Vector3.right);
                if (Combat.Combo == first.Combo) sawFirst = true;
                t += Time.deltaTime;
                yield return null;
            }

            StopSteer();
            yield return WalkThrough(second.transform.position, Vector3.forward, 2.2f);
            Check(sawFirst && Combat.Combo == second.Combo, $"two pads in one walk: passed {Name(first.Combo)} ({sawFirst}) → ends on {Name(Combat.Combo)}");
            yield return RunCombo(second.Combo, "two pads");
        }

        private IEnumerator MidAttack()
        {
            ComboLoadoutPad target = _pads["AerialHeavy"];
            ComboDefinition sweeping = _pads["Sweeping"].Combo;
            yield return Settle();
            if (Combat.Combo != sweeping) Combat.SetCombo(sweeping); // known start (idle); TwoPads already left it on Sweeping

            // Stand 2m short of the pad, facing it, then run the two-step Sweeping combo: step 2 travels ~2.7m.
            Vector3 c = target.transform.position;
            yield return PlaceAt(c + Vector3.forward * 2.0f);

            _triggers.Clear();
            _windows.Clear();
            yield return PressAttack(Vector3.back);
            bool pressed2 = false, sawPending = false, comboDuringAttack = true;
            float t = 0f;
            int lastStep = 0;
            while (t < 6f && (Combat.IsAttacking || t < 0.2f))
            {
                if (!pressed2 && Combat.ComboStepIndex == 0) { pressed2 = true; yield return PressAttack(Vector3.back); }
                if (Combat.IsAttacking)
                {
                    lastStep = Mathf.Max(lastStep, Combat.ComboStepIndex);
                    if (Combat.HasPendingCombo && Combat.PendingCombo == target.Combo) sawPending = true;
                    if (Combat.Combo != sweeping) comboDuringAttack = false;
                }

                t += Time.deltaTime;
                yield return null;
            }

            Vector3 p = Combat.transform.position;
            bool onPad = Mathf.Abs(p.x - c.x) < 0.8f && Mathf.Abs(p.z - c.z) < 0.8f;
            AttackDefinition sweep = sweeping.Steps[sweeping.StepCount - 1].Attack;
            var sweepIds = new HashSet<string>(sweep.Windows.Select(w => w.Id));
            for (int i = 0; i < 5; i++) yield return null;
            Check(sawPending, $"mid-attack: the pad was touched while attacking → queued {Name(target.Combo)} (HasPendingCombo seen: {sawPending}); player ended at {p} (on pad: {onPad})");
            Check(comboDuringAttack && lastStep == sweeping.StepCount - 1,
                $"mid-attack: the Sweeping combo ran to its last step uninterrupted [{string.Join(" → ", _triggers)}]");
            Check(Combat.Combo == target.Combo && !Combat.HasPendingCombo, $"mid-attack: after the attack ended the active combo is {Name(Combat.Combo)}");
            _log.AppendLine($"       (sweep windows authored: {string.Join(", ", sweepIds)}; struck: {string.Join(", ", _windows)})");
            yield return RunCombo(target.Combo, "after mid-attack select");
        }
    }
}
