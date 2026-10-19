using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Visual check for attack authoring: renders a clip on the Player rig (posed exactly as the Attack Authoring
/// window poses it — AnimationMode sampling) at evenly spaced times into a contact sheet: top-down row (initial
/// facing = image up, white arrow) and side row (facing = image right). Limb tips carry markers: red = right
/// hand, blue = left hand, orange = right foot, cyan = left foot. A frame whose time falls inside one of the
/// attack asset's windows gets a yellow ground marker under the character. Output: Temp/Sheets/&lt;clip&gt;.png.
/// Editor-only diagnostic; writes nothing to the project.
/// </summary>
public static class RenderClipContactSheet
{
    public static string Clip = "Combo02_2";
    public static string Attack = "";
    public static float From = 0f, To = 1f;
    public const int Columns = 10, Cell = 260;

    public static string Execute()
    {
        // Spec file (run_script takes no arguments): "Clip|AttackAsset|from|to" per line; renders each line.
        if (File.Exists("Temp/sheet_spec.txt"))
        {
            var results = new List<string>();
            foreach (string line in File.ReadAllLines("Temp/sheet_spec.txt"))
            {
                string[] f = line.Split('|');
                if (f.Length < 4) continue;
                Clip = f[0];
                Attack = f[1];
                From = float.Parse(f[2], System.Globalization.CultureInfo.InvariantCulture);
                To = float.Parse(f[3], System.Globalization.CultureInfo.InvariantCulture);
                results.Add(RenderOne());
            }

            return string.Join("\n", results);
        }

        return RenderOne();
    }

    private static string RenderOne()
    {
        // A bare name is a Frank Fighter clip; a full asset path renders any humanoid clip (e.g. Frank RPG Dual).
        string clipPath = Clip.Contains("/") ? Clip : $"Assets/Frank_Fighter/Assets/Animations/FBX Animation/Root_Motion/Frank_RPG_Fighter_{Clip}.FBX";
        string sheetName = Path.GetFileNameWithoutExtension(clipPath).Replace("Frank_RPG_Fighter_", string.Empty);
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        var attack = string.IsNullOrEmpty(Attack) ? null
            : AssetDatabase.LoadAssetAtPath<ToyChest.Gameplay.Combat.AttackDefinition>($"Assets/Game/Content/Attacks/Attack_{Attack}.asset");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Content/Prefabs/Player.prefab");
        var origin = new Vector3(500f, 0f, 500f);
        var temp = new List<Object>();
        GameObject preview = Object.Instantiate(prefab, origin, Quaternion.identity);
        temp.Add(preview);
        foreach (MonoBehaviour b in preview.GetComponentsInChildren<MonoBehaviour>()) b.enabled = false;
        Animator animator = preview.GetComponentInChildren<Animator>();

        Shader lit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        Material Mat(Color c)
        {
            var m = new Material(lit);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            temp.Add(m);
            return m;
        }

        GameObject Prim(PrimitiveType type, Vector3 pos, Vector3 scale, Color c, Transform parent = null)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = Mat(c);
            if (parent == null) temp.Add(g);
            return g;
        }

        Prim(PrimitiveType.Plane, origin, new Vector3(0.8f, 1f, 0.8f), new Color(0.25f, 0.27f, 0.3f));
        Prim(PrimitiveType.Cube, origin + new Vector3(0f, 0.01f, 1.25f), new Vector3(0.05f, 0.02f, 0.6f), Color.white);
        GameObject activeMark = Prim(PrimitiveType.Cylinder, origin + Vector3.up * 0.02f, new Vector3(1.4f, 0.01f, 1.4f), new Color(1f, 0.85f, 0.1f));
        var tips = new (HumanBodyBones bone, Color c)[]
        {
            (HumanBodyBones.RightHand, Color.red), (HumanBodyBones.LeftHand, new Color(0.2f, 0.4f, 1f)),
            (HumanBodyBones.RightToes, new Color(1f, 0.55f, 0f)), (HumanBodyBones.LeftToes, Color.cyan),
        };
        foreach (var (bone, c) in tips)
        {
            Transform t = animator.GetBoneTransform(bone) ?? animator.GetBoneTransform(bone == HumanBodyBones.RightToes ? HumanBodyBones.RightFoot : HumanBodyBones.LeftFoot);
            Prim(PrimitiveType.Sphere, t.position, Vector3.one * 0.13f, c, t).transform.localPosition = Vector3.zero;
        }

        var camGo = new GameObject("~SheetCam");
        temp.Add(camGo);
        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
        cam.orthographic = true;
        cam.orthographicSize = 1.7f;
        var rt = new RenderTexture(Cell, Cell, 24);
        temp.Add(rt);
        cam.targetTexture = rt;
        var sheet = new Texture2D(Cell * Columns, Cell * 2, TextureFormat.RGB24, false);
        var cellTex = new Texture2D(Cell, Cell, TextureFormat.RGB24, false);
        temp.Add(sheet);
        temp.Add(cellTex);

        AnimationMode.StartAnimationMode();
        try
        {
            for (int i = 0; i < Columns; i++)
            {
                float n = Mathf.Lerp(From, To, Columns == 1 ? 0f : i / (float)(Columns - 1));
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(animator.gameObject, clip, n * clip.length);
                AnimationMode.EndSampling();
                bool inWindow = false;
                if (attack != null) foreach (var w in attack.Windows) inWindow |= n >= w.Start && n <= w.End;
                activeMark.SetActive(inWindow);
                Vector3 hips = animator.GetBoneTransform(HumanBodyBones.Hips).position;
                Vector3 focus = new Vector3(hips.x, 1f, hips.z + 0.3f);

                // Top-down: forward (+Z) is image-up.
                cam.transform.SetPositionAndRotation(focus + Vector3.up * 10f, Quaternion.LookRotation(Vector3.down, Vector3.forward));
                Grab(cam, rt, cellTex, sheet, i, 1);
                // Side: from the character's right (+X) looking left, so forward (+Z) is image-right.
                cam.transform.SetPositionAndRotation(new Vector3(focus.x + 10f, 1.2f, focus.z), Quaternion.LookRotation(Vector3.left, Vector3.up));
                Grab(cam, rt, cellTex, sheet, i, 0);
            }
        }
        finally
        {
            AnimationMode.StopAnimationMode();
            Directory.CreateDirectory("Temp/Sheets");
            File.WriteAllBytes($"Temp/Sheets/{sheetName}{(attack != null ? "_win" : "")}.png", sheet.EncodeToPNG());
            cam.targetTexture = null;
            foreach (Object o in temp) if (o != null) Object.DestroyImmediate(o);
        }

        return $"Temp/Sheets/{sheetName}.png: {Columns} frames {From * clip.length:0.00}-{To * clip.length:0.00}s of {clip.length:0.00}s";
    }

    private static void Grab(Camera cam, RenderTexture rt, Texture2D cellTex, Texture2D sheet, int column, int row)
    {
        cam.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        cellTex.ReadPixels(new Rect(0, 0, Cell, Cell), 0, 0);
        cellTex.Apply();
        RenderTexture.active = previous;
        sheet.SetPixels(column * Cell, row * Cell, Cell, Cell, cellTex.GetPixels());
        // A thin divider so frames read as separate cells.
        for (int y = 0; y < Cell; y++) sheet.SetPixel(column * Cell, row * Cell + y, Color.black);
    }
}
