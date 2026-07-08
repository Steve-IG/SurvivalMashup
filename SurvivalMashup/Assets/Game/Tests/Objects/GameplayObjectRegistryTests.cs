using System;
using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;

namespace ToyChest.Tests.Objects
{
    /// <summary>
    /// Verifies the Gameplay Object Registry: registration and removal, deterministic
    /// enumeration order preserved across removals, lookup, guard rails, and the object
    /// lifecycle driving membership (register on activate, unregister on destroy).
    /// </summary>
    public sealed class GameplayObjectRegistryTests
    {
        private sealed class FakeCapability : IGameplayCapability
        {
        }

        private static readonly DefinitionId TestDefinition = new DefinitionId("object.test");

        private GameplayObjectRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _registry = new GameplayObjectRegistry();
        }

        private static GameplayObject Compose(GameplayObjectRegistry registry)
        {
            return new GameplayObject(
                GameplayObjectId.New(),
                TestDefinition,
                eventBus: null,
                capabilities: new IGameplayCapability[] { new FakeCapability() },
                registry: registry);
        }

        [Test]
        public void Register_AddsObject_AndReportsMembership()
        {
            GameplayObject obj = Compose(null);

            _registry.Register(obj);

            Assert.AreEqual(1, _registry.Count);
            Assert.IsTrue(_registry.Contains(obj.Id));
            Assert.AreSame(obj, _registry.Get(obj.Id));
            Assert.IsTrue(_registry.TryGet(obj.Id, out GameplayObject found));
            Assert.AreSame(obj, found);
        }

        [Test]
        public void Register_RejectsNull_DuplicateId_AndDestroyed()
        {
            GameplayObject obj = Compose(null);
            _registry.Register(obj);

            Assert.Throws<ArgumentNullException>(() => _registry.Register(null));
            Assert.Throws<InvalidOperationException>(() => _registry.Register(obj), "Same object twice is a duplicate id.");

            GameplayObject destroyed = Compose(null);
            destroyed.Activate();
            destroyed.Destroy();
            Assert.Throws<InvalidOperationException>(() => _registry.Register(destroyed), "Destroyed objects are not live.");
        }

        [Test]
        public void Enumeration_IsRegistrationOrder_PreservedAcrossRemoval()
        {
            GameplayObject a = Compose(null);
            GameplayObject b = Compose(null);
            GameplayObject c = Compose(null);
            _registry.Register(a);
            _registry.Register(b);
            _registry.Register(c);

            Assert.IsTrue(_registry.Unregister(b.Id));

            IReadOnlyList<GameplayObject> objects = _registry.Objects;
            Assert.AreEqual(2, objects.Count);
            Assert.AreSame(a, objects[0], "Order is registration order.");
            Assert.AreSame(c, objects[1], "Removing a middle element does not reorder the survivors.");
        }

        [Test]
        public void Unregister_IsIdempotent_ForUnknownId()
        {
            Assert.IsFalse(_registry.Unregister(GameplayObjectId.New()));
        }

        [Test]
        public void Lifecycle_DrivesMembership()
        {
            GameplayObject obj = Compose(_registry);
            Assert.IsFalse(_registry.Contains(obj.Id), "A composed-but-not-activated object is not yet live.");

            obj.Activate();
            Assert.IsTrue(_registry.Contains(obj.Id), "Activation registers the object.");
            Assert.AreEqual(1, _registry.Count);

            obj.Destroy();
            Assert.IsFalse(_registry.Contains(obj.Id), "Destroy unregisters the object.");
            Assert.AreEqual(0, _registry.Count);
        }

        [Test]
        public void Destroy_IsIdempotent_AgainstRegistry()
        {
            GameplayObject obj = Compose(_registry);
            obj.Activate();

            obj.Destroy();
            Assert.DoesNotThrow(() => obj.Destroy(), "Second destroy is a no-op and must not throw on the registry.");
            Assert.AreEqual(0, _registry.Count);
        }
    }
}
