using System;
using ToyChest.Framework.Data;
using ToyChest.Framework.Modifiers;
using ToyChest.Systems.Attributes;
using ToyChest.Systems.GameplayEffects;
using ToyChest.Systems.Tags;

namespace ToyChest.Systems.StatusEffects
{
    /// <summary>
    /// Runtime state of one active status on one object: remaining duration, stacks, and the
    /// periodic accumulator. Owns applying and revoking its definition's contributions —
    /// granted tags and attribute modifiers (registered under this instance as the modifier
    /// source, applied once per stack) — and executes the definition's Gameplay Effect
    /// sequences through the runner. Never reads the clock; the owning set injects time.
    /// </summary>
    public sealed class StatusEffectInstance
    {
        private readonly GameplayEffectRunner _runner;
        private readonly GameplayTagTable _tagTable;
        private readonly EffectTarget _owner;
        private float _periodAccumulator;
        private bool _contributionsActive;

        internal StatusEffectInstance(
            StatusEffectDefinition definition,
            EffectTarget owner,
            GameplayTagTable tagTable,
            GameplayEffectRunner runner)
        {
            Definition = definition;
            _owner = owner;
            _tagTable = tagTable;
            _runner = runner;
            Stacks = 1;
            RemainingSeconds = definition.DurationSeconds;
        }

        /// <summary>The immutable status definition.</summary>
        public StatusEffectDefinition Definition { get; }

        /// <summary>Current stack count (1 unless the IncreaseStacks rule adds more).</summary>
        public int Stacks { get; private set; }

        /// <summary>Seconds until natural expiry. Meaningless for Infinite statuses.</summary>
        public float RemainingSeconds { get; private set; }

        /// <summary>
        /// Progress toward the next periodic execution, in seconds. Authoritative runtime state
        /// the Save Framework persists so periodic timing survives a save/load exactly.
        /// </summary>
        public float PeriodAccumulator => _periodAccumulator;

        /// <summary>Whether a timed status has run out.</summary>
        public bool IsExpired =>
            Definition.DurationType == StatusDurationType.Timed && RemainingSeconds <= 0f;

        /// <summary>Applies granted tags and modifiers, then runs the on-apply effect sequence.</summary>
        internal void Activate()
        {
            AddGrantedTags();
            ApplyModifierConfigs();
            _contributionsActive = true;
            _runner.Execute(Definition.OnApplyEffects, BuildContext());
        }

        /// <summary>Resets the remaining duration.</summary>
        internal void Refresh()
        {
            RemainingSeconds = Definition.DurationSeconds;
        }

        /// <summary>
        /// Reconstructs this instance's runtime state from persisted authoritative values
        /// (rehydration for the Save Framework) and re-applies its contributions — granted tags
        /// once, attribute modifiers once per stack, matching a live status that reached this
        /// stack count. Runs no on-apply effects and publishes nothing: the on-apply gameplay
        /// already happened before the save. Called once, on a freshly created instance.
        /// </summary>
        internal void Restore(int stacks, float remainingSeconds, float periodAccumulator)
        {
            if (stacks < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(stacks), stacks, "A restored status has at least one stack.");
            }

            if (remainingSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(remainingSeconds), remainingSeconds, "Restored remaining duration must be non-negative.");
            }

