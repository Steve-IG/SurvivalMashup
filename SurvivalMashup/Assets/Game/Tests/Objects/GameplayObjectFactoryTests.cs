using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Objects;
using ToyChest.Systems.Attributes;
using ToyChest.Systems.Resources;
using ToyChest.Systems.Tags;
using ToyChest.Tests.Events;
using ToyChest.Tests.Resources;
using UnityEditor;
using UnityEngine;

namespace ToyChest.Tests.Objects
{
    /// <summary>
    /// Verifies the composition root: capability assembly from definitions in dependency
    /// order, attribute-bound resource wiring, initial tags, the always-present tag
    /// container, and composed-but-not-activated delivery.
    /// </summary>
    public sealed class GameplayObjectFactoryTests
    {
        private ResourceTestFactory _definitions;
        private readonly List<Object> _created = new List<Object>();
        private GameplayTagTable _tagTable;
        private GameplayObjectFactory _factory;
        private EventBus _bus;

        [SetUp]
        public void SetUp()
        {
            _definitions = new ResourceTestFactory();
            _tagTable = new GameplayTagTable();
            _bus = new EventBus(new RecordingLogger());
            _factory = new GameplayObjectFactory(
                new GameplayObjectContext(_bus, new DataRegistry(), _tagTable));
        }

