using System.Collections.Generic;
using ToyChest.Framework.Data;
using ToyChest.Systems.GameplayEffects;
using ToyChest.Systems.Tags;
using UnityEngine;

namespace ToyChest.Systems.Abilities
{
    /// <summary>
    /// Immutable authoring definition of one ability (Fireball, Dash, Harvest). An ability is
    /// pure orchestration data (Engine Principle 24): activation gates, costs, a cooldown, a
    /// target mode, and the Gameplay Effect sequence that answers "what happens". The engine
    /// never needs a Fireball.cs — Fireball is one of these assets, usable by any actor type.
    /// Gameplay mutations live entirely in the referenced effects; the Ability System answers
    /// only when and why they run. See Docs/Systems/ABILITY_SYSTEM.md.
    /// </summary>
    [CreateAssetMenu(menuName = "ToyChest/Abilities/Ability Definition", fileName = "Ability_")]
    public sealed class AbilityDefinition : GameplayDefinition
    {
        [SerializeField]
        private string _displayName;

        [SerializeField]
        [Tooltip("Organizational category: Combat, Movement, Utility, Companion, Ultimate, Interaction. New categories are data, not code.")]
        private string _category;

        [SerializeField]
        [Tooltip("Descriptive tags (Fire, Projectile, Healing). Tags drive interactions, not inheritance.")]
        private List<TagDefinition> _tags = new List<TagDefinition>();

        [Header("Activation")]
        [SerializeField]
        private AbilityTargetMode _targetMode = AbilityTargetMode.Self;

        [SerializeField]
        [Tooltip("Owner tags that must all be present (hierarchical match) to activate. Empty = no requirement.")]
        private List<TagDefinition> _requiredOwnerTags = new List<TagDefinition>();

        [SerializeField]
        [Tooltip("Owner tags any of which block activation (State.Stunned, State.Silenced). Empty = never blocked.")]
        private List<TagDefinition> _blockedByOwnerTags = new List<TagDefinition>();

        [Header("Costs")]
        [SerializeField]
        [Tooltip("Resource costs consumed on activation. All-or-nothing: activation fails unless every cost is payable in full.")]
        private List<ResourceCostConfig> _costs = new List<ResourceCostConfig>();

        [Header("Cooldown")]
        [SerializeField]
        [Min(0f)]
        [Tooltip("Fixed cooldown in seconds after activation. Zero = no cooldown.")]
        private float _cooldownSeconds;

        [Header("Effects")]
        [SerializeField]
        [Tooltip("Gameplay Effect sequence executed in order against the resolved target on activation.")]
        private List<GameplayEffectDefinition> _effects = new List<GameplayEffectDefinition>();

        /// <summary>Designer-facing display name.</summary>
        public string DisplayName => _displayName;

        /// <summary>Organizational category. Organizational only; never affects behavior.</summary>
        public AbilityCategory Category => AbilityCategory.FromAuthored(_category);

        /// <summary>Descriptive tags driving interactions and AI metadata.</summary>
        public IReadOnlyList<TagDefinition> Tags => _tags;

        /// <summary>How the ability resolves its target.</summary>
        public AbilityTargetMode TargetMode => _targetMode;

        /// <summary>Owner tags that must all be present to activate.</summary>
        public IReadOnlyList<TagDefinition> RequiredOwnerTags => _requiredOwnerTags;

        /// <summary>Owner tags any of which block activation.</summary>
        public IReadOnlyList<TagDefinition> BlockedByOwnerTags => _blockedByOwnerTags;

        /// <summary>Resource costs consumed on activation, all-or-nothing.</summary>
        public IReadOnlyList<ResourceCostConfig> Costs => _costs;

        /// <summary>Fixed cooldown in seconds; zero disables the cooldown.</summary>
        public float CooldownSeconds => _cooldownSeconds;

        /// <summary>Effects executed on activation.</summary>
        public IReadOnlyList<GameplayEffectDefinition> Effects => _effects;
    }
}
