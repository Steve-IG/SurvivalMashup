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
    /// The assembled game-wide services produced by the Service Creation phase
    /// (Docs/Architecture/ENGINE_STARTUP.md, phase 2), held together so the bootstrap can hand
    /// them to consumers by injection.
    ///
    /// This is a plain immutable holder of the concrete services created once at startup — not a
    /// service locator. It exposes each service as a typed member; it does not resolve services by
    /// type at runtime, and there is no global/static access to it. Consumers receive the specific
    /// services they need through constructor injection, consistent with the rest of the engine.
    /// </summary>
    public sealed class RuntimeServices
    {
        internal RuntimeServices(
            IGameLogger logger,
            IEventBus eventBus,
            IDataRegistry dataRegistry,
            GameplayTagTable tagTable,
            GameplayObjectRegistry objects,
            GameplayObjectContext context,
            GameplayObjectFactory factory,
            SaveManager saveManager)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            EventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            DataRegistry = dataRegistry ?? throw new ArgumentNullException(nameof(dataRegistry));
            TagTable = tagTable ?? throw new ArgumentNullException(nameof(tagTable));
            Objects = objects ?? throw new ArgumentNullException(nameof(objects));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            SaveManager = saveManager ?? throw new ArgumentNullException(nameof(saveManager));
        }

        /// <summary>Game-wide logging capability.</summary>
        public IGameLogger Logger { get; }

        /// <summary>The one game-wide event transport.</summary>
        public IEventBus EventBus { get; }

        /// <summary>The authoritative definition lookup, populated during Registry Population.</summary>
        public IDataRegistry DataRegistry { get; }

        /// <summary>The session's interned tag hierarchy.</summary>
        public GameplayTagTable TagTable { get; }

        /// <summary>The authoritative live-object set for the session.</summary>
        public GameplayObjectRegistry Objects { get; }

        /// <summary>The injected service context handed to the composition root.</summary>
        public GameplayObjectContext Context { get; }

        /// <summary>The composition root that turns a definition into a wired Gameplay Object.</summary>
        public GameplayObjectFactory Factory { get; }

        /// <summary>Coordinates capture and reconstruction of authoritative state.</summary>
        public SaveManager SaveManager { get; }

        /// <summary>
        /// The objects reconstructed from the save during startup, in payload order; empty for a new
        /// game. They are already activated and present in <see cref="Objects"/>.
        /// </summary>
        public IReadOnlyList<GameplayObject> RestoredObjects { get; internal set; } = Array.Empty<GameplayObject>();
    }
}
