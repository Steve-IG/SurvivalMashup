using NUnit.Framework;
using ToyChest.Gameplay.Enemy;
using UnityEngine;

namespace ToyChest.Tests.Enemy
{
    /// <summary>
    /// Verifies the pure pursuit planner: the readable Idle / Pursue / Attack phases and the planar
    /// velocity, decided deterministically from positions and ranges without a scene.
    /// </summary>
    public sealed class PursuitMotorTests
    {
        private const float Tolerance = 1e-4f;

        private const float Aggro = 12f;
        private const float AttackRange = 2f;
        private const float Speed = 3.5f;

        private static PursuitStep Plan(Vector2 self, Vector2 target, bool hasTarget = true)
        {
            return new PursuitMotor().Tick(self, target, hasTarget, Speed, Aggro, AttackRange);
        }

        [Test]
        public void NoTarget_IsIdle()
        {
            PursuitStep step = Plan(Vector2.zero, Vector2.zero, hasTarget: false);
            Assert.AreEqual(PursuitPhase.Idle, step.Phase);
            Assert.IsFalse(step.ShouldAttack);
            Assert.AreEqual(Vector2.zero, step.PlanarVelocity);
        }

        [Test]
        public void TargetBeyondAggro_IsIdle()
        {
            PursuitStep step = Plan(Vector2.zero, new Vector2(20f, 0f));
            Assert.AreEqual(PursuitPhase.Idle, step.Phase);
            Assert.AreEqual(Vector2.zero, step.PlanarVelocity);
        }

        [Test]
        public void TargetWithinAggro_Pursues_AtSpeed_TowardTarget()
        {
            PursuitStep step = Plan(Vector2.zero, new Vector2(6f, 0f));
            Assert.AreEqual(PursuitPhase.Pursue, step.Phase);
            Assert.AreEqual(Speed, step.PlanarVelocity.magnitude, Tolerance, "Pursuit moves at the enemy's speed.");
            Assert.AreEqual(new Vector2(Speed, 0f), step.PlanarVelocity, "Pursuit heads straight at the target.");
        }

        [Test]
        public void TargetWithinAttackRange_Attacks_AndStops()
        {
            PursuitStep step = Plan(Vector2.zero, new Vector2(1.5f, 0f));
            Assert.AreEqual(PursuitPhase.Attack, step.Phase);
            Assert.IsTrue(step.ShouldAttack, "In striking range the enemy signals an attack.");
            Assert.AreEqual(Vector2.zero, step.PlanarVelocity, "The enemy stops to attack.");
        }

        [Test]
        public void Pursuit_IsDeterministic()
        {
            var self = new Vector2(1f, 2f);
            var target = new Vector2(7f, 9f);
            PursuitStep a = Plan(self, target);
            PursuitStep b = Plan(self, target);
            Assert.AreEqual(a.Phase, b.Phase);
            Assert.AreEqual(a.PlanarVelocity, b.PlanarVelocity, "The same inputs always plan the same step.");
        }

        [Test]
        public void ZeroSpeed_StillAttacksInRange_ButDoesNotDriftWhilePursuing()
        {
            var motor = new PursuitMotor();
            PursuitStep pursuing = motor.Tick(Vector2.zero, new Vector2(6f, 0f), true, 0f, Aggro, AttackRange);
            Assert.AreEqual(PursuitPhase.Pursue, pursuing.Phase);
            Assert.AreEqual(Vector2.zero, pursuing.PlanarVelocity, "A stationary enemy plans no motion.");

            PursuitStep attacking = motor.Tick(Vector2.zero, new Vector2(1f, 0f), true, 0f, Aggro, AttackRange);
            Assert.AreEqual(PursuitPhase.Attack, attacking.Phase, "Speed does not gate the ability to attack in range.");
        }
    }
}
