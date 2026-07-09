using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Boot;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Objects;
using ToyChest.Systems.Attributes;
using ToyChest.Systems.Resources;
using ToyChest.Systems.Tags;
using ToyChest.Tests.Events;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ToyChest.Tests.Content
{
    /// <summary>
    /// End-to-end verification of the authored Player content through the real runtime pipeline:
    /// the authored ScriptableObject definitions load from disk, register through
    /// <see cref="RuntimeBootstrap"/>, and compose into a live Player Gameplay Object with the
    /// expected capabilities. The Player is composed entirely from existing capabilities (attributes,
    /// a bound resource, a tag) — no player-specific engine code — proving the framework supports a
    /// controllable character through authored data alone.
    /// </summary>
    public sealed class PlayerContentTests
    {
        private const float Tolerance = 1e-4f;
        private const string DefinitionsFolder = "Assets/Game/Content/Definitions";

        private const string PlayerId = "object.player";
        private const string MaxHealthId = "attribute.max_health";
        private const string MovementSpeedId = "attribute.movement_speed";
        private const string HealthId = "resource.health";
        private const string PlayerTagPath = "Actor.Player";

        private RuntimeServices _services;

        [SetUp]
        public void SetUp()
        {
            _services = new RuntimeBootstrap().Run(
                new BootstrapConfiguration(new RecordingLogger(), new[] { LoadAuthoredDefinitions() }));
        }

        [Test]
        public void AuthoredPlayerDefinitions_PopulateTheRegistry()
        {
            Assert.IsTrue(_services.DataRegistry.Contains<GameplayObjectDefinition>(new DefinitionId(PlayerId)),
                "The Player object definition should load and register.");
            Assert.IsTrue(_services.DataRegistry.Contains<AttributeDefinition>(new DefinitionId(MovementSpeedId)));
            Assert.IsTrue(_services.TagTable.TryGetTag(PlayerTagPath, out _));
        }

        [Test]
        public void Player_ComposesMovementAndHealthCapabilities_FromAuthoredData()
        {
            GameplayObject player = SpawnPlayer();

            Assert.IsTrue(player.IsActive);
            Assert.IsTrue(_services.Objects.Contains(player.Id));

            AttributeSet attributes = player.Get<AttributeSet>();
            Assert.AreEqual(5f, attributes.GetValue(new DefinitionId(MovementSpeedId)), Tolerance,
                "Movement Speed is an authored attribute the locomotion adapter reads.");
            Assert.AreEqual(50f, attributes.GetValue(new DefinitionId(MaxHealthId)), Tolerance);

            ResourceValue health = player.Get<ResourceSet>().GetResource(new DefinitionId(HealthId));
            Assert.AreEqual(50f, health.Maximum, Tolerance, "Current Health binds its maximum to Maximum Health.");
            Assert.AreEqual(50f, health.Current, Tolerance);

            Assert.IsTrue(player.Get<GameplayTagContainer>().HasTag(_services.TagTable.GetTag(PlayerTagPath)),
                "The player carries its Actor.Player identity tag from spawn.");
        }

        private GameplayObject SpawnPlayer()
        {
            GameplayObjectDefinition definition =
                _services.DataRegistry.Get<GameplayObjectDefinition>(new DefinitionId(PlayerId));
            GameplayObject player = _services.Factory.Create(definition);
            player.Activate();
            return player;
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
                $"No authored definitions found under {DefinitionsFolder}.");
            return new DirectDefinitionSource(definitions);
        }
    }
}
