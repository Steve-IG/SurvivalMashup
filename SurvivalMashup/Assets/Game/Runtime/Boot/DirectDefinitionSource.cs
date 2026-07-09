using System;
using System.Collections.Generic;
using ToyChest.Framework.Data;

namespace ToyChest.Boot
{
    /// <summary>
    /// A definition source that yields a fixed set of already-held definitions.
    /// The "direct source" described in Docs/Architecture/DATA_REGISTRY.md: it owns no loading
    /// policy and knows nothing about Addressables — it simply hands back definitions supplied to
    /// it. Used by scenes that reference definitions directly, by tooling, and by tests that
    /// exercise the real <see cref="RuntimeBootstrap"/> without a content build.
    /// Engine-agnostic (plain C#), so it composes alongside the Addressables source in a
    /// <see cref="BootstrapConfiguration"/>.
    /// </summary>
    public sealed class DirectDefinitionSource : IDefinitionSource
    {
        private readonly IReadOnlyList<IDefinition> _definitions;

        /// <summary>Creates a source over the supplied definitions, in the given order.</summary>
        public DirectDefinitionSource(params IDefinition[] definitions)
            : this((IEnumerable<IDefinition>)definitions)
        {
        }

        /// <summary>Creates a source over the supplied definitions, in enumeration order.</summary>
        public DirectDefinitionSource(IEnumerable<IDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            var copy = new List<IDefinition>();
            foreach (IDefinition definition in definitions)
            {
                if (definition == null)
                {
                    throw new ArgumentException("Definition list contains null.", nameof(definitions));
                }

                copy.Add(definition);
            }

            _definitions = copy;
        }

        /// <inheritdoc />
        public IEnumerable<IDefinition> LoadDefinitions() => _definitions;
    }
}
