using System.Collections.Generic;
using ToyChest.Framework.Data;
using ToyChest.Systems.GameplayEffects;
using ToyChest.Systems.Tags;
using UnityEngine;

namespace ToyChest.Systems.StatusEffects
{
    /// <summary>
    /// Immutable authoring definition of one status effect (Burning, Frozen, Inspired).
    /// A status is pure composition (Engine Principle 24): granted tags, attribute modifiers,
    /// and Gameplay Effect sequences for apply, periodic ticks, and expiry. The engine never
    /// needs a Burning.cs — Burning is one of these assets. Duration, stacking, and scheduling
    /// are executed by the Status Effect System; gameplay mutations are executed by the
    /// Gameplay Effect System. See Docs/Systems/STATUS_EFFECT_SYSTEM.md.
    /// </summary>
    [CreateAssetMenu(menuName = "ToyChest/Status Effects/Status Effect Definition", fileName = "Status_")]
    public sealed class StatusEffectDefinition : GameplayDefinition
    {
        [SerializeField]
        private string _displayName;

        [Header("Duration")]
        [SerializeField]
        private StatusDurationType _durationType = StatusDurationType.Timed;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Lifetime in seconds. Ignored for Infinite statuses.")]
        private float _durationSeconds = 5f;

        [Header("Stacking")]
        [SerializeField]
        private StatusStackingRule _stackingRule = StatusStackingRule.RefreshDuration;

        [SerializeField]
        [Min(1)]
        [Tooltip("Stack cap for the IncreaseStacks rule. Ignored by other rules.")]
        private int _maximumStacks = 1;

        [Header("Contributions")]
        [SerializeField]
        [Tooltip("Tags granted while the status is active (Burning grants State.Burning).")]
        private List<TagDefinition> _grantedTags = new List<TagDefinition>();

        [SerializeField]
        [Tooltip("Attribute modifiers active while the status is active. Applied per stack under IncreaseStacks.")]
        private List<AttributeModifierConfig> _modifiers = new List<AttributeModifierConfig>();

        [Header("Effect Sequences")]
        [SerializeField]
        [Tooltip("Executed once when the status is first applied.")]
        private List<GameplayEffectDefinition> _onApplyEffects = new List<GameplayEffectDefinition>();

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds between periodic executions. Zero disables periodic effects.")]
        private float _periodSeconds;

        [SerializeField]
        [Tooltip("Executed every period while active (Burning: deal fire damage).")]
        private List<GameplayEffectDefinition> _periodicEffects = new List<GameplayEffectDefinition>();

        [SerializeField]
        [Tooltip("Executed once when the status expires or is removed.")]
        private List<GameplayEffectDefinition> _onEndEffects = new List<GameplayEffectDefinition>();

        /// <summary>Designer-facing display name.</summary>
        public string DisplayName => _displayName;

        /// <summary>How the status's lifetime is measured.</summary>
        public StatusDurationType DurationType => _durationType;

        /// <summary>Lifetime in seconds for Timed statuses.</summary>
        public float DurationSeconds => _durationSeconds;

        /// <summary>Behavior when the status is re-applied while active.</summary>
        public StatusStackingRule StackingRule => _stackingRule;

        /// <summary>Stack cap for the IncreaseStacks rule.</summary>
        public int MaximumStacks => _maximumStacks;

        /// <summary>Tags granted while active.</summary>
        public IReadOnlyList<TagDefinition> GrantedTags => _grantedTags;

        /// <summary>Attribute modifiers active while active.</summary>
        public IReadOnlyList<AttributeModifierConfig> Modifiers => _modifiers;

        /// <summary>Effects executed once on first application.</summary>
        public IReadOnlyList<GameplayEffectDefinition> OnApplyEffects => _onApplyEffects;

        /// <summary>Seconds between periodic executions; zero disables them.</summary>
        public float PeriodSeconds => _periodSeconds;

        /// <summary>Effects executed every period.</summary>
        public IReadOnlyList<GameplayEffectDefinition> PeriodicEffects => _periodicEffects;

        /// <summary>Effects executed once when the status ends.</summary>
        public IReadOnlyList<GameplayEffectDefinition> OnEndEffects => _onEndEffects;
    }
}
