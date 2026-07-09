using System;
using System.Collections.Generic;
using ToyChest.Core.Logging;
using ToyChest.Framework.Data;
using ToyChest.Framework.Events;

namespace ToyChest.Boot
{
    /// <summary>
    /// The launch configuration the bootstrap reads to bring up a session
    /// (Docs/Architecture/ENGINE_STARTUP.md, phase 1): which logger to use, which definition
    /// sources populate the Data Registry, and the save payload to reconstruct (or none, for a new
    /// game). Immutable and engine-agnostic, so the same <see cref="RuntimeBootstrap"/> runs from
    /// the Bootstrap scene, an integration test, or a future dedicated server.
    /// </summary>
    public sealed class BootstrapConfiguration
    {
        /// <summary>
        /// Creates a launch configuration.
        /// </summary>
        /// <param name="logger">Game-wide logging capability. Required.</param>
        /// <param name="definitionSources">
        /// Sources enumerated, in order, to populate the Data Registry. Required and non-empty:
        /// a session with no sources cannot resolve any content. A source that yields nothing
        /// (an empty Addressables label before content exists) is fine.
        /// </param>
        /// <param name="saveJson">
        /// The save payload to reconstruct, or null/empty for a new game. A new game starts from an
        /// empty world; authored starting state arrives once content and a starting scene exist.
        /// </param>
        /// <param name="diagnostics">Optional event-bus debug observer for editor/development tooling.</param>
        public BootstrapConfiguration(
            IGameLogger logger,
            IEnumerable<IDefinitionSource> definitionSources,
            string saveJson = null,
            IEventBusDiagnostics diagnostics = null)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (definitionSources == null)
            {
                throw new ArgumentNullException(nameof(definitionSources));
            }

            var sources = new List<IDefinitionSource>();
            foreach (IDefinitionSource source in definitionSources)
            {
                if (source == null)
                {
                    throw new ArgumentException("Definition source list contains null.", nameof(definitionSources));
                }

                sources.Add(source);
            }

            if (sources.Count == 0)
            {
                throw new ArgumentException(
                    "At least one definition source is required; a session with no sources resolves no content.",
                    nameof(definitionSources));
            }

            DefinitionSources = sources;
            SaveJson = saveJson;
            Diagnostics = diagnostics;
        }

        /// <summary>Game-wide logging capability.</summary>
        public IGameLogger Logger { get; }

        /// <summary>Definition sources enumerated in order during Registry Population.</summary>
        public IReadOnlyList<IDefinitionSource> DefinitionSources { get; }

        /// <summary>The save payload to reconstruct, or null/empty for a new game.</summary>
        public string SaveJson { get; }

        /// <summary>Whether this launch reconstructs a save rather than starting a new game.</summary>
        public bool HasSave => !string.IsNullOrEmpty(SaveJson);

        /// <summary>Optional event-bus debug observer; null in production.</summary>
        public IEventBusDiagnostics Diagnostics { get; }
    }
}
