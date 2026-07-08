using System;
using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Framework.Data;

namespace ToyChest.Tests.Data
{
    /// <summary>Verifies DefinitionId validation, equality, and dictionary behavior.</summary>
    public sealed class DefinitionIdTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Constructor_RejectsEmptyValues(string value)
        {
            Assert.Throws<ArgumentException>(() => _ = new DefinitionId(value));
        }

        [Test]
        public void Default_IsInvalid()
        {
            Assert.IsFalse(default(DefinitionId).IsValid);
        }

        [Test]
        public void Constructed_IsValid()
        {
            Assert.IsTrue(new DefinitionId("ability.fireball").IsValid);
        }

        [Test]
        public void Equality_IsOrdinal()
        {
            Assert.AreEqual(new DefinitionId("item.ore"), new DefinitionId("item.ore"));
            Assert.AreNotEqual(new DefinitionId("item.ore"), new DefinitionId("item.Ore"));
            Assert.IsTrue(new DefinitionId("a") != new DefinitionId("b"));
        }

        [Test]
        public void WorksAsDictionaryKey()
        {
            var dictionary = new Dictionary<DefinitionId, int>
            {
                [new DefinitionId("ability.fireball")] = 1,
            };

            Assert.AreEqual(1, dictionary[new DefinitionId("ability.fireball")]);
        }

        [Test]
        public void ToString_ReturnsValue()
        {
            Assert.AreEqual("ability.fireball", new DefinitionId("ability.fireball").ToString());
        }
    }
}
