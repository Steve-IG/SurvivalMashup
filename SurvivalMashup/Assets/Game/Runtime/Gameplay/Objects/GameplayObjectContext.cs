using System;
using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Objects;
using ToyChest.Systems.Tags;

namespace ToyChest.Gameplay.Objects
{
    /// <summary>
    /// The injected services the composition root needs to assemble Gameplay Objects.
    /// Built once at bootstrap and handed to the <see cref="GameplayObjectFactory"/>;
    /// no capability locates services globally. Immutable.
    /// </summary>
    public sealed class GameplayObjectContext
    {
        /// <summary>
        /// Creates the service context for object composition. When <paramref name="registry"/>
        /// is supplied, every composed object registers itself there on activation and
        /// unregisters on destroy; pass null in tests that do not exercise the live-object set.
        /// </summary>
        public GameplayObjectContext(
            IEventBus eventBus,
            IDataRegistry dataRegistry,
            GameplayTagTable tagTable,
            GameplayObjectRegistry registry = null)
        {
            EventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            DataRegistry = dataRegistry ?? throw new ArgumentNullException(nameof(dataRegistry));
            TagTable = tagTable ?? throw new ArgumentNullException(nameof(tagTable));
            Registry = registry;
        }

        /// <summary>Publishes lifecycle and capability events.</summary>
        public IEventBus EventBus { get; }

        /// <summary>Canonical source of gameplay definitions.</summary>
        public IDataRegistry DataRegistry { get; }

        /// <summary>The session's interned tag hierarchy.</summary>
        public GameplayTagTable TagTable { get; }

        /// <summary>The authoritative live-object set composed objects join, or null when unused.</summary>
        public GameplayObjectRegistry Registry { get; }
    }
}
