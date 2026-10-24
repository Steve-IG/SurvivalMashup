using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Enemy;
using ToyChest.Gameplay.Objects;
using ToyChest.Gameplay.Player;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.Attributes;
using ToyChest.Systems.Inventory;
using ToyChest.Systems.Resources;
using ToyChest.Systems.StatusEffects;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Play-mode validation of Review Group C — the Grunt Brute elite encounter. Enter play from Bootstrap.unity, focus the
/// Game view (queued key presses are dropped otherwise), then run. Melee (Q), roll (Left Ctrl + D), and Ground Slam (R)
/// are real queued keyboard input; positioning between phases is teleporting, and the last stretch of the elite's health
/// is removed directly (to reach the defeat without a minute of scripted combat). Checks:
/// <list type="bullet">
/// <item>the encounter object composes through Bootstrap → VerticalSlice and starts undefeated, with nothing spawned;</item>
/// <item>stepping onto the pad spawns the elite (300/300 HP, elite status and tag, slower) and two escorts, once;</item>
/// <item>the elite engages: the slam's wind-up starts, the ground telegraph shows from the wind-up's first frame to the
/// impact with its fill growing, the Slam animation plays, and the area lands ~1.2 s later;</item>
/// <item>a player standing in the circle takes the slam's 22 damage on the impact frame — and a punch during the wind-up
/// does not cancel it;</item>
/// <item>after the slam the elite stays rooted through its recovery, where a punch lands for 20;</item>
/// <item>rolling out of the circle during the wind-up avoids all slam damage;</item>
/// <item>Ground Slam, pressed during the elite's recovery, hits the elite and two escorts for exactly 30 each;</item>
/// <item>reinforcements arrive once at half health;</item>
/// <item>defeat fires Completed once with the reward granted (Brute's Horn +1, charge spent), the banner shows, and
/// nothing fires twice;</item>
/// <item>a rematch from the pad plays again, completes, and is refused the reward (no second horn).</item>
/// </list>
/// Output: console [VerifyEliteEncounter] + Temp/VerifyEliteEncounter.txt. Reads public seams only; adds no gameplay.
/// </summary>
public static class VerifyEliteEncounter
{
    public static string Execute()
    {
        var encounter = Object.FindAnyObjectByType<EliteEncounter>();
        var combat = Object.FindAnyObjectByType<PlayerCombat>();
        if (encounter == null || combat == null) return "no EliteEncounter/PlayerCombat (enter play from Bootstrap)";
        var probe = new GameObject("~EliteEncounterProbe").AddComponent<Probe>();
        probe.Encounter = encounter;
        probe.Combat = combat;
        return "probe started; read [VerifyEliteEncounter]";
    }

    public sealed class Probe : MonoBehaviour
    {
        private static readonly DefinitionId Health = new DefinitionId("resource.health");
        private static readonly DefinitionId Charge = new DefinitionId("resource.encounter_charge");
        private static readonly DefinitionId Horn = new DefinitionId("item.brute_horn");
        private static readonly DefinitionId EliteStatus = new DefinitionId("status.elite_brute");
        private static readonly DefinitionId EnemySpeed = new DefinitionId("attribute.enemy_speed");
        private static readonly Vector3 Park = new Vector3(-24f, 1f, 24f);

        public EliteEncounter Encounter;
        public PlayerCombat Combat;

        private readonly StringBuilder _log = new StringBuilder();
        private bool _allPass = true;
        private CharacterController _cc;
        private GameplayObject _player;
        private ResourceValue _playerHealth;
        private int _started, _reinforced, _completed;
        private AbilityActivationResult _lastResult;
        private int _windUps, _struckCount, _struckFrame = -1, _struckDamaged = -1, _windUpFrame = -1;
        private float _windUpTime, _struckTime;
        private readonly List<int> _playerDamageFrames = new List<int>();
        private readonly List<float> _playerDamage = new List<float>();
        private EnemyCombatant _elite;

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

        private static ResourceValue HealthOf(EnemyCombatant e) =>
            e != null && e.ComposedObject != null && e.ComposedObject.TryGet(out ResourceSet r) ? r.GetResource(Health) : null;

        private void HealPlayer() => _playerHealth.SetCurrent(_playerHealth.Maximum);

        private void BindElite(EnemyCombatant elite)
        {
            _elite = elite;
            _elite.SignatureWindUpStarted += () => { _windUps++; _windUpFrame = Time.frameCount; _windUpTime = Time.time; };
            _elite.SignatureStruck += (center, radius, damaged) =>
            {
                _struckCount++;
                _struckFrame = Time.frameCount;
                _struckTime = Time.time;
                _struckDamaged = damaged;
            };
        }

        private IEnumerator UntilWindUp(float limit)
        {
            int before = _windUps;
            float t = 0f;
            while (_windUps == before && t < limit) { t += Time.deltaTime; yield return null; }
        }

