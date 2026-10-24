using UnityEngine;

namespace ToyChest.Gameplay.Player
{
    /// <summary>
    /// The player-controlled orbit point of the third-person action camera (the God of War / Assassin's Creed shape):
    /// a pivot that rides at the player's shoulder height and that the look input (mouse or right stick) yaws and
    /// pitches. The gameplay Cinemachine camera follows this pivot with a Third Person Follow (shoulder offset, arm
    /// length, distance, obstacle avoidance), so the camera orbits the player and frames them over the shoulder.
    /// Movement already reads the active camera's facing, so "forward" is always where the camera looks.
    ///
    /// <para>Framing follows the common third-person action camera rules (see COMBAT.md, *Gameplay Camera*): a close
    /// camera at roughly eye height, nearly level, with the character on the left third; player-driven orbit with a
    /// shaped stick curve; gamepad-only auto-align behind travel; a pitch range that keeps the camera out of the floor.</para>
    ///
    /// <para>Presentation only: it moves no gameplay object. It also locks and hides the cursor while playing (Escape
    /// releases it; clicking the game view captures it again), so mouse look does not need a held button.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class ThirdPersonCameraPivot : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Unity tag of the player the pivot follows.")]
        private string _playerTag = "Player";

        [Header("Framing")]
        [SerializeField]
        [Tooltip("Height of the pivot above the player's root (the body centre, ~1 m off the ground), in metres. The camera " +
                 "frames this point (plus the rig's shoulder offset), so shoulder height (0.45) puts the head just above " +
                 "screen centre and the body down to the bottom edge, like God of War.")]
        private float _height = 0.45f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds for the pivot to catch up with the player's position (0 = locked on).")]
        private float _followSmoothing = 0.06f;

        [Header("Orbit")]
        [SerializeField]
        [Tooltip("Initial yaw in degrees (0 = looking along world +Z).")]
        private float _initialYaw;

        [SerializeField]
        [Tooltip("Initial pitch in degrees (positive looks down at the player).")]
        private float _initialPitch = 5f;

        [SerializeField]
        [Tooltip("Lowest pitch (looking up). A close camera swings below the pivot when looking up, so this keeps it " +
                 "above the floor (-20 at 3 m keeps it ~0.4 m off the ground).")]
        private float _minPitch = -20f;

        [SerializeField]
        [Tooltip("Highest pitch (looking down from above).")]
        private float _maxPitch = 55f;

        [Header("Look sensitivity")]
        [SerializeField]
        [Min(0f)]
        [Tooltip("Mouse: degrees of rotation per pixel of mouse movement.")]
        private float _mouseDegreesPerPixel = 0.12f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Gamepad: horizontal turn rate at full stick deflection, in degrees per second.")]
        private float _stickYawSpeed = 200f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Gamepad: vertical turn rate at full stick deflection, in degrees per second.")]
        private float _stickPitchSpeed = 120f;

        [SerializeField]
        [Min(1f)]
        [Tooltip("Gamepad response curve: stick deflection is raised to this power, so small deflections aim finely and " +
                 "full deflection still turns at full speed (1 = linear).")]
        private float _stickResponseExponent = 1.8f;

        [SerializeField]
        [Range(0f, 0.5f)]
        [Tooltip("Gamepad: stick deflection below this is ignored (drift).")]
        private float _stickDeadZone = 0.12f;

        [SerializeField]
        [Tooltip("Invert vertical look (pushing up looks down).")]
        private bool _invertY;

        [Header("Auto-align (gamepad only)")]
        [SerializeField]
        [Tooltip("When the player last looked with a stick and has left it alone, gently swing the camera behind the " +
                 "direction of travel (Assassin's Creed / God of War exploration behaviour). Mouse players are never auto-rotated.")]
        private bool _autoAlign = true;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Seconds without look input before auto-align starts.")]
        private float _autoAlignDelay = 1.5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Auto-align turn rate, in degrees per second, at a 90-degree difference (slower when nearly aligned).")]
        private float _autoAlignSpeed = 70f;

        [SerializeField]
        [Range(0f, 180f)]
        [Tooltip("Auto-align ignores travel more than this many degrees from the camera's facing, so running toward the " +
                 "camera never spins it around.")]
        private float _autoAlignMaxAngle = 120f;

        [Header("Cursor")]
        [SerializeField]
        [Tooltip("Lock and hide the cursor during play so the mouse drives the camera. Escape releases it.")]
        private bool _lockCursor = true;

