using UnityEngine;

namespace ToyChest.Gameplay.HitDetection
{
    /// <summary>
    /// The pure geometry of a <em>swept limb</em>: a capsule (segment + radius) that moved from last frame's
    /// pose to this frame's. A fast kick can travel farther than its own thickness in one frame and skip
    /// straight past a target if only its current pose is tested; sweeping sub-samples the motion so every
    /// position the limb passed through is covered. Separated from physics so it is testable without a scene
    /// (Engine Principle 17); <see cref="HitDetector.DetectLimbSweep"/> runs one physics capsule overlap per
    /// sample produced here.
    /// </summary>
    public static class LimbSweep
    {
        /// <summary>Upper bound on sub-samples per sweep, bounding cost for very fast limbs.</summary>
        public const int MaxSamples = 12;

        /// <summary>
        /// How many intermediate capsules cover the motion from (<paramref name="prevA"/>,<paramref name="prevB"/>)
        /// to (<paramref name="curA"/>,<paramref name="curB"/>) without gaps: enough that consecutive samples
        /// overlap (step ≤ ¾ of the radius), at least 1 (the current pose), at most <see cref="MaxSamples"/>.
        /// </summary>
        public static int SampleCount(Vector3 prevA, Vector3 prevB, Vector3 curA, Vector3 curB, float radius)
        {
            float travel = Mathf.Max((curA - prevA).magnitude, (curB - prevB).magnitude);
            float step = Mathf.Max(0.01f, radius * 0.75f);
            return Mathf.Clamp(Mathf.CeilToInt(travel / step), 1, MaxSamples);
        }

        /// <summary>
        /// Sample <paramref name="index"/> (1..<paramref name="count"/>) of the sweep: the segment endpoints at
        /// that fraction of the way from last frame's pose to this frame's. Sample <paramref name="count"/> is
        /// exactly the current pose.
        /// </summary>
        public static void Sample(
            Vector3 prevA, Vector3 prevB, Vector3 curA, Vector3 curB, int index, int count,
            out Vector3 a, out Vector3 b)
        {
            float t = count <= 0 ? 1f : Mathf.Clamp01(index / (float)count);
            a = Vector3.Lerp(prevA, curA, t);
            b = Vector3.Lerp(prevB, curB, t);
        }

        /// <summary>Shortest distance from <paramref name="point"/> to the segment <paramref name="a"/>–<paramref name="b"/>.</summary>
        public static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            return (point - ClosestPointOnSegment(point, a, b)).magnitude;
        }

        /// <summary>The point on segment <paramref name="a"/>–<paramref name="b"/> nearest <paramref name="point"/>.</summary>
        public static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            if (lengthSq < 1e-8f)
            {
                return a;
            }

            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSq);
            return a + ab * t;
        }

        /// <summary>
        /// Whether a sphere of <paramref name="targetRadius"/> at <paramref name="point"/> touches the limb
        /// capsule anywhere along its sweep. The pure equivalent of the physics query, used by tests.
        /// </summary>
        public static bool SweepTouches(
            Vector3 prevA, Vector3 prevB, Vector3 curA, Vector3 curB, float radius,
            Vector3 point, float targetRadius)
        {
            int count = SampleCount(prevA, prevB, curA, curB, radius);
            float reach = radius + targetRadius;
            for (int i = 1; i <= count; i++)
            {
                Sample(prevA, prevB, curA, curB, i, count, out Vector3 a, out Vector3 b);
                if (DistanceToSegment(point, a, b) <= reach)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
