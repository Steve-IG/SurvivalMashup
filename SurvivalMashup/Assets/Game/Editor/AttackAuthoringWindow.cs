using System.Collections.Generic;
using System.IO;
using ToyChest.Gameplay.Combat;
using ToyChest.Gameplay.HitDetection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ToyChest.Editor
{
    /// <summary>
    /// The melee attack authoring tool (<b>Tools ▸ ToyChest ▸ Attack Authoring</b>). Pick an attack asset (or
    /// create one from a clip), pick a character to preview on, and:
    /// <list type="bullet">
    /// <item><b>Propose windows</b> — <see cref="AttackWindowProposer"/> analyses the clip (limb strike speeds,
    /// leap takeoff/landing, root-motion stop) and fills in the hit windows and commitment end.</item>
    /// <item><b>Scrub</b> the timeline — the character is posed at that moment in the Scene view, with the limbs
    /// of every open window drawn as red strike capsules (inactive limbs grey), so window placement is judged
    /// against the actual motion.</item>
    /// <item><b>Edit</b> the windows, limbs, abilities and re-hit intervals directly (undoable).</item>
    /// </list>
    /// New attack workflow: New attack from clip → Propose windows → scrub and tweak → assign the asset to a
    /// combo step. Character-independent: windows name limbs by role, so the same asset works on any character
    /// whose strike profile has those limbs.
    /// </summary>
    public sealed class AttackAuthoringWindow : EditorWindow
    {
        private const string AttackFolder = "Assets/Game/Content/Attacks";
        private const string PreviewName = "~AttackPreview";

        private static readonly Color[] WindowColors =
        {
            new Color(0.95f, 0.35f, 0.25f), new Color(0.25f, 0.65f, 0.95f), new Color(0.35f, 0.85f, 0.35f),
            new Color(0.95f, 0.75f, 0.2f), new Color(0.75f, 0.4f, 0.95f),
        };

        [SerializeField] private AttackDefinition _attack;
        [SerializeField] private Animator _character;
        [SerializeField] private float _playbackSpeed = 1f;
        [SerializeField] private float _time; // normalized
        [SerializeField] private string _lastReport;

        private SerializedObject _serialized;
        private Vector2 _scroll;
        private bool _playing;
        private double _lastTick;
        private GameObject _ownedPreview;

        [MenuItem("Tools/ToyChest/Attack Authoring")]
        public static void Open()
        {
            GetWindow<AttackAuthoringWindow>("Attack Authoring");
        }

        /// <summary>Opens the tool on a specific attack (e.g. from a script or a combo step).</summary>
        public static void Open(AttackDefinition attack)
        {
            AttackAuthoringWindow window = GetWindow<AttackAuthoringWindow>("Attack Authoring");
            window._attack = attack;
            window._serialized = null;
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGui;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            EditorApplication.update -= OnEditorUpdate;
            StopPreview();
            if (_ownedPreview != null)
            {
                DestroyImmediate(_ownedPreview);
            }
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUI.BeginChangeCheck();
            _attack = (AttackDefinition)EditorGUILayout.ObjectField("Attack", _attack, typeof(AttackDefinition), false);
            if (EditorGUI.EndChangeCheck())
            {
                _serialized = null;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _character = (Animator)EditorGUILayout.ObjectField("Preview character", _character, typeof(Animator), true);
                if (GUILayout.Button("Use Player", GUILayout.Width(80)))
                {
                    UsePlayerPreview();
                }
            }

            _playbackSpeed = EditorGUILayout.FloatField(
                new GUIContent("Playback speed", "The animator state's speed — strike detection measures on-screen limb speed."),
                Mathf.Max(0.01f, _playbackSpeed));

            if (GUILayout.Button("New attack from clip…"))
            {
                CreateFromClip();
            }

            if (_attack == null)
            {
                EditorGUILayout.HelpBox("Pick an attack asset, or create one from a clip.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            AnimationClip clip = _attack.Clip;
            using (new EditorGUI.DisabledScope(clip == null || _character == null || !_character.isHuman))
            {
                if (GUILayout.Button("Propose windows from the clip", GUILayout.Height(26)))
                {
                    ProposeWindows();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                string hint = _attack.StrikeHint >= 0f && clip != null ? $"{_attack.StrikeHint * clip.length:0.000}s" : "none";
                GUILayout.Label($"Strike frame: {hint}", GUILayout.Width(150));
                if (GUILayout.Button(new GUIContent("Set strike frame",
                        "Marks the current time as the moment the blow lands. The proposer then picks the limb that is " +
                        "striking at this frame instead of guessing from motion alone.")))
                {
                    SetStrikeHint(_time);
                }

                using (new EditorGUI.DisabledScope(_attack.StrikeHint < 0f))
                {
                    if (GUILayout.Button("Clear", GUILayout.Width(50)))
                    {
                        SetStrikeHint(-1f);
                    }
                }
            }

            if (clip == null)
            {
                EditorGUILayout.HelpBox("Assign the attack's clip below to propose and preview.", MessageType.Warning);
            }
            else if (_character == null)
            {
                EditorGUILayout.HelpBox("Pick a humanoid character (or press Use Player) to propose and preview.", MessageType.Warning);
            }

            DrawTimeline(clip);

            if (!string.IsNullOrEmpty(_lastReport))
            {
                EditorGUILayout.HelpBox(_lastReport.TrimEnd(), MessageType.None);
            }

            DrawTimingAndValidation();

            EditorGUILayout.Space();
            if (_serialized == null || _serialized.targetObject != _attack)
            {
                _serialized = new SerializedObject(_attack);
            }

            _serialized.Update();
            EditorGUILayout.PropertyField(_serialized.FindProperty("_clip"));
            EditorGUILayout.PropertyField(_serialized.FindProperty("_abilityId"));
            EditorGUILayout.PropertyField(_serialized.FindProperty("_commitEnd"));
            EditorGUILayout.PropertyField(_serialized.FindProperty("_cancelFrom"));
            EditorGUILayout.PropertyField(_serialized.FindProperty("_windows"),
                new GUIContent("Windows", "Tick 'Verified' on a window once you have scrubbed it against the animation and seen it land in play."), true);
            if (_serialized.ApplyModifiedProperties())
            {
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndScrollView();
        }

        // --- Timeline ---------------------------------------------------------------------------------

        private void DrawTimeline(AnimationClip clip)
        {
            IReadOnlyList<HitWindow> windows = _attack.Windows;
            float rowHeight = 16f;
            float height = 22f + Mathf.Max(1, windows.Count) * (rowHeight + 2f) + 6f;
            Rect area = GUILayoutUtility.GetRect(10f, height, GUILayout.ExpandWidth(true));
            area.xMin += 4f;
            area.xMax -= 4f;
            EditorGUI.DrawRect(area, new Color(0.16f, 0.16f, 0.16f));

            Rect ruler = new Rect(area.x, area.y, area.width, 18f);
            float length = clip != null ? clip.length : 1f;
            for (int i = 0; i <= 10; i++)
            {
                float x = ruler.x + ruler.width * i / 10f;
                EditorGUI.DrawRect(new Rect(x, ruler.y + 12f, 1f, 6f), new Color(0.5f, 0.5f, 0.5f));
                if (i % 2 == 0)
                {
                    GUI.Label(new Rect(x + 2f, ruler.y, 60f, 14f), $"{length * i / 10f:0.00}s", EditorStyles.miniLabel);
                }
            }

            for (int w = 0; w < windows.Count; w++)
            {
                HitWindow window = windows[w];
                Rect row = new Rect(area.x, area.y + 20f + w * (rowHeight + 2f), area.width, rowHeight);
                Rect bar = new Rect(row.x + row.width * window.Start, row.y,
                    Mathf.Max(2f, row.width * (window.End - window.Start)), rowHeight);
                bool open = window.OverlapsSpan(_time, _time);
                Color c = WindowColors[w % WindowColors.Length];
                if (window.IsVerified)
                {
                    EditorGUI.DrawRect(bar, open ? c : c * 0.6f);
                }
                else
                {
                    // A suggestion, not yet checked against the animation: drawn as an outline only.
                    Color edge = open ? c : c * 0.75f;
                    EditorGUI.DrawRect(new Rect(bar.x, bar.y, bar.width, 2f), edge);
                    EditorGUI.DrawRect(new Rect(bar.x, bar.yMax - 2f, bar.width, 2f), edge);
                    EditorGUI.DrawRect(new Rect(bar.x, bar.y, 2f, bar.height), edge);
                    EditorGUI.DrawRect(new Rect(bar.xMax - 2f, bar.y, 2f, bar.height), edge);
                }

                string state = window.IsVerified ? "✓" : "? suggested";
                GUI.Label(new Rect(bar.x + 3f, bar.y, 320f, rowHeight), $"{window.Id}  ({window.Limbs})  {state}", EditorStyles.whiteMiniLabel);
            }

            if (_attack.CommitEnd > 0f)
            {
                float x = area.x + area.width * _attack.CommitEnd;
                EditorGUI.DrawRect(new Rect(x - 1f, area.y, 2f, area.height), new Color(1f, 0.85f, 0.2f));
                GUI.Label(new Rect(x + 3f, area.yMax - 14f, 90f, 14f), "commit end", EditorStyles.miniLabel);
            }

            if (_attack.CancelFrom > 0f)
            {
                // The cancellable recovery tail: from the cancel point to the commit end.
                float x0 = area.x + area.width * _attack.CancelFrom;
                float x1 = area.x + area.width * Mathf.Max(_attack.CancelFrom, _attack.CommitEnd);
                EditorGUI.DrawRect(new Rect(x0, area.yMax - 4f, Mathf.Max(2f, x1 - x0), 4f), new Color(0.3f, 0.85f, 0.4f));
                GUI.Label(new Rect(x0 + 3f, area.yMax - 18f, 90f, 14f), "cancellable", EditorStyles.miniLabel);
            }

            if (_attack.StrikeHint >= 0f)
            {
                float x = area.x + area.width * _attack.StrikeHint;
                EditorGUI.DrawRect(new Rect(x - 1f, area.y, 2f, area.height), new Color(1f, 0.3f, 0.3f));
                GUI.Label(new Rect(x + 3f, area.yMax - 28f, 90f, 14f), "strike", EditorStyles.miniLabel);
            }

            float cursor = area.x + area.width * _time;
            EditorGUI.DrawRect(new Rect(cursor - 1f, area.y, 2f, area.height), Color.white);

            // Scrub by clicking / dragging anywhere on the timeline.
            Event e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && area.Contains(e.mousePosition))
            {
                _playing = false;
                SetTime((e.mousePosition.x - area.x) / area.width);
                e.Use();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(_playing ? "Pause" : "Play", GUILayout.Width(60)))
                {
                    _playing = !_playing;
                    _lastTick = EditorApplication.timeSinceStartup;
                }

                float newTime = EditorGUILayout.Slider(_time, 0f, 1f);
                if (!Mathf.Approximately(newTime, _time))
                {
                    _playing = false;
                    SetTime(newTime);
                }

                GUILayout.Label($"{_time * length:0.000}s", GUILayout.Width(60));
            }
        }

        private void SetTime(float normalized)
        {
            _time = Mathf.Clamp01(normalized);
            SamplePreview();
            Repaint();
        }

        private void OnEditorUpdate()
        {
            if (!_playing || _attack == null || _attack.Clip == null)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            float delta = (float)(now - _lastTick);
            _lastTick = now;
            float t = _time + delta * _playbackSpeed / _attack.Clip.length;
            SetTime(t > 1f ? 0f : t);
        }

        // --- Preview ----------------------------------------------------------------------------------

        private void SamplePreview()
        {
            if (_attack == null || _attack.Clip == null || _character == null)
            {
                return;
            }

            if (!AnimationMode.InAnimationMode())
            {
                AnimationMode.StartAnimationMode();
            }

            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(_character.gameObject, _attack.Clip, _time * _attack.Clip.length);
            AnimationMode.EndSampling();
            SceneView.RepaintAll();
        }

        private static void StopPreview()
        {
            if (AnimationMode.InAnimationMode())
            {
                AnimationMode.StopAnimationMode();
            }
        }

        private void OnSceneGui(SceneView view)
        {
            if (_attack == null || _character == null || !AnimationMode.InAnimationMode())
            {
                return;
            }

            StrikeLimbMask active = StrikeLimbMask.None;
            foreach (HitWindow w in _attack.Windows)
            {
                if (w.OverlapsSpan(_time, _time)) active |= w.Limbs;
            }

            var profile = _character.GetComponentInParent<CharacterStrikeProfile>();
            Dictionary<StrikeLimb, StrikeShape> shapes = CharacterStrikeProfile.ResolveShapes(_character, profile != null ? profile.AuthoredShapes : null);
            foreach (KeyValuePair<StrikeLimb, StrikeShape> pair in shapes)
            {
                if (!CharacterStrikeProfile.TryResolvePose(_character, pair.Value, out Vector3 a, out Vector3 b))
                {
                    continue;
                }

                bool on = StrikeLimbs.Contains(active, pair.Key);
                Handles.color = on ? new Color(1f, 0.2f, 0.15f) : new Color(0.6f, 0.6f, 0.6f, 0.6f);
                Handles.DrawLine(a, b, on ? 4f : 1f);
                Handles.DrawWireDisc(b, view.camera.transform.forward, pair.Value.Radius);
                if (on)
                {
                    Handles.Label(b + Vector3.up * 0.15f, pair.Key.ToString());
                }
            }

            foreach (HitWindow w in _attack.Windows)
            {
                if (w.Area == null || !w.OverlapsSpan(_time, _time)) continue;
                Transform bone = _character.GetBoneTransform(w.AreaBone);
                if (bone == null) continue;
                Handles.color = new Color(1f, 0.6f, 0.1f);
                Handles.DrawWireDisc(bone.position, Vector3.up, w.Area.Volume.Radius);
            }
        }

        // --- Timing & validation ----------------------------------------------------------------------

        // The attack's separate timing concepts, then everything that would stop it working in play: the asset itself
        // (checked against the preview character's limbs) and every combo step that uses it (checked against the
        // Animator controller the step plays on).
        private void DrawTimingAndValidation()
        {
            AnimatorController controller = _character != null ? _character.runtimeAnimatorController as AnimatorController : null;
            float speed = _playbackSpeed;
            var issues = new List<AttackValidation.Issue>();
            var usedBy = new List<string>();
            HashSet<StrikeLimb> limbs = _character != null ? AttackValidation.AvailableLimbs(_character) : null;

            foreach (string guid in AssetDatabase.FindAssets("t:ComboDefinition"))
            {
                var combo = AssetDatabase.LoadAssetAtPath<ComboDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                for (int i = 0; combo != null && i < combo.StepCount; i++)
                {
                    ComboStep step = combo.Steps[i];
                    if (step.Attack != _attack)
                    {
                        continue;
                    }

                    usedBy.Add($"{combo.name} step {i + 1} (state '{step.AnimatorState}')");
                    AnimatorState state = AttackValidation.FindState(controller, step.AnimatorState);
                    if (state != null)
                    {
                        speed = state.speed;
                    }

                    foreach (AttackValidation.Issue issue in AttackValidation.ValidateStep(step, controller, limbs))
                    {
                        // The asset-level issues are reported once below.
                        if (!issues.Exists(x => x.Message == issue.Message))
                        {
                            issues.Add(new AttackValidation.Issue(issue.Severity, $"{combo.name}/{i + 1}: {issue.Message}"));
                        }
                    }
                }
            }

            EditorGUILayout.LabelField("Timing", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(AttackValidation.TimingSummary(_attack, speed) +
                                    (Mathf.Approximately(speed, 1f) ? string.Empty : $"\n(at Animator state speed {speed:0.##})"), MessageType.None);

            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(usedBy.Count > 0 ? "Used by: " + string.Join(", ", usedBy) : "Not used by any combo step yet.",
                EditorStyles.wordWrappedMiniLabel);

            List<AttackValidation.Issue> own = AttackValidation.ValidateAttack(_attack, limbs);
            foreach (AttackValidation.Issue issue in own)
            {
                issues.RemoveAll(x => x.Message.EndsWith(issue.Message));
            }

            own.AddRange(issues);
            if (own.Count == 0)
            {
                EditorGUILayout.HelpBox("No problems found. All windows verified.", MessageType.Info);
            }

            foreach (AttackValidation.Issue issue in own)
            {
                MessageType type = issue.Severity == AttackValidation.Severity.Error ? MessageType.Error
                    : issue.Severity == AttackValidation.Severity.Warning ? MessageType.Warning : MessageType.Info;
                EditorGUILayout.HelpBox(issue.Message, type);
            }
        }

        // --- Actions ----------------------------------------------------------------------------------

        private void SetStrikeHint(float normalized)
        {
            Undo.RecordObject(_attack, "Set strike frame");
            var so = new SerializedObject(_attack);
            so.FindProperty("_strikeHint").floatValue = normalized;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(_attack);
            Repaint();
        }

        private void ProposeWindows()
        {
            AttackWindowProposer.Proposal proposal = AttackWindowProposer.Propose(_character, _attack.Clip, _playbackSpeed, _attack.StrikeHint);
            ApplyProposal(_attack, proposal);
            _lastReport = proposal.Report;
            _serialized = null;
            SamplePreview();
        }

        /// <summary>
        /// Writes a proposal into an attack asset (windows — all unverified — commitment end, cancel point). Undoable.
        /// The same step the Propose button performs; exposed so batch authoring scripts use the identical path.
        /// </summary>
        public static void ApplyProposal(AttackDefinition attack, AttackWindowProposer.Proposal proposal)
        {
            Undo.RecordObject(attack, "Propose attack windows");
            var so = new SerializedObject(attack);
            SerializedProperty windows = so.FindProperty("_windows");
            windows.arraySize = proposal.Windows.Count;
            for (int i = 0; i < proposal.Windows.Count; i++)
            {
                HitWindow w = proposal.Windows[i];
                SerializedProperty p = windows.GetArrayElementAtIndex(i);
                p.FindPropertyRelative("_id").stringValue = w.Id;
                p.FindPropertyRelative("_start").floatValue = w.Start;
                p.FindPropertyRelative("_end").floatValue = w.End;
                p.FindPropertyRelative("_limbs").intValue = (int)w.Limbs;
                p.FindPropertyRelative("_area").objectReferenceValue = null;
                p.FindPropertyRelative("_abilityId").stringValue = string.Empty;
                p.FindPropertyRelative("_rehitInterval").floatValue = 0f;
                p.FindPropertyRelative("_verified").boolValue = false; // a proposal is a suggestion until checked
            }

            so.FindProperty("_commitEnd").floatValue = proposal.CommitEnd;
            so.FindProperty("_cancelFrom").floatValue = proposal.CancelFrom;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(attack);
            AssetDatabase.SaveAssets();
        }

        private void CreateFromClip()
        {
            string path = EditorUtility.OpenFilePanelWithFilters("Attack clip (FBX or .anim)", "Assets", new[] { "Animation", "fbx,FBX,anim" });
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            path = "Assets" + path.Substring(Application.dataPath.Length);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                EditorUtility.DisplayDialog("Attack Authoring", "No animation clip found in that file.", "OK");
                return;
            }

            _attack = CreateAttackAsset(clip, Path.GetFileNameWithoutExtension(path));
            _serialized = null;
            Selection.activeObject = _attack;
        }

        /// <summary>Creates (or loads) Assets/Game/Content/Attacks/Attack_&lt;name&gt;.asset for a clip.</summary>
        public static AttackDefinition CreateAttackAsset(AnimationClip clip, string name)
        {
            Directory.CreateDirectory(AttackFolder);
            string assetPath = $"{AttackFolder}/Attack_{name}.asset";
            var attack = AssetDatabase.LoadAssetAtPath<AttackDefinition>(assetPath);
            if (attack == null)
            {
                attack = CreateInstance<AttackDefinition>();
                AssetDatabase.CreateAsset(attack, assetPath);
            }

            var so = new SerializedObject(attack);
            so.FindProperty("_clip").objectReferenceValue = clip;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(attack);
            AssetDatabase.SaveAssets();
            return attack;
        }

        private void UsePlayerPreview()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Content/Prefabs/Player.prefab");
            if (prefab == null)
            {
                return;
            }

            if (_ownedPreview == null)
            {
                _ownedPreview = (GameObject)Instantiate(prefab);
                _ownedPreview.name = PreviewName;
                _ownedPreview.hideFlags = HideFlags.DontSave;
                foreach (MonoBehaviour b in _ownedPreview.GetComponentsInChildren<MonoBehaviour>())
                {
                    b.enabled = false; // a posed mannequin: no gameplay runs on it
                }
            }

            _character = _ownedPreview.GetComponentInChildren<Animator>();
            SamplePreview();
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.Frame(new Bounds(_ownedPreview.transform.position + Vector3.up, Vector3.one * 3f), false);
            }
        }
    }
}
