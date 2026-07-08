using System;
using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Objects;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.Attributes;
using ToyChest.Systems.GameplayEffects;
using ToyChest.Systems.Resources;
using ToyChest.Systems.Tags;
using ToyChest.Tests.Effects;
using ToyChest.Tests.Events;
using ToyChest.Tests.Resources;

namespace ToyChest.Tests.Abilities
{
    /// <summary>
    /// Verifies the ability orchestration pipeline: grant and revoke, the deterministic
    /// validation order, tag gates, all-or-nothing costs, cooldown lifecycle, Self and
    /// Provided targeting, effect dispatch through the runner, and ability events.
    /// </summary>
    public sealed class AbilitySystemTests
    {
        private const float Tolerance = 1e-4f;
        private static readonly DefinitionId FireballId = new DefinitionId("ability.fireball");
        private static readonly DefinitionId ManaId = new DefinitionId("resource.mana");
        private static readonly DefinitionId HealthId = new DefinitionId("resource.health");

        private AbilityTestFactory _abilities;
        private EffectTestFactory _effects;
        private ResourceTestFactory _definitions;
        private GameplayTagTable _tagTable;
        private EventBus _bus;

        private AttributeSet _ownerAttributes;
        private ResourceSet _ownerResources;
        private GameplayTagContainer _ownerTags;
        private AbilitySet _set;
        private GameplayObjectId _owner;

        [SetUp]
        public void SetUp()
        {
            _abilities = new AbilityTestFactory();
            _effects = new EffectTestFactory();
            _definitions = new ResourceTestFactory();
            _tagTable = new GameplayTagTable();
            _bus = new EventBus(new RecordingLogger());
            _owner = GameplayObjectId.New();

            _ownerAttributes = new AttributeSet();
            _ownerResources = new ResourceSet();
            _ownerResources.AddResource(_definitions.CreateLiteral("resource.mana", 100f));
            _ownerResources.AddResource(_definitions.CreateLiteral("resource.health", 100f));
            _ownerTags = new GameplayTagContainer(_tagTable);

            _set = new AbilitySet(
                _owner, _bus, _tagTable, new GameplayEffectRunner(),
                new EffectTarget(_ownerResources, _ownerAttributes, _ownerTags));
        }

        [TearDown]
        public void TearDown()
        {
            _abilities.Cleanup();
            _effects.Cleanup();
            _definitions.Cleanup();
        }

        private ResourceValue Mana => _ownerResources.GetResource(ManaId);

        private ResourceValue Health => _ownerResources.GetResource(HealthId);

        private AbilityDefinition CreateSelfDamageAbility(
            float manaCost = 20f, float cooldownSeconds = 0f, float damage = 10f)
        {
            return _abilities.CreateAbility(
                "ability.fireball",
                costs: manaCost > 0f ? new[] { new ResourceCostConfig("resource.mana", manaCost) } : null,
                cooldownSeconds: cooldownSeconds,
                effects: new GameplayEffectDefinition[]
                {
                    _effects.CreateDamage("fx.fireball-hit", "resource.health", damage),
                });
        }

        private EffectTarget CreateEnemyTarget(out ResourceSet enemyResources)
        {
            enemyResources = new ResourceSet();
            enemyResources.AddResource(_definitions.CreateLiteral("resource.health", 50f));
            return new EffectTarget(enemyResources, null, new GameplayTagContainer(_tagTable));
        }

        // ---------------------------------------------------------------- Grant / Revoke

        [Test]
        public void Grant_AddsAbility_AndPublishesGranted()
        {
            var granted = new List<AbilityGranted>();
            using IDisposable token = _bus.Subscribe<AbilityGranted>(granted.Add);

            AbilityInstance instance = _set.Grant(CreateSelfDamageAbility());

            Assert.IsTrue(_set.Has(FireballId));
            Assert.AreEqual(1, _set.Count);
            Assert.AreSame(instance, _set.GetAbility(FireballId));
            Assert.AreEqual(1, granted.Count);
            Assert.AreEqual(_owner, granted[0].Owner);
            Assert.AreEqual(FireballId, granted[0].Ability);
        }

        [Test]
        public void Grant_Duplicate_FailsClearly()
        {
            _set.Grant(CreateSelfDamageAbility());
            Assert.Throws<InvalidOperationException>(() => _set.Grant(CreateSelfDamageAbility()));
        }

