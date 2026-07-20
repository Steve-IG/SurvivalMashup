using NUnit.Framework;
using ToyChest.Gameplay.Player;

namespace ToyChest.Tests.Player
{
    /// <summary>
    /// Verifies the pure melee-swing timing in isolation (no scene, no MonoBehaviour): a swing runs
    /// wind-up → impact → recovery → idle, the impact fires exactly once at the contact frame, the swing
    /// reports when it is fully recovered, and a single large delta can resolve the whole swing. This is
    /// the deterministic core the combat adapter uses to land damage on the swing rather than the input.
    /// </summary>
    public sealed class MeleeSwingTests
    {
        [Test]
        public void NewSwing_IsIdle()
        {
            var swing = new MeleeSwing();

            Assert.IsFalse(swing.IsActive);
            Assert.AreEqual(MeleePhase.Idle, swing.Phase);
        }

        [Test]
        public void Begin_EntersWindUp()
        {
            var swing = new MeleeSwing();
            swing.Begin(windUpDuration: 0.1f, recoveryDuration: 0.2f);

            Assert.IsTrue(swing.IsActive);
            Assert.IsTrue(swing.IsWindingUp);
        }

        [Test]
        public void Advance_DuringWindUp_DoesNotImpact()
        {
            var swing = new MeleeSwing();
            swing.Begin(0.1f, 0.2f);

            MeleeSwingTick tick = swing.Advance(0.05f);

            Assert.IsFalse(tick.Impacted, "The blow has not landed halfway through the wind-up.");
            Assert.IsTrue(swing.IsWindingUp);
        }

        [Test]
        public void Advance_WhenWindUpCompletes_ImpactsOnce()
        {
            var swing = new MeleeSwing();
            swing.Begin(0.1f, 0.2f);

            Assert.IsFalse(swing.Advance(0.05f).Impacted);
            MeleeSwingTick atContact = swing.Advance(0.05f);

            Assert.IsTrue(atContact.Impacted, "Impact fires when the wind-up completes.");
            Assert.IsTrue(swing.IsRecovering);
            Assert.IsFalse(swing.Advance(0.05f).Impacted, "Impact never fires twice for one swing.");
        }

        [Test]
        public void Advance_AfterRecovery_Finishes_AndReturnsToIdle()
        {
            var swing = new MeleeSwing();
            swing.Begin(0.1f, 0.2f);

            swing.Advance(0.1f);                       // impact
            Assert.IsFalse(swing.Advance(0.1f).Finished, "Still recovering.");
            MeleeSwingTick end = swing.Advance(0.1f);  // recovery done

            Assert.IsTrue(end.Finished);
            Assert.IsFalse(swing.IsActive);
            Assert.AreEqual(MeleePhase.Idle, swing.Phase);
        }

        [Test]
        public void Advance_LargeDelta_ImpactsAndFinishesInOneTick()
        {
            var swing = new MeleeSwing();
            swing.Begin(0.1f, 0.2f);

            MeleeSwingTick tick = swing.Advance(5f);

            Assert.IsTrue(tick.Impacted, "A delta past the whole swing still lands the blow.");
            Assert.IsTrue(tick.Finished, "...and finishes the recovery in the same tick.");
            Assert.IsFalse(swing.IsActive);
        }

        [Test]
        public void ZeroWindUp_ImpactsOnFirstAdvance()
        {
            var swing = new MeleeSwing();
            swing.Begin(0f, 0.2f);

            Assert.IsTrue(swing.Advance(0.001f).Impacted, "A zero wind-up connects immediately.");
        }

        [Test]
        public void Cancel_ReturnsToIdleWithoutImpact()
        {
            var swing = new MeleeSwing();
            swing.Begin(0.1f, 0.2f);
            swing.Cancel();

            Assert.IsFalse(swing.IsActive);
            Assert.IsFalse(swing.Advance(1f).Impacted, "A cancelled swing never lands.");
        }

        [Test]
        public void CompleteWindUp_WhileWindingUp_LandsAndEntersRecovery()
        {
            var swing = new MeleeSwing();
            swing.Begin(0.5f, 0.3f);

            // An animation contact event fires early, before the wind-up timer would have elapsed.
            Assert.IsTrue(swing.CompleteWindUp(), "The contact event lands the blow while winding up.");
            Assert.IsTrue(swing.IsRecovering);
            Assert.IsFalse(swing.Advance(0.1f).Impacted, "The timer must not land a second blow.");
        }

        [Test]
        public void CompleteWindUp_WhenNotWindingUp_DoesNothing()
        {
            var swing = new MeleeSwing();

            Assert.IsFalse(swing.CompleteWindUp(), "An idle swing has no blow to land.");

            swing.Begin(0.1f, 0.3f);
            swing.Advance(0.1f); // now recovering
            Assert.IsFalse(swing.CompleteWindUp(), "A recovering swing has already landed.");
        }
    }
}
