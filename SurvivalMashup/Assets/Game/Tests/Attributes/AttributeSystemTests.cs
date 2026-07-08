using System;
using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Modifiers;
using ToyChest.Systems.Attributes;
using ToyChest.Tests.Events;

namespace ToyChest.Tests.Attributes
{
    /// <summary>
    /// Verifies attribute runtime values, shared-stack modifier application, change events,
    /// and set ownership rules.
    /// </summary>
    public sealed class AttributeSystemTests
    {
        private const float Tolerance = 1e-4f;
        private AttributeTestFactory _factory;
        private readonly object _source = new object();

        [SetUp]
        public void SetUp()
        {
            _factory = new AttributeTestFactory();
        }

        [TearDown]
        public void TearDown()
        {
            _factory.Cleanup();
        }

        [Test]
        public void NewValue_StartsAtBase()
        {
            var value = new AttributeValue(_factory.Create("attribute.max-health", 100f));
            Assert.AreEqual(100f, value.CurrentValue, Tolerance);
        }

        [Test]
        public void Modifier_RecomputesUsingSharedOrder()
        {
            var value = new AttributeValue(_factory.Create("attribute.attack", 100f));
            value.AddModifier(Modifier.Flat(10f, _source));
            value.AddModifier(Modifier.AdditivePercent(0.5f, _source));

            Assert.AreEqual(165f, value.CurrentValue, Tolerance);
        }

        [Test]
        public void ClampsToDefinitionBounds()
        {
            var value = new AttributeValue(_factory.Create("attribute.armor", 10f, min: 0f, max: 50f));
            value.AddModifier(Modifier.Flat(1000f, _source));
            Assert.AreEqual(50f, value.CurrentValue, Tolerance);

            value.RemoveModifiersFrom(_source);
            value.AddModifier(Modifier.Flat(-1000f, _source));
            Assert.AreEqual(0f, value.CurrentValue, Tolerance);
        }

        [Test]
        public void ValueChanged_FiresWithPreviousAndNew()
        {
            var value = new AttributeValue(_factory.Create("attribute.speed", 5f));
            var changes = new List<(float, float)>();
            value.ValueChanged += (previous, next) => changes.Add((previous, next));

            value.AddModifier(Modifier.Flat(3f, _source));

            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(5f, changes[0].Item1, Tolerance);
            Assert.AreEqual(8f, changes[0].Item2, Tolerance);
        }

        [Test]
        public void ValueChanged_DoesNotFireWhenResultUnchanged()
        {
            var value = new AttributeValue(_factory.Create("attribute.speed", 5f, min: 0f, max: 5f));
            int calls = 0;
            value.ValueChanged += (_, _) => calls++;

            // Already clamped at max 5; adding more flat cannot change the result.
            value.AddModifier(Modifier.Flat(100f, _source));

            Assert.AreEqual(0, calls);
        }

        [Test]
        public void RemoveModifiersFrom_RestoresValue()
        {
            var value = new AttributeValue(_factory.Create("attribute.attack", 100f));
            value.AddModifier(Modifier.Flat(50f, _source));
            Assert.AreEqual(150f, value.CurrentValue, Tolerance);

            value.RemoveModifiersFrom(_source);
            Assert.AreEqual(100f, value.CurrentValue, Tolerance);
        }

        [Test]
        public void Set_PublishesAttributeChanged()
        {
            var bus = new EventBus(new RecordingLogger());
            var received = new List<AttributeChanged>();
            using IDisposable token = bus.Subscribe<AttributeChanged>(received.Add);

            var set = new AttributeSet(bus);
            AttributeValue attack = set.AddAttribute(_factory.Create("attribute.attack", 100f));
            attack.AddModifier(Modifier.Flat(25f, _source));

            Assert.AreEqual(1, received.Count);
            Assert.AreEqual(new DefinitionId("attribute.attack"), received[0].Attribute);
            Assert.AreEqual(100f, received[0].PreviousValue, Tolerance);
            Assert.AreEqual(125f, received[0].NewValue, Tolerance);
        }

        [Test]
        public void Set_RejectsDuplicateAttribute()
        {
            var set = new AttributeSet();
            set.AddAttribute(_factory.Create("attribute.attack", 100f));

            Assert.Throws<InvalidOperationException>(
                () => set.AddAttribute(_factory.Create("attribute.attack", 50f)));
        }

        [Test]
        public void Set_GetValue_UnknownThrows()
        {
            var set = new AttributeSet();
            Assert.Throws<KeyNotFoundException>(() => set.GetValue(new DefinitionId("attribute.missing")));
            Assert.IsFalse(set.TryGetValue(new DefinitionId("attribute.missing"), out _));
        }
    }
}
