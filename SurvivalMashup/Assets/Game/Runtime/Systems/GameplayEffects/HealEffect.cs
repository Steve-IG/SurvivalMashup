using ToyChest.Framework.Data;
using ToyChest.Systems.Resources;
using UnityEngine;

namespace ToyChest.Systems.GameplayEffects
{
    /// <summary>
    /// Atomic effect: restore a target resource as healing. Distinct from
    /// <see cref="AddResourceEffect"/> because healing will gain combat semantics
    /// (healing effectiveness, anti-heal) that neutral resource gain never will.
    /// </summary>
    [CreateAssetMenu(menuName = "ToyChest/Effects/Heal", fileName = "Fx_Heal")]
    public sealed class HealEffect : GameplayEffectDefinition
    {
        [SerializeField]
        [Tooltip("The resource restored, e.g. resource.health.")]
        private string _resourceId = "resource.health";

        [SerializeField]
        [Min(0f)]
        private float _amount;

        /// <inheritdoc />
        protected override void Execute(in EffectContext context)
        {
            ResourceSet resources = Require(context.Target.Resources, "target");
            ResourceValue resource = Require(resources.GetResource(new DefinitionId(_resourceId)), $"target resource '{_resourceId}'");
            resource.Restore(_amount);
        }
    }
}
