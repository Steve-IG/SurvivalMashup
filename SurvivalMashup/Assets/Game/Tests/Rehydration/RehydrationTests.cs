using System;
using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Modifiers;
using ToyChest.Framework.Objects;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.Attributes;
using ToyChest.Systems.GameplayEffects;
using ToyChest.Systems.Resources;
using ToyChest.Systems.StatusEffects;
using ToyChest.Systems.Tags;
using ToyChest.Tests.Abilities;
using ToyChest.Tests.Effects;
using ToyChest.Tests.Events;
using ToyChest.Tests.Resources;

namespace ToyChest.Tests.Rehydration
{
    /// <summary>
    /// Verifies the runtime restoration APIs the Save Framework will call to rehydrate
    /// authoritative runtime state deterministically without replaying gameplay: resource
    /// current, ability cooldown, and status duration / stacks / periodic accumulator. Each
    /// restore is event-quiet, and restored state advances identically to live state.
    /// </summary>
    public sealed class RehydrationTests
    {
        private const float Tolerance = 1e-4f;

        private EffectTestFactory _effects;
        private AbilityTestFactory _abilities;
        private ResourceTestFactory _definitions;
        private GameplayTagTable _tagTable;
        private EventBus _bus;
        private GameplayObjectId _owner;

        [SetUp]
        public void SetUp()
        {
            _effects = new EffectTestFactory();
            _abilities = new AbilityTestFactory();
            _definitions = new ResourceTestFactory();
            _tagTable = new GameplayTagTable();
            _bus = new EventBus(new RecordingLogger());
            _owner = GameplayObjectId.New();
        }

        [TearDown]
        public void TearDown()
        {
            _effects.Cleanup();
            _abilities.Cleanup();
            _definitions.Cleanup();
        }

        // ---------------------------------------------------------------- Resource

        [Test]
        public void ResourceValue_Restore_SetsClampedCurrent_Quietly()
        {
            var value = new ResourceValue(_definitions.CreateLiteral("resource.health", 100f));
            var changes = new List<float>();
            value.ValueChanged += (previous, next, max) => changes.Add(next);

            value.RestoreCurrent(40f);
            Assert.AreEqual(40f, value.Current, Tolerance);
            Assert.AreEqual(0, changes.Count, "RestoreCurrent reconstructs state without replaying change callbacks.");

            value.RestoreCurrent(999f);
            Assert.AreEqual(100f, value.Current, Tolerance, "RestoreCurrent clamps to the maximum.");

            value.RestoreCurrent(-5f);
            Assert.AreEqual(0f, value.Current, Tolerance, "RestoreCurrent clamps to zero.");
        }

        // ---------------------------------------------------------------- Ability cooldown

        [Test]
        public void AbilitySet_RestoreCooldown_RestoresRemaining_Quietly_AndResumesTicking()
        {
            AbilitySet set = CreateAbilitySet(out ResourceSet _);
            AbilityDefinition ability = _abilities.CreateAbility("ability.dash", cooldownSeconds: 5f);
            set.Grant(ability);

            var ended = new List<AbilityCooldownEnded>();
            using IDisposable token = _bus.Subscribe<AbilityCooldownEnded>(ended.Add);

            set.RestoreCooldown(ability.Id, 3f);
            Assert.AreEqual(3f, set.GetAbility(ability.Id).CooldownRemaining, Tolerance);
            Assert.IsTrue(set.GetAbility(ability.Id).IsOnCooldown);
            Assert.AreEqual(0, ended.Count, "Restore publishes nothing.");

            set.Tick(3f);
            Assert.AreEqual(0f, set.GetAbility(ability.Id).CooldownRemaining, Tolerance);
            Assert.AreEqual(1, ended.Count, "Restored cooldown resumes ticking and ends exactly once.");
        }

        [Test]
        public void AbilitySet_RestoreCooldown_UngrantedAbility_Throws()
        {
            AbilitySet set = CreateAbilitySet(out ResourceSet _);
            Assert.Throws<InvalidOperationException>(() => set.RestoreCooldown(new DefinitionId("ability.absent"), 1f));
        }

        // ---------------------------------------------------------------- Status

