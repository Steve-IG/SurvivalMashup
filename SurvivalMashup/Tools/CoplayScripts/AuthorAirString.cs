using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ToyChest.Editor;
using ToyChest.Gameplay.Combat;
using ToyChest.Gameplay.HitDetection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Aerial combos, stage 1 — a real air string authored through the existing pipeline: Frank RPG Fighter
/// AirCombo_Launcher → AirCombo_1 → AirCombo_2 → AirCombo_3, using the pack's <c>_0H</c> (ZeroHeight) clips, whose
/// root stays at standing height so the CHARACTER CONTROLLER supplies the height. Each attack asset gets an
/// <see cref="AttackDefinition.AirHeight"/> profile derived from the clip's full-height twin (its root height minus
/// the _0H twin's, so the profile is exactly the jump the animator authored), which PlayerCombat feeds to
/// PlayerLocomotion while the step is committed (gravity held). No per-attack gameplay code.
/// <para>Entry points: <c>Propose</c> (configure clips, create attacks via CreateAttackAsset → Propose (on the
/// full-height twin, so the leap analysis sees the real height) → ApplyProposal, author height profiles; report to
/// Temp/AirStringProposals.txt) and <c>Execute</c> (designer corrections, Animator states, Combo_Test_AirString, a
/// loadout pad). Idempotent. Editor-only tooling.</para>
/// </summary>
public static class AuthorAirString
{
    private const string Folder = "Assets/Frank_Fighter/Assets/Animations/FBX Animation/Root_Motion/Frank_RPG_Fighter_";
    private const string PlayerPrefab = "Assets/Game/Content/Prefabs/Player.prefab";
    private const string ScenePath = "Assets/Game/Scenes/VerticalSlice.unity";
    private const string ControllerPath = "Assets/Game/Content/Animation/PlayerAnimator.controller";
    private const string ComboPath = "Assets/Game/Content/Definitions/Combo_Test_AirString.asset";

    /// <summary>The air string, in order: clip (full-height twin = clip without the _0H suffix) and the step's ability.</summary>
    public static readonly (string clip, string ability)[] Steps =
    {
        ("AirCombo_Launcher", "ability.player_strike"),
        ("AirCombo_1", "ability.player_combo2"),
        ("AirCombo_2", "ability.player_combo2"),
        ("AirCombo_3", "ability.player_combo3"),
    };

    private static string AttackName(string clip) => $"Fighter_{clip}_0H";
    private static string StateName(string clip) => $"Fighter_{clip}_0H";
    private static AttackDefinition LoadAttack(string clip) =>
        AssetDatabase.LoadAssetAtPath<AttackDefinition>($"Assets/Game/Content/Attacks/Attack_{AttackName(clip)}.asset");

    // --- phase 1: clips, attacks, proposals, height profiles --------------------------------------------

    public static string Propose()
    {
        var report = new StringBuilder();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
        GameObject preview = Object.Instantiate(prefab);
        preview.hideFlags = HideFlags.DontSave;
        foreach (MonoBehaviour b in preview.GetComponentsInChildren<MonoBehaviour>()) b.enabled = false;
        Animator animator = preview.GetComponentInChildren<Animator>();
        try
        {
            foreach ((string name, _) in Steps)
            {
                AnimationClip zero = ConfigureClip($"{Folder}{name}_0H.FBX", report);
                var full = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{Folder}{name}.FBX");
                AttackDefinition attack = AttackAuthoringWindow.CreateAttackAsset(zero, AttackName(name));

                AttackWindowProposer.Proposal proposal = AttackWindowProposer.Propose(animator, full, 1f, -1f);
                AttackAuthoringWindow.ApplyProposal(attack, proposal);
                AnimationCurve height = HeightProfile(full, zero);
                var so = new SerializedObject(attack);
                so.FindProperty("_airHeight").animationCurveValue = height;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(attack);

                report.AppendLine($"## {AttackName(name)} ({zero.length:0.00}s) — proposed on the full-height twin {name}");
                report.AppendLine(proposal.Report.TrimEnd());
                report.AppendLine("Height profile (m above step start, every 10%):" +
                                  string.Concat(Enumerable.Range(0, 11).Select(i => $" {height.Evaluate(i / 10f) - height.Evaluate(0f):0.00}")));
                report.AppendLine(AttackValidation.TimingSummary(attack));
            }
        }
        finally
        {
            Object.DestroyImmediate(preview);
        }

        AssetDatabase.SaveAssets();
        File.WriteAllText("Temp/AirStringProposals.txt", report.ToString());
        return report.ToString();
    }

