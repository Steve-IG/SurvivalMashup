using System;

namespace ToyChest.Systems.Tags
{
    /// <summary>
    /// Runtime handle for one interned hierarchical tag.
    /// A lightweight index into the <see cref="GameplayTagTable"/> that interned it —
    /// never a string. Paths appear only at authoring, serialization, and debugging
    /// boundaries. Obtain instances from the table; a default-constructed tag is invalid.
    /// </summary>
    public readonly struct GameplayTag : IEquatable<GameplayTag>
    {
        // Stored as index + 1 so default(GameplayTag) is invalid rather than tag zero.
        private readonly int _indexPlusOne;

        internal GameplayTag(int index)
        {
            _indexPlusOne = index + 1;
        }

        /// <summary>Interned index within the owning table. Valid only for valid tags.</summary>
        internal int Index => _indexPlusOne - 1;

        /// <summary>False for default-constructed handles, which name no tag.</summary>
        public bool IsValid => _indexPlusOne > 0;

        /// <inheritdoc />
        public bool Equals(GameplayTag other)
        {
            return _indexPlusOne == other._indexPlusOne;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is GameplayTag other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return _indexPlusOne;
        }

        public static bool operator ==(GameplayTag left, GameplayTag right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GameplayTag left, GameplayTag right)
        {
            return !left.Equals(right);
        }
    }
}
