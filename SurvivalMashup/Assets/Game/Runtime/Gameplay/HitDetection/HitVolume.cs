using System;
using UnityEngine;

namespace ToyChest.Gameplay.HitDetection
{
    /// <summary>
    /// The authored, reusable <em>shape</em> of a hit test — the canonical answer to
    /// <em>"what region does this action sweep?"</em> It is pure geometry: a shape, a reach, an optional
    /// frontal arc, and how many results to keep. It deliberately carries <b>no faction filter</b> (tag,
    /// layers) and <b>no anchor</b> (socket, offset): the filter is the attacker's concern (a
    /// <see cref="HitFilter"/> supplied at query time) and the anchor is the attack's placement concern
    /// (a <see cref="HitVolumeAnchor"/>). Keeping those out is what lets a single "Sword Slash Wide" or
    /// "Explosion Large" volume be reused by the player, an enemy, and a boss with no duplicated data.
    ///
    /// Authored inline on a thin adapter, or — the production path — shared as a <see cref="HitVolumeAsset"/>
    /// preset referenced by many attacks. Consumed by <see cref="HitDetector"/> and tested through
    /// <see cref="HitQuery"/>.
    /// </summary>
    [Serializable]
    public struct HitVolume
    {
        [SerializeField]
        [Tooltip("Geometry of the test. Sphere = omnidirectional (explosion, hazard, aura); Cone = frontal arc (melee).")]
        private HitShape _shape;

        [SerializeField]
        [Tooltip("Reach in world units. Broad-phase radius: nothing beyond this can ever be hit.")]
        private float _radius;

        [SerializeField]
        [Range(0f, 180f)]
        [Tooltip("Cone only: half-angle of the frontal arc, in degrees. 60 ≈ a 120° swing. Ignored for Sphere.")]
        private float _coneHalfAngleDegrees;

        [SerializeField]
        [Tooltip("False: keep only the nearest hit (single-target strike). True: keep every hit in the volume (cleave, explosion).")]
        private bool _multiTarget;

        [SerializeField]
        [Tooltip("Multi-target cap. The N nearest hits are kept, so a big AoE cannot resolve an unbounded number.")]
        private int _maxTargets;

        /// <summary>Builds a volume in code (used by tests, presets, and adapters that compose one at runtime).</summary>
        public HitVolume(HitShape shape, float radius, float coneHalfAngleDegrees, bool multiTarget, int maxTargets)
        {
            _shape = shape;
            _radius = radius;
            _coneHalfAngleDegrees = coneHalfAngleDegrees;
            _multiTarget = multiTarget;
            _maxTargets = maxTargets;
        }

        /// <summary>Geometry of the test.</summary>
        public HitShape Shape => _shape;

        /// <summary>Reach in world units; also the broad-phase overlap radius.</summary>
        public float Radius => _radius;

        /// <summary>Cone half-angle in degrees; only meaningful for <see cref="HitShape.Cone"/>.</summary>
        public float ConeHalfAngleDegrees => _coneHalfAngleDegrees;

        /// <summary>Whether every hit in the volume is kept (true) or only the nearest (false).</summary>
        public bool MultiTarget => _multiTarget;

        /// <summary>Maximum hits kept when <see cref="MultiTarget"/> is true (at least one).</summary>
        public int MaxTargets => Mathf.Max(1, _maxTargets);
    }
}
