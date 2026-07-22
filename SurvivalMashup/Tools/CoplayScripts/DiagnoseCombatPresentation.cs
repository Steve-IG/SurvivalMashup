using System.Collections.Generic;
using System.Reflection;
using System.Text;
using ToyChest.Debugging;
using ToyChest.Gameplay.Presentation;
using UnityEngine;

/// <summary>
/// Diagnostic for the RG5C reports: (1) is a CombatDebugOverlay actually present in the scene, (2) is the
/// hit flash bound to any renderers — it only accepts materials exposing "_BaseColor", so a character whose
/// shader names its colour differently silently flashes nothing — and (3) what shaders/colour properties
/// the characters actually use. Read-only.
/// </summary>
public static class DiagnoseCombatPresentation
{
    public static string Execute()
    {
        var sb = new StringBuilder("[DiagnoseCombatPresentation]\n");

        var overlay = Object.FindAnyObjectByType<CombatDebugOverlay>(FindObjectsInactive.Include);
        sb.AppendLine($"CombatDebugOverlay in scene: {(overlay != null ? "YES on '" + overlay.name + "'" : "NO  <-- F2 cannot work without it")}");

        foreach (var flashOwner in Object.FindObjectsByType<HitFlash>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Report(sb, flashOwner);
        }

        // Characters that have no HitFlash at all.
        foreach (string tag in new[] { "Player", "Enemy" })
        {
            foreach (GameObject go in GameObject.FindGameObjectsWithTag(tag))
            {
                Transform root = go.transform.root;
                if (root.GetComponentInChildren<HitFlash>(true) == null)
                {
                    sb.AppendLine($"'{root.name}' (tag {tag}): NO HitFlash component");
                }
            }
        }

        string msg = sb.ToString();
        Debug.Log(msg);
        return msg;
    }

    private static void Report(StringBuilder sb, HitFlash flash)
    {
        var field = typeof(HitFlash).GetField("_renderers", BindingFlags.Instance | BindingFlags.NonPublic);
        var bound = field?.GetValue(flash) as List<Renderer>;
        int boundCount = bound?.Count ?? -1;

        var all = flash.GetComponentsInChildren<Renderer>(true);
        sb.AppendLine($"HitFlash on '{flash.transform.root.name}/{flash.name}': renderers found={all.Length}, bound(_BaseColor)={boundCount}"
                      + (boundCount == 0 ? "   <-- flash does nothing" : string.Empty));

        var seen = new HashSet<string>();
        foreach (Renderer r in all)
        {
            Material m = r.sharedMaterial;
            if (m == null || m.shader == null || !seen.Add(m.shader.name))
            {
                continue;
            }

            sb.AppendLine($"    shader '{m.shader.name}'  _BaseColor={m.HasProperty("_BaseColor")}  " +
                          $"_Color={m.HasProperty("_Color")}  _MainColor={m.HasProperty("_MainColor")}  " +
                          $"_TintColor={m.HasProperty("_TintColor")}");
        }
    }
}
