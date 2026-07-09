using System;
using NUnit.Framework;
using ToyChest.Framework.Data;
using ToyChest.Framework.Modifiers;
using ToyChest.Framework.Objects;
using ToyChest.Systems.Attributes;
using ToyChest.Systems.GameplayEffects;
using ToyChest.Systems.Inventory;
using ToyChest.Systems.Items;
using ToyChest.Systems.Resources;
using ToyChest.Systems.Tags;
using ToyChest.Tests.Resources;

namespace ToyChest.Tests.Effects
{
    /// <summary>
    /// Verifies atomic effect execution: resource mutation, tag mutation, modifier
    /// application under a revocable source, condition gating, deterministic sequence
    /// running, and clear failures on missing capabilities.
    /// </summary>
    public sealed class GameplayEffectTests
    {
        private const float Tolerance = 1e-4f;

        private EffectTestFactory _effects;
        private ResourceTestFactory _definitions;
        private GameplayTagTable _tagTable;
        private GameplayEffectRunner _runner;

        private AttributeSet _attributes;
        private ResourceSet _resources;
        private GameplayTagContainer _tags;
        private EffectContext _context;

        [SetUp]
        public void SetUp()
        {
            _effects = new EffectTestFactory();
            _definitions = new ResourceTestFactory();
            _tagTable = new GameplayTagTable();
            _runner = new GameplayEffectRunner();

            _attributes = new AttributeSet();
            _attributes.AddAttribute(_definitions.CreateAttribute("attribute.max-health", 100f));
            _attributes.AddAttribute(_definitions.CreateAttribute("attribute.move-speed", 10f));
            _resources = new ResourceSet(attributes: _attributes);
            _resources.AddResource(_definitions.CreateBound("resource.health", "attribute.max-health"));
            _tags = new GameplayTagContainer(_tagTable);

            _context = new EffectContext(
                default, new EffectTarget(_resources, _attributes, _tags), _tagTable);
        }

        [TearDown]
        public void TearDown()
        {
            _effects.Cleanup();
            _definitions.Cleanup();
        }

        private ResourceValue Health => _resources.GetResource(new DefinitionId("resource.health"));

        [Test]
        public void Damage_ReducesTargetResource()
        {
            var damage = _effects.CreateDamage("fx.damage", "resource.health", 30f);

            Assert.IsTrue(damage.TryExecute(in _context));

            Assert.AreEqual(70f, Health.Current, Tolerance);
        }

        [Test]
        public void Heal_RestoresTargetResource()
        {
            Health.Consume(50f);
            var heal = _effects.CreateHeal("fx.heal", "resource.health", 20f);

            heal.TryExecute(in _context);

            Assert.AreEqual(70f, Health.Current, Tolerance);
        }

        [Test]
        public void AddAndRemoveResource_AreNeutralMutations()
        {
            Health.Consume(50f);
            _effects.CreateAddResource("fx.gain", "resource.health", 10f).TryExecute(in _context);
            Assert.AreEqual(60f, Health.Current, Tolerance);

            _effects.CreateRemoveResource("fx.spend", "resource.health", 25f).TryExecute(in _context);
            Assert.AreEqual(35f, Health.Current, Tolerance);
        }

        [Test]
        public void AddTag_And_RemoveTag_MutateContainer()
        {
            TagDefinition burning = _effects.CreateTag("State.Burning");
            _tagTable.RegisterTag("State.Burning");

            _effects.CreateAddTag("fx.ignite", burning).TryExecute(in _context);
            Assert.IsTrue(_tags.HasTagExact(_tagTable.GetTag("State.Burning")));

            _effects.CreateRemoveTag("fx.douse", burning).TryExecute(in _context);
            Assert.IsFalse(_tags.HasTagExact(_tagTable.GetTag("State.Burning")));
        }

