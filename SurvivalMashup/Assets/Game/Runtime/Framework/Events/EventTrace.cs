#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Reflection;

namespace ToyChest.Framework.Events
{
    /// <summary>
    /// Debug observer that records recent event dispatches into a fixed-size ring buffer
    /// and tracks live subscription counts per event type. Feeds the event trace tooling;
    /// compiled into editor and development builds only, so release builds pay nothing.
    /// Because dispatch is depth-first and entries are recorded in dispatch order,
    /// causality chains can be reconstructed directly from the trace.
    /// </summary>
    public sealed class EventTrace : IEventBusDiagnostics
    {
        private const string UncategorizedLabel = "Uncategorized";

        private static readonly Dictionary<Type, string> CategoryCache = new Dictionary<Type, string>();

        private readonly EventTraceEntry[] _entries;
        private readonly Dictionary<Type, int> _subscriberCounts = new Dictionary<Type, int>();
        private readonly Func<int> _frameProvider;
        private readonly Func<float> _timeProvider;

        private int _nextIndex;
        private int _recordedCount;

        /// <summary>Total number of handler exceptions observed since creation.</summary>
        public int HandlerFailureCount { get; private set; }

        /// <summary>
        /// Creates a trace holding the most recent <paramref name="capacity"/> events.
        /// </summary>
        /// <param name="capacity">Ring buffer size; must be positive.</param>
        /// <param name="frameProvider">
        /// Source of frame numbers. Defaults to <see cref="UnityEngine.Time.frameCount"/>;
        /// tests inject their own to stay engine-independent.
        /// </param>
        /// <param name="timeProvider">
        /// Source of elapsed seconds. Defaults to <see cref="UnityEngine.Time.realtimeSinceStartup"/>.
        /// </param>
        public EventTrace(int capacity = 256, Func<int> frameProvider = null, Func<float> timeProvider = null)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Trace capacity must be positive.");
            }

            _entries = new EventTraceEntry[capacity];
            _frameProvider = frameProvider ?? (() => UnityEngine.Time.frameCount);
            _timeProvider = timeProvider ?? (() => UnityEngine.Time.realtimeSinceStartup);
        }

        /// <summary>Live subscription counts per event type, for the subscription table tooling.</summary>
        public IReadOnlyDictionary<Type, int> SubscriberCounts => _subscriberCounts;

        /// <inheritdoc />
        public void OnEventPublished<TEvent>(in TEvent gameplayEvent, int subscriberCount)
            where TEvent : struct, IGameplayEvent
        {
            _entries[_nextIndex] = new EventTraceEntry(
                typeof(TEvent),
                ResolveCategory(typeof(TEvent)),
                _frameProvider(),
                _timeProvider(),
                subscriberCount,
                gameplayEvent.ToString());

            _nextIndex = (_nextIndex + 1) % _entries.Length;
            if (_recordedCount < _entries.Length)
            {
                _recordedCount++;
            }
        }

        /// <inheritdoc />
        public void OnSubscriptionAdded(Type eventType, int subscriberCount)
        {
            _subscriberCounts[eventType] = subscriberCount;
        }

        /// <inheritdoc />
        public void OnSubscriptionRemoved(Type eventType, int subscriberCount)
        {
            if (subscriberCount <= 0)
            {
                _subscriberCounts.Remove(eventType);
            }
            else
            {
                _subscriberCounts[eventType] = subscriberCount;
            }
        }

        /// <inheritdoc />
        public void OnHandlerFailed(Type eventType, Exception exception)
        {
            HandlerFailureCount++;
        }

        /// <summary>
        /// Copies the recorded entries, oldest first, into a new list.
        /// Intended for tooling; not for per-frame gameplay use.
        /// </summary>
        public List<EventTraceEntry> GetEntries()
        {
            var result = new List<EventTraceEntry>(_recordedCount);
            int start = (_nextIndex - _recordedCount + _entries.Length) % _entries.Length;
            for (int i = 0; i < _recordedCount; i++)
            {
                result.Add(_entries[(start + i) % _entries.Length]);
            }

            return result;
        }

        private static string ResolveCategory(Type eventType)
        {
            if (CategoryCache.TryGetValue(eventType, out string cached))
            {
                return cached;
            }

            var attribute = eventType.GetCustomAttribute<EventCategoryAttribute>();
            string category = attribute?.Category ?? UncategorizedLabel;
            CategoryCache[eventType] = category;
            return category;
        }
    }
}
#endif
