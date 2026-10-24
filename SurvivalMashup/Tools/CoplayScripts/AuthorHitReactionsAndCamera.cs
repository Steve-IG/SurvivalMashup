using System.Linq;
using ToyChest.Gameplay.Objects;
using ToyChest.Gameplay.Player;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Authors the RG C review adjustments. Idempotent; run in edit mode
/// (run_script --entry AuthorHitReactionsAndCamera.Execute). Editor-only tooling: content and scene, no runtime code.
/// <list type="bullet">
/// <item><b>Hit reactions</b>: <c>attribute.stability</c> (base 10) on every enemy body and <c>attribute.impact_power</c>
/// (base 1) on the player; the Grunt Brute's status adds +30 Stability (40). Window impacts: the combo finisher hits
/// at 16 (staggers a Grunt), Ground Slam at 30 (knocks Grunts back, flinches the Brute); everything else keeps the
/// default 10 (flinches a Grunt, shrugged off by the Brute).</item>
/// <item><b>Enemy hit animations</b>: the EnemyAnimator's Hit state plays Frank Fighter Hit01; new HitHeavy (Hit02) and
/// Knockback (Hit_Knockback) states with matching triggers.</item>
/// <item><b>Tuning</b>: Grunt speed 3.5 → 2.6 (the Brute's status now −0.3, so it stays slower at 2.3), the claw 8 → 5
/// damage, Ground Slam cooldown 6 → 3 s.</item>
/// <item><b>Camera</b>: an orbiting over-the-shoulder action camera — a <see cref="ThirdPersonCameraPivot"/> driven by
/// mouse / right stick, followed by the gameplay Cinemachine camera through a Third Person Follow.</item>
/// </list>
/// </summary>
public static class AuthorHitReactionsAndCamera
{
    private const string Label = "definitions";
    private const string Defs = "Assets/Game/Content/Definitions/";
    private const string Attacks = "Assets/Game/Content/Attacks/";
    private const string ControllerPath = "Assets/Game/Content/Animation/EnemyAnimator.controller";
    private const string ClipFolder = "Assets/Frank_Fighter/Assets/Animations/FBX Animation/Root_Motion/Frank_RPG_Fighter_";
    private const string ScenePath = "Assets/Game/Scenes/VerticalSlice.unity";

