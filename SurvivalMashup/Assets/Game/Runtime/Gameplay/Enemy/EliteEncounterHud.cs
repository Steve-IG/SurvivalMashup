using ToyChest.Gameplay.Objects;
using ToyChest.Systems.Abilities;
using UnityEngine;

namespace ToyChest.Gameplay.Enemy
{
    /// <summary>
    /// A minimal IMGUI readout for an <see cref="EliteEncounter"/>, in the style of <c>PlayerHealthBar</c>: a prompt above
    /// the challenge pad while the player is near it, the elite's name and health bar across the top of the screen during
    /// the fight (with a callout while its signature attack winds up), and a completion banner naming the reward when the
    /// elite falls. Read-only presentation over the encounter's public state; removing it changes no behaviour.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EliteEncounter))]
    public sealed class EliteEncounterHud : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The encounter shown. Defaults to the sibling.")]
        private EliteEncounter _encounter;

        [SerializeField]
        [Tooltip("Unity tag of the player, for the pad prompt's distance check.")]
        private string _playerTag = "Player";

        [SerializeField]
        [Tooltip("The pad prompt shows while the player is within this distance of the pad and the encounter is armed.")]
        private float _promptDistance = 8f;

        [SerializeField]
        [Tooltip("Boss bar size in pixels.")]
        private Vector2 _barSize = new Vector2(520f, 18f);

        [SerializeField]
        [Tooltip("Seconds the completion banner stays up.")]
        private float _bannerSeconds = 5f;

        [SerializeField]
        private Color _barColor = new Color(0.85f, 0.18f, 0.12f);

        [SerializeField]
        private Color _backgroundColor = new Color(0f, 0f, 0f, 0.6f);

        private Texture2D _pixel;
        private Transform _player;
        private string _eliteName = "Elite";
        private float _bannerTimer;
        private bool _bannerRewarded;
        private GUIStyle _title;
        private GUIStyle _body;

        /// <summary>Whether the completion banner is showing. Probes read this.</summary>
        public bool IsBannerShowing => _bannerTimer > 0f;

        private void Awake()
        {
            if (_encounter == null)
            {
                _encounter = GetComponent<EliteEncounter>();
            }

            _pixel = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
        }

        private void OnEnable()
        {
            _encounter.Started += OnStarted;
            _encounter.Completed += OnCompleted;
        }

        private void OnDisable()
        {
            _encounter.Started -= OnStarted;
            _encounter.Completed -= OnCompleted;
        }

        private void OnDestroy()
        {
            if (_pixel != null)
            {
                Destroy(_pixel);
            }
        }

        private void OnStarted(EnemyCombatant elite)
        {
            _bannerTimer = 0f;
            var spawner = elite.GetComponent<GameplayObjectSpawner>();
            _eliteName = spawner != null && spawner.Definition != null ? spawner.Definition.DisplayName : elite.name;
        }

        private void OnCompleted(AbilityActivationResult result)
        {
            _bannerTimer = _bannerSeconds;
            _bannerRewarded = result == AbilityActivationResult.Activated;
        }

        private void Update()
        {
            if (_bannerTimer > 0f)
            {
                _bannerTimer -= Time.unscaledDeltaTime;
            }
        }

        private void OnGUI()
        {
            if (_pixel == null)
            {
                return;
            }

            EnsureStyles();
            if (_encounter.State == EliteEncounterState.Active)
            {
                DrawBossBar();
            }
            else if (_encounter.State == EliteEncounterState.Armed && _bannerTimer <= 0f)
            {
                DrawPadPrompt();
            }

            if (_bannerTimer > 0f)
            {
                DrawBanner();
            }
        }

        private void DrawBossBar()
        {
            if (!_encounter.TryGetEliteHealth(out float current, out float maximum) || maximum <= 0f)
            {
                return;
            }

            float x = (Screen.width - _barSize.x) * 0.5f;
            const float y = 34f;
            GUI.Label(new Rect(x, y - 26f, _barSize.x, 24f), _eliteName.ToUpperInvariant(), _title);

            Color previous = GUI.color;
            GUI.color = _backgroundColor;
            GUI.DrawTexture(new Rect(x - 2f, y - 2f, _barSize.x + 4f, _barSize.y + 4f), _pixel);
            GUI.color = _barColor;
            GUI.DrawTexture(new Rect(x, y, _barSize.x * Mathf.Clamp01(current / maximum), _barSize.y), _pixel);
            GUI.color = previous;
            GUI.Label(new Rect(x, y, _barSize.x, _barSize.y), $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(maximum)}", _body);

            EnemyCombatant elite = _encounter.Elite;
            if (elite != null && elite.IsSignatureWindingUp)
            {
                GUI.Label(new Rect(x, y + _barSize.y + 6f, _barSize.x, 24f), "SHOCKWAVE SLAM — GET OUT OF THE CIRCLE", _title);
            }
            else if (elite != null && elite.IsSignatureRecovering)
            {
                GUI.Label(new Rect(x, y + _barSize.y + 6f, _barSize.x, 24f), "STAGGERED — STRIKE NOW", _title);
            }
        }

        private void DrawPadPrompt()
        {
            if (_player == null)
            {
                GameObject found = GameObject.FindGameObjectWithTag(_playerTag);
                _player = found != null ? found.transform : null;
            }

            Camera view = Camera.main;
            if (_player == null || view == null || Vector3.Distance(_player.position, transform.position) > _promptDistance)
            {
                return;
            }

            Vector3 screen = view.WorldToScreenPoint(transform.position + Vector3.up * 1.5f);
            if (screen.z <= 0f)
            {
                return;
            }

            string text = _encounter.HasBeenDefeated
                ? "Elite challenge — rematch (reward already claimed)\nStep onto the red pad"
                : "Elite challenge — Grunt Brute\nStep onto the red pad";
            var rect = new Rect(screen.x - 200f, Screen.height - screen.y - 24f, 400f, 48f);
            Color previous = GUI.color;
            GUI.color = _backgroundColor;
            GUI.DrawTexture(rect, _pixel);
            GUI.color = previous;
            GUI.Label(rect, text, _body);
        }

        private void DrawBanner()
        {
            string detail = _bannerRewarded
                ? $"{_encounter.RewardDisplayName} added to your inventory"
                : "Rematch won — reward already claimed";
            var rect = new Rect((Screen.width - 520f) * 0.5f, Screen.height * 0.22f, 520f, 70f);
            Color previous = GUI.color;
            GUI.color = _backgroundColor;
            GUI.DrawTexture(rect, _pixel);
            GUI.color = previous;
            GUI.Label(new Rect(rect.x, rect.y + 6f, rect.width, 30f), "ELITE DEFEATED", _title);
            GUI.Label(new Rect(rect.x, rect.y + 38f, rect.width, 24f), detail, _body);
        }

        private void EnsureStyles()
        {
            if (_title != null)
            {
                return;
            }

            _title = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = Color.white },
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 13, normal = { textColor = Color.white },
            };
        }
    }
}
