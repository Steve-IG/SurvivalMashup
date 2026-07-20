using NUnit.Framework;
using ToyChest.Gameplay.HitDetection;
using UnityEngine;

namespace ToyChest.Tests.HitDetection
{
    /// <summary>
    /// Verifies the pure geometry at the heart of the canonical hit-detection vocabulary in isolation
    /// (no scene, no physics, no MonoBehaviour) — the same engine-independent testing the locomotion and
    /// swing math get. The point of these tests is architectural: <em>one</em> vocabulary
    /// (<see cref="HitVolume"/> + <see cref="HitQuery"/>) must correctly describe every kind of attack, so
    /// each region below drives the identical query with a different authored volume and shows it behaves
    /// as that archetype requires — player melee, enemy melee, explosion/hazard, and a future projectile.
    /// </summary>
    public sealed class HitQueryTests
    {
        private static readonly Vector3 Origin = Vector3.zero;
        private static readonly Vector3 Forward = Vector3.forward;

        // A short directional swing: the shape both player and enemy melee use.
        private static HitVolume MeleeArc(float radius = 2.5f, float halfAngle = 70f)
        {
            return new HitVolume(HitShape.Cone, radius, halfAngle, multiTarget: false, maxTargets: 1);
        }

        // An omnidirectional blast: the shape an explosion, shockwave, aura, or persistent hazard uses.
        private static HitVolume Radial(float radius, bool multiTarget = true, int maxTargets = 16)
        {
            return new HitVolume(HitShape.Sphere, radius, 0f, multiTarget, maxTargets);
        }

        // --- Player & enemy melee: a frontal cone that respects facing --------------------------------

        [Test]
        public void MeleeCone_TargetDirectlyAhead_IsHit()
        {
            HitVolume swing = MeleeArc();

            Assert.IsTrue(HitQuery.Contains(swing, Origin, Forward, new Vector3(0f, 0f, 1.5f)),
                "A target in front and within reach is inside a melee swing.");
        }

        [Test]
        public void MeleeCone_TargetBehind_IsNotHit()
        {
            HitVolume swing = MeleeArc();

            // The core defect the architecture fixes: a swing must not connect with something behind the
            // attacker. Facing is part of hit detection, not an afterthought.
            Assert.IsFalse(HitQuery.Contains(swing, Origin, Forward, new Vector3(0f, 0f, -1.5f)),
                "A target behind the attacker must never be hit by a frontal swing.");
        }

        [Test]
        public void MeleeCone_TargetInsideReachButOutsideArc_IsNotHit()
        {
            HitVolume swing = MeleeArc(radius: 2.5f, halfAngle: 45f);

            // 90° to the side: within reach, but well outside a 45° half-angle arc.
            Assert.IsFalse(HitQuery.Contains(swing, Origin, Forward, new Vector3(1.5f, 0f, 0f)),
                "A target beside the attacker is outside a narrow frontal arc.");
        }

        [Test]
        public void MeleeCone_TargetBeyondReach_IsNotHit()
        {
            HitVolume swing = MeleeArc(radius: 2.5f);

            Assert.IsFalse(HitQuery.Contains(swing, Origin, Forward, new Vector3(0f, 0f, 5f)),
                "A target ahead but past the swing's reach is not hit.");
        }

        [Test]
        public void MeleeCone_WideArc_HitsTargetToTheSide()
        {
            HitVolume cleave = MeleeArc(radius: 2.5f, halfAngle: 120f);

            // The same vocabulary scales to a wide cleave purely through authored data (the half-angle).
            Assert.IsTrue(HitQuery.Contains(cleave, Origin, Forward, new Vector3(1.5f, 0f, 0.2f)),
                "A wide cleave arc reaches targets well off the forward axis.");
        }

        // --- Explosion / persistent hazard: an omnidirectional sphere ---------------------------------

        [Test]
        public void RadialSphere_TargetBehind_IsHit()
        {
            HitVolume blast = Radial(4f);

            // An explosion has no facing: a target behind the origin is hit exactly like one in front.
            Assert.IsTrue(HitQuery.Contains(blast, Origin, Forward, new Vector3(0f, 0f, -3f)),
                "A radial blast is omnidirectional — direction to the target is irrelevant.");
        }

        [Test]
        public void RadialSphere_TargetBeyondReach_IsNotHit()
        {
            HitVolume blast = Radial(4f);

            Assert.IsFalse(HitQuery.Contains(blast, Origin, Forward, new Vector3(5f, 0f, 0f)),
                "A radial blast still respects its reach.");
        }

        [Test]
        public void RadialSphere_TargetAboveWithinReach_IsHit()
        {
            HitVolume blast = Radial(4f);

            // Reach is true 3D distance, so a tall blast (or a giant's overhead slam) catches a target
            // above the origin, not only ones on the ground plane.
            Assert.IsTrue(HitQuery.Contains(blast, Origin, Forward, new Vector3(1f, 3f, 1f)),
                "Vertical reach counts: a target above the origin within 3D range is hit.");
        }

        // --- Future projectile: the same vocabulary, with a moving origin -----------------------------

        [Test]
        public void Projectile_ModeledAsSweptSphereAtAdvancedOrigin_HitsWhatItReaches()
        {
            // A projectile needs no new concept: model it as a small sphere whose origin is the projectile's
            // position along its path. Before it arrives, the target is outside the volume; once the origin
            // advances to the target, the same query reports the hit. This is how the architecture supports
            // projectiles without a bespoke code path.
            HitVolume tip = Radial(0.4f, multiTarget: false, maxTargets: 1);

            Vector3 targetAhead = new Vector3(0f, 0f, 10f);
            Vector3 earlyOrigin = new Vector3(0f, 0f, 2f);   // projectile still in flight
            Vector3 arrivedOrigin = new Vector3(0f, 0f, 9.8f); // projectile reached the target

            Assert.IsFalse(HitQuery.Contains(tip, earlyOrigin, Forward, targetAhead),
                "A projectile in flight has not yet reached a target ahead of it.");
            Assert.IsTrue(HitQuery.Contains(tip, arrivedOrigin, Forward, targetAhead),
                "When the projectile's swept origin reaches the target, the same query reports the hit.");
        }

        // --- Facing degeneracies ----------------------------------------------------------------------

        [Test]
        public void WithinCone_TargetAtOrigin_IsContained()
        {
            // No meaningful direction to reject when the target coincides with the origin.
            Assert.IsTrue(HitQuery.WithinCone(Forward, Vector3.zero, 30f));
        }

        [Test]
        public void WithinCone_DegenerateForward_IsContained()
        {
            // A caller with no planar facing (straight up) cannot reject on direction.
            Assert.IsTrue(HitQuery.WithinCone(Vector3.up, new Vector3(0f, 0f, 1f), 30f));
        }

        [Test]
        public void WithinCone_IgnoresVerticalOffset()
        {
            // Facing is a horizontal-plane test: a target dead ahead but higher up is still "in front".
            Assert.IsTrue(HitQuery.WithinCone(Forward, new Vector3(0f, 3f, 1f), 20f),
                "Cone facing is measured on the ground plane, so vertical offset does not swing the angle.");
        }
    }
}
