using UnityEngine;

namespace ToyChest.Gameplay.HitDetection
{
    /// <summary>
    /// The pure, deterministic math that places a hit volume in the world — the engine-independent core of
    /// socket/bone anchoring, testable with no scene (like <see cref="HitQuery"/>). Given an anchor's world
    /// pose (a hand bone, a weapon tip, a projectile nose) and an authored local offset, it produces the
    /// query origin and forward. Because the anchor pose is read live each query, a volume placed on a bone
    /// <em>follows that bone automatically</em> through the animation — no per-frame bookkeeping and no
    /// per-attack hand-positioning. The offset is expressed in the anchor's local space, so one authored
    /// "+5cm forward" reused across many characters lands correctly on each rig.
    /// </summary>
    public static class HitAnchor
    {
        /// <summary>
        /// The world origin of a volume anchored at <paramref name="anchorPosition"/>/
        /// <paramref name="anchorRotation"/> with an authored <paramref name="localOffset"/> (in the
        /// anchor's local space). Reused across characters: the same offset rotates with each rig's bone.
        /// </summary>
        public static Vector3 ResolveOrigin(Vector3 anchorPosition, Quaternion anchorRotation, Vector3 localOffset)
        {
            return anchorPosition + anchorRotation * localOffset;
        }

        /// <summary>The anchor's forward direction (its local +Z in world space).</summary>
        public static Vector3 ResolveForward(Quaternion anchorRotation)
        {
            return anchorRotation * Vector3.forward;
        }
    }
}
