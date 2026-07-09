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
    }
}
