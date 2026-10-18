using System.Collections.Generic;
using System.Text;
using ToyChest.Editor;
using ToyChest.Gameplay.Combat;
using ToyChest.Gameplay.HitDetection;
using ToyChest.Gameplay.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// RG5E attack library, phase 2 (run after AuthorAttackLibrary has proposed every attack):
/// 1. applies the designer corrections to the proposals — each one listed below with the reason, found by
///    reviewing the proposals against RenderClipContactSheet frames (proposals stay "suggested" until verified);
/// 2. adds the HV_GroundShockwave area preset; 3. adds one Animator state + trigger per library clip (Any State
///    entry on the trigger, single exit on !AttackCommitted — the shipped attack-state pattern);
/// 4. authors the four test combos; 5. gives the Player a Weapon1 socket strike shape (weapon-socket proxy);
/// 6. places a ComboLoadoutPad per combo in the dojo. Idempotent. Editor-only tooling.
/// </summary>
public static class AuthorAttackLibraryContent
{
    // GetComponent returns Unity's "fake null" in the editor, which defeats ??.
    private static T Ensure<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private const string AttackFolder = "Assets/Game/Content/Attacks/";
    private const string DefinitionsFolder = "Assets/Game/Content/Definitions/";
    private const string ControllerPath = "Assets/Game/Content/Animation/PlayerAnimator.controller";
    private const string PlayerPrefab = "Assets/Game/Content/Prefabs/Player.prefab";
    private const string ScenePath = "Assets/Game/Scenes/VerticalSlice.unity";
    private const string ShockwavePath = "Assets/Game/Content/HitVolumes/HV_GroundShockwave.asset";

    // The assets AuthorAttackLibrary created (kept in sync with its list; run_script compiles one file at a time).
    private static readonly string[] AttackNames =
    {
        "Fighter_Combo04_1", "Fighter_Combo04_2", "Fighter_Combo03_1", "Fighter_Combo02_1", "Fighter_Combo02_2",
        "Fighter_Combo06_1", "Fighter_AirCombo_Launcher", "Fighter_Combo02_3", "Fighter_Combo02_2_WeaponProxy",
    };

    private struct Win
    {
        public string Id;
        public float Start, End; // clip seconds
        public StrikeLimbMask Limbs;
        public bool Shockwave;
    }

    private static Win W(string id, float start, float end, StrikeLimbMask limbs, bool shockwave = false) =>
        new Win { Id = id, Start = start, End = end, Limbs = limbs, Shockwave = shockwave };

    // Designer corrections (null = the proposal is kept as-is).
    private static readonly Dictionary<string, (Win[] windows, string why)> Corrections = new Dictionary<string, (Win[], string)>
    {
        ["Fighter_Combo02_1"] = (new[] { W("Left hook", 0.18f, 0.32f, StrikeLimbMask.LeftHand) },
            "Proposal was a 0.05s forward 'Left punch'; the clip is a wide backhand hook — widened to the hand's whole fast arc (0.18–0.32s, peak 56 m/s) so the sweep's near and far edges are live."),
        ["Fighter_Combo02_2"] = (new[] { W("Overhead smash", 0.22f, 0.32f, StrikeLimbMask.RightHand) },
            "Timing/limb kept (right hand, 0.22–0.32s); renamed — it is an overhead smash, not a punch."),
        ["Fighter_Combo06_1"] = (new[]
            {
                W("Right sweep kick", 0.28f, 0.78f, StrikeLimbMask.RightFoot),
                W("Left sweep kick", 0.33f, 0.80f, StrikeLimbMask.LeftFoot),
            },
            "Sweep windows ended at 0.62s (half-peak speed); the legs keep circling at kicking speed until ~0.8s (contact sheet), so the windows were extended. Each foot stays its own hit instance (once per target per foot)."),
        ["Fighter_AirCombo_Launcher"] = (new[] { W("Rising kick", 0.28f, 0.40f, StrikeLimbMask.BothFeet) },
            "Leap path always proposes a hand 'Slam' at landing; this clip lands in a crouch with the hands at hip height — slam removed. Both feet rise together, so one window (one hit) instead of two."),
        ["Fighter_Combo02_3"] = (new[] { W("Hammer slam", 0.53f, 0.64f, StrikeLimbMask.RightHand, shockwave: true) },
            "Leap path proposed airborne kicks (the feet merely trail during the dive) and a two-hand slam; the blow is the right hammer-fist landing (~0.57s, 99 m/s). Kicks removed; slam = right hand + HV_GroundShockwave area on the hand for the group hit."),
        ["Fighter_Combo02_2_WeaponProxy"] = (new[] { W("Weapon chop", 0.20f, 0.34f, StrikeLimbMask.Weapon1) },
            "Weapon-socket proxy: the overhead smash re-pointed at the Weapon1 socket shape (hilt→tip under the right hand); window widened 0.02s each side because the 0.75m tip travels further than the fist."),
    };

