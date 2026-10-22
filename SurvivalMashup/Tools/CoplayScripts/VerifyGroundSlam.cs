using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Enemy;
using ToyChest.Gameplay.Player;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.Resources;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Play-mode validation of Review Group B — Ground Slam. Enter play from Bootstrap.unity, then run. Every action is
/// pressed with queued keyboard input (R = Ability1, Q = Attack, Left Ctrl = Roll), so it runs the real
/// input → PlayerInputBridge → PlayerAbilityActivator → AbilitySet → PlayerCombat → Animator → hit window →
/// HitDetector → impact ability path. Checks:
/// <list type="bullet">
/// <item>a slam hits three dummies around the player (front, side, behind), each exactly once, through the slam window;</item>
/// <item>a successful activation starts the 6 s cooldown; an immediate re-press is refused (OnCooldown) and plays nothing;</item>
/// <item>a slam with no targets still plays, bursts, and starts the cooldown;</item>
/// <item>an ungranted activation is refused (NotGranted) without playing or consuming a cooldown;</item>
/// <item>a press during a roll commits nothing unless the slam actually starts;</item>
/// <item>a slam damages several Grunts of a real wave;</item>
/// <item>ordinary melee (Q) still plays the unarmed combo and lands ability.player_strike while the slam cools down.</item>
/// </list>
/// Output: console [VerifyGroundSlam] + Temp/VerifyGroundSlam.txt. Reads public seams only; adds no gameplay.
/// </summary>
public static class VerifyGroundSlam
{
    public static string Execute()
    {
        var combat = Object.FindAnyObjectByType<PlayerCombat>();
        if (combat == null) return "no PlayerCombat (enter play from Bootstrap)";
        new GameObject("~GroundSlamProbe").AddComponent<Probe>().Combat = combat;
        return "probe started; read [VerifyGroundSlam]";
    }

    public sealed class Probe : MonoBehaviour
    {
        private static readonly DefinitionId Health = new DefinitionId("resource.health");
        private static readonly DefinitionId Activation = new DefinitionId("ability.ground_slam");

        public PlayerCombat Combat;
        private readonly StringBuilder _log = new StringBuilder();
        private bool _allPass = true;

        private PlayerAbilityActivator _abilities;
        private AbilitySet _abilitySet;
        private CharacterController _cc;
        private int _activated, _refused, _attacked, _contacted, _impacts;
        private AbilityActivationResult _lastRefusal;
        private readonly List<string> _triggers = new List<string>();
        private readonly List<string> _windows = new List<string>();
        private readonly Dictionary<Transform, float> _damage = new Dictionary<Transform, float>();
        private readonly List<(ResourceValue value, System.Action<float, float, float> handler)> _watched = new List<(ResourceValue, System.Action<float, float, float>)>();

        private void Check(bool ok, string line)
        {
            if (!ok) _allPass = false;
            _log.AppendLine((ok ? "  ok   " : "  FAIL ") + line);
        }

        private static void Keys(params Key[] k) => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(k));

        private float Cooldown()
        {
            _abilities.TryGetCooldown(0, out float remaining, out _);
            return remaining;
        }

        private void ResetCounters()
        {
            _activated = _refused = _attacked = _contacted = _impacts = 0;
            _triggers.Clear();
            _windows.Clear();
            foreach (Transform t in _damage.Keys.ToList()) _damage[t] = 0f;
        }

        private IEnumerator Tap(Key key)
        {
            Keys(key);
            yield return null;
            Keys();
            yield return null;
        }

        private IEnumerator UntilIdle(float limit = 4f)
        {
            float t = 0f;
            while (t < 0.1f || (Combat.IsAttacking && t < limit)) { t += Time.deltaTime; yield return null; }
            for (int i = 0; i < 5; i++) yield return null;
        }

        private IEnumerator UntilReady()
        {
            float t = 0f;
            while (Cooldown() > 0f && t < 10f) { t += Time.deltaTime; yield return null; }
        }

        private void Watch(Transform target)
        {
            var b = target.GetComponent<GameplayObjectBehaviour>();
            if (b == null || b.Object == null || !b.Object.TryGet(out ResourceSet r)) return;
            ResourceValue health = r.GetResource(Health);
            _damage[target] = 0f;
            System.Action<float, float, float> handler = (previous, current, maximum) =>
            {
                if (current < previous) _damage[target] += previous - current;
            };
            health.ValueChanged += handler;
            _watched.Add((health, handler));
        }

        private IEnumerator Place(Vector3 origin, IList<Transform> targets, Vector3[] offsets, Vector3 parkFrom)
        {
            for (int i = 0; i < targets.Count; i++)
                targets[i].position = i < offsets.Length ? origin + offsets[i] : parkFrom + new Vector3(1.5f * i, 0f, 0f);
            Physics.SyncTransforms();
            _cc.enabled = false; Combat.transform.SetPositionAndRotation(origin, Quaternion.identity); _cc.enabled = true;
            Physics.SyncTransforms();
            for (int i = 0; i < 10; i++) yield return null;
        }

