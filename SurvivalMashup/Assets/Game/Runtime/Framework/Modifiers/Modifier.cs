using System;

namespace ToyChest.Framework.Modifiers
{
    /// <summary>
    /// One immutable contribution to a computed value, supplied by a source such as
    /// equipment, a status effect, or a buff. Modifiers describe a change; they never
    /// apply themselves. The <see cref="ModifierStack"/> combines them deterministically.
    /// A modifier carries an opaque source key so the exact contributions from one source
    /// can be removed later without disturbing others.
    /// </summary>
    public readonly struct Modifier : IEquatable<Modifier>
    {
        /// <summary>How this modifier combines with the value.</summary>
        public readonly ModifierOperation Operation;

        /// <summary>
        /// The magnitude, interpreted per <see cref="Operation"/>:
        /// Flat is an absolute amount; AdditivePercent and MultiplicativePercent are
        /// fractions (0.2 means 20%); Override is the replacement value.
        /// </summary>
        public readonly float Value;

        /// <summary>
        /// Tie-break priority. For overrides, the highest priority wins; among equal
        /// priorities the highest override value wins, keeping evaluation deterministic.
        /// Ignored by Flat, Additive, and Multiplicative operations, which are order-free.
        /// </summary>
        public readonly int Priority;

        /// <summary>
        /// Opaque identity of the contributing source. Removing a source removes exactly
        /// the modifiers registered under this key. Never null in practice.
        /// </summary>
        public readonly object Source;

        /// <summary>Creates a modifier contribution.</summary>
        public Modifier(ModifierOperation operation, float value, object source, int priority = 0)
        {
            Operation = operation;
            Value = value;
            Source = source;
            Priority = priority;
        }

        /// <summary>Convenience constructor for a flat additive modifier.</summary>
        public static Modifier Flat(float value, object source) =>
            new Modifier(ModifierOperation.Flat, value, source);

        /// <summary>Convenience constructor for an additive-percentage modifier (0.2 = +20%).</summary>
        public static Modifier AdditivePercent(float fraction, object source) =>
            new Modifier(ModifierOperation.AdditivePercent, fraction, source);

        /// <summary>Convenience constructor for a compounding multiplicative modifier (0.5 = x1.5).</summary>
        public static Modifier MultiplicativePercent(float fraction, object source) =>
            new Modifier(ModifierOperation.MultiplicativePercent, fraction, source);

        /// <summary>Convenience constructor for an override modifier.</summary>
        public static Modifier Override(float value, object source, int priority = 0) =>
            new Modifier(ModifierOperation.Override, value, source, priority);

        /// <inheritdoc />
        public bool Equals(Modifier other)
        {
            return Operation == other.Operation
                && Value.Equals(other.Value)
                && Priority == other.Priority
                && Equals(Source, other.Source);
        }

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is Modifier other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(Operation, Value, Priority, Source);
    }
}
