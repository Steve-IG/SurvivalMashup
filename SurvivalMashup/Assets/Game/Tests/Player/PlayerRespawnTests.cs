using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Boot;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Objects;
using ToyChest.Gameplay.Player;
using ToyChest.Systems.Resources;
using ToyChest.Systems.StatusEffects;
using ToyChest.Systems.Tags;
using ToyChest.Tests.Events;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ToyChest.Tests.Player
{
    /// <summary>
    /// Verifies the pure respawn core: <see cref="PlayerRespawn.Restore"/> returns a downed player
    /// to a clean spawn state using only existing capability operations — health refilled through
    /// the Resource System, active statuses cleared through the Status Effect System — with no
    /// respawn or checkpoint manager. Driven against the real authored player so the reset is
    /// exercised over genuine composed capabilities.
    /// </summary>
    public sealed class PlayerRespawnTests
    {
        private const float Tolerance = 1e-4f;
        private const float BaseMaxHealth = 50f;
        private const string DefinitionsFolder = "Assets/Game/Content/Definitions";
        private const string PlayerId = "object.player";
        private const string HealthId = "resource.health";

        private RuntimeServices _services;

        [SetUp]
        public void SetUp()
        {
            _services = Boot();
        }

        [Test]
        public void Restore_RefillsHealthToMaximum_AndClearsActiveStatuses()
        {
            GameplayObject player = Spawn(PlayerId);
            ResourceValue health = Health(player);
            StatusEffectSet statuses = player.Get<StatusEffectSet>();
            GameplayTag poisoned = _services.TagTable.GetTag("State.Poisoned");

            health.Consume(35f); // 50 -> 15.
            statuses.Apply(Status("status.poison"));
            statuses.Apply(Status("status.spikes"));
            Assert.AreEqual(2, statuses.Count);
            Assert.IsTrue(player.Get<GameplayTagContainer>().HasTag(poisoned));

            PlayerRespawn.Restore(player);

            Assert.AreEqual(BaseMaxHealth, health.Current, Tolerance, "Respawn refills health to Maximum Health.");
            Assert.AreEqual(0, statuses.Count, "Respawn clears every active status through the Status Effect System.");
            Assert.IsFalse(player.Get<GameplayTagContainer>().HasTag(poisoned),
                "Clearing the status also revokes its granted tag.");
        }

        [Test]
        public void Restore_AfterHazardKillsPlayer_ReturnsToFullCleanState()
        {
            GameplayObject player = Spawn(PlayerId);
            ResourceValue health = Health(player);
            StatusEffectSet statuses = player.Get<StatusEffectSet>();

            bool died = false;
            health.Depleted += () => died = true;

            health.Consume(45f); // 50 -> 5.
            statuses.Apply(Status("status.spikes"));
            statuses.Tick(0.5f); // Lethal hazard tick: 5 - 8 -> 0.
            Assert.IsTrue(died, "The hazard kills the player.");
            Assert.IsTrue(health.IsDepleted);

            PlayerRespawn.Restore(player);

            Assert.AreEqual(BaseMaxHealth, health.Current, Tolerance, "The respawned player is at full health.");
            Assert.AreEqual(0, statuses.Count, "The respawned player carries no leftover hazard statuses.");
            Assert.IsFalse(health.IsDepleted, "The respawned player is alive again.");
        }

        private GameplayObject Spawn(string id)
        {
            GameplayObjectDefinition definition =
                _services.DataRegistry.Get<GameplayObjectDefinition>(new DefinitionId(id));
            GameplayObject obj = _services.Factory.Create(definition);
            obj.Activate();
            return obj;
        }

        private StatusEffectDefinition Status(string id) =>
            _services.DataRegistry.Get<StatusEffectDefinition>(new DefinitionId(id));

        private static ResourceValue Health(GameplayObject obj) =>
            obj.Get<ResourceSet>().GetResource(new DefinitionId(HealthId));

        private static RuntimeServices Boot()
        {
            return new RuntimeBootstrap().Run(
                new BootstrapConfiguration(new RecordingLogger(), new[] { LoadAuthoredDefinitions() }));
        }

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

            Assert.Greater(definitions.Count, 0, $"No authored definitions found under {DefinitionsFolder}.");
            return new DirectDefinitionSource(definitions);
        }
    }
}
