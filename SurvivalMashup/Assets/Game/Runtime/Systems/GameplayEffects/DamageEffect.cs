using ToyChest.Framework.Data;
using ToyChest.Systems.Resources;
using UnityEngine;

namespace ToyChest.Systems.GameplayEffects
{
    /// <summary>
    /// Atomic effect: reduce a target resource as damage. In Milestone 0 this is a direct
    /// resource reduction; when the Damage System arrives, this effect routes through it for
    /// resistances and mitigation without changing authored content. Kept distinct from
    /// <see cref="RemoveResourceEffect"/> because damage will gain combat semantics that
    /// neutral resource removal never will.
    /// </summary>
    [CreateAssetMenu(menuName = "ToyChest/Effects/Damage", fileName = "Fx_Damage")]
    public sealed class DamageEffect : GameplayEffectDefinition
    {
        [SerializeField]
        [Tooltip("The resource damaged, e.g. resource.health.")]
        private string _resourceId = "resource.health";

        [SerializeField]
        [Min(0f)]
        private float _amount;

        /// <inheritdoc />
        protected override void Execute(in EffectContext context)
        {
            ResourceSet resources = Require(context.Target.Resources, "target");
            ResourceValue resource = Require(resources.GetResource(new DefinitionId(_resourceId)), $"target resource '{_resourceId}'");
            resource.Consume(_amount);
        }
    }
}
