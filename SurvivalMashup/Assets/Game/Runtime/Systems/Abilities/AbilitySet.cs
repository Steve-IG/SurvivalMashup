using System;
using System.Collections.Generic;
using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Objects;
using ToyChest.Systems.GameplayEffects;
using ToyChest.Systems.Resources;
using ToyChest.Systems.Tags;

namespace ToyChest.Systems.Abilities
{
    /// <summary>
    /// The ability capability of one gameplay object: its granted abilities, their cooldowns,
    /// and the activation pipeline. Pure orchestration — validation, target resolution, cost
    /// consumption, cooldown bookkeeping, effect-sequence dispatch, and Ability events. All
    /// gameplay mutations flow through the Gameplay Effect System (effects) and the Resource
    /// System (costs); no gameplay logic lives here. Ticks deterministically in grant order
    /// with injected time. Composed by the factory with its sibling capabilities wired at
    /// construction (Capability Independence: no runtime sibling lookup).
    /// See Docs/Systems/ABILITY_SYSTEM.md.
    /// </summary>
    public sealed class AbilitySet : ITickingCapability
    {
        private readonly List<AbilityInstance> _instances = new List<AbilityInstance>();
        private readonly Dictionary<DefinitionId, AbilityInstance> _byId =
            new Dictionary<DefinitionId, AbilityInstance>();

        private readonly GameplayObjectId _owner;
        private readonly IEventBus _eventBus;
        private readonly GameplayTagTable _tagTable;
        private readonly GameplayEffectRunner _runner;
        private readonly EffectTarget _ownerTarget;

        /// <summary>
        /// Creates the capability. Sibling capabilities the abilities read and their effects
        /// mutate are wired here, at composition time, per the Capability Independence guideline.
        /// </summary>
        /// <param name="owner">Identity used in published events. May be default in isolated tests.</param>
        /// <param name="eventBus">Publishes ability events; null runs silently.</param>
        /// <param name="tagTable">Resolves authored tag paths.</param>
        /// <param name="runner">Executes the definitions' effect sequences.</param>
        /// <param name="ownerTarget">The owner's capabilities: cost source, tag-gate source, and Self target.</param>
        public AbilitySet(
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

        /// <summary>Number of granted abilities.</summary>
        public int Count => _instances.Count;

        /// <summary>
        /// The granted abilities in deterministic grant order, for save capture and tooling.
        /// Each <see cref="AbilityInstance"/> exposes its definition id and authoritative
        /// remaining cooldown; "is on cooldown" is derived (Engine Principle 25).
        /// </summary>
        public IReadOnlyList<AbilityInstance> Abilities => _instances;

        /// <summary>Whether an ability with this definition id is granted.</summary>
        public bool Has(DefinitionId ability) => _byId.ContainsKey(ability);

        /// <summary>The granted ability's runtime instance, or null when not granted.</summary>
        public AbilityInstance GetAbility(DefinitionId ability)
        {
            return _byId.TryGetValue(ability, out AbilityInstance instance) ? instance : null;
        }

        /// <summary>
        /// Grants an ability and publishes <see cref="AbilityGranted"/>. Each ability is
        /// granted at most once per object; loadout and charge models are future extensions.
        /// </summary>
        public AbilityInstance Grant(AbilityDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            DefinitionId id = definition.Id;
            if (_byId.ContainsKey(id))
            {
                throw new InvalidOperationException(
                    $"Ability '{id}' is already granted to this object. Each ability is granted once per object.");
            }

            var instance = new AbilityInstance(definition);
            _instances.Add(instance);
            _byId.Add(id, instance);
            _eventBus?.Publish(new AbilityGranted(_owner, id));
            return instance;
        }

        /// <summary>
        /// Revokes a granted ability (unlearn, equipment removed) and publishes
        /// <see cref="AbilityRevoked"/>. Returns false when the ability is not granted.
        /// </summary>
        public bool Revoke(DefinitionId ability)
        {
            if (!_byId.TryGetValue(ability, out AbilityInstance instance))
            {
                return false;
            }

            _instances.Remove(instance);
            _byId.Remove(ability);
            _eventBus?.Publish(new AbilityRevoked(_owner, ability));
            return true;
        }

        /// <summary>
        /// Restores a granted ability's remaining cooldown from persisted authoritative state
        /// (rehydration for the Save Framework). Sets state directly; publishes no events, since
        /// reconstruction replays no gameplay. Throws when the ability is not granted — the
        /// caller must grant abilities (through the object's definition) before restoring them.
        /// </summary>
        public void RestoreCooldown(DefinitionId ability, float remainingSeconds)
        {
            if (!_byId.TryGetValue(ability, out AbilityInstance instance))
            {
                throw new InvalidOperationException(
                    $"Cannot restore cooldown for ability '{ability}': it is not granted to this object. " +
                    "Reconstruct the object's granted abilities before restoring their runtime state.");
            }

            instance.RestoreCooldown(remainingSeconds);
        }

        /// <summary>
        /// Validates a Self-mode activation without committing anything: no costs, no cooldown,
        /// no effects, no events. For UI availability and AI evaluation.
        /// </summary>
        public AbilityActivationResult CanActivate(DefinitionId ability)
        {
            return Validate(ability, default, hasExplicitTarget: false, out _);
        }

        /// <summary>
        /// Validates a Provided-mode activation against <paramref name="target"/> without
        /// committing anything.
        /// </summary>
        public AbilityActivationResult CanActivate(DefinitionId ability, in EffectTarget target)
        {
            return Validate(ability, in target, hasExplicitTarget: true, out _);
        }

        /// <summary>Activates a Self-mode ability. Effects execute against the owner.</summary>
        public AbilityActivationResult TryActivate(DefinitionId ability)
        {
            return Activate(ability, default, hasExplicitTarget: false);
        }

        /// <summary>
        /// Activates a Provided-mode ability against the target the activator selected.
        /// Targeting stays independent of effects: the caller (input, AI, interaction) chooses
        /// the target; the ability validates and dispatches.
        /// </summary>
        public AbilityActivationResult TryActivate(DefinitionId ability, in EffectTarget target)
        {
            return Activate(ability, in target, hasExplicitTarget: true);
        }

        /// <summary>
        /// Advances every cooldown in grant order, publishing <see cref="AbilityCooldownEnded"/>
        /// exactly once per ready transition. Deterministic for a given sequence of deltas.
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "Delta time must be non-negative.");
            }

