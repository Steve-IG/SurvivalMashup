using System;
using UnityEngine;

namespace ToyChest.Gameplay.Combat
{
    /// <summary>How strongly a body reacts to a blow, from lightest to heaviest.</summary>
    public enum HitReactionLevel
    {
        /// <summary>Shrugged off: damage only — no hit animation, no interrupt (a weak hit on a heavy body).</summary>
        None = 0,

        /// <summary>A flinch: hit animation; interrupts an attack only inside its short early interrupt window.</summary>
        Flinch = 1,

        /// <summary>A stagger: heavy hit animation, interrupts any interruptible wind-up, briefly locks the body.</summary>
        Stagger = 2,

        /// <summary>A stagger plus knockback: the body is driven back along the blow's direction.</summary>
        Knockback = 3,
    }

    /// <summary>The outcome of one blow against one body.</summary>
    public readonly struct HitReaction
    {
        /// <summary>The reaction tier.</summary>
        public readonly HitReactionLevel Level;

        /// <summary>Impact Power ÷ Stability — how far the blow out-weighed the body.</summary>
        public readonly float Ratio;

        /// <summary>Seconds the body is locked out of moving and attacking (0 for None / Flinch).</summary>
        public readonly float StaggerSeconds;

        /// <summary>Metres the body is driven back (0 below Knockback).</summary>
        public readonly float KnockbackDistance;

        public HitReaction(HitReactionLevel level, float ratio, float staggerSeconds, float knockbackDistance)
        {
            Level = level;
            Ratio = ratio;
            StaggerSeconds = staggerSeconds;
            KnockbackDistance = knockbackDistance;
        }

        /// <summary>Whether the body visibly reacts (any tier above None).</summary>
        public bool Reacts => Level != HitReactionLevel.None;

        public override string ToString() => $"{Level} (×{Ratio:0.00}, stagger {StaggerSeconds:0.00}s, knockback {KnockbackDistance:0.00}m)";
    }

    /// <summary>
    /// The authored thresholds that turn a blow's weight into a reaction. Everything is a ratio of the blow's Impact
    /// Power to the body's Stability, so the same rules scale from a jab on a Grunt to a giant's stomp on a knight:
    /// raise a body's Stability (an attribute — statuses and equipment can modify it) and the same blow drops a tier;
    /// raise an attacker's Impact Power and every blow it lands climbs one.
    /// </summary>
    [Serializable]
    public sealed class HitReactionRules
    {
        [SerializeField]
        [Min(0f)]
        [Tooltip("Impact ÷ Stability at or above which the body flinches (hit animation; early-window interrupt).")]
        private float _flinchRatio = 0.5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Impact ÷ Stability at or above which the body staggers (heavy hit animation; interrupts any interruptible wind-up).")]
        private float _staggerRatio = 1.5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Impact ÷ Stability at or above which the body is also knocked back.")]
        private float _knockbackRatio = 2.5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds a staggered body cannot move or attack.")]
        private float _staggerSeconds = 0.45f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Knockback distance, in metres, at exactly the knockback ratio.")]
        private float _knockbackBaseDistance = 1.5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Extra knockback metres per 1.0 of ratio beyond the knockback ratio.")]
        private float _knockbackDistancePerRatio = 1f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Knockback never exceeds this many metres.")]
        private float _maxKnockbackDistance = 5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Extra lock-out seconds a knockback adds on top of the stagger (the body is airborne-ish while sliding back).")]
        private float _knockbackExtraSeconds = 0.25f;

        public HitReactionRules()
        {
        }

        public HitReactionRules(
            float flinchRatio, float staggerRatio, float knockbackRatio, float staggerSeconds,
            float knockbackBaseDistance, float knockbackDistancePerRatio, float maxKnockbackDistance, float knockbackExtraSeconds)
        {
            _flinchRatio = flinchRatio;
            _staggerRatio = staggerRatio;
            _knockbackRatio = knockbackRatio;
            _staggerSeconds = staggerSeconds;
            _knockbackBaseDistance = knockbackBaseDistance;
            _knockbackDistancePerRatio = knockbackDistancePerRatio;
            _maxKnockbackDistance = maxKnockbackDistance;
            _knockbackExtraSeconds = knockbackExtraSeconds;
        }

        public float FlinchRatio => _flinchRatio;
        public float StaggerRatio => _staggerRatio;
        public float KnockbackRatio => _knockbackRatio;
        public float StaggerSeconds => _staggerSeconds;
        public float KnockbackBaseDistance => _knockbackBaseDistance;
        public float KnockbackDistancePerRatio => _knockbackDistancePerRatio;
        public float MaxKnockbackDistance => _maxKnockbackDistance;
        public float KnockbackExtraSeconds => _knockbackExtraSeconds;
    }

    /// <summary>
    /// Pure, deterministic hit-reaction resolution (Engine Principle 17): compares a blow's Impact Power with the
    /// body's Stability under authored <see cref="HitReactionRules"/>. No scene, clock, or randomness.
    /// </summary>
    public static class HitReactionResolver
    {
        /// <summary>Stability is floored here so a zero-Stability body reacts maximally rather than dividing by zero.</summary>
        public const float MinimumStability = 0.01f;

        public static HitReaction Resolve(float impactPower, float stability, HitReactionRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            float ratio = Mathf.Max(0f, impactPower) / Mathf.Max(MinimumStability, stability);
            if (impactPower <= 0f || ratio < rules.FlinchRatio)
            {
                return new HitReaction(HitReactionLevel.None, ratio, 0f, 0f);
            }

            if (ratio < rules.StaggerRatio)
            {
                return new HitReaction(HitReactionLevel.Flinch, ratio, 0f, 0f);
            }

            if (ratio < rules.KnockbackRatio)
            {
                return new HitReaction(HitReactionLevel.Stagger, ratio, rules.StaggerSeconds, 0f);
            }

            float distance = Mathf.Min(
                rules.MaxKnockbackDistance,
                rules.KnockbackBaseDistance + rules.KnockbackDistancePerRatio * (ratio - rules.KnockbackRatio));
            return new HitReaction(HitReactionLevel.Knockback, ratio, rules.StaggerSeconds + rules.KnockbackExtraSeconds, distance);
        }
    }
}
