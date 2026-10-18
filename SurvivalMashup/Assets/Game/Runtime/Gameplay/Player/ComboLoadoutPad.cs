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
            if (combat == null || combat.Combo == _combo)
            {
                return;
            }

            combat.SetCombo(_combo);
            Debug.Log($"[ComboLoadoutPad] Equipped combo '{_combo.name}'.", this);
        }
    }
}