    public static string Execute()
    {
        var log = new System.Text.StringBuilder();
        AuthorAttributes(log);
        AuthorImpacts(log);
        AuthorTuning(log);
        AuthorAnimator(log);
        AuthorCamera(log);
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    /// <summary>Re-authors only the gameplay camera (leaves tuning, impacts, and animator untouched).</summary>
    public static string ExecuteCameraOnly()
    {
        var log = new System.Text.StringBuilder();
        AuthorCamera(log);
        return log.ToString();
    }

    // ------------------------------------------------------------------ attributes

    private static void AuthorAttributes(System.Text.StringBuilder log)
    {
        var stability = Copy("Attr_EnemySpeed", "Attr_Stability", so =>
        {
            so.FindProperty("_definitionId").stringValue = "attribute.stability";
            so.FindProperty("_displayName").stringValue = "Stability";
            so.FindProperty("_baseValue").floatValue = 10f;
        });

        var power = Copy("Attr_EnemySpeed", "Attr_ImpactPower", so =>
        {
            so.FindProperty("_definitionId").stringValue = "attribute.impact_power";
            so.FindProperty("_displayName").stringValue = "Impact Power";
            so.FindProperty("_baseValue").floatValue = 1f;
        });

        foreach (string body in new[] { "Obj_Grunt", "Obj_GruntBrute", "Obj_TrainingDummy" })
        {
            AddToList(Defs + body + ".asset", "_attributes", stability);
        }

        AddToList(Defs + "Obj_Player.asset", "_attributes", power);

        Edit(Defs + "Status_EliteBrute.asset", so =>
        {
            SerializedProperty modifiers = so.FindProperty("_modifiers");
            SetModifier(modifiers, "attribute.enemy_speed", -0.3f);
            SetModifier(modifiers, "attribute.stability", 30f);
        });

        log.Append("attributes: stability (Grunt/Brute/Dummy, Brute +30), impact_power (Player) | ");
    }

    // ------------------------------------------------------------------ impacts

    private static void AuthorImpacts(System.Text.StringBuilder log)
    {
        // The finisher's every window hits hard enough to stagger a Grunt; the slam knocks Grunts back.
        SetWindowImpacts(Attacks + "Attack_Player_Combo3.asset", 16f);
        SetWindowImpacts(Attacks + "Attack_GroundSlam.asset", 30f);
        log.Append("impacts: Combo3 windows 16, Ground Slam 30 (others default 10) | ");
    }

    private static void SetWindowImpacts(string path, float impact)
    {
        Edit(path, so =>
        {
            SerializedProperty windows = so.FindProperty("_windows");
            for (int i = 0; i < windows.arraySize; i++)
            {
                windows.GetArrayElementAtIndex(i).FindPropertyRelative("_impact").floatValue = impact;
            }
        });
    }

    // ------------------------------------------------------------------ tuning

    private static void AuthorTuning(System.Text.StringBuilder log)
    {
        Edit(Defs + "Attr_EnemySpeed.asset", so => so.FindProperty("_baseValue").floatValue = 2.6f);
        Edit(Defs + "Fx_Damage_EnemyStrike.asset", so => so.FindProperty("_amount").floatValue = 5f);
        Edit(Defs + "Ability_GroundSlam.asset", so =>
        {
            // Only replaces the original 6 s; a designer's own tuning is left alone on re-runs.
            SerializedProperty cooldown = so.FindProperty("_cooldownSeconds");
            if (Mathf.Approximately(cooldown.floatValue, 6f)) cooldown.floatValue = 3f;
        });
        log.Append("tuning: Grunt speed 2.6, claw 5, Ground Slam cooldown 3s | ");
    }

    // ------------------------------------------------------------------ animator

    private static void AuthorAnimator(System.Text.StringBuilder log)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState locomotion = machine.states.Select(s => s.state).First(s => s.name == "Locomotion");

        // Every wind-up restarts the attack clip: with self-transition off, a swing begun while the previous swing's clip
        // was still playing kept that clip (and received its stale contact event, landing the blow with no telegraph).
        AnimatorState attack = machine.states.Select(s => s.state).First(s => s.name == "Attack");
        foreach (AnimatorStateTransition t in machine.anyStateTransitions.Where(t => t.destinationState == attack))
        {
            t.canTransitionToSelf = true;
        }

        AnimatorState hit = machine.states.Select(s => s.state).First(s => s.name == "Hit");
        AnimationClip light = Clip("Hit01");
        hit.motion = light;
        hit.speed = light.length / 0.45f; // a quick flinch: ~0.45 s

        AnimatorState heavy = EnsureHitState(controller, machine, locomotion, "HitHeavy", new Vector3(380f, 320f, 0f));
        AnimationClip heavyClip = Clip("Hit02");
        heavy.motion = heavyClip;
        heavy.speed = heavyClip.length / 0.6f;

        AnimatorState knockback = EnsureHitState(controller, machine, locomotion, "Knockback", new Vector3(420f, 380f, 0f));
        AnimationClip knockbackClip = Clip("Hit_Knockback");
        knockback.motion = knockbackClip;
        knockback.speed = knockbackClip.length / 0.85f;

        EditorUtility.SetDirty(controller);
        log.Append($"animator: Hit=Hit01 ({light.length:0.00}s), HitHeavy=Hit02 ({heavyClip.length:0.00}s), Knockback=Hit_Knockback ({knockbackClip.length:0.00}s) | ");
    }

    private static AnimatorState EnsureHitState(AnimatorController controller, AnimatorStateMachine machine, AnimatorState locomotion, string name, Vector3 position)
    {
        if (controller.parameters.All(p => p.name != name))
        {
            controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
        }

        AnimatorState state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name);
        if (state != null)
        {
            return state;
        }

        state = machine.AddState(name, position);
        AnimatorStateTransition enter = machine.AddAnyStateTransition(state);
        enter.AddCondition(AnimatorConditionMode.If, 0f, name);
        enter.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
        enter.hasExitTime = false;
        enter.duration = 0.05f;
        enter.canTransitionToSelf = true;

