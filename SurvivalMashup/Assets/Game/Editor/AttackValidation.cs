using System.Collections.Generic;
using System.Text;
using ToyChest.Gameplay.Combat;
using ToyChest.Gameplay.HitDetection;
using UnityEditor.Animations;
using UnityEngine;

namespace ToyChest.Editor
{
    /// <summary>
    /// Checks authored attack data for the mistakes that otherwise only show up in play — a window that can
    /// never fire, a limb the character doesn't have, a commitment that ends mid-strike, a combo step whose
    /// Animator state is missing or has no way back to locomotion — and summarises an attack's separate timing
    /// concepts (startup, contact, chain point, cancel point, recovery, commitment end, clip end) so it is
    /// obvious which are configured. Shared by the Attack Authoring window and the EditMode content tests, so
    /// the tool and CI enforce the same rules. Editor-only; reads data, changes nothing.
    /// </summary>
    public static class AttackValidation
    {
        public enum Severity
        {
            Info,
            Warning,
            Error,
        }

        public readonly struct Issue
        {
            public Issue(Severity severity, string message)
            {
                Severity = severity;
                Message = message;
            }

            public Severity Severity { get; }
            public string Message { get; }

            public override string ToString() => $"{Severity}: {Message}";
        }

        /// <summary>
        /// Checks one attack asset. <paramref name="availableLimbs"/> (optional) is the set of limbs the target
        /// character's strike profile provides; windows naming any other limb are errors.
        /// </summary>
        public static List<Issue> ValidateAttack(AttackDefinition attack, ICollection<StrikeLimb> availableLimbs = null)
        {
            var issues = new List<Issue>();
            if (attack == null)
            {
                issues.Add(new Issue(Severity.Error, "No attack asset."));
                return issues;
            }

            if (attack.Clip == null)
            {
                issues.Add(new Issue(Severity.Error, "No clip assigned — the attack cannot be previewed or proposed."));
            }
            else if (!attack.Clip.isHumanMotion)
            {
                issues.Add(new Issue(Severity.Warning, $"Clip '{attack.Clip.name}' is not humanoid motion; limb windows assume a humanoid rig."));
            }

            IReadOnlyList<HitWindow> windows = attack.Windows;
            if (windows.Count == 0)
            {
                issues.Add(new Issue(Severity.Error, "No hit windows — this attack can never hit anything."));
            }

            var ids = new HashSet<string>();
            int unverified = 0;
            for (int i = 0; i < windows.Count; i++)
            {
                HitWindow w = windows[i];
                string label = $"Window '{w.Id}'";
                if (w.End <= w.Start)
                {
                    issues.Add(new Issue(Severity.Error, $"{label} ends ({w.End:0.000}) at or before it starts ({w.Start:0.000}) — it never opens."));
                }

                if (w.Limbs == StrikeLimbMask.None && w.Area == null)
                {
                    issues.Add(new Issue(Severity.Error, $"{label} names no limbs and no area — it cannot strike."));
                }

                if (availableLimbs != null)
                {
                    for (int limb = 0; limb < StrikeLimbs.Count; limb++)
                    {
                        if (StrikeLimbs.Contains(w.Limbs, (StrikeLimb)limb) && !availableLimbs.Contains((StrikeLimb)limb))
                        {
                            issues.Add(new Issue(Severity.Error,
                                $"{label} uses {(StrikeLimb)limb}, which the character's strike profile does not provide (author a shape/socket for it)."));
                        }
                    }
                }

                if (!ids.Add(w.Id ?? string.Empty))
                {
                    issues.Add(new Issue(Severity.Warning, $"Two windows are named '{w.Id}' — debug output (WindowStruck) cannot tell them apart."));
                }

                if (!w.IsVerified)
                {
                    unverified++;
                }
            }

            float lastEnd = attack.LastContactEnd;
            if (attack.CommitEnd <= 0f)
            {
                issues.Add(new Issue(Severity.Warning, "No commitment end — the combo step's recovery timer decides when the attack releases."));
            }
            else if (windows.Count > 0 && attack.CommitEnd < lastEnd)
            {
                issues.Add(new Issue(Severity.Error,
                    $"Commitment ends ({attack.CommitEnd:0.00}) before the last window closes ({lastEnd:0.00}) — windows after it never strike."));
            }

            if (attack.CancelFrom > 0f)
            {
                if (attack.CancelFrom < lastEnd)
                {
                    issues.Add(new Issue(Severity.Warning,
                        $"Cancel point ({attack.CancelFrom:0.00}) is before the last window closes ({lastEnd:0.00}); runtime holds it until {lastEnd:0.00}."));
                }

                if (attack.CommitEnd > 0f && attack.CancelFrom >= attack.CommitEnd)
                {
                    issues.Add(new Issue(Severity.Warning, "Cancel point is at/after the commitment end — it has no effect."));
                }
            }

            if (unverified > 0)
            {
                issues.Add(new Issue(Severity.Warning,
                    $"{unverified} of {windows.Count} window(s) are unverified suggestions — scrub/playtest them and tick Verified."));
            }

            return issues;
        }

