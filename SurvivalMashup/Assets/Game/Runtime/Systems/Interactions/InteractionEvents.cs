using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Objects;

namespace ToyChest.Systems.Interactions
{
    /// <summary>Fact: an interaction executed — its ability activation was committed.</summary>
    [EventCategory(EventCategories.Interaction)]
    public readonly struct InteractionExecuted : IGameplayEvent
    {
        public readonly GameplayObjectId Interactor;
        public readonly GameplayObjectId Interactable;
        public readonly DefinitionId Interaction;
        public readonly DefinitionId Ability;

        public InteractionExecuted(
            GameplayObjectId interactor, GameplayObjectId interactable, DefinitionId interaction, DefinitionId ability)
        {
            Interactor = interactor;
            Interactable = interactable;
            Interaction = interaction;
            Ability = ability;
        }

        /// <inheritdoc />
        public override string ToString() => $"InteractionExecuted({Interaction} on {Interactable} by {Interactor})";
    }

    /// <summary>Fact: an interaction attempt was rejected, with the first failing check as reason.</summary>
    [EventCategory(EventCategories.Interaction)]
    public readonly struct InteractionFailed : IGameplayEvent
    {
        public readonly GameplayObjectId Interactor;
        public readonly GameplayObjectId Interactable;
        public readonly DefinitionId Interaction;
        public readonly InteractionResult Reason;

        public InteractionFailed(
            GameplayObjectId interactor, GameplayObjectId interactable, DefinitionId interaction, InteractionResult reason)
        {
            Interactor = interactor;
            Interactable = interactable;
            Interaction = interaction;
            Reason = reason;
        }

        /// <inheritdoc />
        public override string ToString() => $"InteractionFailed({Interaction} on {Interactable} by {Interactor}: {Reason})";
    }
}
