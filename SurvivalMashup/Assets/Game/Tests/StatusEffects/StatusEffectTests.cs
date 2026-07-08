using System;
using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Modifiers;
using ToyChest.Framework.Objects;
using ToyChest.Systems.Attributes;
using ToyChest.Systems.GameplayEffects;
using ToyChest.Systems.Resources;
using ToyChest.Systems.StatusEffects;
using ToyChest.Systems.Tags;
using ToyChest.Tests.Effects;
using ToyChest.Tests.Events;
using ToyChest.Tests.Resources;

namespace ToyChest.Tests.StatusEffects
{
    /// <summary>
    /// Verifies status lifecycle: contribution application and revocation, duration and
    /// expiry, deterministic periodic execution, all four stacking rules, events, and
    /// silent teardown on dispose.
    /// </summary>
    public sealed class StatusEffectTests
    {
        private const float Tolerance = 1e-4f;
        private static readonly DefinitionId MoveSpeed = new DefinitionId("attribute.move-speed");
        private static readonly DefinitionId HealthId = new DefinitionId("resource.health");

        private EffectTestFactory _effects;
        private ResourceTestFactory _definitions;
        private GameplayTagTable _tagTable;
        private EventBus _bus;

        private AttributeSet _attributes;
        private ResourceSet _resources;
        private GameplayTagContainer _tags;
        private StatusEffectSet _statuses;
        private GameplayObjectId _owner;

        [SetUp]
        public void SetUp()
        {
            _effects = new EffectTestFactory();
            _definitions = new ResourceTestFactory();
            _tagTable = new GameplayTagTable();
            _bus = new EventBus(new RecordingLogger());
            _owner = GameplayObjectId.New();

            _attributes = new AttributeSet();
            _attributes.AddAttribute(_definitions.CreateAttribute("attribute.max-health", 100f));
            _attributes.AddAttribute(_definitions.CreateAttribute("attribute.move-speed", 10f));
            _resources = new ResourceSet(attributes: _attributes);
            _resources.AddResource(_definitions.CreateBound("resource.health", "attribute.max-health"));
            _tags = new GameplayTagContainer(_tagTable);

            _statuses = new StatusEffectSet(
                _owner, _bus, _tagTable, new GameplayEffectRunner(),
                new EffectTarget(_resources, _attributes, _tags));
        }

        [TearDown]
        public void TearDown()
        {
            _effects.Cleanup();
            _definitions.Cleanup();
        }

        private ResourceValue Health => _resources.GetResource(HealthId);

        private StatusEffectDefinition CreateBurning(
            float duration = 3f, float period = 1f, float damagePerTick = 5f)
        {
            TagDefinition burningTag = _effects.CreateTag("State.Burning");
            _tagTable.RegisterTag("State.Burning");
            return _effects.CreateStatus(
                "status.burning",
                durationSeconds: duration,
                grantedTags: new[] { burningTag },
                periodSeconds: period,
                periodic: new GameplayEffectDefinition[]
                {
                    _effects.CreateDamage("fx.burn-tick", "resource.health", damagePerTick),
                });
        }

        [Test]
        public void Apply_GrantsContributions_AndPublishesApplied()
        {
            var applied = new List<StatusApplied>();
            using IDisposable token = _bus.Subscribe<StatusApplied>(applied.Add);

            var frozen = _effects.CreateStatus(
                "status.frozen",
                grantedTags: new[] { RegisteredTag("State.Frozen") },
                modifiers: new[] { new AttributeModifierConfig("attribute.move-speed", ModifierOperation.AdditivePercent, -0.5f) });

            _statuses.Apply(frozen);

            Assert.IsTrue(_tags.HasTagExact(_tagTable.GetTag("State.Frozen")));
            Assert.AreEqual(5f, _attributes.GetValue(MoveSpeed), Tolerance);
            Assert.AreEqual(1, applied.Count);
            Assert.AreEqual(_owner, applied[0].Owner);
            Assert.IsTrue(_statuses.Has(new DefinitionId("status.frozen")));
        }

