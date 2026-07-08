using System;
using System.Collections.Generic;
using ToyChest.Core.Logging;

namespace ToyChest.Framework.Events
{
    /// <summary>
    /// The game-wide event transport. Dispatches typed gameplay events synchronously on the
    /// main thread with deterministic subscription-order delivery and allocation-free
    /// steady-state publishing. Plain C#: no scene, MonoBehaviour, or engine lifecycle
    /// dependency, so it is fully testable outside Unity play mode.
    /// Create one instance at bootstrap and provide it through constructor injection;
    /// there is no global singleton. See Docs/Architecture/EVENT_SYSTEM.md.
    /// </summary>
    public sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, IEventChannel> _channels = new Dictionary<Type, IEventChannel>();
        private readonly IGameLogger _logger;
        private readonly IEventBusDiagnostics _diagnostics;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly int _ownerThreadId;
#endif

        /// <summary>
        /// Creates a bus that reports handler failures through <paramref name="logger"/>.
        /// </summary>
        /// <param name="logger">Receives handler failure reports. Required.</param>
        /// <param name="diagnostics">
        /// Optional debug observer for editor and development tooling. Production passes null.
        /// </param>
        public EventBus(IGameLogger logger, IEventBusDiagnostics diagnostics = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _diagnostics = diagnostics;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _ownerThreadId = Environment.CurrentManagedThreadId;
#endif
        }

        /// <inheritdoc />
        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct, IGameplayEvent
        {
            AssertOwnerThread();
            return GetOrCreateChannel<TEvent>().Subscribe(handler);
        }

        /// <inheritdoc />
        public void Publish<TEvent>(in TEvent gameplayEvent) where TEvent : struct, IGameplayEvent
        {
            AssertOwnerThread();

            EventChannel<TEvent> channel = null;
            int subscriberCount = 0;
            if (_channels.TryGetValue(typeof(TEvent), out IEventChannel untypedChannel))
            {
                channel = (EventChannel<TEvent>)untypedChannel;
                subscriberCount = channel.SubscriberCount;
            }

            // Recorded before dispatch so nested (depth-first) publishes appear after their
            // cause in the trace, keeping causality chains readable.
            _diagnostics?.OnEventPublished(in gameplayEvent, subscriberCount);

            channel?.Publish(in gameplayEvent);
        }

        internal void NotifySubscriptionAdded(Type eventType, int subscriberCount)
        {
            _diagnostics?.OnSubscriptionAdded(eventType, subscriberCount);
        }

        internal void NotifySubscriptionRemoved(Type eventType, int subscriberCount)
        {
            _diagnostics?.OnSubscriptionRemoved(eventType, subscriberCount);
        }

        internal void NotifyHandlerFailed(Type eventType, Exception exception)
        {
            _diagnostics?.OnHandlerFailed(eventType, exception);
        }

        private EventChannel<TEvent> GetOrCreateChannel<TEvent>() where TEvent : struct, IGameplayEvent
        {
            if (_channels.TryGetValue(typeof(TEvent), out IEventChannel existing))
            {
                return (EventChannel<TEvent>)existing;
            }

            var channel = new EventChannel<TEvent>(this, _logger);
            _channels.Add(typeof(TEvent), channel);
            return channel;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void AssertOwnerThread()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Environment.CurrentManagedThreadId != _ownerThreadId)
            {
                throw new InvalidOperationException(
                    "EventBus was accessed from a background thread. The bus is main-thread only; " +
                    "marshal back to the main thread before subscribing or publishing. " +
                    "Check for event usage inside async continuations, tasks, or worker threads.");
            }
#endif
        }
    }
}
