using UnityEngine;

namespace ToyChest.Gameplay.Player
{
    /// <summary>
    /// A minimal third-person follow camera: it trails a target at a fixed offset and looks at it,
    /// with light positional damping for smoothness. Deliberately basic — it establishes the camera
    /// foundation for the vertical slice without owning any gameplay. Orbit/aim control, collision
    /// avoidance, and a Cinemachine-driven rig are later work and can replace this with no engine
    /// impact, since movement reads the active camera's facing rather than this component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThirdPersonCameraRig : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The transform this camera follows (the player).")]
        private Transform _target;

        [SerializeField]
        [Tooltip("Offset from the target, in world space: behind and above for a third-person view.")]
        private Vector3 _offset = new Vector3(0f, 6f, -8f);

        [SerializeField]
        [Tooltip("Follow smoothing time in seconds. Zero snaps directly to the target.")]
        private float _smoothTime = 0.12f;

        private Vector3 _velocity;

        /// <summary>Assigns the follow target at runtime (e.g. after the player is composed).</summary>
        public void SetTarget(Transform target)
        {
            _target = target;
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                return;
            }

            Vector3 desired = _target.position + _offset;
            transform.position = _smoothTime > 0f
                ? Vector3.SmoothDamp(transform.position, desired, ref _velocity, _smoothTime)
                : desired;

            transform.LookAt(_target.position);
        }
    }
}