        [Test]
        public void Expiry_RevokesContributions_AndPublishesExpired()
        {
            var expired = new List<StatusExpired>();
            using IDisposable token = _bus.Subscribe<StatusExpired>(expired.Add);

            _statuses.Apply(CreateBurning(duration: 2f, period: 1f, damagePerTick: 5f));
            _statuses.Tick(2.5f);

            Assert.AreEqual(1, expired.Count);
            Assert.IsFalse(_statuses.Has(new DefinitionId("status.burning")));
            Assert.IsFalse(_tags.HasTagExact(_tagTable.GetTag("State.Burning")), "Expiry must revoke granted tags.");
        }

        [Test]
        public void Periodic_ExecutesOncePerFullPeriod()
        {
            var ticks = new List<StatusPeriodicTick>();
            using IDisposable token = _bus.Subscribe<StatusPeriodicTick>(ticks.Add);

            _statuses.Apply(CreateBurning(duration: 10f, period: 1f, damagePerTick: 5f));

            _statuses.Tick(0.5f);
            Assert.AreEqual(100f, Health.Current, Tolerance, "No full period elapsed yet.");

            _statuses.Tick(0.5f);
            _statuses.Tick(0.5f);
            _statuses.Tick(0.5f);

            Assert.AreEqual(90f, Health.Current, Tolerance, "Two full periods -> two damage ticks.");
            Assert.AreEqual(2, ticks.Count);
        }

        [Test]
        public void Periodic_DoesNotOvertickPastExpiry()
        {
            _statuses.Apply(CreateBurning(duration: 1f, period: 0.5f, damagePerTick: 10f));

            _statuses.Tick(5f);

            Assert.AreEqual(80f, Health.Current, Tolerance,
                "Only the periods inside the 1s lifetime may execute, regardless of delta size.");
        }

        [Test]
        public void RefreshDuration_ResetsLifetime_AndPublishesRefreshed()
        {
            var refreshed = new List<StatusRefreshed>();
            using IDisposable token = _bus.Subscribe<StatusRefreshed>(refreshed.Add);
            StatusEffectDefinition burning = CreateBurning(duration: 3f, period: 10f);

            _statuses.Apply(burning);
            _statuses.Tick(2f);
            _statuses.Apply(burning);
            _statuses.Tick(2f);

            Assert.IsTrue(_statuses.Has(burning.Id), "Refreshed status must survive past its original duration.");
            Assert.AreEqual(1, refreshed.Count);

            _statuses.Tick(1.5f);
            Assert.IsFalse(_statuses.Has(burning.Id));
        }

        [Test]
        public void IgnoreDuplicate_LeavesExistingUntouched()
        {
            var refreshed = new List<StatusRefreshed>();
            using IDisposable token = _bus.Subscribe<StatusRefreshed>(refreshed.Add);
            var status = _effects.CreateStatus(
                "status.shielded", stackingRule: StatusStackingRule.IgnoreDuplicate, durationSeconds: 3f);

            StatusEffectInstance first = _statuses.Apply(status);
            StatusEffectInstance second = _statuses.Apply(status);

            Assert.AreSame(first, second);
            Assert.AreEqual(0, refreshed.Count);
            Assert.AreEqual(1, _statuses.Count);
        }

        [Test]
        public void ReplaceExisting_RemovesThenReapplies()
        {
            var removed = new List<StatusRemoved>();
            var applied = new List<StatusApplied>();
            using IDisposable removedToken = _bus.Subscribe<StatusRemoved>(removed.Add);
            using IDisposable appliedToken = _bus.Subscribe<StatusApplied>(applied.Add);
            var status = _effects.CreateStatus(
                "status.mark",
                stackingRule: StatusStackingRule.ReplaceExisting,
                grantedTags: new[] { RegisteredTag("State.Marked") });

            _statuses.Apply(status);
            _statuses.Apply(status);

            Assert.AreEqual(1, removed.Count);
            Assert.AreEqual(2, applied.Count);
            Assert.AreEqual(1, _statuses.Count);
            Assert.IsTrue(_tags.HasTagExact(_tagTable.GetTag("State.Marked")),
                "Replacement must leave exactly one active claim on the granted tag.");
        }

