using System;
using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Objects;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.GameplayEffects;
using ToyChest.Systems.Interactions;
using ToyChest.Systems.Resources;
using ToyChest.Systems.Tags;
using ToyChest.Tests.Abilities;
using ToyChest.Tests.Effects;
using ToyChest.Tests.Events;
using ToyChest.Tests.Resources;

namespace ToyChest.Tests.Interactions
{
    /// <summary>
    /// Verifies interaction orchestration: routing to the interactable's abilities for Self
    /// and Provided target modes, the deterministic validation order, interactor tag gates,
    /// priority-based selection, availability queries, and the interaction event facts.
    /// </summary>
    public sealed class InteractionSystemTests
    {
        private const float Tolerance = 1e-4f;
        private static readonly DefinitionId OpenId = new DefinitionId("interaction.open");

        private InteractionTestFactory _interactions;
        private AbilityTestFactory _abilities;
        private EffectTestFactory _effects;
        private ResourceTestFactory _resources;

        private GameplayTagTable _tagTable;
        private EventBus _bus;
        private GameplayEffectRunner _runner;
        private InteractionSystem _system;
        private GameplayObject _interactor;
        private ResourceSet _interactorResources;
        private GameplayTagContainer _interactorTags;

        [SetUp]
        public void SetUp()
        {
            _interactions = new InteractionTestFactory();
            _abilities = new AbilityTestFactory();
            _effects = new EffectTestFactory();
            _resources = new ResourceTestFactory();

            _tagTable = new GameplayTagTable();
            _bus = new EventBus(new RecordingLogger());
            _runner = new GameplayEffectRunner();
            _system = new InteractionSystem(_bus, _tagTable);

            _interactorResources = new ResourceSet();
            _interactorResources.AddResource(_resources.CreateLiteral("resource.wood", 100f));
            _interactorTags = new GameplayTagContainer(_tagTable);
            _interactor = new GameplayObject(
                GameplayObjectId.New(), new DefinitionId("object.player"), _bus,
                new IGameplayCapability[] { _interactorResources, _interactorTags });
        }

        [TearDown]
        public void TearDown()
        {
            _interactions.Cleanup();
            _abilities.Cleanup();
            _effects.Cleanup();
            _resources.Cleanup();
        }

        /// <summary>
        /// Builds an interactable the way the composition root does: an ability set owning the
        /// interaction abilities (granted), a tag container, and the interaction set.
        /// </summary>
        private GameplayObject CreateInteractable(params InteractionDefinition[] declared)
        {
            var tags = new GameplayTagContainer(_tagTable);
            var abilitySet = new AbilitySet(
                GameplayObjectId.New(), _bus, _tagTable, _runner, new EffectTarget(null, null, tags));
            var interactionSet = new InteractionSet(declared);
            foreach (InteractionDefinition interaction in declared)
            {
                if (!abilitySet.Has(interaction.Ability.Id))
                {
                    abilitySet.Grant(interaction.Ability);
                }
            }

            return new GameplayObject(
                GameplayObjectId.New(), new DefinitionId("object.chest"), _bus,
                new IGameplayCapability[] { tags, abilitySet, interactionSet });
        }

        private InteractionDefinition CreateOpenInteraction(
            float cooldownSeconds = 0f,
            int priority = 0,
            TagDefinition[] requiredInteractorTags = null,
            TagDefinition[] blockedByInteractorTags = null,
            string id = "interaction.open",
            string abilityId = "ability.open")
        {
            _tagTable.RegisterTag("State.Opened");
            AbilityDefinition open = _abilities.CreateAbility(
                abilityId,
                cooldownSeconds: cooldownSeconds,
                effects: new GameplayEffectDefinition[]
                {
                    _effects.CreateAddTag("fx.mark-opened", _effects.CreateTag("State.Opened")),
                });
            return _interactions.CreateInteraction(
                id, open, priority, requiredInteractorTags, blockedByInteractorTags);
        }

        private InteractionDefinition CreateHarvestInteraction(string id = "interaction.harvest", int priority = 0)
        {
            AbilityDefinition harvest = _abilities.CreateAbility(
                "ability." + id,
                targetMode: AbilityTargetMode.Provided,
                effects: new GameplayEffectDefinition[]
                {
                    _effects.CreateAddResource("fx." + id, "resource.wood", 5f),
                });
            return _interactions.CreateInteraction(id, harvest, priority);
        }

