#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace ToyChest.Framework.Events
{
    /// <summary>
    /// One recorded event dispatch in the <see cref="EventTrace"/> ring buffer.
    /// Entries are diagnostic data only; they never participate in gameplay.
    /// </summary>
    public readonly struct EventTraceEntry
    {
        /// <summary>The concrete event type that was published.</summary>
        public readonly Type EventType;

        /// <summary>The event's declared category, or "Uncategorized" when none is declared.</summary>
        public readonly string Category;

        /// <summary>Frame number at publication time.</summary>
        public readonly int Frame;

        /// <summary>Elapsed time in seconds at publication time.</summary>
        public readonly float Time;

        /// <summary>Number of subscribers the event was dispatched to.</summary>
        public readonly int SubscriberCount;

        /// <summary>Payload summary produced by the event's ToString implementation.</summary>
        public readonly string PayloadSummary;

        public EventTraceEntry(Type eventType, string category, int frame, float time, int subscriberCount, string payloadSummary)
        {
            EventType = eventType;
            Category = category;
            Frame = frame;
            Time = time;
            SubscriberCount = subscriberCount;
            PayloadSummary = payloadSummary;
        }
    }
}
#endif
