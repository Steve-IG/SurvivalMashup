using System.Collections;
using ToyChest.Gameplay.Enemy;
using ToyChest.Gameplay.Player;
using UnityEngine;

/// <summary>
/// Play-mode verification of the finisher's cancellable recovery (AttackDefinition.CancelFrom):
/// (A) holding the stick through the finisher — the get-up is cut at the cancel point: the commitment drops,
///     the player is running (speed &gt; 3) well before the old commit end, and the animator leaves Combo3;
/// (B) mashing attack through the finisher — a fresh combo (step 0) starts at the cancel point;
/// (C) both still land every finisher hit first (kicks + slam), i.e. cancelling never cuts a strike.
/// Times are from the finisher's start. Reads public seams only; adds no gameplay.
/// </summary>
public static class VerifyFinisherCancel
{
    public static string Execute()
    {
        var combat = Object.FindAnyObjectByType<PlayerCombat>();
        var dummy = Object.FindAnyObjectByType<TrainingDummy>();
        if (combat == null || dummy == null)
        {
            Debug.LogError("[VerifyFinisherCancel] Missing PlayerCombat or TrainingDummy.");
            return "missing";
        }

        var probe = new GameObject("~FinisherCancelProbe").AddComponent<Probe>();
        probe.Combat = combat;
        probe.Dummy = dummy;
        return "probe started; read the console for [VerifyFinisherCancel]";
    }

    public sealed class Probe : MonoBehaviour
    {
        public PlayerCombat Combat;
        public TrainingDummy Dummy;

        private int _hits;
        private float _finisherStart = -1f;

        private void OnImpacted(Vector3 p)
        {
            if (_finisherStart >= 0f) _hits++;
        }

        private IEnumerator Start()
        {
            var bridge = Combat.GetComponent<PlayerInputBridge>();
            var loco = Combat.GetComponent<PlayerLocomotion>();
            var animator = Combat.GetComponentInChildren<Animator>();
            if (bridge != null) bridge.enabled = false;
            for (int i = 0; i < 5; i++) yield return null;
            Combat.Impacted += OnImpacted;

            // (A) Stick held (toward the camera-relative forward) from the slam onward.
            yield return ChainToFinisher();
            int hitsA = 0;
            float releasedA = -1f, runningA = -1f, leftStateA = -1f;
            int combo3 = Animator.StringToHash("Combo3");
            while (Time.time - _finisherStart < 3f)
            {
                float t = Time.time - _finisherStart;
                if (t > 0.9f) loco.SetMoveInput(new Vector2(0f, 1f)); // after the slam lands
                if (releasedA < 0f && !Combat.IsMovementCommitted) releasedA = t;
                if (runningA < 0f && loco.CurrentPlanarSpeed > 3f) runningA = t;
                if (leftStateA < 0f && animator != null && animator.GetCurrentAnimatorStateInfo(0).shortNameHash != combo3 &&
                    !animator.IsInTransition(0) && t > 0.5f) leftStateA = t;
                if (runningA >= 0f && leftStateA >= 0f) break;
                yield return null;
            }

            hitsA = _hits;
            loco.SetMoveInput(Vector2.zero);
            yield return new WaitForSeconds(1f);

            // (B) Attack mashed through the finisher (from just after the slam).
            yield return ChainToFinisher();
            float restartB = -1f;
            while (Time.time - _finisherStart < 3f)
            {
                float t = Time.time - _finisherStart;
                if (t > 0.9f)
                {
                    Combat.TryAttack();
                    if (Combat.ComboStepIndex == 0) { restartB = t; break; }
                }

                yield return null;
            }

            int hitsB = _hits;
            yield return new WaitUntilOrTimeout(() => !Combat.IsAttacking, 3f);
            Combat.Impacted -= OnImpacted;
            if (bridge != null) bridge.enabled = true;

            const float oldCommitEnd = 1.87f;
            bool pass = releasedA > 0.9f && releasedA < 1.3f && runningA > 0f && runningA < 1.6f &&
                        leftStateA > 0f && leftStateA < 1.6f && restartB > 0.9f && restartB < 1.3f &&
                        hitsA >= 3 && hitsB >= 3;
            string msg = $"[VerifyFinisherCancel] (A) stick: released={releasedA:0.00}s running={runningA:0.00}s " +
                         $"animatorLeftCombo3={leftStateA:0.00}s (was committed to {oldCommitEnd:0.00}s) hits={hitsA} | " +
                         $"(B) mash: newComboAt={restartB:0.00}s hits={hitsB} => {(pass ? "PASS" : "FAIL")}";
            if (pass) Debug.Log(msg); else Debug.LogError(msg);
            Destroy(gameObject);
        }

        // Place in front of the dummy, chain hit1 → hit2 → finisher; returns when the finisher starts.
        private IEnumerator ChainToFinisher()
        {
            yield return new WaitForSeconds(0.3f);
            Transform player = Combat.transform;
            player.position = Dummy.transform.position + new Vector3(0f, 0f, -2.0f);
            player.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            Physics.SyncTransforms();
            yield return null;

            _hits = 0;
            _finisherStart = -1f;
            Combat.TryAttack();
            float t = 0f;
            while (t < 4f && _finisherStart < 0f)
            {
                if (Combat.ComboStepIndex < 2) Combat.TryAttack();
                else _finisherStart = Time.time;
                t += Time.deltaTime;
                yield return null;
            }
        }

        private sealed class WaitUntilOrTimeout : CustomYieldInstruction
        {
            private readonly System.Func<bool> _condition;
            private readonly float _deadline;

            public WaitUntilOrTimeout(System.Func<bool> condition, float timeout)
            {
                _condition = condition;
                _deadline = Time.time + timeout;
            }

            public override bool keepWaiting => !_condition() && Time.time < _deadline;
        }
    }
}
