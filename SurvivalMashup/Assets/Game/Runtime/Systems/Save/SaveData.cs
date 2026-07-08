using System;
using System.Collections.Generic;

namespace ToyChest.Systems.Save
{
    /// <summary>
    /// The root of one save payload: a version stamp and the authoritative state of every live
    /// Gameplay Object at capture time, in the registry's deterministic enumeration order.
    ///
    /// This is a stable serialization contract (Engine Principle 21). It is a flat, plain-data
    /// model — no live references, no capability object graph, no caches, no lookup structures,
    /// no event subscriptions — so it round-trips through <c>UnityEngine.JsonUtility</c> and any
    /// future serializer deterministically. It carries only what a save must (Engine Principle 25,
    /// Persistence Boundary): definitions are referenced by stable id and everything derived is
    /// rebuilt on load by re-running composition, not read back from the file.
    ///
    /// Evolving the contract is a versioned, intentional act: bump <see cref="SaveManager.CurrentVersion"/>
    /// and add migration, exactly as with event-contract evolution. See Docs/Systems/SAVE_SYSTEM.md.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>
        /// The schema version this payload was written with. Checked on load; a mismatch is a
        /// clear, early failure rather than a silent misread (migration is a future addition).
        /// </summary>
        public int Version;

        /// <summary>Per-object authoritative state, in registry (activation) order.</summary>
        public List<GameplayObjectState> Objects = new List<GameplayObjectState>();
    }
}
