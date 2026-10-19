using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using ToyChest.Gameplay.Combat;
using ToyChest.Gameplay.Enemy;
using ToyChest.Gameplay.Player;
using UnityEngine;

/// <summary>
/// Play-mode verification of how an attack ENDS on the attack-asset pipeline (RG5F rewrite).
///
/// <para>The original probe (RG5D) fired the clip-event seam <c>PlayerCombat.NotifyAttackEnd()</c> and expected the
/// attack to end that frame. Since the limb-window migration every shipped step is an attack asset, whose
/// termination comes from <see cref="AttackDefinition.CommitEnd"/> read against the live Animator time
/// (<c>AttackTimeline</c> → <c>ComboSequencer.NotifyAttackEnd</c>), and clip events are deliberately ignored — so the
/// old probe could only fail. Its purpose is kept: the exit must be exact (no early release, no stranded rooting) and
/// snappy (movement resumes at once). It now checks, on step 1 of every authored combo with a movement input held:</para>
/// <list type="bullet">
/// <item>rooted for the whole commitment (input-driven speed ≈ 0);</item>
/// <item>a stray clip <c>NotifyAttackEnd()</c> mid-commitment is ignored (asset steps own their timing);</item>
/// <item>the release lands on the exact frame the clip crosses its expected end — <c>CommitEnd</c>, or the cancel
///   point when the attack is cancellable (the held input cancels it there, at most one frame later) — never before
///   its last hit window closes, and never on the fallback recovery timer;</item>
/// <item>with no press buffered the combo ends with the commitment, and movement resumes within a few frames.</item>
/// </list>
/// <para>Then the shipped combo is chained by pressing ahead: each next step must start only once the previous step's
/// last window has closed and before its commit end (chaining skips recovery), and the finisher must release at its
/// commit end. Finally a <b>fault-injection</b> run equips an in-memory clone of the shipped combo whose fallback
/// recovery timer is shorter than the clip — the documented "timer runs ahead of the event" regression — and the
/// probe must report FAIL on it. Reads public seams only; adds no gameplay. Output: console [VerifyAttackEnd] +
/// Temp/VerifyAttackEnd.txt.</para>
/// </summary>
public static class VerifyAttackEnd
{
    private static readonly string[] Combos =
    {
        "Combo_PlayerMelee", "Combo_Test_QuickStrikes", "Combo_Test_Sweeping", "Combo_Test_AerialHeavy", "Combo_Test_WeaponProxy",
    };

    public static string Execute()
    {
        var combat = Object.FindAnyObjectByType<PlayerCombat>();
        var dummy = Object.FindAnyObjectByType<TrainingDummy>();
        if (combat == null || dummy == null)
        {
            Debug.LogError("[VerifyAttackEnd] Missing PlayerCombat or TrainingDummy (enter play from Bootstrap).");
            return "missing player/dummy";
        }

        var probe = new GameObject("~AttackEndProbe").AddComponent<Probe>();
        probe.Combat = combat;
        probe.Dummy = dummy;
        return "attack-end probe started; read the console for [VerifyAttackEnd]";
    }

    public sealed class Probe : MonoBehaviour
    {
        public PlayerCombat Combat;
        public TrainingDummy Dummy;

        private readonly StringBuilder _log = new StringBuilder();
        private PlayerLocomotion _loco;
        private Animator _animator;
        private bool _allPass = true;
        private static readonly Vector2 Held = new Vector2(1f, 0f);

