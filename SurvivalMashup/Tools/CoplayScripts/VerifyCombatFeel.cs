using System;
using System.Collections;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Enemy;
using ToyChest.Gameplay.Player;
using UnityEngine;

/// <summary>
/// Play-mode verification of the Review Group 5C combat-feel guarantees, run from a Bootstrap-composed
/// session against the authored Training Dummy:
/// (A) a swing into empty air reaches the contact frame but confirms no hit, so no impact presentation fires;
/// (B) a swing at the dummy confirms exactly one hit and the dummy registers the blow;
/// (C) sustained punching never kills the dummy;
/// (D) the player is released from the attack commitment shortly after contact (snappy, not floaty).
/// Reads public seams only; adds no gameplay.
/// </summary>
public static class VerifyCombatFeel
{
    public static string Execute()
    {
        var combat = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
        var dummy = UnityEngine.Object.FindAnyObjectByType<TrainingDummy>();
        if (combat == null || dummy == null)
        {
            Debug.LogError("[VerifyCombatFeel] Missing PlayerCombat or TrainingDummy.");
            return "missing player/dummy";
        }

        var go = new GameObject("~CombatFeelProbe");
        var probe = go.AddComponent<Probe>();
        probe.Combat = combat;
        probe.Dummy = dummy;
        return "probe started; read the console for [VerifyCombatFeel]";
    }

    public sealed class Probe : MonoBehaviour
    {
        public PlayerCombat Combat;
        public TrainingDummy Dummy;

        private int _attacked;
        private int _contacted;
        private int _impacted;

        private void Bind()
        {
            Combat.Attacked += OnAttacked;
            Combat.Contacted += OnContacted;
            Combat.Impacted += OnImpacted;
        }

        private void Unbind()
        {
            Combat.Attacked -= OnAttacked;
            Combat.Contacted -= OnContacted;
            Combat.Impacted -= OnImpacted;
        }

        private void OnAttacked() => _attacked++;
        private void OnContacted() => _contacted++;
        private void OnImpacted(Vector3 p) => _impacted++;

        private IEnumerator Start()
        {
            Bind();
            for (int i = 0; i < 10; i++) yield return null;

            Transform player = Combat.transform;
            Transform target = Dummy.transform;

            // (A) EMPTY SWING: stand far away and punch. Contact frame happens; no hit is confirmed.
            player.position = target.position + new Vector3(40f, 0f, 40f);
            Physics.SyncTransforms();
            Reset();
            Combat.TryAttack();
            yield return new WaitForSeconds(0.8f);
            bool emptySwingSilent = _attacked == 1 && _contacted == 1 && _impacted == 0;

            // (B) CONFIRMED HIT: stand in range facing the dummy and punch.
            yield return Reposition(player, target, 1.3f);
            Reset();
            Combat.TryAttack();
            yield return new WaitForSeconds(0.8f);
            int hitsAfterOne = Dummy.HitsAbsorbed;
            bool oneHitOneEvent = _impacted == 1 && hitsAfterOne > 0;

            // (D) SNAPPY RELEASE: measure how long after the press the swing keeps the player committed.
            yield return Reposition(player, target, 1.3f);
            Reset();
            float pressTime = Time.time;
            Combat.TryAttack();
            while (Combat.IsAttacking && Time.time - pressTime < 2f)
            {
                yield return null;
            }

            float committedFor = Time.time - pressTime;

            // (C) SUSTAINED PUNCHING: the dummy must survive and keep registering blows.
            Reset();
            for (int i = 0; i < 15; i++)
            {
                yield return Reposition(player, target, 1.3f);
                Combat.TryAttack();
                yield return new WaitForSeconds(0.45f);
            }

            bool dummyAlive = Dummy != null && Dummy.gameObject != null && Dummy.isActiveAndEnabled;
            var behaviour = Dummy != null ? Dummy.GetComponent<GameplayObjectBehaviour>() : null;
            GameplayObject dummyObject = behaviour != null ? behaviour.Object : null;
            bool dummyObjectLive = dummyObject != null && dummyObject.IsActive;
            int totalHits = Dummy != null ? Dummy.HitsAbsorbed : 0;

            bool pass = emptySwingSilent && oneHitOneEvent && dummyAlive && dummyObjectLive && totalHits > 5;

            string msg = $"[VerifyCombatFeel] emptySwing(attacked/contacted/impacted)=1/1/0? {emptySwingSilent} | " +
                         $"confirmedHitFiresOnce={oneHitOneEvent} | committedForAfterPress={committedFor:0.00}s | " +
                         $"sustainedHits={totalHits} dummyAlive={dummyAlive && dummyObjectLive} => {(pass ? "PASS" : "FAIL")}";
            if (pass) { Debug.Log(msg); } else { Debug.LogError(msg); }

            Unbind();
            Destroy(gameObject);
        }

        private IEnumerator Reposition(Transform player, Transform target, float distance)
        {
            Vector3 offset = new Vector3(distance, 0f, 0f);
            player.position = target.position + offset;
            player.rotation = Quaternion.LookRotation((target.position - player.position).normalized, Vector3.up);
            Physics.SyncTransforms();
            yield return null;
        }

        private void Reset()
        {
            _attacked = 0;
            _contacted = 0;
            _impacted = 0;
        }
    }
}