        [Test]
        public void Grant_Null_FailsClearly()
        {
            Assert.Throws<ArgumentNullException>(() => _set.Grant(null));
        }

        [Test]
        public void Revoke_RemovesAbility_AndPublishesRevoked()
        {
            var revoked = new List<AbilityRevoked>();
            using IDisposable token = _bus.Subscribe<AbilityRevoked>(revoked.Add);
            _set.Grant(CreateSelfDamageAbility());

            Assert.IsTrue(_set.Revoke(FireballId));
            Assert.IsFalse(_set.Has(FireballId));
            Assert.IsNull(_set.GetAbility(FireballId));
            Assert.AreEqual(1, revoked.Count);
            Assert.AreEqual(FireballId, revoked[0].Ability);
        }

        [Test]
        public void Revoke_NotGranted_ReturnsFalse()
        {
            Assert.IsFalse(_set.Revoke(FireballId));
        }

        // ---------------------------------------------------------------- Activation

        [Test]
        public void TryActivate_SelfAbility_ExecutesEffects_ConsumesCosts_AndPublishesActivated()
        {
            var activated = new List<AbilityActivated>();
            using IDisposable token = _bus.Subscribe<AbilityActivated>(activated.Add);
            _set.Grant(CreateSelfDamageAbility(manaCost: 20f, damage: 10f));

            AbilityActivationResult result = _set.TryActivate(FireballId);

            Assert.AreEqual(AbilityActivationResult.Activated, result);
            Assert.AreEqual(80f, Mana.Current, Tolerance, "The mana cost is consumed through the Resource System.");
            Assert.AreEqual(90f, Health.Current, Tolerance, "Self mode executes the effect sequence against the owner.");
            Assert.AreEqual(1, activated.Count);
            Assert.AreEqual(_owner, activated[0].Owner);
            Assert.AreEqual(FireballId, activated[0].Ability);
        }

        [Test]
        public void TryActivate_ProvidedTarget_ExecutesEffectsAgainstTarget_NotOwner()
        {
            AbilityDefinition bolt = _abilities.CreateAbility(
                "ability.bolt",
                targetMode: AbilityTargetMode.Provided,
                effects: new GameplayEffectDefinition[]
                {
                    _effects.CreateDamage("fx.bolt-hit", "resource.health", 15f),
                });
            _set.Grant(bolt);
            EffectTarget enemy = CreateEnemyTarget(out ResourceSet enemyResources);

            AbilityActivationResult result = _set.TryActivate(bolt.Id, in enemy);

            Assert.AreEqual(AbilityActivationResult.Activated, result);
            Assert.AreEqual(35f, enemyResources.GetResource(HealthId).Current, Tolerance);
            Assert.AreEqual(100f, Health.Current, Tolerance, "The owner is the source, never the implicit target.");
        }

        [Test]
        public void TryActivate_NotGranted_FailsAndPublishesFailure()
        {
            var failed = new List<AbilityActivationFailed>();
            using IDisposable token = _bus.Subscribe<AbilityActivationFailed>(failed.Add);

            AbilityActivationResult result = _set.TryActivate(FireballId);

            Assert.AreEqual(AbilityActivationResult.NotGranted, result);
            Assert.AreEqual(1, failed.Count);
            Assert.AreEqual(AbilityActivationResult.NotGranted, failed[0].Reason);
        }

        [Test]
        public void TryActivate_SelfAbilityWithExplicitTarget_IsInvalidTarget()
        {
            _set.Grant(CreateSelfDamageAbility());
            EffectTarget enemy = CreateEnemyTarget(out _);

            Assert.AreEqual(AbilityActivationResult.InvalidTarget, _set.TryActivate(FireballId, in enemy));
            Assert.AreEqual(100f, Mana.Current, Tolerance, "A rejected activation commits nothing.");
        }

        [Test]
        public void TryActivate_ProvidedAbilityWithoutTarget_IsInvalidTarget()
        {
            AbilityDefinition bolt = _abilities.CreateAbility("ability.bolt", targetMode: AbilityTargetMode.Provided);
            _set.Grant(bolt);

            Assert.AreEqual(AbilityActivationResult.InvalidTarget, _set.TryActivate(bolt.Id));
            Assert.AreEqual(AbilityActivationResult.InvalidTarget, _set.TryActivate(bolt.Id, default));
        }

