using System;
using ToyChest.Framework.Modifiers;
using UnityEngine;

namespace ToyChest.Systems.StatusEffects
{
    /// <summary>
    /// Authoring configuration for one attribute modifier contributed by a status effect
    /// (Frozen: -50% Move Speed). Applied when the status activates and revoked when it ends;
    /// with the IncreaseStacks rule, applied once per stack so magnitude scales.
    /// </summary>
    [Serializable]
    public struct AttributeModifierConfig
    {
        [SerializeField]
        [Tooltip("The attribute modified, e.g. attribute.move-speed.")]
        private string _attributeId;

        [SerializeField]
        private ModifierOperation _operation;

        [SerializeField]
        [Tooltip("Magnitude per the operation: flat amount, fraction for percentages (-0.5 = -50%), or override value.")]
        private float _value;

        /// <summary>The attribute this modifier targets.</summary>
        public readonly string AttributeId => _attributeId;

        /// <summary>How the modifier combines.</summary>
        public readonly ModifierOperation Operation => _operation;

        /// <summary>The modifier magnitude.</summary>
        public readonly float Value => _value;

        /// <summary>Creates a config (tests and tooling; designers author through the inspector).</summary>
        public AttributeModifierConfig(string attributeId, ModifierOperation operation, float value)
        {
            _attributeId = attributeId;
            _operation = operation;
            _value = value;
        }
    }
}
