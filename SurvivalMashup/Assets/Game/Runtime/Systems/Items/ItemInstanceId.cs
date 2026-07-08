using System;

namespace ToyChest.Systems.Items
{
    /// <summary>
    /// Stable persistent identity of one Item Instance, distinct from
    /// <see cref="ToyChest.Framework.Data.DefinitionId"/>, which identifies the shared
    /// immutable item definition. Saves, trading, and future networking reference item
    /// instances through this id. Internally GUID-backed; the GUID is an implementation
    /// detail — external code sees only equality, validity, and the string form.
    /// </summary>
    public readonly struct ItemInstanceId : IEquatable<ItemInstanceId>
    {
        private readonly Guid _value;

        private ItemInstanceId(Guid value)
        {
            _value = value;
        }

        /// <summary>Creates a new unique item instance id.</summary>
        public static ItemInstanceId New()
        {
            return new ItemInstanceId(Guid.NewGuid());
        }

        /// <summary>
        /// Parses the string form produced by <see cref="ToString"/>, for save restoration.
        /// </summary>
        public static bool TryParse(string value, out ItemInstanceId id)
        {
            if (Guid.TryParseExact(value, "N", out Guid parsed) && parsed != Guid.Empty)
            {
                id = new ItemInstanceId(parsed);
                return true;
            }

            id = default;
            return false;
        }

        /// <summary>False for default-constructed ids, which identify nothing.</summary>
        public bool IsValid => _value != Guid.Empty;

        /// <inheritdoc />
        public bool Equals(ItemInstanceId other)
        {
            return _value.Equals(other._value);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is ItemInstanceId other && Equals(other);
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

        public static bool operator ==(ItemInstanceId left, ItemInstanceId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ItemInstanceId left, ItemInstanceId right)
        {
            return !left.Equals(right);
        }
    }
}
