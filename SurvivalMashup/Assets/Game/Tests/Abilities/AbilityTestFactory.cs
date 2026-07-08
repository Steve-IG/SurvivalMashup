using System.Collections.Generic;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.GameplayEffects;
using ToyChest.Systems.Tags;
using UnityEditor;
using UnityEngine;

namespace ToyChest.Tests.Abilities
{
    /// <summary>
    /// Builds AbilityDefinition assets in memory for tests, setting private serialized
    /// fields through SerializedObject and tracking instances for teardown.
    /// </summary>
    internal sealed class AbilityTestFactory
    {
        private readonly List<Object> _created = new List<Object>();

        public AbilityDefinition CreateAbility(
            string id,
            AbilityTargetMode targetMode = AbilityTargetMode.Self,
            ResourceCostConfig[] costs = null,
            float cooldownSeconds = 0f,
            TagDefinition[] requiredOwnerTags = null,
            TagDefinition[] blockedByOwnerTags = null,
            GameplayEffectDefinition[] effects = null)
        {
            var ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            _created.Add(ability);

            var serialized = new SerializedObject(ability);
            serialized.FindProperty("_definitionId").stringValue = id;
            serialized.FindProperty("_targetMode").enumValueIndex = (int)targetMode;
            serialized.FindProperty("_cooldownSeconds").floatValue = cooldownSeconds;
            FillCostArray(serialized.FindProperty("_costs"), costs);
            FillObjectArray(serialized.FindProperty("_requiredOwnerTags"), requiredOwnerTags);
            FillObjectArray(serialized.FindProperty("_blockedByOwnerTags"), blockedByOwnerTags);
            FillObjectArray(serialized.FindProperty("_effects"), effects);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return ability;
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

        private static void FillCostArray(SerializedProperty property, ResourceCostConfig[] values)
        {
            int count = values?.Length ?? 0;
            property.arraySize = count;
            for (int i = 0; i < count; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_resourceId").stringValue = values[i].ResourceId;
                element.FindPropertyRelative("_amount").floatValue = values[i].Amount;
            }
        }
    }
}
