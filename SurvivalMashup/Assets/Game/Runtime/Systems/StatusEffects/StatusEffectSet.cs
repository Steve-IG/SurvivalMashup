using System;
using System.Collections.Generic;
using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Objects;
using ToyChest.Systems.GameplayEffects;
using ToyChest.Systems.Tags;

namespace ToyChest.Systems.StatusEffects
{
    /// <summary>
    /// The status-effect capability of one gameplay object: its active status instances, their
    /// scheduling, and their lifecycle events. Owns duration, stacking, refresh, expiration,
    /// and periodic scheduling; all gameplay mutations flow through the Gameplay Effect System
    /// and the contribution rules on <see cref="StatusEffectInstance"/>. Ticks deterministically
    /// in application order with injected time. Composed by the factory with its sibling
    /// capabilities wired at construction (Capability Independence: no runtime sibling lookup).
    /// See Docs/Systems/STATUS_EFFECT_SYSTEM.md.
    /// </summary>
    public sealed class StatusEffectSet : ITickingCapability, IDisposable
    {
        private readonly List<StatusEffectInstance> _instances = new List<StatusEffectInstance>();
        private readonly Dictionary<DefinitionId, StatusEffectInstance> _byId =
            new Dictionary<DefinitionId, StatusEffectInstance>();

        private readonly List<StatusEffectInstance> _expiredScratch = new List<StatusEffectInstance>();
        private readonly GameplayObjectId _owner;
        private readonly IEventBus _eventBus;
        private readonly GameplayTagTable _tagTable;
        private readonly GameplayEffectRunner _runner;
        private readonly EffectTarget _ownerTarget;

        /// <summary>
        /// Creates the capability. Sibling capabilities the statuses mutate are wired here,
        /// at composition time, per the Capability Independence guideline.
        /// </summary>
        /// <param name="owner">Identity used in published events. May be default in isolated tests.</param>
        /// <param name="eventBus">Publishes status events; null runs silently.</param>
        /// <param name="tagTable">Resolves authored tag paths.</param>
        /// <param name="runner">Executes the definitions' effect sequences.</param>
        /// <param name="ownerTarget">The owner's capabilities that statuses contribute to.</param>
        public StatusEffectSet(
            GameplayObjectId owner,
            IEventBus eventBus,
            GameplayTagTable tagTable,
            GameplayEffectRunner runner,
            EffectTarget ownerTarget)
        {
            _owner = owner;
            _eventBus = eventBus;
            _tagTable = tagTable ?? throw new ArgumentNullException(nameof(tagTable));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            _ownerTarget = ownerTarget;
        }

        /// <summary>Number of active status instances.</summary>
        public int Count => _instances.Count;

        /// <summary>
        /// The active statuses in deterministic application order, for save capture and tooling.
        /// Each <see cref="StatusEffectInstance"/> exposes its definition id and authoritative
        /// stacks, remaining duration, and periodic accumulator (Engine Principle 25).
        /// </summary>
        public IReadOnlyList<StatusEffectInstance> ActiveStatuses => _instances;

        /// <summary>Whether a status with this definition id is active.</summary>
        public bool Has(DefinitionId status) => _byId.ContainsKey(status);

        /// <summary>Current stack count of an active status, or zero when absent.</summary>
        public int GetStacks(DefinitionId status)
        {
            return _byId.TryGetValue(status, out StatusEffectInstance instance) ? instance.Stacks : 0;
        }

        /// <summary>
        /// Applies a status. A first application activates contributions, runs on-apply
        /// effects, and publishes <see cref="StatusApplied"/>. Re-application follows the
        /// definition's stacking rule. Returns the active instance.
        /// </summary>
        public StatusEffectInstance Apply(StatusEffectDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            DefinitionId id = definition.Id;
            if (_byId.TryGetValue(id, out StatusEffectInstance existing))
            {
                return ApplyStackingRule(definition, existing);
            }

            var instance = new StatusEffectInstance(definition, _ownerTarget, _tagTable, _runner);
            _instances.Add(instance);
            _byId.Add(id, instance);
            instance.Activate();
            _eventBus?.Publish(new StatusApplied(_owner, id));
            return instance;
        }

