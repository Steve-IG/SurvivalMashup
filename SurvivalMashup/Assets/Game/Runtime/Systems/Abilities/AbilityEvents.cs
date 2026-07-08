using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Objects;

namespace ToyChest.Systems.Abilities
{
    /// <summary>Fact: an ability was granted to an object. Owned solely by the Ability System.</summary>
    [EventCategory(EventCategories.Ability)]
    public readonly struct AbilityGranted : IGameplayEvent
    {
        public readonly GameplayObjectId Owner;
        public readonly DefinitionId Ability;

        public AbilityGranted(GameplayObjectId owner, DefinitionId ability)
        {
            Owner = owner;
            Ability = ability;
        }

        /// <inheritdoc />
        public override string ToString() => $"AbilityGranted({Ability} to {Owner})";
    }

    /// <summary>Fact: an ability was revoked from an object.</summary>
    [EventCategory(EventCategories.Ability)]
    public readonly struct AbilityRevoked : IGameplayEvent
    {
        public readonly GameplayObjectId Owner;
        public readonly DefinitionId Ability;

        public AbilityRevoked(GameplayObjectId owner, DefinitionId ability)
        {
            Owner = owner;
            Ability = ability;
        }

        /// <inheritdoc />
        public override string ToString() => $"AbilityRevoked({Ability} from {Owner})";
    }

    /// <summary>
    /// Fact: an activation was committed — validation passed, costs were consumed, and the
    /// cooldown started. The effect consequences dispatch depth-first after this event.
    /// </summary>
    [EventCategory(EventCategories.Ability)]
    public readonly struct AbilityActivated : IGameplayEvent
    {
        public readonly GameplayObjectId Owner;
        public readonly DefinitionId Ability;

        public AbilityActivated(GameplayObjectId owner, DefinitionId ability)
        {
            Owner = owner;
            Ability = ability;
        }

        /// <inheritdoc />
        public override string ToString() => $"AbilityActivated({Ability} by {Owner})";
    }

    /// <summary>Fact: an activation attempt was rejected, with the first failing check as reason.</summary>
    [EventCategory(EventCategories.Ability)]
    public readonly struct AbilityActivationFailed : IGameplayEvent
    {
        public readonly GameplayObjectId Owner;
        public readonly DefinitionId Ability;
        public readonly AbilityActivationResult Reason;

        public AbilityActivationFailed(GameplayObjectId owner, DefinitionId ability, AbilityActivationResult reason)
        {
            Owner = owner;
            Ability = ability;
            Reason = reason;
        }

        /// <inheritdoc />
        public override string ToString() => $"AbilityActivationFailed({Ability} by {Owner}: {Reason})";
    }

    /// <summary>Fact: an ability began cooling down.</summary>
    [EventCategory(EventCategories.Ability)]
    public readonly struct AbilityCooldownStarted : IGameplayEvent
    {
        public readonly GameplayObjectId Owner;
        public readonly DefinitionId Ability;
        public readonly float DurationSeconds;

        public AbilityCooldownStarted(GameplayObjectId owner, DefinitionId ability, float durationSeconds)
        {
            Owner = owner;
            Ability = ability;
            DurationSeconds = durationSeconds;
        }

        /// <inheritdoc />
        public override string ToString() => $"AbilityCooldownStarted({Ability} on {Owner}, {DurationSeconds}s)";
    }

    /// <summary>Fact: an ability's cooldown completed; it is ready again.</summary>
    [EventCategory(EventCategories.Ability)]
    public readonly struct AbilityCooldownEnded : IGameplayEvent
    {
        public readonly GameplayObjectId Owner;
        public readonly DefinitionId Ability;

        public AbilityCooldownEnded(GameplayObjectId owner, DefinitionId ability)
        {
            Owner = owner;
            Ability = ability;
        }

        /// <inheritdoc />
        public override string ToString() => $"AbilityCooldownEnded({Ability} on {Owner})";
    }
}
