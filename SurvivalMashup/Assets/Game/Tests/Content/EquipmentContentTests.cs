using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Boot;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Objects;
using ToyChest.Gameplay.Player;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.Attributes;
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
    /// End-to-end verification of the Review Group 6 equipment content against the real authored
    /// definitions. It proves equipment modifies gameplay entirely through the existing Gameplay
    /// Object architecture: an Equipment Cache grants two equippable items through the loot path,
    /// the player's equip caller moves them from the bag into authored slots, and their
    /// contributions activate through the systems that own them — an attribute modifier
    /// (Movement Speed), an attribute-driven resource maximum (Maximum Health), granted gameplay
    /// tags, a granted ability, and a passive periodic status. Unequip revokes every contribution,
    /// and the equipped loadout with all its grants round-trips through the Save System.
    /// </summary>
    public sealed class EquipmentContentTests
    {
        private const float Tolerance = 1e-4f;
        private const string DefinitionsFolder = "Assets/Game/Content/Definitions";

        private const string PlayerId = "object.player";
        private const string CacheId = "object.equipment_cache";
        private const string LootEquipInteractionId = "interaction.loot_equipment";
        private const string BootsItemId = "item.boots_of_swiftness";
        private const string CharmItemId = "item.lucky_charm";
        private const string BootsSlotId = "slot.boots";
        private const string CharmSlotId = "slot.charm";
        private const string MoveSpeedId = "attribute.movement_speed";
        private const string MaxHealthId = "attribute.max_health";
        private const string HealthId = "resource.health";
        private const string SwiftTag = "Equipment.Swift";
        private const string LuckyTag = "Equipment.Lucky";
        private const string SecondWindAbilityId = "ability.second_wind";
        private const string LuckyRegenStatusId = "status.lucky_regen";

        private RuntimeServices _services;

        [SetUp]
        public void SetUp()
        {
            _services = Boot();
        }

        [Test]
        public void AuthoredEquipmentContent_PopulatesTheRegistry()
        {
            Assert.IsTrue(_services.DataRegistry.Contains<ItemDefinition>(new DefinitionId(BootsItemId)));
            Assert.IsTrue(_services.DataRegistry.Contains<ItemDefinition>(new DefinitionId(CharmItemId)));
            Assert.IsTrue(_services.DataRegistry.Contains<EquipmentSlotDefinition>(new DefinitionId(BootsSlotId)));
            Assert.IsTrue(_services.DataRegistry.Contains<EquipmentSlotDefinition>(new DefinitionId(CharmSlotId)));
            Assert.IsTrue(_services.DataRegistry.Contains<GameplayObjectDefinition>(new DefinitionId(CacheId)));
            Assert.IsTrue(_services.DataRegistry.Contains<AbilityDefinition>(new DefinitionId(SecondWindAbilityId)));
            Assert.IsTrue(_services.DataRegistry.Contains<StatusEffectDefinition>(new DefinitionId(LuckyRegenStatusId)));
        }

        [Test]
        public void Player_ComposesAnEquipmentSet_FromAuthoredSlots()
        {
            GameplayObject player = Spawn(_services, PlayerId);
            Assert.IsTrue(player.TryGet(out EquipmentSet equipment), "The player composes an EquipmentSet from its authored slots.");
            Assert.IsTrue(equipment.HasSlot(new DefinitionId(BootsSlotId)));
            Assert.IsTrue(equipment.HasSlot(new DefinitionId(CharmSlotId)));
        }

        [Test]
        public void LootingTheCache_AddsBothEquippablesToTheInventory()
        {
            GameplayObject player = Spawn(_services, PlayerId);
            GameplayObject cache = Spawn(_services, CacheId);
            var interactions = new InteractionSystem(_services.EventBus, _services.TagTable);

            InteractionResult result = interactions.TryInteract(player, cache, new DefinitionId(LootEquipInteractionId));

            Assert.AreEqual(InteractionResult.Executed, result);
            InventorySet bag = player.Get<InventorySet>();
            Assert.AreEqual(1, bag.QuantityOf(new DefinitionId(BootsItemId)));
            Assert.AreEqual(1, bag.QuantityOf(new DefinitionId(CharmItemId)));
        }

        [Test]
        public void Equipping_ActivatesEveryAuthoredContribution()
        {
            GameplayObject player = LootedPlayer();
            EquipAllManaged(player);

            AttributeSet attributes = player.Get<AttributeSet>();
            Assert.AreEqual(8f, attributes.GetValue(new DefinitionId(MoveSpeedId)), Tolerance,
                "Boots of Swiftness add +3 to the authored base Movement Speed of 5.");
            Assert.AreEqual(75f, attributes.GetValue(new DefinitionId(MaxHealthId)), Tolerance,
                "The Lucky Charm adds +25 to the authored base Maximum Health of 50.");
            Assert.AreEqual(75f, player.Get<ResourceSet>().GetResource(new DefinitionId(HealthId)).Maximum, Tolerance,
                "Current Health's maximum tracks the modified Maximum Health attribute.");

            GameplayTagContainer tags = player.Get<GameplayTagContainer>();
            Assert.IsTrue(tags.HasTag(_services.TagTable.GetTag(SwiftTag)), "Boots grant Equipment.Swift.");
            Assert.IsTrue(tags.HasTag(_services.TagTable.GetTag(LuckyTag)), "The Charm grants Equipment.Lucky.");

            Assert.IsTrue(player.Get<AbilitySet>().Has(new DefinitionId(SecondWindAbilityId)),
                "The Charm grants the Second Wind ability through the Ability System.");
            Assert.IsTrue(player.Get<StatusEffectSet>().Has(new DefinitionId(LuckyRegenStatusId)),
                "The Charm applies its passive regeneration status through the Status Effect System.");
        }

        [Test]
        public void Unequipping_RevokesEveryContribution_AndReturnsItems()
        {
            GameplayObject player = LootedPlayer();
            EquipAllManaged(player);
            EquipmentSet equipment = player.Get<EquipmentSet>();
            InventorySet bag = player.Get<InventorySet>();

            Assert.IsTrue(InventoryEquip.TryUnequipToInventory(bag, equipment, new DefinitionId(BootsSlotId), out _));
            Assert.IsTrue(InventoryEquip.TryUnequipToInventory(bag, equipment, new DefinitionId(CharmSlotId), out _));

            AttributeSet attributes = player.Get<AttributeSet>();
            Assert.AreEqual(5f, attributes.GetValue(new DefinitionId(MoveSpeedId)), Tolerance, "Movement Speed returns to base.");
            Assert.AreEqual(50f, attributes.GetValue(new DefinitionId(MaxHealthId)), Tolerance, "Maximum Health returns to base.");
            Assert.IsFalse(player.Get<GameplayTagContainer>().HasTag(_services.TagTable.GetTag(SwiftTag)));
            Assert.IsFalse(player.Get<GameplayTagContainer>().HasTag(_services.TagTable.GetTag(LuckyTag)));
            Assert.IsFalse(player.Get<AbilitySet>().Has(new DefinitionId(SecondWindAbilityId)));
            Assert.IsFalse(player.Get<StatusEffectSet>().Has(new DefinitionId(LuckyRegenStatusId)));
            Assert.AreEqual(1, bag.QuantityOf(new DefinitionId(BootsItemId)), "The boots return to the bag.");
            Assert.AreEqual(1, bag.QuantityOf(new DefinitionId(CharmItemId)), "The charm returns to the bag.");
        }

        [Test]
        public void EquippedLoadout_SurvivesSaveAndReload_WithAllContributionsRestored()
        {
            GameplayObject player = LootedPlayer();
            EquipAllManaged(player);

            SaveManager save = _services.SaveManager;
            SaveData reloaded = save.FromJson(save.ToJson(save.Capture(_services.Objects)));

            RuntimeServices reload = Boot();
            IReadOnlyList<GameplayObject> restored =
                reload.SaveManager.Restore(reloaded, reload.Factory, reload.DataRegistry);
            GameplayObject restoredPlayer = FindByDefinition(restored, PlayerId);

            EquipmentSet equipment = restoredPlayer.Get<EquipmentSet>();
            Assert.IsNotNull(equipment.GetEquipped(new DefinitionId(BootsSlotId)), "Boots stay equipped across save/reload.");
            Assert.IsNotNull(equipment.GetEquipped(new DefinitionId(CharmSlotId)), "The charm stays equipped across save/reload.");

            AttributeSet attributes = restoredPlayer.Get<AttributeSet>();
            Assert.AreEqual(8f, attributes.GetValue(new DefinitionId(MoveSpeedId)), Tolerance,
                "The equipment-granted Movement Speed modifier is re-applied on reload.");
            Assert.AreEqual(75f, attributes.GetValue(new DefinitionId(MaxHealthId)), Tolerance,
                "The equipment-granted Maximum Health modifier is re-applied on reload.");
            Assert.AreEqual(75f, restoredPlayer.Get<ResourceSet>().GetResource(new DefinitionId(HealthId)).Maximum, Tolerance);

            Assert.IsTrue(restoredPlayer.Get<GameplayTagContainer>().HasTag(reload.TagTable.GetTag(SwiftTag)),
                "Equipment-derived tags are re-established by re-equipping on reload.");
            Assert.IsTrue(restoredPlayer.Get<GameplayTagContainer>().HasTag(reload.TagTable.GetTag(LuckyTag)));
            Assert.IsTrue(restoredPlayer.Get<AbilitySet>().Has(new DefinitionId(SecondWindAbilityId)),
                "The equipment-granted ability is re-granted on reload.");
            Assert.IsTrue(restoredPlayer.Get<StatusEffectSet>().Has(new DefinitionId(LuckyRegenStatusId)),
                "The equipment-applied passive status is re-applied on reload.");
        }

        // ------------------------------------------------------------------ helpers

        private GameplayObject LootedPlayer()
        {
            GameplayObject player = Spawn(_services, PlayerId);
            GameplayObject cache = Spawn(_services, CacheId);
            var interactions = new InteractionSystem(_services.EventBus, _services.TagTable);
            interactions.TryInteract(player, cache, new DefinitionId(LootEquipInteractionId));
            return player;
        }

        private void EquipAllManaged(GameplayObject player)
        {
            InventorySet bag = player.Get<InventorySet>();
            EquipmentSet equipment = player.Get<EquipmentSet>();
            EquipmentSlotDefinition[] managed =
            {
                _services.DataRegistry.Get<EquipmentSlotDefinition>(new DefinitionId(BootsSlotId)),
                _services.DataRegistry.Get<EquipmentSlotDefinition>(new DefinitionId(CharmSlotId)),
            };

            while (InventoryEquip.TryEquipFromInventory(bag, equipment, managed, out _, out _))
            {
            }
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
