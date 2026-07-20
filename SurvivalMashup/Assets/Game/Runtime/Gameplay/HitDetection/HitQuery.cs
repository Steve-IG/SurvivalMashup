using UnityEngine;

namespace ToyChest.Gameplay.HitDetection
{
    /// <summary>
    /// The pure, deterministic geometry at the heart of hit detection, separated from Unity's physics so
    /// it is fully testable without a scene (Engine Principle 17, mirroring <c>LocomotionMotor</c> and
    /// <c>MeleeSwing</c>). It answers one question — <em>is this point inside this volume?</em> — and owns
    /// no gameplay meaning. <see cref="HitDetector"/> uses it as the narrow phase behind Unity's overlap
    /// broad phase; tests call it directly to prove a punch cannot land behind the attacker, an explosion
    /// is omnidirectional, and reach is respected.
    ///
    /// Facing is evaluated on the horizontal plane (the ground plane the characters turn within), while
    /// reach is a true 3D distance, so a tall explosion still catches a target above the origin. This
    /// matches the planar facing the locomotion and combat adapters already use.
    /// </summary>
    public static class HitQuery
    {
        /// <summary>
        /// Whether <paramref name="point"/> lies inside the volume centred at <paramref name="origin"/>
        /// facing <paramref name="forward"/>. A Sphere test is reach only (omnidirectional); a Cone test
        /// additionally requires the point to fall within the frontal half-angle. A point exactly at the
        /// origin is always contained (degenerate direction).
        /// </summary>
        public static bool Contains(in HitVolume volume, Vector3 origin, Vector3 forward, Vector3 point)
        {
            Vector3 toPoint = point - origin;

            // Reach: true 3D distance, so vertical reach (a giant's overhead slam, a tall blast) counts.
            if (toPoint.sqrMagnitude > volume.Radius * volume.Radius)
            {
                return false;
            }

            if (volume.Shape != HitShape.Cone)
            {
                return true;
            }

            return WithinCone(forward, toPoint, volume.ConeHalfAngleDegrees);
        }

        /// <summary>
        /// Whether a direction to a target falls within a frontal cone of the given half-angle around
        /// <paramref name="forward"/>, measured on the horizontal plane. A target at the origin, or a
        /// degenerate forward, counts as within (there is no meaningful facing to reject).
        /// </summary>
        public static bool WithinCone(Vector3 forward, Vector3 toTarget, float halfAngleDegrees)
        {
            Vector3 flatForward = new Vector3(forward.x, 0f, forward.z);
            Vector3 flatToTarget = new Vector3(toTarget.x, 0f, toTarget.z);

            if (flatForward.sqrMagnitude < 1e-6f || flatToTarget.sqrMagnitude < 1e-6f)
            {
                return true;
            }

            float half = Mathf.Clamp(halfAngleDegrees, 0f, 180f);
            float angle = Vector3.Angle(flatForward, flatToTarget);
            return angle <= half + 1e-4f;
        }
    }
}
