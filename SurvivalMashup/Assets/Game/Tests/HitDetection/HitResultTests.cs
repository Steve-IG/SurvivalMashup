using NUnit.Framework;
using ToyChest.Gameplay.HitDetection;
using UnityEngine;

namespace ToyChest.Tests.HitDetection
{
    /// <summary>
    /// Verifies the value-type parts of the hit-detection vocabulary that need no scene: the deterministic
    /// nearest-first ordering multi-target resolution depends on, and that an authored <see cref="HitVolume"/>
    /// reports its configuration back faithfully (including the multi-target cap floor).
    /// </summary>
    public sealed class HitResultTests
    {
        [Test]
        public void Compare_OrdersNearestFirst()
        {
            var near = new HitResult(null, Vector3.zero, sqrDistance: 1f);
            var far = new HitResult(null, Vector3.zero, sqrDistance: 9f);

            Assert.Less(HitResult.Compare(near, far), 0, "The nearer hit sorts before the farther one.");
            Assert.Greater(HitResult.Compare(far, near), 0, "The farther hit sorts after the nearer one.");
        }

        [Test]
        public void Compare_EqualDistanceWithoutObjects_IsStable()
        {
            var a = new HitResult(null, Vector3.zero, sqrDistance: 4f);
            var b = new HitResult(null, Vector3.zero, sqrDistance: 4f);

            // No objects to break the tie by id (mirrors PlayerInteractor's ordinal-id tiebreak): equal.
            Assert.AreEqual(0, HitResult.Compare(a, b));
        }

        [Test]
        public void HitVolume_ReportsAuthoredConfiguration()
        {
            var volume = new HitVolume(HitShape.Cone, radius: 3f, coneHalfAngleDegrees: 55f,
                multiTarget: true, maxTargets: 8);

            Assert.AreEqual(HitShape.Cone, volume.Shape);
            Assert.AreEqual(3f, volume.Radius);
            Assert.AreEqual(55f, volume.ConeHalfAngleDegrees);
            Assert.IsTrue(volume.MultiTarget);
            Assert.AreEqual(8, volume.MaxTargets);
        }

        [Test]
        public void HitVolume_MaxTargets_NeverBelowOne()
        {
            var volume = new HitVolume(HitShape.Sphere, radius: 3f, coneHalfAngleDegrees: 0f,
                multiTarget: true, maxTargets: 0);

            Assert.AreEqual(1, volume.MaxTargets, "A multi-target volume always keeps at least one hit.");
        }
    }
}
