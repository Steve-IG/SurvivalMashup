using System.Collections.Generic;
using System.Text;
using ToyChest.Gameplay.Combat;
using ToyChest.Gameplay.HitDetection;
using UnityEditor;
using UnityEngine;

namespace ToyChest.Editor
{
    /// <summary>
    /// Proposes an attack's hit windows and commitment end by analysing its clip on a character — the
    /// automation that makes authoring many melee attacks cheap. It samples the clip onto a humanoid and reads
    /// each limb's <b>extension</b> (how far its tip is in front of the hips, along the character's facing),
    /// its height, and its speed, plus the humanoid root curves:
    /// <list type="bullet">
    /// <item><b>Ground strikes</b> are scored by <em>snap</em>: how far a limb drives out in front of the body
    /// (rise) plus how far it pulls back right after (fall). A punch extends and retracts; a guard hand
    /// extends and holds; an off arm swings back. The best-scoring hand is the punch; the other hand counts
    /// only if it scores nearly as well (merged into the same window when simultaneous, its own window when it
    /// is a deliberate one-two). Feet count only if they leave the ground — a kick lifts the foot, a step
    /// doesn't.</item>
    /// <item><b>Leaps</b> (root height rises ≥ <see cref="MinApex"/>): mid-flip every limb whips around the hips,
    /// so feet strike by speed while airborne and hands strike at the landing slam.</item>
    /// <item><b>Commitment end</b> — where the root stops travelling.</item>
    /// </list>
    /// The result is a starting point: the Attack Authoring window shows it on a scrubbable timeline with live
    /// limb preview for a designer to adjust.
    /// </summary>
    public static class AttackWindowProposer
    {
        public const float MinRise = 0.3f;           // m a limb must drive forward for a ground strike
        public const float SecondHandScore = 0.8f;   // other hand counts if it snaps this well relative to the best
        public const float KickLift = 0.35f;         // m a foot must rise off the ground to be a kick
        public const float AirKickSpeed = 18f;       // m/s for a mid-leap foot peak to count
        private const float RiseLookback = 0.35f;    // s window to measure a strike's rise
        private const float FallLookahead = 0.3f;    // s window to measure a strike's retraction
        private const float ExtendedTolerance = 0.15f; // m below peak still counted as "extended"
        private const float OneTwoGap = 0.08f;       // s: hand strikes closer than this are one (two-handed) motion; a one-two jab is ~0.13s apart
        private const float HintReach = 0.15f;       // s: how far from the strike frame a drive's peak may be
        private const float CancelDelay = 0.08f;     // s after the last window before the recovery may be cancelled
        private const float MinCancelTail = 0.6f;    // s: shorter recovery tails stay committed
        private const float StrikeActiveTime = 0.08f; // s a blow stays active past full extension
        private const float MinApex = 0.5f;
        private const float LandTolerance = 0.12f;  // m above the start height still counted as landed
        public const float SweepArc = 120f;          // deg a fast limb must travel around the body to be a sweep
        public const float SweepSpeed = 15f;         // m/s peak for a sweep (hook, backfist, spin kick)
        private const float SlamHalfWidth = 0.05f;
        private const float RootMovingSpeed = 0.25f;
        private const float SampleRate = 60f;

        /// <summary>The proposal: windows (normalized clip time), commitment end, and a readable account of why.</summary>
        public sealed class Proposal
        {
            public readonly List<HitWindow> Windows = new List<HitWindow>();
            public float CommitEnd;
            public float CancelFrom;
            public string Report;
        }

        private sealed class Track
        {
            public readonly List<float> Extension = new List<float>();
            public readonly List<float> Height = new List<float>();
            public readonly List<float> Speed = new List<float>();
            public readonly List<float> Yaw = new List<float>(); // unwrapped degrees around the hips, from the facing
            public readonly List<float> Reach = new List<float>(); // planar distance from the hips
        }

        private struct Sweep
        {
            public StrikeLimb Limb;
            public float Start, End, PeakTime, PeakSpeed, Arc;
        }

        private struct Strike
        {
            public StrikeLimb Limb;
            public float Start, End, PeakTime, Score, Rise;
        }

