using System.Collections.Generic;
using ToyChest.Systems.Attributes;
using UnityEditor;
using UnityEngine;

namespace ToyChest.Tests.Attributes
{
    /// <summary>
    /// Builds AttributeDefinition assets in memory for tests, tracking them for teardown.
    /// Uses SerializedObject to set the definition's private serialized fields, exercising the
    /// same authoring path designers use.
    /// </summary>
    internal sealed class AttributeTestFactory
    {
        private readonly List<Object> _created = new List<Object>();

        public AttributeDefinition Create(string id, float baseValue, float min = 0f, float max = float.MaxValue)
        {
            var definition = ScriptableObject.CreateInstance<AttributeDefinition>();
            _created.Add(definition);

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_definitionId").stringValue = id;
            serialized.FindProperty("_displayName").stringValue = id;
            serialized.FindProperty("_baseValue").floatValue = baseValue;
            serialized.FindProperty("_minimumValue").floatValue = min;
            serialized.FindProperty("_maximumValue").floatValue = max;
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
