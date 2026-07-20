using NUnit.Framework;
using ToyChest.Gameplay.HitDetection;
using UnityEngine;

namespace ToyChest.Tests.HitDetection
{
    /// <summary>
    /// Verifies the pure socket/bone-anchoring math with no scene: a volume placed on an anchor rides that
    /// anchor's pose, an authored local offset is applied in the anchor's local space (so the same offset
    /// reused across characters lands correctly on each rig), and the same offset on two different anchors
    /// resolves to two different world origins — the basis of character-independent reuse.
    /// </summary>
    public sealed class HitAnchorTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void ResolveOrigin_NoOffset_IsAnchorPosition()
        {
            Vector3 anchor = new Vector3(3f, 1f, 4f);

            Vector3 origin = HitAnchor.ResolveOrigin(anchor, Quaternion.identity, Vector3.zero);

            Assert.AreEqual(anchor, origin);
        }

        [Test]
        public void ResolveOrigin_AppliesOffsetInAnchorLocalSpace()
        {
            // Anchor rotated 90° about Y: its local +Z points along world +X, so a "+Z forward" offset
            // reaches past the fist in the direction the hand faces — not a fixed world axis.
            Quaternion rot = Quaternion.Euler(0f, 90f, 0f);

            Vector3 origin = HitAnchor.ResolveOrigin(Vector3.zero, rot, new Vector3(0f, 0f, 1f));

            Assert.AreEqual(1f, origin.x, Tolerance);
            Assert.AreEqual(0f, origin.z, Tolerance);
        }

        [Test]
        public void ResolveOrigin_SameOffsetDifferentAnchors_ProducesDifferentOrigins()
        {
            // One authored "+5cm forward, +10cm up" reused by two characters whose hands are in different
            // places resolves to each character's own hand — this is what lets one attack serve many rigs.
            Vector3 offset = new Vector3(0f, 0.1f, 0.05f);

            Vector3 a = HitAnchor.ResolveOrigin(new Vector3(0f, 1.2f, 0f), Quaternion.identity, offset);
            Vector3 b = HitAnchor.ResolveOrigin(new Vector3(10f, 1.8f, 5f), Quaternion.identity, offset);

            Assert.Greater((a - b).magnitude, 1f, "Two characters' hands resolve to clearly different origins.");
            Assert.AreEqual(1.3f, a.y, Tolerance);
            Assert.AreEqual(0.05f, a.z, Tolerance);
            Assert.AreEqual(10f, b.x, Tolerance);
            Assert.AreEqual(1.9f, b.y, Tolerance);
            Assert.AreEqual(5.05f, b.z, Tolerance);
        }

        [Test]
        public void ResolveForward_IsAnchorForward()
        {
            Quaternion rot = Quaternion.Euler(0f, 90f, 0f);

            Vector3 forward = HitAnchor.ResolveForward(rot);

            Assert.AreEqual(1f, forward.x, Tolerance);
            Assert.AreEqual(0f, forward.z, Tolerance);
        }
    }
}
