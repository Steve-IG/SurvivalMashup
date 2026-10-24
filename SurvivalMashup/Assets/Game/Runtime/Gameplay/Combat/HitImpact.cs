using ToyChest.Framework.Objects;
using UnityEngine;

namespace ToyChest.Gameplay.Combat
{
    /// <summary>
    /// The physical weight of one confirmed blow, delivered to the struck body after its damage has been applied: how
    /// hard it hit (<see cref="Power"/> — the attack's authored impact × the attacker's Impact Power attribute), which
    /// way it pushes (<see cref="Direction"/>, planar, attacker → target), and where it connected. Damage is the
    /// ability's job; this carries only what a body's <see cref="IHitReactionReceiver"/> needs to decide whether it
    /// flinches, staggers, or is knocked back (see <see cref="HitReactionResolver"/>).
    /// </summary>
    public readonly struct HitImpact
    {
        /// <summary>How hard the blow hit, in the same units as a target's Stability attribute.</summary>
        public readonly float Power;

        /// <summary>Planar unit direction the blow pushes the target (attacker → target).</summary>
        public readonly Vector3 Direction;

        /// <summary>World-space point where the blow connected.</summary>
        public readonly Vector3 Point;

        /// <summary>The attacking Gameplay Object (may be null for environmental sources).</summary>
        public readonly GameplayObject Source;

        public HitImpact(float power, Vector3 direction, Vector3 point, GameplayObject source)
        {
            Power = Mathf.Max(0f, power);
            Vector3 planar = new Vector3(direction.x, 0f, direction.z);
            Direction = planar.sqrMagnitude > 1e-6f ? planar.normalized : Vector3.zero;
            Point = point;
            Source = source;
        }
    }

    /// <summary>
    /// Implemented by a character's behaviour adapter (enemies today; companions or the player later) to react to the
    /// physical weight of a blow. Attackers deliver a <see cref="HitImpact"/> to the struck body after the ability has
    /// applied its damage; the receiver resolves the reaction against its own Stability with
    /// <see cref="HitReactionResolver"/> and acts on it (hit animation, attack interrupt, knockback).
    /// </summary>
    public interface IHitReactionReceiver
    {
        /// <summary>Reacts to one confirmed blow. Returns the reaction that was applied.</summary>
        HitReaction ReceiveImpact(in HitImpact impact);
    }
}
