using ToyChest.Systems.Tags;
using UnityEngine;

namespace ToyChest.Systems.GameplayEffects
{
    /// <summary>
    /// Gates an effect on the target holding (or lacking) a tag, matched hierarchically.
    /// The canonical reusable condition: "bonus damage to Plant", "cannot ignite the Wet".
    /// </summary>
    [CreateAssetMenu(menuName = "ToyChest/Effects/Conditions/Requires Tag", fileName = "Cond_RequiresTag")]
    public sealed class RequiresTagCondition : EffectCondition
    {
        [SerializeField]
        [Tooltip("The tag the target is checked against, matched hierarchically.")]
        private TagDefinition _tag;

        [SerializeField]
        [Tooltip("True: the target must hold the tag. False: the target must NOT hold it.")]
        private bool _mustBePresent = true;

        /// <inheritdoc />
        public override bool IsSatisfied(in EffectContext context)
        {
            if (context.Target.Tags == null)
            {
                // A participant without a tag container holds no tags.
                return !_mustBePresent;
            }

            bool present = context.TagTable.TryGetTag(_tag.TagPath, out GameplayTag tag)
                && context.Target.Tags.HasTag(tag);
            return present == _mustBePresent;
        }
    }
}