        /// <summary>
        /// Analyse <paramref name="clip"/> as played at <paramref name="playbackSpeed"/> on the humanoid
        /// <paramref name="animator"/> (its pose is restored afterwards).
        /// </summary>
        public static Proposal Propose(Animator animator, AnimationClip clip, float playbackSpeed, float strikeHint = -1f)
        {
            var proposal = new Proposal();
            var report = new StringBuilder();
            if (animator == null || !animator.isHuman || clip == null)
            {
                proposal.Report = "Needs a humanoid character and a clip.";
                return proposal;
            }

            float speed = playbackSpeed <= 0.01f ? 1f : playbackSpeed;
            float length = clip.length;
            Dictionary<StrikeLimb, Track> tracks = Sample(animator, clip, speed, out float dt, out float ground);

            // The designer's word beats inference: an explicit strike frame, else a contact event already on the clip.
            float hintTime = strikeHint >= 0f ? strikeHint * length : -1f;
            string hintSource = "the strike-frame hint";
            if (hintTime < 0f)
            {
                foreach (AnimationEvent e in AnimationUtility.GetAnimationEvents(clip))
                {
                    if (e.functionName == "OnAttackContact" || e.functionName == "OnAttackContactStart")
                    {
                        hintTime = e.time;
                        hintSource = $"the clip's {e.functionName} event";
                        break;
                    }
                }
            }

            if (hintTime >= 0f)
            {
                report.AppendLine($"Strike frame: {hintTime:0.00}s (from {hintSource}).");
            }

            if (TryMeasureLeap(clip, out float takeoff, out float landing, out float apex))
            {
                report.AppendLine($"Leap: apex {apex:0.00}m, takeoff {takeoff:0.00}s, landing {landing:0.00}s.");
                ProposeLeap(proposal, report, tracks, dt, takeoff, landing, length);
            }
            else
            {
                report.AppendLine("Ground attack.");
                ProposeGround(proposal, report, tracks, dt, ground, length, hintTime);
            }

            proposal.Windows.Sort((a, b) => a.Start.CompareTo(b.Start));

            float lastWindowEnd = 0f;
            foreach (HitWindow w in proposal.Windows) lastWindowEnd = Mathf.Max(lastWindowEnd, w.End);
            float rootStop = FindRootMotionEnd(clip) / length;
            proposal.CommitEnd = Mathf.Clamp(Mathf.Max(rootStop + 0.03f / length, lastWindowEnd + 0.02f), 0.05f, 0.95f);
            report.AppendLine($"Commitment end: {proposal.CommitEnd:0.00} (root motion stops at {rootStop:0.00}).");

            // Once the last blow has landed, a long tail (getting up, resetting a stance) is optional: movement or
            // a new attack may cut it. Short tails are left committed — there is nothing worth cancelling.
            float cancelFrom = lastWindowEnd + CancelDelay / length;
            if (proposal.Windows.Count > 0 && (proposal.CommitEnd - cancelFrom) * length >= MinCancelTail)
            {
                proposal.CancelFrom = cancelFrom;
                report.AppendLine($"Cancel point: {cancelFrom * length:0.00}s — the {(proposal.CommitEnd - cancelFrom) * length:0.00}s " +
                                  "recovery after the last strike can be cut by moving or attacking.");
            }
            if (proposal.Windows.Count == 0)
            {
                report.AppendLine("No strike found — add windows by hand.");
            }

            proposal.Report = report.ToString();
            return proposal;
        }

        // --- Ground attacks -----------------------------------------------------------------------------

