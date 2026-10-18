using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Gameplay.Combat;

namespace ToyChest.Tests.Combat
{
    /// <summary>
    /// Verifies the combo's ordering and continuation feel with no scene, abilities, or animation — the
    /// pure <see cref="ComboSequencer"/> only. It proves the responsive-but-forgiving contract the review
    /// group requires: the first attack starts on press, a continuation press inside the window chains the
    /// next attack with no dead time, a press outside the window does not, the combo advances
    /// deterministically and terminates after the final step, and — crucially — the sequencer holds no
    /// attack-specific branches, so any number of arbitrary steps sequence identically.
    /// </summary>
    public sealed class ComboSequencerTests
    {
        private const float WindUp = 0.3f;
        private const float Recovery = 0.3f;
        private const float Window = 0.18f; // shorter than recovery, so a late-but-in-recovery press can miss it

        private static ComboSequencer Make(int steps, float windUp = WindUp, float recovery = Recovery, float window = Window)
        {
            var timings = new List<ComboStepTiming>();
            for (int i = 0; i < steps; i++)
            {
                timings.Add(new ComboStepTiming(windUp, recovery, window));
            }

            return new ComboSequencer(timings);
        }

        // --- First attack -----------------------------------------------------------------------------

        [Test]
        public void Press_WhenIdle_StartsFirstStep()
        {
            var combo = Make(3);

            ComboTick tick = combo.Press();

            Assert.IsTrue(tick.StepStarted);
            Assert.AreEqual(0, tick.StepIndex);
            Assert.IsTrue(combo.IsActive);
            Assert.AreEqual(0, combo.StepIndex);
            Assert.AreEqual(ComboPhase.WindUp, combo.Phase);
        }

        [Test]
        public void FirstStep_ReachesContact_ThenRecovers()
        {
            var combo = Make(3);
            combo.Press();

            ComboTick contact = combo.Advance(WindUp);
            Assert.IsTrue(contact.Impacted);
            Assert.AreEqual(0, contact.StepIndex);
            Assert.AreEqual(ComboPhase.Recovery, combo.Phase);
        }

        [Test]
        public void EmptyCombo_PressDoesNothing()
        {
            var combo = Make(0);
            ComboTick tick = combo.Press();

            Assert.IsFalse(tick.StepStarted);
            Assert.IsFalse(combo.IsActive);
        }

        // --- Continuation window ----------------------------------------------------------------------

        [Test]
        public void ContinuationPress_InsideWindow_ChainsNextStep()
        {
            var combo = Make(3);
            combo.Press();
            combo.Advance(WindUp); // contact on step 0, now recovering

            ComboTick queued = combo.Press(); // within the window (sinceImpact == 0)
            Assert.IsTrue(queued.Queued, "A press just after contact is buffered as a continuation.");
            Assert.IsTrue(combo.HasQueuedContinuation);

            ComboTick chained = combo.Advance(0.02f);
            Assert.IsTrue(chained.StepStarted);
            Assert.AreEqual(1, chained.StepIndex, "The buffered continuation chains to the next step immediately.");
            Assert.AreEqual(ComboPhase.WindUp, combo.Phase);
        }

        [Test]
        public void LatePress_DuringCommittedRecovery_StillBuffers()
        {
            // The whole committed phase is a press-ahead window, not just the instant after contact.
            var combo = Make(3);
            combo.Press();
            combo.Advance(WindUp);      // contact on step 0
            combo.Advance(Recovery * 0.9f); // late in the committed recovery

            Assert.IsTrue(combo.Press().Queued, "A late press while the attack is still committed buffers.");
            Assert.IsTrue(combo.Advance(0.01f).StepStarted);
            Assert.AreEqual(1, combo.StepIndex);
        }

        [Test]
        public void ContinuationPress_AfterReleaseGraceExpires_StartsFresh()
        {
            var combo = Make(3);
            combo.Press();
            combo.Advance(WindUp);   // contact
            combo.Advance(Recovery); // commitment ends -> Release
            ComboTick end = combo.Advance(Window + 0.02f); // grace runs out

            Assert.IsTrue(end.Finished, "With no continuation inside the grace, the combo ends.");
            ComboTick fresh = combo.Press();
            Assert.IsTrue(fresh.StepStarted);
            Assert.AreEqual(0, fresh.StepIndex, "A press after the window starts the combo over, not hit 2.");
        }

        [Test]
        public void ReleaseGrace_IsUncommitted_AndAPressContinuesImmediately()
        {
            var combo = Make(3);
            combo.Press();
            Assert.IsTrue(combo.IsCommitted, "Wind-up is committed.");
            combo.Advance(WindUp);
            Assert.IsTrue(combo.IsCommitted, "Recovery is committed.");

            ComboTick released = combo.Advance(Recovery);
            Assert.IsTrue(released.Released);
            Assert.AreEqual(ComboPhase.Release, combo.Phase);
            Assert.IsFalse(combo.IsCommitted, "In Release the player is free to move…");
            Assert.IsTrue(combo.IsActive, "…but the combo is still open.");

            ComboTick next = combo.Press();
            Assert.IsTrue(next.StepStarted, "A press in the grace continues straight to the next step.");
            Assert.AreEqual(1, next.StepIndex);
            Assert.IsTrue(combo.IsCommitted);
        }

