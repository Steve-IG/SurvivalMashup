using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Objects;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.Equipment;
using ToyChest.Systems.Interactions;
using ToyChest.Systems.Inventory;
using ToyChest.Systems.Items;
using ToyChest.Systems.Tags;
using ToyChest.Tests.Abilities;
using ToyChest.Tests.Effects;
using ToyChest.Tests.Events;
using ToyChest.Tests.Interactions;
using ToyChest.Tests.Items;
using UnityEditor;
using UnityEngine;

namespace ToyChest.Tests.Objects
{
    /// <summary>
    /// Verifies the composition root's Review Group 7 wiring: inventory, equipment, and
    /// interaction capabilities composed from the object definition, interaction abilities
    /// auto-granted, and a definition declaring none of them composing none of them.
    /// </summary>
    public sealed class GameplayObjectFactoryItemSystemsTests
    {
        private readonly List<Object> _created = new List<Object>();
        private ItemTestFactory _items;
        private InteractionTestFactory _interactions;
        private AbilityTestFactory _abilities;
        private EffectTestFactory _effects;
        private GameplayTagTable _tagTable;
        private GameplayObjectFactory _factory;
        private EventBus _bus;

        [SetUp]
        public void SetUp()
        {
            _items = new ItemTestFactory();
            _interactions = new InteractionTestFactory();
            _abilities = new AbilityTestFactory();
            _effects = new EffectTestFactory();
            _tagTable = new GameplayTagTable();
            _bus = new EventBus(new RecordingLogger());
            _factory = new GameplayObjectFactory(
                new GameplayObjectContext(_bus, new DataRegistry(), _tagTable));
        }

        [TearDown]
        public void TearDown()
        {
            _items.Cleanup();
            _interactions.Cleanup();
            _abilities.Cleanup();
            _effects.Cleanup();
            foreach (Object created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public void Create_ComposesInventory_WithDeclaredCapacity()
        {
            GameplayObjectDefinition definition = CreateObjectDefinition(
                "object.chest", inventory: _interactions.CreateInventory("inventory.small-chest", 8));

            GameplayObject chest = _factory.Create(definition);

            Assert.IsTrue(chest.Has<InventorySet>());
            Assert.AreEqual(8, chest.Get<InventorySet>().SlotCapacity);
        }

        [Test]
        public void Create_ComposesEquipment_WithDeclaredSlotLayout()
        {
            EquipmentSlotDefinition weapon = _items.CreateSlot("slot.weapon");
            EquipmentSlotDefinition helmet = _items.CreateSlot("slot.helmet");
            GameplayObjectDefinition definition = CreateObjectDefinition(
                "object.player", equipmentSlots: new[] { weapon, helmet });

            GameplayObject player = _factory.Create(definition);

            Assert.IsTrue(player.Has<EquipmentSet>());
            Assert.AreEqual(2, player.Get<EquipmentSet>().Slots.Count);
            Assert.IsTrue(player.Get<EquipmentSet>().HasSlot(weapon.Id));
        }

        [Test]
        public void Create_ComposesInteractions_AndAutoGrantsTheirAbilities()
        {
            AbilityDefinition open = _abilities.CreateAbility("ability.open");
            InteractionDefinition interaction = _interactions.CreateInteraction("interaction.open", open);
            GameplayObjectDefinition definition = CreateObjectDefinition(
                "object.chest", interactions: new[] { interaction });

            GameplayObject chest = _factory.Create(definition);

            Assert.IsTrue(chest.Has<InteractionSet>());
            Assert.IsTrue(chest.Get<InteractionSet>().Has(interaction.Id));
            Assert.IsTrue(chest.Get<AbilitySet>().Has(open.Id),
                "Interaction abilities are granted at composition so interactions can execute.");
        }

        [Test]
        public void Create_InteractionAbilityAlsoDeclaredOnObject_IsGrantedOnce()
        {
            AbilityDefinition open = _abilities.CreateAbility("ability.open");
            InteractionDefinition interaction = _interactions.CreateInteraction("interaction.open", open);
            GameplayObjectDefinition definition = CreateObjectDefinition(
                "object.chest", initialAbilities: new[] { open }, interactions: new[] { interaction });

            GameplayObject chest = _factory.Create(definition);

            Assert.AreEqual(1, chest.Get<AbilitySet>().Count, "The declared grant satisfies the interaction; no duplicate.");
        }

        [Test]
        public void Create_WithoutItemSystemDeclarations_ComposesNoneOfThem()
        {
            GameplayObjectDefinition definition = CreateObjectDefinition("object.pebble");

            GameplayObject pebble = _factory.Create(definition);

            Assert.IsFalse(pebble.Has<InventorySet>());
            Assert.IsFalse(pebble.Has<EquipmentSet>());
            Assert.IsFalse(pebble.Has<InteractionSet>());
        }

        [Test]
        public void ComposedObject_ExecutesInteraction_EndToEnd()
        {
            _tagTable.RegisterTag("State.Opened");
            AbilityDefinition open = _abilities.CreateAbility(
                "ability.open",
                effects: new ToyChest.Systems.GameplayEffects.GameplayEffectDefinition[]
                {
                    _effects.CreateAddTag("fx.mark-opened", _effects.CreateTag("State.Opened")),
                });
            InteractionDefinition interaction = _interactions.CreateInteraction("interaction.open", open);
            GameplayObject chest = _factory.Create(
                CreateObjectDefinition("object.chest", interactions: new[] { interaction }));
            GameplayObject player = _factory.Create(CreateObjectDefinition("object.player"));

            var system = new InteractionSystem(_bus, _tagTable);
            InteractionResult result = system.TryInteract(player, chest, interaction.Id);

            Assert.AreEqual(InteractionResult.Executed, result);
            Assert.IsTrue(chest.Get<GameplayTagContainer>().HasTagExact(_tagTable.GetTag("State.Opened")));
        }

        private GameplayObjectDefinition CreateObjectDefinition(
            string id,
            InventoryDefinition inventory = null,
            EquipmentSlotDefinition[] equipmentSlots = null,
            InteractionDefinition[] interactions = null,
            AbilityDefinition[] initialAbilities = null)
        {
            var definition = ScriptableObject.CreateInstance<GameplayObjectDefinition>();
            _created.Add(definition);

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_definitionId").stringValue = id;
            serialized.FindProperty("_inventory").objectReferenceValue = inventory;
            FillArray(serialized.FindProperty("_equipmentSlots"), equipmentSlots);
            FillArray(serialized.FindProperty("_interactions"), interactions);
            FillArray(serialized.FindProperty("_abilities"), initialAbilities);
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
