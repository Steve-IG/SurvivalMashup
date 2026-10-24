using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ToyChest.Boot;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Combat;
using ToyChest.Gameplay.Enemy;
using ToyChest.Gameplay.Objects;
using ToyChest.Systems.Attributes;
using ToyChest.Systems.StatusEffects;
using ToyChest.Tests.Events;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ToyChest.Tests.Content
{
    /// <summary>
    /// Hit reactions against the real authored content: the player's Impact Power, each body's Stability, the attack
    /// windows' Impact values, and the enemies' authored reaction rules combine into the intended ladder — a punch
    /// flinches a Grunt, the combo finisher staggers it, Ground Slam knocks it back; the Grunt Brute shrugs off punches
    /// and finishers and only flinches under Ground Slam. Play-mode behaviour (animations, interrupts, knockback
    /// motion) is covered by the VerifyHitReactions probe.
    /// </summary>
    public sealed class HitReactionContentTests
    {
        private const string DefinitionsFolder = "Assets/Game/Content/Definitions";
        private const string Attacks = "Assets/Game/Content/Attacks/";
        private static readonly DefinitionId Stability = new DefinitionId("attribute.stability");
        private static readonly DefinitionId ImpactPower = new DefinitionId("attribute.impact_power");

        private RuntimeServices _services;
        private HitReactionRules _gruntRules;
        private HitReactionRules _bruteRules;

        [SetUp]
        public void SetUp()
        {
            _services = new RuntimeBootstrap().Run(new BootstrapConfiguration(new RecordingLogger(), new[] { LoadAuthoredDefinitions() }));
            _gruntRules = Rules("Assets/Game/Content/Prefabs/Grunt.prefab");
            _bruteRules = Rules("Assets/Game/Content/Prefabs/GruntBrute.prefab");
        }

        [Test]
        public void Bodies_HaveAuthoredStability_AndThePlayerHasImpactPower()
        {
            Assert.AreEqual(10f, Spawn("object.grunt").Get<AttributeSet>().GetValue(Stability));
            Assert.AreEqual(10f, Spawn("object.training_dummy").Get<AttributeSet>().GetValue(Stability));
            Assert.AreEqual(40f, Brute().Get<AttributeSet>().GetValue(Stability), "The elite status adds +30: a heavy body.");
            Assert.AreEqual(1f, Spawn("object.player").Get<AttributeSet>().GetValue(ImpactPower));
        }

        [Test]
        public void Grunt_Punch_Flinches_Finisher_Staggers_GroundSlam_KnocksBack()
        {
            float stability = Spawn("object.grunt").Get<AttributeSet>().GetValue(Stability);
            Assert.AreEqual(HitReactionLevel.Flinch, Resolve(FirstImpact("Attack_Player_Attack"), stability, _gruntRules));
            Assert.AreEqual(HitReactionLevel.Flinch, Resolve(FirstImpact("Attack_Player_Combo2"), stability, _gruntRules));
            Assert.AreEqual(HitReactionLevel.Stagger, Resolve(FirstImpact("Attack_Player_Combo3"), stability, _gruntRules));
            Assert.AreEqual(HitReactionLevel.Knockback, Resolve(FirstImpact("Attack_GroundSlam"), stability, _gruntRules));
        }

        [Test]
        public void Brute_ShrugsOffPunchesAndFinishers_ButGroundSlamFlinchesIt()
        {
            float stability = Brute().Get<AttributeSet>().GetValue(Stability);
            Assert.AreEqual(HitReactionLevel.None, Resolve(FirstImpact("Attack_Player_Attack"), stability, _bruteRules));
            Assert.AreEqual(HitReactionLevel.None, Resolve(FirstImpact("Attack_Player_Combo3"), stability, _bruteRules));
            Assert.AreEqual(HitReactionLevel.Flinch, Resolve(FirstImpact("Attack_GroundSlam"), stability, _bruteRules));
        }

        [Test]
        public void Grunt_FlinchInterrupt_IsAShortWindowAtTheStartOfItsWindUp()
        {
            var combatant = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Content/Prefabs/Grunt.prefab").GetComponent<EnemyCombatant>());
            float window = combatant.FindProperty("_interruptWindowSeconds").floatValue;
            float windUp = combatant.FindProperty("_windUpSeconds").floatValue;
            Assert.Greater(window, 0f);
            Assert.LessOrEqual(window, windUp * 0.6f, "Only the start of the swing: once committed, a punch no longer stops it.");
        }

        [Test]
        public void GruntTuning_SlowerAndWeaker()
        {
            Assert.AreEqual(2.6f, Spawn("object.grunt").Get<AttributeSet>().GetValue(new DefinitionId("attribute.enemy_speed")), 1e-4f);
            float claw = new SerializedObject(AssetDatabase.LoadAssetAtPath<Object>(DefinitionsFolder + "/Fx_Damage_EnemyStrike.asset")).FindProperty("_amount").floatValue;
            Assert.AreEqual(5f, claw, 1e-4f);
            Assert.Less(Brute().Get<AttributeSet>().GetValue(new DefinitionId("attribute.enemy_speed")), 2.6f, "The Brute stays slower than a Grunt.");
        }

        // ------------------------------------------------------------------ helpers

        private static HitReactionLevel Resolve(float impact, float stability, HitReactionRules rules) =>
            HitReactionResolver.Resolve(impact * 1f, stability, rules).Level; // player Impact Power is 1

        private static float FirstImpact(string attack) =>
            AssetDatabase.LoadAssetAtPath<AttackDefinition>(Attacks + attack + ".asset").Windows[0].Impact;

        // The rules the prefab's EnemyCombatant actually carries (defaults included).
        private static HitReactionRules Rules(string prefabPath)
        {
            var combatant = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).GetComponent<EnemyCombatant>();
            FieldInfo field = typeof(EnemyCombatant).GetField("_hitReactionRules", BindingFlags.NonPublic | BindingFlags.Instance);
            return (HitReactionRules)field.GetValue(combatant);
        }

        private GameplayObject Brute()
        {
            GameplayObject brute = Spawn("object.grunt_brute");
            brute.Get<StatusEffectSet>().Apply(_services.DataRegistry.Get<StatusEffectDefinition>(new DefinitionId("status.elite_brute")));
            return brute;
        }

        private GameplayObject Spawn(string id)
        {
            GameplayObject obj = _services.Factory.Create(_services.DataRegistry.Get<GameplayObjectDefinition>(new DefinitionId(id)));
            obj.Activate();
            return obj;
        }

        private static DirectDefinitionSource LoadAuthoredDefinitions()
        {
            var definitions = new List<IDefinition>();
            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { DefinitionsFolder }))
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
