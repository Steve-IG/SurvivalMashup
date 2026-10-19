using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Combat;
using ToyChest.Gameplay.Enemy;
using ToyChest.Gameplay.HitDetection;
using ToyChest.Gameplay.Player;
using ToyChest.Systems.Resources;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Play-mode validation of the first REAL weapon attack (JC SM_Sword_01 + Frank RPG Dual Combo03_1, Combo_Test_Sword).
/// Enter play from Bootstrap.unity, then run. The combo is selected by walking onto the Sword pad with queued keyboard
/// input and every attack is pressed with Q, so it runs the normal input → combo → Animator → limb windows →
/// HitDetector → ability path. Checks:
/// <list type="bullet">
/// <item>the Weapon2 strike shape resolves to the blade sockets, which ride the right-hand bone (anchor follows the socket);</item>
/// <item>the window opens as the blade starts to drive and closes after full extension (tip speed at open/peak/close);</item>
/// <item>every contact point lies on the blade capsule at that frame (the volume tracks the weapon, not the fist);</item>
/// <item>reach ahead at 0.9 / 1.4 / 2.0 / 2.6 / 3.2m; nothing behind or on the left flank (the cut comes from the right);</item>
/// <item>a three-dummy arc: each target at most once per window;</item>
/// <item>a real Grunt loses health through ability.player_strike, with Impacted feedback per hit.</item>
/// </list>
/// Output: console [VerifySwordAttack] + Temp/VerifySwordAttack.txt. Reads public seams only; adds no gameplay.
/// </summary>
public static class VerifySwordAttack
{
    public static string Execute()
    {
        var combat = Object.FindAnyObjectByType<PlayerCombat>();
        if (combat == null) return "no PlayerCombat (enter play from Bootstrap)";
        new GameObject("~SwordProbe").AddComponent<Probe>().Combat = combat;
        return "probe started; read [VerifySwordAttack]";
    }

    public sealed class Probe : MonoBehaviour
    {
        public PlayerCombat Combat;
        private readonly StringBuilder _log = new StringBuilder();
        private readonly List<(Vector3 point, float distToBlade, Transform nearest)> _contacts = new List<(Vector3, float, Transform)>();
        private readonly List<string> _windows = new List<string>();
        private readonly List<Transform> _dummies = new List<Transform>();
        private bool _allPass = true;
        private Animator _animator;
        private CharacterStrikeProfile _profile;
        private Transform _hilt, _tip, _hand;
        private int _active;

        private void Check(bool ok, string line)
        {
            if (!ok) _allPass = false;
            _log.AppendLine((ok ? "  ok   " : "  FAIL ") + line);
        }

        private static void Keys(params Key[] k) => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(k));

        private float BladeDistance(Vector3 p)
        {
            Vector3 a = _hilt.position, b = _tip.position;
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector3.Distance(p, a + ab * t);
        }

        private void OnImpacted(Vector3 p)
        {
            Transform nearest = null;
            float best = float.MaxValue;
            for (int i = 0; i < _active; i++)
            {
                float d = (_dummies[i].position - p).sqrMagnitude;
                if (d < best) { best = d; nearest = _dummies[i]; }
            }

            _contacts.Add((p, BladeDistance(p), nearest));

            // Diagnostic: what was actually struck (nearest enemy of any kind to the contact point).
            EnemyCombatant any = null;
            float anyBest = float.MaxValue;
            foreach (EnemyCombatant e in Object.FindObjectsByType<EnemyCombatant>())
            {
                float d = (e.transform.position - p).sqrMagnitude;
                if (d < anyBest) { anyBest = d; any = e; }
            }

            Vector3 local = Quaternion.Inverse(Combat.transform.rotation) * (p - Combat.transform.position);
            _log.AppendLine($"         impact at local {local:0.00} → {(any != null ? any.name : "?")} ({Mathf.Sqrt(anyBest):0.00}m) player {Combat.transform.position:0.00} yaw {Combat.transform.eulerAngles.y:0}");
        }

        private void OnWindow(string id) => _windows.Add(id);