        private static void ProposeGround(
            Proposal p, StringBuilder report, Dictionary<StrikeLimb, Track> tracks, float dt, float ground, float length, float hintTime)
        {
            if (hintTime >= 0f && TryProposeFromHint(p, report, tracks, dt, ground, length, hintTime))
            {
                return;
            }

            var hands = new List<Strike>();
            foreach (StrikeLimb limb in new[] { StrikeLimb.RightHand, StrikeLimb.LeftHand })
            {
                if (TryBestStrike(limb, tracks[limb], dt, out Strike s))
                {
                    hands.Add(s);
                    report.AppendLine($"  {limb}: drives out {s.Rise:0.00}m, snap {s.Score:0.00} at {s.PeakTime:0.00}s.");
                }
            }

            // Two hands that both drive well forward at different moments cannot be told apart by motion alone
            // (a sweep/parry/feint vs the blow, or a lead-hand straight that lands and holds). Say so rather than
            // guess silently — the strike frame resolves it.
            if (hands.Count > 1)
            {
                var byScore = new List<Strike>(hands);
                byScore.Sort((a, b) => b.Score.CompareTo(a.Score));
                Strike top = byScore[0], other = byScore[1];
                // Ambiguous = the other hand drives as far but is about to be dropped for snapping less.
                if (other.Rise >= top.Rise * SecondHandScore && other.Score < top.Score * SecondHandScore)
                {
                    report.AppendLine($"  ⚠ AMBIGUOUS: {top.Limb} ({top.Rise:0.00}m at {top.PeakTime:0.00}s) and {other.Limb} " +
                                      $"({other.Rise:0.00}m at {other.PeakTime:0.00}s) both drive forward. Proposing {top.Limb}; " +
                                      "if the blow is the other hand, scrub to the impact frame, press 'Set strike frame' and propose again.");
                }
            }

            if (hands.Count > 0)
            {
                hands.Sort((a, b) => b.Score.CompareTo(a.Score));
                Strike best = hands[0];
                if (hands.Count > 1 && hands[1].Score >= best.Score * SecondHandScore)
                {
                    Strike other = hands[1];
                    if (Mathf.Abs(other.PeakTime - best.PeakTime) <= OneTwoGap)
                    {
                        best.Start = Mathf.Min(best.Start, other.Start);
                        best.End = Mathf.Max(best.End, other.End);
                        Add(p, report, "Punch", StrikeLimbMask.BothHands, best, length);
                    }
                    else
                    {
                        Add(p, report, HandName(best.Limb), StrikeLimbs.ToMask(best.Limb), best, length);
                        Add(p, report, HandName(other.Limb), StrikeLimbs.ToMask(other.Limb), other, length);
                    }
                }
                else
                {
                    if (hands.Count > 1)
                    {
                        report.AppendLine($"  {hands[1].Limb}: snap {hands[1].Score:0.00} is under {SecondHandScore:P0} of the punch — guard/off arm, not a blow.");
                    }

                    Add(p, report, HandName(best.Limb), StrikeLimbs.ToMask(best.Limb), best, length);
                }
            }

            foreach (StrikeLimb limb in new[] { StrikeLimb.RightFoot, StrikeLimb.LeftFoot })
            {
                if (!TryBestStrike(limb, tracks[limb], dt, out Strike s))
                {
                    continue;
                }

                int peak = Mathf.RoundToInt(s.PeakTime / dt);
                float lift = tracks[limb].Height[peak] - ground;
                if (lift < KickLift)
                {
                    report.AppendLine($"  {limb}: moves {s.Rise:0.00}m forward but stays {lift:0.00}m off the ground — a step, not a kick.");
                    continue;
                }

                Add(p, report, limb == StrikeLimb.RightFoot ? "Right kick" : "Left kick", StrikeLimbs.ToMask(limb), s, length);
            }

            // Forward-drive scoring cannot judge a hook, backfist or spin kick. Flag any fast wide sweep the proposal does
            // not cover rather than silently missing (or mis-timing) the blow.
            foreach (StrikeLimb limb in new[] { StrikeLimb.RightHand, StrikeLimb.LeftHand, StrikeLimb.RightFoot, StrikeLimb.LeftFoot })
            {
                foreach (Sweep sw in FindSweeps(limb, tracks[limb], dt, ground))
                {
                    if (!Covers(p, limb, sw.PeakTime / length))
                    {
                        report.AppendLine($"  ⚠ SWEEP NOT PROPOSED: {limb} sweeps {sw.Arc:0}° at {sw.PeakSpeed:0} m/s ({sw.Start:0.00}–{sw.End:0.00}s). " +
                                          "If that is the blow (a hook / spin), scrub to its impact, press 'Set strike frame' and propose again.");
                    }
                }
            }
        }

