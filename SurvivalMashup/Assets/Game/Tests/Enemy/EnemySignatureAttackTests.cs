using NUnit.Framework;
using ToyChest.Gameplay.Enemy;

namespace ToyChest.Tests.Enemy
{
    /// <summary>
    /// Review Group C — the timing of an elite's signature attack (the Grunt Brute's Shockwave Slam), with no scene: it
    /// waits out an initial delay, begins only within its trigger range, lands exactly once at the end of its wind-up,
    /// cannot be interrupted by hits when authored so, roots through its recovery, and then waits out its cooldown
    /// before it can begin again. The wind-up progress the ground telegraph shows runs 0 → 1 across the wind-up.
    /// </summary>
    public sealed class EnemySignatureAttackTests
    {
        private const float WindUp = 1.2f;
        private const float Recover = 1.6f;
        private const float Cooldown = 5f;
        private const float InitialDelay = 1.5f;
        private const float Range = 4.5f;

        private static EnemySignatureAttack Make(bool interruptible = false, float initialDelay = 0f)
        {
            return new EnemySignatureAttack(WindUp, Recover, Cooldown, initialDelay, Range, interruptible);
        }

        [Test]
        public void InitialDelay_MustElapse_BeforeTheFirstUse()
        {
            var slam = Make(initialDelay: InitialDelay);
            Assert.IsFalse(slam.ShouldBegin(1f), "Not on the elite's first frame: the player gets a moment to read the fight.");

            slam.Advance(InitialDelay - 0.01f);
            Assert.IsFalse(slam.ShouldBegin(1f));

            slam.Advance(0.02f);
            Assert.IsTrue(slam.ShouldBegin(1f));
        }

        [Test]
        public void Begins_OnlyWithinTriggerRange()
        {
            var slam = Make();
            Assert.IsTrue(slam.ShouldBegin(Range));
            Assert.IsFalse(slam.ShouldBegin(Range + 0.01f));
        }

        [Test]
        public void LandsOnce_AtTheEndOfTheWindUp()
        {
            var slam = Make();
            Assert.IsTrue(slam.Begin());

            int strikes = 0;
            float strikeTime = -1f;
            float t = 0f;
            for (int i = 0; i < 200; i++)
            {
                t += 0.02f;
                if (slam.Advance(0.02f).Struck)
                {
                    strikes++;
                    strikeTime = t;
                }
            }

            Assert.AreEqual(1, strikes);
            Assert.AreEqual(WindUp, strikeTime, 0.021f, "The blow lands when the telegraph completes, not before.");
        }

        [Test]
        public void WindUpProgress_RunsFromZeroToOne_AcrossTheTelegraph()
        {
            var slam = Make();
            slam.Begin();
            Assert.AreEqual(0f, slam.Cycle.WindUpProgress, 1e-4f);

            slam.Advance(WindUp * 0.5f);
            Assert.AreEqual(0.5f, slam.Cycle.WindUpProgress, 1e-4f);

            slam.Advance(WindUp * 0.5f);
            Assert.AreEqual(EnemyAttackPhase.Recover, slam.Cycle.Phase);
            Assert.AreEqual(0f, slam.Cycle.WindUpProgress, "No telegraph outside the wind-up.");
        }

        [Test]
        public void NotInterruptible_AHitDuringTheWindUpDoesNotCancelIt()
        {
            var slam = Make(interruptible: false);
            slam.Begin();
            slam.Advance(WindUp * 0.5f);

            Assert.IsFalse(slam.TryInterrupt());
            Assert.IsTrue(slam.Cycle.IsWindingUp);
            Assert.IsTrue(slam.Advance(WindUp).Struck, "A punch cannot stop the slam: the answer is to leave the circle.");
        }

        [Test]
        public void Interruptible_WhenAuthoredSo_AndThenWaitsOutItsCooldown()
        {
            var slam = Make(interruptible: true);
            slam.Begin();
            slam.Advance(WindUp * 0.5f);

            Assert.IsTrue(slam.TryInterrupt());
            Assert.IsFalse(slam.Cycle.IsAttacking);
            Assert.AreEqual(Cooldown, slam.CooldownRemaining, 1e-4f);
            Assert.IsFalse(slam.ShouldBegin(1f));
        }

        [Test]
        public void Recovery_IsNeverInterruptible_ThePunishWindowAlwaysPlaysOut()
        {
            var slam = Make(interruptible: true);
            slam.Begin();
            slam.Advance(WindUp);
            Assert.AreEqual(EnemyAttackPhase.Recover, slam.Cycle.Phase);

            Assert.IsFalse(slam.TryInterrupt());
            Assert.AreEqual(EnemyAttackPhase.Recover, slam.Cycle.Phase);
        }

        [Test]
        public void Cooldown_StartsWhenRecoveryEnds_ThenItCanBeginAgain()
        {
            var slam = Make();
            slam.Begin();
            slam.Advance(WindUp);
            Assert.IsFalse(slam.Begin(), "Cannot begin again mid-attack.");

            Assert.IsTrue(slam.Advance(Recover).Recovered);
            Assert.AreEqual(Cooldown, slam.CooldownRemaining, 1e-4f);
            Assert.IsFalse(slam.ShouldBegin(1f));

            slam.Advance(Cooldown);
            Assert.IsTrue(slam.ShouldBegin(1f));
            Assert.IsTrue(slam.Begin());
        }

        [Test]
        public void Cooldown_DoesNotTick_WhileAttacking()
        {
            var slam = Make();
            slam.Begin();
            slam.Advance(WindUp);
            slam.Advance(Recover * 0.5f);
            Assert.AreEqual(0f, slam.CooldownRemaining, "The cooldown is measured from the end of recovery.");
        }
    }
}
