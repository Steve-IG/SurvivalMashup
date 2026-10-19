using System.Collections;
using ToyChest.Gameplay.Presentation;
using UnityEngine;

namespace ToyChest.Gameplay.Player
{
    /// <summary>
    /// The thin presentation adapter that turns the player's swing and impact into juice. It consumes
    /// <see cref="PlayerCombat"/>'s existing event seams only and owns no gameplay: a swing plays a whoosh
    /// and the connect fires an impact camera impulse, spawns a one-shot impact VFX at the hit point, plays
    /// an impact sound, and briefly pauses time (hit-stop) for weight. Every knob is authored; anything
    /// left unassigned is simply skipped, so the same adapter degrades cleanly to a hook. Nothing here
    /// feeds back into gameplay — the enemy's own hit flash already fires from the damage it takes, now
    /// naturally synced to the contact frame because damage lands at impact.
    ///
    /// <para><b>One beat, one hit-stop.</b> <see cref="PlayerCombat.Impacted"/> fires once per target, and a swept limb
    /// reaches each body on a different frame — so per-impact hit-stop and shake turned one kick through a crowd into a
    /// chain of separate freezes (measured: six in 0.4s) and a stacked camera jolt. The weighty juice (hit-stop, camera
    /// impulse) now fires once per <i>beat</i> — one hit window of one step (a contact event for steps without an
    /// attack asset) — while every target still gets its own impact VFX and sound at its own contact point.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAttackFeedback : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Combat adapter whose swing/impact events drive the juice. Defaults to the sibling.")]
        private PlayerCombat _combat;

        [SerializeField]
        [Tooltip("Camera-shake impulse source, fired on a connected hit. Defaults to the sibling.")]
        private CombatImpulse _impulse;

        [Header("Impact VFX")]
        [SerializeField]
        [Tooltip("One-shot particle burst spawned at the hit point when a swing connects. Optional.")]
        private GameObject _impactVfx;

        [SerializeField]
        [Tooltip("Extra world-space nudge on the hit point (already at body height from PlayerCombat).")]
        private Vector3 _impactVfxOffset = Vector3.zero;

        [SerializeField]
        [Tooltip("Seconds before a spawned impact VFX is destroyed.")]
        private float _impactVfxLifetime = 2f;

        [Header("Camera")]
        [SerializeField]
        [Tooltip("Camera-shake force when a swing connects (stronger than the swing-start shake).")]
        private float _impactShake = 0.35f;

        [Header("Sound (optional hooks)")]
        [SerializeField]
        [Tooltip("Audio source used for swing/impact sounds. Defaults to the sibling if present.")]
        private AudioSource _audioSource;

        [SerializeField]
        [Tooltip("Whoosh played when a swing starts. Optional.")]
        private AudioClip _swingSound;

        [SerializeField]
        [Tooltip("Impact sound played when a swing connects. Optional.")]
        private AudioClip _impactSound;

        [Header("Hit pause (local presentation feel)")]
        [SerializeField]
        [Tooltip("Real-time seconds to briefly slow time on a connected hit for weight. Zero disables.")]
        private float _hitStopDuration = 0.05f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Time scale held during the hit-stop (0 = frozen).")]
        private float _hitStopScale = 0.06f;

        private bool _bound;
        private Coroutine _hitStopRoutine;

        // The current beat: bumped when a step starts, keyed by the window that struck (WindowStruck precedes Impacted).
        private int _stepSerial;
        private string _beatWindow;
        private string _feltBeat;

        private void Awake()
        {
            if (_combat == null)
            {
                _combat = GetComponent<PlayerCombat>();
            }

            if (_impulse == null)
            {
                _impulse = GetComponent<CombatImpulse>();
            }

            if (_audioSource == null)
            {
                _audioSource = GetComponent<AudioSource>();
            }
        }

        private void OnEnable()
        {
            if (_combat != null && !_bound)
            {
                _combat.Attacked += OnSwing;
                _combat.WindowStruck += OnWindowStruck;
                _combat.Impacted += OnImpact;
                _bound = true;
            }
        }

        private void OnDisable()
        {
            if (_combat != null && _bound)
            {
                _combat.Attacked -= OnSwing;
                _combat.WindowStruck -= OnWindowStruck;
                _combat.Impacted -= OnImpact;
                _bound = false;
            }

            // Never leave time scaled if we are torn down mid hit-stop.
            if (_hitStopRoutine != null)
            {
                Time.timeScale = 1f;
                _hitStopRoutine = null;
            }
        }

        private void OnWindowStruck(string windowId) => _beatWindow = windowId;

        private void OnSwing(string animatorTrigger)
        {
            _stepSerial++;
            _beatWindow = null;

            if (_swingSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_swingSound);
            }
        }

        private void OnImpact(Vector3 hitPoint)
        {
            // The first target of a beat carries its weight; later targets of the same beat add only their own VFX/sound.
            string beat = _stepSerial + "/" + (_beatWindow ?? "contact");
            bool firstOfBeat = beat != _feltBeat;
            _feltBeat = beat;

            if (firstOfBeat && _impulse != null)
            {
                _impulse.Shake(_impactShake);
            }

            if (_impactVfx != null)
            {
                GameObject burst = Instantiate(_impactVfx, hitPoint + _impactVfxOffset, Quaternion.identity);
                Destroy(burst, Mathf.Max(0.1f, _impactVfxLifetime));
            }

            if (_impactSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_impactSound);
            }

            if (firstOfBeat && _hitStopDuration > 0f && isActiveAndEnabled)
            {
                if (_hitStopRoutine != null)
                {
                    StopCoroutine(_hitStopRoutine);
                    Time.timeScale = 1f;
                }

                _hitStopRoutine = StartCoroutine(HitStop());
            }
        }

        private IEnumerator HitStop()
        {
            Time.timeScale = Mathf.Clamp01(_hitStopScale);
            yield return new WaitForSecondsRealtime(_hitStopDuration);
            Time.timeScale = 1f;
            _hitStopRoutine = null;
        }
    }
}