        // The fastest striking limb (feet only when lifted) within HintReach of the strike frame.
        private static float FastestAt(Dictionary<StrikeLimb, Track> tracks, float dt, float ground, float hintTime, out StrikeLimb fastestLimb)
        {
            float best = 0f;
            fastestLimb = StrikeLimb.RightHand;
            int from = Mathf.Max(0, Mathf.FloorToInt((hintTime - HintReach) / dt));
            foreach (StrikeLimb limb in new[] { StrikeLimb.RightHand, StrikeLimb.LeftHand, StrikeLimb.RightFoot, StrikeLimb.LeftFoot })
            {
                Track t = tracks[limb];
                bool foot = limb == StrikeLimb.RightFoot || limb == StrikeLimb.LeftFoot;
                int to = Mathf.Min(t.Speed.Count - 1, Mathf.CeilToInt((hintTime + HintReach) / dt));
                for (int i = from; i <= to; i++)
                {
                    if (t.Speed[i] > best && (!foot || t.Height[i] - ground >= KickLift))
                    {
                        best = t.Speed[i];
                        fastestLimb = limb;
                    }
                }
            }

            return best;
        }

        private static bool Covers(Proposal p, StrikeLimb limb, float normalized)
        {
            foreach (HitWindow w in p.Windows)
            {
                if (StrikeLimbs.Contains(w.Limbs, limb) && normalized >= w.Start && normalized <= w.End) return true;
            }

            return false;
        }

        private static string SweepName(StrikeLimb limb) => limb switch
        {
            StrikeLimb.RightHand => "Right hook",
            StrikeLimb.LeftHand => "Left hook",
            StrikeLimb.RightFoot => "Right sweep kick",
            _ => "Left sweep kick",
        };

        // Fast wide arcs of a limb around the hips: a local speed peak ≥ SweepSpeed whose fast stretch (speed ≥ half the
        // peak) travels ≥ SweepArc of yaw at a real distance from the body. Feet must also leave the ground (a spin
        // kick lifts the foot; pivoting on it doesn't).
        private static List<Sweep> FindSweeps(StrikeLimb limb, Track track, float dt, float ground)
        {
            var sweeps = new List<Sweep>();
            List<float> v = track.Speed;
            bool foot = limb == StrikeLimb.RightFoot || limb == StrikeLimb.LeftFoot;
            for (int i = 1; i < v.Count - 1; i++)
            {
                if (v[i] < SweepSpeed || v[i] < v[i - 1] || v[i] < v[i + 1])
                {
                    continue;
                }

                float half = v[i] * 0.5f;
                int l = i, r = i;
                while (l > 1 && v[l - 1] >= half) l--;
                while (r < v.Count - 1 && v[r + 1] >= half) r++;
                float yawMin = float.MaxValue, yawMax = float.MinValue, reach = 0f, top = float.MinValue;
                for (int k = l; k <= r; k++)
                {
                    yawMin = Mathf.Min(yawMin, track.Yaw[k]);
                    yawMax = Mathf.Max(yawMax, track.Yaw[k]);
                    reach = Mathf.Max(reach, track.Reach[k]);
                    top = Mathf.Max(top, track.Height[k]);
                }

                if (yawMax - yawMin < SweepArc || reach < 0.5f || (foot && top - ground < KickLift))
                {
                    continue;
                }

                var sweep = new Sweep
                {
                    Limb = limb, Start = Mathf.Max(0, l - 1) * dt, End = (r + 1) * dt,
                    PeakTime = i * dt, PeakSpeed = v[i], Arc = yawMax - yawMin,
                };

                if (sweeps.Count > 0 && sweep.Start <= sweeps[sweeps.Count - 1].End)
                {
                    if (sweep.PeakSpeed > sweeps[sweeps.Count - 1].PeakSpeed) sweeps[sweeps.Count - 1] = sweep;
                    continue;
                }

                sweeps.Add(sweep);
            }

            return sweeps;
        }

