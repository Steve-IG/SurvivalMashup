using System.Collections.Generic;
using ToyChest.Framework.Modifiers;
using ToyChest.Systems.GameplayEffects;
using ToyChest.Systems.Items;
using ToyChest.Systems.StatusEffects;
using ToyChest.Systems.Tags;
using UnityEditor;
using UnityEngine;

namespace ToyChest.Tests.Effects
{
    /// <summary>
    /// Builds Gameplay Effect, condition, tag, and Status Effect assets in memory for tests,
    /// setting private serialized fields through SerializedObject and tracking instances
    /// for teardown.
    /// </summary>
    internal sealed class EffectTestFactory
    {
        private readonly List<Object> _created = new List<Object>();

        public TagDefinition CreateTag(string path)
        {
            var tag = ScriptableObject.CreateInstance<TagDefinition>();
            Track(tag);
            var serialized = new SerializedObject(tag);
            serialized.FindProperty("_tagPath").stringValue = path;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return tag;
        }

        public RequiresTagCondition CreateRequiresTag(TagDefinition tag, bool mustBePresent)
        {
            var condition = ScriptableObject.CreateInstance<RequiresTagCondition>();
            Track(condition);
            var serialized = new SerializedObject(condition);
            serialized.FindProperty("_tag").objectReferenceValue = tag;
            serialized.FindProperty("_mustBePresent").boolValue = mustBePresent;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return condition;
        }

        public DamageEffect CreateDamage(string id, string resourceId, float amount, params EffectCondition[] conditions)
        {
            return CreateResourceEffect<DamageEffect>(id, resourceId, amount, conditions);
        }

        public HealEffect CreateHeal(string id, string resourceId, float amount)
        {
            return CreateResourceEffect<HealEffect>(id, resourceId, amount);
        }

        public AddResourceEffect CreateAddResource(string id, string resourceId, float amount)
        {
            return CreateResourceEffect<AddResourceEffect>(id, resourceId, amount);
        }

        public RemoveResourceEffect CreateRemoveResource(string id, string resourceId, float amount)
        {
            return CreateResourceEffect<RemoveResourceEffect>(id, resourceId, amount);
        }

        public AddTagEffect CreateAddTag(string id, TagDefinition tag)
        {
            var effect = ScriptableObject.CreateInstance<AddTagEffect>();
            Track(effect);
            var serialized = new SerializedObject(effect);
            serialized.FindProperty("_definitionId").stringValue = id;
            serialized.FindProperty("_tag").objectReferenceValue = tag;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return effect;
        }

        public RemoveTagEffect CreateRemoveTag(string id, TagDefinition tag)
        {
            var effect = ScriptableObject.CreateInstance<RemoveTagEffect>();
            Track(effect);
            var serialized = new SerializedObject(effect);
            serialized.FindProperty("_definitionId").stringValue = id;
            serialized.FindProperty("_tag").objectReferenceValue = tag;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return effect;
        }

        public ItemDefinition CreateItem(string id, int maxStackSize = 99)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            Track(item);
            var serialized = new SerializedObject(item);
            serialized.FindProperty("_definitionId").stringValue = id;
            serialized.FindProperty("_maxStackSize").intValue = maxStackSize;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        public AddItemEffect CreateAddItem(string id, ItemDefinition item, int quantity)
        {
            var effect = ScriptableObject.CreateInstance<AddItemEffect>();
            Track(effect);
            var serialized = new SerializedObject(effect);
            serialized.FindProperty("_definitionId").stringValue = id;
            serialized.FindProperty("_item").objectReferenceValue = item;
            serialized.FindProperty("_quantity").intValue = quantity;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return effect;
        }

        public ApplyModifierEffect CreateApplyModifier(string id, string attributeId, ModifierOperation operation, float value)
        {
            var effect = ScriptableObject.CreateInstance<ApplyModifierEffect>();
            Track(effect);
            var serialized = new SerializedObject(effect);
            serialized.FindProperty("_definitionId").stringValue = id;
            serialized.FindProperty("_attributeId").stringValue = attributeId;
            serialized.FindProperty("_operation").enumValueIndex = (int)operation;
            serialized.FindProperty("_value").floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return effect;
        }

        public StatusEffectDefinition CreateStatus(
            string id,
            StatusDurationType durationType = StatusDurationType.Timed,
            float durationSeconds = 5f,
            StatusStackingRule stackingRule = StatusStackingRule.RefreshDuration,
            int maximumStacks = 1,
            TagDefinition[] grantedTags = null,
            AttributeModifierConfig[] modifiers = null,
            GameplayEffectDefinition[] onApply = null,
            float periodSeconds = 0f,
            GameplayEffectDefinition[] periodic = null,
            GameplayEffectDefinition[] onEnd = null)
        {
            var status = ScriptableObject.CreateInstance<StatusEffectDefinition>();
            Track(status);
            var serialized = new SerializedObject(status);
            serialized.FindProperty("_definitionId").stringValue = id;
            serialized.FindProperty("_durationType").enumValueIndex = (int)durationType;
            serialized.FindProperty("_durationSeconds").floatValue = durationSeconds;
            serialized.FindProperty("_stackingRule").enumValueIndex = (int)stackingRule;
            serialized.FindProperty("_maximumStacks").intValue = maximumStacks;
            FillObjectArray(serialized.FindProperty("_grantedTags"), grantedTags);
            FillModifierArray(serialized.FindProperty("_modifiers"), modifiers);
            FillObjectArray(serialized.FindProperty("_onApplyEffects"), onApply);
            serialized.FindProperty("_periodSeconds").floatValue = periodSeconds;
            FillObjectArray(serialized.FindProperty("_periodicEffects"), periodic);
            FillObjectArray(serialized.FindProperty("_onEndEffects"), onEnd);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return status;
        }

        public void Cleanup()
        {
            foreach (Object created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        private TEffect CreateResourceEffect<TEffect>(
            string id, string resourceId, float amount, params EffectCondition[] conditions)
            where TEffect : GameplayEffectDefinition
        {
            var effect = ScriptableObject.CreateInstance<TEffect>();
            Track(effect);
            var serialized = new SerializedObject(effect);
            serialized.FindProperty("_definitionId").stringValue = id;
            serialized.FindProperty("_resourceId").stringValue = resourceId;
            serialized.FindProperty("_amount").floatValue = amount;
            FillObjectArray(serialized.FindProperty("_conditions"), conditions);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return effect;
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
