using UnityEngine;

namespace ToyChest.Framework.Objects
{
    /// <summary>
    /// Thin MonoBehaviour bridge between the Unity scene and a plain-C# <see cref="GameplayObject"/>.
    /// Responsibilities are strictly: hold a composed object from the factory, translate the Unity
    /// lifecycle into object lifecycle (activate on enable, tick from Update with injected delta,
    /// destroy on OnDestroy), and expose the object to scene-side code. Contains no gameplay
    /// rules; all business logic lives in the object and its capabilities.
    /// </summary>
    public sealed class GameplayObjectBehaviour : MonoBehaviour
    {
        /// <summary>The composed object this behaviour drives, or null before <see cref="Bind"/>.</summary>
        public GameplayObject Object { get; private set; }

        /// <summary>
        /// Attaches a composed (not yet activated) object to this behaviour. If the behaviour
        /// is already enabled, the object activates immediately; otherwise on the next enable.
        /// Binding twice is a programming error.
        /// </summary>
        public void Bind(GameplayObject gameplayObject)
        {
            if (Object != null)
            {
                throw new System.InvalidOperationException(
                    $"'{name}' is already bound to GameplayObject '{Object.Id}'. One behaviour drives one object.");
            }

            Object = gameplayObject ?? throw new System.ArgumentNullException(nameof(gameplayObject));

            if (isActiveAndEnabled && !Object.IsActive && !Object.IsDestroyed)
            {
                Object.Activate();
            }
        }

        private void OnEnable()
        {
            if (Object != null && !Object.IsActive && !Object.IsDestroyed)
            {
                Object.Activate();
            }
        }

        private void Update()
        {
            if (Object != null && Object.IsActive)
            {
                Object.Tick(Time.deltaTime);
            }
        }

        private void OnDestroy()
        {
            Object?.Destroy();
        }
    }
}
