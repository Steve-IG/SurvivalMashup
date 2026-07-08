using ToyChest.Framework.Data;
using UnityEngine;

namespace ToyChest.Systems.Tags
{
    /// <summary>
    /// Authoring asset declaring one hierarchical gameplay tag.
    /// The tag path is the stable identity: it serves as the Data Registry id and is
    /// interned into the Gameplay Tag Table at startup, ancestors included.
    /// Tags describe gameplay; they never implement it.
    /// </summary>
    [CreateAssetMenu(menuName = "ToyChest/Tags/Tag Definition", fileName = "Tag_")]
    public sealed class TagDefinition : ScriptableObject, IDefinition
    {
        [SerializeField]
        [Tooltip("Dot-separated hierarchical path, e.g. Element.Fire.Burning. Letters and digits only per segment.")]
        private string _tagPath;

        [SerializeField]
        [TextArea]
        [Tooltip("What this tag means and which systems are expected to react to it.")]
        private string _description;

        /// <summary>The dot-separated hierarchical tag path.</summary>
        public string TagPath => _tagPath;

        /// <summary>Designer-facing description of the tag's meaning.</summary>
        public string Description => _description;

        /// <inheritdoc />
        public DefinitionId Id => new DefinitionId(_tagPath);

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_tagPath))
            {
                Debug.LogWarning(
                    $"TagDefinition asset '{name}' has no tag path. It cannot be registered until one is set.",
                    this);
            }
        }
#endif
    }
}
