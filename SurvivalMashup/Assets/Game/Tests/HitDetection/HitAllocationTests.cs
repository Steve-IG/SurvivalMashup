using NUnit.Framework;
using ToyChest.Gameplay.HitDetection;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace ToyChest.Tests.HitDetection
{
    /// <summary>
    /// Verifies that repeated hit detection allocates no managed memory — the property that lets an
    /// animation fire many contact windows per second, and many attacks resolve every frame, without
    /// generating GC pressure. The detector reuses a pooled collider buffer and result lists and sorts with
    /// a cached comparer (no per-call delegate). Measured with an empty world (no physics scene), which
    /// still exercises the clear/overlap/sort/copy path allocation-wise.
    /// </summary>
    public sealed class HitAllocationTests
    {
        [Test]
        public void RepeatedDetection_DoesNotAllocate()
        {
            var detector = new HitDetector();
            var volume = new HitVolume(HitShape.Cone, 2.5f, 70f, multiTarget: false, maxTargets: 1);
            HitFilter filter = HitFilter.Any;

            // Warm up so first-call JIT / one-time setup is not counted as a per-call allocation.
            for (int i = 0; i < 4; i++)
            {
                detector.Detect(volume, filter, Vector3.zero, Vector3.forward, null);
            }

            Assert.That(() =>
            {
                for (int i = 0; i < 64; i++)
                {
                    detector.Detect(volume, filter, Vector3.zero, Vector3.forward, null);
                }
            }, Is.Not.AllocatingGCMemory());
        }
    }
}
