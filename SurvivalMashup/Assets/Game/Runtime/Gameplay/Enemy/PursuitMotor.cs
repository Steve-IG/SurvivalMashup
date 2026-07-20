using UnityEngine;

namespace ToyChest.Gameplay.Enemy
{
    /// <summary>
    /// Pure, deterministic pursuit planner, separated from any MonoBehaviour so it is testable
    /// without a scene (Engine Principle 17, Favor Deterministic Behavior). It is the enemy
    /// counterpart to the NPC's <c>WanderMotor</c>: given the enemy's position, the target's
    /// position (if any), the enemy's speed, and the ranges, it decides the readable phase —
    /// Idle, Pursue, or Attack — and the planar velocity for the step.
    ///
    /// It is intentionally minimal: no behaviour tree, no state-machine framework, no scheduler,
    /// no internal timers. Attack <em>cadence</em> is owned by the enemy's attack Ability cooldown,
    /// not here — this planner simply reports "in striking range" as an Attack phase every frame,
    /// and the Ability System rate-limits the actual strikes. Holds no state; a given set of
    /// inputs always yields the same step.
    /// </summary>
    public sealed class PursuitMotor
    {
        /// <summary>
        /// Plans one step. When <paramref name="hasTarget"/> is false the enemy is Idle. Otherwise,
        /// if the target is within <paramref name="attackRange"/> the enemy stops and Attacks; if it
        /// is within <paramref name="aggroRange"/> the enemy Pursues at <paramref name="speed"/>;
        /// beyond aggro range it is Idle. A non-positive speed yields no motion (still reports the
        /// phase, so an in-range enemy still attacks).
        /// </summary>
        public PursuitStep Tick(
            Vector2 position, Vector2 target, bool hasTarget, float speed, float aggroRange, float attackRange)
        {
            if (!hasTarget)
            {
                return PursuitStep.Idle;
            }

            Vector2 toTarget = target - position;
            float distance = toTarget.magnitude;

            if (distance <= attackRange)
            {
                // In reach: stop and attack. Cadence is the attack ability's cooldown.
                return new PursuitStep(Vector2.zero, PursuitPhase.Attack);
            }

            if (distance > aggroRange)
            {
                return PursuitStep.Idle;
            }

            if (speed <= 0f || distance <= 1e-4f)
            {
                return new PursuitStep(Vector2.zero, PursuitPhase.Pursue);
            }

            return new PursuitStep(toTarget / distance * speed, PursuitPhase.Pursue);
        }
    }
}
