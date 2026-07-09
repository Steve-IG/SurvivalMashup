using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Boot;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Objects;
using ToyChest.Gameplay.Player;
using ToyChest.Systems.Equipment;
using ToyChest.Systems.Interactions;
using ToyChest.Systems.Inventory;
using ToyChest.Systems.Items;
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
    /// The Milestone 1 certification test: it walks the whole vertical-slice loop against the real
    /// authored content through the real boot, interaction, equipment, status, and save systems —
    /// meet the Villager, loot the world, equip the cache, complete an equipment-gated objective,
    /// survive a hazard, recover, die, respawn — and proves it all round-trips through Save/Load.
    /// Every major system from Review Groups 3–7 is exercised in one cohesive flow, with no new
    /// framework: the objective is just an interaction whose ability is gated on the Lucky Charm's
    /// tag and whose one-shot cost is a Resource, so completion is authoritative and persists.
    /// </summary>
    public sealed class VerticalSliceIntegrationTests
    {
        private const float Tolerance = 1e-4f;
        private const string DefinitionsFolder = "Assets/Game/Content/Definitions";

        private const string PlayerId = "object.player";
        private const string VillagerId = "object.villager";
        private const string SupplyCrateId = "object.supply_crate";
        private const string CacheId = "object.equipment_cache";
        private const string ShrineId = "object.healing_shrine";
        private const string MachineId = "object.ancient_machine";

        private const string GreetId = "interaction.greet";
        private const string LootId = "interaction.loot";
        private const string LootEquipId = "interaction.loot_equipment";
        private const string PrayId = "interaction.pray";
        private const string ActivateMachineId = "interaction.activate_machine";

        private const string FieldRationId = "item.field_ration";
        private const string ScrapId = "item.scrap_metal";
        private const string BootsItemId = "item.boots_of_swiftness";
        private const string CharmItemId = "item.lucky_charm";
        private const string RelicId = "item.relic";
        private const string BootsSlotId = "slot.boots";
        private const string CharmSlotId = "slot.charm";

        private const string HealthId = "resource.health";
        private const string MaxHealthId = "attribute.max_health";
        private const string LuckyTag = "Equipment.Lucky";
        private const string PoisonStatusId = "status.poison";

        private RuntimeServices _services;
        private InteractionSystem _interactions;

        [SetUp]
        public void SetUp()
        {
            _services = Boot();
            _interactions = new InteractionSystem(_services.EventBus, _services.TagTable);
        }

        [Test]
        public void EveryVerticalSliceObject_IsAuthoredAndRegistered()
        {
            foreach (string id in new[] { PlayerId, VillagerId, SupplyCrateId, CacheId, ShrineId, MachineId })
            {
                Assert.IsTrue(_services.DataRegistry.Contains<GameplayObjectDefinition>(new DefinitionId(id)),
                    $"The vertical slice object '{id}' must be authored and registered.");
            }

            Assert.IsTrue(_services.DataRegistry.Contains<ItemDefinition>(new DefinitionId(RelicId)),
                "The objective reward item must be registered so it can be restored by id.");
        }

        [Test]
        public void FullProgressionLoop_CompletesTheGatedObjective_AndPersists()
        {
            GameplayObject player = Spawn(PlayerId);
            GameplayObject villager = Spawn(VillagerId);
            GameplayObject supplyCrate = Spawn(SupplyCrateId);
            GameplayObject cache = Spawn(CacheId);
            GameplayObject machine = Spawn(MachineId);
            InventorySet bag = player.Get<InventorySet>();

            // 1. Meet the Villager, receive the Field Ration.
            Assert.AreEqual(InteractionResult.Executed, _interactions.TryInteract(player, villager, new DefinitionId(GreetId)));
            Assert.AreEqual(1, bag.QuantityOf(new DefinitionId(FieldRationId)));

            // 2. Loot the world.
            Assert.AreEqual(InteractionResult.Executed, _interactions.TryInteract(player, supplyCrate, new DefinitionId(LootId)));
            Assert.AreEqual(1, bag.QuantityOf(new DefinitionId(ScrapId)));
            Assert.AreEqual(InteractionResult.Executed, _interactions.TryInteract(player, cache, new DefinitionId(LootEquipId)));
            Assert.AreEqual(1, bag.QuantityOf(new DefinitionId(BootsItemId)));
            Assert.AreEqual(1, bag.QuantityOf(new DefinitionId(CharmItemId)));

            // 3. The objective is gated: without the Lucky Charm equipped, the machine refuses.
            Assert.AreEqual(InteractionResult.MissingInteractorTag,
                _interactions.TryInteract(player, machine, new DefinitionId(ActivateMachineId)),
                "The Ancient Machine cannot be activated before the Lucky Charm grants Equipment.Lucky.");
            Assert.AreEqual(0, bag.QuantityOf(new DefinitionId(RelicId)));

            // 4. Equip the cache; the Charm grants Equipment.Lucky.
            EquipAll(player);
            Assert.IsTrue(player.Get<GameplayTagContainer>().HasTag(_services.TagTable.GetTag(LuckyTag)));

            // 5. Now the objective completes and rewards the Relic.
            Assert.AreEqual(InteractionResult.Executed,
                _interactions.TryInteract(player, machine, new DefinitionId(ActivateMachineId)));
            Assert.AreEqual(1, bag.QuantityOf(new DefinitionId(RelicId)), "Completing the objective yields the Valley Relic.");

            // 6. The machine's one-shot charge is spent — it cannot be completed twice.
            Assert.AreEqual(InteractionResult.AbilityRejected,
                _interactions.TryInteract(player, machine, new DefinitionId(ActivateMachineId)));
            Assert.AreEqual(1, bag.QuantityOf(new DefinitionId(RelicId)));

            // 7. The whole run round-trips through Save/Load deterministically.
            SaveManager save = _services.SaveManager;
            SaveData reloaded = save.FromJson(save.ToJson(save.Capture(_services.Objects)));
            RuntimeServices reload = Boot();
            IReadOnlyList<GameplayObject> restored =
                reload.SaveManager.Restore(reloaded, reload.Factory, reload.DataRegistry);

            GameplayObject restoredPlayer = FindByDefinition(restored, PlayerId);
            GameplayObject restoredMachine = FindByDefinition(restored, MachineId);
            InventorySet restoredBag = restoredPlayer.Get<InventorySet>();

            Assert.AreEqual(1, restoredBag.QuantityOf(new DefinitionId(FieldRationId)), "Field Ration persists.");
            Assert.AreEqual(1, restoredBag.QuantityOf(new DefinitionId(ScrapId)), "Scrap Metal persists.");
            Assert.AreEqual(1, restoredBag.QuantityOf(new DefinitionId(RelicId)), "The objective reward persists.");
            Assert.IsTrue(restoredPlayer.Get<GameplayTagContainer>().HasTag(reload.TagTable.GetTag(LuckyTag)),
                "The equipped loadout (and its Equipment.Lucky tag) is re-established on reload.");

            var reloadInteractions = new InteractionSystem(reload.EventBus, reload.TagTable);
            Assert.AreEqual(InteractionResult.AbilityRejected,
                reloadInteractions.TryInteract(restoredPlayer, restoredMachine, new DefinitionId(ActivateMachineId)),
                "The reloaded objective reproduces its already-completed state — the machine stays spent.");
        }

        [Test]
        public void HazardDamage_ShrineRecovery_Death_AndRespawn_WorkTogether()
        {
            GameplayObject player = Spawn(PlayerId);
            GameplayObject shrine = Spawn(ShrineId);
            EquipAll(LootedFrom(player)); // Boosts Maximum Health to 75 via the Lucky Charm.

            ResourceValue health = player.Get<ResourceSet>().GetResource(new DefinitionId(HealthId));
            StatusEffectSet statuses = player.Get<StatusEffectSet>();
            Assert.AreEqual(75f, health.Maximum, Tolerance, "Equipped Maximum Health is the authored 50 plus the Charm's 25.");

            // Hazard: standing in poison (as the Poison Pool's HazardVolume applies it) deals damage over
            // time. The Charm's passive Lucky Regen (+1/s) partly offsets it — two statuses composing — so
            // three seconds nets 50 - 15 (poison) + 3 (regen) = 38.
            statuses.Apply(_services.DataRegistry.Get<StatusEffectDefinition>(new DefinitionId(PoisonStatusId)));
            statuses.Tick(3f);
            Assert.AreEqual(38f, health.Current, Tolerance, "Poison and the Charm's passive regen compose to a net -12.");

            // Recovery: the Healing Shrine restores, clamped at the equipment-boosted maximum (38 + 40 -> 75).
            Assert.AreEqual(InteractionResult.Executed, _interactions.TryInteract(player, shrine, new DefinitionId(PrayId)));
            Assert.AreEqual(75f, health.Current, Tolerance, "The shrine heals to the boosted Maximum Health, never above it.");

            // Death: a lethal hazard tick depletes health and is detected once.
            int deaths = 0;
            health.Depleted += () => deaths++;
            health.Consume(74f); // 75 -> 1.
            statuses.Apply(_services.DataRegistry.Get<StatusEffectDefinition>(new DefinitionId(PoisonStatusId)));
            statuses.Tick(1f); // 1 - 5 -> 0.
            Assert.AreEqual(1, deaths, "Death is detected exactly once, on the transition to zero.");

            // Respawn: the existing systems restore the player to a clean state.
            PlayerRespawn.Restore(player);
            Assert.AreEqual(75f, health.Current, Tolerance, "Respawn refills to the (still equipment-boosted) Maximum Health.");
            Assert.AreEqual(0, statuses.Count, "Respawn clears the hazard statuses.");
        }

        // ------------------------------------------------------------------ helpers

        private GameplayObject LootedFrom(GameplayObject player)
        {
            GameplayObject cache = Spawn(CacheId);
            _interactions.TryInteract(player, cache, new DefinitionId(LootEquipId));
            return player;
        }

        private void EquipAll(GameplayObject player)
        {
            if (player.Get<InventorySet>().QuantityOf(new DefinitionId(CharmItemId)) == 0)
            {
                LootedFrom(player);
            }

            EquipmentSlotDefinition[] managed =
            {
                _services.DataRegistry.Get<EquipmentSlotDefinition>(new DefinitionId(BootsSlotId)),
                _services.DataRegistry.Get<EquipmentSlotDefinition>(new DefinitionId(CharmSlotId)),
            };

            while (InventoryEquip.TryEquipFromInventory(player.Get<InventorySet>(), player.Get<EquipmentSet>(), managed, out _, out _))
            {
            }
        }

        private GameplayObject Spawn(string id)
        {
            GameplayObjectDefinition definition =
                _services.DataRegistry.Get<GameplayObjectDefinition>(new DefinitionId(id));
            GameplayObject obj = _services.Factory.Create(definition);
            obj.Activate();
            return obj;
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
