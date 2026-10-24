using NUnit.Framework;
using ToyChest.Gameplay.Enemy;

namespace ToyChest.Tests.Enemy
{
    /// <summary>
    /// Verifies the enemy attack's timing and — the RG5D addition — its authored interrupt windows, with no
    /// scene: a hit during an interruptible phase cancels the attack so its pending strike never lands, a hit
    /// in a non-interruptible phase leaves the attack running, and interruption never permanently disables
    /// future attacks. The interrupt window is data (per-phase flags), never a hardcoded time threshold.
    /// </summary>
    public sealed class EnemyAttackCycleTests
    {
        private const float WindUp = 0.5f;
        private const float Recover = 0.6f;

        private static EnemyAttackCycle Make(bool windUpInterruptible = true, bool recoverInterruptible = false)
        {
            return new EnemyAttackCycle(WindUp, Recover, windUpInterruptible, recoverInterruptible);
        }

        // --- Timing -----------------------------------------------------------------------------------

        [Test]
        public void Advance_WindUpCompletes_Strikes_ThenRecovers()
        {
            var cycle = Make();
            cycle.BeginWindUp();
            Assert.AreEqual(EnemyAttackPhase.WindUp, cycle.Phase);

            EnemyAttackTick tick = cycle.Advance(WindUp);
            Assert.IsTrue(tick.Struck);
            Assert.AreEqual(EnemyAttackPhase.Recover, cycle.Phase);

            EnemyAttackTick end = cycle.Advance(Recover);
            Assert.IsTrue(end.Recovered);
            Assert.AreEqual(EnemyAttackPhase.Ready, cycle.Phase);
        }

        [Test]
        public void Advance_StrikesExactlyOncePerWindUp()
        {
            var cycle = Make();
            cycle.BeginWindUp();

            int strikes = 0;
            for (int i = 0; i < 100; i++)
            {
                if (cycle.Advance(0.02f).Struck) strikes++;
            }

            Assert.AreEqual(1, strikes);
        }

        [Test]
        public void CompleteWindUp_LandsOnce_TimerAddsNoSecondStrike()
        {
            var cycle = Make();
            cycle.BeginWindUp();

            Assert.IsTrue(cycle.CompleteWindUp(), "The animation contact lands the blow.");
            Assert.IsFalse(cycle.CompleteWindUp(), "A repeated contact event must not re-land it.");

            int strikes = 0;
            for (int i = 0; i < 50; i++)
            {
                if (cycle.Advance(0.02f).Struck) strikes++;
            }

            Assert.AreEqual(0, strikes, "The wind-up timer must not add a second strike after an animation contact.");
        }

        [Test]
        public void Reset_IsAFairDodge_NoStrike()
        {
            var cycle = Make();
            cycle.BeginWindUp();
            cycle.Advance(0.2f);

            cycle.Reset();

            Assert.AreEqual(EnemyAttackPhase.Ready, cycle.Phase);
            Assert.IsFalse(cycle.Advance(WindUp).Struck, "A reset attack never strikes.");
        }

        // --- Interrupt windows ------------------------------------------------------------------------

        [Test]
        public void IsInterruptible_FollowsAuthoredPhaseFlags()
        {
            var cycle = Make(windUpInterruptible: true, recoverInterruptible: false);
            Assert.IsFalse(cycle.IsInterruptible, "Ready is never interruptible.");

            cycle.BeginWindUp();
            Assert.IsTrue(cycle.IsInterruptible, "Wind-up is interruptible as authored.");

            cycle.Advance(WindUp); // now recovering
            Assert.IsFalse(cycle.IsInterruptible, "Recovery is not interruptible as authored.");
        }

        [Test]
        public void TryInterrupt_DuringInterruptibleWindUp_CancelsAttack_AndPreventsPendingStrike()
        {
            var cycle = Make(windUpInterruptible: true);
            cycle.BeginWindUp();
            cycle.Advance(0.2f); // part-way through the wind-up, before the strike

            Assert.IsTrue(cycle.TryInterrupt(), "A hit during the interruptible wind-up cancels the attack.");
            Assert.AreEqual(EnemyAttackPhase.Ready, cycle.Phase);

            int strikes = 0;
            for (int i = 0; i < 100; i++)
            {
                if (cycle.Advance(0.02f).Struck) strikes++;
            }

            Assert.AreEqual(0, strikes, "The cancelled attack must never deal its pending strike.");
        }

