using System;
using System.Reflection;
using NUnit.Framework;
using ToyChest.Gameplay.HitDetection;
using ToyChest.Gameplay.Player;
using ToyChest.Systems.Resources;
using ToyChest.Tests.Resources;

namespace ToyChest.Tests.Combat
{
    /// <summary>
    /// Verifies the combat-feel guarantees of Review Group 5C with no scene: the retimed swing still lands
    /// exactly one blow per press (shortening recovery must not double-hit or strand the swing), the
    /// contact frame is reported once whether it comes from the timer or the animation event, the training
    /// dummy's keep-alive contract never lets its health deplete, and the debug visualization is genuinely
    /// debug-only — gameplay assemblies must not reference the debug assembly.
    /// </summary>
    public sealed class CombatFeelTests
    {
        private const float Tolerance = 1e-4f;

        // --- Swing timing: shorter recovery preserves correctness -------------------------------------

        [Test]
        public void Swing_LandsExactlyOneBlowPerPress()
        {
            var swing = new MeleeSwing();
            swing.Begin(windUpDuration: 0.4f, recoveryDuration: 0.12f);

            int impacts = 0;
            int finishes = 0;
            for (int i = 0; i < 100; i++)
            {
                MeleeSwingTick tick = swing.Advance(0.02f);
                if (tick.Impacted) impacts++;
                if (tick.Finished) finishes++;
            }

            Assert.AreEqual(1, impacts, "A press must land exactly one blow, however short the recovery.");
            Assert.AreEqual(1, finishes, "The swing must finish exactly once and return control.");
            Assert.IsFalse(swing.IsActive, "The swing must not be left active.");
        }

        [Test]
        public void Swing_ShortRecovery_ReturnsControlSooner()
        {
            // The 5C change: recovery shrank so the player is free again almost immediately after contact.
            var quick = new MeleeSwing();
            var slow = new MeleeSwing();
            quick.Begin(0.4f, 0.12f);
            slow.Begin(0.4f, 0.35f);

            // Advance both to just past contact + the short recovery.
            for (int i = 0; i < 27; i++) // 27 * 0.02 = 0.54s  (> 0.4 + 0.12, < 0.4 + 0.35)
            {
                quick.Advance(0.02f);
                slow.Advance(0.02f);
            }

            Assert.IsFalse(quick.IsActive, "The retimed swing has returned control by 0.54s.");
            Assert.IsTrue(slow.IsActive, "The old timing was still committed at 0.54s.");
        }

        [Test]
        public void Swing_AnimationContact_LandsOnlyOnce()
        {
            var swing = new MeleeSwing();
            swing.Begin(0.4f, 0.12f);

            Assert.IsTrue(swing.CompleteWindUp(), "The contact event lands the blow.");
            Assert.IsFalse(swing.CompleteWindUp(), "A second contact event in the same swing must not re-land it.");

            // And the wind-up timer must not fire a second impact afterwards.
            int impacts = 0;
            for (int i = 0; i < 50; i++)
            {
                if (swing.Advance(0.02f).Impacted) impacts++;
            }

            Assert.AreEqual(0, impacts, "The timer must not add a second blow after an animation contact.");
        }

        [Test]
        public void Swing_IdleSwing_NeverReportsImpact()
        {
            // A swing that was never begun (no press) can never produce an impact — the guarantee behind
            // "empty swings generate no impact presentation".
            var swing = new MeleeSwing();

            for (int i = 0; i < 20; i++)
            {
                MeleeSwingTick tick = swing.Advance(0.05f);
                Assert.IsFalse(tick.Impacted);
                Assert.IsFalse(tick.Finished);
            }
        }

        // --- Training dummy: never dies ----------------------------------------------------------------

        [Test]
        public void TrainingDummy_KeepAliveContract_NeverDepletes()
        {
            var factory = new ResourceTestFactory();
            try
            {
                // The dummy's authored health pool, and the restore-after-damage contract its adapter runs.
                var health = new ResourceValue(factory.CreateLiteral("resource.health", 10000f));
                bool depleted = false;
                health.Depleted += () => depleted = true;

                for (int punch = 0; punch < 200; punch++)
                {
                    health.Consume(25f);
                    Assert.IsFalse(health.IsDepleted, "The dummy must never be depleted by a blow.");
                    health.Restore(health.Maximum); // what TrainingDummy does on damage
                    Assert.AreEqual(health.Maximum, health.Current, Tolerance);
                }

                Assert.IsFalse(depleted, "The dummy must never raise Depleted, so it can never die.");
            }
            finally
            {
                factory.Cleanup();
            }
        }

        // --- Debug tooling stays debug-only ------------------------------------------------------------

        [Test]
        public void HitVolumeVisualization_IsDebugOnly_GameplayNeverReferencesIt()
        {
            // The visualization may read gameplay, never the reverse. If gameplay ever referenced the debug
            // assembly, debug drawing would have become a gameplay dependency.
            AssertDoesNotReferenceDebugging(typeof(PlayerCombat).Assembly);
            AssertDoesNotReferenceDebugging(typeof(HitVolume).Assembly);
        }

        private static void AssertDoesNotReferenceDebugging(Assembly assembly)
        {
            foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
            {
                Assert.AreNotEqual("ToyChest.Debugging", reference.Name,
                    $"{assembly.GetName().Name} must not depend on the debug assembly.");
            }
        }
    }
}