        // With a strike frame: the blow is whichever limb's forward drive is happening at that moment (its window
        // contains the frame, else its peak is nearest within HintReach). A second hand counts only if it too is
        // mid-drive at the strike frame and drives just as far (a genuine two-handed blow).
        private static bool TryProposeFromHint(
            Proposal p, StringBuilder report, Dictionary<StrikeLimb, Track> tracks, float dt, float ground, float length, float hintTime)
        {
            // A hook, backfist or spin kick crosses in front of the body rather than driving out of it, so check for a
            // sweep at the strike frame first: the fastest limb travelling a wide arc there is the blow.
            // Only a sweep by the limb that is actually moving fastest at the strike frame counts: an off arm swinging
            // out for balance while the other hand smashes down is not the blow.
            float fastest = FastestAt(tracks, dt, ground, hintTime, out StrikeLimb fastestLimb);
            var sweeps = new List<Sweep>();
            foreach (StrikeLimb limb in new[] { StrikeLimb.RightHand, StrikeLimb.LeftHand, StrikeLimb.RightFoot, StrikeLimb.LeftFoot })
            {
                foreach (Sweep sw in FindSweeps(limb, tracks[limb], dt, ground))
                {
                    bool atStrike = (hintTime >= sw.Start && hintTime <= sw.End) || Mathf.Abs(sw.PeakTime - hintTime) <= HintReach;
                    if (!atStrike)
                    {
                        continue;
                    }

                    if (sw.PeakSpeed < fastest * SecondHandScore)
                    {
                        report.AppendLine($"  {limb}: sweeps {sw.Arc:0}° but only {sw.PeakSpeed:0} m/s — {fastestLimb} moves faster ({fastest:0} m/s) at the strike frame; not the blow.");
                        continue;
                    }

                    sweeps.Add(sw);
                }
            }

            if (sweeps.Count > 0)
            {
                sweeps.Sort((a, b) => b.PeakSpeed.CompareTo(a.PeakSpeed));
                foreach (Sweep sw in sweeps)
                {
                    // The fastest sweep is the blow; another limb sweeping just as fast through the same frame (both feet
                    // of a tornado kick) is a blow too, with its own window — one hit instance per limb.
                    if (sw.PeakSpeed < sweeps[0].PeakSpeed * SecondHandScore)
                    {
                        report.AppendLine($"  {sw.Limb}: sweeps {sw.Arc:0}° at {sw.PeakSpeed:0} m/s — slower than the blow, not proposed.");
                        continue;
                    }

                    report.AppendLine($"  {sw.Limb}: sweeps {sw.Arc:0}° around the body at {sw.PeakSpeed:0} m/s (peak {sw.PeakTime:0.00}s) — a sweep through the strike frame.");
                    var strike = new Strike { Limb = sw.Limb, Start = sw.Start, End = sw.End, PeakTime = sw.PeakTime };
                    Add(p, report, SweepName(sw.Limb), StrikeLimbs.ToMask(sw.Limb), strike, length);
                }

                return true;
            }

            var all = new List<Strike>();
            foreach (StrikeLimb limb in new[] { StrikeLimb.RightHand, StrikeLimb.LeftHand, StrikeLimb.RightFoot, StrikeLimb.LeftFoot })
            {
                foreach (Strike s in FindStrikes(limb, tracks[limb], dt))
                {
                    bool foot = limb == StrikeLimb.RightFoot || limb == StrikeLimb.LeftFoot;
                    if (foot && tracks[limb].Height[Mathf.RoundToInt(s.PeakTime / dt)] - ground < KickLift)
                    {
                        continue; // a step, not a kick
                    }

                    all.Add(s);
                }
            }

            Strike chosen = default;
            float bestDistance = float.MaxValue;
            foreach (Strike s in all)
            {
                float distance = hintTime >= s.Start && hintTime <= s.End ? 0f : Mathf.Abs(s.PeakTime - hintTime);
                if (distance <= HintReach && (distance < bestDistance || (distance == bestDistance && s.Rise > chosen.Rise)))
                {
                    chosen = s;
                    bestDistance = distance;
                }
            }

            if (bestDistance == float.MaxValue)
            {
                report.AppendLine($"  No limb drives forward within {HintReach:0.00}s of the strike frame — falling back to motion analysis.");
                return false;
            }

            StrikeLimbMask mask = StrikeLimbs.ToMask(chosen.Limb);
            foreach (Strike s in all)
            {
                if (s.Limb != chosen.Limb && IsHand(s.Limb) && IsHand(chosen.Limb) &&
                    hintTime >= s.Start && hintTime <= s.End && s.Rise >= chosen.Rise * SecondHandScore)
                {
                    mask |= StrikeLimbs.ToMask(s.Limb);
                    chosen.Start = Mathf.Min(chosen.Start, s.Start);
                    chosen.End = Mathf.Max(chosen.End, s.End);
                }
            }

            foreach (Strike s in all)
            {
                if (s.Limb != chosen.Limb && IsHand(s.Limb) && !StrikeLimbs.Contains(mask, s.Limb))
                {
                    report.AppendLine($"  {s.Limb}: also drives forward {s.Rise:0.00}m at {s.PeakTime:0.00}s — not the strike (away from the strike frame).");
                }
            }

            string name = mask == StrikeLimbMask.BothHands ? "Punch"
                : chosen.Limb == StrikeLimb.RightFoot ? "Right kick"
                : chosen.Limb == StrikeLimb.LeftFoot ? "Left kick"
                : HandName(chosen.Limb);
            report.AppendLine($"  {chosen.Limb}: drives out {chosen.Rise:0.00}m, peaking {chosen.PeakTime:0.00}s — the limb striking at the strike frame.");
            Add(p, report, name, mask, chosen, length);
            return true;
        }

