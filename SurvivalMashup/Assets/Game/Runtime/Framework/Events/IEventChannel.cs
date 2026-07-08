namespace ToyChest.Framework.Events
{
    /// <summary>
    /// Non-generic view of a per-event-type channel, used by the bus to store
    /// heterogeneous channels and by diagnostics to report subscriber counts.
    /// </summary>
    internal interface IEventChannel
    {
        /// <summary>Number of live (non-disposed) subscriptions on this channel.</summary>
        int SubscriberCount { get; }
    }
}
