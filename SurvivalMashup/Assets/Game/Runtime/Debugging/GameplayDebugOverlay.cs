using System.Collections.Generic;
using ToyChest.Boot;
using ToyChest.Framework.Objects;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.Attributes;
using ToyChest.Systems.Interactions;
using ToyChest.Systems.Inventory;
using ToyChest.Systems.Resources;
using ToyChest.Systems.Tags;
using UnityEngine;

namespace ToyChest.Debugging
{
    /// <summary>
    /// A minimal runtime inspector for the live gameplay world, drawn with IMGUI so it needs no
    /// scene setup, canvas, or assets. It reads the bootstrapped <see cref="RuntimeServices"/> and
    /// walks the <see cref="GameplayObjectRegistry"/>, showing each live object's identity plus its
    /// tags, attributes, resources, abilities, inventory, and advertised interactions — the
    /// Gameplay Object Inspector, Tag Viewer, and Attribute/Resource Viewer the milestone asks for,
    /// in one panel. Purely a reader: it never mutates gameplay, so it is safe to leave enabled and
    /// exists only to make gameplay iteration faster. Lives in a dev-only assembly that ships with
    /// nothing referencing it. Toggle with <see cref="_toggleKey"/> (F1 by default).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayDebugOverlay : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The scene's GameBootstrap. Left empty, the overlay finds the one in the scene.")]
        private GameBootstrap _bootstrap;

        [SerializeField]
        [Tooltip("Whether the overlay is visible when play begins. Press F1 to toggle at runtime.")]
        private bool _visibleOnStart = true;

        // Reused across frames so the inspector allocates nothing while idle.
        private readonly List<GameplayTag> _tagScratch = new List<GameplayTag>();
        private bool _visible;
        private Vector2 _scroll;

        private void Awake()
        {
            _visible = _visibleOnStart;
            if (_bootstrap == null)
            {
                _bootstrap = FindAnyObjectByType<GameBootstrap>();
            }
        }

        private void Update()
        {
            if (TogglePressed())
            {
                _visible = !_visible;
            }
        }

        // Reads the F1 toggle through whichever input backend the project has active, so the
        // overlay never touches the legacy Input class when the Input System package owns input.
        private static bool TogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null && keyboard.f1Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.F1);
#else
            return false;
#endif
        }

        private void OnGUI()
        {
            if (!_visible)
            {
                return;
            }

            RuntimeServices services = _bootstrap != null ? _bootstrap.Services : null;

            const float width = 420f;
            float height = Mathf.Min(Screen.height - 20f, 640f);
            GUILayout.BeginArea(new Rect(10f, 10f, width, height), GUI.skin.box);

            if (services == null)
            {
                GUILayout.Label("[ToyChest Debug]  waiting for bootstrap…   (F1 to toggle)");
                GUILayout.EndArea();
                return;
            }

            GameplayObjectRegistry world = services.Objects;
            GUILayout.Label(
                $"[ToyChest Debug]   live objects: {world.Count}   tags interned: {services.TagTable.Count}   (F1 to toggle)");

            _scroll = GUILayout.BeginScrollView(_scroll);
            IReadOnlyList<GameplayObject> objects = world.Objects;
            if (objects.Count == 0)
            {
                GUILayout.Label("  (no live objects)");
            }

            for (int i = 0; i < objects.Count; i++)
            {
                DrawObject(objects[i], services.TagTable);
                GUILayout.Space(6f);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawObject(GameplayObject obj, GameplayTagTable tagTable)
        {
            string shortId = obj.Id.ToString();
            if (shortId.Length > 8)
            {
                shortId = shortId.Substring(0, 8);
            }

            GUILayout.Label($"▸ {obj.DefinitionId}   [{shortId}]   {(obj.IsActive ? "active" : "inactive")}");

            if (obj.TryGet(out GameplayTagContainer tags))
            {
                _tagScratch.Clear();
                tags.CopyTagsTo(_tagScratch);
                if (_tagScratch.Count > 0)
                {
                    var line = new System.Text.StringBuilder("    tags:");
                    for (int t = 0; t < _tagScratch.Count; t++)
                    {
                        line.Append(' ').Append(tagTable.GetPath(_tagScratch[t]));
                    }

                    GUILayout.Label(line.ToString());
                }
            }

            if (obj.TryGet(out AttributeSet attributes))
            {
                IReadOnlyList<AttributeValue> values = attributes.Attributes;
                for (int a = 0; a < values.Count; a++)
                {
                    AttributeValue value = values[a];
                    GUILayout.Label($"    attr  {value.Definition.Id} = {value.CurrentValue:0.##}");
                }
            }

            if (obj.TryGet(out ResourceSet resources))
            {
                IReadOnlyList<ResourceValue> values = resources.Resources;
                for (int r = 0; r < values.Count; r++)
                {
                    ResourceValue value = values[r];
                    GUILayout.Label($"    res   {value.Definition.Id} = {value.Current:0.##} / {value.Maximum:0.##}");
                }
            }

            if (obj.TryGet(out AbilitySet abilities) && abilities.Count > 0)
            {
                IReadOnlyList<AbilityInstance> granted = abilities.Abilities;
                var line = new System.Text.StringBuilder("    abilities:");
                for (int b = 0; b < granted.Count; b++)
                {
                    line.Append(' ').Append(granted[b].Definition.Id);
                }

                GUILayout.Label(line.ToString());
            }

            if (obj.TryGet(out InventorySet inventory))
            {
                GUILayout.Label($"    inventory: {inventory.StackCount} / {inventory.SlotCapacity} slots");
            }

            if (obj.TryGet(out InteractionSet interactions) && interactions.Interactions.Count > 0)
            {
                var line = new System.Text.StringBuilder("    interactions:");
                for (int n = 0; n < interactions.Interactions.Count; n++)
                {
                    line.Append(' ').Append(interactions.Interactions[n].DisplayName);
                }

                GUILayout.Label(line.ToString());
            }
        }
    }
}
