using NUnit.Framework;
using ToyChest.Gameplay.HitDetection;
using UnityEngine;

namespace ToyChest.Tests.HitDetection
{
    /// <summary>
    /// Verifies the composition the authoring workflow relies on, with no scene: multiple hit volumes on
    /// one attack cover the union of their regions (multi-volume), re-resolving the same volume many times
    /// is stateless and repeatable (the basis of multi-hit contact windows firing off one animation), and
    /// the same shared shape reused by two characters keeps identical geometry while its anchor differs.
    /// </summary>
    public sealed class HitCompositionTests
    {
        // Whether any of a set of authored (volume, origin, forward) regions contains a point — the pure
        // core of an attack that authors several HitVolumeEmitters resolved together.
        private static bool AnyContains(Vector3 point, params (HitVolume volume, Vector3 origin, Vector3 forward)[] regions)
        {
            for (int i = 0; i < regions.Length; i++)
            {
                if (HitQuery.Contains(regions[i].volume, regions[i].origin, regions[i].forward, point))
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void MultipleVolumes_CoverTheUnionOfTheirRegions()
        {
            // A two-fist smash: one cone from the left hand, one from the right. A target in front of the
            // right fist (but not the left) must still register — the attack is the union of its regions.
            var cone = new HitVolume(HitShape.Cone, 2f, 45f, multiTarget: false, maxTargets: 1);
            var leftFist = (cone, new Vector3(-0.5f, 0f, 0f), Vector3.forward);
            var rightFist = (cone, new Vector3(0.5f, 0f, 0f), Vector3.forward);

            Vector3 aheadOfRightFist = new Vector3(0.5f, 0f, 1.5f);
            Vector3 farLeft = new Vector3(-5f, 0f, 0f);

            Assert.IsTrue(AnyContains(aheadOfRightFist, leftFist, rightFist),
                "A multi-volume attack covers the union of its authored regions.");
            Assert.IsFalse(AnyContains(farLeft, leftFist, rightFist),
                "A point outside every region is not hit.");
        }

        [Test]
        public void RepeatedResolution_IsStatelessAndIdentical()
        {
            // Multi-hit: an animation fires several OnAttackContact events, each re-resolving the same
            // volume. The query holds no per-swing state, so N resolutions of identical inputs match.
            var volume = new HitVolume(HitShape.Cone, 2.5f, 60f, multiTarget: false, maxTargets: 1);
            Vector3 point = new Vector3(0f, 0f, 1.5f);

            bool first = HitQuery.Contains(volume, Vector3.zero, Vector3.forward, point);
            for (int contact = 0; contact < 5; contact++)
            {
                Assert.AreEqual(first, HitQuery.Contains(volume, Vector3.zero, Vector3.forward, point),
                    "Every contact window resolves the same volume identically.");
            }
        }

        [Test]
        public void SharedPresetReusedAcrossCharacters_KeepsGeometryVariesAnchor()
        {
            // One "Sword Slash Wide" preset used by two characters: identical shape, different origins.
            var preset = ScriptableObject.CreateInstance<HitVolumeAsset>();
            try
            {
                HitVolume shared = preset.Volume;

                Vector3 playerHand = HitAnchor.ResolveOrigin(new Vector3(0f, 1.2f, 0f), Quaternion.identity, Vector3.zero);
                Vector3 enemyHand = HitAnchor.ResolveOrigin(new Vector3(8f, 1.5f, 2f), Quaternion.identity, Vector3.zero);

                // Same geometry drives both queries; only where it is anchored differs.
                Assert.IsTrue(HitQuery.Contains(shared, playerHand, Vector3.forward, playerHand + Vector3.forward));
                Assert.IsTrue(HitQuery.Contains(shared, enemyHand, Vector3.forward, enemyHand + Vector3.forward));
                Assert.AreNotEqual(playerHand, enemyHand);
            }
            finally
            {
                Object.DestroyImmediate(preset);
            }
        }
    }
}