        private IEnumerator Start()
        {
            _loco = Combat.GetComponent<PlayerLocomotion>();
            _animator = Combat.GetComponentInChildren<Animator>();
            var bridge = Combat.GetComponent<PlayerInputBridge>();
            if (bridge != null) bridge.enabled = false;
            ComboDefinition original = Combat.Combo;

            foreach (string name in Combos)
            {
                ComboDefinition combo = FindCombo(name);
                if (combo == null) { Fail($"{name}: combo asset not found"); continue; }
                yield return SingleStep(combo, name, expectPass: true);
            }

            yield return Chain(FindCombo("Combo_PlayerMelee"));

            // Fault injection: the same shipped step 1 with a fallback recovery timer far shorter than its clip.
            ComboDefinition faulty = Object.Instantiate(FindCombo("Combo_PlayerMelee"));
            faulty.name = "Combo_PlayerMelee (fault: recovery 0.05s)";
            typeof(ComboStep).GetField("_recoveryDuration", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(faulty.Steps[0], 0.05f);
            yield return SingleStep(faulty, faulty.name, expectPass: false);
            Object.Destroy(faulty);

            yield return Settle();
            Combat.SetCombo(original);
            _loco.SetMoveInput(Vector2.zero);
            if (bridge != null) bridge.enabled = true;

            string verdict = _allPass ? "PASS" : "FAIL";
            _log.Insert(0, $"[VerifyAttackEnd] => {verdict}\n");
            System.IO.File.WriteAllText("Temp/VerifyAttackEnd.txt", _log.ToString());
            if (_allPass) Debug.Log(_log.ToString()); else Debug.LogError(_log.ToString());
            Destroy(gameObject);
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

        private void Fail(string line)
        {
            _allPass = false;
            _log.AppendLine("  FAIL " + line);
        }

        private IEnumerator Settle()
        {
            _loco.SetMoveInput(Vector2.zero);
            float t = 0f;
            while ((Combat.IsAttacking || !_loco.IsGrounded || _loco.IsRolling) && t < 4f)
            {
                t += Time.deltaTime;
                yield return null;
            }

            for (int i = 0; i < 20; i++) yield return null;
        }

        private IEnumerator Place()
        {
            yield return Settle();
            var cc = Combat.GetComponent<CharacterController>();
            Transform player = Combat.transform;
            Vector3 target = Dummy.transform.position;
            cc.enabled = false;
            player.position = new Vector3(target.x, player.position.y, target.z - 1.3f);
            player.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            cc.enabled = true;
            Physics.SyncTransforms();
            for (int i = 0; i < 10; i++) yield return null;
        }

        // The normalized time of a step's Animator state, preferring the state being transitioned into — the same
        // reading PlayerCombat uses — or -1 when that state is not playing.
        private float StateTime(int stateHash)
        {
            if (_animator.IsInTransition(0))
            {
                AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(0);
                if (next.shortNameHash == stateHash) return next.normalizedTime;
            }

            AnimatorStateInfo current = _animator.GetCurrentAnimatorStateInfo(0);
            return current.shortNameHash == stateHash ? current.normalizedTime : -1f;
        }

        private IEnumerator SingleStep(ComboDefinition combo, string label, bool expectPass)
        {
            yield return Place();
            Combat.SetCombo(combo);
            yield return null;
            if (Combat.Combo != combo) { Fail($"{label}: SetCombo did not equip it while idle"); yield break; }

            ComboStep step = combo.Steps[0];
            AttackDefinition attack = step.Attack;
            if (attack == null) { Fail($"{label}: step 1 has no attack asset — not on the asset pipeline"); yield break; }

            bool cancellable = attack.CancelFrom > 0f;
            float expected = cancellable ? Mathf.Max(attack.CancelFrom, attack.LastContactEnd) : attack.CommitEnd;
            int stateHash = Animator.StringToHash(step.AnimatorState);

            _loco.SetMoveInput(Held);
            yield return null;
            Combat.TryAttack();
            yield return new WaitForEndOfFrame();
            if (!Combat.IsAttacking) { Fail($"{label}: the attack did not start"); yield break; }

            int frame = 0, crossFrame = -1, releaseFrame = -1, strayFrame = -1;
            float lastTime = 0f, releaseTime = -1f, rootedMax = 0f;
            bool strayIgnored = true, endedWithCommit = false;
            while (frame < 600)
            {
                _loco.SetMoveInput(Held);
                float t = StateTime(stateHash);
                if (t >= 0f) lastTime = Mathf.Max(lastTime, t);
                if (crossFrame < 0 && lastTime >= expected) crossFrame = frame;

                if (!Combat.IsMovementCommitted)
                {
                    releaseFrame = frame;
                    releaseTime = lastTime;
                    endedWithCommit = !Combat.IsAttacking;
                    break;
                }

                rootedMax = Mathf.Max(rootedMax, _loco.CurrentPlanarSpeed);

                // Mid-commitment (between the first window and the commit end), fire the old clip-event seam once.
                if (strayFrame < 0 && lastTime > attack.FirstContact && lastTime < expected - 0.05f)
                {
                    strayFrame = frame;
                    Combat.NotifyAttackEnd();
                    strayIgnored = Combat.IsMovementCommitted && Combat.IsAttacking;
                }

                frame++;
                yield return new WaitForEndOfFrame();
            }

            float speedAfter = 0f;
            for (int i = 0; i < 25; i++)
            {
                _loco.SetMoveInput(Held);
                speedAfter = Mathf.Max(speedAfter, _loco.CurrentPlanarSpeed);
                yield return null;
            }

            _loco.SetMoveInput(Vector2.zero);

            var problems = new List<string>();
            if (releaseFrame < 0) problems.Add("never released (stranded)");
            if (strayFrame < 0) problems.Add("no mid-commit frame to test the stray clip event");
            else if (!strayIgnored) problems.Add("a stray clip NotifyAttackEnd ended the asset step");
            if (rootedMax > 1.0f) problems.Add($"not rooted while committed (speed {rootedMax:0.00})");
            if (releaseFrame >= 0 && releaseTime < attack.LastContactEnd) problems.Add($"released at {releaseTime:0.000} before the last window closed ({attack.LastContactEnd:0.000})");
            if (releaseFrame >= 0 && (crossFrame < 0 || releaseFrame < crossFrame)) problems.Add($"released early at t={releaseTime:0.000} (expected {expected:0.000}) — timer ran ahead of the clip");
            if (releaseFrame >= 0 && crossFrame >= 0 && releaseFrame - crossFrame > 1) problems.Add($"released {releaseFrame - crossFrame} frames after crossing {expected:0.000}");
            if (releaseFrame >= 0 && !endedWithCommit) problems.Add("combo outlived its commitment with no press buffered");
            if (releaseFrame >= 0 && speedAfter < 3f) problems.Add($"movement did not resume (speed {speedAfter:0.00})");

            bool pass = problems.Count == 0;
            string summary = $"{label}: end={(cancellable ? "cancel " : "commit ")}{expected:0.000} released@{releaseTime:0.000} " +
                             $"(cross f{crossFrame}, release f{releaseFrame}) rooted≤{rootedMax:0.00} strayIgnored={strayIgnored} " +
                             $"comboEnded={endedWithCommit} speedAfter={speedAfter:0.00}";
            if (expectPass)
            {
                if (pass) _log.AppendLine("  ok   " + summary);
                else Fail(summary + " — " + string.Join("; ", problems));
            }
            else
            {
                // The fault must be caught: the probe is only meaningful if it fails on a known-bad configuration.
                if (pass) Fail("fault injection NOT detected: " + summary);
                else _log.AppendLine("  ok   fault detected as intended: " + label + " — " + string.Join("; ", problems));
            }
        }

        private IEnumerator Chain(ComboDefinition combo)
        {
            yield return Place();
            Combat.SetCombo(combo);
            yield return null;

            IReadOnlyList<ComboStep> steps = combo.Steps;
            var hashes = new int[steps.Count];
            for (int i = 0; i < steps.Count; i++) hashes[i] = Animator.StringToHash(steps[i].AnimatorState);

            Combat.TryAttack();
            yield return new WaitForEndOfFrame();
            int currentStep = 0, frame = 0;
            float prevStepTime = 0f, lastTime = 0f;
            bool pressedFor = false;
            var problems = new List<string>();
            var starts = new StringBuilder();
            while (frame < 900 && Combat.IsMovementCommitted)
            {
                int index = Combat.ComboStepIndex;
                if (index != currentStep && index >= 0)
                {
                    AttackDefinition prev = steps[currentStep].Attack;
                    starts.Append($" step{index + 1}@prev t={prevStepTime:0.000}");
                    if (prevStepTime < prev.LastContactEnd - 0.001f) problems.Add($"step {index + 1} chained at {prevStepTime:0.000}, before step {currentStep + 1}'s last window closed ({prev.LastContactEnd:0.000})");
                    if (prev.CommitEnd > 0f && prevStepTime >= prev.CommitEnd) problems.Add($"step {index + 1} chained only after step {currentStep + 1}'s commit end — recovery not skipped");
                    currentStep = index;
                    pressedFor = false;
                    lastTime = 0f;
                }

                float t = StateTime(hashes[currentStep]);
                if (t >= 0f) lastTime = Mathf.Max(lastTime, t);
                prevStepTime = lastTime;

                // Press ahead once per step (during its wind-up), never on the last step.
                if (!pressedFor && currentStep + 1 < steps.Count)
                {
                    Combat.TryAttack();
                    pressedFor = true;
                }

                frame++;
                yield return new WaitForEndOfFrame();
            }

            // Sample the release frame itself (the loop exits on it before reading the clip time).
            float releaseTime = StateTime(hashes[currentStep]);
            if (releaseTime >= 0f) lastTime = Mathf.Max(lastTime, releaseTime);

            AttackDefinition last = steps[steps.Count - 1].Attack;
            if (currentStep != steps.Count - 1) problems.Add($"chain stopped at step {currentStep + 1}");
            if (Combat.IsMovementCommitted) problems.Add("finisher never released");
            else if (lastTime < last.CommitEnd - 0.001f) problems.Add($"finisher released at {lastTime:0.000} before its commit end {last.CommitEnd:0.000}");
            if (Combat.IsAttacking) problems.Add("combo outlived the finisher's commitment");

            string summary = $"chain {combo.name}:{starts} finisher released@{lastTime:0.000} (commit {last.CommitEnd:0.000})";
            if (problems.Count == 0) _log.AppendLine("  ok   " + summary);
            else Fail(summary + " — " + string.Join("; ", problems));
        }
    }
}
