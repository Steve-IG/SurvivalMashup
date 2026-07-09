using NUnit.Framework;
using ToyChest.Gameplay.Player;
using UnityEngine;

namespace ToyChest.Tests.Player
{
    /// <summary>
    /// Verifies the deterministic locomotion math in isolation (no scene, no MonoBehaviour): zero
    /// intent yields no motion, intent is camera-relative, diagonals are clamped so they are not
    /// faster, and speed scales the result. This is the pure gameplay-facing core of movement; the
    /// MonoBehaviour adapter only feeds it input and a speed.
    /// </summary>
    public sealed class LocomotionMotorTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void PlanarVelocity_NoIntent_IsZero()
        {
            Vector3 velocity = LocomotionMotor.PlanarVelocity(Vector2.zero, Vector3.forward, 5f);

            Assert.AreEqual(0f, velocity.magnitude, Tolerance);
        }

        [Test]
        public void PlanarVelocity_ForwardIntent_MovesAlongCameraForward()
        {
            Vector3 velocity = LocomotionMotor.PlanarVelocity(new Vector2(0f, 1f), Vector3.forward, 5f);

            Assert.AreEqual(new Vector3(0f, 0f, 5f), velocity);
        }

        [Test]
        public void PlanarVelocity_IsCameraRelative()
        {
            // Camera facing +X: forward intent should move along +X.
            Vector3 velocity = LocomotionMotor.PlanarVelocity(new Vector2(0f, 1f), Vector3.right, 5f);

            Assert.AreEqual(5f, velocity.x, Tolerance);
            Assert.AreEqual(0f, velocity.z, Tolerance);
        }

        [Test]
        public void PlanarVelocity_StrafeIntent_MovesAlongCameraRight()
        {
            // Camera facing +Z: right is +X, so strafe-right intent moves along +X.
            Vector3 velocity = LocomotionMotor.PlanarVelocity(new Vector2(1f, 0f), Vector3.forward, 5f);

            Assert.AreEqual(5f, velocity.x, Tolerance);
            Assert.AreEqual(0f, velocity.z, Tolerance);
        }

        [Test]
        public void PlanarVelocity_DiagonalIntent_IsClampedToSpeed()
        {
            Vector3 velocity = LocomotionMotor.PlanarVelocity(new Vector2(1f, 1f), Vector3.forward, 5f);

            Assert.AreEqual(5f, velocity.magnitude, Tolerance,
                "A full diagonal must not exceed the movement speed.");
        }

        [Test]
        public void PlanarVelocity_FlattensCameraPitch()
        {
            // A camera pitched downward still drives horizontal movement only.
            Vector3 pitched = new Vector3(0f, -1f, 1f);
            Vector3 velocity = LocomotionMotor.PlanarVelocity(new Vector2(0f, 1f), pitched, 5f);

            Assert.AreEqual(0f, velocity.y, Tolerance);
            Assert.AreEqual(5f, velocity.magnitude, Tolerance);
        }

        [Test]
        public void PlanarVelocity_NegativeSpeed_IsClampedToZero()
        {
            Vector3 velocity = LocomotionMotor.PlanarVelocity(new Vector2(0f, 1f), Vector3.forward, -5f);

            Assert.AreEqual(0f, velocity.magnitude, Tolerance);
        }
    }
}
