using System;
using ToyChest.Framework.Events;

namespace ToyChest.Framework.Objects
{
    /// <summary>
    /// One gameplay object's event boundary, and the mechanism behind Construction Before
    /// Participation (Engine Principle 26): while an object is being constructed or restored —
    /// before it activates — the gate is <b>closed</b> and every gameplay event its capabilities
    /// publish is dropped; activation <see cref="Open"/>s the gate and events flow to the real
    /// Event Bus from then on. Construction is therefore event-quiet by lifecycle, not by a
    /// per-method "quiet" flag: capabilities publish exactly as they always do, and observable
    /// gameplay begins at activation.
    ///
    /// This is what unifies spawning, loading, and future streaming under one lifecycle. A fresh
    /// spawn seeds its initial state through the closed gate; a load restores authoritative state
    /// through the closed gate; both open the gate at activation, where the single
    /// <see cref="GameplayObjectSpawned"/> fact announces the fully-formed object. Teardown
    /// closes the gate again before capabilities are disposed, so end-of-life cleanup is quiet
    /// too — the <see cref="GameplayObjectDestroyed"/> fact already announced the teardown.
    ///
    /// Subscriptions always pass through; only publication is gated. The inner bus may be null
    /// (isolated tests), in which case the gate is inert. See Docs/Systems/GAMEPLAY_FRAMEWORK.md,
    /// Object Lifecycle.
    /// </summary>
    public sealed class GameplayObjectEventGate : IEventBus
    {
        private readonly IEventBus _inner;

        /// <summary>Creates a closed gate over <paramref name="inner"/> (which may be null).</summary>
        public GameplayObjectEventGate(IEventBus inner)
        {
            _inner = inner;
        }

        /// <summary>Whether published events currently reach the inner bus.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Opens the gate: publication reaches the inner bus. Called at activation.</summary>
        public void Open() => IsOpen = true;

        /// <summary>Closes the gate: publication is dropped. Called after teardown announces itself.</summary>
        public void Close() => IsOpen = false;

        /// <inheritdoc />
        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct, IGameplayEvent
        {
            return _inner != null ? _inner.Subscribe(handler) : NullSubscription.Instance;
        }

        /// <inheritdoc />
        public void Publish<TEvent>(in TEvent gameplayEvent) where TEvent : struct, IGameplayEvent
        {
            if (IsOpen)
            {
                _inner?.Publish(gameplayEvent);
            }
        }

        private sealed class NullSubscription : IDisposable
        {
            public static readonly NullSubscription Instance = new NullSubscription();

            public void Dispose()
            {
            }
        }
    }
}
