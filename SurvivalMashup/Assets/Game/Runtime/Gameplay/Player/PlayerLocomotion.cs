using System;
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
    /// <see cref="CharacterController"/>. Sprint, jump, and roll are movement <em>feel</em> layered on
    /// the same attribute-driven speed (sprint is a multiplier, not a new resource); none introduces a
    /// gameplay system. Input is pushed in by the input adapter; this component never reads the device.
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
        private float _fallbackSpeed = 7f;

        [SerializeField]
        [Tooltip("Downward acceleration applied while ungrounded, in units per second squared.")]
        private float _gravity = 25f;

        [SerializeField]
        [Tooltip("How quickly the character turns to face its movement direction, in degrees per second.")]
        private float _turnSpeedDegrees = 620f;

        [Header("Feel")]
        [SerializeField]
        [Tooltip("How quickly the character reaches top speed, in units per second squared. High = snappy.")]
        private float _acceleration = 60f;

        [SerializeField]
        [Tooltip("How quickly the character stops, in units per second squared. High = crisp, precise stops.")]
        private float _deceleration = 80f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Fraction of ground acceleration usable while airborne (momentum with control).")]
        private float _airControl = 0.6f;

        [Header("Sprint")]
        [SerializeField]
        [Tooltip("Multiplier applied to the authored Movement Speed while the sprint button is held.")]
        private float _sprintMultiplier = 1.5f;

        [Header("Jump")]
        [SerializeField]
        [Tooltip("Upward launch speed of a jump, in units per second.")]
        private float _jumpSpeed = 8.5f;

        [Header("Roll")]
        [SerializeField]
        [Tooltip("Explosive speed added on top of the character's current momentum when a roll begins, " +
                 "in units per second. Sprinting into a roll keeps its speed and adds this, so a sprint " +
                 "roll is naturally faster and longer than a walking one.")]
        private float _rollImpulse = 8f;

        [SerializeField]
        [Tooltip("Upper bound on the roll's launch speed so the fastest entries stay controllable, " +
                 "in units per second. Zero disables the clamp.")]
        private float _rollMaxSpeed = 20f;

        [SerializeField]
        [Tooltip("How quickly the explosive roll burst bleeds back toward ordinary locomotion, in units " +
                 "per second squared. Low = a long committed glide; high = a short punchy burst.")]
        private float _rollDeceleration = 18f;

        [SerializeField]
        [Tooltip("Duration of the roll before control returns to ordinary locomotion, in seconds.")]
        private float _rollDuration = 0.5f;

        private CharacterController _controller;
        private DefinitionId _speedAttribute;
        private Vector2 _moveInput;
        private float _verticalVelocity;
        private Vector3 _planarVelocity;
        private float _moveLockTimer;
        private float _moveLockScale = 1f;
        private bool _sprinting;
        private float _rollTimer;
        private Vector3 _rollDirection;
        private bool _wasGrounded = true;

        /// <summary>Raised when a jump launches — presentation reads this to play the jump animation.</summary>
        public event Action Jumped;

        /// <summary>Raised when a roll starts — presentation reads this to play the roll animation.</summary>
        public event Action Rolled;

        /// <summary>Raised when the character touches down after being airborne — for a landing animation.</summary>
        public event Action Landed;

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
        public void SetMoveInput(Vector2 moveInput) => _moveInput = moveInput;

        /// <summary>Holds or releases sprint (a speed multiplier, not a toggle and not a resource).</summary>
        public void SetSprint(bool sprinting) => _sprinting = sprinting;

        /// <summary>Launches a jump if grounded and not mid-roll.</summary>
        public void Jump()
        {
            if (_rollTimer > 0f || !_controller.isGrounded)
            {
                return;
            }

            _verticalVelocity = _jumpSpeed;
            Jumped?.Invoke();
        }

        /// <summary>
        /// Starts an evasive roll if grounded and not already rolling. The roll inherits the character's
        /// current momentum and adds an explosive impulse (so sprinting rolls faster and farther than
        /// walking, with no separate path), then bleeds that burst back toward ordinary locomotion.
        /// </summary>
        public void Roll()
        {
            if (_rollTimer > 0f || !_controller.isGrounded)
            {
                return;
            }

            _rollDirection = RollDirection();
            _planarVelocity = LocomotionMotor.RollLaunchVelocity(
                _planarVelocity, _rollDirection, _rollImpulse, _rollMaxSpeed);
            _rollTimer = _rollDuration;
            Rolled?.Invoke();
        }

        /// <summary>
        /// Briefly commits the character to an action by damping movement (attack commitment). A later
        /// call refreshes the window. Ignored while rolling. This is a feel hook, not a restriction.
        /// </summary>
        public void ApplyMovementLock(float duration, float scale)
        {
            _moveLockTimer = Mathf.Max(_moveLockTimer, Mathf.Max(0f, duration));
            _moveLockScale = Mathf.Clamp01(scale);
        }

        /// <summary>The character's top movement speed right now (authored attribute × sprint).</summary>
        public float MaxSpeed => CurrentSpeed();

        /// <summary>The authored base movement speed (no sprint), used to normalize animation blends.</summary>
        public float BaseSpeed => AttributeSpeed();

        /// <summary>Current planar speed in units per second (ignores gravity).</summary>
        public float CurrentPlanarSpeed => new Vector2(_planarVelocity.x, _planarVelocity.z).magnitude;

        /// <summary>Planar speed normalized against the base run speed (run ≈ 1, sprint ≈ sprint multiplier).</summary>
        public float NormalizedSpeed
        {
            get
            {
                float baseSpeed = AttributeSpeed();
                return baseSpeed > 0.01f ? CurrentPlanarSpeed / baseSpeed : 0f;
            }
        }

        /// <summary>Movement relative to facing (strafe, forward), normalized against base run speed — drives the 8-way blend.</summary>
        public Vector2 LocalMove
        {
            get
            {
                float baseSpeed = AttributeSpeed();
                Vector2 local = LocomotionMotor.LocalMoveVector(_planarVelocity, transform.forward);
                return baseSpeed > 0.01f ? local / baseSpeed : Vector2.zero;
            }
        }

        /// <summary>Whether the character is on the ground this frame.</summary>
        public bool IsGrounded => _controller.isGrounded;

        /// <summary>Whether the character is mid-roll.</summary>
        public bool IsRolling => _rollTimer > 0f;

        private void Update()
        {
            float delta = Time.deltaTime;
            bool grounded = _controller.isGrounded;

            if (grounded && !_wasGrounded)
            {
                Landed?.Invoke();
            }

            if (_rollTimer > 0f)
            {
                TickRoll(delta);
            }
            else
            {
                TickLocomotion(delta, grounded);
            }

            _wasGrounded = _controller.isGrounded;
        }

        private void TickLocomotion(float delta, bool grounded)
        {
            Vector3 cameraForward = _cameraTransform != null ? _cameraTransform.forward : Vector3.forward;

            float speedScale = 1f;
            bool locked = _moveLockTimer > 0f;
            if (locked)
            {
                _moveLockTimer -= delta;
                speedScale = _moveLockScale;
            }

            Vector3 desired = LocomotionMotor.PlanarVelocity(_moveInput, cameraForward, CurrentSpeed() * speedScale);
            float accelScale = grounded ? 1f : _airControl;
            _planarVelocity = LocomotionMotor.Accelerate(
                _planarVelocity, desired, _acceleration * accelScale, _deceleration * accelScale, delta);

            // While committed to a swing, let the attack's target-facing hold instead of turning to movement.
            if (!locked)
            {
                FaceDirection(_planarVelocity, delta);
            }

            ApplyGravity(delta, grounded);
            Move(_planarVelocity, delta);
        }

        private void TickRoll(float delta)
        {
            _rollTimer -= delta;

            // Bleed the explosive burst back toward the player's ongoing locomotion intent rather than
            // toward a dead stop: if they keep holding a direction the roll recovers straight into a run
            // (momentum preserved, no re-acceleration from zero and no recovery foot-slide); if they let
            // go it glides down to idle. The same _planarVelocity + Accelerate the ground loop uses makes
            // the hand-back at roll end seamless.
            Vector3 cameraForward = _cameraTransform != null ? _cameraTransform.forward : Vector3.forward;
            Vector3 desired = LocomotionMotor.PlanarVelocity(_moveInput, cameraForward, CurrentSpeed());
            _planarVelocity = LocomotionMotor.Accelerate(
                _planarVelocity, desired, _acceleration, _rollDeceleration, delta);

            // Face the way we are actually moving so late input curves the roll toward the new heading.
            Vector3 faceDirection = _planarVelocity.sqrMagnitude > 1e-4f ? _planarVelocity : _rollDirection;
            FaceDirection(faceDirection, delta);
            ApplyGravity(delta, _controller.isGrounded);
            Move(_planarVelocity, delta);
        }

        private void ApplyGravity(float delta, bool grounded)
        {
            if (grounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }
            else
            {
                _verticalVelocity -= _gravity * delta;
            }
        }

        private void Move(Vector3 planarVelocity, float delta)
        {
            Vector3 motion = planarVelocity;
            motion.y = _verticalVelocity;
            _controller.Move(motion * delta);
        }

        /// <summary>
        /// Applies a world-space root-motion translation from the animation to the character controller
        /// (horizontal only — gravity stays owned here). The attack drives movement through this so the
        /// animated step is real, committed motion the character keeps, instead of a visual that snaps
        /// back to where the clip started. Locomotion itself remains velocity-driven; only the attack
        /// feeds root motion in (gated by the model relay), so the two never fight.
        /// </summary>
        public void ApplyRootMotion(Vector3 worldDelta)
        {
            Vector3 horizontal = new Vector3(worldDelta.x, 0f, worldDelta.z);
            if (horizontal.sqrMagnitude > 0f)
            {
                _controller.Move(horizontal);
            }
        }

        private Vector3 RollDirection()
        {
            Vector3 cameraForward = _cameraTransform != null ? _cameraTransform.forward : Vector3.forward;
            Vector3 intent = LocomotionMotor.PlanarVelocity(_moveInput, cameraForward, 1f);
            if (intent.sqrMagnitude > 1e-4f)
            {
                return intent.normalized;
            }

            Vector3 forward = new Vector3(transform.forward.x, 0f, transform.forward.z);
            return forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
        }

        private float CurrentSpeed() => AttributeSpeed() * (_sprinting ? Mathf.Max(1f, _sprintMultiplier) : 1f);

        private float AttributeSpeed()
        {
            if (_behaviour != null && _behaviour.Object != null &&
                _behaviour.Object.TryGet(out AttributeSet attributes) &&
                attributes.TryGetValue(_speedAttribute, out float speed))
            {
                return speed;
            }

            return _fallbackSpeed;
        }

        private void FaceDirection(Vector3 direction, float delta)
        {
            Vector3 planar = new Vector3(direction.x, 0f, direction.z);
            if (planar.sqrMagnitude < 1e-4f)
            {
                return;
            }

            Quaternion target = Quaternion.LookRotation(planar, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _turnSpeedDegrees * delta);
        }
    }
}