    // Same root settings as the library clips, except height is baked "based upon Original" rather than Feet: a
    // ZeroHeight air pose has its legs tucked, and Feet-based baking would plant them on the floor.
    private static AnimationClip ConfigureClip(string path, StringBuilder report)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        bool changed = importer.clipAnimations.Length == 0;
        foreach (ModelImporterClipAnimation c in clips)
        {
            changed |= !c.lockRootRotation || !c.keepOriginalOrientation || !c.lockRootHeightY || !c.keepOriginalPositionY ||
                       c.lockRootPositionXZ;
            c.lockRootRotation = true;        // Root Transform Rotation: Bake Into Pose (Original)
            c.keepOriginalOrientation = true;
            c.lockRootHeightY = true;         // Root Transform Position (Y): Bake Into Pose (Original) — height comes from the controller
            c.keepOriginalPositionY = true;
            c.heightFromFeet = false;
            c.lockRootPositionXZ = false;     // XZ stays root motion (PlayerLocomotion)
        }

        if (changed)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        report.AppendLine($"Clip {Path.GetFileNameWithoutExtension(path)}: {(changed ? "rotation + Y baked (Original), XZ root motion" : "already configured")}.");
        return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
    }

    // The jump the animator authored: the full clip's root height minus the ZeroHeight twin's, over normalized time.
    private static AnimationCurve HeightProfile(AnimationClip full, AnimationClip zero)
    {
        AnimationCurve fy = RootY(full), zy = RootY(zero);
        var curve = new AnimationCurve();
        const int samples = 60;
        for (int i = 0; i <= samples; i++)
        {
            float n = i / (float)samples;
            float h = fy.Evaluate(n * full.length) - zy.Evaluate(n * zero.length);
            curve.AddKey(new Keyframe(n, h));
        }

        for (int i = 0; i < curve.length; i++) AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        for (int i = 0; i < curve.length; i++) AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        return curve;
    }

    private static AnimationCurve RootY(AnimationClip clip)
    {
        foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(clip))
        {
            if (b.propertyName == "RootT.y") return AnimationUtility.GetEditorCurve(clip, b);
        }

        return AnimationCurve.Constant(0f, clip.length, 0f);
    }

    // --- phase 2: corrections, animator, combo, pad --------------------------------------------------

    private struct Win
    {
        public string Id;
        public float Start, End; // seconds
        public StrikeLimbMask Limbs;
        public bool Shockwave;
    }

    private static Win W(string id, float start, float end, StrikeLimbMask limbs, bool shockwave = false) =>
        new Win { Id = id, Start = start, End = end, Limbs = limbs, Shockwave = shockwave };
    private const string ShockwavePath = "Assets/Game/Content/HitVolumes/HV_GroundShockwave.asset";

    /// <summary>
    /// Designer corrections (seconds), filled in after reviewing Temp/AirStringProposals.txt and the contact sheets.
    /// Key = step clip; null = keep the proposal. The chain to the next step happens when a step's last window closes,
    /// so every non-final step's windows must close while it is still airborne (before its clip's own descent).
    /// </summary>
    // From SurveyLimbSweeps (limb-tip speed peaks on the full-height twins) and the contact sheets. Each clip is a
    // standalone move that ends by landing, so the proposer — correctly for a standalone clip — puts a landing slam
    // last; in a string that would make every step land before chaining. Non-final steps therefore keep only their
    // airborne blows (chain = last window close, while still airborne); only the finisher keeps its landing slam.
    private static readonly Dictionary<string, Win[]> Corrections = new Dictionary<string, Win[]>
    {
        // Feet whip up together at 61 m/s (0.32–0.53s): one rising flip kick; the clip's landing slam is dropped
        // (RG5E made the same call: "the clip lands in a crouch, no slam"). Chain 0.48s, ~1.6m up near the apex.
        ["AirCombo_Launcher"] = new[] { W("Rising kick", 0.30f, 0.48f, StrikeLimbMask.BothFeet) },
        // Proposer saw only the landing slam (its leap path scores feet ≥ 18 m/s); the blows are the hands: left
        // strike 34 m/s @0.40, left spin strike 72 m/s @0.77 (235° arc), right spin strike 51 m/s @1.08 with the
        // right foot. Chain 1.20s, before the clip's own descent (~1.45s).
        ["AirCombo_1"] = new[]
        {
            W("Left strike", 0.36f, 0.45f, StrikeLimbMask.LeftHand),
            W("Left spin strike", 0.72f, 0.88f, StrikeLimbMask.LeftHand),
            W("Right spin strike", 1.02f, 1.20f, StrikeLimbMask.RightHand | StrikeLimbMask.RightFoot),
        },
        // Spin kick (left foot 77 m/s, right 34 m/s @0.28; the proposer flagged it as an unproposed sweep), then a
        // right kick 43 m/s @0.65. The proposer's punches are the arms riding the spin (≤ 18 m/s). Chain 0.73s.
        ["AirCombo_2"] = new[]
        {
            W("Spin kick", 0.22f, 0.38f, StrikeLimbMask.BothFeet),
            W("Right kick", 0.58f, 0.73f, StrikeLimbMask.RightFoot),
        },
        // Finisher: both hands 64–68 m/s @0.52 with the left foot (an overhead swing at the top), then the dive
        // lands (height reaches the floor ~1.22s): a two-fist slam with the ground shockwave, so the string pays off
        // against grounded enemies too.
        ["AirCombo_3"] = new[]
        {
            W("Overhead swing", 0.48f, 0.58f, StrikeLimbMask.BothHands | StrikeLimbMask.LeftFoot),
            W("Landing slam", -1f, -1f, StrikeLimbMask.BothHands, shockwave: true), // timed from the string's height (LandingTime)
        },
    };

    public static string Execute()
    {
        var report = new StringBuilder();
        float entry = 0f; // the string's height above the floor as each step begins (accumulated like PlayerCombat)
        foreach ((string name, _) in Steps)
        {
            AttackDefinition attack = LoadAttack(name);
            if (Corrections.TryGetValue(name, out Win[] wins) && wins != null)
            {
                for (int i = 0; i < wins.Length; i++)
                {
                    if (wins[i].Start >= 0f) continue;
                    float land = LandingTime(attack, entry);
                    wins[i].Start = land - 0.04f;
                    wins[i].End = land + 0.08f;
                    report.AppendLine($"{attack.name}: enters at {entry:0.00}m; the dive reaches the floor at {land:0.00}s → '{wins[i].Id}' {wins[i].Start:0.00}–{wins[i].End:0.00}s.");
                }

                Correct(attack, wins, report);
            }

            entry += attack.AirHeightDelta(0f, attack.LastContactEnd);
            report.AppendLine(AttackValidation.TimingSummary(attack));
        }

        AuthorAnimatorStates(report);
        ComboDefinition combo = AuthorCombo(report);
        AssetDatabase.SaveAssets();
        BuildPad(combo, report);
        File.WriteAllText("Temp/AuthorAirString.txt", report.ToString());
        return report.ToString();
    }

    // The first moment (seconds) the accumulated height reaches the floor — or, if the string ends a little higher
    // than the original take (chain points shave or add height), its lowest point. The proposer's leap "landing" is
    // when a clip returns to its OWN start height, which for a step that begins high is still mid-air.
    private static float LandingTime(AttackDefinition attack, float entry)
    {
        const int samples = 240;
        float lowest = float.MaxValue;
        for (int i = 0; i <= samples; i++) lowest = Mathf.Min(lowest, entry + attack.AirHeightDelta(0f, i / (float)samples));
        float threshold = Mathf.Max(0.05f, lowest + 0.05f);
        for (int i = 0; i <= samples; i++)
        {
            if (entry + attack.AirHeightDelta(0f, i / (float)samples) <= threshold) return i / (float)samples * attack.Clip.length;
        }

        return attack.Clip.length;
    }

    private static void Correct(AttackDefinition attack, Win[] wins, StringBuilder report)
    {
        float length = attack.Clip.length;
        var so = new SerializedObject(attack);
        SerializedProperty windows = so.FindProperty("_windows");
        windows.arraySize = wins.Length;
        for (int i = 0; i < wins.Length; i++)
        {
            SerializedProperty w = windows.GetArrayElementAtIndex(i);
            w.FindPropertyRelative("_id").stringValue = wins[i].Id;
            w.FindPropertyRelative("_start").floatValue = wins[i].Start / length;
            w.FindPropertyRelative("_end").floatValue = wins[i].End / length;
            w.FindPropertyRelative("_limbs").intValue = (int)wins[i].Limbs;
            w.FindPropertyRelative("_area").objectReferenceValue = wins[i].Shockwave ? AssetDatabase.LoadAssetAtPath<HitVolumeAsset>(ShockwavePath) : null;
            w.FindPropertyRelative("_areaBone").intValue = (int)HumanBodyBones.RightHand;
            w.FindPropertyRelative("_abilityId").stringValue = string.Empty;
            w.FindPropertyRelative("_rehitInterval").floatValue = 0f;
            w.FindPropertyRelative("_verified").boolValue = false;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(attack);
        report.AppendLine($"Correction {attack.name}: " + string.Join(", ", wins.Select(w => $"'{w.Id}' {w.Start:0.00}–{w.End:0.00}s {w.Limbs}")) + " (unverified).");
    }

    private static void AuthorAnimatorStates(StringBuilder report)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState loco = AttackValidation.FindState(controller, "Locomotion");
        int row = 0;
        foreach ((string name, _) in Steps)
        {
            string state = StateName(name);
            if (!controller.parameters.Any(p => p.name == state)) controller.AddParameter(state, AnimatorControllerParameterType.Trigger);
            AnimatorState s = AttackValidation.FindState(controller, state) ?? sm.AddState(state, new Vector3(1150f, -140f + 60f * row, 0f));
            row++;
            s.motion = LoadAttack(name).Clip;
            s.speed = 1f;
            if (!sm.anyStateTransitions.Any(t => t.destinationState == s))
            {
                AnimatorStateTransition entry = sm.AddAnyStateTransition(s);
                entry.hasExitTime = false;
                entry.duration = 0.06f;
                entry.canTransitionToSelf = false;
                entry.AddCondition(AnimatorConditionMode.If, 0f, state);
                entry.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            }

            foreach (AnimatorStateTransition t in s.transitions.ToArray())
            {
                if (t.destinationState == loco) s.RemoveTransition(t);
            }

            AnimatorStateTransition exit = s.AddTransition(loco);
            exit.hasExitTime = false;
            exit.duration = 0.12f;
            exit.interruptionSource = TransitionInterruptionSource.None;
            exit.AddCondition(AnimatorConditionMode.IfNot, 0f, "AttackCommitted");
        }

        EditorUtility.SetDirty(controller);
        report.AppendLine("Animator: states + triggers " + string.Join(", ", Steps.Select(s => StateName(s.clip))) + " (AnyState in, exit on !AttackCommitted).");
    }

    private static ComboDefinition AuthorCombo(StringBuilder report)
    {
        var combo = AssetDatabase.LoadAssetAtPath<ComboDefinition>(ComboPath);
        if (combo == null)
        {
            combo = ScriptableObject.CreateInstance<ComboDefinition>();
            AssetDatabase.CreateAsset(combo, ComboPath);
        }

        var so = new SerializedObject(combo);
        SerializedProperty list = so.FindProperty("_steps");
        list.arraySize = Steps.Length;
        for (int i = 0; i < Steps.Length; i++)
        {
            AttackDefinition attack = LoadAttack(Steps[i].clip);
            float seconds = attack.Clip.length;
            float windUp = attack.LastContactEnd * seconds + 0.05f; // fallback timers never ahead of the asset
            float recovery = Mathf.Max(0.1f, seconds - windUp + 0.05f);
            SerializedProperty p = list.GetArrayElementAtIndex(i);
            p.FindPropertyRelative("_attackAbilityId").stringValue = Steps[i].ability;
            p.FindPropertyRelative("_attack").objectReferenceValue = attack;
            p.FindPropertyRelative("_animatorState").stringValue = StateName(Steps[i].clip);
            p.FindPropertyRelative("_animatorTrigger").stringValue = StateName(Steps[i].clip);
            p.FindPropertyRelative("_windUpDuration").floatValue = windUp;
            p.FindPropertyRelative("_recoveryDuration").floatValue = recovery;
            p.FindPropertyRelative("_continuationWindow").floatValue = 0f;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(combo);
        report.AppendLine("Combo_Test_AirString: " + string.Join(" → ", Steps.Select(s => StateName(s.clip))) + ".");
        return combo;
    }

    // A seventh loadout pad, built exactly like the others (1m trigger column).
    private static void BuildPad(ComboDefinition combo, StringBuilder report)
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform group = GameObject.Find("ComboPads")?.transform;
        const string padName = "ComboPad_AirString";
        Transform existing = group != null ? group.Find(padName) : null;
        GameObject pad = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = padName;
        if (group != null) pad.transform.SetParent(group, false);
        pad.transform.position = new Vector3(12f, 0.05f, -4f);
        pad.transform.localScale = new Vector3(1.6f, 0.1f, 1.6f);
        var box = pad.GetComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(1f, 1f / 0.1f, 1f);
        box.center = new Vector3(0f, (0.5f - 0.05f) / 0.1f, 0f);

        Color color = new Color(0.4f, 0.9f, 0.95f);
        const string matPath = "Assets/Game/Content/Materials/M_ComboPad_AirString.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, matPath);
        }

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        EditorUtility.SetDirty(mat);
        pad.GetComponent<Renderer>().sharedMaterial = mat;

        var trigger = pad.GetComponent<ToyChest.Gameplay.Player.ComboLoadoutPad>();
        if (trigger == null) trigger = pad.AddComponent<ToyChest.Gameplay.Player.ComboLoadoutPad>();
        var so = new SerializedObject(trigger);
        so.FindProperty("_combo").objectReferenceValue = combo;
        so.ApplyModifiedPropertiesWithoutUndo();

        Transform labelT = pad.transform.Find("Label");
        GameObject label = labelT != null ? labelT.gameObject : new GameObject("Label");
        label.transform.SetParent(pad.transform, false);
        label.transform.position = pad.transform.position + new Vector3(0f, 1.2f, 0f);
        label.transform.localScale = new Vector3(1f / 1.6f, 10f, 1f / 1.6f) * 0.12f;
        var text = label.GetComponent<TextMesh>();
        if (text == null) text = label.AddComponent<TextMesh>();
        text.text = "AirString";
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.fontSize = 48;
        text.characterSize = 0.5f;
        text.color = color;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        report.AppendLine($"Pad '{padName}' at {pad.transform.position} → {combo.name}.");
    }

    /// <summary>Marks every air-string window verified (run only after contact sheets and live hits pass).</summary>
    public static string MarkVerified()
    {
        var sb = new StringBuilder();
        foreach ((string name, _) in Steps)
        {
            AttackDefinition attack = LoadAttack(name);
            var so = new SerializedObject(attack);
            SerializedProperty windows = so.FindProperty("_windows");
            for (int i = 0; i < windows.arraySize; i++) windows.GetArrayElementAtIndex(i).FindPropertyRelative("_verified").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(attack);
            sb.AppendLine($"{attack.name}: {windows.arraySize} window(s) verified");
        }

        AssetDatabase.SaveAssets();
        return sb.ToString();
    }
}