        private Transform _player;
        private PlayerInputBridge _input;
        private float _yaw;
        private float _pitch;
        private Vector3 _velocity;
        private bool _placed;
        private bool _stickWasLastLook;
        private float _lastLookTime = float.NegativeInfinity;
        private Vector3 _lastPlayerPosition;

        /// <summary>Current yaw, in degrees.</summary>
        public float Yaw => _yaw;

        /// <summary>Current pitch, in degrees.</summary>
        public float Pitch => _pitch;

        /// <summary>Rotates the orbit by the given degrees (pitch clamped). Input drives this; tools and tests may too.</summary>
        public void Orbit(float yawDegrees, float pitchDegrees)
        {
            _yaw = Mathf.Repeat(_yaw + yawDegrees, 360f);
            _pitch = Mathf.Clamp(_pitch + pitchDegrees, _minPitch, _maxPitch);
        }

        private void Awake()
        {
            _yaw = _initialYaw;
            _pitch = Mathf.Clamp(_initialPitch, _minPitch, _maxPitch);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void OnEnable()
        {
            if (_lockCursor && Application.isPlaying)
            {
                SetCursorLocked(true);
            }
        }

        private void OnDisable()
        {
            if (_lockCursor)
            {
                SetCursorLocked(false);
            }
        }

        private void Update()
        {
            if (_player == null)
            {
                GameObject found = GameObject.FindGameObjectWithTag(_playerTag);
                if (found != null)
                {
                    _player = found.transform;
                    _input = found.GetComponent<PlayerInputBridge>();
                }
            }

            UpdateCursor();
            if (_input == null || (_lockCursor && Cursor.lockState != CursorLockMode.Locked && _input.IsLookFromPointer))
            {
                return;
            }

            Vector2 look = _input.LookInput;
            float invert = _invertY ? -1f : 1f;
            if (_input.IsLookFromPointer)
            {
                if (look.sqrMagnitude > 0f)
                {
                    _stickWasLastLook = false;
                    _lastLookTime = Time.time;
                }

                Orbit(look.x * _mouseDegreesPerPixel, -look.y * _mouseDegreesPerPixel * invert);
            }
            else
            {
                look = ShapeStick(look);
                if (look.sqrMagnitude > 0f)
                {
                    _stickWasLastLook = true;
                    _lastLookTime = Time.time;
                }

                Orbit(look.x * _stickYawSpeed * Time.deltaTime, -look.y * _stickPitchSpeed * Time.deltaTime * invert);
            }
        }

        // Radial dead zone, then a power curve on the remaining deflection: fine control near centre, full speed at the rim.
        private Vector2 ShapeStick(Vector2 stick)
        {
            float magnitude = stick.magnitude;
            if (magnitude <= _stickDeadZone)
            {
                return Vector2.zero;
            }

            float normalized = Mathf.Clamp01((magnitude - _stickDeadZone) / (1f - _stickDeadZone));
            return stick / magnitude * Mathf.Pow(normalized, _stickResponseExponent);
        }

        private void AutoAlign(Vector3 planarVelocity)
        {
            if (!_autoAlign || !_stickWasLastLook || Time.time - _lastLookTime < _autoAlignDelay || planarVelocity.sqrMagnitude < 1f)
            {
                return;
            }

            float travelYaw = Mathf.Atan2(planarVelocity.x, planarVelocity.z) * Mathf.Rad2Deg;
            float difference = Mathf.DeltaAngle(_yaw, travelYaw);
            if (Mathf.Abs(difference) > _autoAlignMaxAngle)
            {
                return;
            }

            float rate = _autoAlignSpeed * Mathf.Clamp01(Mathf.Abs(difference) / 90f);
            _yaw = Mathf.Repeat(Mathf.MoveTowardsAngle(_yaw, travelYaw, rate * Time.deltaTime), 360f);
        }

        // After the player has moved this frame, so the camera never lags a frame behind the body.
        private void LateUpdate()
        {
            if (_player == null)
            {
                return;
            }

            if (_placed && Time.deltaTime > 0f)
            {
                Vector3 moved = _player.position - _lastPlayerPosition;
                AutoAlign(new Vector3(moved.x, 0f, moved.z) / Time.deltaTime);
            }

            _lastPlayerPosition = _player.position;
            Vector3 target = _player.position + Vector3.up * _height;
            if (!_placed || _followSmoothing <= 0f)
            {
                transform.position = target;
                _placed = true;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(transform.position, target, ref _velocity, _followSmoothing);
            }

            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void UpdateCursor()
        {
            if (!_lockCursor)
            {
                return;
            }

#if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                SetCursorLocked(false);
            }
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                SetCursorLocked(true);
            }
#endif
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