        /// <summary>
        /// Checks one combo step against the Animator controller it plays on: its attack asset, its state (exists,
        /// plays the attack's clip, entered by its trigger, leaves on <c>!AttackCommitted</c>) and its ability.
        /// </summary>
        public static List<Issue> ValidateStep(ComboStep step, AnimatorController controller, ICollection<StrikeLimb> availableLimbs = null)
        {
            var issues = new List<Issue>();
            if (step == null)
            {
                issues.Add(new Issue(Severity.Error, "Empty step."));
                return issues;
            }

            if (string.IsNullOrEmpty(step.AttackAbilityId))
            {
                issues.Add(new Issue(Severity.Error, "Step has no ability id."));
            }

            if (step.Attack == null)
            {
                issues.Add(new Issue(Severity.Info, "Step has no attack asset — it uses its legacy hit region and clip events."));
            }
            else
            {
                issues.AddRange(ValidateAttack(step.Attack, availableLimbs));
            }

            if (controller == null)
            {
                return issues;
            }

            AnimatorState state = FindState(controller, step.AnimatorState);
            if (state == null)
            {
                issues.Add(new Issue(Severity.Error, $"Animator state '{step.AnimatorState}' not found — its windows will never fire."));
                return issues;
            }

            if (step.Attack != null && step.Attack.Clip != null && state.motion != step.Attack.Clip)
            {
                issues.Add(new Issue(Severity.Error,
                    $"State '{state.name}' plays '{(state.motion != null ? state.motion.name : "nothing")}', not the attack's clip '{step.Attack.Clip.name}' — window timing would be read from the wrong animation."));
            }

            if (!HasParameter(controller, step.AnimatorTrigger, AnimatorControllerParameterType.Trigger))
            {
                issues.Add(new Issue(Severity.Error, $"Animator has no trigger '{step.AnimatorTrigger}'."));
            }
            else if (!HasAnyStateEntry(controller, state, step.AnimatorTrigger))
            {
                issues.Add(new Issue(Severity.Error, $"No Any State → '{state.name}' transition on trigger '{step.AnimatorTrigger}' — the clip never plays."));
            }

            if (!HasCommittedExit(state))
            {
                issues.Add(new Issue(Severity.Error,
                    $"State '{state.name}' has no exit on AttackCommitted == false — the character would hold the clip (or slide) after the attack releases."));
            }

            return issues;
        }

        /// <summary>Checks every step of a combo, plus ordering rules between steps.</summary>
        public static List<Issue> ValidateCombo(ComboDefinition combo, AnimatorController controller, ICollection<StrikeLimb> availableLimbs = null)
        {
            var issues = new List<Issue>();
            if (combo == null || combo.StepCount == 0)
            {
                issues.Add(new Issue(Severity.Error, "Combo has no steps."));
                return issues;
            }

            for (int i = 0; i < combo.StepCount; i++)
            {
                foreach (Issue issue in ValidateStep(combo.Steps[i], controller, availableLimbs))
                {
                    issues.Add(new Issue(issue.Severity, $"Step {i + 1}: {issue.Message}"));
                }

                // Any State transitions don't re-enter the state already playing, so a step that repeats the previous
                // step's state would never start its clip.
                if (i > 0 && combo.Steps[i].AnimatorState == combo.Steps[i - 1].AnimatorState)
                {
                    issues.Add(new Issue(Severity.Error,
                        $"Step {i + 1} uses the same Animator state as step {i} ('{combo.Steps[i].AnimatorState}') — it cannot restart the clip."));
                }
            }

            return issues;
        }

