using System;
using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Systems.Tags;

namespace ToyChest.Tests.Tags
{
    /// <summary>
    /// Verifies counted multi-source tag ownership, O(1) hierarchical queries,
    /// and transition-only change notifications of the Gameplay Tag Container.
    /// </summary>
    public sealed class GameplayTagContainerTests
    {
        private GameplayTagTable _table;
        private GameplayTagContainer _container;
        private GameplayTag _burning;
        private GameplayTag _fire;
        private GameplayTag _element;
        private GameplayTag _melee;

        [SetUp]
        public void SetUp()
        {
            _table = new GameplayTagTable();
            _burning = _table.RegisterTag("Element.Fire.Burning");
            _fire = _table.GetTag("Element.Fire");
            _element = _table.GetTag("Element");
            _melee = _table.RegisterTag("Combat.Melee");
            _container = new GameplayTagContainer(_table);
        }

        [Test]
        public void HasTag_MatchesAncestorsOfHeldTag()
        {
            _container.AddTag(_burning);

            Assert.IsTrue(_container.HasTag(_burning));
            Assert.IsTrue(_container.HasTag(_fire), "Holding Element.Fire.Burning must match an Element.Fire query.");
            Assert.IsTrue(_container.HasTag(_element));
            Assert.IsFalse(_container.HasTag(_melee));
        }

        [Test]
        public void HasTag_IsDirectional()
        {
            _container.AddTag(_fire);

            Assert.IsTrue(_container.HasTag(_fire));
            Assert.IsFalse(_container.HasTag(_burning), "Holding Element.Fire must not match an Element.Fire.Burning query.");
        }

        [Test]
        public void HasTagExact_IgnoresHierarchy()
        {
            _container.AddTag(_burning);

            Assert.IsTrue(_container.HasTagExact(_burning));
            Assert.IsFalse(_container.HasTagExact(_fire));
        }

        [Test]
        public void CountedRemoval_TagPersistsUntilAllSourcesRemove()
        {
            _container.AddTag(_fire);
            _container.AddTag(_fire);

            _container.RemoveTag(_fire);
            Assert.IsTrue(_container.HasTag(_fire), "One source remains; the tag must persist.");

            _container.RemoveTag(_fire);
            Assert.IsFalse(_container.HasTag(_fire));
        }

        [Test]
        public void RemoveTag_NotPresent_ThrowsWithPath()
        {
            var exception = Assert.Throws<InvalidOperationException>(() => _container.RemoveTag(_fire));
            StringAssert.Contains("Element.Fire", exception.Message);
        }

        [Test]
        public void SiblingRemoval_KeepsSharedAncestorAlive()
        {
            GameplayTag frozen = _table.RegisterTag("Element.Ice.Frozen");
            _container.AddTag(_burning);
            _container.AddTag(frozen);

            _container.RemoveTag(_burning);

            Assert.IsTrue(_container.HasTag(_element), "Element.Ice.Frozen still contributes to Element.");
            Assert.IsFalse(_container.HasTag(_fire));
        }

        [Test]
        public void Notifications_FireOnlyOnTransitions()
        {
            var added = new List<GameplayTag>();
            var removed = new List<GameplayTag>();
            _container.TagAdded += added.Add;
            _container.TagRemoved += removed.Add;

            _container.AddTag(_fire);
            _container.AddTag(_fire);
            _container.RemoveTag(_fire);
            _container.RemoveTag(_fire);

            Assert.AreEqual(1, added.Count, "TagAdded fires only on the absent-to-present transition.");
            Assert.AreEqual(1, removed.Count, "TagRemoved fires only on the present-to-absent transition.");
            Assert.AreEqual(_fire, added[0]);
            Assert.AreEqual(_fire, removed[0]);
        }

        [Test]
        public void HasAllHasAnyHasNone_UseHierarchicalMatching()
        {
            _container.AddTag(_burning);
            _container.AddTag(_melee);
            GameplayTag combat = _table.GetTag("Combat");
            GameplayTag ice = _table.RegisterTag("Element.Ice");

            Assert.IsTrue(_container.HasAll(new[] { _fire, combat }));
            Assert.IsFalse(_container.HasAll(new[] { _fire, ice }));
            Assert.IsTrue(_container.HasAny(new[] { ice, combat }));
            Assert.IsFalse(_container.HasAny(new[] { ice }));
            Assert.IsTrue(_container.HasNone(new[] { ice }));
            Assert.IsFalse(_container.HasNone(new[] { combat }));
        }

        [Test]
        public void Count_TracksDistinctExactTags()
        {
            _container.AddTag(_burning);
            _container.AddTag(_burning);
            _container.AddTag(_melee);

            Assert.AreEqual(2, _container.Count);
        }

        [Test]
        public void CopyTagsTo_ReturnsDistinctHeldTags()
        {
            _container.AddTag(_burning);
            _container.AddTag(_melee);

            var results = new List<GameplayTag>();
            _container.CopyTagsTo(results);

            Assert.AreEqual(2, results.Count);
            CollectionAssert.Contains(results, _burning);
            CollectionAssert.Contains(results, _melee);
        }

        [Test]
        public void InvalidTag_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => _container.AddTag(default));
            Assert.Throws<ArgumentException>(() => _container.HasTag(default));
        }
    }
}
