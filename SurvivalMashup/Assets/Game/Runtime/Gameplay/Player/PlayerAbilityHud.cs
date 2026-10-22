using ToyChest.Systems.Abilities;
using UnityEngine;

namespace ToyChest.Gameplay.Player
{
    /// <summary>
    /// A thin presentation adapter showing each ability slot's name, input hint, and cooldown, just above the
    /// <see cref="PlayerHealthBar"/>. It reads the cooldown the Ability System owns through
    /// <see cref="PlayerAbilityActivator"/> — it owns no gameplay and adding or removing it changes no behaviour.
    /// The slot reads READY when usable; on cooldown it drains with the seconds remaining; a press the Ability
    /// System refuses flashes it, so a press on cooldown is visibly answered rather than silently ignored.
    ///
    /// Drawn with IMGUI for the same reason as the health bar: a deliberate placeholder until a real UI system
    /// arrives, not the start of an ability bar.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAbilityHud : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Ability adapter whose slots are displayed. Defaults to the sibling.")]
        private PlayerAbilityActivator _abilities;

        [SerializeField]
        [Tooltip("Input hint per slot, in slot order (e.g. \"R / RMB\"). Presentation only.")]
        private string[] _inputHints = { "R / RMB" };

        [Header("Layout")]
        [SerializeField]
        [Tooltip("Slot size in pixels.")]
        private Vector2 _size = new Vector2(260f, 22f);

        [SerializeField]
        [Tooltip("Offset of the first slot from the bottom-left corner of the screen, in pixels (sits above the health bar).")]
        private Vector2 _margin = new Vector2(24f, 52f);

        [Header("Colours")]
        [SerializeField]
        private Color _readyColor = new Color(0.95f, 0.7f, 0.2f);

        [SerializeField]
        private Color _cooldownColor = new Color(0.45f, 0.45f, 0.5f);

        [SerializeField]
        private Color _backgroundColor = new Color(0f, 0f, 0f, 0.55f);

        [SerializeField]
        [Tooltip("Colour the slot flashes toward when a press is refused.")]
        private Color _refusedFlashColor = new Color(1f, 0.25f, 0.2f);

        [SerializeField]
        [Tooltip("Seconds the refused flash lasts.")]
        private float _flashDuration = 0.3f;

        private float[] _flashTimers = new float[0];
        private Texture2D _pixel;

        private void Awake()
        {
            if (_abilities == null)
            {
                _abilities = GetComponent<PlayerAbilityActivator>();
            }

            _pixel = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
        }

        private void OnEnable()
        {
            if (_abilities != null)
            {
                _abilities.SlotRefused += OnSlotRefused;
            }
        }

        private void OnDisable()
        {
            if (_abilities != null)
            {
                _abilities.SlotRefused -= OnSlotRefused;
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
            for (int i = 0; i < _flashTimers.Length; i++)
            {
                _flashTimers[i] = Mathf.Max(0f, _flashTimers[i] - Time.unscaledDeltaTime);
            }
        }

        private void OnSlotRefused(int index, AbilityActivationResult reason)
        {
            if (index >= _flashTimers.Length)
            {
                System.Array.Resize(ref _flashTimers, index + 1);
            }

            _flashTimers[index] = _flashDuration;
        }

        private void OnGUI()
        {
            if (_abilities == null || _pixel == null)
            {
                return;
            }

            for (int i = 0; i < _abilities.SlotCount; i++)
            {
                if (!_abilities.TryGetCooldown(i, out float remaining, out float duration))
                {
                    continue; // not granted: nothing to show
                }

                float y = Screen.height - _margin.y - _size.y - i * (_size.y + 4f);
                var background = new Rect(_margin.x, y, _size.x, _size.y);
                bool ready = remaining <= 0f;
                float fraction = ready || duration <= 0f ? 1f : Mathf.Clamp01(1f - remaining / duration);

                float flash = i < _flashTimers.Length && _flashDuration > 0f ? Mathf.Clamp01(_flashTimers[i] / _flashDuration) : 0f;
                Color fillColor = Color.Lerp(ready ? _readyColor : _cooldownColor, _refusedFlashColor, flash);

                Draw(background, _backgroundColor);
                Draw(new Rect(background.x, background.y, background.width * fraction, background.height), fillColor);

                string hint = i < _inputHints.Length && !string.IsNullOrEmpty(_inputHints[i]) ? $"[{_inputHints[i]}] " : string.Empty;
                string state = ready ? "READY" : $"{remaining:0.0}s";
                GUI.Label(new Rect(background.x + 8f, background.y, background.width, background.height),
                    $"{hint}{_abilities.GetDisplayName(i)} — {state}");
            }
        }

        private void Draw(Rect rect, Color color)
        {
            if (rect.width <= 0f)
            {
                return;
            }

            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _pixel);
            GUI.color = previous;
        }
    }
}
