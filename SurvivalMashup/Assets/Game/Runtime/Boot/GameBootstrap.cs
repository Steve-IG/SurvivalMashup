using System.Collections.Generic;
using ToyChest.Core.Logging;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Objects;
using ToyChest.Gameplay.Scene;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ToyChest.Boot
{
    /// <summary>
    /// The Bootstrap scene's single entry point (Docs/Architecture/ENGINE_STARTUP.md, phase 1):
    /// the one MonoBehaviour that owns bringing up a session. It is a thin adapter — it reads
    /// launch configuration from the scene, builds the production
    /// <see cref="BootstrapConfiguration"/> (a Unity logger and the Addressables definition source),
    /// and runs the engine-agnostic <see cref="RuntimeBootstrap"/>. All startup logic lives in the
    /// plain-C# bootstrap, which is why it is testable without a scene.
    ///
    /// Bootstrap performs engine initialization only; gameplay lives in the gameplay scene
    /// (Docs/Development/MILESTONE_1_VERTICAL_SLICE.md). After the engine is up, this adapter
    /// transitions into that scene and injects the assembled services into its
    /// <see cref="IGameplaySceneParticipant"/>s — the one seam from Boot (which owns the services)
    /// to scene adapters (which must never fetch a global). It holds no gameplay rules.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Addressables label whose assets are loaded as gameplay definitions at startup.")]
        private string _definitionsLabel = AddressablesDefinitionSource.DefaultDefinitionsLabel;

        [SerializeField]
        [Tooltip("Keep the initialized services alive across scene loads. The Bootstrap scene owns the session.")]
        private bool _persistAcrossScenes = true;

        [SerializeField]
        [Tooltip("Gameplay scene loaded after engine initialization. Blank keeps the session in the Bootstrap scene. " +
                 "Must be added to Build Settings.")]
        private string _gameplaySceneName = "VerticalSlice";

        [SerializeField]
        [Tooltip("Definition ids of Gameplay Objects composed and activated once the engine is up. " +
                 "Each id must resolve to a GameplayObjectDefinition loaded through the definitions label. " +
                 "Scene-present objects are authored with a GameplayObjectSpawner instead of listed here.")]
        private List<string> _startupObjectDefinitionIds = new List<string>();

        /// <summary>The assembled services, available after <see cref="Awake"/>; null before it runs.</summary>
        public RuntimeServices Services { get; private set; }

        private void Awake()
        {
            if (_persistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
            }

            var logger = new UnityGameLogger();

            // Save-slot selection is a future task (Docs/Development/DEVELOPMENT_PLAN.md, Scene
            // Loading); this launch starts a new game. The Addressables source isolates all content
            // loading behind the definitions label.
            var configuration = new BootstrapConfiguration(
                logger,
                new IDefinitionSource[] { new AddressablesDefinitionSource(_definitionsLabel, logger) },
                saveJson: null);

            Services = new RuntimeBootstrap().Run(configuration);
            SpawnStartupObjects(Services);
            TransitionToGameplay();
        }

        // Composes and activates each authored startup definition through the bootstrapped factory
        // (Factory.Create → Activate), so the object joins the live registry — the same path any
        // future spawner uses. This path is for logical, scene-independent objects; objects with a
        // scene presence are authored in the gameplay scene with a GameplayObjectSpawner. A missing
        // id is an authoring error and is logged rather than thrown, so one bad id does not abort.
        private void SpawnStartupObjects(RuntimeServices services)
        {
            for (int i = 0; i < _startupObjectDefinitionIds.Count; i++)
            {
                string rawId = _startupObjectDefinitionIds[i];
                if (string.IsNullOrWhiteSpace(rawId))
                {
                    continue;
                }

                var id = new DefinitionId(rawId);
                if (!services.DataRegistry.TryGet(id, out GameplayObjectDefinition definition))
                {
                    services.Logger.Error(
                        $"[Bootstrap] Startup object '{rawId}' is not a registered GameplayObjectDefinition. " +
                        "Check the id and that its asset carries the definitions label.");
                    continue;
                }

                GameplayObject spawned = services.Factory.Create(definition);
                spawned.Activate();
                services.Logger.Info($"[Bootstrap] Spawned startup object '{rawId}' ({spawned.Id}).");
            }
        }

        // Loads the gameplay scene, then injects the assembled services into its scene adapters. The
        // Bootstrap scene contributes only engine initialization; all gameplay content lives in the
        // gameplay scene. Loading Single unloads Bootstrap while this DontDestroyOnLoad object (and
        // the services it holds) survives, so the gameplay scene owns the only camera and content.
        private void TransitionToGameplay()
        {
            if (string.IsNullOrWhiteSpace(_gameplaySceneName))
            {
                Services.Logger.Info("[Bootstrap] No gameplay scene configured; staying in the Bootstrap scene.");
                return;
            }

            SceneManager.sceneLoaded += OnGameplaySceneLoaded;
            SceneManager.LoadScene(_gameplaySceneName, LoadSceneMode.Single);
        }

        private void OnGameplaySceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            if (scene.name != _gameplaySceneName)
            {
                return;
            }

            SceneManager.sceneLoaded -= OnGameplaySceneLoaded;

            GameplaySceneContext context = GameplaySceneContext.Create(
                Services.Factory, Services.EventBus, Services.TagTable);

            int injected = InjectParticipants(scene, context);
            Services.Logger.Info(
                $"[Bootstrap] Entered gameplay scene '{scene.name}'; injected {injected} scene participant(s).");
        }

        // Scoped to the loaded scene (never a global FindObjectsOfType): every participant in the
        // gameplay scene receives the context exactly once, and composition happens as each spawner
        // reacts to injection.
        private static int InjectParticipants(UnityEngine.SceneManagement.Scene scene, GameplaySceneContext context)
        {
            int injected = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            var buffer = new List<IGameplaySceneParticipant>();
            for (int i = 0; i < roots.Length; i++)
            {
                roots[i].GetComponentsInChildren(true, buffer);
                for (int j = 0; j < buffer.Count; j++)
                {
                    buffer[j].OnGameplaySceneReady(context);
                    injected++;
                }
            }

            return injected;
        }
    }
}