        private IEnumerator UntilStruck(float limit)
        {
            int before = _struckCount;
            float t = 0f;
            while (_struckCount == before && t < limit) { t += Time.deltaTime; yield return null; }
        }

        private float PlayerDamageOnFrame(int frame) =>
            _playerDamageFrames.Select((f, i) => f == frame ? _playerDamage[i] : 0f).Sum();

        private IEnumerator Start()
        {
            _cc = Combat.GetComponent<CharacterController>();
            _player = Combat.GetComponent<GameplayObjectBehaviour>().Object;
            _playerHealth = _player.Get<ResourceSet>().GetResource(Health);
            _playerHealth.ValueChanged += (previous, current, maximum) =>
            {
                if (current < previous) { _playerDamageFrames.Add(Time.frameCount); _playerDamage.Add(previous - current); }
            };
            Encounter.Started += _ => _started++;
            Encounter.Reinforced += () => _reinforced++;
            Encounter.Completed += result => { _completed++; _lastResult = result; };
            InventorySet inventory = _player.Get<InventorySet>();
            int hornsBefore = inventory.QuantityOf(Horn);

            // --- 1. Initialization through Bootstrap ------------------------------------------------------------
            GameplayObject encounterObject = Encounter.GetComponent<GameplayObjectBehaviour>().Object;
            ResourceValue charge = encounterObject != null && encounterObject.TryGet(out ResourceSet er) ? er.GetResource(Charge) : null;
            Check(encounterObject != null && encounterObject.IsActive && encounterObject.DefinitionId == new DefinitionId("object.elite_encounter_brute"),
                "encounter object composed and active via Bootstrap → VerticalSlice (object.elite_encounter_brute)");
            Check(charge != null && charge.Current == 1f && !Encounter.HasBeenDefeated && Encounter.State == EliteEncounterState.Armed,
                $"starts undefeated: charge {(charge != null ? charge.Current : -1f)}, state {Encounter.State}");
            Check(Object.FindObjectsByType<EnemyAreaTelegraph>().Length == 0, "no elite in the scene before the pad is stepped on");

            // --- 2. Step onto the pad -----------------------------------------------------------------------------
            Teleport(Encounter.transform.position + Vector3.up * 1.1f, Vector3.left);
            yield return Frames(5);
            EnemyCombatant elite = Encounter.Elite;
            Check(_started == 1 && Encounter.State == EliteEncounterState.Active && elite != null, $"pad → encounter Active, Started ×{_started}");
            if (elite == null) { Finish(); yield break; }
            BindElite(elite);
            GameplayObject eliteObject = elite.ComposedObject;
            ResourceValue eliteHealth = HealthOf(elite);
            float speed = eliteObject.Get<AttributeSet>().GetValue(EnemySpeed);
            Check(eliteObject.DefinitionId == new DefinitionId("object.grunt_brute") && eliteHealth.Current == 300f && eliteHealth.Maximum == 300f,
                $"elite object.grunt_brute composed at {eliteHealth.Current}/{eliteHealth.Maximum} HP");
            Check(eliteObject.Get<StatusEffectSet>().Has(EliteStatus) && Mathf.Approximately(speed, 2.3f) && elite.HasSignatureAttack,
                $"elite status applied (speed {speed:0.0}), signature attack present, scale {elite.transform.localScale.y:0.00}");
            List<EnemyCombatant> escorts = Encounter.Escorts.Where(e => e != null).ToList();
            Check(escorts.Count == 2 && escorts.All(e => e.ComposedObject != null && e.ComposedObject.DefinitionId == new DefinitionId("object.grunt")),
                $"{escorts.Count} Grunt escorts composed with the elite");
            foreach (EnemyCombatant e in escorts) Move(e, Park + new Vector3(Random.Range(-1f, 1f), 0f, 0f));
            yield return Frames(3);
            Teleport(Encounter.transform.position + new Vector3(-3f, 1.1f, 3f), Vector3.left);
            yield return Frames(3);
            Check(Encounter.State == EliteEncounterState.Active && _started == 1, "stepping off the pad mid-fight neither restarts nor ends it");

            // --- 3. Telegraph → slam lands on a player standing in the circle (a punch cannot stop it) ------------
            var telegraph = elite.GetComponent<EnemyAreaTelegraph>();
            Animator animator = elite.GetComponentInChildren<Animator>();
            Vector3 east = Vector3.right;
            Teleport(elite.transform.position + east * 1.6f, -east);
            HealPlayer();
            yield return UntilWindUp(8f);
            Check(_windUps == 1, $"elite engaged: signature wind-up began ({_windUps})");
            float eliteXAtWindUp = elite.transform.position.x;
            yield return null;
            float firstFill = telegraph.FillFraction;
            bool shownFirst = telegraph.IsShowing && firstFill < 0.1f;
            bool slamState = false, shownThroughout = true;
            float lastFill = -1f, minFill = 1f;
            bool fillGrows = true;
            bool punched = false;
            int punchFrame = -1;
            float eliteBeforePunch = eliteHealth.Current;
            while (_struckCount == 0 && Time.time - _windUpTime < 3f)
            {
                if (!telegraph.IsShowing) shownThroughout = false;
                if (telegraph.FillFraction + 1e-4f < lastFill) fillGrows = false;
                lastFill = telegraph.FillFraction;
                minFill = Mathf.Min(minFill, telegraph.FillFraction);
                slamState |= animator.GetCurrentAnimatorStateInfo(0).IsName("Slam") || animator.GetNextAnimatorStateInfo(0).IsName("Slam");
                if (!punched && Time.time - _windUpTime > 0.25f)
                {
                    punched = true;
                    punchFrame = Time.frameCount;
                    Teleport(Combat.transform.position, elite.transform.position - Combat.transform.position);
                    Keys(Key.Q);
                    yield return null;
                    Keys();
                }

                yield return null;
            }

            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            float handAboveFeet = hand != null ? hand.position.y - animator.transform.position.y : -1f;
            float delay = _struckTime - _windUpTime;
            Check(shownFirst, $"telegraph showing from the wind-up's first frame (fill {firstFill:0.00} at frame +1)");
            Check(shownThroughout && fillGrows, $"telegraph shown every frame until impact, fill grew monotonically (last {lastFill:0.00})");
            Check(slamState, "elite animator entered the Slam state during the wind-up");
            Check(_struckCount == 1 && Mathf.Abs(delay - 1.2f) < 0.06f, $"slam landed {delay:0.000}s after the wind-up began (authored 1.2s)");
            float slamDamage = PlayerDamageOnFrame(_struckFrame);
            Check(Mathf.Approximately(slamDamage, 22f) && _struckDamaged == 1,
                $"player inside the circle took {slamDamage:0} on the impact frame (authored 22), targets damaged {_struckDamaged}");
            Check(punched && eliteHealth.Current < eliteBeforePunch, $"a punch during the wind-up landed ({eliteBeforePunch - eliteHealth.Current:0} damage) yet did not cancel the slam");
            Check(hand != null && handAboveFeet > -0.2f && handAboveFeet < 0.6f, $"impact frame is the slam's landing pose: right hand {handAboveFeet:0.00} m above the elite's feet");
            Check(Mathf.Abs(elite.transform.position.x - eliteXAtWindUp) < 0.05f, "elite rooted during the wind-up");

            // --- 4. Recovery is the punish window -----------------------------------------------------------------
            HealPlayer();
            Vector3 rootedAt = elite.transform.position;
            Check(elite.IsSignatureRecovering, "elite is in its post-slam recovery");
            float beforeRecoveryPunch = eliteHealth.Current;
            Teleport(elite.transform.position + east * 1.6f, -east);
            yield return Tap(Key.Q);
            yield return Seconds(0.6f);
            Check(eliteHealth.Current <= beforeRecoveryPunch - 20f && elite.IsSignatureRecovering,
                $"punch during recovery landed for {beforeRecoveryPunch - eliteHealth.Current:0} and the elite stayed rooted (still recovering)");
            Check(Vector3.Distance(elite.transform.position, rootedAt) < 0.05f, "elite did not move during recovery");
            yield return Seconds(1.2f);

            // --- 5. Dodge: roll out of the circle during the wind-up -------------------------------------------
            HealPlayer();
            Teleport(elite.transform.position + east * 2.2f, east);
            yield return UntilWindUp(9f);
            Check(_windUps == 2, $"second slam wind-up began after the cooldown ({_windUps})");
            yield return Seconds(0.25f);
            HealPlayer();
            int damageEntries = _playerDamageFrames.Count;
            Keys(Key.D, Key.LeftCtrl);
            yield return null;
            Keys(Key.D);
            yield return UntilStruck(3f);
            Keys();
            float dodgedDistance = Vector2.Distance(new Vector2(Combat.transform.position.x, Combat.transform.position.z),
                new Vector2(elite.transform.position.x, elite.transform.position.z));
            float dodgeDamage = PlayerDamageOnFrame(_struckFrame);
            Check(_struckCount == 2 && dodgeDamage == 0f && _struckDamaged == 0,
                $"rolled out (now {dodgedDistance:0.0} m from the elite's centre, radius 3.5): slam landed and dealt {dodgeDamage:0} damage, {_struckDamaged} targets");

            // --- 6. Ground Slam during the recovery hits the elite and its escorts -------------------------------
            yield return Frames(2);
            Move(escorts[0], elite.transform.position + new Vector3(1.2f, 0f, 1.6f));
            Move(escorts[1], elite.transform.position + new Vector3(1.2f, 0f, -1.6f));
            Teleport(elite.transform.position + east * 1.8f, -east);
            float eliteBefore = eliteHealth.Current;
            float[] escortBefore = escorts.Select(e => HealthOf(e).Current).ToArray();
            yield return Tap(Key.R);
            float t6 = 0f;
            while (Combat.IsAttacking && t6 < 3f) { t6 += Time.deltaTime; yield return null; }
            float[] escortDamage = escorts.Select((e, i) => escortBefore[i] - (HealthOf(e) != null ? HealthOf(e).Current : 0f)).ToArray();
            Check(Mathf.Approximately(eliteBefore - eliteHealth.Current, 30f) && escortDamage.All(d => Mathf.Approximately(d, 30f)),
                $"Ground Slam (R) hit the elite for {eliteBefore - eliteHealth.Current:0} and escorts for [{string.Join(", ", escortDamage.Select(d => d.ToString("0")))}] (authored 30 each)");
            foreach (EnemyCombatant e in escorts) if (e != null) Move(e, Park);

            // --- 7. Reinforcements at half health -----------------------------------------------------------------
            eliteHealth.Consume(eliteHealth.Current - eliteHealth.Maximum * 0.45f);
            yield return Frames(3);
            int liveEscorts = Encounter.Escorts.Count(e => e != null);
            Check(_reinforced == 1 && liveEscorts == 4, $"reinforcements arrived once at half health: Reinforced ×{_reinforced}, {liveEscorts} escorts spawned in total");
            foreach (EnemyCombatant e in Encounter.Escorts) if (e != null) Move(e, Park);
            eliteHealth.Consume(eliteHealth.Current * 0.5f);
            yield return Frames(3);
            Check(_reinforced == 1, "no second reinforcement wave");

            // --- 8. Defeat → victory and reward, exactly once -----------------------------------------------------
            Teleport(elite.transform.position + Vector3.forward * 6f, Vector3.back); // north: well clear of the pad (east)
            eliteHealth.Consume(eliteHealth.Current);
            yield return Frames(2);
            var hud = Encounter.GetComponent<EliteEncounterHud>();
            Check(_completed == 1 && _lastResult == AbilityActivationResult.Activated, $"elite defeated → Completed ×{_completed} ({_lastResult})");
            Check(inventory.QuantityOf(Horn) == hornsBefore + 1 && charge.Current == 0f && Encounter.HasBeenDefeated,
                $"reward: Brute's Horn {hornsBefore} → {inventory.QuantityOf(Horn)}, encounter charge {charge.Current} (defeated)");
            Check(hud != null && hud.IsBannerShowing, "completion banner showing");
            yield return Seconds(2.6f);
            Check(elite == null, "elite corpse torn down after its death linger");
            Check(_completed == 1 && inventory.QuantityOf(Horn) == hornsBefore + 1, "no duplicate completion or reward after teardown");
            Check(Encounter.State == EliteEncounterState.Armed, $"pad re-armed for a rematch ({Encounter.State})");

            // --- 9. Rematch: plays again, never pays twice ---------------------------------------------------------
            foreach (EnemyCombatant e in Object.FindObjectsByType<EnemyCombatant>())
                if (e.GetComponent<TrainingDummy>() == null && Vector3.Distance(e.transform.position, Park) < 4f) Move(e, Park + Vector3.right * 2f);
            Teleport(Encounter.transform.position + Vector3.up * 1.1f, Vector3.left);
            yield return Frames(5);
            EnemyCombatant rematch = Encounter.Elite;
            Check(_started == 2 && rematch != null && HealthOf(rematch).Current == 300f, $"rematch started ×{_started}: a fresh elite at {(rematch != null ? HealthOf(rematch).Current : 0f)} HP");
            Teleport(Encounter.transform.position + new Vector3(4f, 1.1f, 0f), Vector3.left);
            yield return Frames(2);
            if (rematch != null) HealthOf(rematch).Consume(HealthOf(rematch).Current);
            yield return Frames(3);
            Check(_completed == 2 && _lastResult == AbilityActivationResult.CannotAfford && inventory.QuantityOf(Horn) == hornsBefore + 1,
                $"rematch won → Completed ×{_completed} ({_lastResult}); horns still {inventory.QuantityOf(Horn)}");

            HealPlayer();
            Finish();
        }

        private void Finish()
        {
            Keys();
            string verdict = _allPass ? "PASS" : "FAIL";
            _log.Insert(0, $"[VerifyEliteEncounter] => {verdict}\n");
            System.IO.File.WriteAllText("Temp/VerifyEliteEncounter.txt", _log.ToString());
            if (_allPass) Debug.Log(_log.ToString()); else Debug.LogError(_log.ToString());
            Destroy(gameObject);
        }
    }
}
