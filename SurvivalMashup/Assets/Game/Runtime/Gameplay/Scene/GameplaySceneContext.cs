using System;
using ToyChest.Framework.Events;
using ToyChest.Gameplay.Objects;
using ToyChest.Systems.Interactions;
using ToyChest.Systems.Tags;

namespace ToyChest.Gameplay.Scene
{
    /// <summary>
    /// The gameplay-layer services a loaded gameplay scene's Unity adapters need to reach the live
    /// engine: the composition root (to compose scene-authored objects) and the interaction
    /// orchestrator (to route contextual interactions). It is the one injection channel from the
    /// Boot layer — which owns the assembled services — into scene <see cref="IGameplaySceneParticipant"/>s,
    /// so no scene component reaches for a global or a service locator.
    ///
    /// This is a plain immutable holder, not a new framework concept: it carries already-created
    /// services (mirroring how <see cref="GameplayObjectContext"/> carries services into the
    /// factory). It is assembled in the Gameplay layer so the Boot layer needs no reference to the
    /// interaction system it does not otherwise use.
    /// </summary>
    public sealed class GameplaySceneContext
    {
        private GameplaySceneContext(GameplayObjectFactory factory, InteractionSystem interactions)
        {
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            Interactions = interactions ?? throw new ArgumentNullException(nameof(interactions));
        }

        /// <summary>The composition root that turns a definition into a wired Gameplay Object.</summary>
        public GameplayObjectFactory Factory { get; }

        /// <summary>The interaction orchestrator scene interactors route contextual interactions through.</summary>
        public InteractionSystem Interactions { get; }

        /// <summary>
        /// Assembles the scene context from the already-created engine services. Constructs the
        /// interaction orchestrator here (it is stateless orchestration built from the event bus and
        /// tag table), keeping that dependency inside the Gameplay layer.
        /// </summary>
        public static GameplaySceneContext Create(
            GameplayObjectFactory factory, IEventBus eventBus, GameplayTagTable tagTable)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (tagTable == null)
            {
                throw new ArgumentNullException(nameof(tagTable));
            }

            return new GameplaySceneContext(factory, new InteractionSystem(eventBus, tagTable));
        }
    }
}
