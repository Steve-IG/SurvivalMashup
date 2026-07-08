using System;
using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Framework.Events;
using ToyChest.Framework.Objects;
using ToyChest.Systems.Tags;
using ToyChest.Tests.Events;

namespace ToyChest.Tests.Tags
{
    /// <summary>
    /// Verifies the tag event bridge: a container composed with an Event Bus publishes
    /// GameplayTagAdded / GameplayTagRemoved on the absent/present transitions only (silent
    /// while reference counts overlap), attributes them to the owner, and enumerates tags in
    /// deterministic registration order.
    /// </summary>
    public sealed class GameplayTagEventBridgeTests
    {
        private GameplayTagTable _table;
        private EventBus _bus;
        private GameplayObjectId _owner;
        private GameplayTagContainer _container;
        private GameplayTag _burning;
        private GameplayTag _frozen;

        [SetUp]
        public void SetUp()
        {
            _table = new GameplayTagTable();
            _bus = new EventBus(new RecordingLogger());
            _owner = GameplayObjectId.New();
            _container = new GameplayTagContainer(_table, _bus, _owner);
            _burning = _table.RegisterTag("State.Burning");
            _frozen = _table.RegisterTag("State.Frozen");
        }

        [Test]
        public void AddTag_PublishesAdded_OnceOnTransition_AttributedToOwner()
        {
            var added = new List<GameplayTagAdded>();
            using IDisposable token = _bus.Subscribe<GameplayTagAdded>(added.Add);

            _container.AddTag(_burning);
            _container.AddTag(_burning); // second source; still present, no new transition.

            Assert.AreEqual(1, added.Count);
            Assert.AreEqual(_owner, added[0].Owner);
            Assert.AreEqual(_burning, added[0].Tag);
        }

        [Test]
        public void RemoveTag_PublishesRemoved_OnlyWhenLastSourceLeaves()
        {
            var removed = new List<GameplayTagRemoved>();
            using IDisposable token = _bus.Subscribe<GameplayTagRemoved>(removed.Add);

            _container.AddTag(_burning);
            _container.AddTag(_burning);

            _container.RemoveTag(_burning);
            Assert.AreEqual(0, removed.Count, "One source remains; the tag is still present.");

            _container.RemoveTag(_burning);
            Assert.AreEqual(1, removed.Count, "Last source leaving publishes the removal.");
            Assert.AreEqual(_burning, removed[0].Tag);
        }

        [Test]
        public void CopyTagsTo_IsDeterministicRegistrationOrder()
        {
            _container.AddTag(_frozen);
            _container.AddTag(_burning);

            var first = new List<GameplayTag>();
            _container.CopyTagsTo(first);
            Assert.AreEqual(new[] { _frozen, _burning }, first);

            // Removing and re-adding places the tag at the end of registration order.
            _container.RemoveTag(_frozen);
            _container.AddTag(_frozen);

            var second = new List<GameplayTag>();
            _container.CopyTagsTo(second);
            Assert.AreEqual(new[] { _burning, _frozen }, second);
        }

        [Test]
        public void NullBus_RunsSilently_WithLocalCallbacksOnly()
        {
            var silent = new GameplayTagContainer(_table);
            var localAdds = new List<GameplayTag>();
            silent.TagAdded += localAdds.Add;

            Assert.DoesNotThrow(() => silent.AddTag(_burning));
            Assert.AreEqual(1, localAdds.Count, "Local C# callback still fires without a bus.");
        }
    }
}