    private static readonly (string name, Color color, (string attack, string ability)[] steps)[] Combos =
    {
        ("Combo_Test_QuickStrikes", new Color(0.3f, 0.75f, 1f), new[]
        {
            ("Fighter_Combo04_1", "ability.player_strike"), ("Fighter_Combo04_2", "ability.player_combo2"), ("Fighter_Combo03_1", "ability.player_combo3"),
        }),
        ("Combo_Test_Sweeping", new Color(0.35f, 0.9f, 0.4f), new[]
        {
            ("Fighter_Combo02_1", "ability.player_strike"), ("Fighter_Combo06_1", "ability.player_combo2"),
        }),
        ("Combo_Test_AerialHeavy", new Color(0.85f, 0.35f, 1f), new[]
        {
            ("Fighter_AirCombo_Launcher", "ability.player_strike"), ("Fighter_Combo02_2", "ability.player_combo2"), ("Fighter_Combo02_3", "ability.player_combo3"),
        }),
        ("Combo_Test_WeaponProxy", new Color(1f, 0.85f, 0.2f), new[]
        {
            ("Fighter_Combo02_2_WeaponProxy", "ability.player_strike"),
        }),
    };

    public static string Execute()
    {
        var report = new StringBuilder();
        HitVolumeAsset shockwave = EnsureShockwave(report);
        ApplyCorrections(shockwave, report);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        AuthorAnimatorStates(controller, report);
        var combos = AuthorCombos(report);
        AuthorWeaponSockets(report);
        AssetDatabase.SaveAssets();
        BuildPads(combos, report);
        return report.ToString();
    }

    private static AttackDefinition LoadAttack(string name) => AssetDatabase.LoadAssetAtPath<AttackDefinition>($"{AttackFolder}Attack_{name}.asset");