        private static bool IsHand(StrikeLimb limb) => limb == StrikeLimb.RightHand || limb == StrikeLimb.LeftHand;

        // The limb's strongest snap (see FindStrikes) — used when there is no strike frame to go by.
        private static bool TryBestStrike(StrikeLimb limb, Track track, float dt, out Strike best)
        {
            best = default;
            bool found = false;
            foreach (Strike s in FindStrikes(limb, track, dt))
            {
                if (!found || s.Score > best.Score)
                {
                    best = s;
                    found = true;
                }
            }

            return found;
        }

        // Every forward drive of a limb: a local extension peak that rose ≥ MinRise over the lookback. Score = rise
        // plus the retraction that follows (a snap). Window = from half-way through the rise to where it leaves full
        // extension — but never more than StrikeActiveTime past the peak: a fist that lands and is then held out in
        // guard is a strike only for the moment of impact, not for as long as it stays extended.
        private static List<Strike> FindStrikes(StrikeLimb limb, Track track, float dt)
        {
            var strikes = new List<Strike>();
            List<float> e = track.Extension;
            int n = e.Count, back = Mathf.CeilToInt(RiseLookback / dt), ahead = Mathf.CeilToInt(FallLookahead / dt);
            int active = Mathf.CeilToInt(StrikeActiveTime / dt);
            for (int i = 1; i < n - 1; i++)
            {
                if (e[i] < e[i - 1] || e[i] < e[i + 1])
                {
                    continue;
                }

                int minIndex = i;
                for (int k = Mathf.Max(0, i - back); k < i; k++) if (e[k] < e[minIndex]) minIndex = k;
                float rise = e[i] - e[minIndex];
                if (rise < MinRise)
                {
                    continue;
                }

                float after = e[i];
                for (int k = i + 1; k <= Mathf.Min(n - 1, i + ahead); k++) after = Mathf.Min(after, e[k]);

                float half = e[minIndex] + rise * 0.5f;
                int start = i;
                while (start > minIndex && e[start - 1] >= half) start--;
                int end = i;
                while (end < Mathf.Min(n - 1, i + active) && e[end + 1] >= e[i] - ExtendedTolerance) end++;

                var strike = new Strike
                {
                    Limb = limb, Start = Mathf.Max(0, start - 1) * dt, End = (end + 1) * dt,
                    PeakTime = i * dt, Score = rise + (e[i] - after), Rise = rise,
                };

                // A plateau yields several equal local maxima: keep one strike per drive.
                if (strikes.Count > 0 && strike.Start <= strikes[strikes.Count - 1].End)
                {
                    if (strike.Score > strikes[strikes.Count - 1].Score) strikes[strikes.Count - 1] = strike;
                    continue;
                }

                strikes.Add(strike);
            }

            return strikes;
        }

