using ToyChest.Framework.Events;

namespace ToyChest.Tests.Events
{
    /// <summary>Simple valued event used by Event System tests.</summary>
    [EventCategory(EventCategories.Resource)]
    internal readonly struct NumberReported : IGameplayEvent
    {
        public readonly int Value;

        public NumberReported(int value)
        {
            Value = value;
        }

        public override string ToString()
        {
            return $"NumberReported({Value})";
        }
    }

    /// <summary>Second event type used to verify channel isolation.</summary>
    internal readonly struct FlagRaised : IGameplayEvent
    {
        public readonly string Name;

        public FlagRaised(string name)
        {
            Name = name;
        }
    }
}
