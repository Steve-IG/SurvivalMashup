using System;
using ToyChest.Systems.Attributes;

namespace ToyChest.Systems.Resources
{
    /// <summary>
    /// Runtime state of one resource on one gameplay object: its current value and its maximum.
    /// Enforces the core invariant at all times:
    ///
    ///   0 &lt;= Current &lt;= Maximum
    ///
    /// The maximum is either literal or bound to an attribute. When bound and the attribute's
    /// value decreases below the current value, the current value is clamped immediately
    /// (approved decision), so the invariant can never be transiently violated.
    /// Raises change/transition callbacks that the owning <see cref="ResourceSet"/> forwards to
    /// the Event Bus. Regeneration is applied by the owner via <see cref="Regenerate"/>, keeping
    /// this type free of any time or engine dependency and fully testable.
    /// See Docs/Systems/RESOURCE_SYSTEM.md.
    /// </summary>
    public sealed class ResourceValue : IDisposable
    {
        private readonly AttributeValue _boundMaximum;
        private float _maximum;
        private bool _disposed;

        /// <summary>Raised on any current-value change, with (previous, new, maximum).</summary>
        public event Action<float, float, float> ValueChanged;

        /// <summary>Raised once when the current value transitions to zero.</summary>
        public event Action Depleted;

        /// <summary>Raised once when the current value transitions to the maximum.</summary>
        public event Action<float> Filled;

        /// <summary>
        /// Creates a resource with a literal maximum.
        /// </summary>
        public ResourceValue(ResourceDefinition definition)
            : this(definition, null)
        {
        }

        /// <summary>
        /// Creates a resource. When <paramref name="boundMaximum"/> is supplied, the maximum
        /// tracks that attribute's value and current is clamped immediately when it decreases.
        /// </summary>
        public ResourceValue(ResourceDefinition definition, AttributeValue boundMaximum)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));

            if (definition.HasMaximumBinding)
            {
                if (boundMaximum == null)
                {
                    throw new ArgumentNullException(
                        nameof(boundMaximum),
                        $"Resource '{definition.Id}' binds its maximum to attribute " +
                        $"'{definition.MaximumAttribute}', but no attribute value was provided.");
                }

                _boundMaximum = boundMaximum;
                _maximum = boundMaximum.CurrentValue;
                _boundMaximum.ValueChanged += OnBoundMaximumChanged;
            }
            else
            {
                _maximum = definition.LiteralMaximum;
            }

            _maximum = Math.Max(0f, _maximum);
            Current = definition.StartAtMaximum
                ? _maximum
                : ClampToRange(definition.StartingValue);
        }

        /// <summary>The immutable definition this value is an instance of.</summary>
        public ResourceDefinition Definition { get; }

        /// <summary>Current value. Always within [0, <see cref="Maximum"/>].</summary>
        public float Current { get; private set; }

        /// <summary>Current maximum, literal or from the bound attribute. Never negative.</summary>
        public float Maximum => _maximum;

        /// <summary>Whether the current value is zero.</summary>
        public bool IsDepleted => Current <= 0f;

        /// <summary>Whether the current value equals the maximum.</summary>
        public bool IsFull => Current >= _maximum;

        /// <summary>
        /// Reduces the current value by <paramref name="amount"/> (non-negative), clamped at zero.
        /// Returns the amount actually consumed.
        /// </summary>
        public float Consume(float amount)
        {
            RequireNonNegative(amount, nameof(amount));
            float previous = Current;
            SetCurrent(Current - amount);
            return previous - Current;
        }

        /// <summary>
        /// Increases the current value by <paramref name="amount"/> (non-negative), clamped at
        /// the maximum. Returns the amount actually restored.
        /// </summary>
        public float Restore(float amount)
        {
            RequireNonNegative(amount, nameof(amount));
            float previous = Current;
            SetCurrent(Current + amount);
            return Current - previous;
        }

        /// <summary>
        /// Applies continuous regeneration for <paramref name="deltaSeconds"/> using the
        /// definition's rate. No-op when the rate is zero or the resource is full.
        /// The owner supplies delta time; this type never reads the clock.
        /// </summary>
        public float Regenerate(float deltaSeconds)
        {
            RequireNonNegative(deltaSeconds, nameof(deltaSeconds));
            float rate = Definition.RegenerationPerSecond;
            if (rate <= 0f || IsFull)
            {
                return 0f;
            }

            return Restore(rate * deltaSeconds);
        }

        /// <summary>Sets the current value directly, clamped into range.</summary>
        public void SetCurrent(float value)
        {
            ApplyCurrent(ClampToRange(value));
        }

        /// <summary>
        /// Restores the current value from persisted authoritative state (rehydration for the
        /// Save Framework), clamped into range. Distinct from <see cref="Restore(float)"/>, which
        /// replenishes by an amount and fires callbacks; this sets the value directly and raises
        /// no change or transition callbacks, because reconstruction re-establishes state rather
        /// than replaying the gameplay that produced it. Restore against the already-restored
        /// maximum (reconstruct bound attributes first).
        /// </summary>
        public void RestoreCurrent(float current)
        {
            Current = ClampToRange(current);
        }

        /// <summary>Detaches the attribute-binding subscription. Safe to call more than once.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_boundMaximum != null)
            {
                _boundMaximum.ValueChanged -= OnBoundMaximumChanged;
            }
        }

        private void OnBoundMaximumChanged(float previousMax, float newMax)
        {
            _maximum = Math.Max(0f, newMax);

            // Approved behavior: a decreased maximum clamps current immediately so the
            // invariant 0 <= Current <= Maximum always holds.
            ApplyCurrent(ClampToRange(Current));
        }

        private void ApplyCurrent(float clampedValue)
        {
            float previous = Current;
            if (clampedValue == previous)
            {
                return;
            }

            Current = clampedValue;
            ValueChanged?.Invoke(previous, Current, _maximum);

            if (Current <= 0f && previous > 0f)
            {
                Depleted?.Invoke();
            }
            else if (Current >= _maximum && previous < _maximum)
            {
                Filled?.Invoke(_maximum);
            }
        }

        private float ClampToRange(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > _maximum ? _maximum : value;
        }

        private static void RequireNonNegative(float value, string parameterName)
        {
            if (value < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName, value, "Amount must be non-negative. Use the complementary operation instead.");
            }
        }
    }
}
