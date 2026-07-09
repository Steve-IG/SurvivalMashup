using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Boot;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Objects;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.Interactions;
using ToyChest.Systems.Resources;
using ToyChest.Systems.Save;
using ToyChest.Systems.StatusEffects;
using ToyChest.Systems.Tags;
using ToyChest.Tests.Events;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ToyChest.Tests.Content
{
    /// <summary>
    /// End-to-end verification that health, damage, healing, timed status effects, environmental
    /// hazards, death, and save/load are all validated through the existing engine and authored
    /// content — no combat, damage, health, or hazard manager. Hazards are ordinary Gameplay
    /// Objects; their behaviour is delivered by applying authored status effects (the caller path
    /// a <c>HazardVolume</c> uses at runtime) and by the Healing Shrine's interaction-driven
    /// ability. Every gameplay state change is driven deterministically against real authored
    /// definitions, exactly as the frozen Milestone 0 systems execute them.
    /// </summary>
    public sealed class HazardContentTests
    {
        private const float Tolerance = 1e-4f;
        private const float BaseMaxHealth = 50f;
        private const string DefinitionsFolder = "Assets/Game/Content/Definitions";

        private const string PlayerId = "object.player";
        private const string HealthId = "resource.health";
        private const string SpikeStatusId = "status.spikes";
        private const string PoisonStatusId = "status.poison";
        private const string WarmthStatusId = "status.warmth";
        private const string PrayInteractionId = "interaction.pray";
        private const string ShrineId = "object.healing_shrine";
        private const string PoisonedTagPath = "State.Poisoned";

        private RuntimeServices _services;

        [SetUp]
        public void SetUp()
        {
            _services = Boot();
        }

        [Test]
        public void AuthoredHazardContent_PopulatesTheRegistry()
        {
            Assert.IsTrue(_services.DataRegistry.Contains<GameplayObjectDefinition>(new DefinitionId("object.spike_trap")));
            Assert.IsTrue(_services.DataRegistry.Contains<GameplayObjectDefinition>(new DefinitionId("object.poison_pool")));
            Assert.IsTrue(_services.DataRegistry.Contains<GameplayObjectDefinition>(new DefinitionId("object.campfire")));
            Assert.IsTrue(_services.DataRegistry.Contains<GameplayObjectDefinition>(new DefinitionId(ShrineId)));
            Assert.IsTrue(_services.DataRegistry.Contains<StatusEffectDefinition>(new DefinitionId(SpikeStatusId)));
            Assert.IsTrue(_services.DataRegistry.Contains<StatusEffectDefinition>(new DefinitionId(PoisonStatusId)));
            Assert.IsTrue(_services.DataRegistry.Contains<StatusEffectDefinition>(new DefinitionId(WarmthStatusId)));
            Assert.IsTrue(_services.DataRegistry.Contains<AbilityDefinition>(new DefinitionId("ability.healing_shrine")));
        }

        [Test]
        public void SpikeStatus_DealsDamageOverTime_AndStopsAfterExpiry()
        {
            GameplayObject player = Spawn(PlayerId);
            ResourceValue health = Health(player);
            StatusEffectSet statuses = player.Get<StatusEffectSet>();

            statuses.Apply(Status(SpikeStatusId)); // 1s duration, 8 damage every 0.5s.

            statuses.Tick(0.5f);
            Assert.AreEqual(BaseMaxHealth - 8f, health.Current, Tolerance, "One spike tick reduces health.");

            statuses.Tick(0.5f);
            Assert.AreEqual(BaseMaxHealth - 16f, health.Current, Tolerance, "The second (final) tick lands as the status expires.");
            Assert.IsFalse(statuses.Has(new DefinitionId(SpikeStatusId)), "The timed status expires after its duration.");

            statuses.Tick(1f);
            Assert.AreEqual(BaseMaxHealth - 16f, health.Current, Tolerance, "Damage-over-time stops once the status has expired.");
        }

        [Test]
        public void WarmthStatus_HealsOverTime_ClampsAtMaximum_AndExpires()
        {
            GameplayObject player = Spawn(PlayerId);
            ResourceValue health = Health(player);
            StatusEffectSet statuses = player.Get<StatusEffectSet>();

            health.Consume(25f); // 50 -> 25, so the 30 total heal would overshoot the maximum.
            statuses.Apply(Status(WarmthStatusId)); // 5s duration, 6 healing every 1s.

            statuses.Tick(1f);
            Assert.AreEqual(31f, health.Current, Tolerance, "One warmth tick restores health.");

            statuses.Tick(4f);
            Assert.AreEqual(BaseMaxHealth, health.Current, Tolerance, "Healing-over-time clamps at Maximum Health, never above.");
            Assert.IsFalse(statuses.Has(new DefinitionId(WarmthStatusId)), "The timed healing status expires after its duration.");
        }

        [Test]
        public void PoisonStatus_GrantsTagOnApply_AndRevokesOnExpiry()
        {
            GameplayObject player = Spawn(PlayerId);
            ResourceValue health = Health(player);
            StatusEffectSet statuses = player.Get<StatusEffectSet>();
            GameplayTagContainer tags = player.Get<GameplayTagContainer>();
            GameplayTag poisoned = _services.TagTable.GetTag(PoisonedTagPath);

            statuses.Apply(Status(PoisonStatusId)); // 6s duration, 5 damage every 1s, grants State.Poisoned.
            Assert.IsTrue(tags.HasTag(poisoned), "Applying the status grants its tag immediately.");

            statuses.Tick(3f);
            Assert.AreEqual(BaseMaxHealth - 15f, health.Current, Tolerance, "Poison deals damage each second while active.");
            Assert.IsTrue(tags.HasTag(poisoned), "The tag persists while the status is active.");

            statuses.Tick(3f);
            Assert.IsFalse(statuses.Has(new DefinitionId(PoisonStatusId)), "The status expires at its duration.");
            Assert.IsFalse(tags.HasTag(poisoned), "Expiry revokes the granted tag.");
            Assert.AreEqual(BaseMaxHealth - 30f, health.Current, Tolerance, "Poison stops after expiry.");
        }

        [Test]
        public void HealingShrine_Interaction_HealsInteractor_AndRespectsMaximumHealth()
        {
            GameplayObject player = Spawn(PlayerId);
            GameplayObject shrine = Spawn(ShrineId);
            ResourceValue health = Health(player);
            var interactions = new InteractionSystem(_services.EventBus, _services.TagTable);

            health.Consume(30f); // 50 -> 20.

            InteractionResult first = interactions.TryInteract(player, shrine, new DefinitionId(PrayInteractionId));
            Assert.AreEqual(InteractionResult.Executed, first, "Praying at the shrine runs its heal ability against the interactor.");
            Assert.AreEqual(BaseMaxHealth, health.Current, Tolerance, "The 40-point heal clamps at Maximum Health (would be 60).");

            InteractionResult second = interactions.TryInteract(player, shrine, new DefinitionId(PrayInteractionId));
            Assert.AreEqual(InteractionResult.Executed, second, "The shrine has no cost or cooldown, so it can be used again.");
            Assert.AreEqual(BaseMaxHealth, health.Current, Tolerance, "Healing at full health leaves Maximum Health unchanged.");
        }

        [Test]
        public void Death_IsDetected_WhenHazardDepletesHealth()
        {
            GameplayObject player = Spawn(PlayerId);
            ResourceValue health = Health(player);
            StatusEffectSet statuses = player.Get<StatusEffectSet>();

            int depletedCount = 0;
            health.Depleted += () => depletedCount++;

            health.Consume(45f); // Prior damage: 50 -> 5.
            statuses.Apply(Status(SpikeStatusId)); // 8 damage per tick.

            statuses.Tick(0.5f); // The hazard tick lands the lethal blow: 5 - 8 -> 0.

            Assert.IsTrue(health.IsDepleted, "Health reaches zero.");
            Assert.AreEqual(1, depletedCount, "Death is detected exactly once, on the transition to zero.");
        }

        [Test]
        public void RuntimeStateChanges_SurviveSaveAndReload_Deterministically()
        {
            GameplayObject player = Spawn(PlayerId);
            StatusEffectSet statuses = player.Get<StatusEffectSet>();
            statuses.Apply(Status(PoisonStatusId));
            statuses.Tick(2.5f); // 2 poison ticks (10 damage); remaining 3.5s, accumulator 0.5s.

            Assert.AreEqual(BaseMaxHealth - 10f, Health(player).Current, Tolerance);

            SaveManager save = _services.SaveManager;
            SaveData reloaded = save.FromJson(save.ToJson(save.Capture(_services.Objects)));

            RuntimeServices reload = Boot();
            IReadOnlyList<GameplayObject> restored =
                reload.SaveManager.Restore(reloaded, reload.Factory, reload.DataRegistry);
            GameplayObject restoredPlayer = FindByDefinition(restored, PlayerId);

            Assert.AreEqual(BaseMaxHealth - 10f, Health(restoredPlayer).Current, Tolerance,
                "Health reduced by the hazard restores exactly.");
            StatusEffectInstance poison = FindStatus(restoredPlayer, PoisonStatusId);
            Assert.IsNotNull(poison, "The active poison status restores.");
            Assert.AreEqual(3.5f, poison.RemainingSeconds, Tolerance, "Remaining duration restores exactly.");
            Assert.AreEqual(0.5f, poison.PeriodAccumulator, Tolerance, "The periodic accumulator restores exactly.");
            Assert.IsTrue(restoredPlayer.Get<GameplayTagContainer>().HasTag(reload.TagTable.GetTag(PoisonedTagPath)),
                "The status re-grants its derived tag on restore.");

            // Continue the reloaded status to expiry: 4 more poison ticks (0.5 + 3.5), then it ends.
            restoredPlayer.Get<StatusEffectSet>().Tick(3.5f);
            Assert.AreEqual(BaseMaxHealth - 30f, Health(restoredPlayer).Current, Tolerance,
                "The restored status keeps ticking deterministically to the same outcome as an unbroken run.");
            Assert.IsFalse(restoredPlayer.Get<StatusEffectSet>().Has(new DefinitionId(PoisonStatusId)),
                "The restored status expires on schedule.");
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

        private static StatusEffectInstance FindStatus(GameplayObject obj, string statusId)
        {
            IReadOnlyList<StatusEffectInstance> active = obj.Get<StatusEffectSet>().ActiveStatuses;
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i].Definition.Id.Value == statusId)
                {
                    return active[i];
                }
            }

            return null;
        }

        private static GameplayObject FindByDefinition(IReadOnlyList<GameplayObject> objects, string definitionId)
        {
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i].DefinitionId.Value == definitionId)
                {
                    return objects[i];
                }
            }

            Assert.Fail($"No restored object with definition '{definitionId}'.");
            return null;
        }

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
