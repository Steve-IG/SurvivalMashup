using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ToyChest.Gameplay.Combat;
using ToyChest.Gameplay.Player;
using UnityEngine;

namespace ToyChest.Tests.Combat
{
    /// <summary>
    /// Aerial attack steps (aerial combos, stage 1) with no scene: an attack is aerial exactly when it carries a height
    /// profile; the controller receives that profile as per-frame deltas that sum to the profile's change however the
    /// frames fall, never move backwards in time, and are zero for ground attacks; and the vertical velocity is held
    /// (no gravity) while an aerial step owns the character, resuming from rest when the hold ends.
    /// </summary>
    public sealed class AerialAttackTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _created) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private AttackDefinition MakeAttack(AnimationCurve height)
        {
            var attack = ScriptableObject.CreateInstance<AttackDefinition>();
            _created.Add(attack);
            if (height != null)
            {
                typeof(AttackDefinition).GetField("_airHeight", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(attack, height);
            }

            return attack;
        }

        // A launcher-like profile: rise 1.7m to an apex, then fall back to the start height.
        private static AnimationCurve Launch() => new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.3f, 0.3f), new Keyframe(0.5f, 1.7f), new Keyframe(0.8f, 0f), new Keyframe(1f, 0f));

        [Test]
        public void AnAttackIsAerialOnlyWithAHeightProfile()
        {
            Assert.IsFalse(MakeAttack(null).IsAerial, "a new attack is a ground attack");
            Assert.IsFalse(MakeAttack(new AnimationCurve()).IsAerial, "an empty profile is a ground attack");
            Assert.IsFalse(MakeAttack(new AnimationCurve(new Keyframe(0f, 1f))).IsAerial, "a single key is not a profile");
            Assert.IsTrue(MakeAttack(Launch()).IsAerial);
        }

        [Test]
        public void GroundAttacksFeedNoHeight()
        {
            Assert.AreEqual(0f, MakeAttack(null).AirHeightDelta(0f, 0.5f));
        }

        [Test]
        public void PerFrameDeltasSumToTheProfilesChangeHoweverTheFramesFall()
        {
            AttackDefinition attack = MakeAttack(Launch());
            foreach (float[] frames in new[]
                     {
                         new[] { 0f, 0.5f },
                         new[] { 0f, 0.1f, 0.2f, 0.35f, 0.5f },
                         new[] { 0f, 0.013f, 0.29f, 0.31f, 0.44f, 0.5f },
                     })
            {
                float sum = 0f;
                for (int i = 1; i < frames.Length; i++) sum += attack.AirHeightDelta(frames[i - 1], frames[i]);
                Assert.AreEqual(1.7f, sum, 1e-4f, string.Join(",", frames));
            }

            // Through the descent: the whole launch nets out back at the start height.
            Assert.AreEqual(0f, attack.AirHeightDelta(0f, 0.5f) + attack.AirHeightDelta(0.5f, 1f), 1e-4f);
        }

        [Test]
        public void TimeNeverRunsBackwardsIntoHeight()
        {
            AttackDefinition attack = MakeAttack(Launch());
            Assert.AreEqual(0f, attack.AirHeightDelta(0.5f, 0.3f), "a backwards reading adds nothing");
            Assert.AreEqual(0f, attack.AirHeightDelta(0.4f, 0.4f), "a stalled reading adds nothing");
        }

        [Test]
        public void TheTimelineSpanDrivesTheDelta()
        {
            // PlayerCombat applies AirHeightDelta(Timeline.Previous, Timeline.Current) each frame; the timeline clamps
            // time to never run backwards within a step, so the controller can only follow the profile forwards.
            AttackDefinition attack = MakeAttack(Launch());
            var timeline = new AttackTimeline(attack);
            float height = 0f;
            foreach (float t in new[] { 0.05f, 0.2f, 0.18f, 0.4f, 0.5f })
            {
                timeline.Advance(t);
                height += attack.AirHeightDelta(timeline.Previous, timeline.Current);
            }

            Assert.AreEqual(1.7f, height, 1e-4f);
        }

        [Test]
        public void AHeldStepAccumulatesNoGravity()
        {
            Assert.AreEqual(0f, LocomotionMotor.VerticalVelocity(-6f, 25f, 0.02f, grounded: false, held: true));
            Assert.AreEqual(0f, LocomotionMotor.VerticalVelocity(5f, 25f, 0.02f, grounded: false, held: true), "a jump's momentum is caught");
            Assert.AreEqual(0f, LocomotionMotor.VerticalVelocity(-2f, 25f, 0.02f, grounded: true, held: true));
        }

        [Test]
        public void ReleasedTheCharacterFallsFromRestUnderNormalGravity()
        {
            float v = LocomotionMotor.VerticalVelocity(0f, 25f, 0.02f, grounded: false, held: false);
            Assert.AreEqual(-0.5f, v, 1e-5f);
            Assert.AreEqual(-2f, LocomotionMotor.VerticalVelocity(-9f, 25f, 0.02f, grounded: true, held: false), "landing settles");
            Assert.AreEqual(8.5f - 0.5f, LocomotionMotor.VerticalVelocity(8.5f, 25f, 0.02f, grounded: true, held: false), 1e-5f, "a jump still rises");
        }
    }
}
