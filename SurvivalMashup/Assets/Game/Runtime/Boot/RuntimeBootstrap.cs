using System;
using System.Collections.Generic;
using ToyChest.Core.Logging;
using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Objects;
using ToyChest.Systems.Save;
using ToyChest.Systems.Tags;

namespace ToyChest.Boot
{
    /// <summary>
    /// The permanent runtime startup path (Docs/Architecture/ENGINE_STARTUP.md). Drives the fixed
    /// startup sequence — Service Creation, Registry Population, Save Loading, Gameplay Object
    /// Reconstruction, Runtime Initialization — so services exist before their consumers,
    /// definitions are registered before they are resolved, and saved state is restored onto
    /// already-reconstructed objects.
    ///
    /// This is the conductor of phase 1 (Bootstrap): it holds no gameplay rules and creates no
    /// framework concepts. It only connects already-existing systems in the one authoritative order,
    /// then returns the assembled <see cref="RuntimeServices"/>. Plain C# and engine-agnostic, so
    /// the Bootstrap scene, an integration test, and a future server all boot through this same code.
    /// The scene adapter (<see cref="GameBootstrap"/>) supplies production configuration and, once
    /// gameplay systems exist, drives phase 7 (Gameplay Start).
    /// </summary>
    public sealed class RuntimeBootstrap
    {
        /// <summary>
        /// Runs startup phases 2–6 for <paramref name="configuration"/> and returns the assembled
        /// services. On return the Data Registry is populated, the Tag Table is interned, and any
        /// saved objects have been reconstructed and activated into the live-object set.
        /// </summary>
        public RuntimeServices Run(BootstrapConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            IGameLogger logger = configuration.Logger;
            logger.Info("[Bootstrap] Starting engine initialization.");

            RuntimeServices services = CreateServices(configuration);
            PopulateRegistry(configuration, services);
            RestoreSave(configuration, services);

            logger.Info(
                $"[Bootstrap] Engine initialized. Live objects: {services.Objects.Count}. Ready for gameplay.");
            return services;
        }

        // Phase 2: Service Creation — construct the game-wide services in dependency order and
        // assemble the composition context. Everything is created once and injected; no globals.
        private static RuntimeServices CreateServices(BootstrapConfiguration configuration)
        {
            IGameLogger logger = configuration.Logger;

            var eventBus = new EventBus(logger, configuration.Diagnostics);
            var dataRegistry = new DataRegistry();
            var tagTable = new GameplayTagTable();
            var objects = new GameplayObjectRegistry();

            // The composition context carries the services the factory injects into capabilities,
            // plus the registry every activated object joins.
            var context = new GameplayObjectContext(eventBus, dataRegistry, tagTable, objects);
            var factory = new GameplayObjectFactory(context);
            var saveManager = new SaveManager();

            logger.Info("[Bootstrap] Services created (event bus, data registry, tag table, object registry, factory, save).");
            return new RuntimeServices(logger, eventBus, dataRegistry, tagTable, objects, context, factory, saveManager);
        }

        // Phase 3: Registry Population — register every definition each source yields, then intern
        // the tag hierarchy from the registered TagDefinitions. Duplicate or invalid ids throw here,
        // at startup, never mid-session. Read-only by convention afterward.
        private static void PopulateRegistry(BootstrapConfiguration configuration, RuntimeServices services)
        {
            int registered = 0;
            foreach (IDefinitionSource source in configuration.DefinitionSources)
            {
                foreach (IDefinition definition in source.LoadDefinitions())
                {
                    services.DataRegistry.Register(definition);
                    registered++;
                }
            }

            // Downstream structures build from the now-populated registry: the Tag Table interns
            // every registered TagDefinition (ancestors included).
            IReadOnlyList<TagDefinition> tagDefinitions = services.DataRegistry.GetAll<TagDefinition>();
            services.TagTable.RegisterDefinitions(tagDefinitions);

            services.Logger.Info(
                $"[Bootstrap] Registry populated: {registered} definition(s); {services.TagTable.Count} tag(s) interned.");
        }

        // Phases 4–6: Save Loading, Reconstruction, and Runtime Initialization. A new game has no
        // payload and starts from an empty world. A save is parsed (read + validated as data), then
        // each persisted object is reconstructed through the composition root, has its authoritative
        // state restored behind a closed event gate, and is activated — the first observable fact.
        private static void RestoreSave(BootstrapConfiguration configuration, RuntimeServices services)
        {
            if (!configuration.HasSave)
            {
                services.Logger.Info("[Bootstrap] New game: no save to reconstruct.");
                return;
            }

            SaveData save = services.SaveManager.FromJson(configuration.SaveJson);
            IReadOnlyList<GameplayObject> restored =
                services.SaveManager.Restore(save, services.Factory, services.DataRegistry);
            services.RestoredObjects = restored;

            services.Logger.Info($"[Bootstrap] Reconstructed and activated {restored.Count} saved object(s).");
        }
    }
}
