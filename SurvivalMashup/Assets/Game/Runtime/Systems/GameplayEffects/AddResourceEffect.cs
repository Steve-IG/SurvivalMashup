using ToyChest.Framework.Data;
using ToyChest.Systems.Resources;
using UnityEngine;

namespace ToyChest.Systems.GameplayEffects
{
    /// <summary>
    /// Atomic effect: add to a target resource (gain mana, generate rage, grant ammo).
    /// Neutral economy operation with no combat semantics.
    /// </summary>
    [CreateAssetMenu(menuName = "ToyChest/Effects/Add Resource", fileName = "Fx_AddResource")]
    public sealed class AddResourceEffect : GameplayEffectDefinition
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
            resource.Restore(_amount);
        }
    }
}
