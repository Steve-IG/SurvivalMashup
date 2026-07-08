using System.Collections.Generic;
using ToyChest.Framework.Data;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.Tags;
using UnityEngine;

namespace ToyChest.Systems.Interactions
{
    /// <summary>
    /// Immutable authoring definition of one interaction an interactable object offers
    /// (Open, Harvest, Talk, Activate). Pure orchestration configuration: the ability that
    /// executes, the priority used when several interactions are in reach, and the gates the
    /// interactor must satisfy. What actually happens is entirely the referenced ability's
    /// concern — the Interaction System routes, it never mutates gameplay.
    /// See Docs/Systems/INTERACTION_SYSTEM.md.
    /// </summary>
    [CreateAssetMenu(menuName = "ToyChest/Interactions/Interaction Definition", fileName = "Interaction_")]
    public sealed class InteractionDefinition : GameplayDefinition
    {
        [SerializeField]
        [Tooltip("Designer-facing verb shown by UI prompts (Open, Harvest, Talk).")]
        private string _displayName;

        [SerializeField]
        [Tooltip("The interactable's ability executed by this interaction. Self mode acts on the interactable; Provided mode targets the interactor.")]
        private AbilityDefinition _ability;

        [SerializeField]
        [Tooltip("Selection priority when several interactions are in reach; higher wins, ties resolve in discovery order.")]
        private int _priority;

        [SerializeField]
        [Tooltip("Interactor tags that must all be present (hierarchical match). Empty = anyone may interact.")]
        private List<TagDefinition> _requiredInteractorTags = new List<TagDefinition>();

        [SerializeField]
        [Tooltip("Interactor tags any of which block the interaction (State.Stunned). Empty = never blocked.")]
        private List<TagDefinition> _blockedByInteractorTags = new List<TagDefinition>();

        /// <summary>Designer-facing verb.</summary>
        public string DisplayName => _displayName;

        /// <summary>The interactable's ability this interaction executes.</summary>
        public AbilityDefinition Ability => _ability;

        /// <summary>Selection priority; higher wins.</summary>
        public int Priority => _priority;

        /// <summary>Interactor tags that must all be present.</summary>
        public IReadOnlyList<TagDefinition> RequiredInteractorTags => _requiredInteractorTags;

        /// <summary>Interactor tags any of which block the interaction.</summary>
        public IReadOnlyList<TagDefinition> BlockedByInteractorTags => _blockedByInteractorTags;
    }
}
