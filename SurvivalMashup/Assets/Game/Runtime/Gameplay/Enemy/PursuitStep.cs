using UnityEngine;

namespace ToyChest.Gameplay.Enemy
{
    /// <summary>
    /// One step of the enemy's pursuit plan: the planar velocity to apply this frame and the phase
    /// that produced it. The phase is what makes the behaviour <em>readable</em> — Idle, Pursue,
    /// or Attack — and is what tests assert.
    /// </summary>
    public readonly struct PursuitStep
    {
        /// <summary>An idle step: no target in range, no motion, no attack.</summary>
        public static readonly PursuitStep Idle = new PursuitStep(Vector2.zero, PursuitPhase.Idle);

        /// <summary>Creates a step.</summary>
        public PursuitStep(Vector2 planarVelocity, PursuitPhase phase)
        {
            PlanarVelocity = planarVelocity;
            Phase = phase;
        }

        /// <summary>Desired planar (XZ) velocity for this frame.</summary>
        public Vector2 PlanarVelocity { get; }

        /// <summary>The behaviour phase that produced this step.</summary>
        public PursuitPhase Phase { get; }

        /// <summary>Whether the enemy should attempt its attack this frame.</summary>
        public bool ShouldAttack => Phase == PursuitPhase.Attack;
    }

    /// <summary>The three readable phases of the enemy's minute-to-minute behaviour.</summary>
    public enum PursuitPhase
    {
        /// <summary>No target within aggro range: hold position.</summary>
        Idle,

        /// <summary>Target acquired and out of reach: close the distance.</summary>
        Pursue,

        /// <summary>Target within striking range: stop and attack.</summary>
        Attack,
    }
}
