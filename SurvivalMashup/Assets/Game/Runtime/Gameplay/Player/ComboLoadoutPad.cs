using ToyChest.Gameplay.Combat;
using UnityEngine;

namespace ToyChest.Gameplay.Player
{
    /// <summary>
    /// A thin training-device for the combat dojo: a visible floor trigger that, when the player steps onto it,
    /// equips its authored <see cref="ComboDefinition"/> on the player's <see cref="PlayerCombat"/> — so the
    /// attack-library combos can be tried back-to-back without editing the Player prefab or using debug keys.
    /// It is the loadout counterpart of the dojo's <c>EnemySpawnTrigger</c>: a Unity contact bridged to one
    /// existing operation (<see cref="PlayerCombat.SetCombo"/>), <b>not</b> a combo-selection system. Every combo
    /// it equips then plays through the normal input → combo → animation → hit-detection → ability path.
    ///
    /// <para>The trigger volume must reach well up into the player's capsule: a CharacterController rests its
    /// skin width (≈0.08m) above the floor, so a paper-thin floor trigger is only grazed and the physics step
    /// never reports the contact. The pad's visual stays a thin slab; its <see cref="BoxCollider"/> is a 1m-tall
    /// column (see <c>AuthorAttackLibraryContent</c>). Selecting during an attack is deferred by
    /// <see cref="PlayerCombat.SetCombo"/> until that combo ends.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ComboLoadoutPad : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Unity tag identifying the player who steps on the pad.")]
        private string _playerTag = "Player";

        [SerializeField]
        [Tooltip("The combo equipped when the player steps on the pad.")]
        private ComboDefinition _combo;

        /// <summary>The combo this pad equips.</summary>
        public ComboDefinition Combo => _combo;

        private void OnTriggerEnter(Collider other)
        {
            if (other == null || !other.CompareTag(_playerTag) || _combo == null)
            {
                return;
            }

            var combat = other.GetComponentInParent<PlayerCombat>();
            if (combat == null)
            {
                return;
            }

            // Always forwarded, even for the active combo: re-selecting it clears a swap queued mid-attack.
            bool changes = combat.Combo != _combo || combat.HasPendingCombo;
            combat.SetCombo(_combo);
            if (changes)
            {
                Debug.Log(combat.HasPendingCombo
                    ? $"[ComboLoadoutPad] Selected combo '{_combo.name}' (applies when the current attack ends)."
                    : $"[ComboLoadoutPad] Equipped combo '{_combo.name}'.", this);
            }
        }
    }
}
