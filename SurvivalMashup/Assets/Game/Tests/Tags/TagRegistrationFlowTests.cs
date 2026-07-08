using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Framework.Data;
using ToyChest.Systems.Tags;
using UnityEditor;
using UnityEngine;

namespace ToyChest.Tests.Tags
{
    /// <summary>
    /// Verifies the bootstrap flow documented in DATA_REGISTRY.md: TagDefinition assets
    /// register into the Data Registry, and the registry's definitions populate the
    /// Gameplay Tag Table.
    /// </summary>
    public sealed class TagRegistrationFlowTests
    {
        private readonly List<TagDefinition> _createdAssets = new List<TagDefinition>();

        [TearDown]
        public void TearDown()
        {
            foreach (TagDefinition definition in _createdAssets)
            {
                Object.DestroyImmediate(definition);
            }

            _createdAssets.Clear();
        }

        [Test]
        public void TagDefinitions_FlowFromRegistryIntoTable()
        {
            var registry = new DataRegistry();
            registry.Register(CreateTagDefinition("Element.Fire.Burning"));
            registry.Register(CreateTagDefinition("Combat.Melee"));

            var table = new GameplayTagTable();
            table.RegisterDefinitions(registry.GetAll<TagDefinition>());

            Assert.AreEqual(5, table.Count, "Both paths plus their ancestors must be interned.");
            Assert.IsTrue(table.TryGetTag("Element.Fire.Burning", out _));
            Assert.IsTrue(table.TryGetTag("Element", out _));
            Assert.IsTrue(table.TryGetTag("Combat.Melee", out _));
        }

        [Test]
        public void TagDefinition_UsesTagPathAsRegistryId()
        {
            var registry = new DataRegistry();
            TagDefinition definition = CreateTagDefinition("Interaction.Harvest");
            registry.Register(definition);

            TagDefinition resolved = registry.Get<TagDefinition>(new DefinitionId("Interaction.Harvest"));

            Assert.AreSame(definition, resolved);
        }

        [Test]
        public void DuplicateTagDefinitions_AreRejectedByRegistry()
        {
            var registry = new DataRegistry();
            registry.Register(CreateTagDefinition("Element.Fire"));

            Assert.Throws<System.InvalidOperationException>(
                () => registry.Register(CreateTagDefinition("Element.Fire")));
        }

        private TagDefinition CreateTagDefinition(string tagPath)
        {
            var definition = ScriptableObject.CreateInstance<TagDefinition>();
            _createdAssets.Add(definition);

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_tagPath").stringValue = tagPath;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }
    }
}