        // --- Leaps --------------------------------------------------------------------------------------

        private static void ProposeLeap(Proposal p, StringBuilder report, Dictionary<StrikeLimb, Track> tracks, float dt, float takeoff, float landing, float length)
        {
            foreach (StrikeLimb limb in new[] { StrikeLimb.RightFoot, StrikeLimb.LeftFoot })
            {
                List<float> v = tracks[limb].Speed;
                int from = Mathf.Max(1, Mathf.FloorToInt((takeoff - 0.05f) / dt)), to = Mathf.Min(v.Count - 2, Mathf.CeilToInt(landing / dt));
                int peak = -1;
                for (int i = from; i <= to; i++) if (peak < 0 || v[i] > v[peak]) peak = i;
                if (peak < 0 || v[peak] < AirKickSpeed)
                {
                    report.AppendLine($"  {limb}: no airborne kick (peak under {AirKickSpeed} m/s).");
                    continue;
                }

                float half = v[peak] * 0.5f;
                int l = peak, r = peak;
                while (l > from && v[l - 1] >= half) l--;
                while (r < to && v[r + 1] >= half) r++;
                var s = new Strike { Limb = limb, Start = Mathf.Max(0, l - 2) * dt, End = (r + 1) * dt, PeakTime = peak * dt };
                Add(p, report, limb == StrikeLimb.RightFoot ? "Right kick" : "Left kick", StrikeLimbs.ToMask(limb), s, length);
                report.AppendLine($"     (airborne foot peak {v[peak]:0} m/s)");
            }

            var slam = new Strike { Start = landing - SlamHalfWidth, End = landing + SlamHalfWidth, PeakTime = landing };
            Add(p, report, "Slam", StrikeLimbMask.BothHands, slam, length);
        }

        // --- Shared -------------------------------------------------------------------------------------

        private static string HandName(StrikeLimb limb) => limb == StrikeLimb.RightHand ? "Right punch" : "Left punch";

        private static void Add(Proposal p, StringBuilder report, string name, StrikeLimbMask limbs, Strike s, float length)
        {
            float start = Mathf.Clamp01(s.Start / length), end = Mathf.Clamp01(s.End / length);
            p.Windows.Add(new HitWindow(name, start, end, limbs));
            report.AppendLine($"  → {name} ({limbs}): {s.Start:0.00}-{s.End:0.00}s");
        }

