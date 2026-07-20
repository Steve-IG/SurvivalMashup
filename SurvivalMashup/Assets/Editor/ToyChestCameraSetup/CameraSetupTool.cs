using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ToyChest.CameraSetupTool
{
    /// <summary>
    /// One-shot authoring tool: sets up the gameplay scene's Cinemachine 3 third-person camera. Thin
    /// presentation only — gameplay is unaffected because movement reads the active camera's facing.
    /// RG3 update: the camera is a <b>stable, fixed-angle</b> follow — it translates with the player
    /// but never rotates, so input feels directly connected to movement. The previous rotation
    /// composer (with look-ahead) re-aimed as the player moved, which read as distracting rotational
    /// drift; it is removed and the vcam holds a constant downward pitch instead.
    /// </summary>
    public static class CameraSetupTool
    {
        // Fixed follow offset (behind + above) and the matching downward pitch that frames the player.
        static readonly Vector3 Offset = new Vector3(0f, 7f, -9f);

        public static void Execute()
        {
            GameObject player = GameObject.Find("Player");
            GameObject mainCam = GameObject.Find("Main Camera");
            if (player == null || mainCam == null)
            {
                Debug.LogError("[CM] Player or Main Camera not found in the open scene.");
                return;
            }

            foreach (var mb in mainCam.GetComponents<MonoBehaviour>())
            {
                if (mb != null && mb.GetType().Name == "ThirdPersonCameraRig")
                {
                    Object.DestroyImmediate(mb);
                }
            }

            if (mainCam.GetComponent<CinemachineBrain>() == null)
            {
                mainCam.AddComponent<CinemachineBrain>();
            }

            GameObject vcamGo = GameObject.Find("CM ThirdPerson");
            if (vcamGo == null)
            {
                vcamGo = new GameObject("CM ThirdPerson");
            }

            var cam = vcamGo.GetComponent<CinemachineCamera>();
            if (cam == null) cam = vcamGo.AddComponent<CinemachineCamera>();
            cam.Follow = player.transform;
            cam.LookAt = null; // no Aim component: rotation stays fixed (see below)
            cam.Lens.FieldOfView = 50f;
            cam.Priority = 10;

            // Body: world-space (fixed-angle) position follow with light, symmetric damping so the
            // camera keeps up with the player without lag or overshoot.
            var follow = vcamGo.GetComponent<CinemachineFollow>();
            if (follow == null) follow = vcamGo.AddComponent<CinemachineFollow>();
            follow.FollowOffset = Offset;
            follow.TrackerSettings.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace;
            follow.TrackerSettings.PositionDamping = new Vector3(0.12f, 0.12f, 0.12f);

            // No Aim component → the camera keeps a constant rotation. Pitch it down to look at the
            // player from the offset; this never changes as the player moves, so there is no drift.
            float pitch = Mathf.Atan2(Offset.y, -Offset.z) * Mathf.Rad2Deg;
            vcamGo.transform.rotation = Quaternion.Euler(pitch, 0f, 0f);

            // Remove the previous re-aiming composer if it exists (the source of the rotational drift).
            var oldComposer = vcamGo.GetComponent<CinemachineRotationComposer>();
            if (oldComposer != null) Object.DestroyImmediate(oldComposer);

            if (vcamGo.GetComponent<CinemachineDeoccluder>() == null)
            {
                var deo = vcamGo.AddComponent<CinemachineDeoccluder>();
                deo.AvoidObstacles.Enabled = true;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log($"[CM] DONE — stable fixed-angle vcam (pitch {pitch:F1}) on {player.name}; brain on {mainCam.name}.");
        }
    }
}
