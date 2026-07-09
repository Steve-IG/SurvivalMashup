using UnityEngine;
using UnityEngine.InputSystem;

namespace ToyChest.Gameplay.Player
{
    /// <summary>
    /// The thin Unity Input System adapter: it reads the player's authored input actions and
    /// forwards intent to the gameplay adapters — the move vector to <see cref="PlayerLocomotion"/>
    /// and the interact button to <see cref="PlayerInteractor"/> (which routes through the engine's
    /// interaction system). It holds no gameplay rules and makes no gameplay decisions; it only
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
        [Tooltip("Locomotion adapter the move vector drives. Defaults to the sibling.")]
        private PlayerLocomotion _locomotion;

        [SerializeField]
        [Tooltip("Interactor adapter the interact button triggers. Defaults to the sibling.")]
        private PlayerInteractor _interactor;

        [SerializeField]
        [Tooltip("Equipment adapter the equip button toggles. Defaults to the sibling. Optional.")]
        private PlayerEquipmentController _equipment;

        private InputAction _move;
        private InputAction _interact;
        private InputAction _equip;

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

            if (_interact != null)
            {
                _interact.performed += OnInteract;
            }

            if (_equip != null)
            {
                _equip.performed += OnEquip;
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

            _actions?.FindActionMap(_actionMap, throwIfNotFound: false)?.Disable();
            _move = null;
            _interact = null;
            _equip = null;
        }

        private void Update()
        {
            if (_locomotion != null && _move != null)
            {
                _locomotion.SetMoveInput(_move.ReadValue<Vector2>());
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
    }
}
