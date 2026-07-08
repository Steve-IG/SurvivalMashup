using System;
using NUnit.Framework;
using ToyChest.Framework.Modifiers;

namespace ToyChest.Tests.Modifiers
{
    /// <summary>
    /// Verifies the single shared modifier stack: the fixed evaluation order
    /// Base → Flat → Additive % → Multiplicative % → Override → Clamp, order-independence,
    /// deterministic override resolution, and source-scoped removal.
    /// </summary>
    public sealed class ModifierStackTests
    {
        private const float Tolerance = 1e-4f;
        private ModifierStack _stack;
        private readonly object _sourceA = new object();
        private readonly object _sourceB = new object();

        [SetUp]
        public void SetUp()
        {
            _stack = new ModifierStack();
        }

        [Test]
        public void NoModifiers_ReturnsBaseValue()
        {
            Assert.AreEqual(50f, _stack.Evaluate(50f, 0f, 1000f), Tolerance);
        }

        [Test]
        public void Flat_AddsToBase()
        {
            _stack.Add(Modifier.Flat(10f, _sourceA));
            _stack.Add(Modifier.Flat(5f, _sourceB));

            Assert.AreEqual(65f, _stack.Evaluate(50f, 0f, 1000f), Tolerance);
        }

        [Test]
        public void AdditivePercent_SumsThenScalesPostFlat()
        {
            _stack.Add(Modifier.Flat(50f, _sourceA));          // base 50 + 50 = 100
            _stack.Add(Modifier.AdditivePercent(0.2f, _sourceA));
            _stack.Add(Modifier.AdditivePercent(0.3f, _sourceB)); // +50% => 150

            Assert.AreEqual(150f, _stack.Evaluate(50f, 0f, 1000f), Tolerance);
        }

        [Test]
        public void MultiplicativePercent_Compounds()
        {
            _stack.Add(Modifier.MultiplicativePercent(0.5f, _sourceA)); // x1.5
            _stack.Add(Modifier.MultiplicativePercent(0.2f, _sourceB)); // x1.2

            // 100 * 1.5 * 1.2 = 180
            Assert.AreEqual(180f, _stack.Evaluate(100f, 0f, 1000f), Tolerance);
        }

        [Test]
        public void FullOrder_IsBaseFlatAdditiveMultiplicative()
        {
            _stack.Add(Modifier.Flat(10f, _sourceA));               // (100 + 10)
            _stack.Add(Modifier.AdditivePercent(0.5f, _sourceA));   // * 1.5  = 165
            _stack.Add(Modifier.MultiplicativePercent(0.2f, _sourceB)); // * 1.2 = 198

            Assert.AreEqual(198f, _stack.Evaluate(100f, 0f, 1000f), Tolerance);
        }

        [Test]
        public void Evaluation_IsIndependentOfInsertionOrder()
        {
            _stack.Add(Modifier.MultiplicativePercent(0.2f, _sourceB));
            _stack.Add(Modifier.Flat(10f, _sourceA));
            _stack.Add(Modifier.AdditivePercent(0.5f, _sourceA));

            Assert.AreEqual(198f, _stack.Evaluate(100f, 0f, 1000f), Tolerance);
        }

        [Test]
        public void Override_ReplacesComputedValue()
        {
            _stack.Add(Modifier.Flat(1000f, _sourceA));
            _stack.Add(Modifier.Override(5f, _sourceB));

            Assert.AreEqual(5f, _stack.Evaluate(100f, 0f, 10000f), Tolerance);
        }

        [Test]
        public void Override_HighestPriorityWins()
        {
            _stack.Add(Modifier.Override(5f, _sourceA, priority: 1));
            _stack.Add(Modifier.Override(9f, _sourceB, priority: 2));

            Assert.AreEqual(9f, _stack.Evaluate(100f, 0f, 1000f), Tolerance);
        }

        [Test]
        public void Override_EqualPriority_HighestValueWins_Deterministically()
        {
            _stack.Add(Modifier.Override(7f, _sourceA, priority: 1));
            _stack.Add(Modifier.Override(3f, _sourceB, priority: 1));

            Assert.AreEqual(7f, _stack.Evaluate(100f, 0f, 1000f), Tolerance);
        }

        [Test]
        public void Clamp_AppliesAfterOverride()
        {
            _stack.Add(Modifier.Override(500f, _sourceA));
            Assert.AreEqual(100f, _stack.Evaluate(0f, 0f, 100f), Tolerance);

            _stack.Clear();
            _stack.Add(Modifier.Flat(-999f, _sourceA));
            Assert.AreEqual(0f, _stack.Evaluate(50f, 0f, 100f), Tolerance);
        }

        [Test]
        public void RemoveSource_RemovesOnlyThatSource()
        {
            _stack.Add(Modifier.Flat(10f, _sourceA));
            _stack.Add(Modifier.Flat(20f, _sourceB));

            int removed = _stack.RemoveSource(_sourceA);

            Assert.AreEqual(1, removed);
            Assert.AreEqual(70f, _stack.Evaluate(50f, 0f, 1000f), Tolerance);
            Assert.IsFalse(_stack.ContainsSource(_sourceA));
            Assert.IsTrue(_stack.ContainsSource(_sourceB));
        }

        [Test]
        public void RemoveSource_Unknown_IsNoOp()
        {
            _stack.Add(Modifier.Flat(10f, _sourceA));
            Assert.AreEqual(0, _stack.RemoveSource(_sourceB));
            Assert.AreEqual(1, _stack.Count);
        }
    }
}
