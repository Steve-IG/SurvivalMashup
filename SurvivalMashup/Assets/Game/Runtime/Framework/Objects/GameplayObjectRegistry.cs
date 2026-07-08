using System;
using System.Collections.Generic;

namespace ToyChest.Framework.Objects
{
    /// <summary>
    /// The authoritative owner of the set of live Gameplay Objects for one session (or one
    /// simulation scope). A plain-C# service — no Unity, no scene, no statics — so it is
    /// testable in isolation and reusable by Save, AI, Simulation, Debugging, and future
    /// Multiplayer, all of which need to enumerate "every object that currently exists".
    ///
    /// Membership is driven by the object lifecycle, not by this type: a <see cref="GameplayObject"/>
    /// registers itself when it activates and unregisters when it is destroyed (see
    /// <see cref="GameplayObject.Activate"/> and <see cref="GameplayObject.Destroy"/>). The
    /// registry therefore holds exactly the live objects and never resurrects a destroyed one.
    ///
    /// Iteration order is deterministic: registration (activation) order, preserved across
    /// removals, consistent with the ordered iteration used elsewhere in the engine
    /// (Engine Principle 17, Favor Deterministic Behavior). This matters for save ordering,
    /// replay, and multiplayer, where enumeration order must not depend on hash layout.
    /// See Docs/Systems/GAMEPLAY_FRAMEWORK.md, Gameplay Object Registry.
    /// </summary>
    public sealed class GameplayObjectRegistry
    {
        private readonly List<GameplayObject> _ordered = new List<GameplayObject>();
        private readonly Dictionary<GameplayObjectId, GameplayObject> _byId =
            new Dictionary<GameplayObjectId, GameplayObject>();

        /// <summary>Number of live objects currently registered.</summary>
        public int Count => _ordered.Count;

        /// <summary>
        /// The live objects in deterministic registration order. A stable, read-only view for
        /// enumeration; callers must not mutate the registry while iterating it.
        /// </summary>
        public IReadOnlyList<GameplayObject> Objects => _ordered;

        /// <summary>
        /// Records a newly live object. Called by <see cref="GameplayObject.Activate"/>;
        /// gameplay code does not call this directly. Registering the same id twice, or a
        /// destroyed object, is a programming error.
        /// </summary>
        public void Register(GameplayObject gameplayObject)
        {
            if (gameplayObject == null)
            {
                throw new ArgumentNullException(nameof(gameplayObject));
            }

            if (gameplayObject.IsDestroyed)
            {
                throw new InvalidOperationException(
                    $"Cannot register destroyed GameplayObject '{gameplayObject.Id}'. Only live objects are registered.");
            }

            if (_byId.ContainsKey(gameplayObject.Id))
            {
                throw new InvalidOperationException(
                    $"GameplayObject '{gameplayObject.Id}' is already registered. Each object registers once, on activation.");
            }

            _byId.Add(gameplayObject.Id, gameplayObject);
            _ordered.Add(gameplayObject);
        }

        /// <summary>
        /// Removes a destroyed object. Called by <see cref="GameplayObject.Destroy"/>.
        /// Returns false when the id was not registered, so teardown stays idempotent.
        /// </summary>
        public bool Unregister(GameplayObjectId id)
        {
            if (!_byId.Remove(id))
            {
                return false;
            }

            for (int i = 0; i < _ordered.Count; i++)
            {
                if (_ordered[i].Id == id)
                {
                    _ordered.RemoveAt(i);
                    break;
                }
            }

            return true;
        }

        /// <summary>Whether an object with this id is currently live and registered.</summary>
        public bool Contains(GameplayObjectId id) => _byId.ContainsKey(id);

        /// <summary>The live object with this id, or null when none is registered.</summary>
        public GameplayObject Get(GameplayObjectId id)
        {
            return _byId.TryGetValue(id, out GameplayObject gameplayObject) ? gameplayObject : null;
        }

        /// <summary>Tolerant lookup of a live object by id.</summary>
        public bool TryGet(GameplayObjectId id, out GameplayObject gameplayObject)
        {
            return _byId.TryGetValue(id, out gameplayObject);
        }
    }
}