        private IEnumerator Start()
        {
            _animator = Combat.GetComponentInChildren<Animator>();
            _profile = Combat.GetComponent<CharacterStrikeProfile>();
            _hand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            StrikeShape blade = _profile.AuthoredShapes.FirstOrDefault(s => s.Limb == StrikeLimb.Weapon2);
            var loco = Combat.GetComponent<PlayerLocomotion>();
            var cc = Combat.GetComponent<CharacterController>();
            ComboDefinition original = Combat.Combo;

            // --- 1. Select through the pad with real input ------------------------------------------------
            ComboLoadoutPad pad = Object.FindObjectsByType<ComboLoadoutPad>().FirstOrDefault(p => p.name == "ComboPad_Sword");
            Check(pad != null && pad.Combo != null, $"Sword pad present → {(pad != null && pad.Combo != null ? pad.Combo.name : "missing")}");
            if (pad == null) { Finish(original); yield break; }
            cc.enabled = false; Combat.transform.position = pad.transform.position + new Vector3(0f, 1.1f, 2.0f); cc.enabled = true;
            for (int i = 0; i < 15; i++) yield return null;
            Vector3 f = Camera.main.transform.forward; f.y = 0; f.Normalize();
            Key back = Vector3.Dot(f, Vector3.back) > 0.7f ? Key.W : Key.S;
            float t0 = 0f;
            while (Combat.transform.position.z > pad.transform.position.z && t0 < 3f) { Keys(back); t0 += Time.deltaTime; yield return null; }
            Keys();
            for (int i = 0; i < 10; i++) yield return null;
            ComboDefinition sword = pad.Combo;
            Check(Combat.Combo == sword, $"walked onto the Sword pad → {Combat.Combo?.name}");
            AttackDefinition attack = sword.Steps[0].Attack;
            HitWindow window = attack.Windows[0];

            // --- 2. Anchor: the strike shape is the blade, riding the hand bone -----------------------------
            Check(blade != null && blade.Source == StrikeShapeSource.Sockets && blade.StartSocket != null && blade.EndSocket != null,
                "Weapon2 = Sockets shape");
            _hilt = blade.StartSocket;
            _tip = blade.EndSocket;
            bool rides = _hilt.IsChildOf(_hand) && _tip.IsChildOf(_hand);
            Check(rides && _hilt.parent.name == "Sword_Prop", $"sockets {_hilt.name}→{_tip.name} live under Sword_Prop under {_hand.name} (length {Vector3.Distance(_hilt.position, _tip.position):0.00}m)");

            var dummies = Object.FindObjectsByType<TrainingDummy>().Select(d => d.transform).ToList();
            _dummies.AddRange(dummies);
            var grunts = Object.FindObjectsByType<EnemyCombatant>().Where(e => e.GetComponent<TrainingDummy>() == null).Select(e => e.transform).ToList();
            for (int i = 0; i < grunts.Count; i++) grunts[i].position = new Vector3(18f + 2f * (i % 3), 1f, 18f + 2f * (i / 3));
            Combat.Impacted += OnImpacted;
            Combat.WindowStruck += OnWindow;

            Vector3 origin = new Vector3(-12f, 1f, -12f);
            // Faces +Z with a short real input (PlayerLocomotion keeps turning toward its last move direction, so a
            // forced rotation alone is undone), then re-places the player at the origin keeping that facing.
            Vector3 cf = Camera.main.transform.forward; cf.y = 0f; cf.Normalize();
            Key north = Vector3.Dot(cf, Vector3.forward) > 0.7f ? Key.W : Vector3.Dot(cf, Vector3.back) > 0.7f ? Key.S
                : Vector3.Dot(Vector3.Cross(Vector3.up, cf), Vector3.forward) > 0.7f ? Key.D : Key.A;
            IEnumerator Place(Vector3[] offsets)
            {
                _active = offsets.Length;
                for (int i = 0; i < _dummies.Count; i++)
                    _dummies[i].position = i < offsets.Length ? origin + offsets[i] : new Vector3(4f + 1.5f * i, 1f, -22f); // parked on the floor (±25m), far from the origin
                // Dummies carry CharacterControllers and autoSyncTransforms is off: sync now, or their controllers
                // move from the stale position on the next frame and undo the placement.
                Physics.SyncTransforms();
                cc.enabled = false; Combat.transform.SetPositionAndRotation(origin + Vector3.back * 4f, Quaternion.identity); cc.enabled = true;
                for (int i = 0; i < 15; i++) { Keys(north); yield return null; }
                Keys();
                for (int i = 0; i < 20; i++) yield return null;
                cc.enabled = false; Combat.transform.position = origin; cc.enabled = true;
                Physics.SyncTransforms();
                for (int i = 0; i < 5; i++) yield return null;
            }

            // One sword attack pressed with Q; records the blade tip's speed and the window's open/close frames.
            float openSpeed = 0f, peakSpeed = 0f, closeSpeed = 0f, maxTipFront = 0f;
            IEnumerator Swing()
            {
                float facing = Vector3.Angle(Combat.transform.forward, Vector3.forward);
                var placed = new StringBuilder();
                for (int i = 0; i < _active; i++)
                    placed.Append($" {_dummies[i].name}@{Quaternion.Inverse(Combat.transform.rotation) * (_dummies[i].position - Combat.transform.position):0.00}");
                _log.AppendLine($"         swing: player {Combat.transform.position:0.00} yaw {Combat.transform.eulerAngles.y:0} grounded {Combat.GetComponent<PlayerLocomotion>().IsGrounded} | targets{placed}");
                if (facing > 10f) Check(false, $"probe setup: player faces {facing:0}° off +Z before the swing");
                _contacts.Clear(); _windows.Clear();
                openSpeed = peakSpeed = closeSpeed = maxTipFront = 0f;
                Keys(Key.Q); yield return null; Keys();
                int hash = Animator.StringToHash(sword.Steps[0].AnimatorState);
                Vector3 prev = _tip.position - Combat.transform.position;
                bool wasOpen = false;
                float t = 0f;
                yield return new WaitForEndOfFrame();
                while (t < 3f && (Combat.IsAttacking || t < 0.1f))
                {
                    AnimatorStateInfo s = _animator.IsInTransition(0) && _animator.GetNextAnimatorStateInfo(0).shortNameHash == hash
                        ? _animator.GetNextAnimatorStateInfo(0) : _animator.GetCurrentAnimatorStateInfo(0);
                    float nt = s.shortNameHash == hash ? s.normalizedTime : -1f;
                    Vector3 rel = _tip.position - Combat.transform.position;
                    float speed = Time.deltaTime > 0f ? (rel - prev).magnitude / Time.deltaTime : 0f;
                    prev = rel;
                    bool open = nt >= window.Start && nt <= window.End;
                    if (open && !wasOpen) openSpeed = speed;
                    if (open) { peakSpeed = Mathf.Max(peakSpeed, speed); maxTipFront = Mathf.Max(maxTipFront, Vector3.Dot(rel, Combat.transform.forward)); }
                    if (!open && wasOpen) closeSpeed = speed;
                    wasOpen = open;
                    t += Time.deltaTime;
                    yield return new WaitForEndOfFrame();
                }

                for (int i = 0; i < 10; i++) yield return null;
            }

            // --- 3. Trajectory + window alignment + contacts on the blade: one dummy at 1.4m ---------------
            yield return Place(new[] { new Vector3(0f, 0f, 1.4f) });
            yield return Swing();
            Check(_windows.Count == 1 && _windows[0] == window.Id, $"1.4m ahead: struck by [{string.Join(", ", _windows)}] (want one '{window.Id}')");
            float worst = _contacts.Count > 0 ? _contacts.Max(c => c.distToBlade) : float.MaxValue;
            Check(_contacts.Count > 0 && worst <= blade.Radius + 0.35f,
                $"contact point on the blade capsule: worst {worst:0.00}m from the hilt→tip segment (r {blade.Radius:0.00} + reach padding)");
            Check(peakSpeed > 2f * Mathf.Max(0.5f, openSpeed) || openSpeed > 5f,
                $"window aligned with the blade's motion: tip {openSpeed:0.0} m/s at open → peak {peakSpeed:0.0} m/s → {closeSpeed:0.0} m/s after close; tip reaches {maxTipFront:0.00}m ahead");

            // --- 4. Reach / facing ---------------------------------------------------------------------
            var reach = new StringBuilder();
            foreach (float d in new[] { 0.9f, 1.4f, 2.0f, 2.6f, 3.2f })
            {
                yield return Place(new[] { new Vector3(0f, 0f, d) });
                yield return Swing();
                reach.Append($" {d:0.0}m:{(_windows.Count > 0 ? "hit" : "miss")}");
                if (d <= 2.0f) Check(_windows.Count == 1, $"front {d:0.0}m: {_windows.Count} hit(s) (within the blade's reach)");
            }

            _log.AppendLine("       reach:" + reach);
            yield return Place(new[] { new Vector3(0f, 0f, -1.2f) });
            yield return Swing();
            Check(_windows.Count == 0, $"behind 1.2m: {_windows.Count} hits (want 0)");
            yield return Place(new[] { new Vector3(-1.2f, 0f, 0f) });
            yield return Swing();
            _log.AppendLine($"       left flank 1.2m: {_windows.Count} hit(s) (the cut comes from the right; informative)");

            // --- 5. Arc of three: once per target per window ------------------------------------------
            yield return Place(new[] { new Vector3(-0.8f, 0f, 1.3f), new Vector3(0f, 0f, 1.6f), new Vector3(0.8f, 0f, 1.3f) });
            yield return Swing();
            var perTarget = _contacts.GroupBy(c => c.nearest).Select(g => g.Count()).ToList();
            Check(perTarget.All(n => n == 1), $"arc of 3: {perTarget.Count} target(s) hit, hits per target [{string.Join(",", perTarget)}] (each ≤ 1 per window)");

            // --- 6. Real Grunt: damage through the ability + per-hit feedback --------------------------
            Transform grunt = Live(grunts);
            if (grunt == null)
            {
                // Every Grunt is dead (e.g. after VerifyAttackLibrary): spawn a wave the normal way, via the spawn pad.
                var spawnPad = Object.FindAnyObjectByType<EnemySpawnTrigger>();
                cc.enabled = false; Combat.transform.position = spawnPad.transform.position + new Vector3(0f, 1.1f, 2.5f); cc.enabled = true;
                for (int i = 0; i < 10; i++) yield return null;
                Key south = north == Key.W ? Key.S : north == Key.S ? Key.W : north == Key.D ? Key.A : Key.D;
                float w = 0f;
                while (w < 1.5f && Combat.transform.position.z > spawnPad.transform.position.z) { Keys(south); w += Time.deltaTime; yield return null; }
                Keys();
                for (int i = 0; i < 30; i++) yield return null;
                grunts = Object.FindObjectsByType<EnemyCombatant>().Where(e => e.GetComponent<TrainingDummy>() == null).Select(e => e.transform).ToList();
                grunt = Live(grunts);
                _log.AppendLine($"       no live Grunt: walked onto the spawn pad → {(grunt != null ? "wave spawned" : "nothing spawned")}");
            }

            if (grunt != null)
            {
                yield return Place(new Vector3[0]);
                grunt.position = origin + new Vector3(0f, 0f, 1.5f);
                Physics.SyncTransforms();
                for (int i = 0; i < 5; i++) yield return null;
                var behaviour = grunt.GetComponent<GameplayObjectBehaviour>();
                ResourceValue health = null;
                if (behaviour != null && behaviour.Object != null && behaviour.Object.TryGet(out ResourceSet resources))
                    health = resources.GetResource(new DefinitionId("resource.health"));
                float before = health != null ? health.Current : -1f;
                _dummies.Insert(0, grunt); _active = 1;
                yield return Swing();
                _dummies.RemoveAt(0);
                float after = health != null ? health.Current : -1f;
                Check(health != null && after < before && _contacts.Count >= 1,
                    $"Grunt: health {before:0} → {after:0} via ability.player_strike, Impacted ×{_contacts.Count}");
                // Park the (still live, now aggressive) Grunt back in the far corner so it cannot kill the player
                // during whatever probe runs next.
                grunt.position = new Vector3(20f, 1f, 20f); // on the floor (±25m)
                Physics.SyncTransforms();
            }
            else
            {
                Check(false, "no Grunt available for the damage check");
            }

            Combat.Impacted -= OnImpacted;
            Combat.WindowStruck -= OnWindow;
            Finish(original);
        }

        private static Transform Live(List<Transform> grunts)
        {
            foreach (Transform g in grunts)
            {
                if (g == null || !g.gameObject.activeInHierarchy) continue;
                var b = g.GetComponent<GameplayObjectBehaviour>();
                if (b != null && b.Object != null && b.Object.IsActive && b.Object.TryGet(out ResourceSet r) &&
                    r.GetResource(new DefinitionId("resource.health")).Current > 0f) return g;
            }

            return null;
        }

        private void Finish(ComboDefinition original)
        {
            Keys();
            Combat.SetCombo(original);
            string verdict = _allPass ? "PASS" : "FAIL";
            _log.Insert(0, $"[VerifySwordAttack] => {verdict}\n");
            System.IO.File.WriteAllText("Temp/VerifySwordAttack.txt", _log.ToString());
            if (_allPass) Debug.Log(_log.ToString()); else Debug.LogError(_log.ToString());
            Destroy(gameObject);
        }
    }
}
