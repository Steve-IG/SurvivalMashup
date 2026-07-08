using System;
using System.Collections.Generic;
using ToyChest.Framework.Data;
using ToyChest.Framework.Events;

namespace ToyChest.Framework.Objects
{
    /// <summary>
    /// The fundamental runtime entity of the ToyChest Architecture: a lightweight plain-C#
    /// container of capability components with a stable identity and a definition reference.
    /// Contains no gameplay rules — capabilities own behavior, this object owns composition
    /// and shared lifecycle. Composition is sealed at construction: capabilities are supplied
    /// by the composition root (GameplayObjectFactory) and cannot be added to a live object.
    /// Lifecycle: composed → Activate (publishes Spawned) → Tick while active → Destroy
    /// (publishes Destroyed, disposes capabilities; idempotent).
    /// See Docs/Systems/GAMEPLAY_FRAMEWORK.md.
    /// </summary>
    public sealed class GameplayObject
    {
        private enum LifecycleState
        {
            Composed,
            Active,
            Destroyed,
        }

        private readonly Dictionary<Type, IGameplayCapability> _capabilities =
            new Dictionary<Type, IGameplayCapability>();

        private readonly List<ITickingCapability> _tickingCapabilities = new List<ITickingCapability>();
        private readonly GameplayObjectEventGate _events;
        private readonly GameplayObjectRegistry _registry;
        private LifecycleState _state = LifecycleState.Composed;

        /// <summary>
        /// Creates a composed object. Called by the composition root; gameplay code obtains
        /// objects from the factory, never from this constructor.
        /// </summary>
        /// <param name="id">The object's persistent identity. Must be valid.</param>
        /// <param name="definitionId">Stable id of the definition the object was composed from.</param>
        /// <param name="eventBus">
        /// This object's event boundary. The composition root passes the same
        /// <see cref="GameplayObjectEventGate"/> it gave the capabilities, so the whole object
        /// shares one gate and construction is event-quiet (Engine Principle 26); that gate is
        /// adopted directly. Any other bus (isolated tests) is wrapped in a private gate so the
        /// object still controls its own boundary. Null runs silently.
        /// </param>
        /// <param name="capabilities">The full capability set; at most one per concrete type.</param>
        /// <param name="registry">
        /// When supplied, the object registers itself here on activation and unregisters on
        /// destroy, so the registry holds exactly the live objects. Null in isolated tests.
        /// </param>
        public GameplayObject(
            GameplayObjectId id,
            DefinitionId definitionId,
            IEventBus eventBus,
            IEnumerable<IGameplayCapability> capabilities,
            GameplayObjectRegistry registry = null)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException("A GameplayObject requires a valid id. Use GameplayObjectId.New().", nameof(id));
            }

            if (capabilities == null)
            {
                throw new ArgumentNullException(nameof(capabilities));
            }

            Id = id;
            DefinitionId = definitionId;
            _events = eventBus as GameplayObjectEventGate ?? new GameplayObjectEventGate(eventBus);
            _registry = registry;

