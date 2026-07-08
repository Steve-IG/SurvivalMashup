using System;
using System.Collections.Generic;
using ToyChest.Framework.Events;
using ToyChest.Framework.Objects;

namespace ToyChest.Systems.Tags
{
    /// <summary>
    /// The aggregated active tag set of one gameplay object.
    /// Tags are reference-counted: equipment, status effects, and regions may all add the
    /// same tag, and it disappears only when every source removes it. Ancestor counts are
    /// maintained on every change, so hierarchical queries are O(1) dictionary lookups
    /// with zero allocation.
    /// On absent-to-present and present-to-absent transitions the container both raises its
    /// local C# callbacks and — when composed with an Event Bus — publishes
    /// <see cref="GameplayTagAdded"/> / <see cref="GameplayTagRemoved"/>. This is the Gameplay
    /// Tag capability completing the bridge between tag changes and the Event Bus; tag ownership
    /// stays with the Tag System. Distinct present tags are held in registration order so
    /// enumeration (<see cref="CopyTagsTo"/>) is deterministic (Engine Principle 17).
    /// </summary>
    public sealed class GameplayTagContainer : IGameplayCapability
    {
        private readonly GameplayTagTable _table;
        private readonly Dictionary<GameplayTag, int> _exactCounts = new Dictionary<GameplayTag, int>();
        private readonly Dictionary<GameplayTag, int> _hierarchicalCounts = new Dictionary<GameplayTag, int>();
        private readonly List<GameplayTag> _presentOrder = new List<GameplayTag>();
        private readonly IEventBus _eventBus;
        private readonly GameplayObjectId _owner;

        /// <summary>Raised when a tag transitions from absent to present.</summary>
        public event Action<GameplayTag> TagAdded;

        /// <summary>Raised when a tag transitions from present to absent.</summary>
        public event Action<GameplayTag> TagRemoved;

        /// <summary>
        /// Creates a container whose tags belong to <paramref name="table"/>. When
        /// <paramref name="eventBus"/> is supplied, tag transitions publish
        /// <see cref="GameplayTagAdded"/> / <see cref="GameplayTagRemoved"/> for
        /// <paramref name="owner"/>; pass null to run with local callbacks only (isolated tests).
        /// </summary>
        public GameplayTagContainer(GameplayTagTable table, IEventBus eventBus = null, GameplayObjectId owner = default)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _eventBus = eventBus;
            _owner = owner;
        }

        /// <summary>Number of distinct tags currently present (exact, not ancestor-expanded).</summary>
        public int Count => _exactCounts.Count;

        /// <summary>
        /// Adds one source's claim on <paramref name="tag"/>.
        /// Raises <see cref="TagAdded"/> only on the absent-to-present transition.
        /// </summary>
        public void AddTag(GameplayTag tag)
        {
            RequireValid(tag);

            _exactCounts.TryGetValue(tag, out int exactCount);
            _exactCounts[tag] = exactCount + 1;

            GameplayTag current = tag;
            while (current.IsValid)
            {
                _hierarchicalCounts.TryGetValue(current, out int count);
                _hierarchicalCounts[current] = count + 1;
                current = _table.GetParent(current);
            }

            if (exactCount == 0)
            {
                _presentOrder.Add(tag);
                TagAdded?.Invoke(tag);
                _eventBus?.Publish(new GameplayTagAdded(_owner, tag));
            }
        }

        /// <summary>
        /// Removes one source's claim on <paramref name="tag"/>.
        /// Raises <see cref="TagRemoved"/> only on the present-to-absent transition.
        /// </summary>
        /// <exception cref="InvalidOperationException">The tag is not present.</exception>
        public void RemoveTag(GameplayTag tag)
        {
            RequireValid(tag);

            if (!_exactCounts.TryGetValue(tag, out int exactCount))
            {
                throw new InvalidOperationException(
                    $"Cannot remove tag '{_table.GetPath(tag)}': it is not present in this container. " +
                    "Check that the removing source added the tag and is not removing it twice.");
            }

            if (exactCount == 1)
            {
                _exactCounts.Remove(tag);
            }
            else
            {
                _exactCounts[tag] = exactCount - 1;
            }

            GameplayTag current = tag;
            while (current.IsValid)
            {
                int count = _hierarchicalCounts[current];
                if (count == 1)
                {
                    _hierarchicalCounts.Remove(current);
                }
                else
                {
                    _hierarchicalCounts[current] = count - 1;
                }

                current = _table.GetParent(current);
            }

            if (exactCount == 1)
            {
                _presentOrder.Remove(tag);
                TagRemoved?.Invoke(tag);
                _eventBus?.Publish(new GameplayTagRemoved(_owner, tag));
            }
        }

        /// <summary>
        /// Hierarchical presence: true when the container holds <paramref name="tag"/>
        /// or any descendant of it.
        /// </summary>
        public bool HasTag(GameplayTag tag)
        {
            RequireValid(tag);
            return _hierarchicalCounts.ContainsKey(tag);
        }

        /// <summary>Exact presence, ignoring hierarchy.</summary>
        public bool HasTagExact(GameplayTag tag)
        {
            RequireValid(tag);
            return _exactCounts.ContainsKey(tag);
        }

        /// <summary>True when every queried tag matches hierarchically.</summary>
        public bool HasAll(IReadOnlyList<GameplayTag> tags)
        {
            RequireQuery(tags);
            for (int i = 0; i < tags.Count; i++)
            {
                if (!HasTag(tags[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>True when at least one queried tag matches hierarchically.</summary>
        public bool HasAny(IReadOnlyList<GameplayTag> tags)
        {
            RequireQuery(tags);
            for (int i = 0; i < tags.Count; i++)
            {
                if (HasTag(tags[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True when no queried tag matches hierarchically.</summary>
        public bool HasNone(IReadOnlyList<GameplayTag> tags)
        {
            return !HasAny(tags);
        }

        /// <summary>
        /// Copies the distinct present tags into <paramref name="results"/> for serialization,
        /// debugging, and tooling, in deterministic registration order (Engine Principle 17).
        /// </summary>
        public void CopyTagsTo(List<GameplayTag> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            for (int i = 0; i < _presentOrder.Count; i++)
            {
                results.Add(_presentOrder[i]);
            }
        }

        private void RequireValid(GameplayTag tag)
        {
            if (!tag.IsValid)
            {
                throw new ArgumentException(
                    "The GameplayTag is invalid. Obtain tags from the GameplayTagTable via RegisterTag or GetTag.");
            }
        }

        private static void RequireQuery(IReadOnlyList<GameplayTag> tags)
        {
            if (tags == null)
            {
                throw new ArgumentNullException(nameof(tags));
            }
        }
    }
}
