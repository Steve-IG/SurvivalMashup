using System.Collections;
using ToyChest.Gameplay.Player;
using UnityEngine;

/// <summary>
/// Play-mode probe for the attack root-motion grounding bug: a root-motion attack clip must never make the
/// character read as airborne. Triggers one swing and samples every frame for 2s, recording whether
/// PlayerLocomotion.IsGrounded ever went false and whether the animator entered Fall or Land. Before the
/// fix, an attack clip with forward root motion applied a second, vertical-less CharacterController.Move
/// (from OnAnimatorMove, after Update), which cleared isGrounded → Fall → a spurious Land on swing end.
/// Spawns a temporary probe object and logs the verdict; the object destroys itself.
/// </summary>
public static class VerifyAttackGrounding
{
    public static string Execute()
    {
        var combat = Object.FindAnyObjectByType<PlayerCombat>();
        var locomotion = Object.FindAnyObjectByType<PlayerLocomotion>();
        if (combat == null || locomotion == null)
        {
            Debug.LogError("[VerifyAttackGrounding] Missing PlayerCombat / PlayerLocomotion.");
            return "missing player";
        }

        var go = new GameObject("~AttackGroundingProbe");
        var probe = go.AddComponent<Probe>();
        probe.Combat = combat;
        probe.Locomotion = locomotion;
        probe.ModelAnimator = combat.GetComponentInChildren<Animator>();
        return "probe started; read the console for [VerifyAttackGrounding]";
    }

    public sealed class Probe : MonoBehaviour
    {
        public PlayerCombat Combat;
        public PlayerLocomotion Locomotion;
        public Animator ModelAnimator;

        private IEnumerator Start()
        {
            // Let the character settle on the ground before swinging.
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            bool groundedBefore = Locomotion.IsGrounded;
            Combat.TryAttack();

            bool everUngrounded = false;
            bool sawFall = false;
            bool sawLand = false;
            float elapsed = 0f;

            while (elapsed < 2f)
            {
                if (!Locomotion.IsGrounded)
                {
                    everUngrounded = true;
                }

                if (ModelAnimator != null)
                {
                    AnimatorStateInfo info = ModelAnimator.GetCurrentAnimatorStateInfo(0);
                    if (info.IsName("Fall")) sawFall = true;
                    if (info.IsName("Land")) sawLand = true;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            bool pass = groundedBefore && !everUngrounded && !sawFall && !sawLand;
            string msg = $"[VerifyAttackGrounding] groundedBefore={groundedBefore} everUngroundedDuringAttack={everUngrounded} " +
                         $"sawFall={sawFall} sawLand={sawLand} => {(pass ? "PASS" : "FAIL")}";
            if (pass) { Debug.Log(msg); } else { Debug.LogError(msg); }

            Destroy(gameObject);
        }
    }
}
