using System;
using System.Collections.Generic;
using ToyChest.Core.Logging;

namespace ToyChest.Framework.Events
{
    /// <summary>
    /// Dispatch channel for a single event type.
    /// Owns the subscription list, the cached dispatch snapshot, and the deferred-mutation
    /// rules that keep dispatch deterministic while handlers subscribe, unsubscribe,
    /// or publish recursively. Created by <see cref="EventBus"/> on first use; never shared
    /// between event types.
    /// </summary>
    internal sealed class EventChannel<TEvent> : IEventChannel where TEvent : struct, IGameplayEvent
    {
        // Publishing loops that recurse deeper than this indicate an event cycle
        // (A's handler publishes B, whose handler publishes A, ...). Fail clearly
        // instead of overflowing the stack.
        private const int MaxPublishDepth = 32;

        private readonly List<Subscription> _subscriptions = new List<Subscription>();
        private readonly EventBus _owner;
        private readonly IGameLogger _logger;

        private Subscription[] _dispatchSnapshot = Array.Empty<Subscription>();
        private int _dispatchDepth;
        private bool _snapshotDirty;
        private int _liveCount;

        public EventChannel(EventBus owner, IGameLogger logger)
        {
            _owner = owner;
            _logger = logger;
        }

        /// <inheritdoc />
        public int SubscriberCount => _liveCount;

        /// <summary>
        /// Adds a handler. If a dispatch is in flight, the snapshot rebuild is deferred so the
        /// in-flight event is not delivered to the new handler.
        /// </summary>
        public IDisposable Subscribe(Action<TEvent> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var subscription = new Subscription(this, handler);
            _subscriptions.Add(subscription);
            _liveCount++;
            RebuildSnapshotOrDefer();
            _owner.NotifySubscriptionAdded(typeof(TEvent), _liveCount);
            return subscription;
        }

        /// <summary>
        /// Dispatches to the current snapshot in subscription order. Handlers disposed before
        /// their turn are skipped. A throwing handler is logged and isolated; dispatch continues.
        /// Nested publishes recurse depth-first on the same unchanged snapshot.
        /// </summary>
        public void Publish(in TEvent gameplayEvent)
        {
            if (_dispatchDepth >= MaxPublishDepth)
            {
                throw new InvalidOperationException(
                    $"Event publish depth exceeded {MaxPublishDepth} while dispatching '{typeof(TEvent).Name}'. " +
                    "This indicates an event cycle: check for handlers that publish events which " +
                    "re-trigger their own publishers.");
            }

            _dispatchDepth++;
            try
            {
                Subscription[] snapshot = _dispatchSnapshot;
                for (int i = 0; i < snapshot.Length; i++)
                {
                    Subscription subscription = snapshot[i];
                    if (subscription.IsDisposed)
                    {
                        continue;
                    }

                    try
                    {
                        subscription.Handler(gameplayEvent);
                    }
                    catch (Exception exception)
                    {
                        _logger.Error(
                            $"Event handler threw while receiving '{typeof(TEvent).Name}'. " +
                            "The event was delivered to remaining subscribers. Check the handler " +
                            $"registered by '{subscription.Handler.Method.DeclaringType?.FullName}.{subscription.Handler.Method.Name}'.",
                            exception);
                        _owner.NotifyHandlerFailed(typeof(TEvent), exception);
                    }
                }
            }
            finally
            {
                _dispatchDepth--;
                if (_dispatchDepth == 0 && _snapshotDirty)
                {
                    RebuildSnapshot();
                }
            }
        }

        private void OnSubscriptionDisposed()
        {
            _liveCount--;
            RebuildSnapshotOrDefer();
            _owner.NotifySubscriptionRemoved(typeof(TEvent), _liveCount);
        }

        private void RebuildSnapshotOrDefer()
        {
            if (_dispatchDepth == 0)
            {
                RebuildSnapshot();
            }
            else
            {
                _snapshotDirty = true;
            }
        }

        private void RebuildSnapshot()
        {
            _subscriptions.RemoveAll(subscription => subscription.IsDisposed);
            _dispatchSnapshot = _subscriptions.ToArray();
            _snapshotDirty = false;
        }

        /// <summary>
        /// Unsubscribe token returned by <see cref="Subscribe"/>. Disposing twice is safe.
        /// </summary>
        private sealed class Subscription : IDisposable
        {
            private readonly EventChannel<TEvent> _channel;

            public Subscription(EventChannel<TEvent> channel, Action<TEvent> handler)
            {
                _channel = channel;
                Handler = handler;
            }

            public Action<TEvent> Handler { get; }

            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                if (IsDisposed)
                {
                    return;
                }

                IsDisposed = true;
                _channel.OnSubscriptionDisposed();
            }
        }
    }
}
