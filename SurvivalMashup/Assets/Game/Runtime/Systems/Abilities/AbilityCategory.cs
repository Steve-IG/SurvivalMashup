using System;

namespace ToyChest.Systems.Abilities
{
    /// <summary>
    /// Lightweight string-backed identity of an ability's organizational category
    /// (Combat, Movement, Utility, Companion, Ultimate, Interaction). Data-driven: a new
    /// category is an authored string, not code — deliberately not an enum (categories are
    /// content) and not a Gameplay Tag (categories are organizational and never queried by
    /// gameplay logic). Comparison is ordinal. Wrapping the raw string keeps category-aware
    /// call sites (UI grouping, AI filtering, loadout rules) type-stable as they arrive.
    /// </summary>
    public readonly struct AbilityCategory : IEquatable<AbilityCategory>
    {
        private readonly string _value;

        /// <summary>
        /// Wraps <paramref name="value"/> as a category.
        /// </summary>
        /// <exception cref="ArgumentException">The value is null, empty, or whitespace.</exception>
        public AbilityCategory(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "An AbilityCategory requires a non-empty value. Use AbilityCategory.None for uncategorized abilities.",
                    nameof(value));
            }

            _value = value;
        }

        /// <summary>The category of abilities with no authored category.</summary>
        public static AbilityCategory None => default;

        /// <summary>The underlying category name, or null for <see cref="None"/>.</summary>
        public string Value => _value;

        /// <summary>False for <see cref="None"/>, which names no category.</summary>
        public bool IsValid => _value != null;

        /// <summary>Maps authored text to a category, treating blank authoring as <see cref="None"/>.</summary>
        internal static AbilityCategory FromAuthored(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? None : new AbilityCategory(value);
        }

        /// <inheritdoc />
        public bool Equals(AbilityCategory other)
        {
            return string.Equals(_value, other._value, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is AbilityCategory other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return _value ?? "<none>";
        }

        public static bool operator ==(AbilityCategory left, AbilityCategory right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AbilityCategory left, AbilityCategory right)
        {
            return !left.Equals(right);
        }
    }
}
