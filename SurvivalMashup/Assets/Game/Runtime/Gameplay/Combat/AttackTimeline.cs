using System.Collections.Generic;
using UnityEngine;

namespace ToyChest.Gameplay.Combat
{
    /// <summary>What crossed during one <see cref="AttackTimeline.Advance"/>.</summary>
    public readonly struct AttackTimelineTick
    {
        /// <summary>
        /// The attack's strikes are complete this frame — its last hit window has closed. This is the step's
        /// "contact" for combo purposes: a queued continuation may chain from here, so pressing ahead never cuts
        /// a blow off before its windows have had their chance (windows strike from their own start regardless).
        /// </summary>
        public bool ContactReached { get; }

        /// <summary>The attack's committed motion ended this frame (its <see cref="AttackDefinition.CommitEnd"/>).</summary>
        public bool CommitEnded { get; }

        public AttackTimelineTick(bool contactReached, bool commitEnded)
        {
            ContactReached = contactReached;
            CommitEnded = commitEnded;
        }
    }

    /// <summary>
    /// Pure, scene-free reading of an <see cref="AttackDefinition"/> against the attack clip's live normalized
    /// time. Each frame the driver passes the clip's current normalized time; the timeline reports what was
    /// crossed since the last frame (contact, commitment end) and which windows were open during that span.
    /// Working on spans rather than instants means a low frame rate can never skip a short window or event.
    /// </summary>
    public sealed class AttackTimeline
    {
        private readonly AttackDefinition _attack;
        private float _previous;
        private bool _contactFired;
        private bool _endFired;

        public AttackTimeline(AttackDefinition attack)
        {
            _attack = attack;
            Restart();
        }

        /// <summary>Normalized time reached by the last <see cref="Advance"/>.</summary>
        public float Current { get; private set; }

        /// <summary>Normalized time at the start of the last advanced span.</summary>
        public float Previous => _previous;

        /// <summary>
        /// Whether the attack has reached its cancel point (<see cref="AttackDefinition.CancelFrom"/>): its strikes
        /// are done and the remaining recovery may be cut short by movement or a new attack. Never before the last
        /// window has closed, so cancelling can't cut a blow off.
        /// </summary>
        public bool IsCancelable =>
            _attack != null && _attack.CancelFrom > 0f &&
            Current >= Mathf.Max(_attack.CancelFrom, _attack.LastContactEnd);

        /// <summary>Rewinds to the clip's start (a step began).</summary>
        public void Restart()
        {
            _previous = 0f;
            Current = 0f;
            _contactFired = false;
            _endFired = false;
        }

        /// <summary>
        /// Moves the timeline to <paramref name="normalizedTime"/> (time never runs backwards within a step)
        /// and reports what was crossed in (previous, now].
        /// </summary>
        public AttackTimelineTick Advance(float normalizedTime)
        {
            _previous = Current;
            Current = Mathf.Max(Current, normalizedTime);

            bool contact = false;
            float strikesDone = _attack != null ? _attack.LastContactEnd : -1f;
            if (!_contactFired && strikesDone >= 0f && Current >= strikesDone)
            {
                _contactFired = true;
                contact = true;
            }

            bool end = false;
            float commitEnd = _attack != null ? _attack.CommitEnd : 0f;
            if (!_endFired && commitEnd > 0f && Current >= commitEnd)
            {
                _endFired = true;
                end = true;
            }

            return new AttackTimelineTick(contact, end);
        }

        /// <summary>Indices of the windows open at any point in the last advanced span, into <paramref name="into"/>.</summary>
        public void OpenWindows(List<int> into)
        {
            into.Clear();
            if (_attack == null)
            {
                return;
            }

            IReadOnlyList<HitWindow> windows = _attack.Windows;
            for (int i = 0; i < windows.Count; i++)
            {
                if (windows[i].OverlapsSpan(_previous, Current))
                {
                    into.Add(i);
                }
            }
        }
    }

    /// <summary>
    /// The re-hit rule for hit windows. By default a window strikes each target <b>once</b> (one hit instance);
    /// a window with a re-hit interval may strike the same target again once that many seconds have passed.
    /// Separate windows never share a record, which is what makes "once per kick, once for the slam" work.
    /// Pure and scene-free; target identity is any reference (the struck GameplayObject at runtime).
    /// </summary>
    public sealed class HitWindowLedger
    {
        private readonly Dictionary<(int window, object target), float> _lastHit =
            new Dictionary<(int window, object target), float>();

        /// <summary>Forgets every hit (a new step began).</summary>
        public void Clear() => _lastHit.Clear();

        /// <summary>Whether window <paramref name="window"/> may strike <paramref name="target"/> at <paramref name="now"/>.</summary>
        public bool CanStrike(int window, object target, float now, float rehitInterval)
        {
            if (!_lastHit.TryGetValue((window, target), out float last))
            {
                return true;
            }

            return rehitInterval > 0f && now - last >= rehitInterval;
        }

        /// <summary>Records that window <paramref name="window"/> struck <paramref name="target"/> at <paramref name="now"/>.</summary>
        public void Record(int window, object target, float now) => _lastHit[(window, target)] = now;
    }
}