        [TearDown]
        public void TearDown()
        {
            _definitions.Cleanup();
            foreach (Object created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public void Create_ComposesDeclaredCapabilities_WithBoundResources()
        {
            AttributeDefinition maxHealth = _definitions.CreateAttribute("attribute.max-health", 100f);
            ResourceDefinition health = _definitions.CreateBound("resource.health", "attribute.max-health");
            GameplayObjectDefinition definition = CreateObjectDefinition(
                "object.wolf", new[] { maxHealth }, new[] { health }, new[] { "Enemy.Beast" });

            GameplayObject wolf = _factory.Create(definition);

            Assert.IsTrue(wolf.Has<AttributeSet>());
            Assert.IsTrue(wolf.Has<ResourceSet>());
            Assert.IsTrue(wolf.Has<GameplayTagContainer>());

            ResourceValue healthValue = wolf.Get<ResourceSet>().GetResource(new DefinitionId("resource.health"));
            Assert.AreEqual(100f, healthValue.Maximum, 1e-4f, "The bound maximum must resolve against this object's attributes.");

            GameplayTag beast = _tagTable.GetTag("Enemy.Beast");
            Assert.IsTrue(wolf.Get<GameplayTagContainer>().HasTagExact(beast));
        }

        [Test]
        public void Create_EmptyDefinition_StillHasUniversalCapabilities_AndNothingElse()
        {
            GameplayObjectDefinition definition = CreateObjectDefinition("object.pebble", null, null, null);

            GameplayObject pebble = _factory.Create(definition);

            Assert.IsTrue(pebble.Has<GameplayTagContainer>(), "Every object carries a tag container for runtime state tags.");
            Assert.IsTrue(pebble.Has<ToyChest.Systems.StatusEffects.StatusEffectSet>(), "Every object can receive statuses.");
            Assert.IsTrue(pebble.Has<ToyChest.Systems.Abilities.AbilitySet>(), "Every object can receive ability grants at runtime.");
            Assert.IsFalse(pebble.Has<AttributeSet>());
            Assert.IsFalse(pebble.Has<ResourceSet>());
            Assert.AreEqual(3, pebble.CapabilityCount);
        }

        [Test]
        public void Create_ReturnsComposedButNotActivated()
        {
            var spawned = new List<GameplayObjectSpawned>();
            using System.IDisposable token = _bus.Subscribe<GameplayObjectSpawned>(spawned.Add);
            GameplayObjectDefinition definition = CreateObjectDefinition("object.pebble", null, null, null);

            GameplayObject pebble = _factory.Create(definition);
            Assert.IsFalse(pebble.IsActive);
            Assert.AreEqual(0, spawned.Count, "The factory composes; the spawner activates.");

            pebble.Activate();
            Assert.AreEqual(1, spawned.Count);
            Assert.AreEqual(new DefinitionId("object.pebble"), spawned[0].Definition);
        }

        [Test]
        public void Create_DistinctObjects_GetDistinctIds()
        {
            GameplayObjectDefinition definition = CreateObjectDefinition("object.pebble", null, null, null);

            GameplayObject first = _factory.Create(definition);
            GameplayObject second = _factory.Create(definition);

            Assert.AreNotEqual(first.Id, second.Id);
            Assert.AreEqual(first.DefinitionId, second.DefinitionId);
        }

        [Test]
        public void ComposedObject_RunsStatusEffects_EndToEnd()
        {
            var effects = new ToyChest.Tests.Effects.EffectTestFactory();
            try
            {
                AttributeDefinition maxHealth = _definitions.CreateAttribute("attribute.max-health", 100f);
                ResourceDefinition health = _definitions.CreateBound("resource.health", "attribute.max-health");
                GameplayObjectDefinition definition = CreateObjectDefinition(
                    "object.wolf", new[] { maxHealth }, new[] { health }, null);

                GameplayObject wolf = _factory.Create(definition);
                wolf.Activate();

                _tagTable.RegisterTag("State.Burning");
                var burning = effects.CreateStatus(
                    "status.burning",
                    durationSeconds: 2f,
                    grantedTags: new[] { effects.CreateTag("State.Burning") },
                    periodSeconds: 1f,
                    periodic: new ToyChest.Systems.GameplayEffects.GameplayEffectDefinition[]
                    {
                        effects.CreateDamage("fx.burn", "resource.health", 10f),
                    });

                wolf.Get<ToyChest.Systems.StatusEffects.StatusEffectSet>().Apply(burning);
                Assert.IsTrue(wolf.Get<GameplayTagContainer>().HasTagExact(_tagTable.GetTag("State.Burning")));

                wolf.Tick(1f);
                wolf.Tick(1f);
                wolf.Tick(0.5f);

                ResourceValue healthValue = wolf.Get<ResourceSet>().GetResource(new DefinitionId("resource.health"));
                Assert.AreEqual(80f, healthValue.Current, 1e-4f, "Two burn ticks inside the 2s lifetime.");
                Assert.IsFalse(wolf.Get<ToyChest.Systems.StatusEffects.StatusEffectSet>().Has(burning.Id));
                Assert.IsFalse(wolf.Get<GameplayTagContainer>().HasTagExact(_tagTable.GetTag("State.Burning")),
                    "Expiry revokes the granted tag on the composed object.");
            }
            finally
            {
                effects.Cleanup();
            }
        }

        [Test]
        public void ComposedObject_ActivatesInitialAbility_EndToEnd()
        {
            var effects = new ToyChest.Tests.Effects.EffectTestFactory();
            var abilities = new ToyChest.Tests.Abilities.AbilityTestFactory();
            try
            {
                ResourceDefinition mana = _definitions.CreateLiteral("resource.mana", 50f);
                ResourceDefinition health = _definitions.CreateLiteral("resource.health", 100f);
                ToyChest.Systems.Abilities.AbilityDefinition mend = abilities.CreateAbility(
                    "ability.mend",
                    costs: new[] { new ToyChest.Systems.Abilities.ResourceCostConfig("resource.mana", 30f) },
                    cooldownSeconds: 4f,
                    effects: new ToyChest.Systems.GameplayEffects.GameplayEffectDefinition[]
                    {
                        effects.CreateHeal("fx.mend", "resource.health", 25f),
                    });

                GameplayObjectDefinition definition = CreateObjectDefinition(
                    "object.cleric", null, new[] { mana, health }, null, initialAbilities: new[] { mend });

                GameplayObject cleric = _factory.Create(definition);
                cleric.Activate();

                var abilitySet = cleric.Get<ToyChest.Systems.Abilities.AbilitySet>();
                Assert.IsTrue(abilitySet.Has(mend.Id), "Declared abilities are granted at composition.");

                ResourceValue clericHealth = cleric.Get<ResourceSet>().GetResource(new DefinitionId("resource.health"));
                clericHealth.SetCurrent(60f);

                Assert.AreEqual(
                    ToyChest.Systems.Abilities.AbilityActivationResult.Activated,
                    abilitySet.TryActivate(mend.Id));
                Assert.AreEqual(85f, clericHealth.Current, 1e-4f);
                Assert.AreEqual(20f, cleric.Get<ResourceSet>().GetResource(new DefinitionId("resource.mana")).Current, 1e-4f);

                Assert.AreEqual(
                    ToyChest.Systems.Abilities.AbilityActivationResult.OnCooldown,
                    abilitySet.TryActivate(mend.Id));

                cleric.Tick(4f);
                Assert.AreEqual(
                    ToyChest.Systems.Abilities.AbilityActivationResult.CannotAfford,
                    abilitySet.TryActivate(mend.Id),
                    "The object's Tick advances ability cooldowns; the second cast now fails only on mana.");
            }
            finally
            {
                effects.Cleanup();
                abilities.Cleanup();
            }
        }

        [Test]
        public void Create_UnregisteredInitialTag_FailsClearly()
        {
            GameplayObjectDefinition definition = CreateObjectDefinition(
                "object.ghost", null, null, new[] { "Enemy.Unregistered" }, registerTags: false);

            Assert.Throws<KeyNotFoundException>(() => _factory.Create(definition));
        }

        [Test]
        public void Create_SeedsInitialTagsAndAbilities_EventQuietly_UntilActivation()
        {
            var tagsAdded = new List<GameplayTagAdded>();
            var granted = new List<ToyChest.Systems.Abilities.AbilityGranted>();
            var spawned = new List<GameplayObjectSpawned>();
            using System.IDisposable a = _bus.Subscribe<GameplayTagAdded>(tagsAdded.Add);
            using System.IDisposable b = _bus.Subscribe<ToyChest.Systems.Abilities.AbilityGranted>(granted.Add);
            using System.IDisposable c = _bus.Subscribe<GameplayObjectSpawned>(spawned.Add);

            var abilities = new ToyChest.Tests.Abilities.AbilityTestFactory();
            try
            {
                ToyChest.Systems.Abilities.AbilityDefinition dash = abilities.CreateAbility("ability.dash");
                GameplayObjectDefinition definition = CreateObjectDefinition(
                    "object.wolf", null, null, new[] { "Enemy.Beast" }, initialAbilities: new[] { dash });

                GameplayObject wolf = _factory.Create(definition);
                Assert.AreEqual(0, tagsAdded.Count, "Construction is event-quiet: seeding initial tags publishes nothing.");
                Assert.AreEqual(0, granted.Count, "Construction is event-quiet: seeding initial ability grants publishes nothing.");
                Assert.AreEqual(0, spawned.Count);

                wolf.Activate();
                Assert.AreEqual(1, spawned.Count, "Activation is the single fact announcing the fully-composed object.");
                Assert.AreEqual(0, tagsAdded.Count, "Initial tags are spawn state announced by GameplayObjectSpawned, not runtime additions.");
                Assert.AreEqual(0, granted.Count);
                Assert.IsTrue(wolf.Get<GameplayTagContainer>().HasTagExact(_tagTable.GetTag("Enemy.Beast")));
                Assert.IsTrue(wolf.Get<ToyChest.Systems.Abilities.AbilitySet>().Has(dash.Id));
            }
            finally
            {
                abilities.Cleanup();
            }
        }

        [Test]
        public void Destroy_RevokesStatusContributions_EventQuietly()
        {
            var effects = new ToyChest.Tests.Effects.EffectTestFactory();
            try
            {
                _tagTable.RegisterTag("State.Burning");
                GameplayObject wolf = _factory.Create(CreateObjectDefinition("object.wolf", null, null, null));
                wolf.Activate();

                ToyChest.Systems.StatusEffects.StatusEffectDefinition burning = effects.CreateStatus(
                    "status.burning", durationSeconds: 5f, grantedTags: new[] { effects.CreateTag("State.Burning") });
                wolf.Get<ToyChest.Systems.StatusEffects.StatusEffectSet>().Apply(burning);

                var tagsRemoved = new List<GameplayTagRemoved>();
                using System.IDisposable token = _bus.Subscribe<GameplayTagRemoved>(tagsRemoved.Add);

                wolf.Destroy();

                Assert.AreEqual(0, tagsRemoved.Count,
                    "Teardown is event-quiet: GameplayObjectDestroyed announces it, and disposing the status set revokes its granted tag silently.");
            }
            finally
            {
                effects.Cleanup();
            }
        }

        private GameplayObjectDefinition CreateObjectDefinition(
            string id,
            AttributeDefinition[] attributes,
            ResourceDefinition[] resources,
            string[] initialTagPaths,
            bool registerTags = true,
            ToyChest.Systems.Abilities.AbilityDefinition[] initialAbilities = null)
        {
            var definition = ScriptableObject.CreateInstance<GameplayObjectDefinition>();
            _created.Add(definition);

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_definitionId").stringValue = id;
            FillArray(serialized.FindProperty("_attributes"), attributes);
            FillArray(serialized.FindProperty("_resources"), resources);
            FillArray(serialized.FindProperty("_abilities"), initialAbilities);

            var tagDefinitions = new List<TagDefinition>();
            if (initialTagPaths != null)
            {
                foreach (string path in initialTagPaths)
                {
                    var tag = ScriptableObject.CreateInstance<TagDefinition>();
                    _created.Add(tag);
                    var tagSerialized = new SerializedObject(tag);
                    tagSerialized.FindProperty("_tagPath").stringValue = path;
                    tagSerialized.ApplyModifiedPropertiesWithoutUndo();
                    tagDefinitions.Add(tag);

                    if (registerTags)
                    {
                        _tagTable.RegisterTag(path);
                    }
                }
            }

            FillArray(serialized.FindProperty("_initialTags"), tagDefinitions.ToArray());
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
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