        private IEnumerator Start()
        {
            _abilities = Combat.GetComponent<PlayerAbilityActivator>();
            _cc = Combat.GetComponent<CharacterController>();
            var behaviour = Combat.GetComponent<GameplayObjectBehaviour>();
            Check(_abilities != null && _abilities.SlotCount == 1, $"PlayerAbilityActivator on the composed Player, {(_abilities != null ? _abilities.SlotCount : 0)} slot(s)");
            Check(behaviour.Object.TryGet(out _abilitySet) && _abilitySet.Has(Activation) && _abilitySet.Has(new DefinitionId("ability.ground_slam_impact")),
                "Player granted ability.ground_slam + ability.ground_slam_impact through Bootstrap → VerticalSlice composition");
            if (_abilities == null || _abilitySet == null) { Finish(); yield break; }
            _abilities.TryGetCooldown(0, out _, out float duration);
            Check(Mathf.Approximately(duration, 6f) && Cooldown() == 0f, $"slot 0 '{_abilities.GetDisplayName(0)}' ready, authored cooldown {duration:0.0}s");

            _abilities.SlotActivated += _ => _activated++;
            _abilities.SlotRefused += (_, reason) => { _refused++; _lastRefusal = reason; };
            Combat.Attacked += trigger => { _attacked++; _triggers.Add(trigger); };
            Combat.Contacted += () => _contacted++;
            Combat.Impacted += _ => _impacts++;
            Combat.WindowStruck += id => _windows.Add(id);

            var dummies = Object.FindObjectsByType<TrainingDummy>().Select(d => d.transform).OrderBy(d => d.name).ToList();
            foreach (Transform d in dummies) Watch(d);
            Vector3 origin = new Vector3(-12f, 1f, -12f);
            Vector3 park = new Vector3(4f, 1f, -22f);

            // --- 1. Slam from input hits three dummies around the player ----------------------------------------
            Vector3[] around = { new Vector3(0f, 0f, 1.3f), new Vector3(1.3f, 0f, 0f), new Vector3(0f, 0f, -1.2f) };
            yield return Place(origin, dummies, around, park);
            ResetCounters();
            yield return Tap(Key.R);
            float cooldownAtPress = Cooldown();
            yield return UntilIdle();
            var struck = dummies.Take(3).Where(d => _damage[d] > 0f).ToList();
            Check(_activated == 1 && _triggers.SequenceEqual(new[] { "Fighter_Combo02_3" }),
                $"R → slot activated ×{_activated}, attack trigger [{string.Join(",", _triggers)}]");
            Check(cooldownAtPress > 5.5f, $"cooldown started at press: {cooldownAtPress:0.00}s");
            Check(struck.Count == 3 && _windows.All(w => w == "Hammer slam") && _windows.Count == 3,
                $"slam struck {struck.Count}/3 dummies (front/side/behind) via [{string.Join(",", _windows)}], damage [{string.Join(", ", dummies.Take(3).Select(d => _damage[d].ToString("0")))}]");
            Check(dummies.Take(3).All(d => Mathf.Approximately(_damage[d], 0f) || Mathf.Approximately(_damage[d], 30f)),
                "each dummy damaged once for the authored 30 (no double hits)");

            // --- 2. Immediate repeat is refused ------------------------------------------------------------------
            ResetCounters();
            float before = Cooldown();
            yield return Tap(Key.R);
            for (int i = 0; i < 15; i++) yield return null;
            Check(_refused == 1 && _lastRefusal == AbilityActivationResult.OnCooldown && _attacked == 0 && !Combat.IsAttacking,
                $"immediate re-press refused ({_lastRefusal}) ×{_refused}, attacks started {_attacked}");
            Check(Cooldown() <= before, $"refused press did not restart the cooldown ({before:0.00}s → {Cooldown():0.00}s)");
            Check(dummies.Take(3).All(d => _damage[d] == 0f), "no second slam damage");

            // --- 3. Ordinary melee is unchanged while the slam cools down -----------------------------------------
            yield return Place(origin, dummies, new[] { new Vector3(0f, 0f, 1.2f) }, park);
            ResetCounters();
            yield return Tap(Key.Q);
            yield return UntilIdle();
            Check(Combat.Combo != null && Combat.Combo.name == "Combo_PlayerMelee" && _triggers.FirstOrDefault() == "Attack",
                $"Q plays the melee combo {Combat.Combo?.name} trigger [{string.Join(",", _triggers)}]");
            Check(_damage[dummies[0]] > 0f && _activated == 0 && Cooldown() > 0f,
                $"punch landed ({_damage[dummies[0]]:0} damage via ability.player_strike) during slam cooldown {Cooldown():0.0}s");

            // --- 4. Cooldown ends, then a slam with no targets still commits ---------------------------------------
            yield return UntilReady();
            Check(Cooldown() == 0f, "cooldown elapsed → ready again");
            yield return Place(origin, dummies, new Vector3[0], park);
            ResetCounters();
            yield return Tap(Key.R);
            float whiffCooldown = Cooldown();
            yield return UntilIdle();
            Check(_activated == 1 && _attacked == 1 && _contacted >= 1 && _impacts == 0,
                $"no-target slam played: activated {_activated}, contact {_contacted}, impacts {_impacts}");
            Check(whiffCooldown > 5.5f, $"no-target slam still started the cooldown ({whiffCooldown:0.00}s)");
            Check(Object.FindObjectsByType<ParticleSystem>().Any(p => p.gameObject.name.StartsWith("ToonBodySlam")),
                "ground burst VFX spawned at the slam frame with no hit");

            // --- 5. Ungranted activation does not consume a cooldown ----------------------------------------------
            yield return UntilReady();
            AbilityDefinition definition = _abilitySet.GetAbility(Activation).Definition;
            _abilitySet.Revoke(Activation);
            ResetCounters();
            yield return Tap(Key.R);
            for (int i = 0; i < 15; i++) yield return null;
            Check(_refused == 1 && _lastRefusal == AbilityActivationResult.NotGranted && _attacked == 0,
                $"ungranted press refused ({_lastRefusal}), attacks started {_attacked}");
            _abilitySet.Grant(definition);
            Check(Cooldown() == 0f, $"re-granted: no cooldown was consumed ({Cooldown():0.00}s)");

            // --- 6. Press during a roll: nothing commits unless the slam starts ----------------------------------
            ResetCounters();
            yield return Tap(Key.LeftCtrl);
            yield return Tap(Key.R);
            for (int i = 0; i < 30; i++) yield return null;
            yield return UntilIdle();
            Check((_activated == 0 && Cooldown() == 0f) || (_activated == 1 && _attacked == 1 && Cooldown() > 0f),
                $"press during roll: {(_activated == 1 ? "buffered and slammed after the roll" : "dropped")} — activated {_activated}, attacks {_attacked}, cooldown {Cooldown():0.00}s (never committed without a slam)");

            // --- 7. Grunt wave -----------------------------------------------------------------------------------
            yield return UntilReady();
            var grunts = LiveGrunts();
            if (grunts.Count < 2)
            {
                var spawnPad = Object.FindAnyObjectByType<EnemySpawnTrigger>();
                if (spawnPad != null)
                {
                    _cc.enabled = false; Combat.transform.position = spawnPad.transform.position + Vector3.up * 1.1f; _cc.enabled = true;
                    Physics.SyncTransforms();
                    for (int i = 0; i < 40; i++) yield return null;
                }

                grunts = LiveGrunts();
            }

            if (grunts.Count >= 2)
            {
                foreach (Transform g in grunts) if (!_damage.ContainsKey(g)) Watch(g);
                Vector3 arena = new Vector3(12f, 1f, -12f);
                yield return Place(arena, grunts, new[] { new Vector3(0f, 0f, 1.3f), new Vector3(-1.2f, 0f, 0.4f), new Vector3(1.2f, 0f, 0.4f) }, new Vector3(20f, 1f, 20f));
                ResetCounters();
                yield return Tap(Key.R);
                yield return UntilIdle();
                int hit = grunts.Take(3).Count(g => _damage[g] > 0f);
                Check(_activated == 1 && hit >= 2, $"Grunt wave: slam damaged {hit}/{Mathf.Min(3, grunts.Count)} Grunts [{string.Join(", ", grunts.Take(3).Select(g => _damage[g].ToString("0")))}]");
                foreach (Transform g in grunts) g.position = new Vector3(20f, 1f, 20f);
                Physics.SyncTransforms();
            }
            else
            {
                Check(false, $"no Grunt wave available ({grunts.Count} live)");
            }

            Finish();
        }

        private static List<Transform> LiveGrunts()
        {
            var live = new List<Transform>();
            foreach (EnemyCombatant e in Object.FindObjectsByType<EnemyCombatant>())
            {
                if (e.GetComponent<TrainingDummy>() != null || !e.gameObject.activeInHierarchy) continue;
                var b = e.GetComponent<GameplayObjectBehaviour>();
                if (b != null && b.Object != null && b.Object.IsActive && b.Object.TryGet(out ResourceSet r) && r.GetResource(Health).Current > 0f)
                    live.Add(e.transform);
            }

            return live;
        }

        private void Finish()
        {
            Keys();
            foreach (var (value, handler) in _watched) value.ValueChanged -= handler;
            string verdict = _allPass ? "PASS" : "FAIL";
            _log.Insert(0, $"[VerifyGroundSlam] => {verdict}\n");
            System.IO.File.WriteAllText("Temp/VerifyGroundSlam.txt", _log.ToString());
            if (_allPass) Debug.Log(_log.ToString()); else Debug.LogError(_log.ToString());
            Destroy(gameObject);
        }
    }
}
