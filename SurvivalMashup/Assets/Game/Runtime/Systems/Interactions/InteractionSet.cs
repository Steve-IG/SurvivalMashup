using System;
using System.Collections.Generic;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;

namespace ToyChest.Systems.Interactions
{
    /// <summary>
    /// The interactable capability of one gameplay object: the interactions it advertises.
    /// Pure data exposure — discovery reads it, the Interaction System validates and routes
    /// through it, and the referenced abilities (granted to the owner's Ability Set at
    /// composition) do the work. An object without this capability is not interactable.
    /// See Docs/Systems/INTERACTION_SYSTEM.md.
    /// </summary>
    public sealed class InteractionSet : IGameplayCapability
    {
        private readonly List<InteractionDefinition> _interactions = new List<InteractionDefinition>();
        private readonly Dictionary<DefinitionId, InteractionDefinition> _byId =
            new Dictionary<DefinitionId, InteractionDefinition>();

        /// <summary>Creates the capability from the object definition's declared interactions.</summary>
        public InteractionSet(IReadOnlyList<InteractionDefinition> interactions)
        {
            if (interactions == null)
            {
                throw new ArgumentNullException(nameof(interactions));
            }

            for (int i = 0; i < interactions.Count; i++)
            {
                InteractionDefinition interaction = interactions[i];
                if (interaction == null)
                {
                    throw new ArgumentException("Interaction list contains null. Check the object definition.", nameof(interactions));
                }

                if (interaction.Ability == null)
                {
                    throw new ArgumentException(
                        $"Interaction '{interaction.Id}' references no ability. An interaction without an ability does nothing.",
                        nameof(interactions));
                }

                if (_byId.ContainsKey(interaction.Id))
                {
                    throw new ArgumentException(
                        $"Duplicate interaction '{interaction.Id}'. An object advertises each interaction once.",
                        nameof(interactions));
                }

                _interactions.Add(interaction);
                _byId.Add(interaction.Id, interaction);
            }
        }

        /// <summary>The advertised interactions, in declared order.</summary>
        public IReadOnlyList<InteractionDefinition> Interactions => _interactions;

        /// <summary>Whether an interaction with this definition id is advertised.</summary>
        public bool Has(DefinitionId interaction) => _byId.ContainsKey(interaction);

        /// <summary>The advertised interaction, or null when not advertised.</summary>
        public InteractionDefinition Get(DefinitionId interaction)
        {
            return _byId.TryGetValue(interaction, out InteractionDefinition definition) ? definition : null;
        }
    }
}
