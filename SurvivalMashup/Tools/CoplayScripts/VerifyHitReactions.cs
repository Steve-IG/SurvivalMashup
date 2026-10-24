using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Combat;
using ToyChest.Gameplay.Enemy;
using ToyChest.Gameplay.Player;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Play-mode validation of the RG C review adjustments. Enter play from Bootstrap.unity, focus the Game view, run.
/// <list type="bullet">
/// <item><b>Camera</b>: the gameplay camera follows the orbit pivot from behind and above at its authored distance;
/// orbiting the pivot 90° swings the camera, and W then moves the player along the new camera forward.</item>
/// <item><b>Real input on a Training Dummy</b>: the Q combo's punches flinch it (Hit state), the finisher staggers it
/// (HitHeavy state).</item>
/// <item><b>Ground Slam (R) on two Grunts</b>: both knocked back (Knockback state) and actually displaced.</item>
/// <item><b>Interrupt window on a live, attacking Grunt</b> (impacts delivered through the public receiver): a flinch at
/// the start of its wind-up cancels the swing; a flinch after the window does not (the swing lands); a stagger after
/// the window still cancels it.</item>
/// <item><b>Grunt Brute</b>: a punch-weight blow is shrugged off (no reaction, no hit animation); a slam-weight blow
/// flinches it.</item>
/// </list>
/// Output: console [VerifyHitReactions] + Temp/VerifyHitReactions.txt.
/// </summary>
public static class VerifyHitReactions
{
    public static string Execute()
    {
        var combat = Object.FindAnyObjectByType<PlayerCombat>();
        if (combat == null) return "no PlayerCombat (enter play from Bootstrap)";
        new GameObject("~HitReactionProbe").AddComponent<Probe>().Combat = combat;
        return "probe started; read [VerifyHitReactions]";
    }

    public sealed class Probe : MonoBehaviour
    {
        public PlayerCombat Combat;
        private readonly StringBuilder _log = new StringBuilder();
        private bool _allPass = true;
        private CharacterController _cc;
        private readonly List<(GameplayObject target, HitReaction reaction)> _reactions = new List<(GameplayObject, HitReaction)>();

        private void Check(bool ok, string line)
        {
            if (!ok) _allPass = false;
            _log.AppendLine((ok ? "  ok   " : "  FAIL ") + line);
        }

        private static void Keys(params Key[] k) => InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(k));

        private IEnumerator Tap(Key key)
        {
            Keys(key);
            yield return null;
            Keys();
            yield return null;
        }

        private IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        private IEnumerator Seconds(float s)
        {
            float t = 0f;
            while (t < s) { t += Time.deltaTime; yield return null; }
        }

        private IEnumerator UntilIdle(float limit = 4f)
        {
            float t = 0f;
            while (t < 0.1f || (Combat.IsAttacking && t < limit)) { t += Time.deltaTime; yield return null; }
            yield return Frames(5);
        }

        private void Teleport(Vector3 position, Vector3 face)
        {
            _cc.enabled = false;
            Combat.transform.SetPositionAndRotation(position, Quaternion.LookRotation(new Vector3(face.x, 0f, face.z), Vector3.up));
            _cc.enabled = true;
            Physics.SyncTransforms();
        }

        private static void Move(Component c, Vector3 position)
        {
            var cc = c.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            c.transform.position = position;
            if (cc != null) cc.enabled = true;
            Physics.SyncTransforms();
        }

        private static bool InState(Animator animator, string state) =>
            animator.GetCurrentAnimatorStateInfo(0).IsName(state) || animator.GetNextAnimatorStateInfo(0).IsName(state);

        private IEnumerator WatchState(Animator animator, string state, float seconds, System.Action<bool> result)
        {
            bool seen = false;
            float t = 0f;
            while (t < seconds) { seen |= InState(animator, state); t += Time.deltaTime; yield return null; }
            result(seen);
        }