        [Test]
        public void PressAhead_DuringWindUp_ChainsAtContact_WithNoDeadTime()
        {
            var combo = Make(3);
            combo.Press();

            ComboTick queued = combo.Press(); // pressed ahead, mid wind-up
            Assert.IsTrue(queued.Queued);

            ComboTick contact = combo.Advance(WindUp);
            Assert.IsTrue(contact.Impacted, "The current blow still lands on its contact frame.");

            ComboTick chained = combo.Advance(0.01f);
            Assert.IsTrue(chained.StepStarted, "The pressed-ahead continuation chains on the very next tick — no recovery dead time.");
            Assert.AreEqual(1, chained.StepIndex);
        }

        [Test]
        public void NoContinuation_TerminatesAfterRecoveryAndGrace()
        {
            var combo = Make(3);
            combo.Press();
            combo.Advance(WindUp);

            Assert.IsTrue(combo.Advance(Recovery).Released);
            ComboTick end = combo.Advance(Window);
            Assert.IsTrue(end.Finished);
            Assert.IsFalse(combo.IsActive);
            Assert.AreEqual(-1, combo.StepIndex);
        }

        [Test]
        public void FinalStep_CommitmentEnd_FinishesTheCombo()
        {
            // The last hit has nothing to continue to, so ending its commitment ends the combo outright.
            var combo = Make(1);
            combo.Press();
            combo.Advance(WindUp);

            ComboTick end = combo.Advance(Recovery);
            Assert.IsTrue(end.Finished);
            Assert.IsFalse(combo.IsActive);
            Assert.IsFalse(combo.IsCommitted);
        }

        // --- Deterministic progression & termination --------------------------------------------------

