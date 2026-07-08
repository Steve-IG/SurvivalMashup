using System;

namespace ToyChest.Framework.Data
{
    /// <summary>
    /// Stable identifier addressing an immutable gameplay definition.
    /// Saves, networking, and tooling reference definitions through these ids, so an id
    /// never changes after content ships. Comparison is ordinal; the hash is cached.
    /// Convention: lowercase dot-separated family namespacing ("ability.fireball");
    /// Tag Definitions use their tag path directly. See Docs/Architecture/DATA_REGISTRY.md.
    /// </summary>
    public readonly struct DefinitionId : IEquatable<DefinitionId>
    {
        private readonly string _value;
        private readonly int _hashCode;

        /// <summary>
        /// Wraps <paramref name="value"/> as a definition id.
        /// </summary>
        /// <exception cref="ArgumentException">The value is null, empty, or whitespace.</exception>
        public DefinitionId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A DefinitionId requires a non-empty value. Check the definition asset's id field.",
                    nameof(value));
            }

            _value = value;
            _hashCode = StringComparer.Ordinal.GetHashCode(value);
        }

        /// <summary>The underlying identifier string.</summary>
        public string Value => _value;

        /// <summary>False for default-constructed ids, which identify nothing.</summary>
        public bool IsValid => _value != null;

        /// <inheritdoc />
        public bool Equals(DefinitionId other)
        {
            return string.Equals(_value, other._value, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is DefinitionId other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return _hashCode;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return _value ?? "<invalid>";
        }

        public static bool operator ==(DefinitionId left, DefinitionId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DefinitionId left, DefinitionId right)
        {
            return !left.Equals(right);
        }
    }
}
