using System.Collections.Generic;
using UnityEngine;

namespace ToyChest.Gameplay.Combat
{
    /// <summary>
    /// An authored, ordered sequence of attacks — the whole combo expressed as <b>data</b>, not code. A
    /// three-hit melee string, or a "Punch → Punch → Kick → Ground Slam" mix, is this asset with its
    /// <see cref="Steps"/> filled in; no combo has a bespoke <c>Punch1/Punch2/Punch3</c> implementation.
    /// The <see cref="ComboSequencer"/> orders the steps and the driver (<c>PlayerCombat</c>) maps each to
    /// its authored ability, hit volume, and animation, so adding, reordering, or retiming a combo is an
    /// asset edit. Nothing here assumes a step is melee — a step is whatever its ability and hit volume
    /// make it.
    ///
    /// This is a plain authoring asset (like a hit-volume preset), not a gameplay <c>Definition</c>: it has
    /// no runtime id and needs no Data Registry entry, because the combat adapter references it directly.
    /// Create via <b>Assets ▸ Create ▸ ToyChest ▸ Combat ▸ Combo</b>.
    /// </summary>
    [CreateAssetMenu(menuName = "ToyChest/Combat/Combo", fileName = "Combo_")]
    public sealed class ComboDefinition : ScriptableObject
    {
        [SerializeField]
        [Tooltip("The attacks in order. The first begins on the attack press; each subsequent one chains when " +
                 "the player continues the combo within a step's continuation window.")]
        private List<ComboStep> _steps = new List<ComboStep>();

        /// <summary>The ordered attack steps.</summary>
        public IReadOnlyList<ComboStep> Steps => _steps;

        /// <summary>The number of steps in the combo.</summary>
        public int StepCount => _steps.Count;

        /// <summary>
        /// Extracts the pure per-step timings the <see cref="ComboSequencer"/> runs on, in order. Kept
        /// separate from the authoring data so the sequencer stays Unity-free and testable.
        /// </summary>
        public List<ComboStepTiming> BuildTimings()
        {
            var timings = new List<ComboStepTiming>(_steps.Count);
            for (int i = 0; i < _steps.Count; i++)
            {
                timings.Add(_steps[i].Timing);
            }

            return timings;
        }
    }
}
