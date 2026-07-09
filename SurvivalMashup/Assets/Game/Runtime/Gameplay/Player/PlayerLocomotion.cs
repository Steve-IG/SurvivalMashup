using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Systems.Attributes;
using UnityEngine;

namespace ToyChest.Gameplay.Player
{
    /// <summary>
    /// The thin Unity adapter that turns move intent into character motion. It owns no gameplay
    /// rules: movement speed is read from the composed object's <see cref="AttributeSet"/> (an
    /// authored attribute, one source of truth), the direction math lives in
    /// <see cref="LocomotionMotor"/>, and this component only bridges those to the scene's
    /// <see cref="CharacterController"/>. Input is pushed in via <see cref="SetMoveInput"/> by the
    /// input adapter; this component never reads the input device itself.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerLocomotion : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The behaviour whose composed object supplies the movement-speed attribute. Defaults to the sibling.")]
        private GameplayObjectBehaviour _behaviour;

        [SerializeField]
        [Tooltip("Camera whose facing makes movement camera-relative. Defaults to the main camera.")]
        private Transform _cameraTransform;

        [SerializeField]
        [Tooltip("Definition id of the movement-speed attribute read from the composed object.")]
        private string _movementSpeedAttributeId = "attribute.movement_speed";

        [SerializeField]
        [Tooltip("Speed used before an object is bound or when it declares no movement-speed attribute.")]
        private float _fallbackSpeed = 5f;

        [SerializeField]
        [Tooltip("Downward acceleration applied while ungrounded, in units per second squared.")]
        private float _gravity = 20f;

        [SerializeField]
        [Tooltip("How quickly the character turns to face its movement direction, in degrees per second.")]
        private float _turnSpeedDegrees = 720f;

        private CharacterController _controller;
        private DefinitionId _speedAttribute;
        private Vector2 _moveInput;
        private float _verticalVelocity;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _speedAttribute = new DefinitionId(_movementSpeedAttributeId);
            if (_behaviour == null)
            {
                _behaviour = GetComponent<GameplayObjectBehaviour>();
            }

            if (_cameraTransform == null && Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }
        }

        /// <summary>Sets the current move intent (strafe, forward), each component in [-1, 1].</summary>
        public void SetMoveInput(Vector2 moveInput)
        {
            _moveInput = moveInput;
        }

        private void Update()
        {
            float delta = Time.deltaTime;
            Vector3 cameraForward = _cameraTransform != null ? _cameraTransform.forward : Vector3.forward;
            Vector3 velocity = LocomotionMotor.PlanarVelocity(_moveInput, cameraForward, CurrentSpeed());

            FaceMovement(velocity, delta);

            // Simple grounded gravity so the controller stays on terrain; full traversal (jump,
            // dodge, mantle) is later, ability-driven work and out of this slice.
            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }
            else
            {
                _verticalVelocity -= _gravity * delta;
            }

            velocity.y = _verticalVelocity;
            _controller.Move(velocity * delta);
        }

        private float CurrentSpeed()
        {
            if (_behaviour != null && _behaviour.Object != null &&
                _behaviour.Object.TryGet(out AttributeSet attributes) &&
                attributes.TryGetValue(_speedAttribute, out float speed))
            {
                return speed;
            }

            return _fallbackSpeed;
        }

        private void FaceMovement(Vector3 velocity, float delta)
        {
            Vector3 planar = new Vector3(velocity.x, 0f, velocity.z);
            if (planar.sqrMagnitude < 1e-4f)
            {
                return;
            }

            Quaternion target = Quaternion.LookRotation(planar, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _turnSpeedDegrees * delta);
        }
    }
}
