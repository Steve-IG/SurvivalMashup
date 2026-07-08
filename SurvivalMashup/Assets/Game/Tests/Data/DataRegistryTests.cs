using System;
using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Framework.Data;

namespace ToyChest.Tests.Data
{
    /// <summary>
    /// Verifies the Data Registry contract from Docs/Architecture/DATA_REGISTRY.md:
    /// concrete-type bucketing, duplicate rejection, clear failure messages,
    /// and deterministic enumeration order.
    /// </summary>
    public sealed class DataRegistryTests
    {
        private DataRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _registry = new DataRegistry();
        }

        [Test]
        public void Register_RequiresDefinition()
        {
            Assert.Throws<ArgumentNullException>(() => _registry.Register(null));
        }

        [Test]
        public void Register_RejectsInvalidId()
        {
            Assert.Throws<ArgumentException>(() => _registry.Register(new BrokenDefinition()));
        }

        [Test]
        public void Get_ReturnsRegisteredDefinition()
        {
            var apple = new FruitDefinition("fruit.apple", "Apple");
            _registry.Register(apple);

            FruitDefinition resolved = _registry.Get<FruitDefinition>(new DefinitionId("fruit.apple"));

            Assert.AreSame(apple, resolved);
        }

        [Test]
        public void Get_UnknownId_ThrowsWithIdAndType()
        {
            var exception = Assert.Throws<KeyNotFoundException>(
                () => _registry.Get<FruitDefinition>(new DefinitionId("fruit.missing")));

            StringAssert.Contains("fruit.missing", exception.Message);
            StringAssert.Contains(nameof(FruitDefinition), exception.Message);
        }

        [Test]
        public void Register_DuplicateId_ThrowsWithId()
        {
            _registry.Register(new FruitDefinition("fruit.apple", "Apple"));

            var exception = Assert.Throws<InvalidOperationException>(
                () => _registry.Register(new FruitDefinition("fruit.apple", "Second Apple")));

            StringAssert.Contains("fruit.apple", exception.Message);
        }

        [Test]
        public void SameId_DifferentTypes_DoNotCollide()
        {
            _registry.Register(new FruitDefinition("shared.id", "Fruit"));

            Assert.DoesNotThrow(() => _registry.Register(new ToolDefinition("shared.id")));
            Assert.IsTrue(_registry.Contains<FruitDefinition>(new DefinitionId("shared.id")));
            Assert.IsTrue(_registry.Contains<ToolDefinition>(new DefinitionId("shared.id")));
        }

        [Test]
        public void TryGet_ReturnsFalseForUnknown()
        {
            Assert.IsFalse(_registry.TryGet<FruitDefinition>(new DefinitionId("fruit.missing"), out FruitDefinition definition));
            Assert.IsNull(definition);
        }

        [Test]
        public void GetAll_ReturnsRegistrationOrder()
        {
            var apple = new FruitDefinition("fruit.apple", "Apple");
            var banana = new FruitDefinition("fruit.banana", "Banana");
            var cherry = new FruitDefinition("fruit.cherry", "Cherry");
            _registry.Register(apple);
            _registry.Register(banana);
            _registry.Register(cherry);

            IReadOnlyList<FruitDefinition> all = _registry.GetAll<FruitDefinition>();

            CollectionAssert.AreEqual(new[] { apple, banana, cherry }, all);
        }

        [Test]
        public void GetAll_UnknownType_ReturnsEmpty()
        {
            Assert.AreEqual(0, _registry.GetAll<FruitDefinition>().Count);
        }
    }
}
