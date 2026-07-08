using System.Collections.Generic;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.Equipment;
using ToyChest.Systems.Items;
using ToyChest.Systems.StatusEffects;
using ToyChest.Systems.Tags;
using UnityEditor;
using UnityEngine;

namespace ToyChest.Tests.Items
{
    /// <summary>
    /// Builds ItemDefinition, EquipmentSlotDefinition, and EquippableDefinition assets in
    /// memory for tests, setting private serialized fields through SerializedObject and
    /// tracking instances for teardown.
    /// </summary>
    internal sealed class ItemTestFactory
    {
        private readonly List<Object> _created = new List<Object>();

        public ItemDefinition CreateItem(
            string id,
            int maxStackSize = 1,
            TagDefinition[] tags = null,
            ItemComponentDefinition[] components = null)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            Track(item);

            var serialized = new SerializedObject(item);
            serialized.FindProperty("_definitionId").stringValue = id;
            serialized.FindProperty("_displayName").stringValue = id;
            serialized.FindProperty("_maxStackSize").intValue = maxStackSize;
            FillObjectArray(serialized.FindProperty("_tags"), tags);
            FillObjectArray(serialized.FindProperty("_components"), components);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        public EquipmentSlotDefinition CreateSlot(string id)
        {
            var slot = ScriptableObject.CreateInstance<EquipmentSlotDefinition>();
            Track(slot);

            var serialized = new SerializedObject(slot);
            serialized.FindProperty("_definitionId").stringValue = id;
            serialized.FindProperty("_displayName").stringValue = id;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return slot;
        }

        public EquippableDefinition CreateEquippable(
            EquipmentSlotDefinition[] allowedSlots,
            TagDefinition[] requiredOwnerTags = null,
            TagDefinition[] grantedTags = null,
            AttributeModifierConfig[] attributeModifiers = null,
            AbilityDefinition[] grantedAbilities = null,
            StatusEffectDefinition[] appliedStatusEffects = null)
        {
            var equippable = ScriptableObject.CreateInstance<EquippableDefinition>();
            Track(equippable);

            var serialized = new SerializedObject(equippable);
            FillObjectArray(serialized.FindProperty("_allowedSlots"), allowedSlots);
            FillObjectArray(serialized.FindProperty("_requiredOwnerTags"), requiredOwnerTags);
            FillObjectArray(serialized.FindProperty("_grantedTags"), grantedTags);
            FillModifierArray(serialized.FindProperty("_attributeModifiers"), attributeModifiers);
            FillObjectArray(serialized.FindProperty("_grantedAbilities"), grantedAbilities);
            FillObjectArray(serialized.FindProperty("_appliedStatusEffects"), appliedStatusEffects);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return equippable;
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

        private static void FillModifierArray(SerializedProperty property, AttributeModifierConfig[] values)
        {
            int count = values?.Length ?? 0;
            property.arraySize = count;
            for (int i = 0; i < count; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_attributeId").stringValue = values[i].AttributeId;
                element.FindPropertyRelative("_operation").enumValueIndex = (int)values[i].Operation;
                element.FindPropertyRelative("_value").floatValue = values[i].Value;
            }
        }

        private void Track(Object created)
        {
            _created.Add(created);
        }
    }
}
