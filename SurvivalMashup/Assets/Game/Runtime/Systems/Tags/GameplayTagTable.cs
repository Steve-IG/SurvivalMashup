using System;
using System.Collections.Generic;

namespace ToyChest.Systems.Tags
{
    /// <summary>
    /// Interns the hierarchical tag vocabulary for one session and answers structural
    /// questions about it: parents, depth, ancestry. Registering a path automatically
    /// registers every ancestor. Runtime identity is the interned index carried by
    /// <see cref="GameplayTag"/>; interned indices are session-local and never persisted —
    /// serialization uses paths. Plain C#; created at bootstrap and populated from
    /// TagDefinition assets via the Data Registry, or directly by tests and tooling.
    /// See Docs/Systems/TAG_SYSTEM.md.
    /// </summary>
    public sealed class GameplayTagTable
    {
        private const char Separator = '.';

        private readonly Dictionary<string, int> _pathToIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<Entry> _entries = new List<Entry>();

        /// <summary>Number of interned tags, ancestors included.</summary>
        public int Count => _entries.Count;

        /// <summary>
        /// Interns <paramref name="path"/> and every ancestor, returning the leaf tag.
        /// Idempotent: re-registering an existing path returns the existing tag.
        /// </summary>
        /// <exception cref="ArgumentException">The path is empty or malformed.</exception>
        public GameplayTag RegisterTag(string path)
        {
            ValidatePath(path);

            int parentIndex = -1;
            int segmentStart = 0;
            GameplayTag result = default;

            while (segmentStart <= path.Length)
            {
                int separatorIndex = path.IndexOf(Separator, segmentStart);
                int segmentEnd = separatorIndex < 0 ? path.Length : separatorIndex;
                string ancestorPath = path.Substring(0, segmentEnd);

                if (!_pathToIndex.TryGetValue(ancestorPath, out int index))
                {
                    index = _entries.Count;
                    _entries.Add(new Entry(ancestorPath, parentIndex, CountDepth(ancestorPath)));
                    _pathToIndex.Add(ancestorPath, index);
                }

                parentIndex = index;
                result = new GameplayTag(index);

                if (separatorIndex < 0)
                {
                    break;
                }

                segmentStart = separatorIndex + 1;
            }

            return result;
        }

        /// <summary>
        /// Resolves a registered path, throwing when unknown. Querying an unregistered
        /// tag is a content or programming error; use <see cref="TryGetTag"/> to probe.
        /// </summary>
        public GameplayTag GetTag(string path)
        {
            if (TryGetTag(path, out GameplayTag tag))
            {
                return tag;
            }

            throw new KeyNotFoundException(
                $"Tag '{path}' is not registered. Check the tag path spelling and that its " +
                "TagDefinition is included in a definition source at bootstrap.");
        }

        /// <summary>Tolerant path resolution.</summary>
        public bool TryGetTag(string path, out GameplayTag tag)
        {
            if (path != null && _pathToIndex.TryGetValue(path, out int index))
            {
                tag = new GameplayTag(index);
                return true;
            }

            tag = default;
            return false;
        }

        /// <summary>The full dot-separated path of a tag, for serialization and display.</summary>
        public string GetPath(GameplayTag tag)
        {
            return GetEntry(tag).Path;
        }

        /// <summary>The parent tag, or an invalid tag for roots.</summary>
        public GameplayTag GetParent(GameplayTag tag)
        {
            int parentIndex = GetEntry(tag).ParentIndex;
            return parentIndex < 0 ? default : new GameplayTag(parentIndex);
        }

        /// <summary>Number of segments in the tag's path: "Element.Fire" has depth 2.</summary>
        public int GetDepth(GameplayTag tag)
        {
            return GetEntry(tag).Depth;
        }

        /// <summary>
        /// Whether <paramref name="query"/> matches <paramref name="tag"/> hierarchically:
        /// true when they are equal or the query is an ancestor of the tag.
        /// </summary>
        public bool Matches(GameplayTag query, GameplayTag tag)
        {
            RequireValid(query);
            GameplayTag current = tag;
            while (current.IsValid)
            {
                if (current == query)
                {
                    return true;
                }

                current = GetParent(current);
            }

            return false;
        }

        /// <summary>Interns the paths of every supplied tag definition.</summary>
        public void RegisterDefinitions(IEnumerable<TagDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            foreach (TagDefinition definition in definitions)
            {
                RegisterTag(definition.TagPath);
            }
        }

        private Entry GetEntry(GameplayTag tag)
        {
            RequireValid(tag);
            return _entries[tag.Index];
        }

        private void RequireValid(GameplayTag tag)
        {
            if (!tag.IsValid || tag.Index >= _entries.Count)
            {
                throw new ArgumentException(
                    "The GameplayTag is invalid or belongs to a different tag table. " +
                    "Obtain tags from this table via RegisterTag or GetTag.");
            }
        }

        private static void ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A tag path must be non-empty.", nameof(path));
            }

            int segmentLength = 0;
            foreach (char character in path)
            {
                if (character == Separator)
                {
                    if (segmentLength == 0)
                    {
                        throw new ArgumentException(
                            $"Tag path '{path}' contains an empty segment. " +
                            "Segments are separated by single dots, e.g. 'Element.Fire.Burning'.",
                            nameof(path));
                    }

                    segmentLength = 0;
                }
                else if (char.IsLetterOrDigit(character))
                {
                    segmentLength++;
                }
                else
                {
                    throw new ArgumentException(
                        $"Tag path '{path}' contains the invalid character '{character}'. " +
                        "Segments use letters and digits only.",
                        nameof(path));
                }
            }

            if (segmentLength == 0)
            {
                throw new ArgumentException(
                    $"Tag path '{path}' ends with a separator. Remove the trailing dot.",
                    nameof(path));
            }
        }

        private static int CountDepth(string path)
        {
            int depth = 1;
            foreach (char character in path)
            {
                if (character == Separator)
                {
                    depth++;
                }
            }

            return depth;
        }

        private readonly struct Entry
        {
            public readonly string Path;
            public readonly int ParentIndex;
            public readonly int Depth;

            public Entry(string path, int parentIndex, int depth)
            {
                Path = path;
                ParentIndex = parentIndex;
                Depth = depth;
            }
        }
    }
}
