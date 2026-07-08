using System;
using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Systems.Tags;

namespace ToyChest.Tests.Tags
{
    /// <summary>
    /// Verifies interning, ancestor registration, path validation, and hierarchy
    /// structure queries of the Gameplay Tag Table.
    /// </summary>
    public sealed class GameplayTagTableTests
    {
        private GameplayTagTable _table;

        [SetUp]
        public void SetUp()
        {
            _table = new GameplayTagTable();
        }

        [Test]
        public void RegisterTag_InternsAllAncestors()
        {
            _table.RegisterTag("Element.Fire.Burning");

            Assert.AreEqual(3, _table.Count);
            Assert.IsTrue(_table.TryGetTag("Element", out _));
            Assert.IsTrue(_table.TryGetTag("Element.Fire", out _));
            Assert.IsTrue(_table.TryGetTag("Element.Fire.Burning", out _));
        }

        [Test]
        public void RegisterTag_IsIdempotent()
        {
            GameplayTag first = _table.RegisterTag("Combat.Melee");
            GameplayTag second = _table.RegisterTag("Combat.Melee");

            Assert.AreEqual(first, second);
            Assert.AreEqual(2, _table.Count);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(".Element")]
        [TestCase("Element.")]
        [TestCase("Element..Fire")]
        [TestCase("Element Fire")]
        [TestCase("Element-Fire")]
        public void RegisterTag_RejectsMalformedPaths(string path)
        {
            Assert.Throws<ArgumentException>(() => _table.RegisterTag(path));
        }

        [Test]
        public void GetTag_UnknownPath_ThrowsWithPath()
        {
            var exception = Assert.Throws<KeyNotFoundException>(() => _table.GetTag("Missing.Tag"));
            StringAssert.Contains("Missing.Tag", exception.Message);
        }

        [Test]
        public void GetPath_RoundTrips()
        {
            GameplayTag tag = _table.RegisterTag("Interaction.Harvest");
            Assert.AreEqual("Interaction.Harvest", _table.GetPath(tag));
        }

        [Test]
        public void GetParent_WalksHierarchy()
        {
            GameplayTag burning = _table.RegisterTag("Element.Fire.Burning");
            GameplayTag fire = _table.GetParent(burning);
            GameplayTag element = _table.GetParent(fire);

            Assert.AreEqual("Element.Fire", _table.GetPath(fire));
            Assert.AreEqual("Element", _table.GetPath(element));
            Assert.IsFalse(_table.GetParent(element).IsValid, "Roots have no parent.");
        }

        [Test]
        public void GetDepth_CountsSegments()
        {
            GameplayTag burning = _table.RegisterTag("Element.Fire.Burning");

            Assert.AreEqual(3, _table.GetDepth(burning));
            Assert.AreEqual(1, _table.GetDepth(_table.GetTag("Element")));
        }

        [Test]
        public void Matches_AncestorMatchesDescendant()
        {
            GameplayTag burning = _table.RegisterTag("Element.Fire.Burning");
            GameplayTag fire = _table.GetTag("Element.Fire");
            GameplayTag element = _table.GetTag("Element");

            Assert.IsTrue(_table.Matches(fire, burning), "Element.Fire must match Element.Fire.Burning.");
            Assert.IsTrue(_table.Matches(element, burning));
            Assert.IsTrue(_table.Matches(burning, burning), "A tag matches itself.");
        }

        [Test]
        public void Matches_IsDirectional()
        {
            GameplayTag burning = _table.RegisterTag("Element.Fire.Burning");
            GameplayTag fire = _table.GetTag("Element.Fire");

            Assert.IsFalse(_table.Matches(burning, fire), "A child query must not match a parent tag.");
        }

        [Test]
        public void Matches_UnrelatedFamilies_DoNotMatch()
        {
            GameplayTag fire = _table.RegisterTag("Element.Fire");
            GameplayTag melee = _table.RegisterTag("Combat.Melee");

            Assert.IsFalse(_table.Matches(fire, melee));
        }

        [Test]
        public void DefaultTag_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => _table.GetPath(default));
        }
    }
}
