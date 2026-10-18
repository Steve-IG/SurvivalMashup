using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Combat;
using ToyChest.Gameplay.Enemy;
using ToyChest.Gameplay.Player;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Play-mode verification of the RG5E attack library (and the shipped combo, as a regression baseline). For each
/// combo it equips the combo through PlayerCombat.SetCombo (what the dojo pads do), then drives it through the
/// normal input seam (TryAttack — pressing ahead to chain, never on the last step) against Training Dummies in
/// controlled placements: single target ahead at several ranges, directly behind, at the right flank, a three-dummy
/// arc, and a roll near the first contact. Records which window hit which dummy (WindowStruck + Impacted), flags any
/// window that hit the same dummy twice in one step, and measures step starts and when the player is free again.
/// Then fights three Grunts with the Aerial/Heavy combo to exercise real targets dying mid-attack. Reads public
/// seams only; adds no gameplay. Output: console [VerifyAttackLibrary] + Temp/VerifyAttackLibrary.txt.
/// </summary>
public static class VerifyAttackLibrary
{
    public static string Execute()
    {
        var probe = new GameObject("~AttackLibraryProbe").AddComponent<Probe>();
        probe.Combat = Object.FindAnyObjectByType<PlayerCombat>();
        return probe.Combat != null ? "probe started; read [VerifyAttackLibrary]" : "no PlayerCombat (enter play from Bootstrap)";
    }

    public sealed class Probe : MonoBehaviour
    {
        public PlayerCombat Combat;

        private static readonly string[] Combos =
        {
            "Combo_PlayerMelee", "Combo_Test_QuickStrikes", "Combo_Test_Sweeping", "Combo_Test_AerialHeavy", "Combo_Test_WeaponProxy",
        };

        private readonly Vector3 _origin = new Vector3(-12f, 1f, -12f);
        private readonly List<Transform> _dummies = new List<Transform>();
        private readonly Dictionary<(int step, string window, int dummy), int> _hits = new Dictionary<(int, string, int), int>();
        private readonly List<string> _stepStarts = new List<string>();
        private string _pendingWindow;
        private float _runStart;
        private int _lastStep = -1;
        private int _active;

        private void OnWindowStruck(string id) => _pendingWindow = id;

        private void OnImpacted(Vector3 point)
        {
            int nearest = -1;
            float best = float.MaxValue;
            for (int i = 0; i < _active; i++)
            {
                float d = (_dummies[i].position - point).sqrMagnitude;
                if (d < best) { best = d; nearest = i; }
            }

            var key = (Combat.ComboStepIndex, _pendingWindow ?? "(legacy)", nearest);
            _hits.TryGetValue(key, out int n);
            _hits[key] = n + 1;
        }

        private IEnumerator Start()
        {
            var bridge = Combat.GetComponent<PlayerInputBridge>();
            if (bridge != null) bridge.enabled = false;
            var loco = Combat.GetComponent<PlayerLocomotion>();
            foreach (TrainingDummy d in Object.FindObjectsByType<TrainingDummy>(FindObjectsSortMode.InstanceID)) _dummies.Add(d.transform);
            var grunts = Object.FindObjectsByType<EnemyCombatant>(FindObjectsSortMode.InstanceID)
                .Where(e => e.GetComponent<TrainingDummy>() == null).Select(e => e.transform).ToList();
            for (int i = 0; i < grunts.Count; i++) grunts[i].position = new Vector3(18f + 2f * (i % 3), 1f, 18f + 2f * (i / 3)); // far corner, still on the ground
            Combat.WindowStruck += OnWindowStruck;
            Combat.Impacted += OnImpacted;
            var report = new StringBuilder();
            ComboDefinition shipped = Combat.Combo;
            bool anyDuplicate = false;

            foreach (string comboName in Combos)
            {
                var combo = AssetDatabase.LoadAssetAtPath<ComboDefinition>($"Assets/Game/Content/Definitions/{comboName}.asset");
                Combat.SetCombo(combo);
                report.AppendLine($"=== {comboName} ({combo.StepCount} steps)");

                var scenarios = new (string name, Vector3[] place, bool roll)[]
                {
                    ("front 0.9m", new[] { new Vector3(0f, 0f, 0.9f) }, false),
                    ("front 1.4m", new[] { new Vector3(0f, 0f, 1.4f) }, false),
                    ("front 2.0m", new[] { new Vector3(0f, 0f, 2.0f) }, false),
                    ("front 3.0m", new[] { new Vector3(0f, 0f, 3.0f) }, false),
                    ("behind 1.0m", new[] { new Vector3(0f, 0f, -1.0f) }, false),
                    ("right flank 1.0m", new[] { new Vector3(1.0f, 0f, 0f) }, false),
                    ("arc of 3", new[] { new Vector3(-0.9f, 0f, 1.0f), new Vector3(0f, 0f, 1.3f), new Vector3(0.9f, 0f, 1.0f) }, false),
                    ("roll at 1st contact", new[] { new Vector3(0f, 0f, 1.0f) }, true),
                };

                foreach (var (name, place, roll) in scenarios)
                {
                    yield return Place(place);
                    yield return RunCombo(combo, roll, loco);
                    string hits = Summarise(combo, out bool duplicate);
                    anyDuplicate |= duplicate;
                    report.AppendLine($"  {name,-20} {hits}{(duplicate ? "  <<DUPLICATE>>" : string.Empty)}  | {string.Join(" ", _stepStarts)}");
                }
            }

            // Real enemies: three Grunts gathered in front, fought with the heavy combo until they die (or 15s).
            var heavy = AssetDatabase.LoadAssetAtPath<ComboDefinition>("Assets/Game/Content/Definitions/Combo_Test_AerialHeavy.asset");
            Combat.SetCombo(heavy);
            yield return Place(new Vector3[0]);
            grunts = grunts.Where(g => g != null).ToList();
            for (int i = 0; i < grunts.Count && i < 3; i++)
            {
                grunts[i].position = _origin + new Vector3(-0.9f + 0.9f * i, 0f, 1.3f);
            }

            Physics.SyncTransforms();
            int impacts = 0;
            System.Action<Vector3> count = _ => impacts++;
            Combat.Impacted += count;
            float t = 0f;
            int combosRun = 0;
            while (t < 15f && grunts.Take(3).Any(g => g != null && g.gameObject.activeInHierarchy && IsAlive(g)))
            {
                Combat.transform.position = _origin;
                Combat.transform.rotation = Quaternion.identity;
                Physics.SyncTransforms();
                float start = Time.time;
                yield return RunCombo(heavy, false, loco);
                combosRun++;
                t += Time.time - start + 0.1f;
            }

            Combat.Impacted -= count;
            int dead = grunts.Take(3).Count(g => g == null || !g.gameObject.activeInHierarchy || !IsAlive(g));
            report.AppendLine($"=== Grunts vs Combo_Test_AerialHeavy: {dead}/3 dead after {combosRun} combos, {impacts} impacts, {t:0.0}s");

            Combat.SetCombo(shipped);
            Combat.WindowStruck -= OnWindowStruck;
            Combat.Impacted -= OnImpacted;
            if (bridge != null) bridge.enabled = true;
            string text = report.ToString();
            System.IO.File.WriteAllText("Temp/VerifyAttackLibrary.txt", text);
            string verdict = anyDuplicate ? "FAIL (a window hit one target twice)" : "no duplicate window hits";
            Debug.Log($"[VerifyAttackLibrary] {verdict}\n{text}");
            Destroy(gameObject);
        }