        // ---------------------------------------------------------------- Costs

        [Test]
        public void TryActivate_InsufficientResource_IsCannotAfford_AndConsumesNothing()
        {
            _set.Grant(CreateSelfDamageAbility(manaCost: 20f, cooldownSeconds: 3f));
            Mana.SetCurrent(5f);

            AbilityActivationResult result = _set.TryActivate(FireballId);

            Assert.AreEqual(AbilityActivationResult.CannotAfford, result);
            Assert.AreEqual(5f, Mana.Current, Tolerance);
            Assert.AreEqual(100f, Health.Current, Tolerance, "No effects run on a rejected activation.");
            Assert.IsFalse(_set.GetAbility(FireballId).IsOnCooldown, "No cooldown starts on a rejected activation.");
        }

        [Test]
        public void TryActivate_OwnerLacksResource_IsCannotAfford()
        {
            AbilityDefinition ability = _abilities.CreateAbility(
                "ability.rage",
                costs: new[] { new ResourceCostConfig("resource.rage", 10f) });
            _set.Grant(ability);

            Assert.AreEqual(AbilityActivationResult.CannotAfford, _set.TryActivate(ability.Id));
        }

        [Test]
        public void TryActivate_MultipleCosts_AreAllOrNothing()
        {
            AbilityDefinition ability = _abilities.CreateAbility(
                "ability.blood-magic",
                costs: new[]
                {
                    new ResourceCostConfig("resource.mana", 10f),
                    new ResourceCostConfig("resource.health", 500f),
                });
            _set.Grant(ability);

            AbilityActivationResult result = _set.TryActivate(ability.Id);

            Assert.AreEqual(AbilityActivationResult.CannotAfford, result);
            Assert.AreEqual(100f, Mana.Current, Tolerance, "The affordable cost must not be consumed when another cost fails.");
        }

        // ---------------------------------------------------------------- Tag gates

        [Test]
        public void TryActivate_MissingRequiredTag_Fails_ThenSucceedsWhenPresent()
        {
            _tagTable.RegisterTag("State.Grounded");
            TagDefinition grounded = _effects.CreateTag("State.Grounded");
            AbilityDefinition jump = _abilities.CreateAbility("ability.jump", requiredOwnerTags: new[] { grounded });
            _set.Grant(jump);

            Assert.AreEqual(AbilityActivationResult.MissingRequiredTag, _set.TryActivate(jump.Id));

            _ownerTags.AddTag(_tagTable.GetTag("State.Grounded"));
            Assert.AreEqual(AbilityActivationResult.Activated, _set.TryActivate(jump.Id));
        }

        [Test]
        public void TryActivate_BlockingTagPresent_IsBlockedByTag()
        {
            _tagTable.RegisterTag("State.Stunned");
            TagDefinition stunned = _effects.CreateTag("State.Stunned");
            _set.Grant(_abilities.CreateAbility("ability.dash", blockedByOwnerTags: new[] { stunned }));
            _ownerTags.AddTag(_tagTable.GetTag("State.Stunned"));

            Assert.AreEqual(AbilityActivationResult.BlockedByTag, _set.TryActivate(new DefinitionId("ability.dash")));
        }

        [Test]
        public void TagGates_MatchHierarchically()
        {
            _tagTable.RegisterTag("State.CrowdControl.Stunned");
            TagDefinition crowdControl = _effects.CreateTag("State.CrowdControl");
            _set.Grant(_abilities.CreateAbility("ability.dash", blockedByOwnerTags: new[] { crowdControl }));
            _ownerTags.AddTag(_tagTable.GetTag("State.CrowdControl.Stunned"));

            Assert.AreEqual(
                AbilityActivationResult.BlockedByTag,
                _set.TryActivate(new DefinitionId("ability.dash")),
                "A descendant tag must satisfy an ancestor gate, matching the Tag System's hierarchy rules.");
        }

        // ---------------------------------------------------------------- Cooldowns

        [Test]
        public void TryActivate_StartsCooldown_AndPublishesCooldownStarted()
        {
            var started = new List<AbilityCooldownStarted>();
            using IDisposable token = _bus.Subscribe<AbilityCooldownStarted>(started.Add);
            _set.Grant(CreateSelfDamageAbility(cooldownSeconds: 3f));

            _set.TryActivate(FireballId);

            AbilityInstance instance = _set.GetAbility(FireballId);
            Assert.IsTrue(instance.IsOnCooldown);
            Assert.AreEqual(3f, instance.CooldownRemaining, Tolerance);
            Assert.AreEqual(1, started.Count);
            Assert.AreEqual(3f, started[0].DurationSeconds, Tolerance);
        }

