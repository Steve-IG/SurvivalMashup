using System.Collections;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Enemy;
using ToyChest.Gameplay.HitDetection;
using ToyChest.Systems.Resources;
using UnityEngine;

/// <summary>
/// Play-mode check that a dead enemy stops being hittable immediately, rather than soaking blows during
/// the death-animation linger before teardown. Kills a Grunt, then runs the same hit query the player's
/// attack uses against its position and requires zero hits while the corpse is still present.
/// </summary>
public static class VerifyDeadEnemyUnhittable
{
    public static string Execute()
    {
        EnemyCombatant target = null;
        foreach (var candidate in Object.FindObjectsByType<EnemyCombatant>(FindObjectsSortMode.None))
        {
            // Skip the training dummy, which is authored never to die.
            if (candidate.GetComponent<TrainingDummy>() == null)
            {
                target = candidate;
                break;
            }
        }

        if (target == null)
        {
            Debug.LogError("[VerifyDeadEnemyUnhittable] No killable enemy found.");
            return "no enemy";
        }

        var go = new GameObject("~DeadEnemyProbe");
        var probe = go.AddComponent<Probe>();
        probe.Target = target;
        return "probe started; read the console for [VerifyDeadEnemyUnhittable]";
    }

    public sealed class Probe : MonoBehaviour
    {
        public EnemyCombatant Target;

        private IEnumerator Start()
        {
            for (int i = 0; i < 5; i++) yield return null;

            var behaviour = Target.GetComponent<GameplayObjectBehaviour>();
            if (behaviour == null || behaviour.Object == null || !behaviour.Object.TryGet(out ResourceSet resources))
            {
                Debug.LogError("[VerifyDeadEnemyUnhittable] Enemy has no resources.");
                Destroy(gameObject);
                yield break;
            }

            ResourceValue health = resources.GetResource(new DefinitionId("resource.health"));
            Vector3 position = Target.transform.position;

            var detector = new HitDetector();
            var volume = new HitVolume(HitShape.Sphere, 3f, 0f, multiTarget: true, maxTargets: 8);
            var filter = new HitFilter("Enemy", ~0);

            int hitsWhileAlive = detector.Detect(volume, filter, position, Vector3.forward, null).Count;

            // Kill it.
            health.Consume(health.Maximum);
            yield return null;
            Physics.SyncTransforms();

            bool corpseStillPresent = Target != null && Target.gameObject != null;
            int hitsWhileDead = detector.Detect(volume, filter, position, Vector3.forward, null).Count;

            bool pass = hitsWhileAlive > 0 && corpseStillPresent && hitsWhileDead == 0;
            string msg = $"[VerifyDeadEnemyUnhittable] hitsWhileAlive={hitsWhileAlive} corpseStillPresent={corpseStillPresent} " +
                         $"hitsWhileDead={hitsWhileDead} => {(pass ? "PASS" : "FAIL")}";
            if (pass) { Debug.Log(msg); } else { Debug.LogError(msg); }

            Destroy(gameObject);
        }
    }
}
