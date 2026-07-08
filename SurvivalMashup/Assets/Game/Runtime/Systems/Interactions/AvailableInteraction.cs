using ToyChest.Framework.Objects;

namespace ToyChest.Systems.Interactions
{
    /// <summary>
    /// One valid interaction discovered for an interactor: the interactable offering it and
    /// the interaction definition. Produced by interaction queries; consumed by input
    /// prompts, AI decision-making, and <see cref="InteractionSystem.TryInteract"/>.
    /// </summary>
    public readonly struct AvailableInteraction
    {
        /// <summary>The interactable object offering the interaction.</summary>
        public readonly GameplayObject Interactable;

        /// <summary>The offered interaction.</summary>
        public readonly InteractionDefinition Interaction;

        /// <summary>Creates a discovered interaction.</summary>
        public AvailableInteraction(GameplayObject interactable, InteractionDefinition interaction)
        {
            Interactable = interactable;
            Interaction = interaction;
        }
    }
}
