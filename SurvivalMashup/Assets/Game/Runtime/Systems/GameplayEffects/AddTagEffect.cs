using ToyChest.Systems.Tags;
using UnityEngine;

namespace ToyChest.Systems.GameplayEffects
{
    /// <summary>
    /// Atomic effect: add one source claim on a tag to the target's tag container.
    /// The claim follows the Tag System's counted ownership rules; pair with
    /// <see cref="RemoveTagEffect"/> from the same invoker to release it.
    /// </summary>
    [CreateAssetMenu(menuName = "ToyChest/Effects/Add Tag", fileName = "Fx_AddTag")]
    public sealed class AddTagEffect : GameplayEffectDefinition
    {
        [SerializeField]
        private TagDefinition _tag;

        /// <inheritdoc />
        protected override void Execute(in EffectContext context)
        {
            GameplayTagContainer tags = Require(context.Target.Tags, "target");
            tags.AddTag(context.TagTable.GetTag(_tag.TagPath));
        }
    }
}
