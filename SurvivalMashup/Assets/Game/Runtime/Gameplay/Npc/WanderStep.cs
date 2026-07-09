using UnityEngine;

namespace ToyChest.Gameplay.Npc
{
    /// <summary>
    /// The result of one <see cref="WanderMotor.Tick"/>: the desired planar velocity for this frame
    /// and whether the NPC is actively moving. It carries no engine coupling; the adapter turns the
    /// planar velocity into a horizontal <see cref="CharacterController"/> move and a facing.
    /// </summary>
    public readonly struct WanderStep
    {
        /// <summary>An idle step: no motion this frame.</summary>
        public static readonly WanderStep Idle = new WanderStep(Vector2.zero, false);

        /// <summary>Builds a step from a planar velocity and whether the NPC is moving.</summary>
        public WanderStep(Vector2 planarVelocity, bool isMoving)
        {
            PlanarVelocity = planarVelocity;
            IsMoving = isMoving;
        }

        /// <summary>Desired horizontal velocity in world units per second (x, z packed as x, y).</summary>
        public Vector2 PlanarVelocity { get; }

        /// <summary>Whether the NPC is steering toward a destination this frame.</summary>
        public bool IsMoving { get; }
    }
}
