using System;
using System.Collections.Generic;

namespace ToyChest.Systems.GameplayEffects
{
    /// <summary>
    /// Executes effect sequences deterministically: in list order, each effect gated by its
    /// own conditions, a blocked effect skipping only itself. This is the one entry point
    /// invokers (Status Effect System, Ability System, interactions) use to run authored
    /// effect lists — no invoker iterates definitions itself.
    /// </summary>
    public sealed class GameplayEffectRunner
    {
        /// <summary>
        /// Runs every effect in <paramref name="effects"/> in order against the context.
        /// Returns the number of effects that executed (conditions passed).
        /// </summary>
        public int Execute(IReadOnlyList<GameplayEffectDefinition> effects, in EffectContext context)
        {
            if (effects == null)
            {
                throw new ArgumentNullException(nameof(effects));
            }

            int executed = 0;
            for (int i = 0; i < effects.Count; i++)
            {
                GameplayEffectDefinition effect = effects[i];
                if (effect == null)
                {
                    throw new InvalidOperationException(
                        $"Effect sequence contains a null entry at index {i}. Check the authoring asset for an unassigned effect slot.");
                }

                if (effect.TryExecute(in context))
                {
                    executed++;
                }
            }

            return executed;
        }
    }
}
