using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Boot;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Enemy;
using ToyChest.Gameplay.Objects;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.Attributes;
using ToyChest.Systems.GameplayEffects;
using ToyChest.Systems.Inventory;
using ToyChest.Systems.Resources;
using ToyChest.Systems.Save;
using ToyChest.Systems.StatusEffects;
using ToyChest.Systems.Tags;
using ToyChest.Tests.Events;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ToyChest.Tests.Content
{
    /// <summary>
    /// Review Group C — the Grunt Brute elite encounter, verified against the real authored content. The elite is the
    /// Grunt composition plus a signature area ability (<c>ability.brute_slam</c>) and an innate status
    /// (<c>status.elite_brute</c>) that its <see cref="EnemyCombatant"/> applies on composition. The encounter object
    /// (<c>object.elite_encounter_brute</c>) owns a one-shot charge and a reward ability that costs it — so the reward
    /// is granted once by the Ability System itself and the spent charge is the persisted "defeated" state. Play-mode
    /// behaviour (pad, spawning, telegraph timing, dodge, victory) is covered by the VerifyEliteEncounter probe.
    /// </summary>
    public sealed class EliteEncounterContentTests
    {
        private const float Tolerance = 1e-4f;
        private const string DefinitionsFolder = "Assets/Game/Content/Definitions";
        private const string BrutePrefabPath = "Assets/Game/Content/Prefabs/GruntBrute.prefab";
        private const string GruntPrefabPath = "Assets/Game/Content/Prefabs/Grunt.prefab";

        private const string PlayerId = "object.player";
        private const string GruntId = "object.grunt";
        private const string BruteId = "object.grunt_brute";
        private const string EncounterId = "object.elite_encounter_brute";

        private static readonly DefinitionId Health = new DefinitionId("resource.health");
        private static readonly DefinitionId MaxHealth = new DefinitionId("attribute.max_health");
        private static readonly DefinitionId EnemySpeed = new DefinitionId("attribute.enemy_speed");
        private static readonly DefinitionId Charge = new DefinitionId("resource.encounter_charge");
        private static readonly DefinitionId EliteStatus = new DefinitionId("status.elite_brute");
        private static readonly DefinitionId Strike = new DefinitionId("ability.enemy_strike");
        private static readonly DefinitionId Slam = new DefinitionId("ability.brute_slam");
        private static readonly DefinitionId Reward = new DefinitionId("ability.elite_reward_brute");
        private static readonly DefinitionId Horn = new DefinitionId("item.brute_horn");
        private static readonly DefinitionId PlayerStrike = new DefinitionId("ability.player_strike");
        private static readonly DefinitionId GroundSlamImpact = new DefinitionId("ability.ground_slam_impact");

        private RuntimeServices _services;

        [SetUp]
        public void SetUp()
        {
            _services = Boot();
        }

        // ------------------------------------------------------------------ the elite

        [Test]
        public void Brute_ComposesFromData_AsAnEnemy_WithTheGruntStrikeAndItsSignatureSlam()
        {
            GameplayObject brute = Spawn(BruteId);
            Assert.IsTrue(brute.Get<GameplayTagContainer>().HasTag(_services.TagTable.GetTag("Actor.Enemy")));
            AbilitySet abilities = brute.Get<AbilitySet>();
            Assert.IsTrue(abilities.Has(Strike), "Keeps the ordinary Grunt claw strike.");
            Assert.IsTrue(abilities.Has(Slam), "Adds the signature Shockwave Slam.");
            Assert.IsNotNull(brute.Get<ResourceSet>().GetResource(Health));
        }

        [Test]
        public void InnateStatus_RaisesHealthToAFullThreeHundred_SlowsIt_MakesItHeavy_AndMarksItElite()
        {
            GameplayObject brute = SpawnBrute();
            ResourceValue health = brute.Get<ResourceSet>().GetResource(Health);
            AttributeSet attributes = brute.Get<AttributeSet>();

            Assert.AreEqual(300f, attributes.GetValue(MaxHealth), Tolerance, "+250 on the shared base Maximum Health of 50.");
            Assert.AreEqual(300f, health.Maximum, Tolerance);
            Assert.AreEqual(300f, health.Current, Tolerance, "The on-apply refill runs after the modifier: it starts at full.");
            Assert.AreEqual(2.3f, attributes.GetValue(EnemySpeed), Tolerance, "Heavier than a Grunt (2.6).");
            Assert.IsTrue(brute.Get<GameplayTagContainer>().HasTag(_services.TagTable.GetTag("Actor.Elite")));
        }

        [Test]
        public void BrutePrefab_IsAGruntVariant_AuthoredWithTheStatusSlamAndTelegraph()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BrutePrefabPath);
            Assert.IsNotNull(prefab, $"Missing {BrutePrefabPath}.");
            Assert.AreEqual(PrefabAssetType.Variant, PrefabUtility.GetPrefabAssetType(prefab));
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<GameObject>(GruntPrefabPath), PrefabUtility.GetCorrespondingObjectFromSource(prefab));
            Assert.AreEqual(BruteId, prefab.GetComponent<GameplayObjectSpawner>().Definition.Id.Value);
            Assert.IsNotNull(prefab.GetComponent<EnemyAreaTelegraph>(), "The slam is telegraphed on the ground.");
            Assert.Greater(prefab.transform.localScale.y, 1f, "Visibly bigger than a Grunt.");

            var combatant = new SerializedObject(prefab.GetComponent<EnemyCombatant>());
            Assert.AreEqual(Slam.Value, combatant.FindProperty("_signatureAbilityId").stringValue);
            SerializedProperty statuses = combatant.FindProperty("_innateStatuses");
            Assert.AreEqual(1, statuses.arraySize);
            Assert.AreEqual(EliteStatus, ((StatusEffectDefinition)statuses.GetArrayElementAtIndex(0).objectReferenceValue).Id);

            var grunt = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(GruntPrefabPath).GetComponent<EnemyCombatant>());
            Assert.IsTrue(string.IsNullOrEmpty(grunt.FindProperty("_signatureAbilityId").stringValue), "An ordinary Grunt has no signature attack.");
            Assert.AreEqual(0, grunt.FindProperty("_innateStatuses").arraySize);
        }

        [Test]
        public void SlamTelegraph_IsFair_ByAuthoredTiming()
        {
            var combatant = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(BrutePrefabPath).GetComponent<EnemyCombatant>());
            float windUp = combatant.FindProperty("_signatureWindUpSeconds").floatValue;
            float recover = combatant.FindProperty("_signatureRecoverSeconds").floatValue;
            float radius = combatant.FindProperty("_signatureRadius").floatValue;
            float trigger = combatant.FindProperty("_signatureTriggerRange").floatValue;

            // The player runs at 7 m/s (Movement Speed base): from the elite's feet the rim is radius / 7 s away.
            Assert.GreaterOrEqual(windUp, 1f, "At least a full second to read the telegraph.");
            Assert.Greater(windUp, radius / 7f * 2f, "Enough time to reach the rim from the centre with margin to spare.");
            Assert.IsFalse(combatant.FindProperty("_signatureWindUpInterruptible").boolValue,
                "Punching cannot stop the slam: leaving the circle is the counterplay.");
            Assert.GreaterOrEqual(recover, 1f, "A real punish window after the slam lands.");
            Assert.GreaterOrEqual(trigger, radius, "Begun only when the player is in or near the circle.");
        }

        [Test]
        public void Slam_DamagesThePlayer_ThroughTheOrdinaryDamagePath_HarderThanTheClaw()
        {
            GameplayObject brute = SpawnBrute();
            GameplayObject player = Spawn(PlayerId);
            ResourceValue health = player.Get<ResourceSet>().GetResource(Health);
            float before = health.Current;

            AbilityDefinition slam = _services.DataRegistry.Get<AbilityDefinition>(Slam);
            AbilityDefinition strike = _services.DataRegistry.Get<AbilityDefinition>(Strike);
            Assert.AreEqual(AbilityTargetMode.Provided, slam.TargetMode);
            Assert.IsInstanceOf<DamageEffect>(slam.Effects[0]);

            var targets = new List<EffectTarget> { EffectTarget.From(player) };
            Assert.AreEqual(AbilityActivationResult.Activated, brute.Get<AbilitySet>().TryActivate(Slam, targets));
            Assert.AreEqual(before - AuthoredAmount(slam.Effects[0]), health.Current, Tolerance);
            Assert.Greater(AuthoredAmount(slam.Effects[0]), AuthoredAmount(strike.Effects[0]) * 2f, "The slam is the blow worth dodging.");
            Assert.Less(AuthoredAmount(slam.Effects[0]), health.Maximum * 0.5f, "Punishing, never a one-shot from full health.");
        }

        [Test]
        public void PlayerMelee_DamagesTheElite_ForItsNormalAmount()
        {
            GameplayObject brute = SpawnBrute();
            ResourceValue health = brute.Get<ResourceSet>().GetResource(Health);
            Assert.AreEqual(AbilityActivationResult.Activated,
                Spawn(PlayerId).Get<AbilitySet>().TryActivate(PlayerStrike, EffectTarget.From(brute)));
            Assert.AreEqual(280f, health.Current, Tolerance, "A punch (20) lands on the elite like on any enemy.");
        }

        [Test]
        public void GroundSlam_DamagesTheEliteAndItsEscorts_ForItsNormalAmount_WithoutBypassingAnything()
        {
            GameplayObject brute = SpawnBrute();
            GameplayObject escort = Spawn(GruntId);
            GameplayObject player = Spawn(PlayerId);
            var targets = new List<EffectTarget> { EffectTarget.From(brute), EffectTarget.From(escort) };

            Assert.AreEqual(AbilityActivationResult.Activated, player.Get<AbilitySet>().TryActivate(GroundSlamImpact, targets));
            Assert.AreEqual(270f, brute.Get<ResourceSet>().GetResource(Health).Current, Tolerance);
            Assert.AreEqual(20f, escort.Get<ResourceSet>().GetResource(Health).Current, Tolerance);

            // Not a shortcut: Ground Slam alone would need ten casts (ten cooldowns, punish windows permitting) to fell the elite.
            float slamDamage = AuthoredAmount(_services.DataRegistry.Get<AbilityDefinition>(GroundSlamImpact).Effects[0]);
            Assert.GreaterOrEqual(Mathf.CeilToInt(300f / slamDamage), 8);
        }

        // ------------------------------------------------------------------ completion and reward

        [Test]
        public void EncounterObject_StartsUndefeated_WithOneCharge()
        {
            ResourceValue charge = Spawn(EncounterId).Get<ResourceSet>().GetResource(Charge);
            Assert.IsNotNull(charge);
            Assert.AreEqual(1f, charge.Current, Tolerance);
        }

        [Test]
        public void Reward_IsGrantedOnce_ThenRefused_SoARematchNeverPaysTwice()
        {
            GameplayObject encounter = Spawn(EncounterId);
            GameplayObject player = Spawn(PlayerId);
            AbilitySet abilities = encounter.Get<AbilitySet>();
            InventorySet inventory = player.Get<InventorySet>();

            Assert.AreEqual(AbilityActivationResult.Activated, abilities.TryActivate(Reward, EffectTarget.From(player)));
            Assert.AreEqual(1, inventory.QuantityOf(Horn), "Victory adds the Brute's Horn through AddItemEffect.");
            Assert.AreEqual(0f, encounter.Get<ResourceSet>().GetResource(Charge).Current, Tolerance, "Spent charge = defeated.");

            Assert.AreEqual(AbilityActivationResult.CannotAfford, abilities.TryActivate(Reward, EffectTarget.From(player)));
            Assert.AreEqual(1, inventory.QuantityOf(Horn), "No duplicate reward.");
        }

        [Test]
        public void DefeatedState_AndTheReward_SurviveSaveAndReload()
        {
            GameplayObject encounter = Spawn(EncounterId);
            GameplayObject player = Spawn(PlayerId);
            encounter.Get<AbilitySet>().TryActivate(Reward, EffectTarget.From(player));

            IReadOnlyList<GameplayObject> restored = SaveAndReload(out RuntimeServices reload);
            GameplayObject restoredEncounter = Find(restored, EncounterId);
            GameplayObject restoredPlayer = Find(restored, PlayerId);

            Assert.AreEqual(0f, restoredEncounter.Get<ResourceSet>().GetResource(Charge).Current, Tolerance, "Still defeated after load.");
            Assert.AreEqual(1, restoredPlayer.Get<InventorySet>().QuantityOf(Horn));
            Assert.AreEqual(AbilityActivationResult.CannotAfford,
                restoredEncounter.Get<AbilitySet>().TryActivate(Reward, EffectTarget.From(restoredPlayer)),
                "A reloaded, defeated encounter never pays out again.");
        }

        [Test]
        public void EliteStats_SurviveSaveAndReload_ThroughItsStatus()
        {
            GameplayObject brute = SpawnBrute();
            brute.Get<ResourceSet>().GetResource(Health).Consume(100f);

            GameplayObject restoredBrute = Find(SaveAndReload(out _), BruteId);
            Assert.IsTrue(restoredBrute.Get<StatusEffectSet>().Has(EliteStatus));
            Assert.AreEqual(300f, restoredBrute.Get<ResourceSet>().GetResource(Health).Maximum, Tolerance);
            Assert.AreEqual(200f, restoredBrute.Get<ResourceSet>().GetResource(Health).Current, Tolerance,
                "The refill is an on-apply effect, so restoring the status does not heal the elite.");
        }

        // ------------------------------------------------------------------ helpers

        // The composition EnemyCombatant performs at runtime: the object, then its innate status.
        private GameplayObject SpawnBrute()
        {
            GameplayObject brute = Spawn(BruteId);
            brute.Get<StatusEffectSet>().Apply(_services.DataRegistry.Get<StatusEffectDefinition>(EliteStatus));
            return brute;
        }

        private GameplayObject Spawn(string id)
        {
            GameplayObjectDefinition definition = _services.DataRegistry.Get<GameplayObjectDefinition>(new DefinitionId(id));
            GameplayObject obj = _services.Factory.Create(definition);
            obj.Activate();
            return obj;
        }

        private IReadOnlyList<GameplayObject> SaveAndReload(out RuntimeServices reload)
        {
            SaveManager save = _services.SaveManager;
            SaveData data = save.FromJson(save.ToJson(save.Capture(_services.Objects)));
            reload = Boot();
            return reload.SaveManager.Restore(data, reload.Factory, reload.DataRegistry);
        }

        private static GameplayObject Find(IReadOnlyList<GameplayObject> objects, string id)
        {
            foreach (GameplayObject candidate in objects)
            {
                if (candidate.DefinitionId == new DefinitionId(id))
                {
                    return candidate;
                }
            }

            Assert.Fail($"{id} was not restored.");
            return null;
        }

        private static float AuthoredAmount(GameplayEffectDefinition effect) =>
            new SerializedObject(effect).FindProperty("_amount").floatValue;

        private static RuntimeServices Boot()
        {
            return new RuntimeBootstrap().Run(
                new BootstrapConfiguration(new RecordingLogger(), new[] { LoadAuthoredDefinitions() }));
        }

        private static DirectDefinitionSource LoadAuthoredDefinitions()
        {
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { DefinitionsFolder });
            var definitions = new List<IDefinition>();
            foreach (string guid in guids)
            {
                if (AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(guid)) is IDefinition definition)
                {
                    definitions.Add(definition);
                }
            }

            return new DirectDefinitionSource(definitions);
        }
    }
}
