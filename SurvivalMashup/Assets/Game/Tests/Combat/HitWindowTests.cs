using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ToyChest.Gameplay.Combat;
using ToyChest.Gameplay.HitDetection;
using UnityEngine;

namespace ToyChest.Tests.Combat
{
    /// <summary>
    /// Verifies the limb-window attack model with no scene: swept limb capsules cannot tunnel past a target,
    /// hit windows are separate hit instances (so one attack can hit a target once per kick and once for the
    /// slam), the optional re-hit interval governs continuous attacks, and the timeline reads contact and
    /// commitment end from normalized clip time without ever skipping a short window.
    /// </summary>
    public sealed class HitWindowTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _created) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private AttackDefinition MakeAttack(float commitEnd, params HitWindow[] windows)
        {
            var attack = ScriptableObject.CreateInstance<AttackDefinition>();
            _created.Add(attack);
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(AttackDefinition).GetField("_windows", flags).SetValue(attack, new List<HitWindow>(windows));
            typeof(AttackDefinition).GetField("_commitEnd", flags).SetValue(attack, commitEnd);
            return attack;
        }

        // --- Cancel point -----------------------------------------------------------------------------

        [Test]
        public void CancelPoint_RecoveryBecomesCancellableOnlyFromCancelFrom()
        {
            AttackDefinition attack = MakeAttack(0.9f, new HitWindow("Slam", 0.4f, 0.5f, StrikeLimbMask.BothHands));
            typeof(AttackDefinition).GetField("_cancelFrom", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(attack, 0.55f);
            var timeline = new AttackTimeline(attack);

            timeline.Advance(0.52f);
            Assert.IsFalse(timeline.IsCancelable, "strikes done but before the cancel point");
            timeline.Advance(0.56f);
            Assert.IsTrue(timeline.IsCancelable);
            timeline.Restart();
            Assert.IsFalse(timeline.IsCancelable, "a restarted step is committed again");
        }

        [Test]
        public void CancelPoint_NeverCutsAStrike_AndZeroMeansNeverCancellable()
        {
            AttackDefinition early = MakeAttack(0.9f, new HitWindow("Slam", 0.4f, 0.6f, StrikeLimbMask.BothHands));
            typeof(AttackDefinition).GetField("_cancelFrom", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(early, 0.3f);
            var timeline = new AttackTimeline(early);
            timeline.Advance(0.5f);
            Assert.IsFalse(timeline.IsCancelable, "a cancel point inside a window waits for the window to close");
            timeline.Advance(0.6f);
            Assert.IsTrue(timeline.IsCancelable);

            var never = new AttackTimeline(MakeAttack(0.9f, new HitWindow("Jab", 0.1f, 0.2f, StrikeLimbMask.RightHand)));
            never.Advance(0.95f);
            Assert.IsFalse(never.IsCancelable);
        }

        // --- Swept limbs ------------------------------------------------------------------------------

        [Test]
        public void FastLimb_SweepCatchesATargetBetweenFrames_ThatThePoseAloneMisses()
        {
            // A foot swings 1.2m in one frame, passing straight through a target it is nowhere near at
            // either end of the frame.
            Vector3 prevA = new Vector3(-0.6f, 1f, 0f), prevB = new Vector3(-0.6f, 1f, 0.5f);
            Vector3 curA = new Vector3(0.6f, 1f, 0f), curB = new Vector3(0.6f, 1f, 0.5f);
            Vector3 target = new Vector3(0f, 1f, 0.25f);

            bool poseOnly = LimbSweep.DistanceToSegment(target, curA, curB) <= 0.1f + 0.05f;
            bool swept = LimbSweep.SweepTouches(prevA, prevB, curA, curB, 0.1f, target, 0.05f);

            Assert.IsFalse(poseOnly, "Testing only the current pose misses the target the kick passed through.");
            Assert.IsTrue(swept, "The swept capsule catches it.");
        }

        [Test]
        public void SweepSampleCount_ScalesWithTravel_AndIsBounded()
        {
            Vector3 a = Vector3.zero, b = Vector3.forward;
            Assert.AreEqual(1, LimbSweep.SampleCount(a, b, a, b, 0.1f), "A still limb is just its current pose.");
            int fast = LimbSweep.SampleCount(a, b, a + Vector3.right, b + Vector3.right, 0.1f);
            Assert.Greater(fast, 1, "A moving limb is sub-sampled.");
            Assert.LessOrEqual(LimbSweep.SampleCount(a, b, a + Vector3.right * 100f, b, 0.1f), LimbSweep.MaxSamples);
        }

        [Test]
        public void SegmentDistance_ClampsToEndpoints()
        {
            Assert.AreEqual(1f, LimbSweep.DistanceToSegment(new Vector3(0f, 1f, 0.5f), Vector3.zero, Vector3.forward), 1e-4f);
            Assert.AreEqual(1f, LimbSweep.DistanceToSegment(new Vector3(0f, 0f, 2f), Vector3.zero, Vector3.forward), 1e-4f);
        }

        // --- Hit instances ----------------------------------------------------------------------------

        [Test]
        public void SeparateWindows_EachStrikeTheSameTargetOnce()
        {
            // "Left kick", "Right kick", "Slam" — one enemy may be hit by each, but each only once.
            var ledger = new HitWindowLedger();
            var enemy = new object();

            for (int window = 0; window < 3; window++)
            {
                Assert.IsTrue(ledger.CanStrike(window, enemy, 1f, 0f), $"Window {window} may strike the enemy.");
                ledger.Record(window, enemy, 1f);
                Assert.IsFalse(ledger.CanStrike(window, enemy, 1.5f, 0f), $"…but window {window} only once.");
            }
        }

        [Test]
        public void RehitInterval_AllowsRepeatHits_OnlyAfterTheInterval()
        {
            var ledger = new HitWindowLedger();
            var enemy = new object();
            ledger.Record(0, enemy, 1.0f);

            Assert.IsFalse(ledger.CanStrike(0, enemy, 1.1f, 0.25f), "Too soon for a continuous attack to re-hit.");
            Assert.IsTrue(ledger.CanStrike(0, enemy, 1.25f, 0.25f), "After the interval it may hit again.");
        }

        [Test]
        public void Ledger_IsPerTarget_AndClearsBetweenSteps()
        {
            var ledger = new HitWindowLedger();
            object a = new object(), b = new object();
            ledger.Record(0, a, 0f);

            Assert.IsTrue(ledger.CanStrike(0, b, 0f, 0f), "Another target is unaffected.");
            ledger.Clear();
            Assert.IsTrue(ledger.CanStrike(0, a, 0f, 0f), "A new step starts with a clean record.");
        }

        // --- Timeline ---------------------------------------------------------------------------------

        [Test]
        public void Timeline_ReportsContactWhenTheStrikesComplete_AndCommitEndOnce()
        {
            AttackDefinition attack = MakeAttack(0.9f,
                new HitWindow("Slam", 0.45f, 0.5f, StrikeLimbMask.BothHands),
                new HitWindow("Kick", 0.15f, 0.3f, StrikeLimbMask.RightFoot));
            var timeline = new AttackTimeline(attack);

            Assert.IsFalse(timeline.Advance(0.16f).ContactReached, "Mid-strike is not yet contact — a chain here would cut the blow.");
            Assert.IsFalse(timeline.Advance(0.4f).ContactReached);
            Assert.IsTrue(timeline.Advance(0.51f).ContactReached, "Contact is when the last window closes (the slam).");
            Assert.IsFalse(timeline.Advance(0.6f).ContactReached, "Contact fires once.");
            Assert.IsTrue(timeline.Advance(0.95f).CommitEnded);
            Assert.IsFalse(timeline.Advance(0.99f).CommitEnded, "Commitment end fires once.");
        }

        [Test]
        public void Timeline_NeverSkipsAShortWindow_WhenAFrameJumpsAcrossIt()
        {
            AttackDefinition attack = MakeAttack(0f, new HitWindow("Jab", 0.40f, 0.42f, StrikeLimbMask.RightHand));
            var timeline = new AttackTimeline(attack);
            var open = new List<int>();

            timeline.Advance(0.30f);
            timeline.OpenWindows(open);
            Assert.AreEqual(0, open.Count);

            timeline.Advance(0.60f); // one long frame leaps clean over the 0.40–0.42 window
            timeline.OpenWindows(open);
            CollectionAssert.AreEqual(new[] { 0 }, open, "A window crossed within one frame still opens for that frame.");

            timeline.Advance(0.70f);
            timeline.OpenWindows(open);
            Assert.AreEqual(0, open.Count, "And it does not reopen once passed.");
        }

        [Test]
        public void Timeline_OpensOverlappingWindowsTogether_AndRestartsPerStep()
        {
            AttackDefinition attack = MakeAttack(0f,
                new HitWindow("Left kick", 0.2f, 0.4f, StrikeLimbMask.LeftFoot),
                new HitWindow("Right kick", 0.3f, 0.5f, StrikeLimbMask.RightFoot));
            var timeline = new AttackTimeline(attack);
            var open = new List<int>();

            timeline.Advance(0.35f);
            timeline.OpenWindows(open);
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, open);

            timeline.Restart();
            Assert.IsTrue(timeline.Advance(0.55f).ContactReached, "A restarted timeline reports contact again.");
        }

        [Test]
        public void TimeNeverRunsBackwards_WithinAStep()
        {
            AttackDefinition attack = MakeAttack(0f, new HitWindow("Strike", 0.5f, 0.6f, StrikeLimbMask.RightHand));
            var timeline = new AttackTimeline(attack);
            timeline.Advance(0.55f);
            timeline.Advance(0.10f); // a stray earlier sample (e.g. a transition read) must not rewind
            Assert.AreEqual(0.55f, timeline.Current, 1e-5f);
        }

        // --- Data -------------------------------------------------------------------------------------

        [Test]
        public void LimbMask_RoundTrips()
        {
            Assert.IsTrue(StrikeLimbs.Contains(StrikeLimbMask.BothFeet, StrikeLimb.LeftFoot));
            Assert.IsFalse(StrikeLimbs.Contains(StrikeLimbMask.BothFeet, StrikeLimb.RightHand));
            Assert.AreEqual(StrikeLimbMask.Head, StrikeLimbs.ToMask(StrikeLimb.Head));
        }

        [Test]
        public void HumanoidDefaults_CoverHandsFeetAndHead()
        {
            var limbs = new HashSet<StrikeLimb>();
            foreach (StrikeShape s in CharacterStrikeProfile.DefaultHumanoidShapes()) limbs.Add(s.Limb);
            CollectionAssert.IsSupersetOf(limbs,
                new[] { StrikeLimb.RightHand, StrikeLimb.LeftHand, StrikeLimb.RightFoot, StrikeLimb.LeftFoot, StrikeLimb.Head });
        }
    }
}
