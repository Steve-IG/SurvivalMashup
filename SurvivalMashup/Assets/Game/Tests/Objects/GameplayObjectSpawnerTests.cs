using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Objects;
using ToyChest.Gameplay.Scene;
using ToyChest.Systems.Tags;
using ToyChest.Tests.Events;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ToyChest.Tests.Objects
{
    /// <summary>
    /// Verifies the canonical scene composition adapter: given an injected
    /// <see cref="GameplaySceneContext"/>, a <see cref="GameplayObjectSpawner"/> composes its
    /// definition through the real <see cref="GameplayObjectFactory"/> and binds the result to the
    /// sibling <see cref="GameplayObjectBehaviour"/>, which activates it. Composition is idempotent.
    /// </summary>
    public sealed class GameplayObjectSpawnerTests
    {
        private readonly List<Object> _created = new List<Object>();
        private readonly List<GameObject> _sceneObjects = new List<GameObject>();
        private GameplaySceneContext _context;

        [SetUp]
        public void SetUp()
        {
            var bus = new EventBus(new RecordingLogger());
            var tagTable = new GameplayTagTable();
            var factory = new GameplayObjectFactory(
                new GameplayObjectContext(bus, new DataRegistry(), tagTable));
            _context = GameplaySceneContext.Create(factory, bus, tagTable);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _sceneObjects)
            {
                Object.DestroyImmediate(go);
            }

            _sceneObjects.Clear();

            foreach (Object created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        [Test]
        public void Compose_BuildsAndBindsAndActivates_TheDefinition()
        {
            GameplayObjectSpawner spawner = CreateSpawner("object.prop");

            spawner.Compose(_context);

            GameplayObjectBehaviour behaviour = spawner.GetComponent<GameplayObjectBehaviour>();
            Assert.IsNotNull(behaviour.Object, "Composition binds the object to the behaviour.");
            Assert.AreEqual(new DefinitionId("object.prop"), behaviour.Object.DefinitionId);
            Assert.IsTrue(behaviour.Object.IsActive, "Binding an active behaviour activates the object.");
        }

        [Test]
        public void Compose_IsIdempotent()
        {
            GameplayObjectSpawner spawner = CreateSpawner("object.prop");

            spawner.Compose(_context);
            GameplayObject first = spawner.GetComponent<GameplayObjectBehaviour>().Object;
            spawner.Compose(_context);
            GameplayObject second = spawner.GetComponent<GameplayObjectBehaviour>().Object;

            Assert.AreSame(first, second, "A second injection must not recompose the object.");
        }

        private GameplayObjectSpawner CreateSpawner(string definitionId)
        {
            GameplayObjectDefinition definition = CreateDefinition(definitionId);

            var go = new GameObject("SceneObject");
            _sceneObjects.Add(go);
            go.AddComponent<GameplayObjectBehaviour>();
            GameplayObjectSpawner spawner = go.AddComponent<GameplayObjectSpawner>();

            var serialized = new SerializedObject(spawner);
            serialized.FindProperty("_definition").objectReferenceValue = definition;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return spawner;
        }

        private GameplayObjectDefinition CreateDefinition(string id)
        {
            var definition = ScriptableObject.CreateInstance<GameplayObjectDefinition>();
            _created.Add(definition);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_definitionId").stringValue = id;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }
    }
}
