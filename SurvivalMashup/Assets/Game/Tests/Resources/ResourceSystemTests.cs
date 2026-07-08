using System;
using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Modifiers;
using ToyChest.Systems.Attributes;
using ToyChest.Systems.Resources;
using ToyChest.Tests.Events;

namespace ToyChest.Tests.Resources
{
    /// <summary>
    /// Verifies resource current/maximum behavior, the 0 &lt;= Current &lt;= Maximum invariant,
    /// regeneration, attribute binding with immediate clamp on maximum decrease, and events.
    /// </summary>
    public sealed class ResourceSystemTests
    {
        private const float Tolerance = 1e-4f;
        private ResourceTestFactory _factory;
        private readonly object _source = new object();

        [SetUp]
        public void SetUp()
        {
            _factory = new ResourceTestFactory();
        }

        [TearDown]
        public void TearDown()
        {
            _factory.Cleanup();
        }

        [Test]
        public void StartsFull_WhenConfigured()
        {
            var value = new ResourceValue(_factory.CreateLiteral("resource.mana", 100f));
            Assert.AreEqual(100f, value.Current, Tolerance);
            Assert.IsTrue(value.IsFull);
        }

        [Test]
        public void StartsAtStartingValue_WhenNotStartingFull()
        {
            var value = new ResourceValue(
                _factory.CreateLiteral("resource.rage", 100f, startAtMaximum: false, startingValue: 20f));
            Assert.AreEqual(20f, value.Current, Tolerance);
        }

        [Test]
        public void Consume_ClampsAtZero_AndReturnsActual()
        {
            var value = new ResourceValue(_factory.CreateLiteral("resource.mana", 50f));
            float consumed = value.Consume(70f);

            Assert.AreEqual(0f, value.Current, Tolerance);
            Assert.AreEqual(50f, consumed, Tolerance);
            Assert.IsTrue(value.IsDepleted);
        }

        [Test]
        public void Restore_ClampsAtMaximum_AndReturnsActual()
        {
            var value = new ResourceValue(
                _factory.CreateLiteral("resource.mana", 50f, startAtMaximum: false, startingValue: 40f));
            float restored = value.Restore(30f);

            Assert.AreEqual(50f, value.Current, Tolerance);
            Assert.AreEqual(10f, restored, Tolerance);
        }

