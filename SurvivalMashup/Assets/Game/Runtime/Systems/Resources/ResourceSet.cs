using System;
using System.Collections.Generic;
using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Objects;
using ToyChest.Systems.Attributes;

namespace ToyChest.Systems.Resources
{
    /// <summary>
    /// The resource capability of one gameplay object: its <see cref="ResourceValue"/> instances
    /// keyed by definition id. Resolves attribute bindings against an <see cref="IAttributeProvider"/>
    /// when adding a bound resource, and forwards per-resource change and lifecycle callbacks to
    /// the Event Bus as <see cref="ResourceChanged"/>, <see cref="ResourceDepleted"/>, and
    /// <see cref="ResourceFull"/>. New resources are added purely as data. Plain C#, testable in
    /// isolation. See Docs/Systems/RESOURCE_SYSTEM.md.
    /// </summary>
    public sealed class ResourceSet : IDisposable, ITickingCapability
    {
        private readonly Dictionary<DefinitionId, ResourceValue> _resources =
            new Dictionary<DefinitionId, ResourceValue>();

        private readonly List<ResourceValue> _ordered = new List<ResourceValue>();
        private readonly IEventBus _eventBus;
        private readonly IAttributeProvider _attributes;
        private readonly GameplayObjectId _owner;

        /// <summary>
        /// Creates a resource set.
        /// </summary>
        /// <param name="eventBus">When supplied, resource changes publish events; null runs silently.</param>
        /// <param name="attributes">
        /// Source for resolving attribute-bound maximums. Required only if bound resources are added.
        /// </param>
        /// <param name="owner">Identifies the composed object in published events; default when unowned.</param>
        public ResourceSet(IEventBus eventBus = null, IAttributeProvider attributes = null, GameplayObjectId owner = default)
        {
            _eventBus = eventBus;
            _attributes = attributes;
            _owner = owner;
        }

        /// <summary>Number of resources in the set.</summary>
        public int Count => _resources.Count;

        /// <summary>
        /// The resources in deterministic registration order, for save capture and tooling.
        /// Each <see cref="ResourceValue"/> exposes its definition id and authoritative current
        /// value; the maximum is derived and never persisted (Engine Principle 25).
        /// </summary>
        public IReadOnlyList<ResourceValue> Resources => _ordered;

        /// <summary>
        /// Adds a resource from its definition and returns the runtime value.
        /// Bound resources resolve their maximum attribute through the provider supplied at
        /// construction. Throws if the id already exists or a required binding cannot be resolved.
        /// </summary>
        public ResourceValue AddResource(ResourceDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            DefinitionId id = definition.Id;
            if (_resources.ContainsKey(id))
            {
                throw new InvalidOperationException(
                    $"Resource '{id}' is already present in this set. Each resource is owned once per object.");
            }

            ResourceValue value = definition.HasMaximumBinding
                ? new ResourceValue(definition, ResolveBoundMaximum(definition))
                : new ResourceValue(definition);

            value.ValueChanged += (previous, next, max) =>
                _eventBus?.Publish(new ResourceChanged(_owner, id, previous, next, max));
            value.Depleted += () => _eventBus?.Publish(new ResourceDepleted(_owner, id));
            value.Filled += max => _eventBus?.Publish(new ResourceFull(_owner, id, max));

            _resources.Add(id, value);
            _ordered.Add(value);
            return value;
        }

        /// <summary>Whether a resource with this id is present.</summary>
        public bool HasResource(DefinitionId resource) => _resources.ContainsKey(resource);

        /// <summary>The runtime resource, or null when not present.</summary>
        public ResourceValue GetResource(DefinitionId resource)
        {
            return _resources.TryGetValue(resource, out ResourceValue value) ? value : null;
        }

        /// <summary>
        /// <see cref="ITickingCapability"/> entry point: regeneration is this capability's
        /// time-driven behavior. Delegates to <see cref="Regenerate"/>.
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            Regenerate(deltaSeconds);
        }

        /// <summary>
        /// Applies continuous regeneration to every resource for <paramref name="deltaSeconds"/>,
        /// in deterministic registration order (Engine Principle 17) so published
        /// <see cref="ResourceChanged"/> ordering is stable for replay and multiplayer.
        /// The owning gameplay object calls this from its update; the system never reads the clock.
        /// </summary>
        public void Regenerate(float deltaSeconds)
        {
            for (int i = 0; i < _ordered.Count; i++)
            {
                _ordered[i].Regenerate(deltaSeconds);
            }
        }

        /// <summary>Disposes every resource, detaching attribute bindings.</summary>
        public void Dispose()
        {
            for (int i = 0; i < _ordered.Count; i++)
            {
                _ordered[i].Dispose();
            }

            _resources.Clear();
            _ordered.Clear();
        }

        private AttributeValue ResolveBoundMaximum(ResourceDefinition definition)
        {
            if (_attributes == null)
            {
                throw new InvalidOperationException(
                    $"Resource '{definition.Id}' binds its maximum to attribute " +
                    $"'{definition.MaximumAttribute}', but this ResourceSet was created without an " +
                    "attribute provider. Supply one to add bound resources.");
            }

            AttributeValue maximum = _attributes.GetAttribute(definition.MaximumAttribute);
            if (maximum == null)
            {
                throw new InvalidOperationException(
                    $"Resource '{definition.Id}' binds its maximum to attribute " +
                    $"'{definition.MaximumAttribute}', which is not present on this object. " +
                    "Add the attribute before the bound resource.");
            }

            return maximum;
        }
    }
}
