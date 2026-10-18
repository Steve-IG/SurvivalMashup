using System.Collections;
using ToyChest.Gameplay.Enemy;
using ToyChest.Gameplay.Player;
using UnityEngine;

/// <summary>
/// Play-mode verification of three combo-feel fixes, driving PlayerCombat / PlayerLocomotion directly:
/// (A) a LATE press — after hit 1's commitment has already ended — still advances to hit 2 (wider window);
/// (B) the leaping finisher releases the player when its motion ends, and a roll leaves no leftover rooting;
/// (C) once released, a held stick makes the Animator leave the attack clip promptly (no walking slide).
/// Reads public seams only; adds no gameplay.
/// </summary>
public static class VerifyComboFixes
{
    public static string Execute()
    {
        var combat = Object.FindAnyObjectByType<PlayerCombat>();
        var dummy = Object.FindAnyObjectByType<TrainingDummy>();
        if (combat == null || dummy == null)
        {
            Debug.LogError("[VerifyComboFixes] Missing PlayerCombat or TrainingDummy.");
            return "missing";
        }

        var probe = new GameObject("~ComboFixesProbe").AddComponent<Probe>();
        probe.Combat = combat;
        probe.Dummy = dummy;
        return "probe started; read the console for [VerifyComboFixes]";
    }

    public sealed class Probe : MonoBehaviour
    {
        public PlayerCombat Combat;
        public TrainingDummy Dummy;

        private PlayerLocomotion _loco;
        private Animator _animator;

        private IEnumerator Start()
        {
            _loco = Combat.GetComponent<PlayerLocomotion>();
            _animator = Combat.GetComponentInChildren<Animator>();
            var bridge = Combat.GetComponent<PlayerInputBridge>();
            if (bridge != null) bridge.enabled = false;
            for (int i = 0; i < 5; i++) yield return null;

            // (A) Late continuation: press once, wait until hit 1 has RELEASED, wait a further 0.25s, press.
            yield return Place();
            Combat.TryAttack();
            yield return WaitUntil(() => Combat.IsAttacking && !Combat.IsMovementCommitted, 3f);
            yield return new WaitForSeconds(0.25f);
            Combat.TryAttack();
            yield return null;
            // Current rule (continuation window closes at the commitment end): a press after release restarts at hit 1.
            bool lateContinues = Combat.ComboStepIndex == 0;
            yield return WaitUntil(() => !Combat.IsAttacking, 6f);

            // (B1) Finisher: chain to hit 3, then measure how long hit 3 keeps the player committed.
            yield return Place();
            Combat.TryAttack();
            float started3 = -1f;
            float t = 0f;
            while (t < 3f && started3 < 0f)
            {
                if (Combat.ComboStepIndex < 2) Combat.TryAttack();
                else started3 = Time.time;
                t += Time.deltaTime;
                yield return null;
            }

            yield return WaitUntil(() => !Combat.IsMovementCommitted, 6f);
            float finisherCommit = started3 >= 0f ? Time.time - started3 : -1f;
            yield return WaitUntil(() => !Combat.IsAttacking, 2f);

            // (B2) Roll out of a committed attack: the roll ends the combo and nothing roots the player after.
            yield return Place();
            Combat.TryAttack();
            yield return new WaitForSeconds(0.1f);
            _loco.Roll();
            yield return WaitUntil(() => !_loco.IsRolling, 2f);
            yield return null;
            bool freeAfterRoll = !Combat.IsAttacking && !_loco.IsMovementCommitted;

            // (C) Released + stick held: the Animator must hand off to locomotion promptly.
            yield return Place();
            Combat.TryAttack();
            yield return WaitUntil(() => Combat.IsAttacking && !Combat.IsMovementCommitted, 3f);
            float releasedAt = Time.time;
            float handOff = -1f;
            while (Time.time - releasedAt < 1f)
            {
                _loco.SetMoveInput(new Vector2(1f, 0f));
                AnimatorStateInfo s = _animator.GetCurrentAnimatorStateInfo(0);
                bool inAttack = s.IsName("Attack") || s.IsName("Combo2") || s.IsName("Combo3");
                if (!inAttack && !_animator.IsInTransition(0))
                {
                    handOff = Time.time - releasedAt;
                    break;
                }

                yield return null;
            }

            _loco.SetMoveInput(Vector2.zero);
            if (bridge != null) bridge.enabled = true;

            // Must release at the clip's OnAttackEnd (~1.87s), measurably before its 2.02s fallback timer.
            bool finisherOk = finisherCommit > 0f && finisherCommit < 1.97f;
            bool handOffOk = handOff >= 0f && handOff < 0.3f;
            bool pass = lateContinues && finisherOk && freeAfterRoll && handOffOk;
            string msg = $"[VerifyComboFixes] (A) pressAfterReleaseRestartsAtHit1={lateContinues} | " +
                         $"(B) finisherCommitted={finisherCommit:0.00}s freeAfterRoll={freeAfterRoll} | " +
                         $"(C) animatorHandOffAfterRelease={handOff:0.00}s => {(pass ? "PASS" : "FAIL")}";
            if (pass) Debug.Log(msg); else Debug.LogError(msg);
            Destroy(gameObject);
        }

        private IEnumerator Place()
        {
            yield return new WaitForSeconds(0.3f);
            Transform player = Combat.transform;
            Transform target = Dummy.transform;
            player.position = target.position + new Vector3(0f, 0f, -1.3f);
            player.rotation = Quaternion.LookRotation((target.position - player.position).normalized, Vector3.up);
            Physics.SyncTransforms();
            yield return null;
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition, float timeout)
        {
            float t = 0f;
            while (!condition() && t < timeout)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }
    }
}