        AnimatorStateTransition exit = state.AddTransition(locomotion);
        exit.hasExitTime = true;
        exit.exitTime = 0.9f;
        exit.duration = 0.12f;
        return state;
    }

    private static AnimationClip Clip(string name)
    {
        return AssetDatabase.LoadAllAssetsAtPath(ClipFolder + name + ".FBX").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
    }

    // ------------------------------------------------------------------ camera

    private static void AuthorCamera(System.Text.StringBuilder log)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        CinemachineCamera vcam = Object.FindObjectsByType<CinemachineCamera>().First();

        GameObject pivotObject = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "CameraPivot");
        if (pivotObject == null)
        {
            pivotObject = new GameObject("CameraPivot");
            SceneManager.MoveGameObjectToScene(pivotObject, scene);
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        pivotObject.transform.SetPositionAndRotation(player.transform.position + Vector3.up * 0.45f, Quaternion.Euler(5f, 0f, 0f));
        ThirdPersonCameraPivot pivot = pivotObject.GetComponent<ThirdPersonCameraPivot>();
        if (pivot == null)
        {
            pivot = pivotObject.AddComponent<ThirdPersonCameraPivot>();
        }

        // God of War framing (measured from the reference): pivot at shoulder height, nearly level, pitch range that keeps
        // the close camera out of the floor.
        var pivotSo = new SerializedObject(pivot);
        pivotSo.FindProperty("_height").floatValue = 0.45f;
        pivotSo.FindProperty("_initialPitch").floatValue = 5f;
        pivotSo.FindProperty("_minPitch").floatValue = -20f;
        pivotSo.FindProperty("_maxPitch").floatValue = 55f;
        pivotSo.ApplyModifiedPropertiesWithoutUndo();

        foreach (var old in vcam.GetComponents<CinemachineFollow>()) Object.DestroyImmediate(old);
        foreach (var old in vcam.GetComponents<CinemachineDeoccluder>()) Object.DestroyImmediate(old);
        foreach (var old in vcam.GetComponents<CinemachineRotationComposer>()) Object.DestroyImmediate(old);

        CinemachineThirdPersonFollow follow = vcam.GetComponent<CinemachineThirdPersonFollow>();
        if (follow == null)
        {
            follow = vcam.gameObject.AddComponent<CinemachineThirdPersonFollow>();
        }

        // Close over the right shoulder (God of War): 3 m back at roughly eye height, the character on the left third
        // filling ~60% of the frame, the fight ahead. The shoulder offset alone places the character off-centre.
        follow.ShoulderOffset = new Vector3(0.75f, 0f, 0f);
        follow.VerticalArmLength = 0f;
        follow.CameraSide = 1f;
        follow.CameraDistance = 3.0f;
        follow.Damping = new Vector3(0.1f, 0.3f, 0.2f);

        // The dojo is flat and open, and every body shares the Default layer, so obstacle avoidance would pull the camera
        // in whenever an enemy crossed behind the player. Off until levels gain walls on their own layer.
        follow.AvoidObstacles.Enabled = false;

        vcam.Follow = pivotObject.transform;
        vcam.LookAt = null;
        vcam.Lens.FieldOfView = 52f; // vertical FOV; ~80 degrees horizontal at 16:9

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.Append($"camera: '{vcam.name}' Third Person Follow on CameraPivot (shoulder 0.75, distance 3.0, FOV 52, pivot 0.45 m, pitch 5)");
    }

    // ------------------------------------------------------------------ helpers

    private static ScriptableObject Copy(string source, string target, System.Action<SerializedObject> edit)
    {
        string targetPath = Defs + target + ".asset";
        if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(targetPath) == null)
        {
            AssetDatabase.CopyAsset(Defs + source + ".asset", targetPath);
            AssetDatabase.ImportAsset(targetPath);
        }

        ScriptableObject asset = Edit(targetPath, edit);
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        AddressableAssetEntry entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(targetPath), settings.DefaultGroup);
        entry.SetLabel(Label, true, true);
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, null, true, true);
        return asset;
    }

    private static ScriptableObject Edit(string path, System.Action<SerializedObject> edit)
    {
        var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
        var so = new SerializedObject(asset);
        edit(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static void AddToList(string path, string property, Object item)
    {
        Edit(path, so =>
        {
            SerializedProperty list = so.FindProperty(property);
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == item) return;
            }

            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = item;
        });
    }

    private static void SetModifier(SerializedProperty modifiers, string attributeId, float value)
    {
        for (int i = 0; i < modifiers.arraySize; i++)
        {
            SerializedProperty m = modifiers.GetArrayElementAtIndex(i);
            if (m.FindPropertyRelative("_attributeId").stringValue == attributeId)
            {
                m.FindPropertyRelative("_value").floatValue = value;
                return;
            }
        }

        modifiers.InsertArrayElementAtIndex(modifiers.arraySize);
        SerializedProperty added = modifiers.GetArrayElementAtIndex(modifiers.arraySize - 1);
        added.FindPropertyRelative("_attributeId").stringValue = attributeId;
        added.FindPropertyRelative("_operation").enumValueIndex = 0;
        added.FindPropertyRelative("_value").floatValue = value;
    }
}
