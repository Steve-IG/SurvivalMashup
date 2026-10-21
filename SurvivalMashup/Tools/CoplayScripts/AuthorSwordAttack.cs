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
/// RG5F follow-up — the first REAL weapon attack. Authors one sword attack through the existing pipeline, with no
/// gameplay code: a JC Stylized Weapons sword prop held in the Player's right hand (Player.prefab and the dojo
/// scene Player), a <c>Weapon2</c> strike shape on sockets at the blade's base and tip (so the RG5E Weapon1 proxy
/// stays intact), a Frank RPG Dual humanoid sword clip configured like the library clips, an attack asset made with
/// the Attack Authoring tool's own CreateAttackAsset → Propose → ApplyProposal path and then corrected from the
/// measured blade-tip motion, an Animator state, <c>Combo_Test_Sword</c>, and a sixth dojo loadout pad.
/// <para>Entry points: <c>Analyse</c> (attach + report the blade-tip motion of each candidate clip, no asset changes
/// beyond the prop) and <c>Execute</c> (everything). Idempotent. Editor-only tooling.</para>
/// </summary>
public static class AuthorSwordAttack
{
    private const string SwordPrefab = "Assets/JC_StylizedWeapons/Prefabs/SM_Sword_01.prefab";
    private const string PlayerPrefab = "Assets/Game/Content/Prefabs/Player.prefab";
    private const string ScenePath = "Assets/Game/Scenes/VerticalSlice.unity";
    private const string ControllerPath = "Assets/Game/Content/Animation/PlayerAnimator.controller";
    private const string DualFolder = "Assets/Frank_RPG_Dual/Assets/Animations/FBX Animation/Root_Motion/Frank_RPG_Dual_";
    private const string PropName = "Sword_Prop";

    // SM_Sword_01 geometry (mesh space, measured): grip/pommel y −0.15…−0.02, guard 0…0.25, blade 0.25…1.05.
    private const float GripY = -0.08f;
    private const float BladeBaseY = 0.25f;
    private const float BladeTipY = 1.05f;
    private const float BladeRadius = 0.07f;

    public static readonly string[] Candidates =
    {
        "Combo01_1", "Combo01_2", "Combo01_3", "Combo02_1", "Combo03_1", "Combo04_1", "Skill03_PowerAttack",
    };

    // --- prop + sockets -------------------------------------------------------------------------------

    // The grip Frank RPG Dual's own clips were animated with, measured from its skinned Sword_R on its own rig
    // (Frank_Dual_Unity_Skin posed by Combo01_1, identical at every sampled frame) and expressed in the hand's
    // anatomical frame — f = wrist→middle knuckle, a = pinky→index across the knuckles, p = out of the palm:
    // blade (0.56 f, 0.82 a, 0.12 p); edge ⟂ blade toward (0.83 f, −0.56 a); grip at 0.50 × the knuckle span along f,
    // −0.05 a, +0.04 p (metres on a 0.123 m hand, scaled to this rig's hand).
    private static readonly Vector3 DualBlade = new Vector3(0.56f, 0.82f, 0.12f);
    private static readonly Vector3 DualEdge = new Vector3(0.83f, -0.56f, 0f);
    private const float DualKnuckles = 0.123f;

    /// <summary>
    /// Puts the sword in the right fist exactly as the Frank RPG Dual animations hold theirs (see DualBlade). The
    /// frame comes from the rig's own finger bones, so it is anatomical rather than tied to one skeleton's bone axes;
    /// humanoid retargeting preserves that relationship between the hand and the motion.
    /// </summary>
    public static Transform AttachSword(GameObject root)
    {
        Animator animator = root.GetComponentInChildren<Animator>();
        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
        Transform middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
        Transform little = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);

        Vector3 fingers = (middle.position - hand.position).normalized;              // wrist → knuckles
        Vector3 across = Vector3.ProjectOnPlane(index.position - little.position, fingers).normalized; // pinky → index
        Vector3 palm = Vector3.Cross(fingers, across).normalized;                    // out of the palm
        float knuckles = Vector3.Distance(hand.position, middle.position);

