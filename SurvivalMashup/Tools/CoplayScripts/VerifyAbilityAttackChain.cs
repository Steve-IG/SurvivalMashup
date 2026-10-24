using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Combat;
using ToyChest.Gameplay.Enemy;
using ToyChest.Gameplay.Player;
using ToyChest.Systems.Resources;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Play-mode validation that ability attacks scale past one step. Enter play from Bootstrap.unity, then run. Builds an
/// in-memory two-pulse slam (Combo_Ability_GroundSlam with its step duplicated — no asset is written) and starts it
/// through <see cref="PlayerCombat.StartAbilityAttack"/>, the seam a slot uses after its activation commits. Checks:
/// <list type="bullet">
/// <item>one start plays both steps with no further input;</item>
/// <item>the melee button (Q) pressed during the first step neither advances nor hijacks the ability;</item>
/// <item>steering (W held) cannot cancel the ability before its final step;</item>
/// <item>each pulse is its own hit instance: both dummies are struck once per step, each pulse goes live and bursts;</item>
/// <item>afterwards the melee combo is restored and Q plays it.</item>
/// </list>
/// Output: console [VerifyAbilityAttackChain] + Temp/VerifyAbilityAttackChain.txt. Reads public seams only.
/// </summary>
public static class VerifyAbilityAttackChain
{
    public static string Execute()
    {
        var combat = Object.FindAnyObjectByType<PlayerCombat>();
        if (combat == null) return "no PlayerCombat (enter play from Bootstrap)";
        new GameObject("~AbilityChainProbe").AddComponent<Probe>().Combat = combat;
        return "probe started; read [VerifyAbilityAttackChain]";
    }

    public sealed class Probe : MonoBehaviour
    {
        public PlayerCombat Combat;
        private readonly StringBuilder _log = new StringBuilder();
        private bool _allPass = true;
        private readonly List<string> _triggers = new List<string>();
        private readonly Dictionary<Transform, int> _hits = new Dictionary<Transform, int>();
        private int _areas, _bursts;

        private void Check(bool ok, string line)
        {
            if (!ok) _allPass = false;
            _log.AppendLine((ok ? "  ok   " : "  FAIL ") + line);
        }

        private static void Keys(params Key[] k) => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(k));

        private static ComboDefinition TwoPulseSlam()
        {
            var source = AssetDatabase.LoadAssetAtPath<ComboDefinition>("Assets/Game/Content/Definitions/Combo_Ability_GroundSlam.asset");
            ComboDefinition copy = Object.Instantiate(source);
            var so = new SerializedObject(copy);
            SerializedProperty steps = so.FindProperty("_steps");
            steps.InsertArrayElementAtIndex(0); // duplicates step 0 → two identical slam steps
            so.ApplyModifiedPropertiesWithoutUndo();
            return copy;
        }

