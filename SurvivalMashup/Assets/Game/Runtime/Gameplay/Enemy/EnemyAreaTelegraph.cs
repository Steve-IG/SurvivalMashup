using UnityEngine;

namespace ToyChest.Gameplay.Enemy
{
    /// <summary>
    /// A thin presentation adapter that telegraphs an enemy's signature area attack on the ground: a faint disc at the
    /// exact radius with a solid rim, and an inner disc that grows from the centre to the rim over the wind-up, so the
    /// player can read both <em>where</em> the blow will land and <em>when</em> (it lands the instant the inner disc
    /// meets the rim). The circle comes from <see cref="EnemyCombatant.TryGetSignatureArea"/> — the same centre and
    /// radius the hit query uses. On the frame the area lands it flashes and spawns an optional ground burst, hit or
    /// miss. It owns no gameplay; removing it changes no behaviour.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyCombatant))]
    public sealed class EnemyAreaTelegraph : MonoBehaviour
    {
        private const int Segments = 64;

        [SerializeField]
        [Tooltip("The combatant whose signature attack is telegraphed. Defaults to the sibling.")]
        private EnemyCombatant _combatant;

        [SerializeField]
        [Tooltip("Telegraph colour; the alphas below are applied on top.")]
        private Color _color = new Color(0.95f, 0.15f, 0.1f, 1f);

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Opacity of the faint disc covering the whole area.")]
        private float _areaAlpha = 0.12f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Opacity of the inner disc that grows to the rim as the wind-up completes.")]
        private float _fillAlpha = 0.35f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Opacity of the rim marking the exact edge of the area.")]
        private float _rimAlpha = 0.95f;

        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Rim width, in metres.")]
        private float _rimWidth = 0.12f;

        [SerializeField]
        [Tooltip("Height above the ground the telegraph is drawn at, to avoid z-fighting.")]
        private float _groundOffset = 0.04f;

        [SerializeField]
        [Tooltip("Seconds the full disc stays lit after the area lands.")]
        private float _impactFlashSeconds = 0.25f;

        [SerializeField]
        [Tooltip("Optional ground burst spawned where the area lands (hit or miss).")]
        private GameObject _impactEffect;

        [SerializeField]
        [Min(0.01f)]
        [Tooltip("The burst prefab's natural radius at scale 1, so it is scaled to match the area.")]
        private float _impactEffectRadius = 1.5f;

        private Material _material;
        private Mesh _discMesh;
        private Mesh _rimMesh;
        private float _rimBuiltRadius = -1f;
        private Transform _area;
        private Transform _fill;
        private Transform _rim;
        private MeshRenderer _areaRenderer;
        private MeshRenderer _fillRenderer;
        private MeshRenderer _rimRenderer;
        private MaterialPropertyBlock _block;
        private float _flashTimer;
        private Vector3 _flashCenter;
        private float _flashRadius;

        /// <summary>Whether the telegraph is currently drawn (wind-up or impact flash). Probes read this.</summary>
        public bool IsShowing => _area != null && _area.gameObject.activeSelf;

        /// <summary>The fraction of the area the growing inner disc currently covers (0 → 1). Probes read this.</summary>
        public float FillFraction { get; private set; }

        private void Awake()
        {
            if (_combatant == null)
            {
                _combatant = GetComponent<EnemyCombatant>();
            }

            _material = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
            _block = new MaterialPropertyBlock();
            _discMesh = BuildDisc();
            _area = CreatePart("~SignatureArea", _discMesh, out _areaRenderer);
            _fill = CreatePart("~SignatureFill", _discMesh, out _fillRenderer);
            _rimMesh = new Mesh { name = "SignatureRim", hideFlags = HideFlags.HideAndDontSave };
            _rim = CreatePart("~SignatureRim", _rimMesh, out _rimRenderer);
            Show(false);
        }

        private void OnEnable()
        {
            if (_combatant != null)
            {
                _combatant.SignatureStruck += OnStruck;
            }
        }

        private void OnDisable()
        {
            if (_combatant != null)
            {
                _combatant.SignatureStruck -= OnStruck;
            }

            Show(false);
        }

        private void OnDestroy()
        {
            if (_area != null) Destroy(_area.gameObject);
            if (_fill != null) Destroy(_fill.gameObject);
            if (_rim != null) Destroy(_rim.gameObject);
            if (_discMesh != null) Destroy(_discMesh);
            if (_rimMesh != null) Destroy(_rimMesh);
            if (_material != null) Destroy(_material);
        }

