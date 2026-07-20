using System;
using System.Collections.Generic;
using ToyChest.Framework.Objects;
using UnityEngine;

namespace ToyChest.Gameplay.HitDetection
{
    /// <summary>
    /// One authored hit region of an attack: a reusable <see cref="HitVolume"/> shape (a shared
    /// <see cref="HitVolumeAsset"/> preset, or an inline shape), placed by a <see cref="HitVolumeAnchor"/>
    /// (socket/bone + offset) and aimed by a <see cref="HitFilter"/> (faction tag + layers). This is the
    /// unit the whole authoring workflow assembles: pick a preset, pick a socket, pick a filter — no code.
    ///
    /// It composes naturally in two directions the brief requires, with no new system:
    /// <list type="bullet">
    /// <item><b>Multi-volume</b> — an attack authors an array of emitters (a dragon's bite + two claws, a
    /// two-handed smash covering two regions); resolving the attack resolves each emitter.</item>
    /// <item><b>Multi-hit</b> — an animation fires several contact events; each event re-resolves the same
    /// emitter(s). The emitter holds no per-swing state, so repeated resolution is clean and, sharing the
    /// caller's <see cref="HitDetector"/>, allocation-free.</item>
    /// </list>
    ///
    /// It owns no gameplay outcome: it returns <see cref="HitResult"/>s; the caller activates the authored
    /// ability against them. Serializable so it is authored inline on a thin adapter; the detector is owned
    /// by the caller and passed in, so one detector serves many emitters without garbage.
    /// </summary>
    [Serializable]
    public sealed class HitVolumeEmitter
    {
        [SerializeField]
        [Tooltip("Shared shape preset. When set, it overrides the inline shape below — the reuse path.")]
        private HitVolumeAsset _preset;

        [SerializeField]
        [Tooltip("Inline shape used when no preset is assigned (quick authoring, tests).")]
        private HitVolume _inlineVolume = new HitVolume(HitShape.Cone, 2.5f, 70f, multiTarget: false, maxTargets: 1);

        [SerializeField]
        [Tooltip("Where this region originates and which way it faces (socket/bone + offset).")]
        private HitVolumeAnchor _anchor = new HitVolumeAnchor();

        [SerializeField]
        [Tooltip("Unity tag a target's collider must carry for this attacker (Enemy, Player). Empty = any.")]
        private string _targetTag;

        [SerializeField]
        [Tooltip("Physics layers this region searches.")]
        private LayerMask _layers = ~0;

        /// <summary>Builds an emitter in code (adapters that compose a default region at runtime, tests).</summary>
        public HitVolumeEmitter(HitVolume volume, HitVolumeAnchor anchor, string targetTag, LayerMask layers)
        {
            _preset = null;
            _inlineVolume = volume;
            _anchor = anchor ?? new HitVolumeAnchor();
            _targetTag = targetTag;
            _layers = layers;
        }

        /// <summary>Parameterless constructor so Unity can serialize/author this type.</summary>
        public HitVolumeEmitter()
        {
        }

        /// <summary>The resolved shape — the shared preset when assigned, otherwise the inline shape.</summary>
        public HitVolume Volume => _preset != null ? _preset.Volume : _inlineVolume;

        /// <summary>The attacker-side filter (tag + layers).</summary>
        public HitFilter Filter => new HitFilter(_targetTag, _layers);

        /// <summary>Binds this region's anchor to the owner and model animator; call once at startup.</summary>
        public void Bind(Transform owner, Animator animator)
        {
            _anchor.Bind(owner, animator);
        }

        /// <summary>
        /// Resolves this region right now into the objects it contains, using the caller's shared
        /// <paramref name="detector"/>. <paramref name="self"/> is never returned. The returned list is the
        /// detector's reused buffer — consume it before the next call.
        /// </summary>
        public IReadOnlyList<HitResult> Detect(GameplayObject self, HitDetector detector)
        {
            return detector.Detect(Volume, Filter, _anchor.Origin, _anchor.Forward, self);
        }
    }
}
