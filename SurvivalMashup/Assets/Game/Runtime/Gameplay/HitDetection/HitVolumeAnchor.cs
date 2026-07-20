using System;
using UnityEngine;

namespace ToyChest.Gameplay.HitDetection
{
    /// <summary>Where a hit volume originates from.</summary>
    public enum HitAnchorSpace
    {
        /// <summary>The owner Gameplay Object's own transform (the 5A default; body slams, self-centred AoE).</summary>
        Owner,

        /// <summary>A humanoid bone resolved from the model's <see cref="Animator"/> (RightHand, Foot, Head, ...).</summary>
        HumanoidBone,

        /// <summary>An explicitly assigned child transform / socket (WeaponTip, ProjectileOrigin, a mount point).</summary>
        Socket,
    }

    /// <summary>Which direction a volume faces.</summary>
    public enum HitFacing
    {
        /// <summary>Face the owner's forward — stable for melee (a punch tracks where the character aims).</summary>
        Owner,

        /// <summary>Face the anchor's own forward — for weapon tips / muzzles whose orientation is the aim.</summary>
        Anchor,
    }

    /// <summary>
    /// The authored placement of a hit volume: which socket or humanoid bone it rides, a local offset, and
    /// whose forward it faces. This is the second half of the authoring workflow (the first being the
    /// reusable <see cref="HitVolume"/> shape) — it is what turns "Punch" into "Punch <em>from the right
    /// hand, +5cm forward</em>" without positioning a collider by hand. Serializable so it is authored in
    /// the inspector; bound once at startup, after which it reads the live bone pose each query so the
    /// volume follows the animation for free.
    /// </summary>
    [Serializable]
    public sealed class HitVolumeAnchor
    {
        [SerializeField]
        [Tooltip("Where the volume originates: the owner transform, a humanoid bone, or an assigned socket.")]
        private HitAnchorSpace _space = HitAnchorSpace.Owner;

        [SerializeField]
        [Tooltip("Humanoid bone used when Space = HumanoidBone (resolved from the model's Animator).")]
        private HumanBodyBones _bone = HumanBodyBones.RightHand;

        [SerializeField]
        [Tooltip("Explicit socket transform used when Space = Socket (WeaponTip, ProjectileOrigin, ...).")]
        private Transform _socket;

        [SerializeField]
        [Tooltip("Local offset from the anchor, in the anchor's local space (e.g. +Z forward to reach past the fist).")]
        private Vector3 _localOffset;

        [SerializeField]
        [Tooltip("Whose forward the cone faces: the owner (stable for melee) or the anchor (weapon tips / muzzles).")]
        private HitFacing _facing = HitFacing.Owner;

        private Transform _owner;
        private Transform _resolved;

        /// <summary>Builds an anchor in code (used by adapters that compose a default anchor at runtime).</summary>
        public HitVolumeAnchor(HitAnchorSpace space, HumanBodyBones bone, Vector3 localOffset, HitFacing facing)
        {
            _space = space;
            _bone = bone;
            _localOffset = localOffset;
            _facing = facing;
        }

        /// <summary>Parameterless constructor so Unity can serialize/author this type.</summary>
        public HitVolumeAnchor()
        {
        }

        /// <summary>Whether the anchor resolved to a live transform.</summary>
        public bool IsBound => _resolved != null;

        /// <summary>
        /// Resolves the anchor to a concrete transform once, from the owner and (for bones) the model's
        /// <paramref name="animator"/>. Falls back to the owner transform when a requested bone or socket
        /// is unavailable, so a non-humanoid or unrigged object still works (just owner-anchored).
        /// </summary>
        public void Bind(Transform owner, Animator animator)
        {
            _owner = owner;

            switch (_space)
            {
                case HitAnchorSpace.HumanoidBone when animator != null && animator.isHuman:
                    _resolved = animator.GetBoneTransform(_bone);
                    break;
                case HitAnchorSpace.Socket:
                    _resolved = _socket;
                    break;
            }

            if (_resolved == null)
            {
                _resolved = owner; // Owner space, or a graceful fallback for a missing bone/socket.
            }
        }

        /// <summary>The live world origin of the volume (anchor pose + local offset), read each query.</summary>
        public Vector3 Origin => HitAnchor.ResolveOrigin(_resolved.position, _resolved.rotation, _localOffset);

        /// <summary>The live world forward the cone faces, per the authored <see cref="HitFacing"/>.</summary>
        public Vector3 Forward
        {
            get
            {
                Transform source = _facing == HitFacing.Anchor ? _resolved : _owner;
                return source != null ? source.forward : Vector3.forward;
            }
        }
    }
}