        // Samples the clip onto the character: per limb, tip extension in front of the hips (along the starting
        // facing), tip height, and on-screen speed relative to the hips. Also returns ground height (lowest foot).
        private static Dictionary<StrikeLimb, Track> Sample(Animator animator, AnimationClip clip, float speed, out float dt, out float ground)
        {
            dt = 1f / SampleRate;
            ground = float.MaxValue;
            var tracks = new Dictionary<StrikeLimb, Track>();
            var previous = new Dictionary<StrikeLimb, Vector3>();
            Dictionary<StrikeLimb, StrikeShape> shapes = CharacterStrikeProfile.ResolveShapes(
                animator, animator.GetComponentInParent<CharacterStrikeProfile>()?.AuthoredShapes);
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform root = animator.transform.root;
            Vector3 facing = new Vector3(root.forward.x, 0f, root.forward.z).normalized;
            foreach (StrikeLimb limb in shapes.Keys) tracks[limb] = new Track();

            bool wasInMode = AnimationMode.InAnimationMode();
            if (!wasInMode) AnimationMode.StartAnimationMode();
            try
            {
                for (float t = 0f; t <= clip.length + 1e-4f; t += dt)
                {
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(animator.gameObject, clip, t);
                    AnimationMode.EndSampling();
                    foreach (KeyValuePair<StrikeLimb, StrikeShape> pair in shapes)
                    {
                        CharacterStrikeProfile.TryResolvePose(animator, pair.Value, out _, out Vector3 tip);
                        Vector3 rel = tip - hips.position;
                        Track track = tracks[pair.Key];
                        track.Extension.Add(Vector3.Dot(rel, facing));
                        track.Height.Add(tip.y);
                        track.Speed.Add(previous.TryGetValue(pair.Key, out Vector3 prev) ? (rel - prev).magnitude / dt * speed : 0f);
                        track.Reach.Add(new Vector2(rel.x, rel.z).magnitude);
                        float yaw = Vector3.SignedAngle(facing, new Vector3(rel.x, 0f, rel.z), Vector3.up);
                        track.Yaw.Add(track.Yaw.Count == 0 ? yaw : track.Yaw[track.Yaw.Count - 1] + Mathf.DeltaAngle(track.Yaw[track.Yaw.Count - 1], yaw));
                        previous[pair.Key] = rel;
                        if (pair.Key == StrikeLimb.RightFoot || pair.Key == StrikeLimb.LeftFoot) ground = Mathf.Min(ground, tip.y);
                    }
                }
            }
            finally
            {
                if (!wasInMode) AnimationMode.StopAnimationMode();
            }

            return tracks;
        }

        /// <summary>Leap takeoff / landing / apex (clip seconds) from the humanoid root height; false if not a leap.</summary>
        public static bool TryMeasureLeap(AnimationClip clip, out float takeoff, out float landing, out float apex)
        {
            takeoff = landing = apex = 0f;
            AnimationCurve y = FindRootCurve(clip, "RootT.y");
            if (y == null)
            {
                return false;
            }

            const float step = 1f / 120f;
            float baseY = y.Evaluate(0f), apexTime = 0f;
            for (float t = 0f; t <= clip.length; t += step)
            {
                float dy = y.Evaluate(t) - baseY;
                if (dy > apex) { apex = dy; apexTime = t; }
            }

            if (apex < MinApex)
            {
                return false;
            }

            for (float t = apexTime; t >= 0f; t -= step)
            {
                if (y.Evaluate(t) - baseY <= 0.05f) { takeoff = t; break; }
            }

            // Touchdown: back within LandTolerance of the start height, or — for clips that land into a crouch or a
            // kneel above it — the bottom of the fall (the root stops dropping after losing most of its height).
            landing = clip.length;
            for (float t = apexTime; t <= clip.length; t += step)
            {
                float dy = y.Evaluate(t) - baseY;
                bool settled = dy <= LandTolerance;
                bool bottomedOut = apex - dy >= apex * 0.6f && y.Evaluate(t + step) - baseY > dy + 1e-4f;
                if (settled || bottomedOut) { landing = t; break; }
            }

            return true;
        }

        /// <summary>Last clip time at which the humanoid root is still travelling (3D) faster than a small threshold.</summary>
        public static float FindRootMotionEnd(AnimationClip clip)
        {
            AnimationCurve x = FindRootCurve(clip, "RootT.x"), y = FindRootCurve(clip, "RootT.y"), z = FindRootCurve(clip, "RootT.z");
            if (x == null || z == null)
            {
                return clip.length * 0.6f;
            }

            const float step = 1f / 60f;
            float last = 0f;
            Vector3 prev = Root(x, y, z, 0f);
            for (float t = step; t <= clip.length; t += step)
            {
                Vector3 p = Root(x, y, z, t);
                if ((p - prev).magnitude / step > RootMovingSpeed) last = t;
                prev = p;
            }

            return last;
        }

        private static Vector3 Root(AnimationCurve x, AnimationCurve y, AnimationCurve z, float t) =>
            new Vector3(x.Evaluate(t), y != null ? y.Evaluate(t) : 0f, z.Evaluate(t));

        private static AnimationCurve FindRootCurve(AnimationClip clip, string property)
        {
            foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(clip))
            {
                if (b.propertyName == property) return AnimationUtility.GetEditorCurve(clip, b);
            }

            return null;
        }
    }
}
