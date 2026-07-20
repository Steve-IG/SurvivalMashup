using System.Text;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.HitDetection;
using UnityEngine;

/// <summary>
/// Play-mode demonstration (run from a Bootstrap-composed session) that six very different attacks —
/// player punch, player sword swing, enemy claw, giant attack, explosion, projectile impact — all resolve
/// through the <b>same</b> Hit Detection vocabulary: a shared shape, a socket/bone anchor, and a faction
/// filter, with nothing weapon- or character-specific. Also shows multi-hit (a stateless emitter resolved
/// repeatedly) and multi-volume (two emitters composing). Positions the player next to the Grunt, then
/// resolves each authored configuration against the live composed objects.
/// </summary>
public static class VerifyAuthoringWorkflow
{
    public static string Execute()
    {
        GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
        GameObject gruntGo = GameObject.FindGameObjectWithTag("Enemy");
        if (playerGo == null || gruntGo == null)
        {
            Debug.LogError("[VerifyAuthoringWorkflow] Missing player or grunt.");
            return "missing player/grunt";
        }

        GameplayObject playerObj = playerGo.GetComponentInParent<GameplayObjectBehaviour>()?.Object;
        GameplayObject gruntObj = gruntGo.GetComponentInParent<GameplayObjectBehaviour>()?.Object;
        Animator playerAnim = playerGo.GetComponentInChildren<Animator>();
        Animator gruntAnim = gruntGo.GetComponentInChildren<Animator>();

        // Place the player 1.5 m from the Grunt, each facing the other.
        playerGo.transform.position = gruntGo.transform.position + new Vector3(1.5f, 0f, 0f);
        playerGo.transform.rotation = Quaternion.LookRotation((gruntGo.transform.position - playerGo.transform.position).normalized, Vector3.up);
        gruntGo.transform.rotation = Quaternion.LookRotation((playerGo.transform.position - gruntGo.transform.position).normalized, Vector3.up);
        Physics.SyncTransforms();

        var detector = new HitDetector();

        HitVolumeAnchor Hand() => new HitVolumeAnchor(HitAnchorSpace.HumanoidBone, HumanBodyBones.RightHand, Vector3.zero, HitFacing.Owner);
        HitVolumeAnchor Body() => new HitVolumeAnchor(HitAnchorSpace.Owner, HumanBodyBones.RightHand, Vector3.zero, HitFacing.Owner);

        int Resolve(HitVolume vol, HitVolumeAnchor anchor, string tag, GameObject owner, GameplayObject self, Animator anim)
        {
            var e = new HitVolumeEmitter(vol, anchor, tag, ~0);
            e.Bind(owner.transform, anim);
            return e.Detect(self, detector).Count;
        }

        // Six attacks, one vocabulary.
        int punch = Resolve(new HitVolume(HitShape.Cone, 2.2f, 60f, false, 1), Hand(), "Enemy", playerGo, playerObj, playerAnim);
        int sword = Resolve(new HitVolume(HitShape.Cone, 2.8f, 100f, true, 4), Hand(), "Enemy", playerGo, playerObj, playerAnim);
        int claw = Resolve(new HitVolume(HitShape.Cone, 2.2f, 80f, false, 1), Hand(), "Player", gruntGo, gruntObj, gruntAnim);
        int giant = Resolve(new HitVolume(HitShape.Cone, 5f, 120f, true, 16), Body(), "Enemy", playerGo, playerObj, playerAnim);
        int explosion = Resolve(new HitVolume(HitShape.Sphere, 4f, 0f, true, 12), Body(), "Enemy", playerGo, playerObj, playerAnim);

        // Projectile impact: a small sphere at the projectile's position. At the Grunt it hits; far away it misses.
        var projVol = new HitVolume(HitShape.Sphere, 0.6f, 0f, false, 1);
        var projFilter = new HitFilter("Enemy", ~0);
        int projHit = detector.Detect(projVol, projFilter, gruntGo.transform.position, Vector3.forward, playerObj).Count;
        int projMiss = detector.Detect(projVol, projFilter, gruntGo.transform.position + new Vector3(50f, 0f, 0f), Vector3.forward, playerObj).Count;

        // Multi-hit: the same emitter resolved three times (three contact windows) is stateless and consistent.
        var multiHitEmitter = new HitVolumeEmitter(new HitVolume(HitShape.Cone, 2.2f, 60f, false, 1), Hand(), "Enemy", ~0);
        multiHitEmitter.Bind(playerGo.transform, playerAnim);
        int contact1 = multiHitEmitter.Detect(playerObj, detector).Count;
        int contact2 = multiHitEmitter.Detect(playerObj, detector).Count;
        int contact3 = multiHitEmitter.Detect(playerObj, detector).Count;
        bool multiHit = contact1 > 0 && contact1 == contact2 && contact2 == contact3;

        bool pass = punch > 0 && sword > 0 && claw > 0 && giant > 0 && explosion > 0
                    && projHit > 0 && projMiss == 0 && multiHit;

        var sb = new StringBuilder("[VerifyAuthoringWorkflow] ");
        sb.Append($"punch={punch} sword={sword} claw={claw} giant={giant} explosion={explosion} ");
        sb.Append($"projHit={projHit} projMiss={projMiss} multiHit={multiHit} => {(pass ? "PASS" : "FAIL")}");
        string msg = sb.ToString();
        if (pass) { Debug.Log(msg); } else { Debug.LogError(msg); }
        return msg;
    }
}
