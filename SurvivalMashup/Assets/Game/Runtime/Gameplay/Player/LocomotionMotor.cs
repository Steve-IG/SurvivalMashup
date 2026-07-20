using UnityEngine;

namespace ToyChest.Gameplay.Player
{
    /// <summary>
    /// Pure, deterministic locomotion math, separated from any MonoBehaviour so it is testable
    /// without a scene (Engine Principle 17, Favor Deterministic Behavior). Turns a 2D move intent
    /// and a camera facing into a camera-relative horizontal velocity. It reads no engine clock and
    /// holds no state; time and speed are injected by the caller.
    /// </summary>
    public static class LocomotionMotor
    {
        /// <summary>
        /// Camera-relative horizontal velocity for a move intent. <paramref name="moveInput"/> is
        /// (strafe, forward) in the range [-1, 1]; <paramref name="cameraForward"/> is the camera's
        /// facing (flattened onto the ground plane); <paramref name="speed"/> is units per second.
        /// The intent magnitude is clamped to 1 so diagonals are not faster, then scaled by speed.
        /// A degenerate camera forward (looking straight down) falls back to world forward.
        /// </summary>
        public static Vector3 PlanarVelocity(Vector2 moveInput, Vector3 cameraForward, float speed)
        {
            Vector3 forward = new Vector3(cameraForward.x, 0f, cameraForward.z);
            if (forward.sqrMagnitude < 1e-6f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward); // +X when forward is +Z

            Vector3 intent = right * moveInput.x + forward * moveInput.y;
            if (intent.sqrMagnitude > 1f)
            {
                intent.Normalize();
            }

            return intent * Mathf.Max(0f, speed);
        }

        /// <summary>
        /// Integrates the current planar velocity toward a desired velocity with separate
        /// acceleration and deceleration rates (units per second squared), producing weighty-but-snappy
        /// movement instead of instant velocity snaps. Speeding up (or changing direction toward a
        /// faster desired) uses <paramref name="acceleration"/>; slowing toward a lower desired speed
        /// (including releasing input, where <paramref name="desiredVelocity"/> is zero) uses
        /// <paramref name="deceleration"/>. Pure and deterministic: <paramref name="deltaTime"/> and
        /// the rates are injected, no engine clock is read.
        /// </summary>
        public static Vector3 Accelerate(
            Vector3 currentVelocity, Vector3 desiredVelocity, float acceleration, float deceleration, float deltaTime)
        {
            float rate = desiredVelocity.sqrMagnitude >= currentVelocity.sqrMagnitude
                ? Mathf.Max(0f, acceleration)
                : Mathf.Max(0f, deceleration);

            return Vector3.MoveTowards(currentVelocity, desiredVelocity, rate * Mathf.Max(0f, deltaTime));
        }

        /// <summary>
        /// The launch velocity of a momentum-based roll: the character's current planar speed plus an
        /// explosive <paramref name="impulse"/>, redirected along <paramref name="rollDirection"/>. The
        /// roll therefore <em>inherits</em> existing momentum — walking, running, and sprinting into a
        /// roll naturally produce progressively faster, longer rolls with no separate code path — while
        /// the impulse guarantees even a standing roll feels explosive. The result is clamped to
        /// <paramref name="maxSpeed"/> (when positive) so the fastest entries stay bounded. Pure and
        /// deterministic. A degenerate roll direction falls back to world forward.
        /// </summary>
        public static Vector3 RollLaunchVelocity(
            Vector3 currentPlanarVelocity, Vector3 rollDirection, float impulse, float maxSpeed)
        {
            Vector3 dir = new Vector3(rollDirection.x, 0f, rollDirection.z);
            if (dir.sqrMagnitude < 1e-6f)
            {
                dir = Vector3.forward;
            }

            dir.Normalize();

            float currentSpeed = new Vector2(currentPlanarVelocity.x, currentPlanarVelocity.z).magnitude;
            float launchSpeed = currentSpeed + Mathf.Max(0f, impulse);
            if (maxSpeed > 0f)
            {
                launchSpeed = Mathf.Min(launchSpeed, maxSpeed);
            }

            return dir * launchSpeed;
        }

        /// <summary>
        /// Projects a planar velocity into the character's own frame, giving (strafe, forward) where
        /// +Y is along <paramref name="facingForward"/> and +X is to its right. This is what a
        /// directional (8-way) locomotion blend tree consumes: "movement relative to facing". Pure and
        /// deterministic. A degenerate facing falls back to world forward.
        /// </summary>
        public static Vector2 LocalMoveVector(Vector3 planarVelocity, Vector3 facingForward)
        {
            Vector3 forward = new Vector3(facingForward.x, 0f, facingForward.z);
            if (forward.sqrMagnitude < 1e-6f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward); // +X when forward is +Z

            Vector3 planar = new Vector3(planarVelocity.x, 0f, planarVelocity.z);
            return new Vector2(Vector3.Dot(planar, right), Vector3.Dot(planar, forward));
        }
    }
}
