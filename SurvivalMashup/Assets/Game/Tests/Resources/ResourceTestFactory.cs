using System.Collections.Generic;
using ToyChest.Systems.Attributes;
using ToyChest.Systems.Resources;
using UnityEditor;
using UnityEngine;

namespace ToyChest.Tests.Resources
{
    /// <summary>
    /// Builds ResourceDefinition and AttributeDefinition assets in memory for resource tests,
    /// setting private serialized fields through SerializedObject and tracking them for teardown.
    /// </summary>
    internal sealed class ResourceTestFactory
    {
        private readonly List<Object> _created = new List<Object>();

        public AttributeDefinition CreateAttribute(string id, float baseValue, float max = float.MaxValue)
        {
            var definition = ScriptableObject.CreateInstance<AttributeDefinition>();
            _created.Add(definition);

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_definitionId").stringValue = id;
            serialized.FindProperty("_baseValue").floatValue = baseValue;
            serialized.FindProperty("_maximumValue").floatValue = max;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        public ResourceDefinition CreateLiteral(
            string id,
            float maximum,
            bool startAtMaximum = true,
            float startingValue = 0f,
            float regenPerSecond = 0f)
        {
            var definition = ScriptableObject.CreateInstance<ResourceDefinition>();
            _created.Add(definition);

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_definitionId").stringValue = id;
            serialized.FindProperty("_literalMaximum").floatValue = maximum;
            serialized.FindProperty("_startAtMaximum").boolValue = startAtMaximum;
            serialized.FindProperty("_startingValue").floatValue = startingValue;
            serialized.FindProperty("_regenerationPerSecond").floatValue = regenPerSecond;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        public ResourceDefinition CreateBound(
            string id,
            string maximumAttributeId,
            bool startAtMaximum = true,
            float startingValue = 0f)
        {
            var definition = ScriptableObject.CreateInstance<ResourceDefinition>();
            _created.Add(definition);

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_definitionId").stringValue = id;
            serialized.FindProperty("_maximumAttributeId").stringValue = maximumAttributeId;
            serialized.FindProperty("_startAtMaximum").boolValue = startAtMaximum;
            serialized.FindProperty("_startingValue").floatValue = startingValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        public void Cleanup()
        {
            foreach (Object created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
        }
    }
}
