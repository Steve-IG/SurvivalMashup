using ToyChest.Framework.Data;
using UnityEngine;

namespace ToyChest.Systems.Attributes
{
    /// <summary>
    /// Immutable authoring definition of one attribute (Maximum Health, Movement Speed,
    /// Mining Speed, ...). The engine never hardcodes attributes; every attribute is data.
    /// Runtime values and modifiers live on <see cref="AttributeValue"/>, never here.
    /// See Docs/Systems/ATTRIBUTE_SYSTEM.md.
    /// </summary>
    [CreateAssetMenu(menuName = "ToyChest/Attributes/Attribute Definition", fileName = "Attr_")]
    public sealed class AttributeDefinition : GameplayDefinition
    {
        [SerializeField]
        [Tooltip("Designer-facing name, e.g. Maximum Health.")]
        private string _displayName;

        [SerializeField]
        private float _baseValue;

        [SerializeField]
        [Tooltip("Lowest value the attribute may compute to after modifiers.")]
        private float _minimumValue;

        [SerializeField]
        [Tooltip("Highest value the attribute may compute to after modifiers.")]
        private float _maximumValue = float.MaxValue;

        /// <summary>Designer-facing display name.</summary>
        public string DisplayName => _displayName;

        /// <summary>The unmodified base value before any modifier is applied.</summary>
        public float BaseValue => _baseValue;

        /// <summary>Inclusive lower bound the computed value is clamped to.</summary>
        public float MinimumValue => _minimumValue;

        /// <summary>Inclusive upper bound the computed value is clamped to.</summary>
        public float MaximumValue => _maximumValue;
    }
}