        private IEnumerator Start()
        {
            _cc = Combat.GetComponent<CharacterController>();
            Combat.HitReacted += (target, reaction) => _reactions.Add((target, reaction));
            var health = Combat.GetComponent<GameplayObjectBehaviour>().Object.Get<ToyChest.Systems.Resources.ResourceSet>()
                .GetResource(new ToyChest.Framework.Data.DefinitionId("resource.health"));

            // --- 1. Camera ------------------------------------------------------------------------------------
            var pivot = Object.FindAnyObjectByType<ThirdPersonCameraPivot>();
            var vcam = Object.FindAnyObjectByType<CinemachineCamera>();
            Check(pivot != null && vcam != null && vcam.Follow == pivot.transform && vcam.GetComponent<CinemachineThirdPersonFollow>() != null,
                "gameplay camera: Third Person Follow on the orbit pivot");
            Teleport(new Vector3(10f, 1.1f, -14f), Vector3.forward);
            yield return Seconds(0.6f);
            Transform cam = Camera.main.transform;
            Vector3 toPlayer = Combat.transform.position - cam.position;
            float back = Vector3.Dot(toPlayer, Vector3.forward);
            float eyeHeight = cam.position.y - (Combat.transform.position.y - 1f);
            Check(back > 2.5f && back < 3.5f && eyeHeight > 1.4f && eyeHeight < 2.2f,
                $"close camera: {back:0.0} m behind the player at {eyeHeight:0.0} m above the ground (eye height), looking along +Z");

            // God of War framing (measured from the reference): character on the left third, head ~39% from the top,
            // feet at the bottom edge.
            Camera view = Camera.main;
            Vector3 root = Combat.transform.position;
            Vector3 body = view.WorldToViewportPoint(root);
            Vector3 head = view.WorldToViewportPoint(root + Vector3.up * 0.8f);
            Vector3 feet = view.WorldToViewportPoint(root - Vector3.up * 1f);
            Check(body.x > 0.3f && body.x < 0.42f, $"character on the left third: x {body.x:0.00} of the screen width (reference 0.36)");
            Check(1f - head.y > 0.33f && 1f - head.y < 0.45f, $"head {1f - head.y:0.00} from the top (reference 0.39)");
            Check(1f - feet.y > 0.92f && 1f - feet.y < 1.06f, $"feet at the bottom edge: {1f - feet.y:0.00} from the top");
            pivot.Orbit(90f - pivot.Yaw, 0f);
            yield return Seconds(0.8f);
            Vector3 camForward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Check(Vector3.Dot(camForward, Vector3.right) > 0.95f, $"orbit 90°: camera now faces +X (dot {Vector3.Dot(camForward, Vector3.right):0.00})");
            Vector3 before = Combat.transform.position;
            Keys(Key.W);
            yield return Seconds(0.5f);
            Keys();
            Vector3 moved = Combat.transform.position - before;
            Check(moved.x > 1f && Mathf.Abs(moved.z) < 0.5f, $"W moves along the camera's forward: moved ({moved.x:0.0}, {moved.z:0.0})");
            pivot.Orbit(-pivot.Yaw, 0f);
            yield return Seconds(0.3f);

            // Gamepad auto-align: after a stick look and 1.5 s of no look input, travelling diagonally swings the camera
            // behind the direction of travel. (Mouse/keyboard players are never auto-rotated; the rest of this probe relies on it.)
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = new Vector2(0.6f, 0f) });
            yield return Frames(3);
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return Frames(2);
            float yawAfterStick = pivot.Yaw;
            Teleport(new Vector3(-14f, 1.1f, -18f), Vector3.forward);
            Keys(Key.W, Key.D);
            yield return Seconds(3.5f);
            Keys();
            float swung = Mathf.DeltaAngle(yawAfterStick, pivot.Yaw);
            Check(swung > 15f, $"gamepad auto-align: walking diagonally swung the camera {swung:0} degrees toward the travel direction");
            InputSystem.RemoveDevice(pad);
            yield return Frames(2);
            pivot.Orbit(-pivot.Yaw, 0f);
            yield return Seconds(0.6f);

            // --- 2. Real input on a Training Dummy: flinch, then the finisher's stagger ------------------------
            TrainingDummy dummy = Object.FindObjectsByType<TrainingDummy>().OrderBy(d => d.name).First();
            Animator dummyAnimator = dummy.GetComponentInChildren<Animator>();
            GameplayObject dummyObject = dummy.GetComponent<GameplayObjectBehaviour>().Object;
            Vector3 origin = new Vector3(-12f, 1.1f, -12f);
            Move(dummy, origin + Vector3.forward * 1.2f);
            Teleport(origin, Vector3.forward);
            yield return Frames(10);
            _reactions.Clear();
            bool sawHit = false, sawHeavy = false;
            for (int press = 0; press < 3; press++)
            {
                yield return Tap(Key.Q);
                float t = 0f;
                while (t < 0.45f) { sawHit |= InState(dummyAnimator, "Hit"); sawHeavy |= InState(dummyAnimator, "HitHeavy"); t += Time.deltaTime; yield return null; }
            }

            yield return UntilIdle();
            var onDummy = _reactions.Where(r => r.target == dummyObject).Select(r => r.reaction.Level).ToList();
            Check(onDummy.Contains(HitReactionLevel.Flinch) && sawHit, $"punches flinch the dummy (reactions [{string.Join(",", onDummy)}]), Hit state played: {sawHit}");
            Check(onDummy.Contains(HitReactionLevel.Stagger) && sawHeavy, $"the combo finisher staggers it, HitHeavy state played: {sawHeavy}");
            Move(dummy, new Vector3(-5.5f, 1f, 6f));

            // --- 3. Ground Slam knocks Grunts back --------------------------------------------------------------
            var grunts = LiveGrunts().Take(2).ToList();
            Check(grunts.Count == 2, $"{grunts.Count} live Grunts available");
            if (grunts.Count == 2)
            {
                Vector3 arena = new Vector3(12f, 1.1f, -12f);
                Teleport(arena, Vector3.forward);
                Move(grunts[0], arena + new Vector3(0f, 0f, 1.6f));
                Move(grunts[1], arena + new Vector3(1.4f, 0f, 0.4f));
                yield return Frames(2);
                Vector3[] start = grunts.Select(g => g.transform.position).ToArray();
                _reactions.Clear();
                bool sawKnockback = false;
                yield return Tap(Key.R);
                float t = 0f;
                while (t < 1.6f) { sawKnockback |= InState(grunts[0].GetComponentInChildren<Animator>(), "Knockback"); t += Time.deltaTime; yield return null; }
                float[] pushed = grunts.Select((g, i) => Vector2.Distance(new Vector2(g.transform.position.x, g.transform.position.z), new Vector2(start[i].x, start[i].z))).ToArray();
                var levels = _reactions.Select(r => r.reaction.Level).ToList();
                Check(levels.Count(l => l == HitReactionLevel.Knockback) == 2, $"Ground Slam: reactions [{string.Join(",", levels)}] (want 2× Knockback, distance {(_reactions.Count > 0 ? _reactions[0].reaction.KnockbackDistance : 0f):0.00} m)");
                Check(pushed.All(d => d > 1f) && sawKnockback, $"Grunts driven back [{string.Join(", ", pushed.Select(d => d.ToString("0.0")))}] m, Knockback state played: {sawKnockback}");
                foreach (EnemyCombatant g in grunts) Move(g, new Vector3(20f, 1f, 20f));
            }

            // --- 4. Interrupt window on an attacking Grunt -------------------------------------------------------
            EnemyCombatant attacker = LiveGrunts().FirstOrDefault();
            if (attacker != null)
            {
                Vector3 ring = new Vector3(-12f, 1.1f, 14f);
                health.SetCurrent(health.Maximum);
                Teleport(ring, Vector3.back);
                Move(attacker, ring + Vector3.back * 1.4f);
                var reactionsAtWindow = new List<string>();

                // a) flinch at the start of the wind-up → cancelled (no blow lands)
                yield return UntilWindUp(attacker, 6f);
                yield return Seconds(0.08f);
                float hp = health.Current;
                HitReaction early = attacker.ReceiveImpact(new HitImpact(10f, attacker.transform.position - ring, attacker.transform.position, null));
                bool cancelledEarly = !attacker.IsWindingUp && attacker.IsStaggered;
                yield return Seconds(0.6f);
                Check(early.Level == HitReactionLevel.Flinch && cancelledEarly && health.Current == hp,
                    $"flinch 0.08 s into the wind-up cancels the swing and pauses the Grunt ({early.Level}, cancelled+paused: {cancelledEarly}, player damage over the next 0.6 s {hp - health.Current:0})");

                // b) flinch after the window → the swing is committed and lands
                health.SetCurrent(health.Maximum);
                Move(attacker, ring + Vector3.back * 1.4f);
                yield return UntilWindUp(attacker, 6f);
                yield return Seconds(0.29f); // past the 0.25 s window, before the swing's contact frame (~0.34 s)
                hp = health.Current;
                string diag = $"elapsed {CycleElapsed(attacker):0.00}s, window {Field<float>(attacker, "_interruptWindowSeconds"):0.00}s, wind-up {Field<float>(attacker, "_windUpSeconds"):0.00}s";
                HitReaction late = attacker.ReceiveImpact(new HitImpact(10f, attacker.transform.position - ring, attacker.transform.position, null));
                bool stillWindingUp = attacker.IsWindingUp;
                yield return Seconds(0.4f);
                Check(late.Level == HitReactionLevel.Flinch && stillWindingUp,
                    $"flinch 0.29 s into the wind-up (past the 0.25 s window) does not stop it: still winding up {stillWindingUp}, the swing then dealt {hp - health.Current:0} [{diag}]");

                // c) stagger after the window → still cancelled
                yield return Seconds(1f);
                health.SetCurrent(health.Maximum);
                Move(attacker, ring + Vector3.back * 1.4f);
                yield return UntilWindUp(attacker, 6f);
                yield return Seconds(0.29f);
                hp = health.Current;
                HitReaction heavy = attacker.ReceiveImpact(new HitImpact(16f, attacker.transform.position - ring, attacker.transform.position, null));
                bool cancelledHeavy = !attacker.IsWindingUp && attacker.IsStaggered;
                yield return Seconds(0.6f);
                Check(heavy.Level == HitReactionLevel.Stagger && cancelledHeavy && health.Current == hp,
                    $"stagger 0.29 s into the wind-up cancels it and locks the Grunt ({heavy.Level}, player damage {hp - health.Current:0})");
                Move(attacker, new Vector3(20f, 1f, 20f));
            }
            else
            {
                Check(false, "no live Grunt for the interrupt-window checks");
            }

            // --- 5. The Grunt Brute is heavy ----------------------------------------------------------------------
            var encounter = Object.FindAnyObjectByType<EliteEncounter>();
            Teleport(encounter.transform.position + Vector3.up * 1.1f, Vector3.left);
            yield return Frames(5);
            EnemyCombatant brute = encounter.Elite;
            if (brute != null)
            {
                foreach (EnemyCombatant e in encounter.Escorts) if (e != null) Move(e, new Vector3(-24f, 1f, 24f));
                Teleport(brute.transform.position + Vector3.right * 9f, Vector3.left);
                yield return Frames(3);
                Animator bruteAnimator = brute.GetComponentInChildren<Animator>();
                HitReaction punch = brute.ReceiveImpact(new HitImpact(10f, Vector3.left, brute.transform.position, null));
                bool flinchAnim = false;
                yield return WatchState(bruteAnimator, "Hit", 0.3f, seen => flinchAnim = seen);
                Check(punch.Level == HitReactionLevel.None && !flinchAnim, $"Brute (stability {brute.Stability:0}) shrugs off a punch-weight blow: {punch}, Hit played {flinchAnim}");
                HitReaction slam = brute.ReceiveImpact(new HitImpact(30f, Vector3.left, brute.transform.position, null));
                bool slamAnim = false;
                yield return WatchState(bruteAnimator, "Hit", 0.4f, seen => slamAnim = seen);
                Check(slam.Level == HitReactionLevel.Flinch && (slamAnim || brute.IsSignatureWindingUp), $"a slam-weight blow flinches it: {slam}, Hit played {slamAnim}");
            }
            else
            {
                Check(false, "the elite encounter did not start");
            }

            health.SetCurrent(health.Maximum);
            Finish();
        }

        private IEnumerator UntilWindUp(EnemyCombatant enemy, float limit)
        {
            float t = 0f;
            while (enemy.IsWindingUp && t < limit) { t += Time.deltaTime; yield return null; }
            while (!enemy.IsWindingUp && t < limit) { t += Time.deltaTime; yield return null; }
        }

        private static T Field<T>(object o, string name) =>
            (T)o.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(o);

        private static float CycleElapsed(EnemyCombatant e) => Field<EnemyAttackCycle>(e, "_attackCycle").WindUpElapsed;

        private static List<EnemyCombatant> LiveGrunts()
        {
            var live = new List<EnemyCombatant>();
            foreach (EnemyCombatant e in Object.FindObjectsByType<EnemyCombatant>())
            {
                if (e.GetComponent<TrainingDummy>() != null || e.HasSignatureAttack || e.IsDead) continue;
                var b = e.GetComponent<GameplayObjectBehaviour>();
                if (b != null && b.Object != null && b.Object.IsActive) live.Add(e);
            }

            return live.OrderBy(e => e.name).ToList();
        }

        private void Finish()
        {
            Keys();
            string verdict = _allPass ? "PASS" : "FAIL";
            _log.Insert(0, $"[VerifyHitReactions] => {verdict}\n");
            System.IO.File.WriteAllText("Temp/VerifyHitReactions.txt", _log.ToString());
            if (_allPass) Debug.Log(_log.ToString()); else Debug.LogError(_log.ToString());
            Destroy(gameObject);
        }
    }
}
