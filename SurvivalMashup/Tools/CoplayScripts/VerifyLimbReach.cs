using System.Collections;
using System.Collections.Generic;
using System.Text;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Combat;
using ToyChest.Gameplay.Enemy;
using ToyChest.Gameplay.HitDetection;
using ToyChest.Gameplay.Player;
using UnityEngine;

/// <summary>
/// Diagnostic: plays each combo step against one dummy at several distances and reports, per step, how close
/// each limb's strike capsule came to the dummy's collider surface (gap = surface distance − capsule radius;
/// ≤ 0 means touching), separately for "while one of that limb's windows was open" and "at any time". Shows
/// whether a miss is a reach problem (the animation never gets there) or a timing problem (it gets there
/// outside its window). Reads public seams only; adds no gameplay.
/// </summary>
public static class VerifyLimbReach
{
    public static string Execute()
    {
        var probe = new GameObject("~LimbReachProbe").AddComponent<Probe>();
        probe.Combat = Object.FindAnyObjectByType<PlayerCombat>();
        probe.Dummy = Object.FindAnyObjectByType<TrainingDummy>();
        return "probe started; read the console for [VerifyLimbReach]";
    }

    public sealed class Probe : MonoBehaviour
    {
        public PlayerCombat Combat;
        public TrainingDummy Dummy;

        private IEnumerator Start()
        {
            var bridge = Combat.GetComponent<PlayerInputBridge>();
            if (bridge != null) bridge.enabled = false;
            var profile = Combat.GetComponent<CharacterStrikeProfile>();
            Collider target = Dummy.GetComponent<Collider>() ?? Dummy.GetComponentInChildren<Collider>();
            Animator animator = Combat.GetComponentInChildren<Animator>();
            var combo = (ComboDefinition)typeof(PlayerCombat)
                .GetField("_combo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(Combat);
            var report = new StringBuilder();
            var limbs = new[] { StrikeLimb.RightHand, StrikeLimb.LeftHand, StrikeLimb.RightFoot, StrikeLimb.LeftFoot };

            foreach (float distance in new[] { 0.9f, 1.3f })
            {
                yield return new WaitForSeconds(0.4f);
                Combat.transform.position = Dummy.transform.position + new Vector3(0f, 0f, -distance);
                Combat.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                Physics.SyncTransforms();
                yield return null;

                var inWindow = new Dictionary<(int, StrikeLimb), float>();
                var any = new Dictionary<(int, StrikeLimb), float>();
                Combat.TryAttack();
                float t = 0f;
                while (t < 6f && (Combat.IsAttacking || t < 0.2f))
                {
                    if (Combat.ComboStepIndex < 2) Combat.TryAttack();
                    yield return new WaitForEndOfFrame();
                    int step = Combat.ComboStepIndex;
                    if (step >= 0 && Combat.IsMovementCommitted)
                    {
                        AttackDefinition attack = combo.Steps[step].Attack;
                        // Same rule as PlayerCombat: during a transition the entering state is the attack's clock.
                        int hash = Animator.StringToHash(combo.Steps[step].AnimatorState);
                        AnimatorStateInfo info = animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == hash
                            ? animator.GetNextAnimatorStateInfo(0)
                            : animator.GetCurrentAnimatorStateInfo(0);
                        if (info.shortNameHash != hash) continue;
                        float n = info.normalizedTime;
                        foreach (StrikeLimb limb in limbs)
                        {
                            if (!profile.TryGetSweep(limb, out _, out _, out Vector3 a, out Vector3 b, out float r)) continue;
                            Vector3 onLimb = LimbSweep.ClosestPointOnSegment(target.bounds.center, a, b);
                            float gap = (target.ClosestPoint(onLimb) - onLimb).magnitude - r;
                            var key = (step, limb);
                            any[key] = any.TryGetValue(key, out float g0) ? Mathf.Min(g0, gap) : gap;
                            bool open = false;
                            foreach (HitWindow w in attack.Windows)
                            {
                                if (StrikeLimbs.Contains(w.Limbs, limb) && n >= w.Start && n < w.End) open = true;
                            }

                            if (open) inWindow[key] = inWindow.TryGetValue(key, out float g1) ? Mathf.Min(g1, gap) : gap;
                        }
                    }

                    t += Time.deltaTime;
                }

                report.Append($"\n d={distance:0.0}m:");
                for (int step = 0; step < 3; step++)
                {
                    report.Append($"\n   hit{step + 1}:");
                    foreach (StrikeLimb limb in limbs)
                    {
                        string win = inWindow.TryGetValue((step, limb), out float gw) ? $"{gw:+0.00;-0.00}" : "  -  ";
                        string all = any.TryGetValue((step, limb), out float ga) ? $"{ga:+0.00;-0.00}" : "  -  ";
                        report.Append($" {limb}[win {win} / any {all}]");
                    }
                }
            }

            if (bridge != null) bridge.enabled = true;
            Debug.Log("[VerifyLimbReach] closest gap to dummy surface, metres (≤0 = touching):" + report);
            Destroy(gameObject);
        }
    }
}
