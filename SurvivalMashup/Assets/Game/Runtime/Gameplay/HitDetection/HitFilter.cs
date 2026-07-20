using UnityEngine;

namespace ToyChest.Gameplay.HitDetection
{
    /// <summary>
    /// The attacker-side half of a hit query: <em>which candidates count?</em> — a Unity tag the target's
    /// collider must carry (Enemy, Player, Destructible) and the physics layers to search. Kept separate
    /// from the reusable <see cref="HitVolume"/> shape so the same shape preset ("Sword Slash Wide") can be
    /// aimed at enemies by the player and at the player by an enemy with no duplicated geometry. Authored
    /// per attacker on the thin adapter, supplied to <see cref="HitDetector.Detect"/> at query time.
    /// </summary>
    public readonly struct HitFilter
    {
        /// <summary>Optional Unity tag a candidate's collider must carry; null/empty means any tag qualifies.</summary>
        public readonly string TargetTag;

        /// <summary>Physics layers the broad-phase overlap searches.</summary>
        public readonly LayerMask Layers;

        /// <summary>Builds a filter.</summary>
        public HitFilter(string targetTag, LayerMask layers)
        {
            TargetTag = targetTag;
            Layers = layers;
        }

        /// <summary>A filter that accepts any object on any layer.</summary>
        public static HitFilter Any => new HitFilter(null, ~0);
    }
}
