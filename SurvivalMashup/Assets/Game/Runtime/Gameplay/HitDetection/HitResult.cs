using System;
using ToyChest.Framework.Objects;
using UnityEngine;

namespace ToyChest.Gameplay.HitDetection
{
    /// <summary>
    /// One GameplayObject a <see cref="HitVolume"/> found, plus where the blow reads. This is the
    /// architectural hinge: hit detection produces <see cref="HitResult"/>s ("what was hit"); the caller
    /// turns each into an <c>EffectTarget</c> and activates an authored ability ("what happens"). The two
    /// responsibilities never mix — a HitResult carries no damage, no effect, no rule, only the object and
    /// the geometry of the contact.
    /// </summary>
    public readonly struct HitResult
    {
        /// <summary>The live GameplayObject inside the volume — the participant an ability will affect.</summary>
        public readonly GameplayObject Object;

        /// <summary>World-space point where the hit reads, for impact presentation (VFX, decals, camera).</summary>
        public readonly Vector3 ContactPoint;

        /// <summary>Squared distance from the query origin to the target, for nearest-first ordering.</summary>
        public readonly float SqrDistance;

        /// <summary>Builds a hit result.</summary>
        public HitResult(GameplayObject gameplayObject, Vector3 contactPoint, float sqrDistance)
        {
            Object = gameplayObject;
            ContactPoint = contactPoint;
            SqrDistance = sqrDistance;
        }

        /// <summary>
        /// Deterministic hit ordering: nearest first, ties broken by ordinal id — the same contract
        /// <c>PlayerInteractor</c> uses for interactables, so multi-target resolution never depends on
        /// physics query order (Engine Principle 17). Exposed for tests that verify ordering without a
        /// physics scene.
        /// </summary>
        public static int Compare(HitResult a, HitResult b)
        {
            int byDistance = a.SqrDistance.CompareTo(b.SqrDistance);
            if (byDistance != 0)
            {
                return byDistance;
            }

            string aId = a.Object != null ? a.Object.Id.ToString() : string.Empty;
            string bId = b.Object != null ? b.Object.Id.ToString() : string.Empty;
            return string.CompareOrdinal(aId, bId);
        }
    }
}