        [Test]
        public void NegativeAmounts_AreRejected()
        {
            var value = new ResourceValue(_factory.CreateLiteral("resource.mana", 50f));
            Assert.Throws<ArgumentOutOfRangeException>(() => value.Consume(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => value.Restore(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => value.Regenerate(-1f));
        }

        [Test]
        public void Regenerate_RestoresByRateTimesDelta()
        {
            var value = new ResourceValue(
                _factory.CreateLiteral("resource.stamina", 100f, startAtMaximum: false, startingValue: 0f, regenPerSecond: 10f));

            value.Regenerate(0.5f);

            Assert.AreEqual(5f, value.Current, Tolerance);
        }

        [Test]
        public void BoundMaximum_TracksAttribute()
        {
            AttributeDefinition maxHealth = _factory.CreateAttribute("attribute.max-health", 100f);
            var attribute = new AttributeValue(maxHealth);
            var resource = new ResourceValue(
                _factory.CreateBound("resource.health", "attribute.max-health"), attribute);

            Assert.AreEqual(100f, resource.Maximum, Tolerance);
            Assert.AreEqual(100f, resource.Current, Tolerance);

            attribute.AddModifier(Modifier.Flat(50f, _source));
            Assert.AreEqual(150f, resource.Maximum, Tolerance);
        }

        [Test]
        public void BoundMaximum_Decrease_ClampsCurrentImmediately()
        {
            AttributeDefinition maxHealth = _factory.CreateAttribute("attribute.max-health", 100f);
            var attribute = new AttributeValue(maxHealth);
            var resource = new ResourceValue(
                _factory.CreateBound("resource.health", "attribute.max-health"), attribute);

            // Full at 100, then max drops to 40: current must clamp down immediately.
            attribute.AddModifier(Modifier.Flat(-60f, _source));

            Assert.AreEqual(40f, resource.Maximum, Tolerance);
            Assert.AreEqual(40f, resource.Current, Tolerance);
            Assert.LessOrEqual(resource.Current, resource.Maximum);
        }

        [Test]
        public void BoundMaximum_DecreaseThenIncrease_DoesNotRefillCurrent()
        {
            AttributeDefinition maxHealth = _factory.CreateAttribute("attribute.max-health", 100f);
            var attribute = new AttributeValue(maxHealth);
            var resource = new ResourceValue(
                _factory.CreateBound("resource.health", "attribute.max-health"), attribute);

            attribute.AddModifier(Modifier.Flat(-60f, _source)); // max 40, current clamped to 40
            attribute.RemoveModifiersFrom(_source);              // max back to 100

            Assert.AreEqual(100f, resource.Maximum, Tolerance);
            Assert.AreEqual(40f, resource.Current, Tolerance, "Raising the maximum must not refill current.");
        }

        [Test]
        public void InvariantHolds_AcrossOperations()
        {
            AttributeDefinition maxHealth = _factory.CreateAttribute("attribute.max-health", 100f);
            var attribute = new AttributeValue(maxHealth);
            var resource = new ResourceValue(
                _factory.CreateBound("resource.health", "attribute.max-health"), attribute);

            resource.Consume(30f);
            AssertInvariant(resource);
            attribute.AddModifier(Modifier.Flat(-90f, _source));
            AssertInvariant(resource);
            resource.Restore(1000f);
            AssertInvariant(resource);
            attribute.RemoveModifiersFrom(_source);
            AssertInvariant(resource);
        }

        [Test]
        public void MissingBinding_ThrowsClearly()
        {
            ResourceDefinition bound = _factory.CreateBound("resource.health", "attribute.max-health");
            Assert.Throws<ArgumentNullException>(() => new ResourceValue(bound));
        }

        [Test]
        public void Set_PublishesChangeDepletedAndFull()
        {
            var bus = new EventBus(new RecordingLogger());
            var changes = new List<ResourceChanged>();
            var depleted = new List<ResourceDepleted>();
            var full = new List<ResourceFull>();
            using IDisposable c = bus.Subscribe<ResourceChanged>(changes.Add);
            using IDisposable d = bus.Subscribe<ResourceDepleted>(depleted.Add);
            using IDisposable f = bus.Subscribe<ResourceFull>(full.Add);

            var set = new ResourceSet(bus);
            ResourceValue mana = set.AddResource(
                _factory.CreateLiteral("resource.mana", 50f, startAtMaximum: false, startingValue: 25f));

            mana.Consume(25f);   // -> depleted
            mana.Restore(50f);   // -> full

            Assert.AreEqual(2, changes.Count);
            Assert.AreEqual(1, depleted.Count);
            Assert.AreEqual(1, full.Count);
            Assert.AreEqual(new DefinitionId("resource.mana"), depleted[0].Resource);
            Assert.AreEqual(50f, full[0].Maximum, Tolerance);
        }

        [Test]
        public void Set_BoundResource_ResolvesThroughAttributeProvider()
        {
            var attributeSet = new AttributeSet();
            attributeSet.AddAttribute(_factory.CreateAttribute("attribute.max-health", 100f));
            var resourceSet = new ResourceSet(eventBus: null, attributes: attributeSet);

            ResourceValue health = resourceSet.AddResource(
                _factory.CreateBound("resource.health", "attribute.max-health"));

            Assert.AreEqual(100f, health.Maximum, Tolerance);
        }

        [Test]
        public void Set_BoundResource_WithoutProvider_ThrowsClearly()
        {
            var set = new ResourceSet();
            Assert.Throws<InvalidOperationException>(
                () => set.AddResource(_factory.CreateBound("resource.health", "attribute.max-health")));
        }

        [Test]
        public void Set_RejectsDuplicateResource()
        {
            var set = new ResourceSet();
            set.AddResource(_factory.CreateLiteral("resource.mana", 50f));
            Assert.Throws<InvalidOperationException>(
                () => set.AddResource(_factory.CreateLiteral("resource.mana", 99f)));
        }

        private static void AssertInvariant(ResourceValue resource)
        {
            Assert.GreaterOrEqual(resource.Current, 0f, "Current must never be negative.");
            Assert.LessOrEqual(resource.Current, resource.Maximum, "Current must never exceed Maximum.");
        }
    }
}
