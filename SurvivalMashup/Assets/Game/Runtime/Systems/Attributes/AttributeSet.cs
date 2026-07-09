using System;
using System.Collections.Generic;
using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Objects;

namespace ToyChest.Systems.Attributes
{
    /// <summary>
    /// The attribute capability of one gameplay object: the collection of its
    /// <see cref="AttributeValue"/> instances, keyed by definition id. Owns the bridge from
    /// per-attribute change callbacks to the Event Bus, publishing <see cref="AttributeChanged"/>
    /// when a value moves. New attributes are added purely as data; this type has no knowledge
    /// of any specific attribute. Plain C#; usable without a MonoBehaviour so it is testable
    /// in isolation. See Docs/Systems/ATTRIBUTE_SYSTEM.md.
    /// </summary>
    public sealed class AttributeSet : IAttributeProvider, IGameplayCapability
    {
        private readonly Dictionary<DefinitionId, AttributeValue> _attributes =
            new Dictionary<DefinitionId, AttributeValue>();

        private readonly List<AttributeValue> _ordered = new List<AttributeValue>();
        private readonly IEventBus _eventBus;
        private readonly GameplayObjectId _owner;

        /// <summary>
        /// Creates an attribute set. When <paramref name="eventBus"/> is supplied, value
        /// changes publish <see cref="AttributeChanged"/>; pass null to run silently (tests
        /// that only assert on values). <paramref name="owner"/> identifies the composed
        /// object in published events; default when unowned.
        /// </summary>
        public AttributeSet(IEventBus eventBus = null, GameplayObjectId owner = default)
        {
            _eventBus = eventBus;
            _owner = owner;
        }

        /// <summary>Number of attributes in the set.</summary>
        public int Count => _attributes.Count;

        /// <summary>
        /// The attributes in deterministic registration order, for tooling and inspection.
        /// Each <see cref="AttributeValue"/> exposes its definition and current computed value;
        /// attribute state is fully derived and never persisted (Engine Principle 25). Mirrors
        /// <c>ResourceSet.Resources</c> and <c>AbilitySet.Abilities</c> for capability parity.
        /// </summary>
        public IReadOnlyList<AttributeValue> Attributes => _ordered;

        /// <summary>
        /// Adds an attribute from its definition and returns the runtime value.
        /// Throws if an attribute with the same id already exists — one source of truth per id.
        /// </summary>
        public AttributeValue AddAttribute(AttributeDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            DefinitionId id = definition.Id;
            if (_attributes.ContainsKey(id))
            {
                throw new InvalidOperationException(
                    $"Attribute '{id}' is already present in this set. Each attribute is owned once per object.");
            }

            var value = new AttributeValue(definition);
            value.ValueChanged += (previous, next) => PublishChange(id, previous, next);
            _attributes.Add(id, value);
            _ordered.Add(value);
            return value;
        }

        /// <inheritdoc />
        public bool HasAttribute(DefinitionId attribute) => _attributes.ContainsKey(attribute);

        /// <inheritdoc />
        public AttributeValue GetAttribute(DefinitionId attribute)
        {
            return _attributes.TryGetValue(attribute, out AttributeValue value) ? value : null;
        }

        /// <inheritdoc />
        public float GetValue(DefinitionId attribute)
        {
            if (_attributes.TryGetValue(attribute, out AttributeValue value))
            {
                return value.CurrentValue;
            }

            throw new KeyNotFoundException(
                $"Attribute '{attribute}' is not present in this set. " +
                "Add it from its definition before querying, or use TryGetValue.");
        }

        /// <inheritdoc />
        public bool TryGetValue(DefinitionId attribute, out float value)
        {
            if (_attributes.TryGetValue(attribute, out AttributeValue attributeValue))
            {
                value = attributeValue.CurrentValue;
                return true;
            }

            value = 0f;
            return false;
        }

        private void PublishChange(DefinitionId id, float previous, float next)
        {
            _eventBus?.Publish(new AttributeChanged(_owner, id, previous, next));
        }
    }
}