        Transform prop = hand.Find(PropName);
        if (prop == null)
        {
            var sword = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SwordPrefab));
            prop = sword.transform;
            prop.name = PropName;
            prop.SetParent(hand, false);
        }

        // Sword mesh axes: +Y blade, +X blade width (edge), +Z flat.
        Vector3 blade = (fingers * DualBlade.x + across * DualBlade.y + palm * DualBlade.z).normalized;
        Vector3 edge = Vector3.ProjectOnPlane(fingers * DualEdge.x + across * DualEdge.y, blade).normalized;
        prop.rotation = Quaternion.LookRotation(Vector3.Cross(edge, blade), blade);
        prop.localScale = Vector3.one / Mathf.Max(1e-4f, hand.lossyScale.x);
        float k = knuckles / DualKnuckles;
        Vector3 grip = hand.position + fingers * (knuckles * 0.50f) + (across * -0.05f + palm * 0.04f) * k;
        prop.position = grip - prop.rotation * new Vector3(0f, GripY, 0f);

        foreach (Collider c in prop.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c, true);

        Transform hilt = Socket(prop, "Sword_Hilt", BladeBaseY);
        Transform tip = Socket(prop, "Sword_Tip", BladeTipY);

        var profile = root.GetComponent<CharacterStrikeProfile>();
        if (profile == null) profile = root.AddComponent<CharacterStrikeProfile>();
        var so = new SerializedObject(profile);
        SerializedProperty shapes = so.FindProperty("_shapes");
        int at = -1;
        for (int i = 0; i < shapes.arraySize; i++)
        {
            if (shapes.GetArrayElementAtIndex(i).FindPropertyRelative("_limb").intValue == (int)StrikeLimb.Weapon2) at = i;
        }

        if (at < 0)
        {
            at = shapes.arraySize;
            shapes.arraySize++;
        }

        SerializedProperty s = shapes.GetArrayElementAtIndex(at);
        s.FindPropertyRelative("_limb").intValue = (int)StrikeLimb.Weapon2;
        s.FindPropertyRelative("_source").intValue = (int)StrikeShapeSource.Sockets;
        s.FindPropertyRelative("_startSocket").objectReferenceValue = hilt;
        s.FindPropertyRelative("_endSocket").objectReferenceValue = tip;
        s.FindPropertyRelative("_endExtension").floatValue = 0f;
        s.FindPropertyRelative("_radius").floatValue = BladeRadius;
        so.ApplyModifiedPropertiesWithoutUndo();
        return prop;
    }

    private static Transform Socket(Transform prop, string name, float y)
    {
        Transform t = prop.Find(name) ?? new GameObject(name).transform;
        t.SetParent(prop, false);
        t.localPosition = new Vector3(0f, y, 0f);
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;
        return t;
    }

    private static void AttachToPrefab(StringBuilder report)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
        try
        {
            AttachSword(root);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            report.AppendLine("Player.prefab: Sword_Prop (SM_Sword_01) in the right hand; Weapon2 = Sword_Hilt→Sword_Tip (0.80m, r 0.07).");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void AttachToScenePlayer(StringBuilder report)
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var combat = Object.FindAnyObjectByType<ToyChest.Gameplay.Player.PlayerCombat>();
        AttachSword(combat.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        report.AppendLine($"Scene player '{combat.name}': Sword_Prop + Weapon2 sockets (other profile settings untouched).");
    }

    // --- analysis -------------------------------------------------------------------------------------

    private struct Motion
    {
        public float Length, PeakSpeed, PeakTime, Start, End, Travel, YawSpan, FrontAtPeak, HeightAtPeak, LeftPeak;
    }

    // Samples the clip on a Player preview and measures the blade tip (Weapon2) relative to the hips: its speed
    // curve, the fast span (≥ 45% of peak), how far it travels and sweeps in that span, and where it is at peak.
    private static Motion Measure(GameObject preview, AnimationClip clip, StringBuilder curve, int every = 3)
    {
        Animator animator = preview.GetComponentInChildren<Animator>();
        Dictionary<StrikeLimb, StrikeShape> shapes = CharacterStrikeProfile.ResolveShapes(
            animator, preview.GetComponent<CharacterStrikeProfile>().AuthoredShapes);
        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        Vector3 facing = Vector3.forward;
        const float dt = 1f / 60f;
        var times = new List<float>();
        var rel = new List<Vector3>();
        var leftRel = new List<Vector3>();

        AnimationMode.StartAnimationMode();
        try
        {
            for (float t = 0f; t <= clip.length + 1e-4f; t += dt)
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(animator.gameObject, clip, t);
                AnimationMode.EndSampling();
                CharacterStrikeProfile.TryResolvePose(animator, shapes[StrikeLimb.Weapon2], out _, out Vector3 tip);
                times.Add(t);
                rel.Add(tip - hips.position);
                leftRel.Add(leftHand.position - hips.position);
            }
        }
        finally
        {
            AnimationMode.StopAnimationMode();
        }

        var m = new Motion { Length = clip.length };
        var speed = new float[rel.Count];
        for (int i = 1; i < rel.Count; i++)
        {
            speed[i] = (rel[i] - rel[i - 1]).magnitude / dt;
            float left = (leftRel[i] - leftRel[i - 1]).magnitude / dt;
            m.LeftPeak = Mathf.Max(m.LeftPeak, left);
            if (speed[i] > m.PeakSpeed) { m.PeakSpeed = speed[i]; m.PeakTime = times[i]; }
        }

        int peak = times.IndexOf(m.PeakTime);
        int a = peak, b = peak;
        while (a > 1 && speed[a - 1] >= 0.45f * m.PeakSpeed) a--;
        while (b < speed.Length - 1 && speed[b + 1] >= 0.45f * m.PeakSpeed) b++;
        m.Start = times[a];
        m.End = times[b];
        for (int i = a + 1; i <= b; i++) m.Travel += (rel[i] - rel[i - 1]).magnitude;
        float yaw0 = Vector3.SignedAngle(facing, new Vector3(rel[a].x, 0f, rel[a].z), Vector3.up);
        float yaw1 = Vector3.SignedAngle(facing, new Vector3(rel[b].x, 0f, rel[b].z), Vector3.up);
        m.YawSpan = Mathf.DeltaAngle(yaw0, yaw1);
        m.FrontAtPeak = Vector3.Dot(rel[peak], facing);
        m.HeightAtPeak = rel[peak].y + hips.position.y;

        if (curve != null)
        {
            for (int i = 0; i < rel.Count; i += every)
            {
                curve.AppendLine($"    t {times[i]:0.00}s ({times[i] / clip.length:0.00}) speed {speed[i],5:0.0} front {Vector3.Dot(rel[i], facing),5:0.00} side {rel[i].x,5:0.00} up {rel[i].y,5:0.00}");
            }
        }

        return m;
    }

    private static GameObject Preview()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
        GameObject preview = Object.Instantiate(prefab);
        preview.hideFlags = HideFlags.DontSave;
        preview.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        foreach (MonoBehaviour behaviour in preview.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
        return preview;
    }

    // --- the attack ----------------------------------------------------------------------------------

    private const string Clip = "Combo03_1";
    private const string AttackName = "Dual_Combo03_1_Sword";
    private const string StateName = "Dual_Combo03_1";
    private const string ComboPath = "Assets/Game/Content/Definitions/Combo_Test_Sword.asset";
    private const string WindowId = "Sword thrust";

    // Designer correction from the measured blade tip (Analyse/Curve, Combo03_1 on the Player with the prop): a slow
    // wind-up raises the sword at the right side (0–0.30s, ≤ 11 m/s); the thrust drives the tip from beside the body
    // to 1.81m in front between 0.32s and 0.40s (peak 45 m/s at 0.37s), then holds. Window = drive start → full
    // extension + 0.08s (the proposer's own strike-end rule), on the blade (Weapon2) — not the fist.
    private const float WindowStartSeconds = 0.32f;
    private const float WindowEndSeconds = 0.48f;
    private const float StrikeSeconds = 0.37f;

    /// <summary>Authors everything (prop on prefab + scene player, clip import, attack, animator state, combo, pad).</summary>
    public static string Execute()
    {
        var report = new StringBuilder();
        AttachToPrefab(report);
        AnimationClip clip = ConfigureClip(report);
        AttackDefinition attack = AuthorAttack(clip, report);
        AuthorAnimatorState(attack, report);
        ComboDefinition combo = AuthorCombo(attack, report);
        AssetDatabase.SaveAssets();
        AttachToScenePlayer(report);
        BuildPad(combo, report);
        File.WriteAllText("Temp/AuthorSwordAttack.txt", report.ToString());
        return report.ToString();
    }

    // Same import settings as the shipped and RG5E clips (ConfigureAttackClipImport): root rotation and height baked
    // into the pose, XZ left as root motion — PlayerLocomotion applies planar root motion only.
    private static AnimationClip ConfigureClip(StringBuilder report)
    {
        string path = $"{DualFolder}{Clip}.FBX";
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        bool changed = importer.clipAnimations.Length == 0;
        foreach (ModelImporterClipAnimation c in clips)
        {
            changed |= !c.lockRootRotation || !c.lockRootHeightY || c.lockRootPositionXZ ||
                        !c.keepOriginalOrientation || c.keepOriginalPositionY || !c.heightFromFeet;
            c.lockRootRotation = true;
            c.keepOriginalOrientation = true;
            c.lockRootHeightY = true;
            c.keepOriginalPositionY = false;
            c.heightFromFeet = true;
            c.lockRootPositionXZ = false;
        }

        if (changed)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        report.AppendLine($"Clip {Clip}: {(changed ? "baked rotation + Y into pose, XZ root motion" : "already configured")}.");
        return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
    }

    private static AttackDefinition AuthorAttack(AnimationClip clip, StringBuilder report)
    {
        // The tool's own path: create → strike frame → propose on the Player rig → apply (lands unverified).
        AttackDefinition attack = AttackAuthoringWindow.CreateAttackAsset(clip, AttackName);
        var so = new SerializedObject(attack);
        so.FindProperty("_strikeHint").floatValue = StrikeSeconds / clip.length;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject preview = Preview();
        try
        {
            AttackWindowProposer.Proposal proposal = AttackWindowProposer.Propose(
                preview.GetComponentInChildren<Animator>(), clip, 1f, attack.StrikeHint);
            AttackAuthoringWindow.ApplyProposal(attack, proposal);
            report.AppendLine($"## Proposer on {Clip} ({clip.length:0.00}s)");
            report.AppendLine(proposal.Report.TrimEnd());
        }
        finally
        {
            Object.DestroyImmediate(preview);
        }

        // Designer correction: the proposer does not analyse weapon limbs, so the blade window replaces its hand
        // proposal. Lands unverified; marked verified only after a contact-sheet check and a live hit (RG5E rule).
        so = new SerializedObject(attack);
        SerializedProperty windows = so.FindProperty("_windows");
        windows.arraySize = 1;
        SerializedProperty w = windows.GetArrayElementAtIndex(0);
        w.FindPropertyRelative("_id").stringValue = WindowId;
        w.FindPropertyRelative("_start").floatValue = WindowStartSeconds / clip.length;
        w.FindPropertyRelative("_end").floatValue = WindowEndSeconds / clip.length;
        w.FindPropertyRelative("_limbs").intValue = (int)StrikeLimbMask.Weapon2;
        w.FindPropertyRelative("_area").objectReferenceValue = null;
        w.FindPropertyRelative("_abilityId").stringValue = string.Empty;
        w.FindPropertyRelative("_rehitInterval").floatValue = 0f;
        w.FindPropertyRelative("_verified").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(attack);
        report.AppendLine($"Correction: '{WindowId}' {WindowStartSeconds:0.00}–{WindowEndSeconds:0.00}s on Weapon2 (blade), unverified.");
        report.AppendLine(AttackValidation.TimingSummary(attack));
        return attack;
    }

    private static void AuthorAnimatorState(AttackDefinition attack, StringBuilder report)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState loco = AttackValidation.FindState(controller, "Locomotion");
        if (!controller.parameters.Any(p => p.name == StateName)) controller.AddParameter(StateName, AnimatorControllerParameterType.Trigger);

        AnimatorState state = AttackValidation.FindState(controller, StateName) ?? sm.AddState(StateName, new Vector3(1150f, -200f, 0f));
        state.motion = attack.Clip;
        state.speed = 1f;
        if (!sm.anyStateTransitions.Any(t => t.destinationState == state))
        {
            AnimatorStateTransition entry = sm.AddAnyStateTransition(state);
            entry.hasExitTime = false;
            entry.duration = 0.06f;
            // Re-entering the same state must restart the clip (mashing a step that restarts itself), or the step freezes.
            entry.canTransitionToSelf = true;
            entry.AddCondition(AnimatorConditionMode.If, 0f, StateName);
            entry.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
        }

        foreach (AnimatorStateTransition t in state.transitions.ToArray())
        {
            if (t.destinationState == loco) state.RemoveTransition(t);
        }

        AnimatorStateTransition exit = state.AddTransition(loco);
        exit.hasExitTime = false;
        exit.duration = 0.12f;
        exit.interruptionSource = TransitionInterruptionSource.None;
        exit.AddCondition(AnimatorConditionMode.IfNot, 0f, "AttackCommitted");
        EditorUtility.SetDirty(controller);
        report.AppendLine($"Animator: state + trigger '{StateName}' (AnyState in, exit on !AttackCommitted).");
    }

    private static ComboDefinition AuthorCombo(AttackDefinition attack, StringBuilder report)
    {
        var combo = AssetDatabase.LoadAssetAtPath<ComboDefinition>(ComboPath);
        if (combo == null)
        {
            combo = ScriptableObject.CreateInstance<ComboDefinition>();
            AssetDatabase.CreateAsset(combo, ComboPath);
        }

        float seconds = attack.Clip.length;
        float windUp = attack.LastContactEnd * seconds + 0.05f;   // fallback timers never ahead of the asset
        float recovery = Mathf.Max(0.1f, seconds - windUp + 0.05f);
        var so = new SerializedObject(combo);
        SerializedProperty list = so.FindProperty("_steps");
        list.arraySize = 1;
        SerializedProperty p = list.GetArrayElementAtIndex(0);
        p.FindPropertyRelative("_attackAbilityId").stringValue = "ability.player_strike";
        p.FindPropertyRelative("_attack").objectReferenceValue = attack;
        p.FindPropertyRelative("_animatorState").stringValue = StateName;
        p.FindPropertyRelative("_animatorTrigger").stringValue = StateName;
        p.FindPropertyRelative("_windUpDuration").floatValue = windUp;
        p.FindPropertyRelative("_recoveryDuration").floatValue = recovery;
        p.FindPropertyRelative("_continuationWindow").floatValue = 0f;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(combo);
        report.AppendLine($"Combo_Test_Sword: [1] {StateName} (ability.player_strike).");
        return combo;
    }

    // A sixth loadout pad beside the RG5E row, built exactly like AuthorAttackLibraryContent.BuildPads (1m trigger column).
    private static void BuildPad(ComboDefinition combo, StringBuilder report)
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        Transform group = GameObject.Find("ComboPads")?.transform;
        const string padName = "ComboPad_Sword";
        Transform existing = group != null ? group.Find(padName) : null;
        GameObject pad = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = padName;
        if (group != null) pad.transform.SetParent(group, false);
        pad.transform.position = new Vector3(9f, 0.05f, -4f);
        pad.transform.localScale = new Vector3(1.6f, 0.1f, 1.6f);
        var box = pad.GetComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(1f, 1f / 0.1f, 1f);
        box.center = new Vector3(0f, (0.5f - 0.05f) / 0.1f, 0f);

        Color color = new Color(0.55f, 0.65f, 0.8f);
        const string matPath = "Assets/Game/Content/Materials/M_ComboPad_Sword.mat";
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
        text.text = "Sword";
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.fontSize = 48;
        text.characterSize = 0.5f;
        text.color = color;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        report.AppendLine($"Pad '{padName}' at {pad.transform.position} → {combo.name}.");
    }

    /// <summary>Marks the sword window verified (run only after the contact sheet and a live hit both pass).</summary>
    public static string MarkVerified()
    {
        var attack = AssetDatabase.LoadAssetAtPath<AttackDefinition>($"Assets/Game/Content/Attacks/Attack_{AttackName}.asset");
        var so = new SerializedObject(attack);
        so.FindProperty("_windows").GetArrayElementAtIndex(0).FindPropertyRelative("_verified").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(attack);
        AssetDatabase.SaveAssets();
        return $"{attack.name}: '{WindowId}' verified";
    }

    /// <summary>Per-sample blade-tip curve of the clip named in Temp/sword_curve.txt (diagnostic).</summary>
    public static string Curve()
    {
        string name = File.Exists("Temp/sword_curve.txt") ? File.ReadAllText("Temp/sword_curve.txt").Trim() : "Combo01_1";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{DualFolder}{name}.FBX");
        GameObject preview = Preview();
        var curve = new StringBuilder();
        try { Measure(preview, clip, curve, 1); }
        finally { Object.DestroyImmediate(preview); }
        File.WriteAllText("Temp/SwordCurve.txt", curve.ToString());
        return "Temp/SwordCurve.txt";
    }

    /// <summary>Attaches the prop (prefab only) and reports the blade-tip motion of every candidate clip.</summary>
    public static string Analyse()
    {
        var report = new StringBuilder();
        AttachToPrefab(report);
        GameObject preview = Preview();
        try
        {
            foreach (string name in Candidates)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{DualFolder}{name}.FBX");
                if (clip == null) { report.AppendLine($"{name}: MISSING"); continue; }
                Motion m = Measure(preview, clip, null);
                report.AppendLine($"{name,-20} {m.Length:0.00}s  tip peak {m.PeakSpeed,5:0.0} m/s @ {m.PeakTime:0.00}s  fast {m.Start:0.00}–{m.End:0.00}s " +
                                  $"travel {m.Travel:0.00}m sweep {m.YawSpan,5:0}°  at peak: front {m.FrontAtPeak:0.00}m height {m.HeightAtPeak:0.00}m  | left hand peak {m.LeftPeak:0.0} m/s");
            }
        }
        finally
        {
            Object.DestroyImmediate(preview);
        }

        File.WriteAllText("Temp/SwordClipAnalysis.txt", report.ToString());
        return report.ToString();
    }
}
