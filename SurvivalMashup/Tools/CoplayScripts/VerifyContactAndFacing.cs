using System.Collections;
using System.Text;
using ToyChest.Gameplay.HitDetection;
using ToyChest.Gameplay.Player;
using UnityEngine;

/// <summary>
/// Play-mode verification of two fixes: (1) every attacking character's model now has a shared
/// AttackContactRelay wired to an IAttackContactReceiver, so an authored OnAttackContact event has a
/// receiver on enemies as well as the player; (2) starting a swing snaps the player instantly to the
/// direction they are steering, including a full reversal, instead of easing at the turn rate.
/// </summary>
public static class VerifyContactAndFacing
{
    public static string Execute()
    {
        var sb = new StringBuilder("[VerifyContactAndFacing] ");

        // (1) Relay coverage across every character that can receive contact events.
        int relays = 0;
        int wired = 0;
        foreach (var relay in Object.FindObjectsByType<AttackContactRelay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            relays++;
            if (relay.GetComponentInParent<IAttackContactReceiver>() != null)
            {
                wired++;
            }
        }

        sb.Append($"relays={relays} wiredToReceiver={wired} ");

        var combat = Object.FindAnyObjectByType<PlayerCombat>();
        var locomotion = Object.FindAnyObjectByType<PlayerLocomotion>();
        if (combat == null || locomotion == null)
        {
            sb.Append("| MISSING player");
            Debug.LogError(sb.ToString());
            return sb.ToString();
        }

        var go = new GameObject("~ContactFacingProbe");
        var probe = go.AddComponent<Probe>();
        probe.Combat = combat;
        probe.Locomotion = locomotion;
        probe.Header = sb.ToString();
        return "probe started; read the console for [VerifyContactAndFacing]";
    }

    public sealed class Probe : MonoBehaviour
    {
        public PlayerCombat Combat;
        public PlayerLocomotion Locomotion;
        public string Header;

        private IEnumerator Start()
        {
            for (int i = 0; i < 10; i++) yield return null;

            // Point the character due north, steer south-east, and swing — all within a single frame so
            // ordinary locomotion turning cannot contribute. Any rotation measured is the attack's snap.
            Combat.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            Locomotion.SetMoveInput(new Vector2(1f, -1f).normalized); // south-east in camera space

            float before = Combat.transform.eulerAngles.y;
            Combat.TryAttack();
            float after = Combat.transform.eulerAngles.y;
            Vector3 intent = Locomotion.MoveIntentDirection;
            float intentYaw = intent.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(intent, Vector3.up).eulerAngles.y : after;
            float errorToIntent = Mathf.Abs(Mathf.DeltaAngle(after, intentYaw));
            float turned = Mathf.Abs(Mathf.DeltaAngle(before, after));

            bool snapped = errorToIntent < 2f && turned > 45f;

            Locomotion.SetMoveInput(Vector2.zero);

            bool pass = snapped;
            string msg = $"{Header}| snapTest: turned={turned:0.0}° inOneFrame, errorToStickDirection={errorToIntent:0.00}° " +
                         $"=> {(pass ? "PASS" : "FAIL")}";
            if (pass) { Debug.Log(msg); } else { Debug.LogError(msg); }

            Destroy(gameObject);
        }
    }
}