        private IEnumerator Start()
        {
            ComboDefinition melee = Combat.Combo;
            ComboDefinition twoPulse = TwoPulseSlam();
            Check(twoPulse.StepCount == 2, $"in-memory two-pulse slam built ({twoPulse.StepCount} steps)");

            var cc = Combat.GetComponent<CharacterController>();
            var dummies = Object.FindObjectsByType<TrainingDummy>().Select(d => d.transform).OrderBy(d => d.name).ToList();

            // This probe checks chaining, not hit reactions: since the review adjustments a slam knocks a dummy ~2 m back,
            // out of the next pulse's area. Make these dummies unshakeable (runtime scene objects only; nothing is saved).
            foreach (Transform d in dummies)
            {
                var combatant = d.GetComponent<EnemyCombatant>();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                typeof(EnemyCombatant).GetField("_stabilityAttributeId", flags).SetValue(combatant, "attribute.none");
                typeof(EnemyCombatant).GetField("_fallbackStability", flags).SetValue(combatant, 100000f);
            }
            Vector3 origin = new Vector3(-12f, 1f, -12f);
            Vector3[] around = { new Vector3(0f, 0f, 1.5f), new Vector3(-1.5f, 0f, 0f) };
            for (int i = 0; i < dummies.Count; i++)
                dummies[i].position = i < around.Length ? origin + around[i] : new Vector3(4f + 1.5f * i, 1f, -22f);
            Physics.SyncTransforms();
            cc.enabled = false; Combat.transform.SetPositionAndRotation(origin, Quaternion.identity); cc.enabled = true;
            Physics.SyncTransforms();
            for (int i = 0; i < 15; i++) yield return null;

            foreach (Transform d in dummies.Take(2))
            {
                _hits[d] = 0;
                var b = d.GetComponent<GameplayObjectBehaviour>();
                ResourceValue health = b.Object.Get<ResourceSet>().GetResource(new DefinitionId("resource.health"));
                Transform target = d;
                health.ValueChanged += (previous, current, maximum) => { if (current < previous) _hits[target]++; };
            }

            Combat.Attacked += t => _triggers.Add(t);
            Combat.AreaActivated += (window, center, radius) =>
            {
                _areas++;
                if (Object.FindObjectsByType<ParticleSystem>().Any(p => p.transform.parent == null && p.name.StartsWith(window.AreaEffect != null ? window.AreaEffect.name : "~none")))
                    _bursts++;
            };

            // --- One start plays both steps; Q and W during step 1 change nothing ----------------------------------
            Check(Combat.StartAbilityAttack(twoPulse), "StartAbilityAttack accepted the two-pulse attack");
            int stepsSeen = 0;
            bool everNotAbility = false;
            float t = 0f;
            Keys(Key.W);
            yield return null;
            Keys(Key.W, Key.Q);
            yield return null;
            Keys(Key.W);
            while (Combat.IsAttacking && t < 6f)
            {
                stepsSeen = Mathf.Max(stepsSeen, Combat.ComboStepIndex + 1);
                if (!Combat.IsPerformingAbilityAttack) everNotAbility = true;
                t += Time.deltaTime;
                yield return null;
            }

            Keys();
            for (int i = 0; i < 20; i++) yield return null;

            Check(_triggers.Count == 2 && _triggers.All(x => x == "Fighter_Combo02_3"),
                $"both steps played from one start, Q never started melee: triggers [{string.Join(",", _triggers)}], steps seen {stepsSeen}");
            Check(!everNotAbility, "IsPerformingAbilityAttack held through both steps (W held: no early cancel)");
            Check(_areas == 2 && _bursts == 2, $"each pulse went live and burst: areas {_areas}, bursts {_bursts}");
            Check(_hits.Values.All(n => n == 2), $"each dummy struck once per pulse: [{string.Join(",", _hits.Values)}]");

            // --- Melee restored --------------------------------------------------------------------------------
            Check(Combat.Combo == melee && !Combat.IsPerformingAbilityAttack, $"melee combo restored ({Combat.Combo?.name})");
            _triggers.Clear();
            for (int i = 0; i < 30; i++) yield return null;
            _log.AppendLine($"         before Q: grounded {Combat.GetComponent<PlayerLocomotion>().IsGrounded}, attacking {Combat.IsAttacking}, pos {Combat.transform.position:0.00}");
            Keys(Key.Q); yield return null; Keys(); yield return null;
            t = 0f;
            while (Combat.IsAttacking && t < 4f) { t += Time.deltaTime; yield return null; }
            var loco = Combat.GetComponent<PlayerLocomotion>();
            Check(_triggers.FirstOrDefault() == "Attack",
                $"Q after the ability plays the melee opener [{string.Join(",", _triggers)}] (grounded {loco.IsGrounded}, rolling {loco.IsRolling}, " +
                $"attacking {Combat.IsAttacking}, phase {Combat.Phase}, pos {Combat.transform.position:0.00})");

            Destroy(twoPulse);
            string verdict = _allPass ? "PASS" : "FAIL";
            _log.Insert(0, $"[VerifyAbilityAttackChain] => {verdict}\n");
            System.IO.File.WriteAllText("Temp/VerifyAbilityAttackChain.txt", _log.ToString());
            if (_allPass) Debug.Log(_log.ToString()); else Debug.LogError(_log.ToString());
            Destroy(gameObject);
        }
    }
}