        [Test]
        public void TryInterrupt_OutsideWindow_DoesNotCancel_AndTheStrikeStillLands()
        {
            // Wind-up not interruptible: a hit while winding up must not cancel the pending blow.
            var cycle = Make(windUpInterruptible: false);
            cycle.BeginWindUp();
            cycle.Advance(0.2f);

            Assert.IsFalse(cycle.TryInterrupt(), "A hit outside the interrupt window does not cancel the attack.");
            Assert.AreEqual(EnemyAttackPhase.WindUp, cycle.Phase);
            Assert.IsTrue(cycle.Advance(WindUp).Struck, "The un-interrupted attack still strikes.");
        }

        [Test]
        public void TryInterrupt_DuringRecovery_OnlyWhenAuthoredInterruptible()
        {
            var committed = Make(recoverInterruptible: false);
            committed.BeginWindUp();
            committed.Advance(WindUp); // recovering
            Assert.IsFalse(committed.TryInterrupt(), "Recovery is committed by default.");
            Assert.AreEqual(EnemyAttackPhase.Recover, committed.Phase);

            var staggerable = Make(recoverInterruptible: true);
            staggerable.BeginWindUp();
            staggerable.Advance(WindUp); // recovering
            Assert.IsTrue(staggerable.TryInterrupt(), "An authored-interruptible recovery is cancelled by a hit.");
            Assert.AreEqual(EnemyAttackPhase.Ready, staggerable.Phase);
        }

        [Test]
        public void TryInterrupt_WhenReady_IsNoOp()
        {
            var cycle = Make();
            Assert.IsFalse(cycle.TryInterrupt());
            Assert.AreEqual(EnemyAttackPhase.Ready, cycle.Phase);
        }

        [Test]
        public void Interruption_DoesNotPermanentlyDisableFutureAttacks()
        {
            var cycle = Make(windUpInterruptible: true);
            cycle.BeginWindUp();
            cycle.Advance(0.2f);
            cycle.TryInterrupt();

            // The enemy resumes: it can wind up and strike again.
            cycle.BeginWindUp();
            Assert.AreEqual(EnemyAttackPhase.WindUp, cycle.Phase);
            Assert.IsTrue(cycle.Advance(WindUp).Struck, "A future attack after an interruption still lands.");
        }

        // --- Early interrupt window (flinch vs stagger) ----------------------------------------------------

        [Test]
        public void EarlyWindow_AFlinchInterruptsOnlyNearTheAttackStart()
        {
            var cycle = new EnemyAttackCycle(WindUp, Recover, windUpInterruptible: true, recoverInterruptible: false, interruptWindowSeconds: 0.2f);
            cycle.BeginWindUp();
            cycle.Advance(0.1f);
            Assert.IsTrue(cycle.IsInterruptible, "Inside the window.");
            Assert.IsTrue(cycle.TryInterrupt(), "A flinch at 0.1 s cancels the swing.");

            cycle.BeginWindUp();
            cycle.Advance(0.3f);
            Assert.IsFalse(cycle.IsInterruptible, "Committed: past the 0.2 s window.");
            Assert.IsFalse(cycle.TryInterrupt(), "A flinch at 0.3 s no longer stops it.");
            Assert.IsTrue(cycle.Advance(WindUp).Struck, "The committed swing still lands.");
        }

        [Test]
        public void EarlyWindow_AStaggerInterruptsTheWholeWindUp()
        {
            var cycle = new EnemyAttackCycle(WindUp, Recover, windUpInterruptible: true, recoverInterruptible: false, interruptWindowSeconds: 0.2f);
            cycle.BeginWindUp();
            cycle.Advance(0.45f);
            Assert.IsTrue(cycle.TryInterrupt(ignoreWindow: true), "A heavy hit stops even a committed swing.");
            Assert.AreEqual(EnemyAttackPhase.Ready, cycle.Phase);
        }

        [Test]
        public void EarlyWindow_NeverOverridesAnUninterruptibleAttack()
        {
            var cycle = new EnemyAttackCycle(WindUp, Recover, windUpInterruptible: false, recoverInterruptible: false, interruptWindowSeconds: 0.2f);
            cycle.BeginWindUp();
            Assert.IsFalse(cycle.TryInterrupt());
            Assert.IsFalse(cycle.TryInterrupt(ignoreWindow: true), "Armour beats even a stagger.");
        }
    }
}
