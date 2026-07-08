using System;
using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Modifiers;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Objects;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.Attributes;
using ToyChest.Systems.Equipment;
using ToyChest.Systems.Inventory;
using ToyChest.Systems.Items;
using ToyChest.Systems.Resources;
using ToyChest.Systems.Save;
using ToyChest.Systems.StatusEffects;
using ToyChest.Systems.Tags;
using ToyChest.Tests.Abilities;
using ToyChest.Tests.Effects;
using ToyChest.Tests.Events;
using ToyChest.Tests.Interactions;
using ToyChest.Tests.Items;
using ToyChest.Tests.Resources;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ToyChest.Tests.Save
{
    /// <summary>
    /// Verifies the Save Framework end-to-end: capturing authoritative state through the registry,
    /// restoring it by reconstructing through the composition root, restoring derived state by
    /// re-running composition (not by serializing it), event-quiet reconstruction with a single
    /// observable spawn, deterministic ordering, the JSON contract, and version guarding.
    /// </summary>
    public sealed class SaveManagerTests
    {
        private const float Tolerance = 1e-4f;

        private readonly List<Object> _created = new List<Object>();
        private ResourceTestFactory _resources;
        private AbilityTestFactory _abilities;
        private EffectTestFactory _effects;
        private ItemTestFactory _items;
        private InteractionTestFactory _interactions;

        private GameplayTagTable _tagTable;
        private EventBus _bus;
        private DataRegistry _definitions;
        private GameplayObjectRegistry _world;
        private GameplayObjectFactory _factory;
        private SaveManager _save;

        // Definitions and instances the tests assert against.
        private GameplayObjectDefinition _playerDef;
        private ResourceDefinition _health;
        private ResourceDefinition _mana;
        private AbilityDefinition _dash;
        private StatusEffectDefinition _burning;
        private ItemDefinition _swordItem;
        private ItemDefinition _potion;
        private EquipmentSlotDefinition _weaponSlot;

        private GameplayObject _player;
        private ItemInstance _sword;
        private ItemInstance _potions;

        [SetUp]
        public void SetUp()
        {
            _resources = new ResourceTestFactory();
            _abilities = new AbilityTestFactory();
            _effects = new EffectTestFactory();
            _items = new ItemTestFactory();
            _interactions = new InteractionTestFactory();

            _tagTable = new GameplayTagTable();
            _tagTable.RegisterTag("State.Burning");
            _bus = new EventBus(new RecordingLogger());
            _definitions = new DataRegistry();
            _world = new GameplayObjectRegistry();
            _factory = new GameplayObjectFactory(new GameplayObjectContext(_bus, _definitions, _tagTable, _world));
            _save = new SaveManager();

            BuildDefinitions();
            RegisterResolvableDefinitions();
            BuildAndPopulatePlayer();
        }

        [TearDown]
        public void TearDown()
        {
            _resources.Cleanup();
            _abilities.Cleanup();
            _effects.Cleanup();
            _items.Cleanup();
            _interactions.Cleanup();
            foreach (Object created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        // ------------------------------------------------------------------ Round trip

        [Test]
        public void Restore_RebuildsAuthoritativeAndDerivedStateOfEveryCapability()
        {
            SaveData data = _save.Capture(_world);

            (GameplayObjectFactory factory, GameplayObjectRegistry world) = NewWorld(_bus);
            IReadOnlyList<GameplayObject> restored = _save.Restore(data, factory, _definitions);

            Assert.AreEqual(1, restored.Count);
            GameplayObject player = restored[0];

            Assert.AreEqual(_player.Id, player.Id, "Identity is authoritative and preserved.");
            Assert.AreEqual(_player.DefinitionId, player.DefinitionId);
            Assert.IsTrue(player.IsActive, "A restored object is activated into the live simulation.");
            Assert.IsTrue(world.Contains(player.Id), "Activation registers the restored object in the world.");

            ResourceValue health = player.Get<ResourceSet>().GetResource(_health.Id);
            Assert.AreEqual(150f, health.Maximum, Tolerance,
                "The equipment attribute modifier is re-applied by composition, so the derived maximum is rebuilt.");
            Assert.AreEqual(130f, health.Current, Tolerance,
                "Current restores against the reconstructed maximum; equipment must restore before resources.");
            Assert.AreEqual(20f, player.Get<ResourceSet>().GetResource(_mana.Id).Current, Tolerance);

            AbilityInstance dash = player.Get<AbilitySet>().GetAbility(_dash.Id);
            Assert.IsTrue(dash.IsOnCooldown);
            Assert.AreEqual(5f, dash.CooldownRemaining, Tolerance);

            StatusEffectSet statuses = player.Get<StatusEffectSet>();
            Assert.IsTrue(statuses.Has(_burning.Id));
            Assert.AreEqual(1, statuses.GetStacks(_burning.Id));
            Assert.IsTrue(player.Get<GameplayTagContainer>().HasTagExact(_tagTable.GetTag("State.Burning")),
                "The status's granted tag is reconstructed, not serialized.");

            InventorySet inventory = player.Get<InventorySet>();
            Assert.AreEqual(1, inventory.StackCount);
            Assert.AreEqual(5, inventory.QuantityOf(_potion.Id));
            Assert.IsNotNull(inventory.GetStack(_potions.Id), "The stack's instance id is preserved across the save boundary.");

            EquipmentSet equipment = player.Get<EquipmentSet>();
            ItemInstance equippedSword = equipment.GetEquipped(_weaponSlot.Id);
            Assert.IsNotNull(equippedSword);
            Assert.AreEqual(_swordItem.Id, equippedSword.Definition.Id);
            Assert.AreEqual(_sword.Id, equippedSword.Id, "The equipped instance id is preserved across the save boundary.");
        }

        [Test]
        public void Capture_SerializesOnlyAuthoritativeState_NotDerived()
        {
            SaveData data = _save.Capture(_world);
            GameplayObjectState state = data.Objects[0];

            // Resource current is authoritative and present; there is no attribute or tag list at all.
            Assert.AreEqual(2, state.Resources.Count, "Both resources' current values are captured.");
            Assert.AreEqual(1, state.Cooldowns.Count, "Only the cooling-down ability is recorded.");
            Assert.AreEqual(_dash.Id.Value, state.Cooldowns[0].AbilityId);
            Assert.AreEqual(1, state.Statuses.Count);
            Assert.AreEqual(1, state.Equipment.Count);
            Assert.AreEqual(1, state.Inventory.Count);

            // The capture model has no field for attribute values or tags: derived state is rebuilt,
            // never stored (Engine Principle 25). This is a structural guarantee of GameplayObjectState.
            foreach (ResourceState resource in state.Resources)
            {
                Assert.IsFalse(string.IsNullOrEmpty(resource.ResourceId));
            }
        }

        // ------------------------------------------------------------------ Event-quiet reconstruction

        [Test]
        public void Restore_IsEventQuiet_AndActivationIsTheOnlyObservableFact()
        {
            SaveData data = _save.Capture(_world);

            var quietBus = new EventBus(new RecordingLogger());
            var attributeChanges = new List<AttributeChanged>();
            var resourceChanges = new List<ResourceChanged>();
            var tagsAdded = new List<GameplayTagAdded>();
            var statusApplied = new List<StatusApplied>();
            var abilitiesGranted = new List<AbilityGranted>();
            var spawned = new List<GameplayObjectSpawned>();
            quietBus.Subscribe<AttributeChanged>(attributeChanges.Add);
            quietBus.Subscribe<ResourceChanged>(resourceChanges.Add);
            quietBus.Subscribe<GameplayTagAdded>(tagsAdded.Add);
            quietBus.Subscribe<StatusApplied>(statusApplied.Add);
            quietBus.Subscribe<AbilityGranted>(abilitiesGranted.Add);
            quietBus.Subscribe<GameplayObjectSpawned>(spawned.Add);

            (GameplayObjectFactory factory, GameplayObjectRegistry _) = NewWorld(quietBus);
            _save.Restore(data, factory, _definitions);

            Assert.AreEqual(0, attributeChanges.Count, "Re-applying equipment modifiers during reconstruction is quiet.");
            Assert.AreEqual(0, resourceChanges.Count, "Restoring resource current values during reconstruction is quiet.");
            Assert.AreEqual(0, tagsAdded.Count, "Re-granting status/equipment tags during reconstruction is quiet.");
            Assert.AreEqual(0, statusApplied.Count, "Reconstructing a status is not a fresh application.");
            Assert.AreEqual(0, abilitiesGranted.Count, "Re-granting composed abilities during reconstruction is quiet.");
            Assert.AreEqual(1, spawned.Count, "Activation is the single observable fact, carrying fully restored state.");
        }

        // ------------------------------------------------------------------ Determinism and JSON

        [Test]
        public void Capture_EnumeratesInRegistryOrder_AndIsDeterministic()
        {
            GameplayObject rock = _factory.Create(CreateObjectDefinition("object.rock"));
            rock.Activate();

            SaveData data = _save.Capture(_world);
            Assert.AreEqual(2, data.Objects.Count);
            Assert.AreEqual(_player.Id.ToString(), data.Objects[0].ObjectId, "Player activated first.");
            Assert.AreEqual(rock.Id.ToString(), data.Objects[1].ObjectId, "Rock activated second; registration order is preserved.");

            Assert.AreEqual(_save.ToJson(data), _save.ToJson(_save.Capture(_world)),
                "Capturing the same world twice yields identical JSON.");
        }

        [Test]
        public void Json_RoundTrip_PreservesState()
        {
            string json = _save.ToJson(_save.Capture(_world));

            SaveData parsed = _save.FromJson(json);
            (GameplayObjectFactory factory, GameplayObjectRegistry _) = NewWorld(_bus);
            GameplayObject player = _save.Restore(parsed, factory, _definitions)[0];

            Assert.AreEqual(_player.Id, player.Id);
            Assert.AreEqual(130f, player.Get<ResourceSet>().GetResource(_health.Id).Current, Tolerance);
            Assert.IsTrue(player.Get<StatusEffectSet>().Has(_burning.Id));
            Assert.AreEqual(5, player.Get<InventorySet>().QuantityOf(_potion.Id));
        }

        [Test]
        public void FromJson_UnsupportedVersion_Throws()
        {
            SaveData data = _save.Capture(_world);
            data.Version = SaveManager.CurrentVersion + 1;
            string json = _save.ToJson(data);

            Assert.Throws<NotSupportedException>(() => _save.FromJson(json));
        }

        [Test]
        public void Restore_UnknownDefinition_Throws()
        {
            var data = new SaveData { Version = SaveManager.CurrentVersion };
            data.Objects.Add(new GameplayObjectState
            {
                ObjectId = GameplayObjectId.New().ToString(),
                DefinitionId = "object.does-not-exist",
            });

            (GameplayObjectFactory factory, GameplayObjectRegistry _) = NewWorld(_bus);
            Assert.Throws<KeyNotFoundException>(() => _save.Restore(data, factory, _definitions));
        }

        // ------------------------------------------------------------------ World building

        private (GameplayObjectFactory, GameplayObjectRegistry) NewWorld(IEventBus bus)
        {
            var world = new GameplayObjectRegistry();
            var factory = new GameplayObjectFactory(new GameplayObjectContext(bus, _definitions, _tagTable, world));
            return (factory, world);
        }

        private void BuildDefinitions()
        {
            AttributeDefinition maxHealth = _resources.CreateAttribute("attribute.max-health", 100f);
            _health = _resources.CreateBound("resource.health", "attribute.max-health");
            _mana = _resources.CreateLiteral("resource.mana", 50f);
            _dash = _abilities.CreateAbility("ability.dash", cooldownSeconds: 5f);
            _burning = _effects.CreateStatus(
                "status.burning",
                durationType: StatusDurationType.Timed,
                durationSeconds: 3f,
                grantedTags: new[] { _effects.CreateTag("State.Burning") });

            _weaponSlot = _items.CreateSlot("slot.weapon");
            EquippableDefinition equippable = _items.CreateEquippable(
                allowedSlots: new[] { _weaponSlot },
                attributeModifiers: new[] { new AttributeModifierConfig("attribute.max-health", ModifierOperation.Flat, 50f) });
            _swordItem = _items.CreateItem("item.sword", maxStackSize: 1, components: new ItemComponentDefinition[] { equippable });
            _potion = _items.CreateItem("item.potion", maxStackSize: 10);

            _playerDef = CreateObjectDefinition(
                "object.player",
                attributes: new[] { maxHealth },
                resources: new[] { _health, _mana },
                abilities: new[] { _dash },
                inventory: _interactions.CreateInventory("inventory.backpack", 8),
                equipmentSlots: new[] { _weaponSlot });
        }

        private void RegisterResolvableDefinitions()
        {
            // Only definitions resolved by id at load time need the Data Registry: the object
            // definition (reconstruction), item definitions (inventory/equipment), and status
            // definitions (independent status restore).
            _definitions.Register(_playerDef);
            _definitions.Register(_swordItem);
            _definitions.Register(_potion);
            _definitions.Register(_burning);
        }

        private void BuildAndPopulatePlayer()
        {
            _player = _factory.Create(_playerDef);
            _player.Activate();

            _sword = new ItemInstance(_swordItem);
            Assert.AreEqual(EquipResult.Equipped, _player.Get<EquipmentSet>().TryEquip(_sword, _weaponSlot.Id));

            _player.Get<ResourceSet>().GetResource(_health.Id).SetCurrent(130f);
            _player.Get<ResourceSet>().GetResource(_mana.Id).SetCurrent(20f);

            Assert.AreEqual(AbilityActivationResult.Activated, _player.Get<AbilitySet>().TryActivate(_dash.Id));

            _player.Get<StatusEffectSet>().Apply(_burning);

            _potions = new ItemInstance(_potion, 5);
            Assert.IsTrue(_player.Get<InventorySet>().TryAdd(_potions));
        }

        private GameplayObjectDefinition CreateObjectDefinition(
            string id,
            AttributeDefinition[] attributes = null,
            ResourceDefinition[] resources = null,
            AbilityDefinition[] abilities = null,
            InventoryDefinition inventory = null,
            EquipmentSlotDefinition[] equipmentSlots = null)
        {
            var definition = ScriptableObject.CreateInstance<GameplayObjectDefinition>();
            _created.Add(definition);

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_definitionId").stringValue = id;
            FillArray(serialized.FindProperty("_attributes"), attributes);
            FillArray(serialized.FindProperty("_resources"), resources);
            FillArray(serialized.FindProperty("_abilities"), abilities);
            serialized.FindProperty("_inventory").objectReferenceValue = inventory;
            FillArray(serialized.FindProperty("_equipmentSlots"), equipmentSlots);
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
