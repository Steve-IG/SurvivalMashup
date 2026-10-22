using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ToyChest.Gameplay.Player
{
    /// <summary>
    /// The thin Unity Input System adapter: it reads the player's authored input actions and
    /// forwards intent to the gameplay adapters — the move vector to <see cref="PlayerLocomotion"/>
    /// and the interact button to <see cref="PlayerInteractor"/> (which routes through the engine's
    /// interaction system), and each ability button to its <see cref="PlayerAbilityActivator"/> slot. It holds no gameplay rules and makes no gameplay decisions; it only
    /// translates device input into calls on sibling adapters, so control bindings stay data and the
    /// gameplay stays engine-owned.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInputBridge : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The authored input actions asset. Its 'Player' map supplies Move and Interact.")]
        private InputActionAsset _actions;

        [SerializeField]
        [Tooltip("Action map read for player control.")]
        private string _actionMap = "Player";

        [SerializeField]
        [Tooltip("Vector2 action driving locomotion.")]
        private string _moveAction = "Move";

        [SerializeField]
        [Tooltip("Button action triggering a contextual interaction.")]
        private string _interactAction = "Interact";

        [SerializeField]
        [Tooltip("Button action toggling equipment (equip from bag / unequip to bag).")]
        private string _equipAction = "Equip";

        [SerializeField]
        [Tooltip("Button action performing a melee attack.")]
        private string _attackAction = "Attack";

        [SerializeField]
        [Tooltip("Button action held to sprint.")]
        private string _sprintAction = "Sprint";

        [SerializeField]
        [Tooltip("Button action performing a jump.")]
        private string _jumpAction = "Jump";

        [SerializeField]
        [Tooltip("Button action performing an evasive roll.")]
        private string _rollAction = "Roll";

        [SerializeField]
        [Tooltip("Button actions for the ability input slots, in slot order (element 0 presses slot 0).")]
        private string[] _abilitySlotActions = { "Ability1" };

        [SerializeField]
        [Tooltip("Locomotion adapter the move vector drives. Defaults to the sibling.")]
        private PlayerLocomotion _locomotion;

        [SerializeField]
        [Tooltip("Interactor adapter the interact button triggers. Defaults to the sibling.")]
        private PlayerInteractor _interactor;

        [SerializeField]
        [Tooltip("Equipment adapter the equip button toggles. Defaults to the sibling. Optional.")]
        private PlayerEquipmentController _equipment;

        [SerializeField]
        [Tooltip("Combat adapter the attack button triggers. Defaults to the sibling. Optional.")]
        private PlayerCombat _combat;

        [SerializeField]
        [Tooltip("Ability adapter the ability buttons press. Defaults to the sibling. Optional.")]
        private PlayerAbilityActivator _abilities;

        private InputAction _move;
        private InputAction _interact;
        private InputAction _equip;
        private InputAction _attack;
        private InputAction _sprint;
        private InputAction _jump;
        private InputAction _roll;
        private InputAction[] _abilitySlots = Array.Empty<InputAction>();
        private Action<InputAction.CallbackContext>[] _abilitySlotHandlers = Array.Empty<Action<InputAction.CallbackContext>>();

        private void Awake()
        {
            if (_locomotion == null)
            {
                _locomotion = GetComponent<PlayerLocomotion>();
            }

            if (_interactor == null)
            {
                _interactor = GetComponent<PlayerInteractor>();
            }

            if (_equipment == null)
            {
                _equipment = GetComponent<PlayerEquipmentController>();
            }

            if (_combat == null)
            {
                _combat = GetComponent<PlayerCombat>();
            }

            if (_abilities == null)
            {
                _abilities = GetComponent<PlayerAbilityActivator>();
            }
        }

        private void OnEnable()
        {
            if (_actions == null)
            {
                Debug.LogError($"[PlayerInputBridge] '{name}' has no input actions asset assigned.", this);
                return;
            }

            InputActionMap map = _actions.FindActionMap(_actionMap, throwIfNotFound: false);
            if (map == null)
            {
                Debug.LogError($"[PlayerInputBridge] '{name}' input asset has no '{_actionMap}' action map.", this);
                return;
            }

            _move = map.FindAction(_moveAction, throwIfNotFound: false);
            _interact = map.FindAction(_interactAction, throwIfNotFound: false);
            _equip = map.FindAction(_equipAction, throwIfNotFound: false);
            _attack = map.FindAction(_attackAction, throwIfNotFound: false);
            _sprint = map.FindAction(_sprintAction, throwIfNotFound: false);
            _jump = map.FindAction(_jumpAction, throwIfNotFound: false);
            _roll = map.FindAction(_rollAction, throwIfNotFound: false);

            if (_interact != null)
            {
                _interact.performed += OnInteract;
            }

            if (_equip != null)
            {
                _equip.performed += OnEquip;
            }

            if (_attack != null)
            {
                _attack.performed += OnAttack;
            }

            if (_jump != null)
            {
                _jump.performed += OnJump;
            }

            if (_roll != null)
            {
                _roll.performed += OnRoll;
            }

            _abilitySlots = new InputAction[_abilitySlotActions.Length];
            _abilitySlotHandlers = new Action<InputAction.CallbackContext>[_abilitySlotActions.Length];
            for (int i = 0; i < _abilitySlotActions.Length; i++)
            {
                int slot = i;
                _abilitySlots[i] = map.FindAction(_abilitySlotActions[i], throwIfNotFound: false);
                _abilitySlotHandlers[i] = _ => _abilities?.TryActivateSlot(slot);
                if (_abilitySlots[i] != null)
                {
                    _abilitySlots[i].performed += _abilitySlotHandlers[i];
                }
            }

            map.Enable();
        }

        private void OnDisable()
        {
            if (_interact != null)
            {
                _interact.performed -= OnInteract;
            }

            if (_equip != null)
            {
                _equip.performed -= OnEquip;
            }

            if (_attack != null)
            {
                _attack.performed -= OnAttack;
            }

            if (_jump != null)
            {
                _jump.performed -= OnJump;
            }

            if (_roll != null)
            {
                _roll.performed -= OnRoll;
            }

            for (int i = 0; i < _abilitySlots.Length; i++)
            {
                if (_abilitySlots[i] != null)
                {
                    _abilitySlots[i].performed -= _abilitySlotHandlers[i];
                }
            }

            _actions?.FindActionMap(_actionMap, throwIfNotFound: false)?.Disable();
            _move = null;
            _interact = null;
            _equip = null;
            _attack = null;
            _sprint = null;
            _jump = null;
            _roll = null;
            _abilitySlots = Array.Empty<InputAction>();
            _abilitySlotHandlers = Array.Empty<Action<InputAction.CallbackContext>>();
        }

        private void Update()
        {
            if (_locomotion != null && _move != null)
            {
                _locomotion.SetMoveInput(_move.ReadValue<Vector2>());
            }

            if (_locomotion != null && _sprint != null)
            {
                _locomotion.SetSprint(_sprint.IsPressed());
            }
        }

        private void OnInteract(InputAction.CallbackContext context)
        {
            _interactor?.TryInteract();
        }

        private void OnEquip(InputAction.CallbackContext context)
        {
            _equipment?.Toggle();
        }

        private void OnAttack(InputAction.CallbackContext context)
        {
            _combat?.TryAttack();
        }

        private void OnJump(InputAction.CallbackContext context)
        {
            _locomotion?.Jump();
        }

        private void OnRoll(InputAction.CallbackContext context)
        {
            _locomotion?.Roll();
        }
    }
}
