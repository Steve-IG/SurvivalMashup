namespace ToyChest.Framework.Objects
{
    /// <summary>
    /// Marker contract for a capability component composable onto a Gameplay Object
    /// (attribute set, resource set, tag container, ability set, ...).
    /// Capabilities own behavior; Gameplay Objects own composition. A capability implements
    /// its domain and never decides what else the object is composed of.
    /// Capabilities are constructed only by the composition root, never at call sites.
    /// </summary>
    public interface IGameplayCapability
    {
    }
}