        /// <summary>
        /// Reconstructs an active status from persisted authoritative state (rehydration for the
        /// Save Framework): re-applies the status's contributions at the saved stack count and
        /// restores its remaining duration and periodic accumulator, running no on-apply effects
        /// and publishing no events. Use only during load, on an object that does not already
        /// carry the status. Returns the restored instance.
        /// </summary>
        public StatusEffectInstance Restore(
            StatusEffectDefinition definition, int stacks, float remainingSeconds, float periodAccumulator)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            DefinitionId id = definition.Id;
            if (_byId.ContainsKey(id))
            {
                throw new InvalidOperationException(
                    $"Cannot restore status '{id}': it is already active on this object. " +
                    "Restore reconstructs a status exactly once during load.");
            }

            var instance = new StatusEffectInstance(definition, _ownerTarget, _tagTable, _runner);
            _instances.Add(instance);
            _byId.Add(id, instance);
            instance.Restore(stacks, remainingSeconds, periodAccumulator);
            return instance;
        }

        /// <summary>
        /// Explicitly removes an active status (dispel, cleanse): revokes contributions, runs
        /// on-end effects, publishes <see cref="StatusRemoved"/>. Returns false when absent.
        /// </summary>
        public bool Remove(DefinitionId status)
        {
            if (!_byId.TryGetValue(status, out StatusEffectInstance instance))
            {
                return false;
            }

            RemoveInstance(instance);
            instance.Deactivate(runEndEffects: true);
            _eventBus?.Publish(new StatusRemoved(_owner, status));
            return true;
        }

        /// <summary>
        /// Advances every active status in application order: periodic sequences execute
        /// (publishing <see cref="StatusPeriodicTick"/> per execution), then expired statuses
        /// are deactivated and publish <see cref="StatusExpired"/>. Deterministic for a given
        /// sequence of deltas.
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "Delta time must be non-negative.");
            }

            for (int i = 0; i < _instances.Count; i++)
            {
                StatusEffectInstance instance = _instances[i];
                int periodicExecutions = instance.Advance(deltaSeconds);
                for (int tick = 0; tick < periodicExecutions; tick++)
                {
                    _eventBus?.Publish(new StatusPeriodicTick(_owner, instance.Definition.Id));
                }

                if (instance.IsExpired)
                {
                    _expiredScratch.Add(instance);
                }
            }

            for (int i = 0; i < _expiredScratch.Count; i++)
            {
                StatusEffectInstance expired = _expiredScratch[i];
                RemoveInstance(expired);
                expired.Deactivate(runEndEffects: true);
                _eventBus?.Publish(new StatusExpired(_owner, expired.Definition.Id));
            }

            _expiredScratch.Clear();
        }

        /// <summary>
        /// Teardown: revokes every active status's contributions without running on-end
        /// effects or publishing events — the object's Destroyed event already announces
        /// the teardown, and end-of-status gameplay must not fire during destruction.
        /// </summary>
        public void Dispose()
        {
            for (int i = 0; i < _instances.Count; i++)
            {
                _instances[i].Deactivate(runEndEffects: false);
            }

            _instances.Clear();
            _byId.Clear();
        }

        private StatusEffectInstance ApplyStackingRule(StatusEffectDefinition definition, StatusEffectInstance existing)
        {
            switch (definition.StackingRule)
            {
                case StatusStackingRule.IgnoreDuplicate:
                    return existing;

                case StatusStackingRule.RefreshDuration:
                    existing.Refresh();
                    _eventBus?.Publish(new StatusRefreshed(_owner, definition.Id));
                    return existing;

                case StatusStackingRule.ReplaceExisting:
                    RemoveInstance(existing);
                    existing.Deactivate(runEndEffects: true);
                    _eventBus?.Publish(new StatusRemoved(_owner, definition.Id));
                    return Apply(definition);

                case StatusStackingRule.IncreaseStacks:
                    if (existing.Stacks < definition.MaximumStacks)
                    {
                        existing.AddStack();
                        _eventBus?.Publish(new StatusStackIncreased(_owner, definition.Id, existing.Stacks));
                    }
                    else
                    {
                        existing.Refresh();
                        _eventBus?.Publish(new StatusRefreshed(_owner, definition.Id));
                    }

                    return existing;

                default:
                    throw new InvalidOperationException(
                        $"Status '{definition.Id}' uses unsupported stacking rule '{definition.StackingRule}'.");
            }
        }

        private void RemoveInstance(StatusEffectInstance instance)
        {
            _instances.Remove(instance);
            _byId.Remove(instance.Definition.Id);
        }
    }
}
