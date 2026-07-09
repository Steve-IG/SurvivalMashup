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
using ToyChest.Systems.Items;
using ToyChest.Systems.Resources;
using ToyChest.Systems.Save;
using ToyChest.Systems.Tags;
using ToyChest.Tests.Events;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ToyChest.Tests.Content
{
    /// <summary>
    /// End-to-end verification that an NPC is an ordinary Gameplay Object: the authored Villager
    /// composes from the same capabilities as the player and world objects (attributes, a bound
    /// health resource, an identity tag, an interaction routed to an ability), the player greets it
    /// through the existing Interaction System, the greet gives the player an item while consuming
    /// the NPC's one-shot gift charge, and the NPC's persistent state round-trips through the Save
    /// System deterministically. No NPC-specific engine path is exercised — this is the same
    /// composition, interaction, effect, and persistence machinery the loot loop uses.
    /// </summary>
    public sealed class VillagerNpcContentTests
    {
        private const float Tolerance = 1e-4f;
        private const string DefinitionsFolder = "Assets/Game/Content/Definitions";

        private const string PlayerId = "object.player";
        private const string VillagerId = "object.villager";
        private const string GreetInteractionId = "interaction.greet";
        private const string FieldRationId = "item.field_ration";
        private const string GiftChargeId = "resource.gift_charge";
        private const string HealthId = "resource.health";
        private const string NpcTagPath = "Actor.Npc";

        private RuntimeServices _services;

        [SetUp]
        public void SetUp()
        {
            _services = Boot();
        }

        [Test]
        public void AuthoredNpcContent_PopulatesTheRegistry()
        {
            Assert.IsTrue(_services.DataRegistry.Contains<GameplayObjectDefinition>(new DefinitionId(VillagerId)),
                "The Villager object definition should load and register.");
            Assert.IsTrue(_services.DataRegistry.Contains<ItemDefinition>(new DefinitionId(FieldRationId)),
                "The gifted item must be registered so the Save System can restore it by id.");
            Assert.IsTrue(_services.DataRegistry.Contains<ResourceDefinition>(new DefinitionId(GiftChargeId)));
        }

        [Test]
        public void Villager_ComposesAsAnOrdinaryGameplayObject()
        {
            GameplayObject villager = Spawn(_services, VillagerId);

            Assert.IsTrue(villager.TryGet(out AttributeSet attributes), "The NPC composes an attribute set.");
            Assert.IsTrue(attributes.TryGetValue(new DefinitionId("attribute.movement_speed"), out _),
                "The NPC reuses the shared Movement Speed attribute — one source of truth for speed.");
            Assert.IsTrue(villager.TryGet(out ResourceSet resources), "The NPC composes a resource set.");
            Assert.IsTrue(resources.HasResource(new DefinitionId(HealthId)), "The NPC carries a persistent health resource.");
            Assert.IsTrue(resources.HasResource(new DefinitionId(GiftChargeId)), "The NPC carries its one-shot gift charge.");
            Assert.IsTrue(villager.TryGet(out GameplayTagContainer tags), "The NPC composes a tag container.");
            Assert.IsTrue(tags.HasTag(_services.TagTable.GetTag(NpcTagPath)), "The NPC carries its identity tag.");
            Assert.IsTrue(villager.TryGet(out InteractionSet interactions), "The NPC advertises an interaction.");
            Assert.IsTrue(interactions.Has(new DefinitionId(GreetInteractionId)));
            Assert.IsTrue(villager.TryGet(out AbilitySet abilities), "The greet interaction's ability is granted at composition.");
            Assert.IsTrue(abilities.Has(new DefinitionId("ability.greet_traveler")));
        }

        [Test]
        public void Greeting_GivesTheItem_AndConsumesTheGiftCharge()
        {
            GameplayObject player = Spawn(_services, PlayerId);
            GameplayObject villager = Spawn(_services, VillagerId);
            var interactions = new InteractionSystem(_services.EventBus, _services.TagTable);

            InventorySet bag = player.Get<InventorySet>();
            ResourceValue charge = villager.Get<ResourceSet>().GetResource(new DefinitionId(GiftChargeId));
            Assert.AreEqual(0, bag.QuantityOf(new DefinitionId(FieldRationId)));
            Assert.AreEqual(1f, charge.Current, Tolerance, "The NPC starts with one gift to give.");

            InteractionResult result = interactions.TryInteract(player, villager, new DefinitionId(GreetInteractionId));

            Assert.AreEqual(InteractionResult.Executed, result);
            Assert.AreEqual(1, bag.QuantityOf(new DefinitionId(FieldRationId)),
                "Greeting runs the NPC ability's Add Item effect against the interactor's inventory.");
            Assert.AreEqual(0f, charge.Current, Tolerance, "The greet consumes the NPC's single gift charge.");
        }

        [Test]
        public void GreetedNpc_CannotGiveASecondGift()
        {
            GameplayObject player = Spawn(_services, PlayerId);
            GameplayObject villager = Spawn(_services, VillagerId);
            var interactions = new InteractionSystem(_services.EventBus, _services.TagTable);

            Assert.AreEqual(InteractionResult.Executed,
                interactions.TryInteract(player, villager, new DefinitionId(GreetInteractionId)));

            InteractionResult second = interactions.TryInteract(player, villager, new DefinitionId(GreetInteractionId));

            Assert.AreEqual(InteractionResult.AbilityRejected, second,
                "With no gift charge left the NPC ability cannot pay its cost, so the greet is rejected.");
            Assert.AreEqual(1, player.Get<InventorySet>().QuantityOf(new DefinitionId(FieldRationId)));
        }

        [Test]
        public void NpcState_SurvivesSaveAndReload_Deterministically()
        {
            GameplayObject player = Spawn(_services, PlayerId);
            GameplayObject villager = Spawn(_services, VillagerId);
            var interactions = new InteractionSystem(_services.EventBus, _services.TagTable);
            interactions.TryInteract(player, villager, new DefinitionId(GreetInteractionId));

            SaveManager save = _services.SaveManager;
            SaveData reloaded = save.FromJson(save.ToJson(save.Capture(_services.Objects)));

            RuntimeServices reload = Boot();
            IReadOnlyList<GameplayObject> restored =
                reload.SaveManager.Restore(reloaded, reload.Factory, reload.DataRegistry);

            GameplayObject restoredPlayer = FindByDefinition(restored, PlayerId);
            GameplayObject restoredVillager = FindByDefinition(restored, VillagerId);

            Assert.AreEqual(1, restoredPlayer.Get<InventorySet>().QuantityOf(new DefinitionId(FieldRationId)),
                "The gifted item persists in the player's inventory across save and reload.");
            Assert.AreEqual(0f, restoredVillager.Get<ResourceSet>().GetResource(new DefinitionId(GiftChargeId)).Current, Tolerance,
                "The NPC's consumed gift charge persists across save and reload.");
            Assert.AreEqual(
                restoredVillager.Get<ResourceSet>().GetResource(new DefinitionId(HealthId)).Maximum,
                restoredVillager.Get<ResourceSet>().GetResource(new DefinitionId(HealthId)).Current, Tolerance,
                "The NPC's attribute-bound health restores to its authored full value.");

            var reloadInteractions = new InteractionSystem(reload.EventBus, reload.TagTable);
            Assert.AreEqual(InteractionResult.AbilityRejected,
                reloadInteractions.TryInteract(restoredPlayer, restoredVillager, new DefinitionId(GreetInteractionId)),
                "The reloaded NPC reproduces its already-greeted state exactly.");
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
