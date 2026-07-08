using System;
using System.Collections.Generic;

namespace ToyChest.Framework.Data
{
    /// <summary>
    /// Standard implementation of <see cref="IDataRegistry"/>.
    /// Plain C#: no engine lifecycle dependency, fully testable outside Unity.
    /// Created at bootstrap and provided through constructor injection; no global singleton.
    /// Fails early and clearly on duplicate or missing ids — content errors surface at
    /// startup, never mid-session.
    /// </summary>
    public sealed class DataRegistry : IDataRegistry
    {
        private readonly Dictionary<Type, ITypeBucket> _buckets = new Dictionary<Type, ITypeBucket>();

        /// <inheritdoc />
        public void Register(IDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            DefinitionId id = definition.Id;
            if (!id.IsValid)
            {
                throw new ArgumentException(
                    $"Definition of type '{definition.GetType().Name}' has an invalid id and cannot be registered. " +
                    "Check the asset's definition id field.",
                    nameof(definition));
            }

            Type concreteType = definition.GetType();
            if (!_buckets.TryGetValue(concreteType, out ITypeBucket bucket))
            {
                bucket = (ITypeBucket)Activator.CreateInstance(typeof(TypeBucket<>).MakeGenericType(concreteType));
                _buckets.Add(concreteType, bucket);
            }

            bucket.Add(id, definition);
        }

        /// <inheritdoc />
        public TDefinition Get<TDefinition>(DefinitionId id) where TDefinition : class, IDefinition
        {
            if (TryGet(id, out TDefinition definition))
            {
                return definition;
            }

            throw new KeyNotFoundException(
                $"No {typeof(TDefinition).Name} is registered under id '{id}'. " +
                "Check that the content exists, its id matches, and its definition source is configured at bootstrap.");
        }

        /// <inheritdoc />
        public bool TryGet<TDefinition>(DefinitionId id, out TDefinition definition) where TDefinition : class, IDefinition
        {
            if (_buckets.TryGetValue(typeof(TDefinition), out ITypeBucket bucket))
            {
                return ((TypeBucket<TDefinition>)bucket).TryGet(id, out definition);
            }

            definition = null;
            return false;
        }

        /// <inheritdoc />
        public IReadOnlyList<TDefinition> GetAll<TDefinition>() where TDefinition : class, IDefinition
        {
            if (_buckets.TryGetValue(typeof(TDefinition), out ITypeBucket bucket))
            {
                return ((TypeBucket<TDefinition>)bucket).All;
            }

            return Array.Empty<TDefinition>();
        }

        /// <inheritdoc />
        public bool Contains<TDefinition>(DefinitionId id) where TDefinition : class, IDefinition
        {
            return TryGet<TDefinition>(id, out _);
        }

        private interface ITypeBucket
        {
            void Add(DefinitionId id, IDefinition definition);
        }

        private sealed class TypeBucket<TDefinition> : ITypeBucket where TDefinition : class, IDefinition
        {
            private readonly Dictionary<DefinitionId, TDefinition> _byId = new Dictionary<DefinitionId, TDefinition>();
            private readonly List<TDefinition> _ordered = new List<TDefinition>();

            public IReadOnlyList<TDefinition> All => _ordered;

            public void Add(DefinitionId id, IDefinition definition)
            {
                if (_byId.ContainsKey(id))
                {
                    throw new InvalidOperationException(
                        $"A {typeof(TDefinition).Name} with id '{id}' is already registered. " +
                        "Every definition id must be unique within its type: check for duplicate " +
                        "assets or a definition source configured twice.");
                }

                var typed = (TDefinition)definition;
                _byId.Add(id, typed);
                _ordered.Add(typed);
            }

            public bool TryGet(DefinitionId id, out TDefinition definition)
            {
                return _byId.TryGetValue(id, out definition);
            }
        }
    }
}
