using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ToyChest.Editor;
using ToyChest.Gameplay.Combat;
using ToyChest.Gameplay.HitDetection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// RG5E attack library, phase 1: creates each library attack through the Attack Authoring tool's own path —
/// <c>CreateAttackAsset</c> → strike frame → <c>AttackWindowProposer.Propose</c> on the Player rig →
/// <c>ApplyProposal</c> (all windows land as unverified suggestions) — and writes the proposer's reports to
/// Temp/AttackLibraryProposals.txt for review. Designer corrections are applied by AuthorAttackLibraryContent.
/// Idempotent; re-proposing overwrites windows. Editor-only tooling.
/// </summary>
public static class AuthorAttackLibrary
{
    private const string ClipFolder = "Assets/Frank_Fighter/Assets/Animations/FBX Animation/Root_Motion/Frank_RPG_Fighter_";

    /// <summary>One library attack: asset name, source clip, strike frame (clip seconds, -1 = none).</summary>
    public static readonly (string asset, string clip, float strikeSeconds)[] Attacks =
    {
        // A. Quick Strikes — forward jabs; forward-drive scoring handles these unhinted.
        ("Fighter_Combo04_1", "Combo04_1", -1f),
        ("Fighter_Combo04_2", "Combo04_2", -1f),
        ("Fighter_Combo03_1", "Combo03_1", -1f), // carries its own OnAttackContact (0.19s) → used as the strike frame
        // B. Sweeping — hooks/backfist/spin kick: strike frames on the sweep's speed peak (SurveyLimbSweeps).
        ("Fighter_Combo02_1", "Combo02_1", 0.23f),
        ("Fighter_Combo02_2", "Combo02_2", 0.28f),
        ("Fighter_Combo06_1", "Combo06_1", 0.57f),
        // C. Aerial / Heavy — leaps (root height), analysed by the leap path.
        ("Fighter_AirCombo_Launcher", "AirCombo_Launcher", -1f),
        ("Fighter_Combo02_3", "Combo02_3", -1f),
        // D. Weapon proxy — the backfist again, re-pointed at the Weapon1 socket shape afterwards.
        ("Fighter_Combo02_2_WeaponProxy", "Combo02_2", 0.28f),
    };

    public static string Execute()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Content/Prefabs/Player.prefab");
        GameObject preview = Object.Instantiate(prefab);
        preview.hideFlags = HideFlags.DontSave;
        foreach (MonoBehaviour b in preview.GetComponentsInChildren<MonoBehaviour>()) b.enabled = false;
        Animator animator = preview.GetComponentInChildren<Animator>();
        var report = new StringBuilder();
        try
        {
            foreach ((string asset, string clipName, float strike) in Attacks)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipFolder}{clipName}.FBX");
                if (clip == null)
                {
                    report.AppendLine($"## {asset}: MISSING CLIP {clipName}");
                    continue;
                }

                AttackDefinition attack = AttackAuthoringWindow.CreateAttackAsset(clip, asset);
                var so = new SerializedObject(attack);
                so.FindProperty("_strikeHint").floatValue = strike >= 0f ? strike / clip.length : -1f;
                so.ApplyModifiedPropertiesWithoutUndo();

                AttackWindowProposer.Proposal proposal = AttackWindowProposer.Propose(animator, clip, 1f, attack.StrikeHint);
                AttackAuthoringWindow.ApplyProposal(attack, proposal);
                report.AppendLine($"## {asset} ({clip.length:0.00}s)");
                report.AppendLine(proposal.Report.TrimEnd());
                report.AppendLine(AttackValidation.TimingSummary(attack));
            }
        }
        finally
        {
            Object.DestroyImmediate(preview);
        }

        AssetDatabase.SaveAssets();
        File.WriteAllText("Temp/AttackLibraryProposals.txt", report.ToString());
        return "wrote Temp/AttackLibraryProposals.txt";
    }
}