        [Test]
        public void IncreaseStacks_ScalesModifiers_AndRespectsCap()
        {
            var stackEvents = new List<StatusStackIncreased>();
            var refreshed = new List<StatusRefreshed>();
            using IDisposable stackToken = _bus.Subscribe<StatusStackIncreased>(stackEvents.Add);
            using IDisposable refreshToken = _bus.Subscribe<StatusRefreshed>(refreshed.Add);
            var chilled = _effects.CreateStatus(
                "status.chilled",
                stackingRule: StatusStackingRule.IncreaseStacks,
                maximumStacks: 3,
                modifiers: new[] { new AttributeModifierConfig("attribute.move-speed", ModifierOperation.Flat, -2f) });

            _statuses.Apply(chilled);
            Assert.AreEqual(8f, _attributes.GetValue(MoveSpeed), Tolerance);

            _statuses.Apply(chilled);
            _statuses.Apply(chilled);
            Assert.AreEqual(3, _statuses.GetStacks(chilled.Id));
            Assert.AreEqual(4f, _attributes.GetValue(MoveSpeed), Tolerance, "Modifier magnitude scales per stack.");
            Assert.AreEqual(2, stackEvents.Count);

            _statuses.Apply(chilled);
            Assert.AreEqual(3, _statuses.GetStacks(chilled.Id), "The cap holds.");
            Assert.AreEqual(1, refreshed.Count, "At cap, re-application refreshes instead.");
        }

        [Test]
        public void Remove_RevokesContributions_AndPublishesRemoved()
        {
            var removed = new List<StatusRemoved>();
            using IDisposable token = _bus.Subscribe<StatusRemoved>(removed.Add);
            var frozen = _effects.CreateStatus(
                "status.frozen",
                grantedTags: new[] { RegisteredTag("State.Frozen") },
                modifiers: new[] { new AttributeModifierConfig("attribute.move-speed", ModifierOperation.Flat, -5f) });

            _statuses.Apply(frozen);
            Assert.IsTrue(_statuses.Remove(frozen.Id));

            Assert.AreEqual(1, removed.Count);
            Assert.IsFalse(_tags.HasTagExact(_tagTable.GetTag("State.Frozen")));
            Assert.AreEqual(10f, _attributes.GetValue(MoveSpeed), Tolerance);
            Assert.IsFalse(_statuses.Remove(frozen.Id), "Removing an absent status reports false.");
        }

        [Test]
        public void OnApplyAndOnEnd_EffectSequencesExecute()
        {
            var status = _effects.CreateStatus(
                "status.siphon",
                durationSeconds: 1f,
                onApply: new GameplayEffectDefinition[] { _effects.CreateDamage("fx.bite", "resource.health", 10f) },
                onEnd: new GameplayEffectDefinition[] { _effects.CreateHeal("fx.mend", "resource.health", 4f) });

            _statuses.Apply(status);
            Assert.AreEqual(90f, Health.Current, Tolerance);

            _statuses.Tick(1.5f);
            Assert.AreEqual(94f, Health.Current, Tolerance, "The on-end sequence runs at expiry.");
        }

        [Test]
        public void Infinite_PersistsUntilExplicitlyRemoved()
        {
            var aura = _effects.CreateStatus("status.aura", durationType: StatusDurationType.Infinite);

            _statuses.Apply(aura);
            _statuses.Tick(1000f);

            Assert.IsTrue(_statuses.Has(aura.Id));
        }

        [Test]
        public void Dispose_RevokesSilently()
        {
            var events = new List<StatusRemoved>();
            var expired = new List<StatusExpired>();
            using IDisposable removedToken = _bus.Subscribe<StatusRemoved>(events.Add);
            using IDisposable expiredToken = _bus.Subscribe<StatusExpired>(expired.Add);
            var frozen = _effects.CreateStatus(
                "status.frozen", grantedTags: new[] { RegisteredTag("State.Frozen") });

            _statuses.Apply(frozen);
            _statuses.Dispose();

            Assert.IsFalse(_tags.HasTagExact(_tagTable.GetTag("State.Frozen")));
            Assert.AreEqual(0, events.Count);
            Assert.AreEqual(0, expired.Count);
            Assert.AreEqual(0, _statuses.Count);
        }

        private TagDefinition RegisteredTag(string path)
        {
            TagDefinition tag = _effects.CreateTag(path);
            _tagTable.RegisterTag(path);
            return tag;
        }
    }
}
