using System;

namespace ToyChest.Framework.Events
{
    /// <summary>
    /// Transports typed gameplay events between systems with deterministic ordering
    /// and zero knowledge of publishers or subscribers.
    /// The bus carries facts that have already occurred; it never decides gameplay outcomes.
    /// One game-wide instance is created at bootstrap and provided through constructor injection.
    /// See Docs/Architecture/EVENT_SYSTEM.md.
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// Registers <paramref name="handler"/> for every future publication of <typeparamref name="TEvent"/>.
        /// Dispatch order within an event type is subscription order.
        /// A handler subscribed during dispatch of the same event type does not receive the in-flight event.
        /// </summary>
        /// <returns>A token that unsubscribes when disposed. Disposing twice is safe.</returns>
        IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct, IGameplayEvent;

        /// <summary>
        /// Dispatches <paramref name="gameplayEvent"/> synchronously on the main thread to all current
        /// subscribers of exactly <typeparamref name="TEvent"/>. Nested publishes dispatch depth-first.
        /// A throwing handler is isolated and logged; remaining handlers still receive the event.
        /// Publishing with zero subscribers is valid and cheap.
        /// </summary>
        void Publish<TEvent>(in TEvent gameplayEvent) where TEvent : struct, IGameplayEvent;
    }
}
