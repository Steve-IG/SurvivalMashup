using System.Collections.Generic;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.Interactions;
using ToyChest.Systems.Inventory;
using ToyChest.Systems.Tags;
using UnityEditor;
using UnityEngine;

namespace ToyChest.Tests.Interactions
{
    /// <summary>
    /// Builds InteractionDefinition and InventoryDefinition assets in memory for tests,
    /// setting private serialized fields through SerializedObject and tracking instances
    /// for teardown.
    /// </summary>
    internal sealed class InteractionTestFactory
    {
        private readonly List<Object> _created = new List<Object>();

        public InteractionDefinition CreateInteraction(
            string id,
            AbilityDefinition ability,
            int priority = 0,
            TagDefinition[] requiredInteractorTags = null,
            TagDefinition[] blockedByInteractorTags = null)
        {
            var interaction = ScriptableObject.CreateInstance<InteractionDefinition>();
            Track(interaction);

            var serialized = new SerializedObject(interaction);
            serialized.FindProperty("_definitionId").stringValue = id;
            serialized.FindProperty("_displayName").stringValue = id;
            serialized.FindProperty("_ability").objectReferenceValue = ability;
            serialized.FindProperty("_priority").intValue = priority;
            FillObjectArray(serialized.FindProperty("_requiredInteractorTags"), requiredInteractorTags);
            FillObjectArray(serialized.FindProperty("_blockedByInteractorTags"), blockedByInteractorTags);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return interaction;
        }

        public InventoryDefinition CreateInventory(string id, int slotCapacity)
        {
            var inventory = ScriptableObject.CreateInstance<InventoryDefinition>();
            Track(inventory);

            var serialized = new SerializedObject(inventory);
            serialized.FindProperty("_definitionId").stringValue = id;
            serialized.FindProperty("_displayName").stringValue = id;
            serialized.FindProperty("_slotCapacity").intValue = slotCapacity;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return inventory;
        }

        public void Cleanup()
        {
            foreach (Object created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        private static void FillObjectArray(SerializedProperty property, Object[] values)
        {
            int count = values?.Length ?? 0;
            property.arraySize = count;
            for (int i = 0; i < count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private void Track(Object created)
        {
            _created.Add(created);
        }
    }
}
