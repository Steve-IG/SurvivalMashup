using ToyChest.Framework.Data;

namespace ToyChest.Framework.Objects
{
    /// <summary>
    /// The load-path view of the composition root: rebuilds a persisted object from its
    /// definition, addressed by stable id, with its original identity. The Save System depends
    /// on this Framework-level contract rather than on the concrete factory in the Gameplay
    /// layer, so persistence never reaches up into composition. Implemented by the
    /// GameplayObjectFactory, which resolves the definition through the Data Registry and
    /// composes exactly as a fresh spawn would — construction is event-quiet, and the object is
    /// returned composed but not activated, ready for authoritative state to be restored onto it.
    /// See Docs/Architecture/ENGINE_STARTUP.md (Gameplay Object Reconstruction).
    /// </summary>
    public interface IGameplayObjectReconstructor
    {
        /// <summary>
        /// Composes the object declared by <paramref name="definition"/> with the persisted
        /// <paramref name="id"/>, returning it composed but not activated. Throws when the
        /// definition id is not registered — a persisted object referencing missing content is
        /// a load error, not a silent skip.
        /// </summary>
        GameplayObject Reconstruct(DefinitionId definition, GameplayObjectId id);
    }
}
