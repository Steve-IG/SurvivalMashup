using System;
using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Gameplay.Npc;
using UnityEngine;

namespace ToyChest.Tests.Npc
{
    /// <summary>
    /// Verifies the deterministic autonomous-movement planner in isolation (no scene, no
    /// MonoBehaviour): it idles before its first move, then wanders to a point within its radius,
    /// steers at the injected speed, returns to idle on arrival, never leaves the radius, and — given
    /// the same seed — always reproduces the same walk. This is the gameplay-facing core of NPC
    /// autonomy; the MonoBehaviour adapter only feeds it position, speed, and time.
    /// </summary>
    public sealed class WanderMotorTests
    {
        private const float Tolerance = 1e-4f;

        // A scripted RNG so behavioural tests place destinations and idle durations exactly.
        // The motor consumes NextDouble() as: idle-duration, then (angle, radial) per destination.
        private sealed class ScriptedRandom : System.Random
        {
            private readonly Queue<double> _values;

            public ScriptedRandom(params double[] values)
            {
                _values = new Queue<double>(values);
            }

            public override double NextDouble()
            {
                return _values.Count > 0 ? _values.Dequeue() : 0.0;
            }
        }

        [Test]
        public void StartsIdle_AndDoesNotMoveBeforeIdleElapses()
        {
            var settings = new WanderSettings(10f, 0.5f, 2f, 2f);
            var motor = new WanderMotor(settings, new ScriptedRandom(0.0));

            WanderStep step = motor.Tick(Vector2.zero, Vector2.zero, 4f, 0.5f);

            Assert.AreEqual(WanderMotor.Phase.Idle, motor.CurrentPhase);
            Assert.IsFalse(step.IsMoving);
            Assert.AreEqual(0f, step.PlanarVelocity.magnitude, Tolerance);
        }

        [Test]
        public void WhenIdleElapses_MovesTowardChosenDestinationAtSpeed()
        {
            var settings = new WanderSettings(10f, 0.5f, 1f, 1f);
            // idle=1.0, angle=0 (cos=1,sin=0), radial=0.25 -> distance = 10*sqrt(0.25) = 5.
            var motor = new WanderMotor(settings, new ScriptedRandom(0.0, 0.0, 0.25));

            WanderStep step = motor.Tick(Vector2.zero, Vector2.zero, 4f, 1.0f);

            Assert.AreEqual(WanderMotor.Phase.Moving, motor.CurrentPhase);
            Assert.AreEqual(new Vector2(5f, 0f), motor.Destination);
            Assert.IsTrue(step.IsMoving);
            Assert.AreEqual(new Vector2(4f, 0f), step.PlanarVelocity);
        }

        [Test]
        public void OnArrival_ReturnsToIdle()
        {
            var settings = new WanderSettings(10f, 0.5f, 1f, 1f);
            var motor = new WanderMotor(settings, new ScriptedRandom(0.0, 0.0, 0.25, 0.0));

            motor.Tick(Vector2.zero, Vector2.zero, 4f, 1.0f);

            // Arrive exactly at the destination: the next tick should stop and idle.
            WanderStep step = motor.Tick(Vector2.zero, new Vector2(5f, 0f), 4f, 0.1f);

            Assert.AreEqual(WanderMotor.Phase.Idle, motor.CurrentPhase);
            Assert.IsFalse(step.IsMoving);
            Assert.AreEqual(0f, step.PlanarVelocity.magnitude, Tolerance);
        }

        [Test]
        public void NonPositiveSpeed_ProducesNoMotion()
        {
            var settings = new WanderSettings(10f, 0.5f, 1f, 1f);
            var motor = new WanderMotor(settings, new ScriptedRandom(0.0, 0.0, 0.25));

            WanderStep step = motor.Tick(Vector2.zero, Vector2.zero, 0f, 1.0f);

            Assert.IsFalse(step.IsMoving);
            Assert.AreEqual(0f, step.PlanarVelocity.magnitude, Tolerance);
        }

        [Test]
        public void MovingVelocity_MagnitudeEqualsSpeed()
        {
            var settings = new WanderSettings(10f, 0.5f, 1f, 1f);
            var motor = new WanderMotor(settings, new ScriptedRandom(0.0, 0.0, 1.0));

            WanderStep step = motor.Tick(Vector2.zero, Vector2.zero, 3.5f, 1.0f);

            Assert.IsTrue(step.IsMoving);
            Assert.AreEqual(3.5f, step.PlanarVelocity.magnitude, Tolerance);
        }

        [Test]
        public void EveryDestination_StaysWithinRadiusOfAnchor()
        {
            const float radius = 7f;
            var anchor = new Vector2(20f, -5f);
            var settings = new WanderSettings(radius, 0.3f, 0.5f, 0.5f);
            var motor = new WanderMotor(settings, 98765);
            Vector2 position = anchor;

            // Drive many idle→move→arrive cycles; a destination is only chosen when moving begins.
            for (int i = 0; i < 500; i++)
            {
                WanderStep step = motor.Tick(anchor, position, 5f, 0.5f);
                if (motor.CurrentPhase == WanderMotor.Phase.Moving)
                {
                    Assert.LessOrEqual(
                        (motor.Destination - anchor).magnitude, radius + Tolerance,
                        "A wander destination must never fall outside the authored radius.");
                    // Teleport to the destination so the next tick registers arrival.
                    position = motor.Destination;
                }
            }
        }

        [Test]
        public void SameSeed_ReproducesTheSameWalk()
        {
            var settings = new WanderSettings(8f, 0.4f, 0.5f, 2f);
            var a = new WanderMotor(settings, 4242);
            var b = new WanderMotor(settings, 4242);
            var anchor = Vector2.zero;
            Vector2 positionA = anchor;
            Vector2 positionB = anchor;

            for (int i = 0; i < 200; i++)
            {
                WanderStep stepA = a.Tick(anchor, positionA, 4f, 0.25f);
                WanderStep stepB = b.Tick(anchor, positionB, 4f, 0.25f);

                Assert.AreEqual(stepA.IsMoving, stepB.IsMoving, $"Movement diverged at step {i}.");
                Assert.AreEqual(stepA.PlanarVelocity, stepB.PlanarVelocity, $"Velocity diverged at step {i}.");
                Assert.AreEqual(a.Destination, b.Destination, $"Destination diverged at step {i}.");

                positionA += stepA.PlanarVelocity * 0.25f;
                positionB = positionA;
            }
        }

        [Test]
        public void Settings_ClampDegenerateAuthoring()
        {
            var settings = new WanderSettings(-3f, -1f, -2f, -5f);

            Assert.AreEqual(0f, settings.Radius, Tolerance);
            Assert.GreaterOrEqual(settings.ArriveThreshold, 0.01f);
            Assert.AreEqual(0f, settings.MinIdleSeconds, Tolerance);
            Assert.GreaterOrEqual(settings.MaxIdleSeconds, settings.MinIdleSeconds);
        }

        [Test]
        public void NullRandom_IsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new WanderMotor(WanderSettings.Default, (System.Random)null));
        }
    }
}
