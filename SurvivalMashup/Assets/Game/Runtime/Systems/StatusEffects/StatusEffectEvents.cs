using ToyChest.Framework.Data;
using ToyChest.Framework.Events;
using ToyChest.Framework.Objects;

namespace ToyChest.Systems.StatusEffects
{
    /// <summary>Fact: a status became active on an object. Owned solely by the Status Effect System.</summary>
    [EventCategory(EventCategories.StatusEffect)]
    public readonly struct StatusApplied : IGameplayEvent
    {
        public readonly GameplayObjectId Owner;
        public readonly DefinitionId Status;

        public StatusApplied(GameplayObjectId owner, DefinitionId status)
        {
            Owner = owner;
            Status = status;
        }

        /// <inheritdoc />
        public override string ToString() => $"StatusApplied({Status} on {Owner})";
    }

    /// <summary>Fact: a status was explicitly removed before expiring (dispel, replace, cleanse).</summary>
    [EventCategory(EventCategories.StatusEffect)]
    public readonly struct StatusRemoved : IGameplayEvent
    {
        public readonly GameplayObjectId Owner;
        public readonly DefinitionId Status;

        public StatusRemoved(GameplayObjectId owner, DefinitionId status)
        {
            Owner = owner;
            Status = status;
        }

        /// <inheritdoc />
        public override string ToString() => $"StatusRemoved({Status} from {Owner})";
    }

    /// <summary>Fact: a status reached the natural end of its duration.</summary>
    [EventCategory(EventCategories.StatusEffect)]
    public readonly struct StatusExpired : IGameplayEvent
    {
        public readonly GameplayObjectId Owner;
        public readonly DefinitionId Status;

        public StatusExpired(GameplayObjectId owner, DefinitionId status)
        {
            Owner = owner;
            Status = status;
        }

        /// <inheritdoc />
        public override string ToString() => $"StatusExpired({Status} on {Owner})";
    }

    /// <summary>Fact: an active status had its duration reset by re-application.</summary>
    [EventCategory(EventCategories.StatusEffect)]
    public readonly struct StatusRefreshed : IGameplayEvent
    {
        public readonly GameplayObjectId Owner;
        public readonly DefinitionId Status;

        public StatusRefreshed(GameplayObjectId owner, DefinitionId status)
        {
            Owner = owner;
            Status = status;
        }

        /// <inheritdoc />
        public override string ToString() => $"StatusRefreshed({Status} on {Owner})";
    }

    /// <summary>Fact: an active status gained a stack.</summary>
    [EventCategory(EventCategories.StatusEffect)]
    public readonly struct StatusStackIncreased : IGameplayEvent
    {
        public readonly GameplayObjectId Owner;
        public readonly DefinitionId Status;
        public readonly int Stacks;

        public StatusStackIncreased(GameplayObjectId owner, DefinitionId status, int stacks)
        {
            Owner = owner;
            Status = status;
            Stacks = stacks;
        }

        /// <inheritdoc />
        public override string ToString() => $"StatusStackIncreased({Status} on {Owner} -> {Stacks})";
    }

    /// <summary>Fact: a status executed its periodic effect sequence.</summary>
    [EventCategory(EventCategories.StatusEffect)]
    public readonly struct StatusPeriodicTick : IGameplayEvent
    {
        public readonly GameplayObjectId Owner;
        public readonly DefinitionId Status;

        public StatusPeriodicTick(GameplayObjectId owner, DefinitionId status)
        {
            Owner = owner;
            Status = status;
        }

        /// <inheritdoc />
        public override string ToString() => $"StatusPeriodicTick({Status} on {Owner})";
    }
}