        // ---------------------------------------------------------------- Execution

        [Test]
        public void TryInteract_SelfModeAbility_ActsOnTheInteractable_AndPublishesExecuted()
        {
            var executed = new List<InteractionExecuted>();
            using IDisposable token = _bus.Subscribe<InteractionExecuted>(executed.Add);
            GameplayObject chest = CreateInteractable(CreateOpenInteraction());

            InteractionResult result = _system.TryInteract(_interactor, chest, OpenId);

            Assert.AreEqual(InteractionResult.Executed, result);
            Assert.IsTrue(chest.Get<GameplayTagContainer>().HasTagExact(_tagTable.GetTag("State.Opened")),
                "A Self-mode interaction ability executes against the interactable itself.");
            Assert.AreEqual(1, executed.Count);
            Assert.AreEqual(_interactor.Id, executed[0].Interactor);
            Assert.AreEqual(chest.Id, executed[0].Interactable);
            Assert.AreEqual(OpenId, executed[0].Interaction);
        }

        [Test]
        public void TryInteract_ProvidedModeAbility_TargetsTheInteractor()
        {
            GameplayObject flower = CreateInteractable(CreateHarvestInteraction());
            _interactorResources.GetResource(new DefinitionId("resource.wood")).SetCurrent(0f);

            InteractionResult result = _system.TryInteract(_interactor, flower, new DefinitionId("interaction.harvest"));

            Assert.AreEqual(InteractionResult.Executed, result);
            Assert.AreEqual(5f, _interactorResources.GetResource(new DefinitionId("resource.wood")).Current, Tolerance,
                "A Provided-mode interaction ability receives the interactor as its target.");
        }

        [Test]
        public void TryInteract_NotInteractableObject_Fails_AndPublishesFailure()
        {
            var failed = new List<InteractionFailed>();
            using IDisposable token = _bus.Subscribe<InteractionFailed>(failed.Add);
            var pebble = new GameplayObject(
                GameplayObjectId.New(), new DefinitionId("object.pebble"), _bus,
                new IGameplayCapability[] { new GameplayTagContainer(_tagTable) });

            Assert.AreEqual(InteractionResult.NotInteractable, _system.TryInteract(_interactor, pebble, OpenId));
            Assert.AreEqual(1, failed.Count);
            Assert.AreEqual(InteractionResult.NotInteractable, failed[0].Reason);
        }

        [Test]
        public void TryInteract_UnadvertisedInteraction_IsUnknownInteraction()
        {
            GameplayObject chest = CreateInteractable(CreateOpenInteraction());

            Assert.AreEqual(
                InteractionResult.UnknownInteraction,
                _system.TryInteract(_interactor, chest, new DefinitionId("interaction.pet")));
        }

        [Test]
        public void TryInteract_MissingInteractorTag_Fails_ThenSucceedsWhenPresent()
        {
            _tagTable.RegisterTag("Adventure.HasChestKey");
            TagDefinition key = _effects.CreateTag("Adventure.HasChestKey");
            GameplayObject chest = CreateInteractable(CreateOpenInteraction(requiredInteractorTags: new[] { key }));

            Assert.AreEqual(InteractionResult.MissingInteractorTag, _system.TryInteract(_interactor, chest, OpenId));

            _interactorTags.AddTag(_tagTable.GetTag("Adventure.HasChestKey"));
            Assert.AreEqual(InteractionResult.Executed, _system.TryInteract(_interactor, chest, OpenId));
        }

        [Test]
        public void TryInteract_BlockedInteractorTag_IsBlockedByInteractorTag()
        {
            _tagTable.RegisterTag("State.Stunned");
            TagDefinition stunned = _effects.CreateTag("State.Stunned");
            GameplayObject chest = CreateInteractable(CreateOpenInteraction(blockedByInteractorTags: new[] { stunned }));
            _interactorTags.AddTag(_tagTable.GetTag("State.Stunned"));

            Assert.AreEqual(InteractionResult.BlockedByInteractorTag, _system.TryInteract(_interactor, chest, OpenId));
        }

