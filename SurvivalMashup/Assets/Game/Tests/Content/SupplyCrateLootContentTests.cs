using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Boot;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Objects;
using ToyChest.Systems.Interactions;
using ToyChest.Systems.Inventory;
using ToyChest.Systems.Items;
using ToyChest.Systems.Resources;
using ToyChest.Systems.Save;
using ToyChest.Tests.Events;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ToyChest.Tests.Content
{
    /// <summary>
    /// End-to-end verification of the Milestone 1 gameplay loop against the real authored content:
    /// a Player and a Supply Crate compose from disk-loaded definitions, the Interaction System
    /// routes the Loot interaction to the crate's ability, the ability's Add Item effect deposits
    /// the authored item into the player's inventory while its cost consumes the crate's single
    /// loot charge, and the whole world round-trips through the Save System deterministically.
    /// This exercises Interaction → Ability → Gameplay Effect → Inventory + Resource + Save/Load
    /// working together, entirely through composition and authored data.
    /// </summary>
    public sealed class SupplyCrateLootContentTests
    {
        private const float Tolerance = 1e-4f;
        private const string DefinitionsFolder = "Assets/Game/Content/Definitions";

        private const string PlayerId = "object.player";
        private const string SupplyCrateId = "object.supply_crate";
        private const string LootInteractionId = "interaction.loot";
        private const string ScrapMetalId = "item.scrap_metal";
        private const string LootChargeId = "resource.loot_charge";

        private RuntimeServices _services;

        [SetUp]
        public void SetUp()
        {
            _services = Boot();
        }

        [Test]
        public void AuthoredLootContent_PopulatesTheRegistry()
        {
            Assert.IsTrue(_services.DataRegistry.Contains<GameplayObjectDefinition>(new DefinitionId(SupplyCrateId)),
                "The Supply Crate object definition should load and register.");
            Assert.IsTrue(_services.DataRegistry.Contains<ItemDefinition>(new DefinitionId(ScrapMetalId)),
                "The looted item must be registered so the Save System can restore it by id.");
            Assert.IsTrue(_services.DataRegistry.Contains<ResourceDefinition>(new DefinitionId(LootChargeId)));
        }

        [Test]
        public void Looting_AddsItemToPlayerInventory_AndDepletesTheCrate()
        {
            GameplayObject player = Spawn(_services, PlayerId);
            GameplayObject crate = Spawn(_services, SupplyCrateId);
            var interactions = new InteractionSystem(_services.EventBus, _services.TagTable);

            InventorySet bag = player.Get<InventorySet>();
            ResourceValue charge = crate.Get<ResourceSet>().GetResource(new DefinitionId(LootChargeId));
            Assert.AreEqual(0, bag.QuantityOf(new DefinitionId(ScrapMetalId)), "The player starts with an empty inventory.");
            Assert.AreEqual(1f, charge.Current, Tolerance, "The crate starts with one loot charge.");

            InteractionResult result = interactions.TryInteract(player, crate, new DefinitionId(LootInteractionId));

            Assert.AreEqual(InteractionResult.Executed, result);
            Assert.AreEqual(1, bag.QuantityOf(new DefinitionId(ScrapMetalId)),
                "Looting runs the crate ability's Add Item effect against the interactor's inventory.");
            Assert.AreEqual(0f, charge.Current, Tolerance,
                "The crate ability's cost consumes the crate's single loot charge.");
        }

        [Test]
        public void DepletedCrate_CannotBeLootedAgain()
        {
            GameplayObject player = Spawn(_services, PlayerId);
            GameplayObject crate = Spawn(_services, SupplyCrateId);
            var interactions = new InteractionSystem(_services.EventBus, _services.TagTable);

            Assert.AreEqual(InteractionResult.Executed,
                interactions.TryInteract(player, crate, new DefinitionId(LootInteractionId)));

            InteractionResult second = interactions.TryInteract(player, crate, new DefinitionId(LootInteractionId));

            Assert.AreEqual(InteractionResult.AbilityRejected, second,
                "A depleted crate cannot afford its loot ability's cost, so the interaction is rejected.");
            Assert.AreEqual(1, player.Get<InventorySet>().QuantityOf(new DefinitionId(ScrapMetalId)),
                "A rejected loot yields nothing further.");
        }

        [Test]
        public void LootedState_SurvivesSaveAndReload_Deterministically()
        {
            GameplayObject player = Spawn(_services, PlayerId);
            GameplayObject crate = Spawn(_services, SupplyCrateId);
            var interactions = new InteractionSystem(_services.EventBus, _services.TagTable);
            interactions.TryInteract(player, crate, new DefinitionId(LootInteractionId));

            // Round-trip the whole world through the serialization contract.
            SaveManager save = _services.SaveManager;
            SaveData reloaded = save.FromJson(save.ToJson(save.Capture(_services.Objects)));

            // Restore into a fresh session (fresh registry and live world).
            RuntimeServices reload = Boot();
            IReadOnlyList<GameplayObject> restored =
                reload.SaveManager.Restore(reloaded, reload.Factory, reload.DataRegistry);

            GameplayObject restoredPlayer = FindByDefinition(restored, PlayerId);
            GameplayObject restoredCrate = FindByDefinition(restored, SupplyCrateId);

            Assert.AreEqual(1, restoredPlayer.Get<InventorySet>().QuantityOf(new DefinitionId(ScrapMetalId)),
                "The looted item persists in the player's inventory across save and reload.");
            Assert.AreEqual(0f, restoredCrate.Get<ResourceSet>().GetResource(new DefinitionId(LootChargeId)).Current, Tolerance,
                "The crate's consumed loot charge persists across save and reload.");

            // Reload is deterministic: the restored crate is still un-lootable.
            var reloadInteractions = new InteractionSystem(reload.EventBus, reload.TagTable);
            Assert.AreEqual(InteractionResult.AbilityRejected,
                reloadInteractions.TryInteract(restoredPlayer, restoredCrate, new DefinitionId(LootInteractionId)),
                "The reloaded crate reproduces its depleted state exactly.");
        }

        private static GameplayObject Spawn(RuntimeServices services, string id)
        {
            GameplayObjectDefinition definition =
                services.DataRegistry.Get<GameplayObjectDefinition>(new DefinitionId(id));
            GameplayObject obj = services.Factory.Create(definition);
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
