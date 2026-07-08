using ToyChest.Framework.Data;
using ToyChest.Framework.Modifiers;
using ToyChest.Systems.Attributes;
using UnityEngine;

namespace ToyChest.Systems.GameplayEffects
{
    /// <summary>
    /// Atomic effect: apply one attribute modifier to the target, registered under the
    /// context's modifier source so the invoker can revoke exactly its own contribution
    /// (a status expiring, equipment unequipped). With no modifier source in the context,
    /// the modifier is registered under this definition and is effectively permanent.
    /// </summary>
    [CreateAssetMenu(menuName = "ToyChest/Effects/Apply Modifier", fileName = "Fx_ApplyModifier")]
    public sealed class ApplyModifierEffect : GameplayEffectDefinition
    {
        [SerializeField]
        [Tooltip("The attribute modified, e.g. attribute.move-speed.")]
        private string _attributeId;

        [SerializeField]
        private ModifierOperation _operation = ModifierOperation.Flat;

        [SerializeField]
        [Tooltip("Magnitude per the operation: flat amount, fraction for percentages (0.2 = 20%), or override value.")]
        private float _value;

        /// <inheritdoc />
        protected override void Execute(in EffectContext context)
        {
            AttributeSet attributes = Require(context.Target.Attributes, "target");
            AttributeValue attribute = Require(
                attributes.GetAttribute(new DefinitionId(_attributeId)), $"target attribute '{_attributeId}'");

            object source = context.ModifierSource ?? this;
            attribute.AddModifier(new Modifier(_operation, _value, source));
        }
    }
}
