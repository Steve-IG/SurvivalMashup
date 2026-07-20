#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ToyChest.EditorTools
{
    /// <summary>
    /// Editor-only convenience: makes entering Play mode always start from the Bootstrap scene, no matter
    /// which scene is currently open. ToyChest composes every scene Gameplay Object through
    /// <c>GameBootstrap</c> (Docs/Architecture/ENGINE_STARTUP.md) — it creates the runtime services and
    /// injects them into each scene's spawners. Pressing Play on a gameplay scene directly skips that, so
    /// nothing composes and capabilities like combat (which need the composed object's AbilitySet) silently
    /// do nothing while locomotion still works off its fallback. Setting Unity's <see cref="EditorSceneManager.playModeStartScene"/>
    /// to Bootstrap removes that footgun. This is pure editor tooling — no runtime code, no gameplay, no
    /// architecture change — and is toggleable from the ToyChest menu (default on).
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromBootstrap
    {
        private const string BootstrapScenePath = "Assets/Game/Scenes/Bootstrap.unity";
        private const string MenuPath = "ToyChest/Play From Bootstrap";
        private const string PrefKey = "ToyChest.PlayFromBootstrap";

        static PlayFromBootstrap()
        {
            // Apply after the asset database is ready (InitializeOnLoad can run before assets resolve).
            EditorApplication.delayCall += Apply;
        }

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        private static void Apply()
        {
            SceneAsset bootstrap = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath);
            if (bootstrap == null)
            {
                Debug.LogWarning($"[PlayFromBootstrap] Bootstrap scene not found at '{BootstrapScenePath}'.");
                return;
            }

            EditorSceneManager.playModeStartScene = Enabled ? bootstrap : null;
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
            Debug.Log($"[PlayFromBootstrap] {(Enabled ? "ON — Play now always boots from Bootstrap." : "OFF — Play uses the open scene.")}");
        }

        [MenuItem(MenuPath, isValidateFunction: true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }
    }
}
#endif