        [Test]
        public void TryInteract_AbilityOnCooldown_IsAbilityRejected()
        {
            GameplayObject chest = CreateInteractable(CreateOpenInteraction(cooldownSeconds: 10f));

            Assert.AreEqual(InteractionResult.Executed, _system.TryInteract(_interactor, chest, OpenId));
            Assert.AreEqual(InteractionResult.AbilityRejected, _system.TryInteract(_interactor, chest, OpenId),
                "The cooldown belongs to the ability; the interaction reports the rejection.");
        }

        [Test]
        public void CanInteract_Validates_WithoutExecutingOrPublishing()
        {
            var executed = new List<InteractionExecuted>();
            var failed = new List<InteractionFailed>();
            using IDisposable executedToken = _bus.Subscribe<InteractionExecuted>(executed.Add);
            using IDisposable failedToken = _bus.Subscribe<InteractionFailed>(failed.Add);
            GameplayObject chest = CreateInteractable(CreateOpenInteraction());

            Assert.AreEqual(InteractionResult.Executed, _system.CanInteract(_interactor, chest, OpenId));
            Assert.IsFalse(chest.Get<GameplayTagContainer>().HasTagExact(_tagTable.GetTag("State.Opened")),
                "A query activates nothing.");
            Assert.AreEqual(0, executed.Count + failed.Count, "A query publishes nothing.");
        }

        // ---------------------------------------------------------------- Discovery / selection

        [Test]
        public void QueryAvailable_CollectsOnlyValidInteractions_InDeterministicOrder()
        {
            _tagTable.RegisterTag("Adventure.HasChestKey");
            TagDefinition key = _effects.CreateTag("Adventure.HasChestKey");
            GameplayObject chest = CreateInteractable(
                CreateOpenInteraction(),
                CreateInteractionRequiringKey(key));
            GameplayObject flower = CreateInteractable(CreateHarvestInteraction());
            var results = new List<AvailableInteraction>();

            _system.QueryAvailable(_interactor, new[] { chest, flower, null, _interactor }, results);

            Assert.AreEqual(2, results.Count, "The gated interaction and non-interactables are filtered out.");
            Assert.AreSame(chest, results[0].Interactable);
            Assert.AreEqual(OpenId, results[0].Interaction.Id);
            Assert.AreSame(flower, results[1].Interactable);
        }

        [Test]
        public void TrySelectBest_PicksHighestPriority_TiesResolveToEarliestCandidate()
        {
            GameplayObject flower = CreateInteractable(CreateHarvestInteraction());
            GameplayObject shrine = CreateInteractable(CreateOpenInteraction(priority: 5, id: "interaction.activate"));

            Assert.IsTrue(_system.TrySelectBest(_interactor, new[] { flower, shrine }, out AvailableInteraction best));
            Assert.AreSame(shrine, best.Interactable, "Higher priority wins regardless of candidate order.");

            GameplayObject secondFlower = CreateInteractable(CreateHarvestInteraction(id: "interaction.harvest2"));
            Assert.IsTrue(_system.TrySelectBest(_interactor, new[] { flower, secondFlower }, out best));
            Assert.AreSame(flower, best.Interactable, "Equal priorities resolve to the earliest candidate.");
        }

        [Test]
        public void TrySelectBest_NoValidInteraction_ReturnsFalse()
        {
            var pebble = new GameplayObject(
                GameplayObjectId.New(), new DefinitionId("object.pebble"), _bus,
                new IGameplayCapability[] { new GameplayTagContainer(_tagTable) });

            Assert.IsFalse(_system.TrySelectBest(_interactor, new[] { pebble }, out _));
        }

        // ---------------------------------------------------------------- Composition validation

        [Test]
        public void InteractionSet_DuplicateOrAbilitylessInteractions_FailComposition()
        {
            InteractionDefinition open = CreateOpenInteraction();
            Assert.Throws<ArgumentException>(() => new InteractionSet(new[] { open, open }));

            InteractionDefinition broken = _interactions.CreateInteraction("interaction.broken", ability: null);
            Assert.Throws<ArgumentException>(() => new InteractionSet(new[] { broken }));
        }

        private InteractionDefinition CreateInteractionRequiringKey(TagDefinition key)
        {
            AbilityDefinition unlock = _abilities.CreateAbility("ability.unlock");
            return _interactions.CreateInteraction(
                "interaction.unlock", unlock, requiredInteractorTags: new[] { key });
        }
    }
}
