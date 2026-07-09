using System;
using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Boot;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Objects;
using ToyChest.Systems.Attributes;
using ToyChest.Systems.Resources;
using ToyChest.Systems.Save;
using ToyChest.Systems.Tags;
using ToyChest.Tests.Events;
using ToyChest.Tests.Resources;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ToyChest.Tests.Boot
{
    /// <summary>
    /// Verifies the permanent runtime startup path (Docs/Architecture/ENGINE_STARTUP.md) end-to-end
    /// through <see cref="RuntimeBootstrap"/>: services are created in dependency order, the Data
    /// Registry is populated and the Tag Table interned during Registry Population, a new game boots
    /// to an empty world, a save is reconstructed and activated during startup, and authored
    /// definitions compose into live objects through the bootstrapped factory. Runs the real
    /// bootstrap with an in-memory <see cref="DirectDefinitionSource"/> — the same code the scene
    /// runs, without a content build (per DATA_REGISTRY.md, Definition Sources).
    /// </summary>
    public sealed class RuntimeBootstrapTests
    {
        private const float Tolerance = 1e-4f;

        private readonly List<Object> _created = new List<Object>();
        private ResourceTestFactory _resources;
        private RecordingLogger _logger;

        [SetUp]
        public void SetUp()
        {
            _resources = new ResourceTestFactory();
            _logger = new RecordingLogger();
        }

        [TearDown]
        public void TearDown()
        {
            _resources.Cleanup();
            foreach (Object created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        // ------------------------------------------------------------------ Service Creation

        [Test]
        public void Run_CreatesEveryService_AndLeavesRegistryReadyToServe()
        {
            RuntimeServices services = new RuntimeBootstrap().Run(NewConfig());

            Assert.IsNotNull(services.Logger);
            Assert.IsNotNull(services.EventBus);
            Assert.IsNotNull(services.DataRegistry);
            Assert.IsNotNull(services.TagTable);
            Assert.IsNotNull(services.Objects);
            Assert.IsNotNull(services.Context);
            Assert.IsNotNull(services.Factory);
            Assert.IsNotNull(services.SaveManager);

            Assert.AreSame(services.DataRegistry, services.Context.DataRegistry,
                "The composition context must inject the same registry the bootstrap populated.");
            Assert.AreSame(services.TagTable, services.Context.TagTable);
            Assert.AreSame(services.Objects, services.Context.Registry,
                "Objects composed through the factory must join the services' live-object set.");
        }

        // ------------------------------------------------------------------ Registry Population

        [Test]
        public void Run_PopulatesDataRegistry_FromSources()
        {
            AttributeDefinition maxHealth = _resources.CreateAttribute("attribute.max-health", 100f);
            ResourceDefinition health = _resources.CreateBound("resource.health", "attribute.max-health");
            GameplayObjectDefinition hero = CreateObjectDefinition("object.hero", new[] { maxHealth }, new[] { health });

            RuntimeServices services = new RuntimeBootstrap().Run(
                NewConfig(new DirectDefinitionSource(maxHealth, health, hero)));

            Assert.IsTrue(services.DataRegistry.Contains<GameplayObjectDefinition>(new DefinitionId("object.hero")));
            Assert.IsTrue(services.DataRegistry.Contains<AttributeDefinition>(new DefinitionId("attribute.max-health")));
            Assert.IsTrue(services.DataRegistry.Contains<ResourceDefinition>(new DefinitionId("resource.health")));
        }

        [Test]
        public void Run_InternsTagHierarchy_FromRegisteredTagDefinitions()
        {
            TagDefinition burning = CreateTagDefinition("Element.Fire.Burning");

            RuntimeServices services = new RuntimeBootstrap().Run(NewConfig(new DirectDefinitionSource(burning)));

            Assert.AreEqual(3, services.TagTable.Count, "Registering a leaf path interns every ancestor.");
            Assert.IsTrue(services.TagTable.TryGetTag("Element", out _));
            Assert.IsTrue(services.TagTable.TryGetTag("Element.Fire", out _));
            Assert.IsTrue(services.TagTable.TryGetTag("Element.Fire.Burning", out _));
        }

        [Test]
        public void Run_DuplicateDefinition_FailsAtStartup()
        {
            AttributeDefinition first = _resources.CreateAttribute("attribute.max-health", 100f);
            AttributeDefinition duplicate = _resources.CreateAttribute("attribute.max-health", 50f);

            Assert.Throws<InvalidOperationException>(
                () => new RuntimeBootstrap().Run(NewConfig(new DirectDefinitionSource(first, duplicate))),
                "Duplicate ids must surface at startup, never mid-session.");
        }

        [Test]
        public void Run_MultipleSources_RegisterInOrder()
        {
            AttributeDefinition attribute = _resources.CreateAttribute("attribute.armor", 5f);
            TagDefinition tag = CreateTagDefinition("Enemy.Beast");

            RuntimeServices services = new RuntimeBootstrap().Run(NewConfig(
                new DirectDefinitionSource(attribute),
                new DirectDefinitionSource(tag)));

            Assert.IsTrue(services.DataRegistry.Contains<AttributeDefinition>(new DefinitionId("attribute.armor")));
            Assert.IsTrue(services.TagTable.TryGetTag("Enemy.Beast", out _));
        }

        // ------------------------------------------------------------------ Save Loading / new game

        [Test]
        public void Run_NewGame_LeavesEmptyWorld()
        {
            RuntimeServices services = new RuntimeBootstrap().Run(NewConfig());

            Assert.AreEqual(0, services.Objects.Count);
            Assert.AreEqual(0, services.RestoredObjects.Count);
        }

        [Test]
        public void Run_ReconstructsAndActivatesSavedObjects_ThroughBootstrapPath()
        {
            AttributeDefinition maxHealth = _resources.CreateAttribute("attribute.max-health", 100f);
            ResourceDefinition health = _resources.CreateBound("resource.health", "attribute.max-health");
            GameplayObjectDefinition hero = CreateObjectDefinition("object.hero", new[] { maxHealth }, new[] { health });

            // A prior session: compose a hero, damage it, capture authoritative state to JSON.
            string saveJson = CaptureHeroSave(hero, currentHealth: 42f, out GameplayObjectId heroId);

            // Boot a fresh session from that save and the same content.
            RuntimeServices services = new RuntimeBootstrap().Run(
                NewConfig(saveJson, new DirectDefinitionSource(maxHealth, health, hero)));

            Assert.AreEqual(1, services.RestoredObjects.Count);
            GameplayObject restored = services.RestoredObjects[0];
            Assert.AreEqual(heroId, restored.Id, "Identity is authoritative and preserved across the save boundary.");
            Assert.IsTrue(restored.IsActive, "Reconstructed objects are activated during Runtime Initialization.");
            Assert.IsTrue(services.Objects.Contains(heroId), "Activation registers the object in the live world.");

            ResourceValue restoredHealth = restored.Get<ResourceSet>().GetResource(new DefinitionId("resource.health"));
            Assert.AreEqual(100f, restoredHealth.Maximum, Tolerance, "The bound maximum is rebuilt by composition.");
            Assert.AreEqual(42f, restoredHealth.Current, Tolerance, "The authoritative current value is restored.");
        }

        // ------------------------------------------------------------------ Live composition path

        [Test]
        public void Run_ThenComposeAndActivate_FromAuthoredDefinition_JoinsLiveWorld()
        {
            AttributeDefinition maxHealth = _resources.CreateAttribute("attribute.max-health", 100f);
            GameplayObjectDefinition wolf = CreateObjectDefinition("object.wolf", new[] { maxHealth }, null);

            RuntimeServices services = new RuntimeBootstrap().Run(NewConfig(new DirectDefinitionSource(maxHealth, wolf)));

            // The permanent runtime path: resolve an authored definition, compose it through the
            // bootstrapped factory, activate it, and it registers in the live-object set.
            GameplayObjectDefinition resolved =
                services.DataRegistry.Get<GameplayObjectDefinition>(new DefinitionId("object.wolf"));
            GameplayObject composed = services.Factory.Create(resolved);
            Assert.IsFalse(composed.IsActive, "The factory composes; the spawner activates.");
            Assert.AreEqual(0, services.Objects.Count);

            composed.Activate();
            Assert.IsTrue(composed.IsActive);
            Assert.AreEqual(1, services.Objects.Count);
            Assert.IsTrue(services.Objects.Contains(composed.Id));
        }

        // ------------------------------------------------------------------ Guards

        [Test]
        public void Run_NullConfiguration_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new RuntimeBootstrap().Run(null));
        }

        [Test]
        public void Configuration_RequiresLoggerAndAtLeastOneSource()
        {
            Assert.Throws<ArgumentNullException>(
                () => new BootstrapConfiguration(null, new[] { new DirectDefinitionSource() }));
            Assert.Throws<ArgumentException>(
                () => new BootstrapConfiguration(_logger, Array.Empty<IDefinitionSource>()));
        }

        // ------------------------------------------------------------------ Helpers

        private BootstrapConfiguration NewConfig(params IDefinitionSource[] sources)
        {
            return NewConfig(null, sources);
        }

        private BootstrapConfiguration NewConfig(string saveJson, params IDefinitionSource[] sources)
        {
            IEnumerable<IDefinitionSource> effective =
                sources != null && sources.Length > 0
                    ? sources
                    : new IDefinitionSource[] { new DirectDefinitionSource() };
            return new BootstrapConfiguration(_logger, effective, saveJson);
        }

        private string CaptureHeroSave(GameplayObjectDefinition hero, float currentHealth, out GameplayObjectId heroId)
        {
            var tagTable = new GameplayTagTable();
            var registry = new DataRegistry();
            registry.Register(hero);
            var world = new GameplayObjectRegistry();
            var bus = new ToyChest.Framework.Events.EventBus(_logger);
            var factory = new GameplayObjectFactory(new GameplayObjectContext(bus, registry, tagTable, world));

            GameplayObject heroObject = factory.Create(hero);
            heroObject.Activate();
            heroObject.Get<ResourceSet>().GetResource(new DefinitionId("resource.health")).SetCurrent(currentHealth);
            heroId = heroObject.Id;

            var save = new SaveManager();
            return save.ToJson(save.Capture(world));
        }

        private GameplayObjectDefinition CreateObjectDefinition(
            string id, AttributeDefinition[] attributes, ResourceDefinition[] resources)
        {
            var definition = ScriptableObject.CreateInstance<GameplayObjectDefinition>();
            _created.Add(definition);

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_definitionId").stringValue = id;
            FillArray(serialized.FindProperty("_attributes"), attributes);
            FillArray(serialized.FindProperty("_resources"), resources);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private TagDefinition CreateTagDefinition(string path)
        {
            var tag = ScriptableObject.CreateInstance<TagDefinition>();
            _created.Add(tag);
            var serialized = new SerializedObject(tag);
            serialized.FindProperty("_tagPath").stringValue = path;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return tag;
        }

        private static void FillArray(SerializedProperty property, Object[] values)
        {
            int count = values?.Length ?? 0;
            property.arraySize = count;
            for (int i = 0; i < count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }
    }
}
