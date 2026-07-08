using System;
using ToyChest.Framework.Modifiers;

namespace ToyChest.Systems.Attributes
{
    /// <summary>
    /// Runtime state of one attribute on one gameplay object: its definition, its modifier
    /// stack, and its current computed value. Recomputes lazily-eagerly — the value is
    /// recalculated the moment modifiers change, so <see cref="CurrentValue"/> is always live.
    /// Modifier math is delegated to the shared <see cref="ModifierStack"/>; this type adds no
    /// parallel modifier logic. Raises <see cref="ValueChanged"/> only on an actual change,
    /// which the owning <see cref="AttributeSet"/> forwards to the Event Bus.
    /// </summary>
    public sealed class AttributeValue
    {
        private readonly ModifierStack _modifiers = new ModifierStack();

        /// <summary>
        /// Raised when the computed value changes, with (previous, new). Plain C# callback;
        /// Event Bus publication is owned by <see cref="AttributeSet"/>.
        /// </summary>
        public event Action<float, float> ValueChanged;

        /// <summary>Creates a runtime value seeded from its definition's base value.</summary>
        public AttributeValue(AttributeDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            CurrentValue = Compute();
        }

        /// <summary>The immutable definition this value is an instance of.</summary>
        public AttributeDefinition Definition { get; }

        /// <summary>The current computed value after all modifiers and clamping.</summary>
        public float CurrentValue { get; private set; }

        /// <summary>Adds a modifier and recomputes, raising <see cref="ValueChanged"/> if it moved.</summary>
        public void AddModifier(Modifier modifier)
        {
            _modifiers.Add(modifier);
            Recompute();
        }

        /// <summary>
        /// Removes every modifier from <paramref name="source"/> and recomputes.
        /// Returns the number removed.
        /// </summary>
        public int RemoveModifiersFrom(object source)
        {
            int removed = _modifiers.RemoveSource(source);
            if (removed > 0)
            {
                Recompute();
            }

            return removed;
        }

        /// <summary>Whether any modifier from <paramref name="source"/> is applied.</summary>
        public bool HasModifierFrom(object source) => _modifiers.ContainsSource(source);

        private void Recompute()
        {
            float previous = CurrentValue;
            float next = Compute();
            if (next != previous)
            {
                CurrentValue = next;
                ValueChanged?.Invoke(previous, next);
            }
        }

        private float Compute()
        {
            return _modifiers.Evaluate(Definition.BaseValue, Definition.MinimumValue, Definition.MaximumValue);
        }
    }
}