            if (periodAccumulator < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(periodAccumulator), periodAccumulator, "Restored periodic accumulator must be non-negative.");
            }

            Stacks = stacks;
            RemainingSeconds = remainingSeconds;
            _periodAccumulator = periodAccumulator;

            AddGrantedTags();
            for (int stack = 0; stack < stacks; stack++)
            {
                ApplyModifierConfigs();
            }

            _contributionsActive = true;
        }

        /// <summary>
        /// Adds a stack, re-applying the modifier configs so magnitude scales, and refreshes
        /// the duration. Caller enforces the stack cap.
        /// </summary>
        internal void AddStack()
        {
            Stacks++;
            ApplyModifierConfigs();
            Refresh();
        }

        /// <summary>
        /// Advances time, executing the periodic sequence once per full period elapsed.
        /// For timed statuses, periodic time never accrues past expiry, so a status expiring
        /// mid-delta cannot over-tick. Returns the number of periodic executions.
        /// </summary>
        internal int Advance(float deltaSeconds)
        {
            int periodicExecutions = 0;

            if (Definition.PeriodSeconds > 0f && Definition.PeriodicEffects.Count > 0)
            {
                float effectiveDelta = Definition.DurationType == StatusDurationType.Timed
                    ? Math.Min(deltaSeconds, Math.Max(0f, RemainingSeconds))
                    : deltaSeconds;

                _periodAccumulator += effectiveDelta;
                while (_periodAccumulator >= Definition.PeriodSeconds)
                {
                    _periodAccumulator -= Definition.PeriodSeconds;
                    _runner.Execute(Definition.PeriodicEffects, BuildContext());
                    periodicExecutions++;
                }
            }

            if (Definition.DurationType == StatusDurationType.Timed)
            {
                RemainingSeconds -= deltaSeconds;
            }

            return periodicExecutions;
        }

        /// <summary>
        /// Revokes granted tags and modifiers and runs the on-end effect sequence.
        /// Safe to call once per activation; subsequent calls are no-ops.
        /// </summary>
        internal void Deactivate(bool runEndEffects)
        {
            if (!_contributionsActive)
            {
                return;
            }

            _contributionsActive = false;
            RemoveGrantedTags();
            RemoveModifierContributions();

            if (runEndEffects)
            {
                _runner.Execute(Definition.OnEndEffects, BuildContext());
            }
        }

        private EffectContext BuildContext()
        {
            return new EffectContext(default, _owner, _tagTable, modifierSource: this);
        }

        private void AddGrantedTags()
        {
            if (Definition.GrantedTags.Count == 0)
            {
                return;
            }

            GameplayTagContainer tags = RequireCapability(_owner.Tags, "grant tags");
            foreach (TagDefinition granted in Definition.GrantedTags)
            {
                tags.AddTag(_tagTable.GetTag(granted.TagPath));
            }
        }

        private void RemoveGrantedTags()
        {
            if (Definition.GrantedTags.Count == 0)
            {
                return;
            }

            GameplayTagContainer tags = _owner.Tags;
            foreach (TagDefinition granted in Definition.GrantedTags)
            {
                tags.RemoveTag(_tagTable.GetTag(granted.TagPath));
            }
        }

        private void ApplyModifierConfigs()
        {
            if (Definition.Modifiers.Count == 0)
            {
                return;
            }

            AttributeSet attributes = RequireCapability(_owner.Attributes, "modify attributes");
            foreach (AttributeModifierConfig config in Definition.Modifiers)
            {
                AttributeValue attribute = attributes.GetAttribute(new DefinitionId(config.AttributeId));
                if (attribute == null)
                {
                    throw new InvalidOperationException(
                        $"Status '{Definition.Id}' modifies attribute '{config.AttributeId}', which the owner lacks. " +
                        "Check the owner's object definition or the status's modifier list.");
                }

                attribute.AddModifier(new Modifier(config.Operation, config.Value, this));
            }
        }

        private void RemoveModifierContributions()
        {
            if (Definition.Modifiers.Count == 0)
            {
                return;
            }

            foreach (AttributeModifierConfig config in Definition.Modifiers)
            {
                _owner.Attributes.GetAttribute(new DefinitionId(config.AttributeId))?.RemoveModifiersFrom(this);
            }
        }

        private TCapability RequireCapability<TCapability>(TCapability capability, string action) where TCapability : class
        {
            if (capability == null)
            {
                throw new InvalidOperationException(
                    $"Status '{Definition.Id}' needs to {action}, but the owner has no {typeof(TCapability).Name}. " +
                    "Check the owner's object definition.");
            }

            return capability;
        }
    }
}
