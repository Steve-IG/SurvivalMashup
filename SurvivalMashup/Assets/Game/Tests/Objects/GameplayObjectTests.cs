using System;
using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Objects;
using ToyChest.Tests.Events;

namespace ToyChest.Tests.Objects
{
    /// <summary>
    /// Verifies the GameplayObject container: capability queries, sealed composition,
    /// lifecycle guards, tick fan-out, lifecycle events, and disposal on destroy.
    /// </summary>
    public sealed class GameplayObjectTests
    {
        private sealed class FakeCapability : IGameplayCapability
        {
        }

        private sealed class FakeTickingCapability : ITickingCapability, IDisposable
        {
            public readonly List<float> Ticks = new List<float>();
            public bool Disposed;

            public void Tick(float deltaSeconds) => Ticks.Add(deltaSeconds);

            public void Dispose() => Disposed = true;
        }

        private static readonly DefinitionId TestDefinition = new DefinitionId("object.test");

        private static GameplayObject CreateObject(IEventBus bus, params IGameplayCapability[] capabilities)
        {
            return new GameplayObject(GameplayObjectId.New(), TestDefinition, bus, capabilities);
        }

        [Test]
        public void Constructor_RequiresValidId()
        {
            Assert.Throws<ArgumentException>(
                () => new GameplayObject(default, TestDefinition, null, Array.Empty<IGameplayCapability>()));
        }

        [Test]
        public void Constructor_RejectsDuplicateCapabilityTypes()
        {
            Assert.Throws<ArgumentException>(
                () => CreateObject(null, new FakeCapability(), new FakeCapability()));
        }

        [Test]
        public void CapabilityQueries_WorkByExactType()
        {
            var fake = new FakeCapability();
            GameplayObject gameplayObject = CreateObject(null, fake);

            Assert.IsTrue(gameplayObject.Has<FakeCapability>());
            Assert.AreSame(fake, gameplayObject.Get<FakeCapability>());
            Assert.IsTrue(gameplayObject.TryGet(out FakeCapability found));
            Assert.AreSame(fake, found);

            Assert.IsFalse(gameplayObject.Has<FakeTickingCapability>());
            Assert.IsFalse(gameplayObject.TryGet(out FakeTickingCapability _));
            Assert.Throws<KeyNotFoundException>(() => gameplayObject.Get<FakeTickingCapability>());
        }

        [Test]
        public void Activate_PublishesSpawned()
        {
            var bus = new EventBus(new RecordingLogger());
            var spawned = new List<GameplayObjectSpawned>();
            using IDisposable token = bus.Subscribe<GameplayObjectSpawned>(spawned.Add);

            GameplayObject gameplayObject = CreateObject(bus);
            gameplayObject.Activate();

            Assert.AreEqual(1, spawned.Count);
            Assert.AreEqual(gameplayObject.Id, spawned[0].Object);
            Assert.AreEqual(TestDefinition, spawned[0].Definition);
            Assert.IsTrue(gameplayObject.IsActive);
        }

        [Test]
        public void Activate_Twice_Throws()
        {
            GameplayObject gameplayObject = CreateObject(null);
            gameplayObject.Activate();

            Assert.Throws<InvalidOperationException>(gameplayObject.Activate);
        }

        [Test]
        public void Tick_FansOutToTickingCapabilitiesOnly()
        {
            var ticking = new FakeTickingCapability();
            GameplayObject gameplayObject = CreateObject(null, new FakeCapability(), ticking);
            gameplayObject.Activate();

            gameplayObject.Tick(0.25f);
            gameplayObject.Tick(0.5f);

            CollectionAssert.AreEqual(new[] { 0.25f, 0.5f }, ticking.Ticks);
        }

        [Test]
        public void Tick_BeforeActivate_Throws()
        {
            GameplayObject gameplayObject = CreateObject(null, new FakeTickingCapability());
            Assert.Throws<InvalidOperationException>(() => gameplayObject.Tick(0.1f));
        }

        [Test]
        public void Destroy_PublishesDestroyed_DisposesCapabilities_AndIsIdempotent()
        {
            var bus = new EventBus(new RecordingLogger());
            var destroyed = new List<GameplayObjectDestroyed>();
            using IDisposable token = bus.Subscribe<GameplayObjectDestroyed>(destroyed.Add);

            var ticking = new FakeTickingCapability();
            GameplayObject gameplayObject = CreateObject(bus, ticking);
            gameplayObject.Activate();

            gameplayObject.Destroy();
            gameplayObject.Destroy();

            Assert.AreEqual(1, destroyed.Count, "Destroy must be idempotent.");
            Assert.IsTrue(ticking.Disposed);
            Assert.IsTrue(gameplayObject.IsDestroyed);
            Assert.IsFalse(gameplayObject.IsActive);
        }

        [Test]
        public void Tick_AfterDestroy_Throws()
        {
            GameplayObject gameplayObject = CreateObject(null, new FakeTickingCapability());
            gameplayObject.Activate();
            gameplayObject.Destroy();

            Assert.Throws<InvalidOperationException>(() => gameplayObject.Tick(0.1f));
        }

        [Test]
        public void Activate_AfterDestroy_Throws()
        {
            GameplayObject gameplayObject = CreateObject(null);
            gameplayObject.Activate();
            gameplayObject.Destroy();

            Assert.Throws<InvalidOperationException>(gameplayObject.Activate);
        }
    }
}
