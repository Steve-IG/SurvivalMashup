using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Systems.Attributes;
using UnityEngine;

namespace ToyChest.Gameplay.Npc
{
    /// <summary>
    /// The thin Unity adapter that gives an NPC simple autonomous movement. It owns no gameplay
    /// rules: movement speed is read from the composed object's <see cref="AttributeSet"/> (the same
    /// authored Movement Speed attribute the player uses — one source of truth), the wander plan is
    /// the pure, deterministic <see cref="WanderMotor"/>, and this component only bridges those to
    /// the scene's <see cref="CharacterController"/>. It is the NPC counterpart to the player's
    /// locomotion adapter, and — like it — is driven by <c>Update</c> rather than any manager,
    /// scheduler, or global update service.
    ///
    /// The NPC stays still until its Gameplay Object is composed and active (the spawner binds the
    /// sibling behaviour when the scene is ready), so wandering never runs on a half-formed object.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class NpcWanderLocomotion : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The behaviour whose composed object supplies the movement-speed attribute. Defaults to the sibling.")]
        private GameplayObjectBehaviour _behaviour;

        [SerializeField]
        [Tooltip("Definition id of the movement-speed attribute read from the composed object.")]
        private string _movementSpeedAttributeId = "attribute.movement_speed";

        [SerializeField]
        [Tooltip("Speed used before an object is bound or when it declares no movement-speed attribute.")]
        private float _fallbackSpeed = 2.5f;

        [SerializeField]
        [Tooltip("Downward acceleration applied while ungrounded, in units per second squared.")]
        private float _gravity = 20f;

        [SerializeField]
        [Tooltip("How quickly the NPC turns to face its movement direction, in degrees per second.")]
        private float _turnSpeedDegrees = 360f;

        [SerializeField]
        [Tooltip("Deterministic wander seed. NPCs with different seeds roam differently; the same seed always reproduces the same walk.")]
        private int _seed = 12345;

        [SerializeField]
        [Tooltip("Authored wander tuning: roam radius from the spawn anchor, arrival distance, and idle pause range.")]
        private WanderSettings _wander = WanderSettings.Default;

        private CharacterController _controller;
        private DefinitionId _speedAttribute;
        private WanderMotor _motor;
        private Vector2 _anchor;
        private float _verticalVelocity;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _speedAttribute = new DefinitionId(_movementSpeedAttributeId);
            if (_behaviour == null)
            {
                _behaviour = GetComponent<GameplayObjectBehaviour>();
            }

            Vector3 position = transform.position;
            _anchor = new Vector2(position.x, position.z);
            _motor = new WanderMotor(_wander, _seed);
        }

        private void Update()
        {
            float delta = Time.deltaTime;
            Vector3 position = transform.position;

            Vector3 planarVelocity = Vector3.zero;
            if (IsActiveObject())
            {
                WanderStep step = _motor.Tick(
                    _anchor, new Vector2(position.x, position.z), CurrentSpeed(), delta);
                planarVelocity = new Vector3(step.PlanarVelocity.x, 0f, step.PlanarVelocity.y);
                FaceMovement(planarVelocity, delta);
            }

            // Simple grounded gravity so the controller rests on terrain, matching the player adapter.
            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }
            else
            {
                _verticalVelocity -= _gravity * delta;
            }

            planarVelocity.y = _verticalVelocity;
            _controller.Move(planarVelocity * delta);
        }

        private bool IsActiveObject()
        {
            return _behaviour != null && _behaviour.Object != null && _behaviour.Object.IsActive;
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