        [Test]
        public void TryActivate_WhileOnCooldown_IsOnCooldown_ThenReadyAfterTick()
        {
            _set.Grant(CreateSelfDamageAbility(manaCost: 0f, cooldownSeconds: 2f));
            _set.TryActivate(FireballId);

            Assert.AreEqual(AbilityActivationResult.OnCooldown, _set.TryActivate(FireballId));

            _set.Tick(2f);
            Assert.AreEqual(AbilityActivationResult.Activated, _set.TryActivate(FireballId));
        }

        [Test]
        public void Tick_PublishesCooldownEnded_ExactlyOncePerReadyTransition()
        {
            var ended = new List<AbilityCooldownEnded>();
            using IDisposable token = _bus.Subscribe<AbilityCooldownEnded>(ended.Add);
            _set.Grant(CreateSelfDamageAbility(cooldownSeconds: 2f));
            _set.TryActivate(FireballId);

            _set.Tick(1f);
            Assert.AreEqual(0, ended.Count);
            Assert.AreEqual(1f, _set.GetAbility(FireballId).CooldownRemaining, Tolerance);

            _set.Tick(1.5f);
            Assert.AreEqual(1, ended.Count);
            Assert.AreEqual(FireballId, ended[0].Ability);

            _set.Tick(1f);
            Assert.AreEqual(1, ended.Count, "A ready ability publishes no further cooldown facts.");
        }

        [Test]
        public void ZeroCooldownAbility_NeverCoolsDown_AndPublishesNoCooldownFacts()
        {
            var started = new List<AbilityCooldownStarted>();
            using IDisposable token = _bus.Subscribe<AbilityCooldownStarted>(started.Add);
            _set.Grant(CreateSelfDamageAbility(manaCost: 0f, cooldownSeconds: 0f));

            Assert.AreEqual(AbilityActivationResult.Activated, _set.TryActivate(FireballId));
            Assert.AreEqual(AbilityActivationResult.Activated, _set.TryActivate(FireballId));
            Assert.IsFalse(_set.GetAbility(FireballId).IsOnCooldown);
            Assert.AreEqual(0, started.Count);
        }

        [Test]
        public void Tick_NegativeDelta_FailsClearly()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _set.Tick(-0.1f));
        }

        // ---------------------------------------------------------------- CanActivate

        [Test]
        public void CanActivate_Validates_WithoutCommittingOrPublishing()
        {
            var failed = new List<AbilityActivationFailed>();
            var activated = new List<AbilityActivated>();
            using IDisposable failedToken = _bus.Subscribe<AbilityActivationFailed>(failed.Add);
            using IDisposable activatedToken = _bus.Subscribe<AbilityActivated>(activated.Add);
            _set.Grant(CreateSelfDamageAbility(manaCost: 20f, cooldownSeconds: 3f));

            Assert.AreEqual(AbilityActivationResult.Activated, _set.CanActivate(FireballId));
            Assert.AreEqual(100f, Mana.Current, Tolerance, "Validation consumes nothing.");
            Assert.IsFalse(_set.GetAbility(FireballId).IsOnCooldown, "Validation starts no cooldown.");
            Assert.AreEqual(100f, Health.Current, Tolerance, "Validation runs no effects.");
            Assert.AreEqual(0, failed.Count + activated.Count, "Validation publishes nothing.");

            Mana.SetCurrent(5f);
            Assert.AreEqual(AbilityActivationResult.CannotAfford, _set.CanActivate(FireballId));
            Assert.AreEqual(0, failed.Count, "A query is not an attempt; only TryActivate publishes failures.");
        }

        [Test]
        public void ValidationOrder_ReportsFirstFailingCheck()
        {
            // On cooldown AND unaffordable: cooldown is checked before costs.
            _set.Grant(CreateSelfDamageAbility(manaCost: 20f, cooldownSeconds: 5f));
            _set.TryActivate(FireballId);
            Mana.SetCurrent(0f);

            Assert.AreEqual(AbilityActivationResult.OnCooldown, _set.CanActivate(FireballId));
        }
    }
}
