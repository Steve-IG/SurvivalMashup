using System;
using System.Collections.Generic;
using ToyChest.Gameplay.HitDetection;
using UnityEngine;

namespace ToyChest.Gameplay.Combat
{
    /// <summary>
    /// One authored attack in an ordered <see cref="ComboDefinition"/>: the data describing a single step,
    /// composed entirely from the pieces the attack-authoring pipeline already established — an existing
    /// ability, a reusable <see cref="HitVolume"/> (preset or inline), a socket/bone anchor, an animation
    /// trigger, and contact/continuation timing. It carries <b>no attack-specific behaviour</b>: whether
    /// the step is a punch, a kick, or an area-of-effect ground slam is entirely a function of which
    /// ability and which hit volume it references, so the combo can represent any of them without the
    /// sequencer or driver knowing the difference.
    ///
    /// <para>Each step keeps its own ability (its own damage, effects, and — resolved through the Ability
    /// System's Multiple Targets path — its own one-or-many targeting), its own hit-volume configuration,
    /// and its own animation with its own <c>OnAttackContact</c> event. The combo only orders them.</para>
    /// </summary>
    [Serializable]
    public sealed class ComboStep
    {
        [SerializeField]
        [Tooltip("Definition id of the ability activated at this step's contact frame. Each step keeps its own ability.")]
        private string _attackAbilityId = "ability.player_strike";

        [Header("Hit region")]
        [SerializeField]
        [Tooltip("Shared hit-volume preset for this step's primary contact region. Overrides the inline shape when set.")]
        private HitVolumeAsset _volumePreset;

        [SerializeField]
        [Tooltip("Inline hit-volume shape used when no preset is assigned.")]
        private HitVolume _inlineVolume = new HitVolume(HitShape.Cone, 2.2f, 60f, multiTarget: true, maxTargets: 3);

        [SerializeField]
        [Tooltip("Humanoid bone this step's hit volume originates from (RightHand for a punch, a foot for a kick).")]
        private HumanBodyBones _anchorBone = HumanBodyBones.RightHand;

        [SerializeField]
        [Tooltip("Local offset from the anchor bone (e.g. +Z to reach past the fist/foot).")]
        private Vector3 _anchorOffset = new Vector3(0f, 0f, 0.1f);

        [SerializeField]
        [Tooltip("Additional simultaneous hit regions for a multi-volume step (e.g. a two-fist smash). Empty for a simple step.")]
        private HitVolumeEmitter[] _extraVolumes;

        [Header("Attack timing asset (preferred)")]
        [SerializeField]
        [Tooltip("Authored attack: limb hit windows + commitment end at normalized clip times. When set, the step's " +
                 "damage comes from these windows (swept limbs / areas, one hit instance per window) and its timing " +
                 "is read from the Animator — the hit region above and the clip's animation events are not used.")]
        private AttackDefinition _attack;

        [SerializeField]
        [Tooltip("Animator state whose normalized time drives the attack asset. Empty = the animator trigger name.")]
        private string _animatorState;

        [Header("Presentation")]
        [SerializeField]
        [Tooltip("Animator trigger fired when this step starts. Distinct triggers select distinct attack clips.")]
        private string _animatorTrigger = "Attack";

        [Header("Timing (tuned to this step's animation clip)")]
        [SerializeField]
        [Min(0f)]
        [Tooltip("Anticipation before the blow lands (press → contact frame), in seconds. A fallback: an authored " +
                 "OnAttackContact event lands it exactly.")]
        private float _windUpDuration = 0.3f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Fallback committed follow-through after the blow, in seconds — used only when the clip has no " +
                 "OnAttackEnd event (which ends the commitment at the moment the clip's motion stops).")]
        private float _recoveryDuration = 0.18f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Optional grace after this attack's commitment ends (its OnAttackEnd) during which a press still " +
                 "continues to the next step. 0 (the default) closes the combo exactly at OnAttackEnd; presses during " +
                 "the attack itself always buffer, so the window is the whole committed attack. Ignored on the final step.")]
        private float _continuationWindow;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds the player stays committed after this step's contact before movement returns. Keep small.")]
        private float _postImpactCommit = 0.05f;

        /// <summary>Definition id of this step's ability.</summary>
        public string AttackAbilityId => _attackAbilityId;

        /// <summary>Animator trigger for this step's clip; never empty (defaults to "Attack").</summary>
        public string AnimatorTrigger => string.IsNullOrEmpty(_animatorTrigger) ? "Attack" : _animatorTrigger;

        /// <summary>The authored attack timing asset, or null for a step driven by its hit region and clip events.</summary>
        public AttackDefinition Attack => _attack;

        /// <summary>The Animator state whose time drives <see cref="Attack"/> (defaults to the trigger name).</summary>
        public string AnimatorState => string.IsNullOrEmpty(_animatorState) ? AnimatorTrigger : _animatorState;

        /// <summary>Wind-up seconds (press → contact frame).</summary>
        public float WindUpDuration => _windUpDuration;

        /// <summary>Seconds committed after contact before movement returns.</summary>
        public float PostImpactCommit => _postImpactCommit;

        /// <summary>The pure timing the sequencer needs: wind-up, recovery, continuation window.</summary>
        public ComboStepTiming Timing => new ComboStepTiming(_windUpDuration, _recoveryDuration, _continuationWindow);

        /// <summary>Additional simultaneous hit regions, or null.</summary>
        public IReadOnlyList<HitVolumeEmitter> ExtraVolumes => _extraVolumes;

        /// <summary>
        /// Builds this step's primary hit-volume emitter, taking the attacker's faction filter (tag +
        /// layers) from the driver so the step authors only geometry and placement, never the faction —
        /// the same separation the shared hit-volume presets rely on.
        /// </summary>
        public HitVolumeEmitter BuildPrimaryEmitter(string targetTag, LayerMask layers)
        {
            HitVolume volume = _volumePreset != null ? _volumePreset.Volume : _inlineVolume;
            var anchor = new HitVolumeAnchor(HitAnchorSpace.HumanoidBone, _anchorBone, _anchorOffset, HitFacing.Owner);
            return new HitVolumeEmitter(volume, anchor, targetTag, layers);
        }
    }
}