    // Every Frank clip is internally named "Take 001", so the state is named after the FBX file instead.
    private static string StateName(AttackDefinition attack) =>
        "Fighter_" + System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(attack.Clip)).Replace("Frank_RPG_Fighter_", string.Empty);

    // --- 1. Corrections -------------------------------------------------------------------------------

    private static void ApplyCorrections(HitVolumeAsset shockwave, StringBuilder report)
    {
        foreach (string attack in AttackNames)
        {
            AttackDefinition asset = LoadAttack(attack);
            var so = new SerializedObject(asset);
            float length = asset.Clip.length;
            if (Corrections.TryGetValue(attack, out var fix))
            {
                SerializedProperty windows = so.FindProperty("_windows");
                windows.arraySize = fix.windows.Length;
                for (int i = 0; i < fix.windows.Length; i++)
                {
                    Win w = fix.windows[i];
                    SerializedProperty p = windows.GetArrayElementAtIndex(i);
                    p.FindPropertyRelative("_id").stringValue = w.Id;
                    p.FindPropertyRelative("_start").floatValue = w.Start / length;
                    p.FindPropertyRelative("_end").floatValue = w.End / length;
                    p.FindPropertyRelative("_limbs").intValue = (int)w.Limbs;
                    p.FindPropertyRelative("_area").objectReferenceValue = w.Shockwave ? shockwave : null;
                    p.FindPropertyRelative("_areaBone").intValue = (int)HumanBodyBones.RightHand;
                    p.FindPropertyRelative("_abilityId").stringValue = string.Empty;
                    p.FindPropertyRelative("_rehitInterval").floatValue = 0f;
                    p.FindPropertyRelative("_verified").boolValue = false;
                }

                report.AppendLine($"{attack}: corrected — {fix.why}");
            }
            else
            {
                report.AppendLine($"{attack}: proposal kept.");
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            // Re-derive the commitment end / cancel point from the corrected windows with the proposer's own rules:
            // commitment never ends before the last window; a ≥0.6s tail after the last blow is cancellable.
            float last = asset.LastContactEnd;
            float commit = Mathf.Max(asset.CommitEnd, last + 0.02f / length);
            float cancel = last + 0.08f / length;
            so.Update();
            so.FindProperty("_commitEnd").floatValue = Mathf.Min(commit, 0.98f);
            so.FindProperty("_cancelFrom").floatValue = (commit - cancel) * length >= 0.6f ? cancel : 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }
    }

    // --- 2. Shockwave preset --------------------------------------------------------------------------

    private static HitVolumeAsset EnsureShockwave(StringBuilder report)
    {
        var asset = AssetDatabase.LoadAssetAtPath<HitVolumeAsset>(ShockwavePath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<HitVolumeAsset>();
            AssetDatabase.CreateAsset(asset, ShockwavePath);
        }

        var so = new SerializedObject(asset);
        SerializedProperty v = so.FindProperty("_volume");
        v.FindPropertyRelative("_shape").intValue = (int)HitShape.Sphere;
        v.FindPropertyRelative("_radius").floatValue = 2.2f;
        v.FindPropertyRelative("_coneHalfAngleDegrees").floatValue = 0f;
        v.FindPropertyRelative("_multiTarget").boolValue = true;
        v.FindPropertyRelative("_maxTargets").intValue = 8;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        report.AppendLine("HV_GroundShockwave: Sphere r=2.2 multi-target (max 8).");
        return asset;
    }

    // --- 3. Animator ----------------------------------------------------------------------------------

    private static void AuthorAnimatorStates(AnimatorController controller, StringBuilder report)
    {
        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState loco = AttackValidation.FindState(controller, "Locomotion");

        // Clean up a state/trigger from an earlier run that named states by the clip's internal name.
        AnimatorState stale = AttackValidation.FindState(controller, "Fighter_Take 001");
        if (stale != null) sm.RemoveState(stale);
        for (int i = controller.parameters.Length - 1; i >= 0; i--)
        {
            if (controller.parameters[i].name == "Fighter_Take 001") controller.RemoveParameter(i);
        }
        var done = new HashSet<string>();
        int row = 0;
        foreach (string attack in AttackNames)
        {
            AttackDefinition asset = LoadAttack(attack);
            string name = StateName(asset);
            if (!done.Add(name))
            {
                continue;
            }

            bool hasParam = false;
            foreach (AnimatorControllerParameter p in controller.parameters) hasParam |= p.name == name;
            if (!hasParam) controller.AddParameter(name, AnimatorControllerParameterType.Trigger);

            AnimatorState state = AttackValidation.FindState(controller, name) ??
                                  sm.AddState(name, new Vector3(900f, -200f + 60f * row, 0f));
            row++;
            state.motion = asset.Clip;
            state.speed = 1f;

            bool hasEntry = false;
            foreach (AnimatorStateTransition t in sm.anyStateTransitions) hasEntry |= t.destinationState == state;
            if (!hasEntry)
            {
                AnimatorStateTransition entry = sm.AddAnyStateTransition(state);
                entry.hasExitTime = false;
                entry.duration = 0.06f;
                entry.canTransitionToSelf = false;
                entry.AddCondition(AnimatorConditionMode.If, 0f, name);
                entry.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            }

            foreach (AnimatorStateTransition t in state.transitions)
            {
                if (t.destinationState == loco) state.RemoveTransition(t);
            }

            AnimatorStateTransition exit = state.AddTransition(loco);
            exit.hasExitTime = false;
            exit.duration = 0.12f;
            exit.interruptionSource = TransitionInterruptionSource.None;
            exit.AddCondition(AnimatorConditionMode.IfNot, 0f, "AttackCommitted");
            report.AppendLine($"Animator: state + trigger '{name}'.");
        }

        EditorUtility.SetDirty(controller);
    }

    // --- 4. Combos ------------------------------------------------------------------------------------

    private static List<ComboDefinition> AuthorCombos(StringBuilder report)
    {
        var result = new List<ComboDefinition>();
        foreach (var (name, _, steps) in Combos)
        {
            string path = $"{DefinitionsFolder}{name}.asset";
            var combo = AssetDatabase.LoadAssetAtPath<ComboDefinition>(path);
            if (combo == null)
            {
                combo = ScriptableObject.CreateInstance<ComboDefinition>();
                AssetDatabase.CreateAsset(combo, path);
            }

            var so = new SerializedObject(combo);
            SerializedProperty list = so.FindProperty("_steps");
            list.arraySize = steps.Length;
            var line = new StringBuilder($"{name}:");
            for (int i = 0; i < steps.Length; i++)
            {
                AttackDefinition attack = LoadAttack(steps[i].attack);
                string state = StateName(attack);
                float seconds = attack.Clip.length;
                // Fallback timers only (the attack asset drives the step): never ahead of the windows / commitment.
                float windUp = attack.LastContactEnd * seconds + 0.05f;
                float recovery = Mathf.Max(0.1f, seconds - windUp + 0.05f);
                SerializedProperty p = list.GetArrayElementAtIndex(i);
                p.FindPropertyRelative("_attackAbilityId").stringValue = steps[i].ability;
                p.FindPropertyRelative("_attack").objectReferenceValue = attack;
                p.FindPropertyRelative("_animatorState").stringValue = state;
                p.FindPropertyRelative("_animatorTrigger").stringValue = state;
                p.FindPropertyRelative("_windUpDuration").floatValue = windUp;
                p.FindPropertyRelative("_recoveryDuration").floatValue = recovery;
                p.FindPropertyRelative("_continuationWindow").floatValue = 0f;
                line.Append($" [{i + 1}] {state}");
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(combo);
            result.Add(combo);
            report.AppendLine(line.ToString());
        }

        return result;
    }

    // --- 5. Weapon socket proxy -----------------------------------------------------------------------

    private static void AuthorWeaponSockets(StringBuilder report)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
        try
        {
            AddWeaponSockets(root);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            report.AppendLine("Player.prefab: Weapon1 = sockets WeaponProxy_Hilt→Tip under RightHand (0.75m, r 0.08).");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // A virtual 0.75m baton extending the forearm line past the fist, parented to the hand bone so it follows the
    // animation exactly as a held weapon's sockets would; Weapon1 strikes along it. Only the Weapon1 shape is
    // touched — the profile's other settings (e.g. the dojo player's reach padding) are left as authored.
    private static void AddWeaponSockets(GameObject root)
    {
        Animator animator = root.GetComponentInChildren<Animator>();
        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform forearm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        Transform hilt = hand.Find("WeaponProxy_Hilt") ?? new GameObject("WeaponProxy_Hilt").transform;
        Transform tip = hand.Find("WeaponProxy_Tip") ?? new GameObject("WeaponProxy_Tip").transform;
        hilt.SetParent(hand, false);
        tip.SetParent(hand, false);
        Vector3 along = (hand.position - forearm.position).normalized;
        hilt.position = hand.position + along * 0.08f;
        tip.position = hand.position + along * 0.83f;

        var profile = Ensure<CharacterStrikeProfile>(root);
        var so = new SerializedObject(profile);
        SerializedProperty shapes = so.FindProperty("_shapes");
        int index = -1;
        for (int i = 0; i < shapes.arraySize; i++)
        {
            if (shapes.GetArrayElementAtIndex(i).FindPropertyRelative("_limb").intValue == (int)StrikeLimb.Weapon1) index = i;
        }

        if (index < 0)
        {
            index = shapes.arraySize;
            shapes.arraySize++;
        }

        SerializedProperty s = shapes.GetArrayElementAtIndex(index);
        s.FindPropertyRelative("_limb").intValue = (int)StrikeLimb.Weapon1;
        s.FindPropertyRelative("_source").intValue = (int)StrikeShapeSource.Sockets;
        s.FindPropertyRelative("_startSocket").objectReferenceValue = hilt;
        s.FindPropertyRelative("_endSocket").objectReferenceValue = tip;
        s.FindPropertyRelative("_endExtension").floatValue = 0f;
        s.FindPropertyRelative("_radius").floatValue = 0.08f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // --- 6. Dojo loadout pads -------------------------------------------------------------------------

    private static void BuildPads(List<ComboDefinition> combos, StringBuilder report)
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        GameObject arena = GameObject.Find("Arena");
        Transform parent = arena != null ? arena.transform : null;
        Transform group = parent != null ? parent.Find("ComboPads") : GameObject.Find("ComboPads")?.transform;
        if (group == null)
        {
            group = new GameObject("ComboPads").transform;
            group.SetParent(parent, false);
        }

        var all = new List<(string label, ComboDefinition combo, Color color)>
        {
            ("Shipped Melee", AssetDatabase.LoadAssetAtPath<ComboDefinition>($"{DefinitionsFolder}Combo_PlayerMelee.asset"), new Color(0.85f, 0.85f, 0.85f)),
        };
        for (int i = 0; i < combos.Count; i++)
        {
            all.Add((Combos[i].name.Replace("Combo_Test_", string.Empty), combos[i], Combos[i].color));
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        for (int i = 0; i < all.Count; i++)
        {
            string padName = $"ComboPad_{all[i].label.Replace(" ", string.Empty)}";
            Transform existing = group.Find(padName);
            GameObject pad = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = padName;
            pad.transform.SetParent(group, false);
            pad.transform.position = new Vector3(-6f + 3f * i, 0.05f, -4f);
            pad.transform.localScale = new Vector3(1.6f, 0.1f, 1.6f);
            pad.GetComponent<BoxCollider>().isTrigger = true;

            string matPath = $"Assets/Game/Content/Materials/M_{padName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", all[i].color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", all[i].color);
            EditorUtility.SetDirty(mat);
            pad.GetComponent<Renderer>().sharedMaterial = mat;

            var trigger = Ensure<ComboLoadoutPad>(pad);
            var so = new SerializedObject(trigger);
            so.FindProperty("_combo").objectReferenceValue = all[i].combo;
            so.ApplyModifiedPropertiesWithoutUndo();

            Transform labelT = pad.transform.Find("Label");
            GameObject label = labelT != null ? labelT.gameObject : new GameObject("Label");
            label.transform.SetParent(pad.transform, false);
            label.transform.position = pad.transform.position + new Vector3(0f, 1.2f, 0f);
            label.transform.localScale = new Vector3(1f / 1.6f, 10f, 1f / 1.6f) * 0.12f;
            var text = Ensure<TextMesh>(label);
            text.text = all[i].label;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = 0.5f;
            text.color = all[i].color;
            report.AppendLine($"Pad '{padName}' at {pad.transform.position} → {(all[i].combo != null ? all[i].combo.name : "MISSING")}");
        }

        // The dojo's Player is a scene object (not a Player.prefab instance), so it needs the socket too.
        PlayerCombat player = Object.FindAnyObjectByType<PlayerCombat>(FindObjectsInactive.Include);
        if (player != null)
        {
            AddWeaponSockets(player.gameObject);
            report.AppendLine($"Scene player '{player.name}': Weapon1 sockets added (other profile settings untouched).");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
