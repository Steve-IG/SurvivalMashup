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
/// <item>the area is centred on the player (above its root bone) with the authored 3.3 m radius: a dummy at 2.8 m is hit, one at 4.2 m is not;</item>
/// <item>damage, every Impacted (hit-stop, shake, impact VFX), and the ground burst land on the same frame — the frame
/// the fist reaches the ground — and the burst plays on the ground under the area centre;</item>
/// <item>the area indicator is shown from the frame the slam starts until it ends, on the ground under the area's centre;</item>
/// <item>a successful activation starts the authored cooldown; an immediate re-press is refused (OnCooldown) and plays nothing;</item>
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
        private float _authoredCooldown;
        private int _areaFrame = -1, _areaCount;
        private readonly Dictionary<Transform, List<int>> _hitFrames = new Dictionary<Transform, List<int>>();
        private readonly HashSet<Transform> _slamHits = new HashSet<Transform>();
        private Vector3 _areaCenter;
        private float _areaRadius, _handAboveRoot, _burstOffset = -1f;
        private readonly List<int> _impactFrames = new List<int>();
        private readonly List<int> _damageFrames = new List<int>();
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
            _areaFrame = -1;
            _areaCount = 0;
            _burstOffset = -1f;
            _impactFrames.Clear();
            _damageFrames.Clear();
            _slamHits.Clear();
            foreach (List<int> frames in _hitFrames.Values) frames.Clear();
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
            _hitFrames[target] = new List<int>();
            System.Action<float, float, float> handler = (previous, current, maximum) =>
            {
                if (current < previous)
                {
                    _damage[target] += previous - current;
                    _damageFrames.Add(Time.frameCount);
                    _hitFrames[target].Add(Time.frameCount);
                    if (Time.frameCount == _areaFrame) _slamHits.Add(target);
                }
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
            _authoredCooldown = duration;
            Check(duration > 0f && Cooldown() == 0f, $"slot 0 '{_abilities.GetDisplayName(0)}' ready, authored cooldown {duration:0.0}s (Ability_GroundSlam → Cooldown Seconds)");

            _abilities.SlotActivated += _ => _activated++;
            _abilities.SlotRefused += (_, reason) => { _refused++; _lastRefusal = reason; };
            Combat.Attacked += trigger => { _attacked++; _triggers.Add(trigger); };
            Combat.Contacted += () => _contacted++;
            Combat.Impacted += _ => { _impacts++; _impactFrames.Add(Time.frameCount); };
            Animator animator = Combat.GetComponentInChildren<Animator>();
            Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Combat.AreaActivated += (window, center, radius) =>
            {
                // PlayerAttackFeedback subscribed first, so its burst for this frame already exists.
                _areaCount++;
                _areaFrame = Time.frameCount;
                _areaCenter = center;
                _areaRadius = radius;
                _handAboveRoot = rightHand.position.y - animator.transform.position.y;
                foreach (ParticleSystem ps in Object.FindObjectsByType<ParticleSystem>())
                {
                    if (ps.transform.parent == null && ps.name.StartsWith("ToonBodySlam"))
                    {
                        float offset = Vector3.Distance(ps.transform.position, Combat.ToCharacterGround(center));
                        if (_burstOffset < 0f || offset < _burstOffset) _burstOffset = offset;
                    }
                }
            };
            Combat.WindowStruck += id => _windows.Add(id);

            var dummies = Object.FindObjectsByType<TrainingDummy>().Select(d => d.transform).OrderBy(d => d.name).ToList();
            foreach (Transform d in dummies) Watch(d);
            Vector3 origin = new Vector3(-12f, 1f, -12f);
            Vector3 park = new Vector3(4f, 1f, -22f);

            // --- 1. Slam from input hits the dummies inside its area, all on the impact frame -------------------
            // Front 1.3 m, side 2.8 m (beyond the old 2.2 m radius, inside 3.3 m even after the slam's ~0.4 m lunge),
            // behind 1.2 m; a fourth at 4.2 m is outside.
            Vector3[] around = { new Vector3(0f, 0f, 1.3f), new Vector3(2.8f, 0f, 0f), new Vector3(0f, 0f, -1.2f), new Vector3(-4.2f, 0f, 0f) };
            yield return Place(origin, dummies, around, park);
            GameObject indicator = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include)
                .Select(r => r.gameObject).FirstOrDefault(g => g.name == "~AbilityAreaIndicator");
            bool indicatorBefore = indicator != null && indicator.activeSelf;
            ResetCounters();
            yield return Tap(Key.R);
            float cooldownAtPress = Cooldown();
            bool indicatorAtStart = indicator != null && indicator.activeSelf && _areaFrame < 0;
            bool indicatorMatches = false;
            string indicatorAt = "no live area";
            if (indicator != null && Combat.TryGetAbilityAttackArea(out Vector3 liveCenter, out float liveRadius))
            {
                Vector3 root = animator.transform.position;
                float centreToRoot = new Vector2(liveCenter.x - root.x, liveCenter.z - root.z).magnitude;
                float discToGround = Vector3.Distance(indicator.transform.position, new Vector3(liveCenter.x, root.y, liveCenter.z));
                indicatorMatches = centreToRoot < 0.01f && discToGround < 0.05f && Mathf.Approximately(liveRadius, 3.3f);
                indicatorAt = $"centre {centreToRoot:0.000} m from the root bone horizontally, disc {discToGround:0.000} m from the ground point, radius {liveRadius:0.00}";
            }
            bool indicatorThroughImpact = true;
            float watch = 0f;
            while (Combat.IsAttacking && watch < 4f)
            {
                if (indicator == null || !indicator.activeSelf) indicatorThroughImpact = false;
                watch += Time.deltaTime;
                yield return null;
            }

            for (int i = 0; i < 5; i++) yield return null;
            bool indicatorAfter = indicator != null && indicator.activeSelf;
            Check(indicator != null && !indicatorBefore && indicatorAtStart && indicatorThroughImpact && !indicatorAfter,
                $"area indicator: hidden before, shown from the slam's start (before impact), through the whole attack, hidden after");
            Check(indicatorMatches, $"indicator on the ground under the area centre (above the root bone), r 3.3 m: {indicatorAt}");
            Check(_areaCount == 1 && Mathf.Approximately(_areaRadius, 3.3f),
                $"area went live once, radius {_areaRadius:0.00} m (2.2 m × 1.5)");
            Check(_handAboveRoot < 0.2f, $"impact frame is the fist on the ground: right hand {_handAboveRoot:0.00} m above the root");
            var slamTargets = dummies.Take(3).ToList();
            Check(_impactFrames.Count == 3 && _impactFrames.All(f => f == _areaFrame) &&
                  slamTargets.All(d => _hitFrames[d].Count == 1 && _hitFrames[d][0] == _areaFrame),
                $"damage frames [{string.Join(",", slamTargets.SelectMany(d => _hitFrames[d]).Distinct())}] and Impacted frames [{string.Join(",", _impactFrames.Distinct())}] == area frame {_areaFrame}");
            Check(_burstOffset >= 0f && _burstOffset < 0.01f, $"ground burst spawned on the area frame at the area centre (offset {_burstOffset:0.000} m)");
            Check(_slamHits.Contains(dummies[1]) && !_slamHits.Contains(dummies[3]),
                $"radius: dummy at 2.8 m hit by the slam ({_slamHits.Contains(dummies[1])}), dummy at 4.2 m not ({_slamHits.Contains(dummies[3])})");
            var struck = dummies.Take(3).Where(d => _damage[d] > 0f).ToList();
            Check(_activated == 1 && _triggers.SequenceEqual(new[] { "Fighter_Combo02_3" }),
                $"R → slot activated ×{_activated}, attack trigger [{string.Join(",", _triggers)}]");
            Check(cooldownAtPress > _authoredCooldown - 0.5f, $"cooldown started at press: {cooldownAtPress:0.00}s of {_authoredCooldown:0.0}s");
            Check(struck.Count == 3 && _windows.All(w => w == "Ground slam") && _windows.Count == 3,
                $"slam struck {struck.Count}/3 dummies (front 1.3 m / side 2.8 m / behind 1.2 m) via [{string.Join(",", _windows)}], damage [{string.Join(", ", dummies.Take(3).Select(d => _damage[d].ToString("0")))}]");
            Check(dummies.Take(3).All(d => Mathf.Approximately(_damage[d], 30f)),
                "each dummy damaged once for the authored 30 (no double hits)");

            // Steps 2–3 need the cooldown to outlast the slam itself (~1.5 s to idle); a shorter authored cooldown makes them moot.
            if (_authoredCooldown >= 2.5f)
            {
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
                float cooldownAtPunch = Cooldown();
                yield return Tap(Key.Q);
                yield return UntilIdle();
                Check(Combat.Combo != null && Combat.Combo.name == "Combo_PlayerMelee" && _triggers.FirstOrDefault() == "Attack",
                    $"Q plays the melee combo {Combat.Combo?.name} trigger [{string.Join(",", _triggers)}]");
                Check(_damage[dummies[0]] > 0f && _activated == 0 && cooldownAtPunch > 0f,
                    $"punch landed ({_damage[dummies[0]]:0} damage via ability.player_strike), pressed during slam cooldown {cooldownAtPunch:0.0}s");

            }
            else
            {
                _log.AppendLine($"  skip immediate-repeat and melee-during-cooldown checks: authored cooldown {_authoredCooldown:0.0}s ends before the slam does");
            }

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
            Check(whiffCooldown > _authoredCooldown - 0.5f, $"no-target slam still started the cooldown ({whiffCooldown:0.00}s)");
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
