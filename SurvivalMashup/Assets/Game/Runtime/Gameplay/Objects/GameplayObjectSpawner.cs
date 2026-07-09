using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Scene;
using UnityEngine;

namespace ToyChest.Gameplay.Objects
{
    /// <summary>
    /// The canonical Unity adapter for a scene-authored Gameplay Object: it references a
    /// <see cref="GameplayObjectDefinition"/>, composes it through the existing
    /// <see cref="GameplayObjectFactory"/> (the one composition root, Engine Principle 22), and
    /// binds the composed object to its sibling <see cref="GameplayObjectBehaviour"/>, which
    /// activates it through the normal lifecycle. Composition happens when the Boot layer injects
    /// the <see cref="GameplaySceneContext"/> after the gameplay scene loads — no scene component
    /// reaches for the factory itself.
    ///
    /// It is intentionally generic: a player, an NPC, a prop, an interactable, or an enemy is the
    /// same adapter pointed at a different definition. It contains no gameplay logic; it only turns
    /// "this scene object is a <c>Definition</c>" into a composed, activated Gameplay Object. Until
    /// scene streaming and scene-loading systems arrive, this is the standard scene composition path.
    /// See Docs/Systems/GAMEPLAY_FRAMEWORK.md, Composition Root.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameplayObjectBehaviour))]
    public sealed class GameplayObjectSpawner : MonoBehaviour, IGameplaySceneParticipant
    {
        [SerializeField]
        [Tooltip("The definition this scene object is composed from. Required.")]
        private GameplayObjectDefinition _definition;

        [SerializeField]
        [Tooltip("The behaviour the composed object binds to. Defaults to the sibling on this GameObject.")]
        private GameplayObjectBehaviour _behaviour;

        private bool _composed;

        /// <summary>The definition this spawner composes from.</summary>
        public GameplayObjectDefinition Definition => _definition;

        private void Reset()
        {
            _behaviour = GetComponent<GameplayObjectBehaviour>();
        }

        /// <summary>
        /// Composes the definition through the injected factory and binds it to the behaviour.
        /// Idempotent: a second injection (or a re-entrant scene load) does not recompose.
        /// </summary>
        public void OnGameplaySceneReady(GameplaySceneContext context)
        {
            Compose(context);
        }

        /// <summary>
        /// Composes and binds using <paramref name="context"/>. Exposed for edit-mode tests that
        /// drive composition directly without a scene load.
        /// </summary>
        public void Compose(GameplaySceneContext context)
        {
            if (_composed)
            {
                return;
            }

            if (context == null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            if (_definition == null)
            {
                Debug.LogError(
                    $"[GameplayObjectSpawner] '{name}' has no definition assigned; nothing to compose.", this);
                return;
            }

            if (_behaviour == null)
            {
                _behaviour = GetComponent<GameplayObjectBehaviour>();
            }

            GameplayObject composed = context.Factory.Create(_definition);
            _behaviour.Bind(composed);
            _composed = true;
        }
    }
}
