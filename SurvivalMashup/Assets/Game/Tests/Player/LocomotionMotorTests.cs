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

        [Test]
        public void Accelerate_MovesTowardDesired_ByAccelerationTimesDelta()
        {
            // From rest toward 5 u/s at 50 u/s^2 over 0.05s advances 2.5 u/s (accel not yet at target).
            Vector3 next = LocomotionMotor.Accelerate(
                Vector3.zero, new Vector3(0f, 0f, 5f), acceleration: 50f, deceleration: 80f, deltaTime: 0.05f);

            Assert.AreEqual(2.5f, next.z, Tolerance);
        }

        [Test]
        public void Accelerate_NeverOvershootsDesired()
        {
            // A large step is clamped exactly to the desired velocity, never past it.
            Vector3 next = LocomotionMotor.Accelerate(
                Vector3.zero, new Vector3(0f, 0f, 5f), acceleration: 50f, deceleration: 80f, deltaTime: 10f);

            Assert.AreEqual(new Vector3(0f, 0f, 5f), next);
        }

        [Test]
        public void Accelerate_UsesDecelerationWhenSlowingToStop()
        {
            // Releasing input (desired = zero) decays the current velocity by deceleration*delta.
            Vector3 next = LocomotionMotor.Accelerate(
                new Vector3(0f, 0f, 5f), Vector3.zero, acceleration: 50f, deceleration: 80f, deltaTime: 0.05f);

            Assert.AreEqual(1f, next.z, Tolerance, "5 - 80*0.05 = 1: deceleration governs stopping.");
        }

        [Test]
        public void Accelerate_AtDesired_Holds()
        {
            Vector3 desired = new Vector3(0f, 0f, 5f);
            Vector3 next = LocomotionMotor.Accelerate(desired, desired, 50f, 80f, 0.05f);

            Assert.AreEqual(desired, next);
        }

        [Test]
        public void LocalMoveVector_MovingAlongFacing_IsPureForward()
        {
            Vector2 local = LocomotionMotor.LocalMoveVector(new Vector3(0f, 0f, 5f), Vector3.forward);

            Assert.AreEqual(0f, local.x, Tolerance, "No strafe when moving straight ahead.");
            Assert.AreEqual(5f, local.y, Tolerance, "Full forward.");
        }

        [Test]
        public void LocalMoveVector_MovingRightOfFacing_IsPureStrafe()
        {
            Vector2 local = LocomotionMotor.LocalMoveVector(new Vector3(5f, 0f, 0f), Vector3.forward);

            Assert.AreEqual(5f, local.x, Tolerance, "Moving to facing-right is +strafe.");
            Assert.AreEqual(0f, local.y, Tolerance, "No forward component.");
        }

        [Test]
        public void LocalMoveVector_IsRelativeToFacing()
        {
            // Facing +X (right); moving +Z world = to the character's left, so strafe is negative.
            Vector2 local = LocomotionMotor.LocalMoveVector(new Vector3(0f, 0f, 5f), Vector3.right);

            Assert.AreEqual(-5f, local.x, Tolerance);
            Assert.AreEqual(0f, local.y, Tolerance);
        }

        [Test]
        public void RollLaunch_FromRest_LaunchesAtImpulseAlongDirection()
        {
            // A standing roll has no momentum to inherit, so the impulse alone drives it — still explosive.
            Vector3 launch = LocomotionMotor.RollLaunchVelocity(
                Vector3.zero, Vector3.forward, impulse: 8f, maxSpeed: 20f);

            Assert.AreEqual(new Vector3(0f, 0f, 8f), launch);
        }

        [Test]
        public void RollLaunch_InheritsExistingMomentum()
        {
            // Rolling forward while already running at 7 keeps that speed and adds the impulse on top.
            Vector3 launch = LocomotionMotor.RollLaunchVelocity(
                new Vector3(0f, 0f, 7f), Vector3.forward, impulse: 8f, maxSpeed: 20f);

            Assert.AreEqual(15f, launch.z, Tolerance, "Roll = current speed (7) + impulse (8).");
        }

        [Test]
        public void RollLaunch_FasterEntryProducesFasterRoll()
        {
            // The whole point of momentum inheritance: sprinting into a roll is stronger than walking,
            // with no separate code path — the launch speed rises monotonically with entry speed.
            float walkRoll = LocomotionMotor.RollLaunchVelocity(
                new Vector3(0f, 0f, 3.5f), Vector3.forward, impulse: 8f, maxSpeed: 100f).magnitude;
            float runRoll = LocomotionMotor.RollLaunchVelocity(
                new Vector3(0f, 0f, 7f), Vector3.forward, impulse: 8f, maxSpeed: 100f).magnitude;
            float sprintRoll = LocomotionMotor.RollLaunchVelocity(
                new Vector3(0f, 0f, 10.5f), Vector3.forward, impulse: 8f, maxSpeed: 100f).magnitude;

            Assert.Less(walkRoll, runRoll, "A running roll must be faster than a walking one.");
            Assert.Less(runRoll, sprintRoll, "A sprinting roll must be faster than a running one.");
        }

        [Test]
        public void RollLaunch_RedirectsMomentumAlongRollDirection()
        {
            // Momentum is inherited as speed but committed to the roll's chosen heading, not the old one.
            Vector3 launch = LocomotionMotor.RollLaunchVelocity(
                new Vector3(0f, 0f, 7f), Vector3.right, impulse: 8f, maxSpeed: 100f);

            Assert.AreEqual(15f, launch.x, Tolerance, "Inherited speed is redirected sideways.");
            Assert.AreEqual(0f, launch.z, Tolerance, "None of the old heading remains.");
        }

        [Test]
        public void RollLaunch_IsClampedToMaxSpeed()
        {
            // The fastest entries stay bounded so a roll never becomes uncontrollable.
            Vector3 launch = LocomotionMotor.RollLaunchVelocity(
                new Vector3(0f, 0f, 10.5f), Vector3.forward, impulse: 8f, maxSpeed: 15f);

            Assert.AreEqual(15f, launch.magnitude, Tolerance);
        }
    }
}
