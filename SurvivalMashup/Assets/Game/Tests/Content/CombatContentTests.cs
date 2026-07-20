using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Boot;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Objects;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.Attributes;
using ToyChest.Systems.GameplayEffects;
using ToyChest.Systems.Items;
using ToyChest.Systems.Resources;
using ToyChest.Systems.Tags;
using ToyChest.Tests.Events;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ToyChest.Tests.Content
{
    /// <summary>
    /// End-to-end verification of the Milestone 2 combat foundation against real authored content:
    /// the player's strike ability damages a Grunt through the existing Ability → Gameplay Effect →
    /// Resource path, honours its cooldown, and kills the Grunt in a readable
    /// number of hits (which triggers the Resource-depleted death signal). The Grunt is an ordinary
    /// Gameplay Object composed from the same capabilities as everything else, and it strikes back
    /// through its own authored ability. No combat/damage manager is involved — this is the same
    /// machinery the loot loop and hazards use.
    /// </summary>
    public sealed class CombatContentTests
    {
        private const float Tolerance = 1e-4f;
        private const float PlayerMaxHealth = 50f;
        private const float GruntMaxHealth = 50f;
        private const string DefinitionsFolder = "Assets/Game/Content/Definitions";

        private const string PlayerId = "object.player";
        private const string GruntId = "object.grunt";
        private const string HealthId = "resource.health";
        private const string PlayerStrikeId = "ability.player_strike";
        private const string EnemyStrikeId = "ability.enemy_strike";
        private const string EnemySpeedId = "attribute.enemy_speed";
        private const string EnemyTagPath = "Actor.Enemy";
        private const string FangItemId = "item.monster_fang";

        private RuntimeServices _services;

        [SetUp]
        public void SetUp()
        {
            _services = Boot();
        }

        [Test]
        public void AuthoredCombatContent_PopulatesTheRegistry()
        {
            Assert.IsTrue(_services.DataRegistry.Contains<GameplayObjectDefinition>(new DefinitionId(GruntId)));
            Assert.IsTrue(_services.DataRegistry.Contains<AbilityDefinition>(new DefinitionId(PlayerStrikeId)));
            Assert.IsTrue(_services.DataRegistry.Contains<AbilityDefinition>(new DefinitionId(EnemyStrikeId)));
            Assert.IsTrue(_services.DataRegistry.Contains<ItemDefinition>(new DefinitionId(FangItemId)),
                "The Grunt's loot must be registered so a pickup can grant it.");
        }

        [Test]
        public void Grunt_ComposesAsAnOrdinaryEnemyGameplayObject()
        {
            GameplayObject grunt = Spawn(GruntId);
            Assert.IsTrue(grunt.TryGet(out GameplayTagContainer tags));
            Assert.IsTrue(tags.HasTag(_services.TagTable.GetTag(EnemyTagPath)), "The Grunt carries the Actor.Enemy identity tag.");
            Assert.IsTrue(grunt.Get<ResourceSet>().HasResource(new DefinitionId(HealthId)), "The Grunt has health.");
            Assert.IsTrue(grunt.Get<AttributeSet>().TryGetValue(new DefinitionId(EnemySpeedId), out _),
                "The Grunt has its own authored pursuit speed.");
            Assert.IsTrue(grunt.Get<AbilitySet>().Has(new DefinitionId(EnemyStrikeId)), "The Grunt has its attack ability.");
        }

        [Test]
        public void PlayerStrike_DamagesTheGrunt()
        {
            GameplayObject player = Spawn(PlayerId);
            GameplayObject grunt = Spawn(GruntId);
            ResourceValue gruntHealth = Health(grunt);

            AbilityActivationResult result =
                player.Get<AbilitySet>().TryActivate(new DefinitionId(PlayerStrikeId), EffectTarget.From(grunt));

            Assert.AreEqual(AbilityActivationResult.Activated, result);
            Assert.AreEqual(GruntMaxHealth - 20f, gruntHealth.Current, Tolerance, "The strike deals 20 damage to the Grunt.");
        }

        [Test]
        public void PlayerStrike_HonoursItsCooldown()
        {
            GameplayObject player = Spawn(PlayerId);
            GameplayObject grunt = Spawn(GruntId);
            AbilitySet abilities = player.Get<AbilitySet>();

            Assert.AreEqual(AbilityActivationResult.Activated,
                abilities.TryActivate(new DefinitionId(PlayerStrikeId), EffectTarget.From(grunt)));
            Assert.AreEqual(AbilityActivationResult.OnCooldown,
                abilities.TryActivate(new DefinitionId(PlayerStrikeId), EffectTarget.From(grunt)),
                "A second immediate swing is on cooldown.");
            Assert.AreEqual(GruntMaxHealth - 20f, Health(grunt).Current, Tolerance, "The blocked swing deals no damage.");
        }

        [Test]
        public void ThreeStrikes_KillTheGrunt_AndDeathIsDetected()
        {
            GameplayObject player = Spawn(PlayerId);
            GameplayObject grunt = Spawn(GruntId);
            ResourceValue gruntHealth = Health(grunt);
            AbilitySet abilities = player.Get<AbilitySet>();

            int deaths = 0;
            gruntHealth.Depleted += () => deaths++;

            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(AbilityActivationResult.Activated,
                    abilities.TryActivate(new DefinitionId(PlayerStrikeId), EffectTarget.From(grunt)),
                    $"Strike {i + 1} should land.");
                abilities.Tick(0.5f);        // Clears the 0.4s cooldown for the next swing.
            }

            Assert.IsTrue(gruntHealth.IsDepleted, "Three 20-damage strikes kill the 50-health Grunt.");
            Assert.AreEqual(1, deaths, "Death is detected once, on the transition to zero.");
        }

        [Test]
        public void EnemyStrike_DamagesThePlayer()
        {
            GameplayObject player = Spawn(PlayerId);
            GameplayObject grunt = Spawn(GruntId);
            ResourceValue playerHealth = Health(player);

            AbilityActivationResult result =
                grunt.Get<AbilitySet>().TryActivate(new DefinitionId(EnemyStrikeId), EffectTarget.From(player));

            Assert.AreEqual(AbilityActivationResult.Activated, result);
            Assert.AreEqual(PlayerMaxHealth - 8f, playerHealth.Current, Tolerance, "The Grunt's claw deals 8 damage to the player.");
        }

        private GameplayObject Spawn(string id)
        {
            GameplayObjectDefinition definition =
                _services.DataRegistry.Get<GameplayObjectDefinition>(new DefinitionId(id));
            GameplayObject obj = _services.Factory.Create(definition);
            obj.Activate();
            return obj;
        }

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
