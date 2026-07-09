using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ToyChest.Editor
{
    /// <summary>
    /// Keeps open scenes and dirty assets clean at editor boundaries so Unity's "save modified
    /// scene?" modal never blocks the main thread during the automated (MCP-driven) workflow.
    ///
    /// Saves automatically:
    /// - before every domain reload (script recompile)
    /// - before entering Play and after returning to Edit mode
    /// - before another scene is opened (scene switch)
    /// - on a short debounce while work remains dirty (safety net between MCP calls)
    /// - when a Cursor hook drops a sentinel file (immediate flush after MCP mutations)
    ///
    /// Also exposes <see cref="SaveAllDirty"/> for explicit Coplay <c>SaveAll</c> script calls.
    /// Always on by project decision (2026-07-08): replaces Unity's discard-unsaved-changes safety
    /// net so autonomous runs need zero user intervention. Only saves scenes that already have a
    /// path (never raises Save-As) and never runs during Play.
    /// </summary>
    [InitializeOnLoad]
    public static class SceneAutoSave
    {
        private const double DebounceSeconds = 2.0;

        private static readonly string SentinelPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", ".cursor", "unity-save-requested"));

        private static double _nextDebounceSaveTime;

        static SceneAutoSave()
        {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorSceneManager.sceneOpening += OnSceneOpening;
            EditorApplication.update += OnEditorUpdate;
        }

        /// <summary>
        /// Saves every dirty on-disk open scene and every dirty asset. Returns a short summary for
        /// logs and Coplay script results. Safe to call repeatedly; no-ops when nothing is dirty.
        /// </summary>
        public static string SaveAllDirty()
        {
            if (Application.isPlaying)
            {
                return "Skipped: in Play mode.";
            }

            int scenesSaved = SaveDirtyOpenScenes();
            AssetDatabase.SaveAssets();

            ClearSentinel();
            _nextDebounceSaveTime = EditorApplication.timeSinceStartup + DebounceSeconds;

            if (scenesSaved == 0)
            {
                return "Assets flushed; no dirty on-disk scenes.";
            }

            return $"Saved {scenesSaved} scene(s); assets flushed.";
        }

        private static void OnBeforeAssemblyReload()
        {
            SaveAllDirty();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode
                || change == PlayModeStateChange.EnteredEditMode)
            {
                SaveAllDirty();
            }
        }

        private static void OnSceneOpening(string path, OpenSceneMode mode)
        {
            SaveAllDirty();
        }

        private static void OnEditorUpdate()
        {
            if (Application.isPlaying)
            {
                return;
            }

            if (SentinelExists())
            {
                SaveAllDirty();
                return;
            }

            if (!HasDirtyWork())
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now < _nextDebounceSaveTime)
            {
                return;
            }

            SaveAllDirty();
        }

        private static bool HasDirtyWork()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty && !string.IsNullOrEmpty(scene.path))
                {
                    return true;
                }
            }

            return false;
        }

        private static int SaveDirtyOpenScenes()
        {
            int saved = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty && !string.IsNullOrEmpty(scene.path))
                {
                    EditorSceneManager.SaveScene(scene);
                    saved++;
                }
            }

            return saved;
        }

        private static bool SentinelExists()
        {
            return File.Exists(SentinelPath);
        }

        private static void ClearSentinel()
        {
            if (File.Exists(SentinelPath))
            {
                File.Delete(SentinelPath);
            }
        }
    }
}
