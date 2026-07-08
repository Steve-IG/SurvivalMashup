using NUnit.Framework;
using ToyChest.Framework.Objects;

namespace ToyChest.Tests.Objects
{
    /// <summary>Verifies GameplayObjectId identity semantics and serialization round-trip.</summary>
    public sealed class GameplayObjectIdTests
    {
        [Test]
        public void New_ProducesValidUniqueIds()
        {
            GameplayObjectId first = GameplayObjectId.New();
            GameplayObjectId second = GameplayObjectId.New();

            Assert.IsTrue(first.IsValid);
            Assert.IsTrue(second.IsValid);
            Assert.AreNotEqual(first, second);
        }

        [Test]
        public void Default_IsInvalid()
        {
            Assert.IsFalse(default(GameplayObjectId).IsValid);
        }

        [Test]
        public void ToString_RoundTripsThroughTryParse()
        {
            GameplayObjectId original = GameplayObjectId.New();

            Assert.IsTrue(GameplayObjectId.TryParse(original.ToString(), out GameplayObjectId parsed));
            Assert.AreEqual(original, parsed);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not-an-id")]
        [TestCase("00000000000000000000000000000000")]
        public void TryParse_RejectsInvalidInput(string value)
        {
            Assert.IsFalse(GameplayObjectId.TryParse(value, out _));
        }

        [Test]
        public void Equality_IsValueBased()
        {
            GameplayObjectId id = GameplayObjectId.New();
            GameplayObjectId.TryParse(id.ToString(), out GameplayObjectId copy);

            Assert.IsTrue(id == copy);
            Assert.IsFalse(id != copy);
            Assert.AreEqual(id.GetHashCode(), copy.GetHashCode());
        }
    }
}
