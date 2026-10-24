using ToyChest.Gameplay.Combat;
using UnityEngine;

namespace ToyChest.Gameplay.Player
{
    /// <summary>
    /// A thin presentation adapter that shows exactly where an ability attack will strike: a flat, semi-transparent
    /// disc with a solid rim, drawn on the ground under the live centre of the attack's area, at its radius (read from
    /// <see cref="PlayerCombat.TryGetAbilityAttackArea"/> — the same emitter the hit detection queries, so the circle
    /// and the damage can never disagree). It appears the moment the ability attack starts, follows the area through
    /// the animation, brightens on the frame the area goes live, and disappears when the attack ends. It owns no
    /// gameplay; removing it changes no behaviour.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAbilityAreaIndicator : MonoBehaviour
    {
        private const int Segments = 72;

        [SerializeField]
        [Tooltip("Combat adapter whose ability-attack area is shown. Defaults to the sibling.")]
        private PlayerCombat _combat;

        [SerializeField]
        [Tooltip("Material for the disc (needs a transparent shader that uses vertex colours). Empty = Sprites/Default.")]
        private Material _material;

        [SerializeField]
        [Tooltip("Indicator colour; the fill and rim alphas below are applied on top.")]
        private Color _color = new Color(1f, 0.55f, 0.15f, 1f);

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Opacity at the centre of the disc (the fill brightens toward the rim).")]
        private float _centerAlpha = 0.08f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Opacity of the fill just inside the rim.")]
        private float _edgeAlpha = 0.3f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Opacity of the rim that marks the exact edge of the area.")]
        private float _rimAlpha = 0.9f;

        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Rim width, in metres.")]
        private float _rimWidth = 0.08f;

        [SerializeField]
        [Tooltip("Height above the ground the disc is drawn at, to avoid z-fighting.")]
        private float _groundOffset = 0.03f;

        [SerializeField]
        [Tooltip("Seconds the disc stays brightened after the area goes live (the impact frame).")]
        private float _impactFlashDuration = 0.2f;

        private Transform _disc;
        private Mesh _mesh;
        private Material _ownedMaterial;
        private Color32[] _colors;
        private float _builtRadius = -1f;
        private float _flashTimer;

        private void Awake()
        {
            if (_combat == null)
            {
                _combat = GetComponent<PlayerCombat>();
            }

            Material material = _material;
            if (material == null)
            {
                _ownedMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
                material = _ownedMaterial;
            }

            _mesh = new Mesh { name = "AbilityAreaIndicator", hideFlags = HideFlags.HideAndDontSave };
            var disc = new GameObject("~AbilityAreaIndicator");
            disc.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var meshRenderer = disc.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            disc.SetActive(false);
            _disc = disc.transform;
        }

        private void OnEnable()
        {
            if (_combat != null)
            {
                _combat.AreaActivated += OnAreaActivated;
            }
        }

        private void OnDisable()
        {
            if (_combat != null)
            {
                _combat.AreaActivated -= OnAreaActivated;
            }

            if (_disc != null)
            {
                _disc.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_disc != null)
            {
                Destroy(_disc.gameObject);
            }

            if (_mesh != null)
            {
                Destroy(_mesh);
            }

            if (_ownedMaterial != null)
            {
                Destroy(_ownedMaterial);
            }
        }

        private void OnAreaActivated(HitWindow window, Vector3 center, float radius)
        {
            if (_combat.IsPerformingAbilityAttack)
            {
                _flashTimer = _impactFlashDuration;
            }
        }

        // After animation and PlayerCombat's own LateUpdate, so the disc sits on this frame's pose.
        private void LateUpdate()
        {
            if (_combat == null || !_combat.TryGetAbilityAttackArea(out Vector3 center, out float radius) || radius <= 0f)
            {
                if (_disc.gameObject.activeSelf)
                {
                    _disc.gameObject.SetActive(false);
                }

                _flashTimer = 0f;
                return;
            }

            if (!Mathf.Approximately(radius, _builtRadius))
            {
                Build(radius);
            }

            _flashTimer = Mathf.Max(0f, _flashTimer - Time.unscaledDeltaTime);
            ApplyColors(_impactFlashDuration > 0f ? _flashTimer / _impactFlashDuration : 0f);

            _disc.SetPositionAndRotation(_combat.ToCharacterGround(center) + Vector3.up * _groundOffset, Quaternion.identity);
            if (!_disc.gameObject.activeSelf)
            {
                _disc.gameObject.SetActive(true);
            }
        }

        // A flat disc in world metres: a centre vertex, the fill's outer ring, then the rim's inner and outer rings.
        private void Build(float radius)
        {
            _builtRadius = radius;
            float rimInner = Mathf.Max(0f, radius - _rimWidth);
            float[] rings = { rimInner, rimInner, radius };

            var vertices = new Vector3[1 + rings.Length * Segments];
            vertices[0] = Vector3.zero;
            for (int r = 0; r < rings.Length; r++)
            {
                for (int i = 0; i < Segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / Segments;
                    vertices[1 + r * Segments + i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * rings[r];
                }
            }

            // Fill fan (centre → ring 0) plus the rim band (ring 1 → ring 2). Ring 1 duplicates ring 0's positions so
            // the fill's soft edge and the rim's solid band get separate colours.
            var triangles = new int[Segments * 3 + Segments * 6];
            int t = 0;
            for (int i = 0; i < Segments; i++)
            {
                int next = (i + 1) % Segments;
                triangles[t++] = 0;
                triangles[t++] = 1 + next;
                triangles[t++] = 1 + i;

                int inner = 1 + Segments + i, innerNext = 1 + Segments + next;
                int outer = 1 + 2 * Segments + i, outerNext = 1 + 2 * Segments + next;
                triangles[t++] = inner;
                triangles[t++] = innerNext;
                triangles[t++] = outer;
                triangles[t++] = innerNext;
                triangles[t++] = outerNext;
                triangles[t++] = outer;
            }

            _mesh.Clear();
            _mesh.vertices = vertices;
            _mesh.triangles = triangles;
            _mesh.RecalculateBounds();
            _colors = new Color32[vertices.Length];
        }

        // The fill brightens toward the rim; the impact flash lifts every alpha toward opaque for a moment.
        private void ApplyColors(float flash)
        {
            Color32 center = WithAlpha(_centerAlpha, flash);
            Color32 edge = WithAlpha(_edgeAlpha, flash);
            Color32 rim = WithAlpha(_rimAlpha, flash);
            _colors[0] = center;
            for (int i = 0; i < Segments; i++)
            {
                _colors[1 + i] = edge;
                _colors[1 + Segments + i] = rim;
                _colors[1 + 2 * Segments + i] = rim;
            }

            _mesh.colors32 = _colors;
        }

        private Color32 WithAlpha(float alpha, float flash)
        {
            Color c = _color;
            c.a = Mathf.Lerp(alpha, Mathf.Min(1f, alpha + 0.5f), Mathf.Clamp01(flash));
            return c;
        }
    }
}