        private static bool IsAlive(Transform enemy)
        {
            var behaviour = enemy.GetComponent<GameplayObjectBehaviour>();
            return behaviour != null && behaviour.Object != null && behaviour.Object.IsActive;
        }

        private IEnumerator Place(Vector3[] offsets)
        {
            yield return new WaitForSeconds(0.35f); // let the previous attack's animation settle
            Combat.transform.position = _origin;
            Combat.transform.rotation = Quaternion.identity;
            _active = offsets.Length;
            for (int i = 0; i < _dummies.Count; i++)
            {
                _dummies[i].position = i < offsets.Length ? _origin + offsets[i] : new Vector3(-20f + 2f * i, 1f, 20f); // parked on the ground, far away
            }

            Physics.SyncTransforms();
            yield return null;
        }

        private IEnumerator RunCombo(ComboDefinition combo, bool rollAtFirstContact, PlayerLocomotion loco)
        {
            _hits.Clear();
            _stepStarts.Clear();
            _lastStep = -1;
            _runStart = Time.time;
            float firstContact = combo.Steps[0].Attack != null
                ? combo.Steps[0].Attack.FirstContact * combo.Steps[0].Attack.Clip.length
                : combo.Steps[0].WindUpDuration;
            bool rolled = false;
            Combat.TryAttack();
            float t = 0f;
            while (t < 8f && (Combat.IsAttacking || Combat.IsMovementCommitted || t < 0.2f))
            {
                if (Combat.ComboStepIndex != _lastStep && Combat.ComboStepIndex >= 0)
                {
                    _lastStep = Combat.ComboStepIndex;
                    _stepStarts.Add($"s{_lastStep + 1}@{Time.time - _runStart:0.00}");
                }

                if (!rollAtFirstContact && Combat.ComboStepIndex >= 0 && Combat.ComboStepIndex < combo.StepCount - 1)
                {
                    Combat.TryAttack(); // press ahead: chains at the step's contact, never cuts it
                }

                if (rollAtFirstContact && !rolled && t >= firstContact - 0.03f)
                {
                    loco.Roll();
                    rolled = true;
                    _stepStarts.Add($"roll@{t:0.00}");
                }

                t += Time.deltaTime;
                yield return null;
            }

            _stepStarts.Add($"free@{Time.time - _runStart:0.00}");
        }

        private string Summarise(ComboDefinition combo, out bool duplicate)
        {
            duplicate = false;
            if (_hits.Count == 0) return "no hits";
            var parts = new List<string>();
            foreach (var group in _hits.GroupBy(h => (h.Key.step, h.Key.window)).OrderBy(g => g.Key.step))
            {
                int targets = group.Count();
                int max = group.Max(g => g.Value);
                duplicate |= max > 1;
                parts.Add($"s{group.Key.step + 1}:{group.Key.window}×{targets}{(max > 1 ? $"(max {max}/target)" : string.Empty)}");
            }

            return string.Join(", ", parts);
        }
    }
}
