using ToyChest.Framework.Data;
using UnityEngine;

namespace ToyChest.Systems.Resources
{
    /// <summary>
    /// Immutable authoring definition of one resource (Current Health, Mana, Energy, Ammo,
    /// Durability, ...). The engine treats every resource identically; unique behavior comes
    /// from configuration. A resource's maximum and regeneration may be literal, or bound to
    /// an attribute so the maximum tracks that attribute — Current Health binds to a
    /// Maximum Health attribute. Runtime state lives on <see cref="ResourceValue"/>.
    /// See Docs/Systems/RESOURCE_SYSTEM.md.
    /// </summary>
    [CreateAssetMenu(menuName = "ToyChest/Resources/Resource Definition", fileName = "Res_")]
    public sealed class ResourceDefinition : GameplayDefinition
    {
        [SerializeField]
        private string _displayName;

        [Header("Maximum")]
        [SerializeField]
        [Tooltip("Literal maximum used when no maximum attribute is bound.")]
        private float _literalMaximum = 100f;

        [SerializeField]
        [Tooltip("Optional attribute id whose value provides the maximum. When set, it overrides the literal maximum.")]
        private string _maximumAttributeId;

        [Header("Starting Value")]
        [SerializeField]
        [Tooltip("If true, the resource starts full at its maximum; otherwise it starts at StartingValue.")]
        private bool _startAtMaximum = true;

        [SerializeField]
        private float _startingValue;

        [Header("Regeneration")]
        [SerializeField]
        [Tooltip("Units restored per second. Zero means no regeneration.")]
        private float _regenerationPerSecond;

        /// <summary>Designer-facing display name.</summary>
        public string DisplayName => _displayName;

        /// <summary>Maximum used when <see cref="HasMaximumBinding"/> is false.</summary>
        public float LiteralMaximum => _literalMaximum;

        /// <summary>Whether the maximum is bound to an attribute rather than literal.</summary>
        public bool HasMaximumBinding => !string.IsNullOrWhiteSpace(_maximumAttributeId);

        /// <summary>The attribute id supplying the maximum. Valid only when <see cref="HasMaximumBinding"/>.</summary>
        public DefinitionId MaximumAttribute => new DefinitionId(_maximumAttributeId);

        /// <summary>Whether the resource starts at its maximum.</summary>
        public bool StartAtMaximum => _startAtMaximum;

        /// <summary>Starting value used when <see cref="StartAtMaximum"/> is false.</summary>
        public float StartingValue => _startingValue;

        /// <summary>Units restored per second by continuous regeneration.</summary>
        public float RegenerationPerSecond => _regenerationPerSecond;
    }
}