            foreach (IGameplayCapability capability in capabilities)
            {
                if (capability == null)
                {
                    throw new ArgumentException("Capability list contains null.", nameof(capabilities));
                }

                Type type = capability.GetType();
                if (_capabilities.ContainsKey(type))
                {
                    throw new ArgumentException(
                        $"Duplicate capability '{type.Name}'. A GameplayObject holds at most one instance per capability type.",
                        nameof(capabilities));
                }

                _capabilities.Add(type, capability);
                if (capability is ITickingCapability ticking)
                {
                    _tickingCapabilities.Add(ticking);
                }
            }
        }

        /// <summary>The object's stable persistent identity.</summary>
        public GameplayObjectId Id { get; }

        /// <summary>Stable id of the definition this object was composed from.</summary>
        public DefinitionId DefinitionId { get; }

        /// <summary>Whether the object is live (activated and not destroyed).</summary>
        public bool IsActive => _state == LifecycleState.Active;

        /// <summary>Whether the object has been torn down.</summary>
        public bool IsDestroyed => _state == LifecycleState.Destroyed;

        /// <summary>Number of composed capabilities.</summary>
        public int CapabilityCount => _capabilities.Count;

        /// <summary>Whether a capability of the exact type is composed onto this object.</summary>
        public bool Has<TCapability>() where TCapability : class, IGameplayCapability
        {
            return _capabilities.ContainsKey(typeof(TCapability));
        }

        /// <summary>
        /// The capability of the exact type. Throws when absent — asking for a capability the
        /// object does not have is a programming error; probe with <see cref="TryGet{T}"/>.
        /// </summary>
        public TCapability Get<TCapability>() where TCapability : class, IGameplayCapability
        {
            if (_capabilities.TryGetValue(typeof(TCapability), out IGameplayCapability capability))
            {
                return (TCapability)capability;
            }

            throw new KeyNotFoundException(
                $"GameplayObject '{Id}' ({DefinitionId}) has no '{typeof(TCapability).Name}' capability. " +
                "Check the object's definition, or use TryGet/Has for optional capabilities.");
        }

        /// <summary>Tolerant capability lookup for genuinely optional capabilities.</summary>
        public bool TryGet<TCapability>(out TCapability capability) where TCapability : class, IGameplayCapability
        {
            if (_capabilities.TryGetValue(typeof(TCapability), out IGameplayCapability found))
            {
                capability = (TCapability)found;
                return true;
            }

            capability = null;
            return false;
        }

        /// <summary>
        /// Makes the object live and publishes <see cref="GameplayObjectSpawned"/>.
        /// Called by the spawner after placement. Activating twice or activating a destroyed
        /// object is a programming error and throws.
        /// </summary>
        public void Activate()
        {
            if (_state != LifecycleState.Composed)
            {
                throw new InvalidOperationException(
                    $"GameplayObject '{Id}' cannot activate from state '{_state}'. " +
                    "Activate exactly once, after composition and before destruction.");
            }

            _state = LifecycleState.Active;

            // Open the event boundary: construction and restoration were event-quiet; observable
            // gameplay begins here (Engine Principle 26, Construction Before Participation).
            _events.Open();

            // Register before announcing the spawn, so a listener reacting to Spawned already
            // sees this object among the live set.
            _registry?.Register(this);
            _events.Publish(new GameplayObjectSpawned(Id, DefinitionId));
        }

        /// <summary>
        /// Advances every ticking capability by <paramref name="deltaSeconds"/>.
        /// Valid only while active; ticking a non-active object is a programming error.
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (_state != LifecycleState.Active)
            {
                throw new InvalidOperationException(
                    $"GameplayObject '{Id}' cannot tick in state '{_state}'. Activate it first and do not tick after destroy.");
            }

            for (int i = 0; i < _tickingCapabilities.Count; i++)
            {
                _tickingCapabilities[i].Tick(deltaSeconds);
            }
        }

        /// <summary>
        /// Tears the object down: publishes <see cref="GameplayObjectDestroyed"/>, then disposes
        /// disposable capabilities. Idempotent, because engine teardown paths can overlap.
        /// </summary>
        public void Destroy()
        {
            if (_state == LifecycleState.Destroyed)
            {
                return;
            }

            _state = LifecycleState.Destroyed;
            _events.Publish(new GameplayObjectDestroyed(Id, DefinitionId));

            // Unregister after announcing the teardown: the object is a registered member for
            // exactly the interval [Spawned published .. Destroyed published], so a Destroyed
            // listener can still resolve it before it leaves the live set.
            _registry?.Unregister(Id);

            // Close the boundary before disposing capabilities: teardown has already been
            // announced by GameplayObjectDestroyed, so capability cleanup (a status revoking its
            // tags and modifiers, a resource detaching its binding) is event-quiet, mirroring the
            // event-quiet construction on the other end of the lifecycle (Engine Principle 26).
            _events.Close();

            foreach (KeyValuePair<Type, IGameplayCapability> pair in _capabilities)
            {
                if (pair.Value is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
    }
}
