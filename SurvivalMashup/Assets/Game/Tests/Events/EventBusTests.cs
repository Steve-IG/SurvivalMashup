using System;
using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Framework.Events;

namespace ToyChest.Tests.Events
{
    /// <summary>
    /// Verifies the Event Bus contract defined in Docs/Architecture/EVENT_SYSTEM.md:
    /// deterministic subscription-order dispatch, deferred mutation during dispatch,
    /// depth-first nested publishing, error isolation, and safe token disposal.
    /// </summary>
    public sealed class EventBusTests
    {
        private RecordingLogger _logger;
        private EventBus _bus;

        [SetUp]
        public void SetUp()
        {
            _logger = new RecordingLogger();
            _bus = new EventBus(_logger);
        }

        [Test]
        public void Constructor_RequiresLogger()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new EventBus(null));
        }

        [Test]
        public void Subscribe_RequiresHandler()
        {
            Assert.Throws<ArgumentNullException>(() => _bus.Subscribe<NumberReported>(null));
        }

        [Test]
        public void Publish_DeliversEventToSubscriber()
        {
            var received = new List<NumberReported>();
            using IDisposable token = _bus.Subscribe<NumberReported>(received.Add);

            _bus.Publish(new NumberReported(7));

            Assert.AreEqual(1, received.Count);
            Assert.AreEqual(7, received[0].Value);
        }

        [Test]
        public void Publish_DeliversInSubscriptionOrder()
        {
            var order = new List<string>();
            using IDisposable first = _bus.Subscribe<NumberReported>(_ => order.Add("first"));
            using IDisposable second = _bus.Subscribe<NumberReported>(_ => order.Add("second"));
            using IDisposable third = _bus.Subscribe<NumberReported>(_ => order.Add("third"));

            _bus.Publish(new NumberReported(1));

            CollectionAssert.AreEqual(new[] { "first", "second", "third" }, order);
        }

        [Test]
        public void Publish_WithNoSubscribers_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _bus.Publish(new NumberReported(1)));
        }

        [Test]
        public void Publish_EventTypes_AreIsolated()
        {
            int numbers = 0;
            int flags = 0;
            using IDisposable numberToken = _bus.Subscribe<NumberReported>(_ => numbers++);
            using IDisposable flagToken = _bus.Subscribe<FlagRaised>(_ => flags++);

            _bus.Publish(new NumberReported(1));

            Assert.AreEqual(1, numbers);
            Assert.AreEqual(0, flags);
        }

        [Test]
        public void Dispose_StopsDelivery()
        {
            int received = 0;
            IDisposable token = _bus.Subscribe<NumberReported>(_ => received++);

            _bus.Publish(new NumberReported(1));
            token.Dispose();
            _bus.Publish(new NumberReported(2));

            Assert.AreEqual(1, received);
        }

        [Test]
        public void Dispose_Twice_IsSafe()
        {
            IDisposable token = _bus.Subscribe<NumberReported>(_ => { });
            token.Dispose();

            Assert.DoesNotThrow(token.Dispose);
        }

        [Test]
        public void Subscribe_DuringDispatch_DoesNotReceiveInFlightEvent()
        {
            int lateHandlerCalls = 0;
            IDisposable lateToken = null;
            using IDisposable outerToken = _bus.Subscribe<NumberReported>(_ =>
            {
                lateToken ??= _bus.Subscribe<NumberReported>(_ => lateHandlerCalls++);
            });

            _bus.Publish(new NumberReported(1));
            Assert.AreEqual(0, lateHandlerCalls, "Handler subscribed mid-dispatch must not see the in-flight event.");

            _bus.Publish(new NumberReported(2));
            Assert.AreEqual(1, lateHandlerCalls, "Handler subscribed mid-dispatch must see subsequent events.");

            lateToken?.Dispose();
        }

        [Test]
        public void Dispose_DuringDispatch_PreventsLaterDelivery()
        {
            int victimCalls = 0;
            IDisposable victimToken = null;
            using IDisposable killerToken = _bus.Subscribe<NumberReported>(_ => victimToken?.Dispose());
            victimToken = _bus.Subscribe<NumberReported>(_ => victimCalls++);

            _bus.Publish(new NumberReported(1));

            Assert.AreEqual(0, victimCalls, "A handler disposed earlier in the same dispatch must not fire.");
        }

        [Test]
        public void NestedPublish_DispatchesDepthFirst()
        {
            var order = new List<string>();
            using IDisposable flagToken = _bus.Subscribe<FlagRaised>(_ => order.Add("nested"));
            using IDisposable trigger = _bus.Subscribe<NumberReported>(_ =>
            {
                order.Add("outer-before");
                _bus.Publish(new FlagRaised("inner"));
                order.Add("outer-after");
            });
            using IDisposable second = _bus.Subscribe<NumberReported>(_ => order.Add("outer-second"));

            _bus.Publish(new NumberReported(1));

            CollectionAssert.AreEqual(
                new[] { "outer-before", "nested", "outer-after", "outer-second" },
                order);
        }

        [Test]
        public void HandlerException_IsIsolatedAndLogged()
        {
            int survivorCalls = 0;
            using IDisposable throwing = _bus.Subscribe<NumberReported>(_ => throw new InvalidOperationException("boom"));
            using IDisposable survivor = _bus.Subscribe<NumberReported>(_ => survivorCalls++);

            Assert.DoesNotThrow(() => _bus.Publish(new NumberReported(1)));
            Assert.AreEqual(1, survivorCalls, "Handlers after a throwing handler must still receive the event.");
            Assert.AreEqual(1, _logger.Errors.Count);
            StringAssert.Contains("NumberReported", _logger.Errors[0]);
            Assert.IsInstanceOf<InvalidOperationException>(_logger.Exceptions[0]);
        }

        [Test]
        public void EventCycle_FailsClearlyWithoutStackOverflow()
        {
            int invocations = 0;
            using IDisposable recursive = _bus.Subscribe<NumberReported>(evt =>
            {
                invocations++;
                _bus.Publish(new NumberReported(evt.Value + 1));
            });

            Assert.DoesNotThrow(() => _bus.Publish(new NumberReported(0)));
            Assert.Greater(_logger.Errors.Count, 0, "The cycle guard must report the runaway publish chain.");
            Assert.LessOrEqual(invocations, 33, "The depth guard must stop the cycle near the documented limit.");
        }
    }
}
