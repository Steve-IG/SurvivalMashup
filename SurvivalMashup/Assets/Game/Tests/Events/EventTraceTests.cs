using System;
using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Framework.Events;

namespace ToyChest.Tests.Events
{
    /// <summary>
    /// Verifies the diagnostic Event Trace: dispatch-order recording, ring-buffer eviction,
    /// category resolution, subscription tracking, and failure counting.
    /// </summary>
    public sealed class EventTraceTests
    {
        private RecordingLogger _logger;
        private EventTrace _trace;
        private EventBus _bus;
        private int _frame;

        [SetUp]
        public void SetUp()
        {
            _logger = new RecordingLogger();
            _frame = 0;
            _trace = new EventTrace(capacity: 4, frameProvider: () => _frame, timeProvider: () => _frame * 0.5f);
            _bus = new EventBus(_logger, _trace);
        }

        [Test]
        public void Constructor_RequiresPositiveCapacity()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new EventTrace(capacity: 0));
        }

        [Test]
        public void RecordsEntries_WithCategoryFrameAndSummary()
        {
            _frame = 42;
            _bus.Publish(new NumberReported(9));

            List<EventTraceEntry> entries = _trace.GetEntries();
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(typeof(NumberReported), entries[0].EventType);
            Assert.AreEqual(EventCategories.Resource, entries[0].Category);
            Assert.AreEqual(42, entries[0].Frame);
            Assert.AreEqual(21f, entries[0].Time);
            Assert.AreEqual(0, entries[0].SubscriberCount);
            StringAssert.Contains("9", entries[0].PayloadSummary);
        }

        [Test]
        public void UncategorizedEvents_AreLabeled()
        {
            _bus.Publish(new FlagRaised("test"));

            List<EventTraceEntry> entries = _trace.GetEntries();
            Assert.AreEqual("Uncategorized", entries[0].Category);
        }

        [Test]
        public void RingBuffer_KeepsMostRecentEntriesOldestFirst()
        {
            for (int i = 1; i <= 6; i++)
            {
                _bus.Publish(new NumberReported(i));
            }

            List<EventTraceEntry> entries = _trace.GetEntries();
            Assert.AreEqual(4, entries.Count);
            StringAssert.Contains("3", entries[0].PayloadSummary);
            StringAssert.Contains("6", entries[3].PayloadSummary);
        }

        [Test]
        public void NestedPublishes_AppearAfterTheirCause()
        {
            using IDisposable trigger = _bus.Subscribe<NumberReported>(_ => _bus.Publish(new FlagRaised("effect")));

            _bus.Publish(new NumberReported(1));

            List<EventTraceEntry> entries = _trace.GetEntries();
            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual(typeof(NumberReported), entries[0].EventType, "The cause must precede its effect in the trace.");
            Assert.AreEqual(typeof(FlagRaised), entries[1].EventType);
        }

        [Test]
        public void TracksLiveSubscriberCounts()
        {
            IDisposable first = _bus.Subscribe<NumberReported>(_ => { });
            IDisposable second = _bus.Subscribe<NumberReported>(_ => { });
            Assert.AreEqual(2, _trace.SubscriberCounts[typeof(NumberReported)]);

            first.Dispose();
            Assert.AreEqual(1, _trace.SubscriberCounts[typeof(NumberReported)]);

            second.Dispose();
            Assert.IsFalse(_trace.SubscriberCounts.ContainsKey(typeof(NumberReported)),
                "Fully unsubscribed event types must leave the subscription table.");
        }

        [Test]
        public void CountsHandlerFailures()
        {
            using IDisposable throwing = _bus.Subscribe<NumberReported>(_ => throw new InvalidOperationException("boom"));

            _bus.Publish(new NumberReported(1));

            Assert.AreEqual(1, _trace.HandlerFailureCount);
        }
    }
}
