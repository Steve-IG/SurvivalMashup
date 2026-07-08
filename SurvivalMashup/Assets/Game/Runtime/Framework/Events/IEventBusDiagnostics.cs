using System;

namespace ToyChest.Framework.Events
{
    /// <summary>
    /// Bus-level observer for debug visibility. Publishers and subscribers require
    /// no modification to be observable. Implementations must never influence dispatch:
    /// diagnostics are read-only bystanders by contract.
    /// Intended for editor and development builds; production runs without one.
    /// </summary>
    public interface IEventBusDiagnostics
    {
        /// <summary>Called immediately before an event is dispatched to its subscribers.</summary>
        void OnEventPublished<TEvent>(in TEvent gameplayEvent, int subscriberCount) where TEvent : struct, IGameplayEvent;

        /// <summary>Called after a subscription is added for <paramref name="eventType"/>.</summary>
        void OnSubscriptionAdded(Type eventType, int subscriberCount);

        /// <summary>Called after a subscription is disposed for <paramref name="eventType"/>.</summary>
        void OnSubscriptionRemoved(Type eventType, int subscriberCount);

        /// <summary>Called when a handler throws during dispatch, after the exception is logged.</summary>
        void OnHandlerFailed(Type eventType, Exception exception);
    }
}
