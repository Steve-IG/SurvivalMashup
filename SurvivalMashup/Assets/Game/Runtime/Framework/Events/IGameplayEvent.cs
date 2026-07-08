namespace ToyChest.Framework.Events
{
    /// <summary>
    /// Marker contract for every gameplay event.
    /// An event is an immutable readonly struct describing a fact that has already occurred.
    /// Events are notifications, never commands; they carry data only and decide nothing.
    /// Named in past tense: ResourceChanged, StatusApplied, ItemEquipped.
    /// See Docs/Architecture/EVENT_SYSTEM.md.
    /// </summary>
    public interface IGameplayEvent
    {
    }
}
