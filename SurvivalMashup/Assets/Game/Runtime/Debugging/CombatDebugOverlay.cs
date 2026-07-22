using System.Collections.Generic;
using ToyChest.Gameplay.HitDetection;
using ToyChest.Gameplay.Player;
using UnityEngine;

namespace ToyChest.Debugging
{
    /// <summary>
    /// Debug tooling for combat feel iteration: it draws the player's authored HitVolume (reach, facing,
    /// cone arc or sphere radius), marks the exact contact point of confirmed hits, and shows a small HUD
    /// with the swing's phase, hit confirmation, targets hit, cooldown, and the volume's shape.
    ///
    /// <b>Debug only, and strictly a reader.</b> Gameplay has no dependency on this component: nothing in
    /// the combat path knows it exists, it never mutates state, and deleting it changes no behaviour. It
    /// consumes the same public event seams presentation uses (<see cref="PlayerCombat.Attacked"/>,
    /// <see cref="PlayerCombat.Contacted"/>, <see cref="PlayerCombat.Impacted"/>) plus read-only accessors.
    ///
    /// Visible in both views: Gizmos cover the Scene view, and an immediate-mode GL pass covers the Game
    /// view (Gizmos do not render there). Toggle at runtime with F2, or per-instance in the Inspector; the
    /// switch is the global <see cref="Enabled"/> so the drawing and the HUD turn on together.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatDebugOverlay : MonoBehaviour
    {
        /// <summary>Global debug switch shared by the visualization and the HUD.</summary>
        public static bool Enabled { get; set; }

        [SerializeField]
        [Tooltip("Player combat adapter being visualised. Defaults to the one in the scene.")]
        private PlayerCombat _combat;

        [SerializeField]
        [Tooltip("Whether combat debug drawing is on when play begins. Press F2 to toggle at runtime.")]
        private bool _enabledOnStart;

        [SerializeField]
        [Tooltip("Draw the HUD readout (phase, hits, cooldown) alongside the volume visualization.")]
        private bool _showHud = true;

        [SerializeField]
        [Tooltip("How many recent confirmed hit locations to keep on screen.")]
        private int _hitHistory = 8;

        [SerializeField]
        [Tooltip("Seconds a confirmed-hit marker stays visible.")]
        private float _hitMarkerLifetime = 2f;

        private static readonly Color VolumeColor = new Color(0.2f, 0.8f, 1f, 0.9f);
        private static readonly Color FacingColor = new Color(1f, 0.9f, 0.2f, 0.9f);
        private static readonly Color HitColor = new Color(1f, 0.25f, 0.2f, 1f);

        private readonly List<TimedPoint> _hits = new List<TimedPoint>();
        private Material _lines;
        private bool _bound;
        private float _lastContactTime = -99f;
        private float _lastHitTime = -99f;

        private void Awake()
        {
            Enabled = _enabledOnStart;
            if (_combat == null)
            {
                _combat = FindAnyObjectByType<PlayerCombat>();
            }
        }

        private void OnEnable()
        {
            if (_combat != null && !_bound)
            {
                _combat.Contacted += OnContacted;
                _combat.Impacted += OnImpacted;
                _bound = true;
            }
        }

        private void OnDisable()
        {
            if (_combat != null && _bound)
            {
                _combat.Contacted -= OnContacted;
                _combat.Impacted -= OnImpacted;
                _bound = false;
            }
        }

        private void Update()
        {
            if (TogglePressed())
            {
                Enabled = !Enabled;
            }

            // The player may be composed after this overlay awakes (scene load order), so keep looking
            // until it exists rather than staying dead for the session.
            if (_combat == null)
            {
                _combat = FindAnyObjectByType<PlayerCombat>();
                if (_combat != null && !_bound)
                {
                    _combat.Contacted += OnContacted;
                    _combat.Impacted += OnImpacted;
                    _bound = true;
                }
            }

            // Age out old hit markers.
            for (int i = _hits.Count - 1; i >= 0; i--)
            {
                if (Time.time - _hits[i].Time > _hitMarkerLifetime)
                {
                    _hits.RemoveAt(i);
                }
            }
        }

        // Reads the F2 toggle through whichever input backend is active, matching GameplayDebugOverlay.
        private static bool TogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null && keyboard.f2Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.F2);
#else
            return false;
#endif
        }

        private void OnContacted()
        {
            _lastContactTime = Time.time;
        }

        private void OnImpacted(Vector3 point)
        {
            _lastHitTime = Time.time;
            _hits.Add(new TimedPoint(point, Time.time));
            while (_hits.Count > Mathf.Max(1, _hitHistory))
            {
                _hits.RemoveAt(0);
            }
        }

        // --- Scene view -------------------------------------------------------------------------------

        private void OnDrawGizmos()
        {
            if (!Enabled || !TryGetVolume(out HitVolume volume, out Vector3 origin, out Vector3 forward))
            {
                return;
            }

            Gizmos.color = VolumeColor;
            DrawVolumeGizmo(volume, origin, forward);

            Gizmos.color = FacingColor;
            Gizmos.DrawLine(origin, origin + Flat(forward) * volume.Radius);

            Gizmos.color = HitColor;
            for (int i = 0; i < _hits.Count; i++)
            {
                Gizmos.DrawWireSphere(_hits[i].Position, 0.12f);
            }
        }

        private static void DrawVolumeGizmo(in HitVolume volume, Vector3 origin, Vector3 forward)
        {
            if (volume.Shape == HitShape.Sphere)
            {
                Gizmos.DrawWireSphere(origin, volume.Radius);
                return;
            }

            float half = volume.ConeHalfAngleDegrees;
            Vector3 flat = Flat(forward);
            Vector3 previous = origin + Quaternion.AngleAxis(-half, Vector3.up) * flat * volume.Radius;
            Gizmos.DrawLine(origin, previous);

            const int segments = 24;
            for (int i = 1; i <= segments; i++)
            {
                float angle = Mathf.Lerp(-half, half, i / (float)segments);
                Vector3 point = origin + Quaternion.AngleAxis(angle, Vector3.up) * flat * volume.Radius;
                Gizmos.DrawLine(previous, point);
                previous = point;
            }

            Gizmos.DrawLine(origin, previous);
        }

        // --- Game view (Gizmos do not render here, so draw immediate-mode lines) -----------------------

        private void OnRenderObject()
        {
            if (!Enabled || !TryGetVolume(out HitVolume volume, out Vector3 origin, out Vector3 forward))
            {
                return;
            }

            EnsureMaterial();
            _lines.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.LINES);

            GL.Color(VolumeColor);
            if (volume.Shape == HitShape.Sphere)
            {
                DrawCircleGL(origin, volume.Radius);
            }
            else
            {
                DrawArcGL(origin, Flat(forward), volume.Radius, volume.ConeHalfAngleDegrees);
            }

            GL.Color(FacingColor);
            Line(origin, origin + Flat(forward) * volume.Radius);

            GL.Color(HitColor);
            for (int i = 0; i < _hits.Count; i++)
            {
                DrawMarkerGL(_hits[i].Position, 0.15f);
            }

            GL.End();
            GL.PopMatrix();
        }

        private void EnsureMaterial()
        {
            if (_lines != null)
            {
                return;
            }

            var shader = Shader.Find("Hidden/Internal-Colored");
            _lines = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _lines.SetInt("_ZWrite", 0);
            _lines.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        }

        private static void DrawCircleGL(Vector3 centre, float radius)
        {
            const int segments = 48;
            Vector3 previous = centre + Vector3.forward * radius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = 360f * i / segments;
                Vector3 point = centre + Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward * radius;
                Line(previous, point);
                previous = point;
            }
        }

        private static void DrawArcGL(Vector3 origin, Vector3 forward, float radius, float halfAngle)
        {
            const int segments = 32;
            Vector3 first = origin + Quaternion.AngleAxis(-halfAngle, Vector3.up) * forward * radius;
            Vector3 previous = first;
            Line(origin, first);

            for (int i = 1; i <= segments; i++)
            {
                float angle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)segments);
                Vector3 point = origin + Quaternion.AngleAxis(angle, Vector3.up) * forward * radius;
                Line(previous, point);
                previous = point;
            }

            Line(origin, previous);
        }

        private static void DrawMarkerGL(Vector3 point, float size)
        {
            Line(point + Vector3.left * size, point + Vector3.right * size);
            Line(point + Vector3.up * size, point + Vector3.down * size);
            Line(point + Vector3.forward * size, point + Vector3.back * size);
        }

        private static void Line(Vector3 a, Vector3 b)
        {
            GL.Vertex(a);
            GL.Vertex(b);
        }

        // --- HUD --------------------------------------------------------------------------------------

        private void OnGUI()
        {
            if (!Enabled || !_showHud || _combat == null)
            {
                return;
            }

            bool justContacted = Time.time - _lastContactTime < 0.25f;
            bool justHit = Time.time - _lastHitTime < 0.25f;

            GUILayout.BeginArea(new Rect(Screen.width - 300f, 10f, 290f, 190f), GUI.skin.box);
            GUILayout.Label("<b>Combat Debug</b> (F2)");
            GUILayout.Label($"Phase: {_combat.Phase}");
            GUILayout.Label($"Contact frame: {(justContacted ? "YES" : "-")}");
            GUILayout.Label($"Hit confirmed: {(justHit ? "YES" : "-")}");
            GUILayout.Label($"Targets hit (last contact): {_combat.LastTargetsHit}");
            GUILayout.Label($"Cooldown: {_combat.AttackCooldownRemaining:0.00}s");

            HitVolumeEmitter emitter = _combat.PrimaryAttackVolume;
            if (emitter != null)
            {
                HitVolume volume = emitter.Volume;
                string shape = volume.Shape == HitShape.Cone
                    ? $"Cone r={volume.Radius:0.0} ±{volume.ConeHalfAngleDegrees:0}°"
                    : $"Sphere r={volume.Radius:0.0}";
                GUILayout.Label($"Hit volume: {shape}");
            }

            GUILayout.EndArea();
        }

        private bool TryGetVolume(out HitVolume volume, out Vector3 origin, out Vector3 forward)
        {
            volume = default;
            origin = default;
            forward = Vector3.forward;

            HitVolumeEmitter emitter = _combat != null ? _combat.PrimaryAttackVolume : null;
            if (emitter == null || !emitter.IsBound)
            {
                return false;
            }

            volume = emitter.Volume;
            origin = emitter.Origin;
            forward = emitter.Forward;
            return true;
        }

        private static Vector3 Flat(Vector3 v)
        {
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            return flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.forward;
        }

        private readonly struct TimedPoint
        {
            public TimedPoint(Vector3 position, float time)
            {
                Position = position;
                Time = time;
            }

            public Vector3 Position { get; }

            public float Time { get; }
        }
    }
}
