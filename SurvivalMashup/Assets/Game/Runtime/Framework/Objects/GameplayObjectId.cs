using System;

namespace ToyChest.Framework.Objects
{
    /// <summary>
    /// Stable persistent identity of one Gameplay Object instance, distinct from
    /// <see cref="ToyChest.Framework.Data.DefinitionId"/>, which identifies the shared
    /// immutable definition. Saves and future networking reference objects through this id.
    /// Internally GUID-backed; the GUID is an implementation detail and is not part of the
    /// public architecture — external code sees only equality, validity, and the string form.
    /// </summary>
    public readonly struct GameplayObjectId : IEquatable<GameplayObjectId>
    {
        private readonly Guid _value;

        private GameplayObjectId(Guid value)
        {
            _value = value;
        }

        /// <summary>Creates a new unique object id.</summary>
        public static GameplayObjectId New()
        {
            return new GameplayObjectId(Guid.NewGuid());
        }

        /// <summary>
        /// Parses the string form produced by <see cref="ToString"/>, for save restoration.
        /// </summary>
        public static bool TryParse(string value, out GameplayObjectId id)
        {
            if (Guid.TryParseExact(value, "N", out Guid parsed) && parsed != Guid.Empty)
            {
                id = new GameplayObjectId(parsed);
                return true;
            }

            id = default;
            return false;
        }

        /// <summary>False for default-constructed ids, which identify nothing.</summary>
        public bool IsValid => _value != Guid.Empty;

        /// <inheritdoc />
        public bool Equals(GameplayObjectId other)
        {
            return _value.Equals(other._value);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is GameplayObjectId other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        /// <summary>The stable string form used at serialization boundaries.</summary>
        public override string ToString()
        {
            return _value.ToString("N");
        }

        public static bool operator ==(GameplayObjectId left, GameplayObjectId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GameplayObjectId left, GameplayObjectId right)
        {
            return !left.Equals(right);
        }
    }
}
