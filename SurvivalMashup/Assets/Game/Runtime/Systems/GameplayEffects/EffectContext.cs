using ToyChest.Systems.Tags;

namespace ToyChest.Systems.GameplayEffects
{
    /// <summary>
    /// Everything an atomic effect may consult while executing: who caused it, who receives it,
    /// the session tag table for resolving authored tag paths, and the modifier source under
    /// which attribute modifiers are applied (so the invoker — a status instance, equipment —
    /// can revoke exactly its own contributions later). Immutable; targeting is decided by the
    /// caller (Ability System, Status Effect System), never by effects.
    /// </summary>
    public readonly struct EffectContext
    {
        /// <summary>Capabilities of the causing participant. May be empty.</summary>
        public readonly EffectTarget Source;

        /// <summary>Capabilities of the receiving participant.</summary>
        public readonly EffectTarget Target;

        /// <summary>The session's interned tag hierarchy, for resolving authored tag paths.</summary>
        public readonly GameplayTagTable TagTable;

        /// <summary>
        /// Identity under which <c>ApplyModifierEffect</c> registers modifiers. When null, the
        /// effect definition itself is used, making the application effectively permanent.
        /// </summary>
        public readonly object ModifierSource;

        /// <summary>Creates an execution context.</summary>
        public EffectContext(EffectTarget source, EffectTarget target, GameplayTagTable tagTable, object modifierSource = null)
        {
            Source = source;
            Target = target;
            TagTable = tagTable;
            ModifierSource = modifierSource;
        }
    }
}
