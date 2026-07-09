using System.Collections.Generic;
using ToyChest.Core.Logging;
using ToyChest.Framework.Data;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace ToyChest.Boot
{
    /// <summary>
    /// The Addressables-backed definition source (Docs/Architecture/DATA_REGISTRY.md, Definition
    /// Sources). It loads every asset tagged with a definitions label and yields the ones that are
    /// <see cref="IDefinition"/>s. All Addressables coupling lives here — the Data Registry and every
    /// gameplay system remain unaware of how definitions were acquired.
    ///
    /// Loading completes synchronously inside <see cref="LoadDefinitions"/> (via
    /// <c>WaitForCompletion</c>) so registration order depends only on source order, not load timing,
    /// exactly as the Registry Population phase requires. Loaded definitions are immutable and held for
    /// the whole session, so their Addressables handle is deliberately not released here.
    /// </summary>
    public sealed class AddressablesDefinitionSource : IDefinitionSource
    {
        /// <summary>
        /// The Addressables label every gameplay definition asset carries. Authoring a new
        /// definition means creating the asset and tagging it with this label; no code change.
        /// </summary>
        public const string DefaultDefinitionsLabel = "definitions";

        private readonly string _label;
        private readonly IGameLogger _logger;

        /// <summary>
        /// Creates a source that loads assets tagged with <paramref name="label"/>.
        /// The optional <paramref name="logger"/> records how many definitions were found.
        /// </summary>
        public AddressablesDefinitionSource(string label = DefaultDefinitionsLabel, IGameLogger logger = null)
        {
            _label = string.IsNullOrWhiteSpace(label) ? DefaultDefinitionsLabel : label;
            _logger = logger;
        }

        /// <inheritdoc />
        public IEnumerable<IDefinition> LoadDefinitions()
        {
            // Resolve locations first: an unknown or unpopulated label yields an empty set rather
            // than throwing, so a project with no authored content yet boots to an empty registry
            // instead of failing. A genuine load failure below still surfaces.
            var locationsHandle = Addressables.LoadResourceLocationsAsync(_label, typeof(Object));
            IList<IResourceLocation> locations = locationsHandle.WaitForCompletion();

            var definitions = new List<IDefinition>();
            if (locations == null || locations.Count == 0)
            {
                Addressables.Release(locationsHandle);
                _logger?.Info($"[Bootstrap] No assets found for definitions label '{_label}'. Registry will be empty.");
                return definitions;
            }

            var assetsHandle = Addressables.LoadAssetsAsync<Object>(locations, null);
            IList<Object> assets = assetsHandle.WaitForCompletion();
            if (assets != null)
            {
                foreach (Object asset in assets)
                {
                    if (asset is IDefinition definition)
                    {
                        definitions.Add(definition);
                    }
                }
            }

            Addressables.Release(locationsHandle);
            _logger?.Info($"[Bootstrap] Loaded {definitions.Count} definition(s) for label '{_label}'.");
            return definitions;
        }
    }
}
