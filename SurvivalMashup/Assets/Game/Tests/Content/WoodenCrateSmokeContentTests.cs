using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Boot;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Objects;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.Attributes;
using ToyChest.Systems.Interactions;
using ToyChest.Systems.Inventory;
using ToyChest.Systems.Resources;
using ToyChest.Systems.Tags;
using ToyChest.Tests.Events;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ToyChest.Tests.Content
{
    /// <summary>
    /// End-to-end verification of the authored Milestone 1 smoke content (the Wooden Crate) through
    /// the real runtime pipeline: the authored ScriptableObject definitions are loaded from disk,
    /// registered through the permanent <see cref="RuntimeBootstrap"/> (via an in-memory
    /// <see cref="DirectDefinitionSource"/> — the same registration path Addressables feeds in a
    /// build, without a content build), composed into a live Gameplay Object, activated, and
    /// discovered by the Interaction System. This proves Authoring → Definition Source → Data
    /// Registry → Factory → Gameplay Object → Registry → Interaction against the actual assets.
    /// </summary>
    public sealed class WoodenCrateSmokeContentTests
    {
        private const float Tolerance = 1e-4f;
        private const string DefinitionsFolder = "Assets/Game/Content/Definitions";

        private const string CrateId = "object.wooden_crate";
        private const string MaxHealthId = "attribute.max_health";
        private const string HealthId = "resource.health";
        private const string OpenAbilityId = "ability.open_crate";
        private const string OpenInteractionId = "interaction.open";
        private const string ContainerTagPath = "Object.Container.Crate";
        private const string OpenStateTagPath = "Object.State.Open";

        private RuntimeServices _services;

        [SetUp]
        public void SetUp()
        {
            _services = new RuntimeBootstrap().Run(
                new BootstrapConfiguration(new RecordingLogger(), new[] { LoadAuthoredDefinitions() }));
        }

        [Test]
        public void AuthoredDefinitions_PopulateTheRegistry()
        {
            Assert.IsTrue(_services.DataRegistry.Contains<GameplayObjectDefinition>(new DefinitionId(CrateId)),
                "The Wooden Crate object definition should load and register.");
            Assert.IsTrue(_services.DataRegistry.Contains<AttributeDefinition>(new DefinitionId(MaxHealthId)));
            Assert.IsTrue(_services.DataRegistry.Contains<ResourceDefinition>(new DefinitionId(HealthId)));

            // Tag definitions interned their whole ancestry into the Tag Table during startup.
            Assert.IsTrue(_services.TagTable.TryGetTag(ContainerTagPath, out _));
            Assert.IsTrue(_services.TagTable.TryGetTag(OpenStateTagPath, out _));
        }

        [Test]
        public void Crate_ComposesEveryDeclaredCapability_AndActivatesIntoTheWorld()
        {
            GameplayObject crate = SpawnCrate();

            Assert.IsTrue(crate.IsActive);
            Assert.IsTrue(_services.Objects.Contains(crate.Id),
                "Activation registers the crate in the live-object set.");

            // Attribute → Resource: Current Health binds its maximum to Maximum Health (50) and
            // starts full.
            Assert.AreEqual(50f, crate.Get<AttributeSet>().GetValue(new DefinitionId(MaxHealthId)), Tolerance);
            ResourceValue health = crate.Get<ResourceSet>().GetResource(new DefinitionId(HealthId));
            Assert.AreEqual(50f, health.Maximum, Tolerance);
            Assert.AreEqual(50f, health.Current, Tolerance);

            // Initial tag, inventory, and the interaction (which granted its ability at composition).
            Assert.IsTrue(crate.Get<GameplayTagContainer>().HasTag(_services.TagTable.GetTag(ContainerTagPath)));
            Assert.AreEqual(4, crate.Get<InventorySet>().SlotCapacity);
            Assert.IsTrue(crate.Get<InteractionSet>().Has(new DefinitionId(OpenInteractionId)));
            Assert.IsTrue(crate.Get<AbilitySet>().Has(new DefinitionId(OpenAbilityId)),
                "The Open interaction's ability is granted to the crate at composition.");
        }

        [Test]
        public void InteractionSystem_DiscoversAndExecutes_TheOpenInteraction()
        {
            GameplayObject crate = SpawnCrate();
            GameplayObject interactor = SpawnCrate(); // any actor; Open gates on no interactor tags.

            var interactions = new InteractionSystem(_services.EventBus, _services.TagTable);
            var candidates = new List<GameplayObject> { crate };

            Assert.IsTrue(interactions.TrySelectBest(interactor, candidates, out AvailableInteraction best),
                "The authored Open interaction should be discoverable on the crate.");
            Assert.AreEqual(new DefinitionId(OpenInteractionId), best.Interaction.Id);
            Assert.AreSame(crate, best.Interactable);

            InteractionResult result =
                interactions.TryInteract(interactor, crate, new DefinitionId(OpenInteractionId));

            Assert.AreEqual(InteractionResult.Executed, result);
            Assert.IsTrue(crate.Get<GameplayTagContainer>().HasTag(_services.TagTable.GetTag(OpenStateTagPath)),
                "Executing Open runs its ability's effect, adding the Open state tag to the crate.");
        }

        private GameplayObject SpawnCrate()
        {
            GameplayObjectDefinition definition =
                _services.DataRegistry.Get<GameplayObjectDefinition>(new DefinitionId(CrateId));
            GameplayObject crate = _services.Factory.Create(definition);
            crate.Activate();
            return crate;
        }

        // Loads every authored definition asset from the content folder, exactly as the Addressables
        // "definitions" label loads them in a build, but resolved through the AssetDatabase so the
        // test needs no content build.
        private static DirectDefinitionSource LoadAuthoredDefinitions()
        {
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { DefinitionsFolder });
            var definitions = new List<IDefinition>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (asset is IDefinition definition)
                {
                    definitions.Add(definition);
                }
            }

            Assert.Greater(definitions.Count, 0,
                $"No authored definitions found under {DefinitionsFolder}. Run the smoke-content authoring.");
            return new DirectDefinitionSource(definitions);
        }
    }
}
