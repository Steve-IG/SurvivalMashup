using NUnit.Framework;
using ToyChest.Gameplay.Combat;
using UnityEngine;

namespace ToyChest.Tests.Combat
{
    /// <summary>
    /// The pure hit-reaction resolution: a blow's Impact Power against a body's Stability, under authored ratio
    /// thresholds, picks None → Flinch → Stagger → Knockback, and knockback distance grows with the ratio up to a cap.
    /// Because only the ratio matters, a stronger attacker or a lighter target climbs the same ladder.
    /// </summary>
    public sealed class HitReactionResolverTests
    {
        private const float Tolerance = 1e-4f;

        // flinch 0.5, stagger 1.5, knockback 2.5, stagger 0.45 s, knockback 1.5 m + 1 m per ratio, cap 5 m, +0.25 s
        private static readonly HitReactionRules Rules = new HitReactionRules(0.5f, 1.5f, 2.5f, 0.45f, 1.5f, 1f, 5f, 0.25f);

        [TestCase(4f, HitReactionLevel.None)]
        [TestCase(5f, HitReactionLevel.Flinch)]
        [TestCase(14.9f, HitReactionLevel.Flinch)]
        [TestCase(15f, HitReactionLevel.Stagger)]
        [TestCase(24.9f, HitReactionLevel.Stagger)]
        [TestCase(25f, HitReactionLevel.Knockback)]
        public void Level_FollowsTheRatioThresholds(float impact, HitReactionLevel expected)
        {
            Assert.AreEqual(expected, HitReactionResolver.Resolve(impact, 10f, Rules).Level);
        }

        [Test]
        public void SameBlow_DropsATier_AgainstAHeavierBody()
        {
            Assert.AreEqual(HitReactionLevel.Flinch, HitReactionResolver.Resolve(10f, 10f, Rules).Level, "A punch flinches a Grunt.");
            Assert.AreEqual(HitReactionLevel.None, HitReactionResolver.Resolve(10f, 40f, Rules).Level, "The same punch is shrugged off by a Brute.");
        }

        [Test]
        public void StrongerAttacker_ClimbsTheLadder_WithTheSameAttack()
        {
            Assert.AreEqual(HitReactionLevel.Flinch, HitReactionResolver.Resolve(10f * 1f, 10f, Rules).Level);
            Assert.AreEqual(HitReactionLevel.Knockback, HitReactionResolver.Resolve(10f * 3f, 10f, Rules).Level,
                "Triple Impact Power turns the same jab into a knockback.");
        }

        [Test]
        public void NoneAndFlinch_DoNotLockTheBody()
        {
            Assert.AreEqual(0f, HitReactionResolver.Resolve(4f, 10f, Rules).StaggerSeconds);
            Assert.AreEqual(0f, HitReactionResolver.Resolve(10f, 10f, Rules).StaggerSeconds);
            Assert.AreEqual(0f, HitReactionResolver.Resolve(10f, 10f, Rules).KnockbackDistance);
        }

        [Test]
        public void Stagger_LocksWithoutKnockback()
        {
            HitReaction stagger = HitReactionResolver.Resolve(16f, 10f, Rules);
            Assert.AreEqual(0.45f, stagger.StaggerSeconds, Tolerance);
            Assert.AreEqual(0f, stagger.KnockbackDistance);
        }

        [Test]
        public void Knockback_GrowsWithRatio_UpToTheCap_AndLocksLonger()
        {
            HitReaction atThreshold = HitReactionResolver.Resolve(25f, 10f, Rules);
            HitReaction harder = HitReactionResolver.Resolve(35f, 10f, Rules);
            HitReaction enormous = HitReactionResolver.Resolve(1000f, 10f, Rules);

            Assert.AreEqual(1.5f, atThreshold.KnockbackDistance, Tolerance);
            Assert.AreEqual(2.5f, harder.KnockbackDistance, Tolerance);
            Assert.AreEqual(5f, enormous.KnockbackDistance, Tolerance, "Capped.");
            Assert.AreEqual(0.7f, atThreshold.StaggerSeconds, Tolerance, "Stagger plus the knockback's extra lock-out.");
        }

        [Test]
        public void ZeroImpact_NeverReacts_AndZeroStability_IsSafe()
        {
            Assert.AreEqual(HitReactionLevel.None, HitReactionResolver.Resolve(0f, 10f, Rules).Level);
            HitReaction weightless = HitReactionResolver.Resolve(1f, 0f, Rules);
            Assert.AreEqual(HitReactionLevel.Knockback, weightless.Level, "A weightless body is bowled over by anything.");
            Assert.IsFalse(float.IsInfinity(weightless.Ratio));
        }

        [Test]
        public void HitImpact_FlattensAndNormalizesItsDirection()
        {
            var impact = new HitImpact(-3f, new Vector3(3f, 5f, 4f), Vector3.zero, null);
            Assert.AreEqual(0f, impact.Power, "Negative power clamps to zero.");
            Assert.AreEqual(0f, impact.Direction.y);
            Assert.AreEqual(1f, impact.Direction.magnitude, Tolerance);
        }
    }
}