        /// <summary>
        /// The attack's separate timing concepts, in seconds at <paramref name="stateSpeed"/>: startup (press → first
        /// window), each contact window, the chain point (last window closes — a queued press continues here),
        /// combo input (presses buffer for the whole committed attack), the cancel point, the recovery after
        /// contact, the commitment end and the clip end.
        /// </summary>
        public static string TimingSummary(AttackDefinition attack, float stateSpeed = 1f)
        {
            if (attack == null || attack.Clip == null)
            {
                return "No clip — no timing.";
            }

            float s = attack.Clip.length / Mathf.Max(0.01f, stateSpeed);
            var sb = new StringBuilder();
            IReadOnlyList<HitWindow> windows = attack.Windows;
            if (windows.Count == 0)
            {
                sb.AppendLine("Startup: — (no windows)");
            }
            else
            {
                sb.AppendLine($"Startup (press → first contact): {attack.FirstContact * s:0.00}s");
                foreach (HitWindow w in windows)
                {
                    sb.AppendLine($"Contact '{w.Id}': {w.Start * s:0.00}–{w.End * s:0.00}s ({w.Limbs}{(w.Area != null ? " + area " + w.Area.name : string.Empty)}){(w.IsVerified ? " ✓ verified" : " ? suggested")}");
                }

                sb.AppendLine($"Chain point (queued press continues): {attack.LastContactEnd * s:0.00}s");
            }

            float commit = attack.CommitEnd > 0f ? attack.CommitEnd * s : -1f;
            sb.AppendLine(commit > 0f
                ? $"Combo input: any press from 0.00s to {commit:0.00}s queues the next step"
                : "Combo input: until the step's fallback recovery timer ends");
            sb.AppendLine(attack.CancelFrom > 0f
                ? $"Cancel point (move/attack cuts the tail): {Mathf.Max(attack.CancelFrom, attack.LastContactEnd) * s:0.00}s"
                : "Cancel point: none — committed until the commitment end (roll/jump still cancel)");
            if (commit > 0f && windows.Count > 0)
            {
                sb.AppendLine($"Recovery after contact: {commit - attack.LastContactEnd * s:0.00}s");
            }

            sb.AppendLine(commit > 0f ? $"Commitment end (character released): {commit:0.00}s" : "Commitment end: fallback timer");
            sb.Append($"Clip end: {s:0.00}s{(commit > 0f ? $" (last {s - commit:0.00}s blends out uncommitted)" : string.Empty)}");
            return sb.ToString();
        }

        /// <summary>The limbs a character provides: its humanoid defaults plus any authored shapes/sockets.</summary>
        public static HashSet<StrikeLimb> AvailableLimbs(Animator character)
        {
            var profile = character != null ? character.GetComponentInParent<CharacterStrikeProfile>(true) : null; // true: prefab assets read as inactive
            return new HashSet<StrikeLimb>(CharacterStrikeProfile.ResolveShapes(character, profile != null ? profile.AuthoredShapes : null).Keys);
        }

        public static AnimatorState FindState(AnimatorController controller, string name)
        {
            if (controller == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                foreach (ChildAnimatorState child in layer.stateMachine.states)
                {
                    if (child.state.name == name)
                    {
                        return child.state;
                    }
                }
            }

            return null;
        }

        private static bool HasParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            foreach (AnimatorControllerParameter p in controller.parameters)
            {
                if (p.name == name && p.type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasAnyStateEntry(AnimatorController controller, AnimatorState state, string trigger)
        {
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                foreach (AnimatorStateTransition t in layer.stateMachine.anyStateTransitions)
                {
                    if (t.destinationState != state)
                    {
                        continue;
                    }

                    foreach (AnimatorCondition c in t.conditions)
                    {
                        if (c.mode == AnimatorConditionMode.If && c.parameter == trigger)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool HasCommittedExit(AnimatorState state)
        {
            foreach (AnimatorStateTransition t in state.transitions)
            {
                foreach (AnimatorCondition c in t.conditions)
                {
                    if (c.mode == AnimatorConditionMode.IfNot && c.parameter == "AttackCommitted")
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
