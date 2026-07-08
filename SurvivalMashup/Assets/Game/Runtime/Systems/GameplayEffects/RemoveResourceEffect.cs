using ToyChest.Framework.Data;
using ToyChest.Systems.Resources;
using UnityEngine;

namespace ToyChest.Systems.GameplayEffects
{
    /// <summary>
    /// Atomic effect: remove from a target resource (spend mana, drain energy, consume ammo).
    /// Neutral economy operation with no combat semantics.
    /// </summary>
    [CreateAssetMenu(menuName = "ToyChest/Effects/Remove Resource", fileName = "Fx_RemoveResource")]
    public sealed class RemoveResourceEffect : GameplayEffectDefinition
    {
        [SerializeField]
        private string _resourceId;

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