        private void OnStruck(Vector3 groundCenter, float radius, int damaged)
        {
            _flashTimer = _impactFlashSeconds;
            _flashCenter = groundCenter;
            _flashRadius = radius;

            if (_impactEffect != null)
            {
                GameObject burst = Instantiate(_impactEffect, groundCenter, Quaternion.identity);
                burst.transform.localScale = Vector3.one * (radius / _impactEffectRadius);
                Destroy(burst, 4f);
            }
        }

        // After the combatant's Update, so the telegraph sits on this frame's position.
        private void LateUpdate()
        {
            if (_combatant != null && _combatant.TryGetSignatureArea(out Vector3 center, out float radius, out float progress))
            {
                Draw(center, radius, progress, flash: 0f);
                return;
            }

            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                Draw(_flashCenter, _flashRadius, 1f, flash: Mathf.Clamp01(_flashTimer / Mathf.Max(0.01f, _impactFlashSeconds)));
                return;
            }

            FillFraction = 0f;
            Show(false);
        }

        private void Draw(Vector3 groundCenter, float radius, float progress, float flash)
        {
            if (radius <= 0f)
            {
                Show(false);
                return;
            }

            if (!Mathf.Approximately(radius, _rimBuiltRadius))
            {
                BuildRim(radius);
            }

            Vector3 position = groundCenter + Vector3.up * _groundOffset;
            _area.SetPositionAndRotation(position, Quaternion.identity);
            _area.localScale = new Vector3(radius, 1f, radius);

            // The fill sits a hair above the area disc so the two never z-fight.
            float fillRadius = Mathf.Max(0.001f, radius * progress);
            _fill.SetPositionAndRotation(position + Vector3.up * 0.005f, Quaternion.identity);
            _fill.localScale = new Vector3(fillRadius, 1f, fillRadius);
            _rim.SetPositionAndRotation(position + Vector3.up * 0.01f, Quaternion.identity);
            FillFraction = progress;

            Tint(_areaRenderer, Mathf.Lerp(_areaAlpha, 0.6f, flash));
            Tint(_fillRenderer, Mathf.Lerp(_fillAlpha, 0.8f, flash));
            Tint(_rimRenderer, _rimAlpha);
            Show(true);
        }

        private void Tint(Renderer target, float alpha)
        {
            Color c = _color;
            c.a = alpha;
            _block.SetColor("_Color", c);
            target.SetPropertyBlock(_block);
        }

        private void Show(bool visible)
        {
            if (_area == null || _area.gameObject.activeSelf == visible)
            {
                return;
            }

            _area.gameObject.SetActive(visible);
            _fill.gameObject.SetActive(visible);
            _rim.gameObject.SetActive(visible);
        }

        private Transform CreatePart(string partName, Mesh mesh, out MeshRenderer meshRenderer)
        {
            var part = new GameObject(partName);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = part.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            return part.transform;
        }

        // A unit disc (radius 1) in the XZ plane, scaled per frame to the area or the growing fill.
        private static Mesh BuildDisc()
        {
            var vertices = new Vector3[Segments + 1];
            var triangles = new int[Segments * 3];
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                vertices[1 + i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = 1 + (i + 1) % Segments;
                triangles[i * 3 + 2] = 1 + i;
            }

            var mesh = new Mesh { name = "SignatureDisc", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        // The rim is built in world metres (not scaled), so its width stays constant whatever the radius.
        private void BuildRim(float radius)
        {
            _rimBuiltRadius = radius;
            float inner = Mathf.Max(0f, radius - _rimWidth);
            var vertices = new Vector3[Segments * 2];
            var triangles = new int[Segments * 6];
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i] = direction * inner;
                vertices[Segments + i] = direction * radius;

                int next = (i + 1) % Segments;
                int t = i * 6;
                triangles[t] = i;
                triangles[t + 1] = next;
                triangles[t + 2] = Segments + i;
                triangles[t + 3] = next;
                triangles[t + 4] = Segments + next;
                triangles[t + 5] = Segments + i;
            }

            _rimMesh.Clear();
            _rimMesh.vertices = vertices;
            _rimMesh.triangles = triangles;
            _rimMesh.RecalculateBounds();
        }
    }
}
