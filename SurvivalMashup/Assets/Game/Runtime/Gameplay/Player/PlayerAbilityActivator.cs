using System;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Combat;
using ToyChest.Systems.Abilities;
using UnityEngine;

namespace ToyChest.Gameplay.Player
{
    /// <summary>
    /// The thin Unity adapter for the player's ability input slots. Each slot is authored data — an activation
    /// ability id plus the attack that performs it — and every press runs the pure <see cref="AbilitySlotActivation"/>
    /// rules against the composed object's <see cref="AbilitySet"/> and the sibling <see cref="PlayerCombat"/>. It
    /// owns no gameplay rules and never branches on which ability a slot holds: the Ability System owns validation,
    /// cost, and cooldown; the attack's hit windows and impact ability own targeting and damage. The input adapter
    /// calls <see cref="TryActivateSlot"/>; this component never reads the input device.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameplayObjectBehaviour))]
    public sealed class PlayerAbilityActivator : MonoBehaviour
    {
        [Serializable]
        private sealed class Slot
        {
            [SerializeField]
            [Tooltip("Name shown on the HUD.")]
            private string _displayName = "Ability";

            [SerializeField]
            [Tooltip("Definition id of the Self-mode activation ability (gates, cost, cooldown).")]
            private string _activationAbilityId;

            [SerializeField]
            [Tooltip("Authored attack performed when the activation commits; its hit windows apply the impact abilities. " +
                     "Empty for an attack-less ability (a Self heal or buff) whose activation's own effects are the whole ability.")]
            private ComboDefinition _attack;

            public string DisplayName => _displayName;

            public string ActivationAbilityId => _activationAbilityId;

            public ComboDefinition Attack => _attack;
        }

        [SerializeField]
        [Tooltip("The behaviour whose composed object owns the abilities. Defaults to the sibling.")]
        private GameplayObjectBehaviour _behaviour;

        [SerializeField]
        [Tooltip("Combat adapter that performs each slot's attack. Defaults to the sibling.")]
        private PlayerCombat _combat;

        [SerializeField]
        [Tooltip("Ability input slots, in input order (slot 0 = the first ability button).")]
        private Slot[] _slots = Array.Empty<Slot>();

        [SerializeField]
        [Tooltip("Seconds a press made while the character is busy (mid-attack, rolling, airborne) is held before it is dropped.")]
        private float _bufferWindow = 0.18f;

        private AbilitySlotActivation[] _activations = Array.Empty<AbilitySlotActivation>();

        /// <summary>Raised with the slot index when a slot's activation commits and its attack starts.</summary>
        public event Action<int> SlotActivated;

        /// <summary>Raised with the slot index and the Ability System's reason when a press is refused (e.g. OnCooldown).</summary>
        public event Action<int, AbilityActivationResult> SlotRefused;

        /// <summary>Number of authored slots.</summary>
        public int SlotCount => _activations.Length;

        private void Awake()
        {
            if (_behaviour == null)
            {
                _behaviour = GetComponent<GameplayObjectBehaviour>();
            }

            if (_combat == null)
            {
                _combat = GetComponent<PlayerCombat>();
            }

            _activations = new AbilitySlotActivation[_slots.Length];
            for (int i = 0; i < _slots.Length; i++)
            {
                _activations[i] = new AbilitySlotActivation(
                    new DefinitionId(_slots[i].ActivationAbilityId), _slots[i].Attack, _bufferWindow);
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < _activations.Length; i++)
            {
                _activations[i].ClearBuffer();
            }
        }

        private void Update()
        {
            AbilitySet abilities = Abilities;
            for (int i = 0; i < _activations.Length; i++)
            {
                if (_activations[i].IsBuffered)
                {
                    Report(i, _activations[i].Tick(Time.deltaTime, abilities, _combat));
                }
            }
        }

        /// <summary>The slot's input was pressed. Returns what the press did.</summary>
        public AbilitySlotPressResult TryActivateSlot(int index)
        {
            if (index < 0 || index >= _activations.Length)
            {
                return AbilitySlotPressResult.None;
            }

            AbilitySlotPressResult result = _activations[index].Press(Abilities, _combat);
            Report(index, result);
            return result;
        }

        /// <summary>The slot's HUD name.</summary>
        public string GetDisplayName(int index) => index >= 0 && index < _slots.Length ? _slots[index].DisplayName : string.Empty;

        /// <summary>
        /// The slot's cooldown state from the Ability System: whether its activation ability is granted, the seconds
        /// remaining, and the authored duration. Read-only, for the HUD.
        /// </summary>
        public bool TryGetCooldown(int index, out float remaining, out float duration)
        {
            remaining = 0f;
            duration = 0f;
            AbilitySet abilities = Abilities;
            if (abilities == null || index < 0 || index >= _activations.Length)
            {
                return false;
            }

            AbilityInstance instance = abilities.GetAbility(_activations[index].ActivationAbility);
            if (instance == null)
            {
                return false;
            }

            remaining = instance.CooldownRemaining;
            duration = instance.Definition.CooldownSeconds;
            return true;
        }

        private AbilitySet Abilities
        {
            get
            {
                GameplayObject owner = _behaviour != null ? _behaviour.Object : null;
                return owner != null && owner.IsActive && owner.TryGet(out AbilitySet abilities) ? abilities : null;
            }
        }

        private void Report(int index, AbilitySlotPressResult result)
        {
            if (result == AbilitySlotPressResult.Activated)
            {
                SlotActivated?.Invoke(index);
            }
            else if (result == AbilitySlotPressResult.Refused)
            {
                SlotRefused?.Invoke(index, _activations[index].LastResult);
            }
        }
    }
}
