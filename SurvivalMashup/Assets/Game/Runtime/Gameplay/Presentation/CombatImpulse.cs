using Unity.Cinemachine;
using UnityEngine;

namespace ToyChest.Gameplay.Presentation
{
    /// <summary>
    /// A thin wrapper over a <see cref="CinemachineImpulseSource"/> so gameplay presentation can add
    /// camera shake without touching the camera rig directly — the shake propagates to whichever
    /// Cinemachine camera carries an impulse listener. Callers (the animator drivers) invoke
    /// <see cref="Shake"/> on combat events; it owns no gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CinemachineImpulseSource))]
    public sealed class CombatImpulse : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The impulse source that emits the shake. Defaults to the sibling.")]
        private CinemachineImpulseSource _source;

        private void Awake()
        {
            if (_source == null)
            {
                _source = GetComponent<CinemachineImpulseSource>();
            }
        }

        /// <summary>Emits a camera-shake impulse scaled by <paramref name="force"/>.</summary>
        public void Shake(float force)
        {
            if (_source != null)
            {
                _source.GenerateImpulseWithForce(force);
            }
        }
    }
}