        [Test]
        public void AddItem_AddsAuthoredStackToTargetInventory()
        {
            ItemDefinition scrap = _effects.CreateItem("item.scrap", maxStackSize: 99);
            var inventory = new InventorySet(default, null, slotCapacity: 8);
            var context = new EffectContext(
                default, new EffectTarget(_resources, _attributes, _tags, inventory), _tagTable);

            _effects.CreateAddItem("fx.loot", scrap, quantity: 3).TryExecute(in context);

            Assert.AreEqual(3, inventory.QuantityOf(new DefinitionId("item.scrap")),
                "Add Item deposits the authored quantity into the target's inventory.");
        }

        [Test]
        public void AddItem_MissingInventory_FailsClearly()
        {
            ItemDefinition scrap = _effects.CreateItem("item.scrap");
            var addItem = _effects.CreateAddItem("fx.loot", scrap, quantity: 1);

            // The default context target carries no inventory.
            var exception = Assert.Throws<InvalidOperationException>(() => addItem.TryExecute(in _context));
            StringAssert.Contains("fx.loot", exception.Message);
        }

        [Test]
        public void ApplyModifier_RegistersUnderContextSource_AndIsRevocable()
        {
            var source = new object();
            var slow = _effects.CreateApplyModifier("fx.slow", "attribute.move-speed", ModifierOperation.AdditivePercent, -0.5f);
            var context = new EffectContext(
                default, new EffectTarget(_resources, _attributes, _tags), _tagTable, modifierSource: source);

            slow.TryExecute(in context);
            Assert.AreEqual(5f, _attributes.GetValue(new DefinitionId("attribute.move-speed")), Tolerance);

            _attributes.GetAttribute(new DefinitionId("attribute.move-speed")).RemoveModifiersFrom(source);
            Assert.AreEqual(10f, _attributes.GetValue(new DefinitionId("attribute.move-speed")), Tolerance);
        }

        [Test]
        public void Condition_GatesExecution()
        {
            TagDefinition wet = _effects.CreateTag("State.Wet");
            GameplayTag wetTag = _tagTable.RegisterTag("State.Wet");
            var notWhileWet = _effects.CreateRequiresTag(wet, mustBePresent: false);
            var ignite = _effects.CreateDamage("fx.ignite-damage", "resource.health", 10f, notWhileWet);

            _tags.AddTag(wetTag);
            Assert.IsFalse(ignite.TryExecute(in _context), "A wet target must not be ignitable.");
            Assert.AreEqual(100f, Health.Current, Tolerance);

            _tags.RemoveTag(wetTag);
            Assert.IsTrue(ignite.TryExecute(in _context));
            Assert.AreEqual(90f, Health.Current, Tolerance);
        }

        [Test]
        public void Runner_ExecutesSequenceInOrder_AndReportsExecutedCount()
        {
            TagDefinition wet = _effects.CreateTag("State.Wet");
            _tagTable.RegisterTag("State.Wet");
            var blocked = _effects.CreateDamage(
                "fx.blocked", "resource.health", 999f, _effects.CreateRequiresTag(wet, mustBePresent: true));
            var first = _effects.CreateDamage("fx.first", "resource.health", 10f);
            var second = _effects.CreateDamage("fx.second", "resource.health", 20f);

            int executed = _runner.Execute(new GameplayEffectDefinition[] { first, blocked, second }, in _context);

            Assert.AreEqual(2, executed, "The blocked effect skips only itself.");
            Assert.AreEqual(70f, Health.Current, Tolerance);
        }

        [Test]
        public void Runner_NullEntry_FailsClearly()
        {
            Assert.Throws<InvalidOperationException>(
                () => _runner.Execute(new GameplayEffectDefinition[] { null }, in _context));
        }

        [Test]
        public void MissingCapability_FailsClearly()
        {
            var damage = _effects.CreateDamage("fx.damage", "resource.health", 10f);
            var noResources = new EffectContext(default, new EffectTarget(null, _attributes, _tags), _tagTable);

            var exception = Assert.Throws<InvalidOperationException>(() => damage.TryExecute(in noResources));
            StringAssert.Contains("fx.damage", exception.Message);
        }
    }
}
