using System.Collections.Generic;

namespace ToyChest.Framework.Modifiers
{
    /// <summary>
    /// Combines modifier contributions into a final value using the single fixed order shared
    /// by the Attribute and Resource systems:
    ///
    ///   Base → Flat → Additive % → Multiplicative % → Override → Clamp
    ///
    /// This is the one reusable modifier implementation in the engine; no system reimplements
    /// modifier math. The stack is deterministic: given the same base and the same set of
    /// modifiers, it always produces the same result regardless of insertion order.
    /// Clamping is applied by the caller through <see cref="Evaluate"/>'s min and max, since
    /// only the owning system knows its bounds. Plain C#, fully testable outside Unity.
    /// See Docs/Systems/ATTRIBUTE_SYSTEM.md and Docs/Systems/RESOURCE_SYSTEM.md.
    /// </summary>
    public sealed class ModifierStack
    {
        private readonly List<Modifier> _modifiers = new List<Modifier>();

        /// <summary>Number of active modifier contributions.</summary>
        public int Count => _modifiers.Count;

        /// <summary>Adds a modifier contribution.</summary>
        public void Add(Modifier modifier)
        {
            _modifiers.Add(modifier);
        }

        /// <summary>
        /// Removes every modifier contributed by <paramref name="source"/>.
        /// Returns the number removed. Removing an unknown source is a no-op.
        /// </summary>
        public int RemoveSource(object source)
        {
            return _modifiers.RemoveAll(modifier => Equals(modifier.Source, source));
        }

        /// <summary>Removes all modifiers, leaving the base value unmodified.</summary>
        public void Clear()
        {
            _modifiers.Clear();
        }

        /// <summary>Whether any modifier from <paramref name="source"/> is present.</summary>
        public bool ContainsSource(object source)
        {
            for (int i = 0; i < _modifiers.Count; i++)
            {
                if (Equals(_modifiers[i].Source, source))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Computes the final value from <paramref name="baseValue"/> and the current modifiers,
        /// then clamps the result to [<paramref name="min"/>, <paramref name="max"/>].
        /// Evaluation order is fixed and independent of the order modifiers were added.
        /// </summary>
        public float Evaluate(float baseValue, float min, float max)
        {
            float flatSum = 0f;
            float additiveFraction = 0f;
            float multiplicativeFactor = 1f;

            bool hasOverride = false;
            float overrideValue = 0f;
            int overridePriority = int.MinValue;

            for (int i = 0; i < _modifiers.Count; i++)
            {
                Modifier modifier = _modifiers[i];
                switch (modifier.Operation)
                {
                    case ModifierOperation.Flat:
                        flatSum += modifier.Value;
                        break;
                    case ModifierOperation.AdditivePercent:
                        additiveFraction += modifier.Value;
                        break;
                    case ModifierOperation.MultiplicativePercent:
                        multiplicativeFactor *= 1f + modifier.Value;
                        break;
                    case ModifierOperation.Override:
                        if (!hasOverride
                            || modifier.Priority > overridePriority
                            || (modifier.Priority == overridePriority && modifier.Value > overrideValue))
                        {
                            hasOverride = true;
                            overrideValue = modifier.Value;
                            overridePriority = modifier.Priority;
                        }

                        break;
                }
            }

            float result = (baseValue + flatSum) * (1f + additiveFraction) * multiplicativeFactor;
            if (hasOverride)
            {
                result = overrideValue;
            }

            return Clamp(result, min, max);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
