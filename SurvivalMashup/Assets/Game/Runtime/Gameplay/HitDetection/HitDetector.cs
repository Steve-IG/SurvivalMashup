using System;
using System.Collections.Generic;
using ToyChest.Framework.Objects;
using UnityEngine;

namespace ToyChest.Gameplay.HitDetection
{
    /// <summary>
    /// The one place gameplay asks "which GameplayObjects does this <see cref="HitVolume"/> contain right
    /// now?" — the canonical hit-detection probe every attack, projectile, explosion, and hazard shares
    /// instead of each inventing its own overlap-and-filter loop. It is a thin Unity adapter over
    /// <see cref="Physics"/>: a broad-phase overlap by reach, a tag/self/liveness filter, the pure
    /// <see cref="HitQuery"/> narrow phase (facing/arc), and a deterministic nearest-first ordering
    /// (<see cref="HitResult.Compare"/>). It owns no gameplay rules and applies no effects — it returns
    /// <see cref="HitResult"/>s and stops. What happens to the hits is entirely the caller's authored
    /// ability and Gameplay Effects.
    ///
    /// One detector is held per adapter and reused, so discovery is <b>allocation-free per query</b> (a
    /// pooled collider buffer, reused result lists, and a cached comparer — no per-call delegate). This
    /// lets an animation fire many contact windows (multi-hit) and an attack carry many volumes
    /// (multi-volume) without generating garbage. Not a manager, not a registry, not ticked: a stateless
    /// query object the caller invokes at the moment of impact (an animation contact event) or on a
    /// hazard's periodic tick.
    /// </summary>
    public sealed class HitDetector
    {
        // Cached so Sort allocates no per-call comparison delegate (verified by an allocation test).
        private static readonly Comparison<HitResult> Order = HitResult.Compare;

        private readonly Collider[] _overlap;
        private readonly List<HitResult> _results = new List<HitResult>();
        private readonly List<HitResult> _sorted = new List<HitResult>();

        /// <summary>
        /// Creates a detector with a pooled overlap buffer. <paramref name="maxCandidates"/> bounds how
        /// many colliders a single broad-phase query inspects, keeping discovery allocation-free.
        /// </summary>
        public HitDetector(int maxCandidates = 16)
        {
            _overlap = new Collider[Mathf.Max(1, maxCandidates)];
        }

        /// <summary>
        /// Resolves the volume placed at <paramref name="origin"/> facing <paramref name="forward"/>,
        /// filtered by <paramref name="filter"/>, into the GameplayObjects it contains, nearest first.
        /// <paramref name="self"/> (the attacker) is never returned. Single-target volumes yield at most
        /// one result; multi-target volumes yield up to <see cref="HitVolume.MaxTargets"/>. The returned
        /// list is owned and reused by this detector — consume it before the next query.
        /// </summary>
        public IReadOnlyList<HitResult> Detect(
            in HitVolume volume, in HitFilter filter, Vector3 origin, Vector3 forward, GameplayObject self)
        {
            _results.Clear();
            _sorted.Clear();

            int count = Physics.OverlapSphereNonAlloc(
                origin, volume.Radius, _overlap, filter.Layers, QueryTriggerInteraction.Collide);

            bool filterByTag = !string.IsNullOrEmpty(filter.TargetTag);

            for (int i = 0; i < count; i++)
            {
                Collider collider = _overlap[i];
                if (collider == null)
                {
                    continue;
                }

                if (filterByTag && !collider.CompareTag(filter.TargetTag))
                {
                    continue;
                }

                var behaviour = collider.GetComponentInParent<GameplayObjectBehaviour>();
                GameplayObject candidate = behaviour != null ? behaviour.Object : null;
                if (candidate == null || candidate == self || !candidate.IsActive)
                {
                    continue;
                }

                Vector3 targetPosition = collider.transform.position;
                if (!HitQuery.Contains(volume, origin, forward, targetPosition))
                {
                    continue;
                }

                if (AlreadyFound(candidate))
                {
                    continue;
                }

                // Containment and ordering use the stable transform position (deterministic), but the
                // reported contact point is the real point on the target's surface nearest the query
                // origin — i.e. where the fist/blade actually meets the body. Impact presentation reads
                // this, so VFX land on the contact rather than at an approximated body offset.
                Vector3 contactPoint = ClosestSurfacePoint(collider, origin);
                float sqrDistance = (targetPosition - origin).sqrMagnitude;
                _results.Add(new HitResult(candidate, contactPoint, sqrDistance));
            }

            // Deterministic nearest-first order so resolution never depends on physics query order.
            _results.Sort(Order);

            int keep = volume.MultiTarget ? Mathf.Min(volume.MaxTargets, _results.Count) : Mathf.Min(1, _results.Count);
            for (int i = 0; i < keep; i++)
            {
                _sorted.Add(_results[i]);
            }

            return _sorted;
        }

        // The point on the collider's surface nearest the query origin. Collider.ClosestPoint is exact for
        // primitives and CharacterControllers (what characters use); a non-convex MeshCollider does not
        // support it, so those fall back to the bounding box, which is still far closer than the object's
        // pivot.
        private static Vector3 ClosestSurfacePoint(Collider collider, Vector3 origin)
        {
            if (collider is MeshCollider mesh && !mesh.convex)
            {
                return collider.bounds.ClosestPoint(origin);
            }

            return collider.ClosestPoint(origin);
        }

        // A GameplayObject can carry several colliders; count each object once.
        private bool AlreadyFound(GameplayObject candidate)
        {
            for (int i = 0; i < _results.Count; i++)
            {
                if (_results[i].Object == candidate)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
