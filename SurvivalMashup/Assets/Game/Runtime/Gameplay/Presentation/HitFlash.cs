using System.Collections.Generic;
using UnityEngine;

namespace ToyChest.Gameplay.Presentation
{
    /// <summary>
    /// A self-contained presentation utility: briefly tints a character's renderers toward a flash
    /// colour so a hit reads instantly. It owns no gameplay — callers (the animator drivers, which
    /// already watch the health resource) invoke <see cref="Flash"/>. The tint is applied through a
    /// <see cref="MaterialPropertyBlock"/> so it never mutates shared material assets, and it lerps
    /// from each renderer's own base colour so the character's normal look is restored exactly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitFlash : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Colour the character briefly tints toward when hit.")]
        private Color _flashColor = new Color(1f, 0.25f, 0.2f);

        [SerializeField]
        [Tooltip("How long the flash lasts, in seconds.")]
        private float _duration = 0.14f;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Peak strength of the tint (0 = none, 1 = full flash colour).")]
        private float _strength = 0.8f;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly List<Color> _originals = new List<Color>();
        private MaterialPropertyBlock _mpb;
        private float _timer;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r.sharedMaterial == null || !r.sharedMaterial.HasProperty(BaseColor))
                {
                    continue;
                }

                _renderers.Add(r);
                _originals.Add(r.sharedMaterial.GetColor(BaseColor));
            }
        }

        /// <summary>Starts (or restarts) the hit flash.</summary>
        public void Flash()
        {
            _timer = _duration;
        }

        private void Update()
        {
            if (_timer <= 0f)
            {
                return;
            }

            _timer -= Time.deltaTime;
            float t = _duration > 0f ? Mathf.Clamp01(_timer / _duration) : 0f;
            float amount = _strength * t;

            for (int i = 0; i < _renderers.Count; i++)
            {
                Renderer r = _renderers[i];
                if (r == null)
                {
                    continue;
                }

                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColor, Color.Lerp(_originals[i], _flashColor, amount));
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
