using System;
using System.Collections.Generic;
using ToyChest.Gameplay.HitDetection;
using UnityEngine;

namespace ToyChest.Gameplay.Combat
{
    /// <summary>
    /// One stretch of an attack during which it can hurt: which limbs strike (and/or an authored area such as
    /// a shockwave), from when to when in the clip, what ability applies, and how often one target may be
    /// re-struck. Each window is its own <b>hit instance</b>: it keeps its own record of who it has struck, so
    /// a flip-kick-and-slam authored as three windows hits an enemy up to three times — once per kick, once
    /// for the slam — no matter how many frames each limb spends inside the target.
    /// </summary>
    [Serializable]
    public sealed class HitWindow
    {
        [SerializeField]
        [Tooltip("Name shown in tooling and debug output (\"Left kick\", \"Slam\").")]
        private string _id = "Strike";

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Window opens at this normalized time of the attack clip (0 = first frame, 1 = last).")]
        private float _start;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Window closes at this normalized time of the attack clip.")]
        private float _end = 1f;

        [SerializeField]
        [Tooltip("Limbs whose swept strike shapes are live during the window (from the character's CharacterStrikeProfile).")]
        private StrikeLimbMask _limbs = StrikeLimbMask.RightHand;

        [SerializeField]
        [Tooltip("Optional authored area live during the window as well (a ground-slam shockwave, a spin's radius).")]
        private HitVolumeAsset _area;

        [SerializeField]
        [Tooltip("Humanoid bone the area is anchored to.")]
        private HumanBodyBones _areaBone = HumanBodyBones.Hips;

        [SerializeField]
        [Tooltip("Ability applied to targets this window strikes. Empty = the attack's ability.")]
        private string _abilityId;

        [SerializeField]
        [Min(0f)]
        [Tooltip("0 = each target is struck at most once per window. >0 = a target may be struck again after this " +
                 "many seconds (continuous attacks: spins, grinders, flurries).")]
        private float _rehitInterval;

        [SerializeField]
        [Tooltip("Authoring only: a designer has checked this window against the animation (scrubbed it, watched it " +
                 "land in play). Proposed windows start unverified — a proposal is a suggestion, not proof. Not read at runtime.")]
        private bool _verified;

        public HitWindow()
        {
        }

        public HitWindow(string id, float start, float end, StrikeLimbMask limbs, float rehitInterval = 0f)
        {
            _id = id;
            _start = Mathf.Clamp01(start);
            _end = Mathf.Clamp01(end);
            _limbs = limbs;
            _rehitInterval = Mathf.Max(0f, rehitInterval);
        }

        public string Id => _id;
        public float Start => _start;
        public float End => _end;
        public StrikeLimbMask Limbs => _limbs;
        public HitVolumeAsset Area => _area;
        public HumanBodyBones AreaBone => _areaBone;
        public string AbilityId => _abilityId;
        public float RehitInterval => _rehitInterval;

        /// <summary>Authoring-only: whether a designer has verified this window against the animation.</summary>
        public bool IsVerified => _verified;

        /// <summary>
        /// Whether the window was open at any point in the normalized-time span (<paramref name="from"/>,
        /// <paramref name="to"/>] — so a short window is never skipped when one frame jumps across it.
        /// </summary>
        public bool OverlapsSpan(float from, float to)
        {
            return to >= _start && from < _end && _end > _start;
        }
    }

    /// <summary>
    /// The authored timing of one melee attack, on its own asset: the clip it belongs to, its hit windows, and
    /// where its committed motion ends (the moment the attack releases the character). This replaces per-clip
    /// FBX animation events as the source of truth — times are normalized clip time, read live from the
    /// Animator, so they are immune to the import pitfalls (normalized-vs-seconds, pending trims applying only
    /// on reimport) and are edited and previewed in the Attack Authoring window
    /// (<b>Tools ▸ ToyChest ▸ Attack Authoring</b>). Character-independent: windows name limbs by role, so the
    /// same attack works on any character whose strike profile has those limbs.
    /// Create via <b>Assets ▸ Create ▸ ToyChest ▸ Combat ▸ Attack</b>.
    /// </summary>
    [CreateAssetMenu(menuName = "ToyChest/Combat/Attack", fileName = "Attack_")]
    public sealed class AttackDefinition : ScriptableObject
    {
        [SerializeField]
        [Tooltip("The animation clip this attack's timing is authored against (used by tooling for length and preview).")]
        private AnimationClip _clip;

        [SerializeField]
        [Tooltip("Default ability applied by windows that do not name their own. Empty = the combo step's ability.")]
        private string _abilityId;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Normalized time the committed motion ends (the attack releases the character; replaces OnAttackEnd). " +
                 "0 = use the combo step's recovery timer instead.")]
        private float _commitEnd;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Normalized time from which the rest of the commitment (the recovery tail — e.g. getting back up after a " +
                 "slam) may be cancelled: moving blends straight into locomotion, attacking starts the next combo. " +
                 "0 = not cancellable; the attack holds the character until its commit end.")]
        private float _cancelFrom;

        [SerializeField]
        [Tooltip("The windows during which this attack can hurt, in any order.")]
        private List<HitWindow> _windows = new List<HitWindow>();

        [SerializeField]
        [Range(-1f, 1f)]
        [Tooltip("Authoring only: normalized time of the attack's main impact (-1 = none). The window proposer uses it to " +
                 "decide WHICH limb is the strike when the motion alone is ambiguous (e.g. one hand sweeps forward while " +
                 "the other lands the blow). Set it in the Attack Authoring window. Not read at runtime.")]
        private float _strikeHint = -1f;

        public AnimationClip Clip => _clip;
        public string AbilityId => _abilityId;
        public float CommitEnd => _commitEnd;

        /// <summary>Normalized time the recovery tail becomes cancellable by movement or a new attack (0 = never).</summary>
        public float CancelFrom => _cancelFrom;

        public IReadOnlyList<HitWindow> Windows => _windows;

        /// <summary>Authoring-only strike-frame hint (normalized time, or -1 for none).</summary>
        public float StrikeHint => _strikeHint;

        /// <summary>
        /// The attack's contact: the earliest window start (the step's "blow lands" moment), or -1 with no windows.
        /// </summary>
        public float FirstContact
        {
            get
            {
                float first = float.MaxValue;
                foreach (HitWindow w in _windows)
                {
                    first = Mathf.Min(first, w.Start);
                }

                return _windows.Count > 0 ? first : -1f;
            }
        }

        /// <summary>The latest window end, or -1 with no windows.</summary>
        public float LastContactEnd
        {
            get
            {
                float last = -1f;
                foreach (HitWindow w in _windows)
                {
                    last = Mathf.Max(last, w.End);
                }

                return last;
            }
        }
    }
}