            for (int i = 0; i < _instances.Count; i++)
            {
                AbilityInstance instance = _instances[i];
                if (instance.Advance(deltaSeconds))
                {
                    _eventBus?.Publish(new AbilityCooldownEnded(_owner, instance.Definition.Id));
                }
            }
        }

        private AbilityActivationResult Activate(DefinitionId ability, in EffectTarget explicitTarget, bool hasExplicitTarget)
        {
            AbilityActivationResult result = Validate(ability, in explicitTarget, hasExplicitTarget, out AbilityInstance instance);
            if (result != AbilityActivationResult.Activated)
            {
                _eventBus?.Publish(new AbilityActivationFailed(_owner, ability, result));
                return result;
            }

            AbilityDefinition definition = instance.Definition;

            // Commit, in the documented order: costs, cooldown, activation fact, effects.
            IReadOnlyList<ResourceCostConfig> costs = definition.Costs;
            for (int i = 0; i < costs.Count; i++)
            {
                _ownerTarget.Resources.GetResource(new DefinitionId(costs[i].ResourceId)).Consume(costs[i].Amount);
            }

            if (definition.CooldownSeconds > 0f)
            {
                instance.StartCooldown();
                _eventBus?.Publish(new AbilityCooldownStarted(_owner, ability, definition.CooldownSeconds));
            }

            _eventBus?.Publish(new AbilityActivated(_owner, ability));

            EffectTarget target = definition.TargetMode == AbilityTargetMode.Self ? _ownerTarget : explicitTarget;
            _runner.Execute(definition.Effects, new EffectContext(_ownerTarget, target, _tagTable));
            return AbilityActivationResult.Activated;
        }

        private AbilityActivationResult Validate(
            DefinitionId ability, in EffectTarget explicitTarget, bool hasExplicitTarget, out AbilityInstance instance)
        {
            if (!_byId.TryGetValue(ability, out instance))
            {
                return AbilityActivationResult.NotGranted;
            }

            AbilityDefinition definition = instance.Definition;

            if (definition.TargetMode == AbilityTargetMode.Self && hasExplicitTarget)
            {
                return AbilityActivationResult.InvalidTarget;
            }

            if (definition.TargetMode == AbilityTargetMode.Provided && (!hasExplicitTarget || IsEmpty(in explicitTarget)))
            {
                return AbilityActivationResult.InvalidTarget;
            }

            AbilityActivationResult tagGate = ValidateOwnerTags(definition);
            if (tagGate != AbilityActivationResult.Activated)
            {
                return tagGate;
            }

            if (instance.IsOnCooldown)
            {
                return AbilityActivationResult.OnCooldown;
            }

            return ValidateCosts(definition);
        }

        private AbilityActivationResult ValidateOwnerTags(AbilityDefinition definition)
        {
            GameplayTagContainer tags = _ownerTarget.Tags;

            IReadOnlyList<TagDefinition> required = definition.RequiredOwnerTags;
            for (int i = 0; i < required.Count; i++)
            {
                if (tags == null || !tags.HasTag(_tagTable.GetTag(required[i].TagPath)))
                {
                    return AbilityActivationResult.MissingRequiredTag;
                }
            }

            IReadOnlyList<TagDefinition> blocking = definition.BlockedByOwnerTags;
            for (int i = 0; i < blocking.Count; i++)
            {
                if (tags != null && tags.HasTag(_tagTable.GetTag(blocking[i].TagPath)))
                {
                    return AbilityActivationResult.BlockedByTag;
                }
            }

            return AbilityActivationResult.Activated;
        }

        private AbilityActivationResult ValidateCosts(AbilityDefinition definition)
        {
            IReadOnlyList<ResourceCostConfig> costs = definition.Costs;
            for (int i = 0; i < costs.Count; i++)
            {
                // An owner lacking the resource can never pay it: CannotAfford, not an error,
                // so any actor type can be asked about any ability (UI greying, AI scoring).
                ResourceValue resource = _ownerTarget.Resources?.GetResource(new DefinitionId(costs[i].ResourceId));
                if (resource == null || resource.Current < costs[i].Amount)
                {
                    return AbilityActivationResult.CannotAfford;
                }
            }

            return AbilityActivationResult.Activated;
        }

        private static bool IsEmpty(in EffectTarget target)
        {
            return target.Resources == null && target.Attributes == null && target.Tags == null;
        }
    }
}
