using ToyChest.Systems.Tags;
using UnityEngine;

namespace ToyChest.Systems.GameplayEffects
{
    /// <summary>
    /// Atomic effect: remove one source claim on a tag from the target's tag container.
    /// Per the Tag System's ownership rules, the invoker must be releasing a claim it
    /// (or its composed sequence) added; removing an absent tag fails clearly.
    /// </summary>
    [CreateAssetMenu(menuName = "ToyChest/Effects/Remove Tag", fileName = "Fx_RemoveTag")]
    public sealed class RemoveTagEffect : GameplayEffectDefinition
    {
        [SerializeField]
        private TagDefinition _tag;

        /// <inheritdoc />
        protected override void Execute(in EffectContext context)
        {
            GameplayTagContainer tags = Require(context.Target.Tags, "target");
            tags.RemoveTag(context.TagTable.GetTag(_tag.TagPath));
        }
    }
}