        [Test]
        public void Combo_AdvancesThroughEveryStep_ThenTerminates()
        {
            var combo = Make(3);
            var startedOrder = new List<int>();
            var impactedOrder = new List<int>();

            ComboTick t = combo.Press();
            startedOrder.Add(t.StepIndex);

            // Press ahead each step so the whole string plays out, then let the last step recover.
            for (int step = 0; step < 3; step++)
            {
                combo.Press(); // buffers a continuation (ignored on the last step — nothing to chain)
                ComboTick contact = combo.Advance(WindUp);
                if (contact.Impacted) impactedOrder.Add(contact.StepIndex);

                ComboTick after = combo.Advance(0.01f);
                if (after.StepStarted) startedOrder.Add(after.StepIndex);
            }

            ComboTick end = combo.Advance(Recovery);

            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, startedOrder, "Steps start in order 0,1,2 — deterministically.");
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, impactedOrder, "Each step lands its blow in order.");
            Assert.IsTrue(end.Finished, "After the final step the combo terminates.");
            Assert.IsFalse(combo.IsActive);
        }

        [Test]
        public void LastStep_CannotBufferContinuation()
        {
            var combo = Make(2);
            combo.Press();                 // step 0
            combo.Press();                 // buffer
            combo.Advance(WindUp);         // contact 0
            combo.Advance(0.01f);          // chain to step 1 (final)

            Assert.AreEqual(1, combo.StepIndex);
            ComboTick tick = combo.Press(); // no step after the last one
            Assert.IsFalse(tick.Queued);
            Assert.IsFalse(combo.HasQueuedContinuation);
        }

        // --- Content-agnostic: arbitrary steps, no attack-specific branches ---------------------------

        [Test]
        public void ArbitraryStepCountAndTimings_SequenceIdentically()
        {
            // A 4-step combo of wildly different per-step timings — a stand-in for "Punch, Punch, Kick,
            // Ground Slam". The sequencer only ever sees timings, so it advances through all four the same
            // way it advances a uniform 3-punch string: proof there is no per-attack hardcoded branch.
            var timings = new List<ComboStepTiming>
            {
                new ComboStepTiming(0.2f, 0.2f, 0.15f),
                new ComboStepTiming(0.35f, 0.25f, 0.2f),
                new ComboStepTiming(0.5f, 0.3f, 0.25f),
                new ComboStepTiming(0.6f, 0.4f, 0.3f),
            };
            var combo = new ComboSequencer(timings);
            var started = new List<int> { combo.Press().StepIndex };

            for (int step = 0; step < 4; step++)
            {
                combo.Press();
                combo.Advance(timings[step].WindUp);
                ComboTick after = combo.Advance(0.01f);
                if (after.StepStarted) started.Add(after.StepIndex);
            }

            ComboTick end = combo.Advance(0.4f);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, started);
            Assert.IsTrue(end.Finished);
        }

        // --- Attack-end seam (snappy, per-clip exit) --------------------------------------------------

        [Test]
        public void NotifyAttackEnd_ReleasesCommitmentAtOnce_ButKeepsTheComboOpen()
        {
            var combo = Make(3, recovery: 1.5f); // a long fallback timer that the event should pre-empt
            combo.Press();
            combo.Advance(WindUp); // contact — the fallback would keep it committed for 1.5s

            ComboTick released = combo.NotifyAttackEnd();

            Assert.IsTrue(released.Released, "OnAttackEnd ends the commitment immediately.");
            Assert.IsFalse(combo.IsCommitted, "The player is free to move at once…");
            Assert.IsTrue(combo.Press().StepStarted, "…and a press still advances to the next hit.");
            Assert.AreEqual(1, combo.StepIndex);
        }

        [Test]
        public void ZeroContinuationWindow_ClosesTheComboExactlyAtAttackEnd()
        {
            // The shipped default: the continuation window is the committed attack itself, nothing after it.
            var combo = Make(3, window: 0f);
            combo.Press();
            combo.Advance(WindUp);

            ComboTick end = combo.NotifyAttackEnd();
            Assert.IsTrue(end.Finished, "With no grace, OnAttackEnd finishes the combo.");

            ComboTick fresh = combo.Press();
            Assert.AreEqual(0, fresh.StepIndex, "A press after OnAttackEnd starts over at hit 1, not hit 2.");
        }

        [Test]
        public void ZeroContinuationWindow_PressDuringTheAttack_StillContinues()
        {
            var combo = Make(3, window: 0f);
            combo.Press();
            combo.Advance(WindUp);
            Assert.IsTrue(combo.Press().Queued, "Any press while the attack is committed still queues hit 2.");

            ComboTick next = combo.NotifyAttackEnd();
            Assert.IsTrue(next.StepStarted);
            Assert.AreEqual(1, next.StepIndex);
        }

        [Test]
        public void NotifyAttackEnd_OnFinalStep_FinishesTheCombo()
        {
            var combo = Make(1, recovery: 3f); // a long finisher whose fallback would strand the player
            combo.Press();
            combo.Advance(WindUp);

            ComboTick end = combo.NotifyAttackEnd();
            Assert.IsTrue(end.Finished);
            Assert.IsFalse(combo.IsActive);
        }

        [Test]
        public void NotifyAttackEnd_WithBufferedContinuation_ChainsInstead()
        {
            var combo = Make(3);
            combo.Press();
            combo.Advance(WindUp); // contact on step 0
            combo.Press();         // buffer a continuation within the window

            ComboTick tick = combo.NotifyAttackEnd();

            Assert.IsTrue(tick.StepStarted, "A buffered continuation chains rather than ending the combo.");
            Assert.AreEqual(1, tick.StepIndex);
        }

        [Test]
        public void NotifyAttackEnd_BeforeContact_IsIgnored()
        {
            var combo = Make(3);
            combo.Press(); // still winding up

            Assert.IsFalse(combo.NotifyAttackEnd().Finished, "The end can never precede the contact; a stray event is a no-op.");
            Assert.AreEqual(ComboPhase.WindUp, combo.Phase);
        }

        [Test]
        public void NotifyAttackEnd_WhenIdle_IsIgnored()
        {
            var combo = Make(3);
            ComboTick tick = combo.NotifyAttackEnd();
            Assert.IsFalse(tick.Finished);
            Assert.IsFalse(tick.StepStarted);
            Assert.IsFalse(combo.IsActive);
        }

        // --- Animation-contact seam -------------------------------------------------------------------

        [Test]
        public void NotifyContact_LandsBlowOnce_AndTimerDoesNotAddAnother()
        {
            var combo = Make(3);
            combo.Press();

            ComboTick byEvent = combo.NotifyContact();
            Assert.IsTrue(byEvent.Impacted, "The animation contact event lands the blow.");
            Assert.AreEqual(ComboPhase.Recovery, combo.Phase);

            Assert.IsFalse(combo.NotifyContact().Impacted, "A repeated contact event must not re-land the blow.");

            int impacts = 0;
            for (int i = 0; i < 50; i++)
            {
                if (combo.Advance(0.02f).Impacted) impacts++;
            }

            Assert.AreEqual(0, impacts, "The wind-up timer must not add a second blow after an animation contact.");
        }

        [Test]
        public void Cancel_ReturnsToIdle()
        {
            var combo = Make(3);
            combo.Press();
            combo.Advance(0.1f);

            combo.Cancel();

            Assert.IsFalse(combo.IsActive);
            Assert.AreEqual(-1, combo.StepIndex);
            Assert.AreEqual(ComboPhase.Idle, combo.Phase);
        }
    }
}
