using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToyChest.Gameplay.HitDetection
{
    /// <summary>How a strike shape finds its two endpoints.</summary>
    public enum StrikeShapeSource
    {
        /// <summary>Two humanoid bones (any humanoid rig, no per-model setup).</summary>
        HumanoidBones,

        /// <summary>Two explicitly assigned transforms (a weapon's hilt and tip, a non-humanoid limb).</summary>
        Sockets,
    }

    /// <summary>
    /// One limb's strike shape: a capsule along the segment start→end (extended past the end by
    /// <see cref="EndExtension"/> to reach the fist, toe, or blade tip), with a radius.
    /// </summary>
    [Serializable]
    public sealed class StrikeShape
    {
        [SerializeField] private StrikeLimb _limb;
        [SerializeField] private StrikeShapeSource _source;
        [SerializeField] private HumanBodyBones _startBone;
        [SerializeField] private HumanBodyBones _endBone;
        [SerializeField] private Transform _startSocket;
        [SerializeField] private Transform _endSocket;

        [SerializeField]
        [Tooltip("Metres the capsule extends past the end bone/socket along the limb (fist, toes, blade tip).")]
        private float _endExtension;

        [SerializeField]
        [Tooltip("Capsule radius in metres — the limb's striking thickness.")]
        private float _radius = 0.1f;

        public StrikeShape()
        {
        }

        public StrikeShape(StrikeLimb limb, HumanBodyBones start, HumanBodyBones end, float endExtension, float radius)
        {
            _limb = limb;
            _source = StrikeShapeSource.HumanoidBones;
            _startBone = start;
            _endBone = end;
            _endExtension = endExtension;
            _radius = radius;
        }

        public StrikeLimb Limb => _limb;
        public StrikeShapeSource Source => _source;
        public HumanBodyBones StartBone => _startBone;
        public HumanBodyBones EndBone => _endBone;
        public Transform StartSocket => _startSocket;
        public Transform EndSocket => _endSocket;
        public float EndExtension => _endExtension;
        public float Radius => _radius;
    }

    /// <summary>
    /// A character's strike profile: one swept capsule per limb that can deliver a blow (both hands, both
    /// feet, the head, weapons). Attacks name limbs by role (<see cref="StrikeLimb"/>), so an attack authored
    /// once works on every character whose profile provides those limbs. With no shapes authored, the profile
    /// <b>generates them from the humanoid avatar</b> (forearm→fist, shin→toe, neck→head), so any humanoid is
    /// combat-ready with zero setup; authored shapes (or a weapon's sockets) override per limb.
    ///
    /// <para>It records each limb's pose once per frame, keeping the previous frame's too, so a hit window
    /// can test the swept motion rather than a single pose (<see cref="HitDetector.DetectLimbSweep"/>). It
    /// owns no gameplay: it answers "where is this limb, and where was it?" and nothing else.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterStrikeProfile : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Limb strike shapes. Leave empty to generate them from the humanoid avatar at runtime; " +
                 "add entries (or use Weapon sockets) to override specific limbs.")]
        private List<StrikeShape> _shapes = new List<StrikeShape>();

        [SerializeField]
        [Tooltip("Animator whose humanoid bones the shapes follow. Defaults to the first one in children.")]
        private Animator _animator;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Extra metres added to every limb's radius at runtime — the feel knob. Limb hitboxes slightly larger " +
                 "than the visual limb make grazing contacts count, as in most action games; 0 = exactly the limb.")]
        private float _reachPadding = 0.1f;

        private readonly LimbState[] _limbs = new LimbState[StrikeLimbs.Count];
        private bool _bound;
        private int _capturedFrame = -1;

        /// <summary>The humanoid defaults: what an empty profile generates. Exposed for tooling and tests.</summary>
        public static List<StrikeShape> DefaultHumanoidShapes()
        {
            return new List<StrikeShape>
            {
                new StrikeShape(StrikeLimb.RightHand, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 0.12f, 0.09f),
                new StrikeShape(StrikeLimb.LeftHand, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 0.12f, 0.09f),
                new StrikeShape(StrikeLimb.RightFoot, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, 0.16f, 0.10f),
                new StrikeShape(StrikeLimb.LeftFoot, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, 0.16f, 0.10f),
                new StrikeShape(StrikeLimb.Head, HumanBodyBones.Neck, HumanBodyBones.Head, 0.14f, 0.12f),
            };
        }

        /// <summary>The shapes authored on this profile (overrides for the generated humanoid defaults).</summary>
        public IReadOnlyList<StrikeShape> AuthoredShapes => _shapes;

        /// <summary>
        /// The effective shape per limb for a character: the humanoid defaults (when the rig is humanoid)
        /// overridden by any authored shapes. Works in edit mode — used by the authoring tool's preview.
        /// </summary>
        public static Dictionary<StrikeLimb, StrikeShape> ResolveShapes(Animator animator, IReadOnlyList<StrikeShape> authored)
        {
            var map = new Dictionary<StrikeLimb, StrikeShape>();
            if (animator != null && animator.isHuman)
            {
                foreach (StrikeShape s in DefaultHumanoidShapes()) map[s.Limb] = s;
            }

            if (authored != null)
            {
                foreach (StrikeShape s in authored)
                {
                    if (s != null) map[s.Limb] = s;
                }
            }

            return map;
        }

        /// <summary>
        /// A shape's capsule endpoints in the character's current pose (no frame bookkeeping). Works in edit
        /// mode — the authoring tool samples a clip onto a character and draws the result.
        /// </summary>
        public static bool TryResolvePose(Animator animator, StrikeShape shape, out Vector3 a, out Vector3 b)
        {
            a = b = Vector3.zero;
            Transform start = null, end = null;
            if (shape.Source == StrikeShapeSource.Sockets)
            {
                start = shape.StartSocket;
                end = shape.EndSocket;
            }
            else if (animator != null && animator.isHuman)
            {
                start = animator.GetBoneTransform(shape.StartBone);
                end = animator.GetBoneTransform(shape.EndBone);
            }

            if (start == null || end == null)
            {
                return false;
            }

            a = start.position;
            Vector3 dir = end.position - a;
            b = dir.sqrMagnitude > 1e-8f ? end.position + dir.normalized * shape.EndExtension : end.position;
            return true;
        }

        /// <summary>Whether this profile provides a usable shape for <paramref name="limb"/>.</summary>
        public bool HasLimb(StrikeLimb limb)
        {
            EnsureBound();
            return _limbs[(int)limb].Valid;
        }

        /// <summary>
        /// The limb's capsule this frame and last frame (sweep endpoints) and its radius. Returns false if the
        /// profile has no usable shape for that limb.
        /// </summary>
        public bool TryGetSweep(
            StrikeLimb limb, out Vector3 prevA, out Vector3 prevB, out Vector3 curA, out Vector3 curB, out float radius)
        {
            EnsureCaptured();
            LimbState s = _limbs[(int)limb];
            prevA = s.PrevA;
            prevB = s.PrevB;
            curA = s.CurA;
            curB = s.CurB;
            radius = s.Radius;
            return s.Valid;
        }

        /// <summary>
        /// Records every limb's pose for this frame (once per frame, whoever asks first), keeping last frame's
        /// as the sweep start. Call after animation has evaluated (LateUpdate).
        /// </summary>
        public void EnsureCaptured()
        {
            EnsureBound();
            int frame = Time.frameCount;
            if (_capturedFrame == frame)
            {
                return;
            }

            bool first = _capturedFrame < 0;
            _capturedFrame = frame;
            for (int i = 0; i < _limbs.Length; i++)
            {
                LimbState s = _limbs[i];
                if (!s.Valid)
                {
                    continue;
                }

                ResolveEndpoints(s, out Vector3 a, out Vector3 b);
                s.PrevA = first ? a : s.CurA;
                s.PrevB = first ? b : s.CurB;
                s.CurA = a;
                s.CurB = b;
                _limbs[i] = s;
            }
        }

        private void Awake()
        {
            EnsureBound();
        }

        // Keep last frame's pose current even on frames nobody sweeps, so a window opening mid-motion has a
        // true previous pose rather than a stale one.
        private void LateUpdate()
        {
            EnsureCaptured();
        }

        private void EnsureBound()
        {
            if (_bound)
            {
                return;
            }

            _bound = true;
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            bool humanoid = _animator != null && _animator.isHuman;
            if (humanoid)
            {
                foreach (StrikeShape shape in DefaultHumanoidShapes())
                {
                    Bind(shape);
                }
            }

            // Authored shapes override the generated defaults limb by limb.
            if (_shapes != null)
            {
                foreach (StrikeShape shape in _shapes)
                {
                    if (shape != null)
                    {
                        Bind(shape);
                    }
                }
            }
        }

        private void Bind(StrikeShape shape)
        {
            Transform start = null, end = null;
            if (shape.Source == StrikeShapeSource.Sockets)
            {
                start = shape.StartSocket;
                end = shape.EndSocket;
            }
            else if (_animator != null && _animator.isHuman)
            {
                start = _animator.GetBoneTransform(shape.StartBone);
                end = _animator.GetBoneTransform(shape.EndBone);
            }

            _limbs[(int)shape.Limb] = new LimbState
            {
                Valid = start != null && end != null,
                Start = start,
                End = end,
                Extension = shape.EndExtension,
                Radius = Mathf.Max(0.01f, shape.Radius) + _reachPadding,
            };
        }

        private static void ResolveEndpoints(LimbState s, out Vector3 a, out Vector3 b)
        {
            a = s.Start.position;
            Vector3 end = s.End.position;
            Vector3 dir = end - a;
            b = dir.sqrMagnitude > 1e-8f ? end + dir.normalized * s.Extension : end;
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.8f);
            for (int i = 0; i < _limbs.Length; i++)
            {
                if (_limbs[i].Valid)
                {
                    Gizmos.DrawLine(_limbs[i].CurA, _limbs[i].CurB);
                    Gizmos.DrawWireSphere(_limbs[i].CurB, _limbs[i].Radius);
                }
            }
        }

        private struct LimbState
        {
            public bool Valid;
            public Transform Start;
            public Transform End;
            public float Extension;
            public float Radius;
            public Vector3 PrevA;
            public Vector3 PrevB;
            public Vector3 CurA;
            public Vector3 CurB;
        }
    }
}
