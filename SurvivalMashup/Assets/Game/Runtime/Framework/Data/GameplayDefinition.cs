using UnityEngine;

namespace ToyChest.Framework.Data
{
    /// <summary>
    /// Standard ScriptableObject base for immutable gameplay definitions
    /// (abilities, attributes, resources, items, effects, ...).
    /// Carries the stable id used by the Data Registry. Derive from this unless the
    /// definition has a natural id of its own, as Tag Definitions do with their tag path.
    /// </summary>
    public abstract class GameplayDefinition : ScriptableObject, IDefinition
    {
        [SerializeField]
        [Tooltip("Stable id, never changed after shipping. Convention: lowercase dot-separated, e.g. ability.fireball")]
        private string _definitionId;

        /// <inheritdoc />
        public DefinitionId Id => new DefinitionId(_definitionId);

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_definitionId))
            {
                Debug.LogWarning(
                    $"Definition asset '{name}' has no definition id. It cannot be registered until one is set.",
                    this);
            }
        }
#endif
    }
}