        [Test]
        public void StatusEffectSet_Restore_ReappliesContributions_Quietly_AndResumesDeterministically()
        {
            AttributeSet attributes = new AttributeSet();
            attributes.AddAttribute(_definitions.CreateAttribute("attribute.move-speed", 10f));
            ResourceSet resources = new ResourceSet();
            resources.AddResource(_definitions.CreateLiteral("resource.health", 100f));

            var tags = new GameplayTagContainer(_tagTable);
            var statuses = new StatusEffectSet(
                _owner, _bus, _tagTable, new GameplayEffectRunner(),
                new EffectTarget(resources, attributes, tags));

            _tagTable.RegisterTag("State.Burning");
            StatusEffectDefinition burning = _effects.CreateStatus(
                "status.burning",
                durationType: StatusDurationType.Timed,
                durationSeconds: 3f,
                stackingRule: StatusStackingRule.IncreaseStacks,
                maximumStacks: 5,
                grantedTags: new[] { _effects.CreateTag("State.Burning") },
                modifiers: new[] { new AttributeModifierConfig("attribute.move-speed", ModifierOperation.Flat, -2f) },
                periodSeconds: 1f,
                periodic: new GameplayEffectDefinition[]
                {
                    _effects.CreateDamage("fx.burn", "resource.health", 5f),
                });

            var applied = new List<StatusApplied>();
            using IDisposable token = _bus.Subscribe<StatusApplied>(applied.Add);

            StatusEffectInstance restored = statuses.Restore(burning, stacks: 2, remainingSeconds: 1.5f, periodAccumulator: 0.5f);

            Assert.AreEqual(0, applied.Count, "Reconstruction is not a fresh application; no StatusApplied.");
            Assert.AreEqual(2, restored.Stacks);
            Assert.AreEqual(1.5f, restored.RemainingSeconds, Tolerance);
            Assert.AreEqual(0.5f, restored.PeriodAccumulator, Tolerance);
            Assert.IsTrue(tags.HasTagExact(_tagTable.GetTag("State.Burning")), "Granted tag is re-applied.");
            Assert.AreEqual(6f, attributes.GetValue(new DefinitionId("attribute.move-speed")), Tolerance,
                "Modifier is re-applied once per restored stack (10 - 2 - 2).");

            // Resume: 0.5 accumulator + 0.5 delta reaches one period -> exactly one periodic tick.
            statuses.Tick(0.5f);
            Assert.AreEqual(95f, resources.GetResource(new DefinitionId("resource.health")).Current, Tolerance);
            Assert.AreEqual(1.0f, restored.RemainingSeconds, Tolerance);
        }

        [Test]
        public void StatusEffectSet_Restore_AlreadyActive_Throws()
        {
            var statuses = new StatusEffectSet(
                _owner, _bus, _tagTable, new GameplayEffectRunner(),
                new EffectTarget(null, null, new GameplayTagContainer(_tagTable)));
            StatusEffectDefinition status = _effects.CreateStatus("status.plain", durationSeconds: 5f);

            statuses.Restore(status, stacks: 1, remainingSeconds: 5f, periodAccumulator: 0f);
            Assert.Throws<InvalidOperationException>(
                () => statuses.Restore(status, stacks: 1, remainingSeconds: 5f, periodAccumulator: 0f));
        }

        // ---------------------------------------------------------------- Deterministic regen

        [Test]
        public void ResourceSet_Regenerate_PublishesInRegistrationOrder()
        {
            var resources = new ResourceSet(_bus, owner: _owner);
            resources.AddResource(_definitions.CreateLiteral("resource.alpha", 100f, startAtMaximum: false, startingValue: 0f, regenPerSecond: 1f));
            resources.AddResource(_definitions.CreateLiteral("resource.beta", 100f, startAtMaximum: false, startingValue: 0f, regenPerSecond: 1f));

            var order = new List<DefinitionId>();
            using IDisposable token = _bus.Subscribe<ResourceChanged>(e => order.Add(e.Resource));

            resources.Regenerate(1f);

            Assert.AreEqual(
                new[] { new DefinitionId("resource.alpha"), new DefinitionId("resource.beta") },
                order,
                "Regeneration and its events follow deterministic registration order.");
        }

        private AbilitySet CreateAbilitySet(out ResourceSet resources)
        {
            resources = new ResourceSet();
            resources.AddResource(_definitions.CreateLiteral("resource.mana", 100f));
            return new AbilitySet(
                _owner, _bus, _tagTable, new GameplayEffectRunner(),
                new EffectTarget(resources, new AttributeSet(), new GameplayTagContainer(_tagTable)));
        }
    }
}
