using System;
using System.Collections.Generic;
using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Objects;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.GameplayEffects;
using ToyChest.Systems.Tags;

namespace ToyChest.Systems.Interactions
{
    /// <summary>
    /// The interaction orchestrator: validates interactions between gameplay objects, selects
    /// among candidates, routes execution to the interactable's abilities, and publishes
    /// interaction events. Pure orchestration — every gameplay consequence belongs to the
    /// activated ability and its Gameplay Effects. Players and AI use the same entry points.
    /// Spatial discovery (range, line of sight) is a presentation-layer adapter concern: the
    /// adapter supplies the candidate list, deterministically ordered, and this system never
    /// touches the scene. Built once at bootstrap with injected services.
    /// See Docs/Systems/INTERACTION_SYSTEM.md.
    /// </summary>
    public sealed class InteractionSystem
    {
        private readonly IEventBus _eventBus;
        private readonly GameplayTagTable _tagTable;

        /// <summary>Creates the orchestrator.</summary>
        /// <param name="eventBus">Publishes interaction events; null runs silently.</param>
        /// <param name="tagTable">Resolves authored tag paths.</param>
        public InteractionSystem(IEventBus eventBus, GameplayTagTable tagTable)
        {
            _eventBus = eventBus;
            _tagTable = tagTable ?? throw new ArgumentNullException(nameof(tagTable));
        }

        /// <summary>
        /// Whether the interactor could execute this interaction right now. Runs the same
        /// validation as <see cref="TryInteract"/> without committing anything or publishing
        /// events. For UI prompts and AI evaluation.
        /// </summary>
        public InteractionResult CanInteract(GameplayObject interactor, GameplayObject interactable, DefinitionId interaction)
        {
            RequireParticipants(interactor, interactable);
            return Validate(interactor, interactable, interaction);
        }

        /// <summary>
        /// Collects every interaction the interactor could execute across the candidates,
        /// in candidate order then declaration order (deterministic for a given candidate
        /// list). Clears <paramref name="results"/> first. Priority ordering is applied by
        /// <see cref="TrySelectBest"/>; presentation may sort differently.
        /// </summary>
        public void QueryAvailable(
            GameplayObject interactor, IReadOnlyList<GameplayObject> candidates, List<AvailableInteraction> results)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            for (int i = 0; i < candidates.Count; i++)
            {
                GameplayObject candidate = candidates[i];
                if (candidate == null || !candidate.TryGet(out InteractionSet interactions))
                {
                    continue;
                }

                for (int j = 0; j < interactions.Interactions.Count; j++)
                {
                    InteractionDefinition interaction = interactions.Interactions[j];
                    if (Validate(interactor, candidate, interaction.Id) == InteractionResult.Executed)
                    {
                        results.Add(new AvailableInteraction(candidate, interaction));
                    }
                }
            }
        }

        /// <summary>
        /// Selects the highest-priority valid interaction across the candidates; ties resolve
        /// to the earliest discovered (candidate order, then declaration order), so selection
        /// is deterministic for a given candidate list. This is the interaction a contextual
        /// input prompt offers.
        /// </summary>
        public bool TrySelectBest(
            GameplayObject interactor, IReadOnlyList<GameplayObject> candidates, out AvailableInteraction best)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            best = default;
            bool found = false;

            for (int i = 0; i < candidates.Count; i++)
            {
                GameplayObject candidate = candidates[i];
                if (candidate == null || !candidate.TryGet(out InteractionSet interactions))
                {
                    continue;
                }

                for (int j = 0; j < interactions.Interactions.Count; j++)
                {
                    InteractionDefinition interaction = interactions.Interactions[j];
                    if (found && interaction.Priority <= best.Interaction.Priority)
                    {
                        continue;
                    }

                    if (Validate(interactor, candidate, interaction.Id) == InteractionResult.Executed)
                    {
                        best = new AvailableInteraction(candidate, interaction);
                        found = true;
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// Executes an interaction: validates, activates the interactable's ability (Self
        /// mode acts on the interactable; Provided mode receives the interactor as target),
        /// and publishes <see cref="InteractionExecuted"/> or <see cref="InteractionFailed"/>.
        /// </summary>
        public InteractionResult TryInteract(GameplayObject interactor, GameplayObject interactable, DefinitionId interaction)
        {
            RequireParticipants(interactor, interactable);

            InteractionResult result = Validate(interactor, interactable, interaction);
            if (result == InteractionResult.Executed)
            {
                InteractionDefinition definition = interactable.Get<InteractionSet>().Get(interaction);
                if (Activate(interactor, interactable, definition) != AbilityActivationResult.Activated)
                {
                    result = InteractionResult.AbilityRejected;
                }
            }

            if (result == InteractionResult.Executed)
            {
                InteractionDefinition definition = interactable.Get<InteractionSet>().Get(interaction);
                _eventBus?.Publish(new InteractionExecuted(interactor.Id, interactable.Id, interaction, definition.Ability.Id));
            }
            else
            {
                _eventBus?.Publish(new InteractionFailed(interactor.Id, interactable.Id, interaction, result));
            }

            return result;
        }

        private InteractionResult Validate(GameplayObject interactor, GameplayObject interactable, DefinitionId interaction)
        {
            if (!interactable.TryGet(out InteractionSet interactions))
            {
                return InteractionResult.NotInteractable;
            }

            InteractionDefinition definition = interactions.Get(interaction);
            if (definition == null)
            {
                return InteractionResult.UnknownInteraction;
            }

            interactor.TryGet(out GameplayTagContainer interactorTags);

            IReadOnlyList<TagDefinition> required = definition.RequiredInteractorTags;
            for (int i = 0; i < required.Count; i++)
            {
                if (interactorTags == null || !interactorTags.HasTag(_tagTable.GetTag(required[i].TagPath)))
                {
                    return InteractionResult.MissingInteractorTag;
                }
            }

            IReadOnlyList<TagDefinition> blocking = definition.BlockedByInteractorTags;
            for (int i = 0; i < blocking.Count; i++)
            {
                if (interactorTags != null && interactorTags.HasTag(_tagTable.GetTag(blocking[i].TagPath)))
                {
                    return InteractionResult.BlockedByInteractorTag;
                }
            }

            AbilitySet abilities = interactable.Get<AbilitySet>();
            AbilityActivationResult abilityResult = definition.Ability.TargetMode == AbilityTargetMode.Self
                ? abilities.CanActivate(definition.Ability.Id)
                : abilities.CanActivate(definition.Ability.Id, EffectTarget.From(interactor));

            return abilityResult == AbilityActivationResult.Activated
                ? InteractionResult.Executed
                : InteractionResult.AbilityRejected;
        }

        private static AbilityActivationResult Activate(
            GameplayObject interactor, GameplayObject interactable, InteractionDefinition definition)
        {
            AbilitySet abilities = interactable.Get<AbilitySet>();
            return definition.Ability.TargetMode == AbilityTargetMode.Self
                ? abilities.TryActivate(definition.Ability.Id)
                : abilities.TryActivate(definition.Ability.Id, EffectTarget.From(interactor));
        }

        private static void RequireParticipants(GameplayObject interactor, GameplayObject interactable)
        {
            if (interactor == null)
            {
                throw new ArgumentNullException(nameof(interactor));
            }

            if (interactable == null)
            {
                throw new ArgumentNullException(nameof(interactable));
            }
        }
    }
}
