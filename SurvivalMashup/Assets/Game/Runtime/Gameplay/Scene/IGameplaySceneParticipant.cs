namespace ToyChest.Gameplay.Scene
{
    /// <summary>
    /// Implemented by a scene Unity adapter that needs the live engine services once the gameplay
    /// scene has loaded. The Boot layer — which owns the assembled services and drives the scene
    /// transition — finds every participant in the freshly loaded gameplay scene and hands it a
    /// <see cref="GameplaySceneContext"/> exactly once. This is the injection seam that keeps scene
    /// components free of globals and service locators: they receive what they need, they never
    /// fetch it.
    /// </summary>
    public interface IGameplaySceneParticipant
    {
        /// <summary>
        /// Called once, after the gameplay scene has loaded, with the injected services. An adapter
        /// that composes an object (a spawner) does so here; an adapter that merely needs a service
        /// (an interactor) caches it here.
        /// </summary>
        void OnGameplaySceneReady(GameplaySceneContext context);
    }
}
