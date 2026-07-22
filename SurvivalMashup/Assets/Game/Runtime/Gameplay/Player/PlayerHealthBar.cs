using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Systems.Resources;
using UnityEngine;

namespace ToyChest.Gameplay.Player
{
    /// <summary>
    /// A thin presentation adapter that shows the player's Current Health on screen. It reads the composed
    /// object's health resource and its <c>ValueChanged</c> facts — it owns no gameplay, never mutates
    /// health, and adding or removing it changes no behaviour.
    ///
    /// The bar fills to <c>Current / Maximum</c>, so it lowers when damaged and returns to full on its own
    /// when respawn refills the resource (no respawn-specific code here — refilling health <em>is</em> the
    /// signal). A brief flash on the bar marks the moment damage lands, replacing the character-tint hit
    /// flash that was removed: the feedback now lives where the player is already looking for it.
    ///
    /// Drawn with IMGUI so it needs no canvas, prefab, or art. That makes it a deliberate placeholder —
    /// when a real UI system arrives this is replaced by a proper HUD widget bound to the same resource.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHealthBar : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The behaviour whose composed object supplies the health resource. Defaults to the sibling.")]
        private GameplayObjectBehaviour _behaviour;

        [SerializeField]
        [Tooltip("Definition id of the resource displayed.")]
        private string _healthResourceId = "resource.health";

        [Header("Layout")]
        [SerializeField]
        [Tooltip("Bar size in pixels.")]
        private Vector2 _size = new Vector2(260f, 22f);

        [SerializeField]
        [Tooltip("Offset from the bottom-left corner of the screen, in pixels.")]
        private Vector2 _margin = new Vector2(24f, 24f);

        [Header("Colours")]
        [SerializeField]
        private Color _fillColor = new Color(0.16f, 0.78f, 0.32f);

        [SerializeField]
        private Color _backgroundColor = new Color(0f, 0f, 0f, 0.55f);

        [SerializeField]
        [Tooltip("Colour the bar flashes toward when damage lands.")]
        private Color _damageFlashColor = new Color(1f, 0.25f, 0.2f);

        [SerializeField]
        [Tooltip("Seconds the damage flash lasts.")]
        private float _flashDuration = 0.35f;

        private DefinitionId _healthResource;
        private ResourceValue _health;
        private float _flashTimer;
        private Texture2D _pixel;

        private void Awake()
        {
            if (_behaviour == null)
            {
                _behaviour = GetComponent<GameplayObjectBehaviour>();
            }

            _healthResource = new DefinitionId(_healthResourceId);
            _pixel = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.ValueChanged -= OnHealthChanged;
                _health = null;
            }
        }

        private void OnDestroy()
        {
            if (_pixel != null)
            {
                Destroy(_pixel);
            }
        }

        private void Update()
        {
            EnsureBound();

            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.unscaledDeltaTime;
            }
        }

        private void EnsureBound()
        {
            if (_health != null || _behaviour == null || _behaviour.Object == null || !_behaviour.Object.IsActive)
            {
                return;
            }

            if (_behaviour.Object.TryGet(out ResourceSet resources))
            {
                _health = resources.GetResource(_healthResource);
                if (_health != null)
                {
                    _health.ValueChanged += OnHealthChanged;
                }
            }
        }

        private void OnHealthChanged(float previous, float current, float maximum)
        {
            if (current < previous)
            {
                _flashTimer = _flashDuration;
            }
        }

        private void OnGUI()
        {
            if (_health == null || _pixel == null)
            {
                return;
            }

            float maximum = _health.Maximum;
            float fraction = maximum > 0f ? Mathf.Clamp01(_health.Current / maximum) : 0f;

            var background = new Rect(_margin.x, Screen.height - _margin.y - _size.y, _size.x, _size.y);
            var fill = new Rect(background.x, background.y, background.width * fraction, background.height);

            float flash = _flashDuration > 0f ? Mathf.Clamp01(_flashTimer / _flashDuration) : 0f;
            Color fillColor = Color.Lerp(_fillColor, _damageFlashColor, flash);

            Draw(background, _backgroundColor);
            if (fill.width > 0f)
            {
                Draw(fill, fillColor);
            }

            var label = new Rect(background.x + 8f, background.y, background.width, background.height);
            GUI.Label(label, $"{Mathf.CeilToInt(_health.Current)} / {Mathf.CeilToInt(maximum)}");
        }

        private void Draw(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _pixel);
            GUI.color = previous;
        }
    }
}
