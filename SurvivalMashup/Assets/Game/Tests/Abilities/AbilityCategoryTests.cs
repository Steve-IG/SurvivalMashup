using System;
using NUnit.Framework;
using ToyChest.Systems.Abilities;

namespace ToyChest.Tests.Abilities
{
    /// <summary>
    /// Verifies the string-backed AbilityCategory value type: ordinal equality, the None
    /// value for uncategorized abilities, and construction validation.
    /// </summary>
    public sealed class AbilityCategoryTests
    {
        private AbilityTestFactory _abilities;

        [SetUp]
        public void SetUp()
        {
            _abilities = new AbilityTestFactory();
        }

        [TearDown]
        public void TearDown()
        {
            _abilities.Cleanup();
        }

        [Test]
        public void Equality_IsOrdinal_OnTheBackingString()
        {
            var combat = new AbilityCategory("Combat");

            Assert.AreEqual(new AbilityCategory("Combat"), combat);
            Assert.AreNotEqual(new AbilityCategory("combat"), combat, "Comparison is ordinal, not case-insensitive.");
            Assert.AreEqual(new AbilityCategory("Combat").GetHashCode(), combat.GetHashCode());
            Assert.IsTrue(combat == new AbilityCategory("Combat"));
            Assert.IsTrue(combat != AbilityCategory.None);
            Assert.AreEqual("Combat", combat.Value);
            Assert.IsTrue(combat.IsValid);
        }

        [Test]
        public void None_IsTheDefault_AndIdentifiesNoCategory()
        {
            Assert.IsFalse(AbilityCategory.None.IsValid);
            Assert.AreEqual(default(AbilityCategory), AbilityCategory.None);
            Assert.IsNull(AbilityCategory.None.Value);
        }

        [Test]
        public void Constructor_RejectsBlankValues()
        {
            Assert.Throws<ArgumentException>(() => new AbilityCategory(null));
            Assert.Throws<ArgumentException>(() => new AbilityCategory("   "));
        }

        [Test]
        public void AbilityDefinition_UnauthoredCategory_ReadsAsNone()
        {
            AbilityDefinition ability = _abilities.CreateAbility("ability.test");

            Assert.AreEqual(AbilityCategory.None, ability.Category,
                "Blank authoring maps to None rather than an invalid wrapped string.");
        }
    }
}
