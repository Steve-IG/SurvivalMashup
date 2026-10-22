using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Boot;
using ToyChest.Framework.Data;
using ToyChest.Framework.Objects;
using ToyChest.Gameplay.Combat;
using ToyChest.Gameplay.HitDetection;
using ToyChest.Gameplay.Objects;
using ToyChest.Gameplay.Player;
using ToyChest.Systems.Abilities;
using ToyChest.Systems.GameplayEffects;
using ToyChest.Systems.Resources;
using ToyChest.Systems.Save;
using ToyChest.Tests.Events;
using UnityEditor;
using Object = UnityEngine.Object;

namespace ToyChest.Tests.Content
{
    /// <summary>
    /// Review Group B — Ground Slam, the first player-activated ability, verified against the real authored content.
    /// Two definitions compose it: the Self-mode activation ability (<c>ability.ground_slam</c>) owns validation and the
    /// cooldown and is committed at press by <see cref="AbilitySlotActivation"/>; the Provided impact ability
    /// (<c>ability.ground_slam_impact</c>) is what the slam's hit window applies to every target through the Multiple
    /// Targets path. The attack itself is the existing Combo02_3 "Hammer slam" window and its HV_GroundShockwave area.
    /// The performer seam (<see cref="IAbilityAttackPerformer"/>, PlayerCombat in play) is faked here, so these tests
    /// prove the activation and cooldown contract; play-mode behaviour is covered by the VerifyGroundSlam probe.
    /// </summary>
    public sealed class GroundSlamContentTests
    {
        private const float Tolerance = 1e-4f;
        private const string DefinitionsFolder = "Assets/Game/Content/Definitions";
        private const string SlamAttackPath = DefinitionsFolder + "/Combo_Ability_GroundSlam.asset";

        private const string PlayerId = "object.player";
        private const string GruntId = "object.grunt";
        private const string HealthId = "resource.health";
        private const string ActivationId = "ability.ground_slam";
        private const string ImpactId = "ability.ground_slam_impact";
        private const string StrikeId = "ability.player_strike";
        private const float SlamCooldown = 6f;
        private const float BufferWindow = 0.18f;

        private static readonly DefinitionId Activation = new DefinitionId(ActivationId);
        private static readonly DefinitionId Impact = new DefinitionId(ImpactId);

        private RuntimeServices _services;
        private ComboDefinition _slamAttack;
        private readonly List<AbilityActivationFailed> _failures = new List<AbilityActivationFailed>();
        private readonly List<AbilityCooldownStarted> _cooldownsStarted = new List<AbilityCooldownStarted>();

        [SetUp]
        public void SetUp()
        {
            _services = Boot();
            _slamAttack = AssetDatabase.LoadAssetAtPath<ComboDefinition>(SlamAttackPath);
            Assert.IsNotNull(_slamAttack, $"Missing {SlamAttackPath}.");
            _failures.Clear();
            _cooldownsStarted.Clear();
            _services.EventBus.Subscribe<AbilityActivationFailed>(e => _failures.Add(e));
            _services.EventBus.Subscribe<AbilityCooldownStarted>(e => _cooldownsStarted.Add(e));
        }

        // ------------------------------------------------------------------ authored content

        [Test]
        public void ActivationAbility_IsSelfMode_WithAuthoredCooldown_AndNoCostOrEffects()
        {
            AbilityDefinition activation = _services.DataRegistry.Get<AbilityDefinition>(Activation);
            Assert.AreEqual(AbilityTargetMode.Self, activation.TargetMode);
            Assert.AreEqual(SlamCooldown, activation.CooldownSeconds, Tolerance);
            Assert.AreEqual(0, activation.Costs.Count, "Ground Slam has no resource cost (no stamina).");
            Assert.AreEqual(0, activation.Effects.Count, "The activation only gates and commits; the impact ability deals damage.");
        }

        [Test]
        public void ImpactAbility_IsProvided_ZeroCooldown_DealingOneAndAHalfPunches()
        {
            AbilityDefinition impact = _services.DataRegistry.Get<AbilityDefinition>(Impact);
            AbilityDefinition strike = _services.DataRegistry.Get<AbilityDefinition>(new DefinitionId(StrikeId));
            Assert.AreEqual(AbilityTargetMode.Provided, impact.TargetMode);
            Assert.AreEqual(0f, impact.CooldownSeconds, "The cooldown lives on the activation, never the impact.");

            Assert.IsInstanceOf<DamageEffect>(impact.Effects[0]);
            Assert.IsInstanceOf<DamageEffect>(strike.Effects[0]);
            Assert.AreEqual(AuthoredAmount(strike.Effects[0]) * 1.5f, AuthoredAmount(impact.Effects[0]), Tolerance);
        }

        [Test]
        public void Player_IsGrantedBothDefinitions_ByItsObjectDefinition()
        {
            AbilitySet abilities = Spawn(PlayerId).Get<AbilitySet>();
            Assert.IsTrue(abilities.Has(Activation));
            Assert.IsTrue(abilities.Has(Impact));
        }

        [Test]
        public void SlamAttack_IsOneStep_ReusingTheHammerSlamShockwave_AndAppliesTheImpactAbility()
        {
            Assert.AreEqual(1, _slamAttack.StepCount);
            ComboStep step = _slamAttack.Steps[0];
            Assert.AreEqual(ImpactId, step.AttackAbilityId);
            Assert.IsNotNull(step.Attack);
            Assert.AreEqual("Attack_Fighter_Combo02_3", step.Attack.name);
            Assert.IsTrue(string.IsNullOrEmpty(step.Attack.AbilityId), "The attack defers to the step's impact ability.");

            HitWindow slam = step.Attack.Windows[0];
            Assert.IsTrue(string.IsNullOrEmpty(slam.AbilityId), "The window defers to the step's impact ability.");
            Assert.IsNotNull(slam.Area, "The slam window carries an area.");
            Assert.AreEqual("HV_GroundShockwave", slam.Area.name);
            Assert.IsTrue(slam.Area.Volume.MultiTarget);
        }

        // ------------------------------------------------------------------ activation and cooldown

        [Test]
        public void Press_CommitsCooldown_BeforeTheAttackStarts()
        {
            AbilitySet abilities = Spawn(PlayerId).Get<AbilitySet>();
            var performer = new FakePerformer(abilities, Activation);
            AbilitySlotActivation slot = NewSlot();

            Assert.AreEqual(AbilitySlotPressResult.Activated, slot.Press(abilities, performer));
            Assert.AreEqual(1, performer.Started);
            Assert.AreEqual(SlamCooldown, performer.CooldownAtStart, Tolerance,
                "The cooldown is already running when the attack starts — before any hit detection.");
            Assert.AreEqual(1, _cooldownsStarted.Count);
            Assert.AreEqual(Activation, _cooldownsStarted[0].Ability);
        }

        [Test]
        public void Whiff_WithNoTargets_StillConsumesTheCooldown()
        {
            AbilitySet abilities = Spawn(PlayerId).Get<AbilitySet>();
            AbilitySlotActivation slot = NewSlot();
            slot.Press(abilities, new FakePerformer(abilities, Activation));

            // The slam window found nothing: the impact ability is never activated against an empty set...
            Assert.AreEqual(AbilityActivationResult.InvalidTarget, abilities.TryActivate(Impact, new List<EffectTarget>()));

            // ...and the cooldown committed at press stands regardless.
            Assert.AreEqual(SlamCooldown, abilities.GetAbility(Activation).CooldownRemaining, Tolerance);
        }

        [Test]
        public void ImmediateRepeat_IsRefusedOnCooldown_WithoutASecondSlam_OrCooldownReset()
        {
            AbilitySet abilities = Spawn(PlayerId).Get<AbilitySet>();
            var performer = new FakePerformer(abilities, Activation);
            AbilitySlotActivation slot = NewSlot();
            slot.Press(abilities, performer);
            abilities.Tick(1f);

            Assert.AreEqual(AbilitySlotPressResult.Refused, slot.Press(abilities, performer));
            Assert.AreEqual(AbilityActivationResult.OnCooldown, slot.LastResult);
            Assert.AreEqual(1, performer.Started, "No second slam plays.");
            Assert.IsFalse(slot.IsBuffered, "A press on cooldown is refused outright, not buffered.");
            Assert.AreEqual(SlamCooldown - 1f, abilities.GetAbility(Activation).CooldownRemaining, Tolerance,
                "A refused press never restarts the cooldown.");
            Assert.AreEqual(1, _failures.Count);
            Assert.AreEqual(AbilityActivationResult.OnCooldown, _failures[0].Reason);
        }

        [Test]
        public void AfterTheCooldown_TheSlamActivatesAgain()
        {
            AbilitySet abilities = Spawn(PlayerId).Get<AbilitySet>();
            var performer = new FakePerformer(abilities, Activation);
            AbilitySlotActivation slot = NewSlot();
            slot.Press(abilities, performer);
            abilities.Tick(SlamCooldown);

            Assert.AreEqual(AbilitySlotPressResult.Activated, slot.Press(abilities, performer));
            Assert.AreEqual(2, performer.Started);
        }

        [Test]
        public void UngrantedActivation_IsRefused_WithoutCommittingOrPlaying()
        {
            AbilitySet abilities = Spawn(PlayerId).Get<AbilitySet>();
            Assert.IsTrue(abilities.Revoke(Activation));
            var performer = new FakePerformer(abilities, Activation);
            AbilitySlotActivation slot = NewSlot();

            Assert.AreEqual(AbilitySlotPressResult.Refused, slot.Press(abilities, performer));
            Assert.AreEqual(AbilityActivationResult.NotGranted, slot.LastResult);
            Assert.AreEqual(0, performer.Started);
            Assert.AreEqual(0, _cooldownsStarted.Count);
            Assert.AreEqual(1, _failures.Count, "The refusal is published as AbilityActivationFailed.");
        }

        [Test]
        public void BusyCharacter_BuffersThePress_WithoutCommitting_ThenFiresWhenFree()
        {
            AbilitySet abilities = Spawn(PlayerId).Get<AbilitySet>();
            var performer = new FakePerformer(abilities, Activation) { CanStart = false };
            AbilitySlotActivation slot = NewSlot();

            Assert.AreEqual(AbilitySlotPressResult.Buffered, slot.Press(abilities, performer));
            Assert.IsFalse(abilities.GetAbility(Activation).IsOnCooldown, "A buffered press commits nothing.");

            performer.CanStart = true;
            Assert.AreEqual(AbilitySlotPressResult.Activated, slot.Tick(0.05f, abilities, performer));
            Assert.AreEqual(1, performer.Started);
            Assert.IsTrue(abilities.GetAbility(Activation).IsOnCooldown);
        }

        [Test]
        public void BufferedPress_ThatExpires_NeverConsumesTheCooldown()
        {
            AbilitySet abilities = Spawn(PlayerId).Get<AbilitySet>();
            var performer = new FakePerformer(abilities, Activation) { CanStart = false };
            AbilitySlotActivation slot = NewSlot();
            slot.Press(abilities, performer);

            AbilitySlotPressResult result = AbilitySlotPressResult.Buffered;
            for (int i = 0; i < 20 && result == AbilitySlotPressResult.Buffered; i++)
            {
                result = slot.Tick(0.05f, abilities, performer);
            }

            Assert.AreEqual(AbilitySlotPressResult.Expired, result);
            Assert.AreEqual(0, performer.Started);
            Assert.IsFalse(abilities.GetAbility(Activation).IsOnCooldown);
            Assert.AreEqual(0, _cooldownsStarted.Count);
            Assert.AreEqual(AbilitySlotPressResult.None, slot.Tick(0.05f, abilities, performer));
        }

        // ------------------------------------------------------------------ damage and melee isolation

        [Test]
        public void ImpactAbility_DamagesEveryTargetHit_InOneActivation()
        {
            GameplayObject player = Spawn(PlayerId);
            var grunts = new[] { Spawn(GruntId), Spawn(GruntId), Spawn(GruntId) };
            var targets = new List<EffectTarget>();
            var before = new float[grunts.Length];
            for (int i = 0; i < grunts.Length; i++)
            {
                before[i] = Health(grunts[i]).Current;
                targets.Add(EffectTarget.From(grunts[i]));
            }

            Assert.AreEqual(AbilityActivationResult.Activated, player.Get<AbilitySet>().TryActivate(Impact, targets));
            for (int i = 0; i < grunts.Length; i++)
            {
                Assert.AreEqual(before[i] - 30f, Health(grunts[i]).Current, Tolerance, $"Grunt {i} takes the slam's damage once.");
            }

            Assert.AreEqual(0, _cooldownsStarted.Count, "Applying the impact never starts a cooldown.");
        }

        [Test]
        public void SlamCooldown_LeavesOrdinaryMeleeUntouched()
        {
            GameplayObject player = Spawn(PlayerId);
            AbilitySet abilities = player.Get<AbilitySet>();
            NewSlot().Press(abilities, new FakePerformer(abilities, Activation));

            var strike = new DefinitionId(StrikeId);
            Assert.AreEqual(0f, abilities.GetAbility(strike).Definition.CooldownSeconds, "Melee keeps its 0 s cooldown.");
            Assert.IsFalse(abilities.GetAbility(strike).IsOnCooldown);
            Assert.AreEqual(AbilityActivationResult.Activated, abilities.TryActivate(strike, EffectTarget.From(Spawn(GruntId))),
                "A punch still lands while the slam cools down.");
        }

        [Test]
        public void SlamCooldown_SurvivesSaveAndReload()
        {
            AbilitySet abilities = Spawn(PlayerId).Get<AbilitySet>();
            NewSlot().Press(abilities, new FakePerformer(abilities, Activation));
            abilities.Tick(2f);

            SaveManager save = _services.SaveManager;
            SaveData reloaded = save.FromJson(save.ToJson(save.Capture(_services.Objects)));
            RuntimeServices reload = Boot();
            IReadOnlyList<GameplayObject> restored = reload.SaveManager.Restore(reloaded, reload.Factory, reload.DataRegistry);

            GameplayObject restoredPlayer = null;
            foreach (GameplayObject candidate in restored)
            {
                if (candidate.DefinitionId == new DefinitionId(PlayerId))
                {
                    restoredPlayer = candidate;
                }
            }

            Assert.IsNotNull(restoredPlayer);
            Assert.AreEqual(SlamCooldown - 2f, restoredPlayer.Get<AbilitySet>().GetAbility(Activation).CooldownRemaining, Tolerance);
        }

        // ------------------------------------------------------------------ helpers

        private AbilitySlotActivation NewSlot() => new AbilitySlotActivation(Activation, _slamAttack, BufferWindow);

        private GameplayObject Spawn(string id)
        {
            GameplayObjectDefinition definition = _services.DataRegistry.Get<GameplayObjectDefinition>(new DefinitionId(id));
            GameplayObject obj = _services.Factory.Create(definition);
            obj.Activate();
            return obj;
        }

        // DamageEffect exposes no amount accessor; read the authored value rather than widen the effect's API for a test.
        private static float AuthoredAmount(GameplayEffectDefinition effect) =>
            new SerializedObject(effect).FindProperty("_amount").floatValue;

        private static ResourceValue Health(GameplayObject obj) => obj.Get<ResourceSet>().GetResource(new DefinitionId(HealthId));

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

        // Stands in for PlayerCombat: records each start and the activation's cooldown at that moment.
        private sealed class FakePerformer : IAbilityAttackPerformer
        {
            private readonly AbilitySet _abilities;
            private readonly DefinitionId _activation;

            public FakePerformer(AbilitySet abilities, DefinitionId activation)
            {
                _abilities = abilities;
                _activation = activation;
            }

            public bool CanStart { get; set; } = true;

            public int Started { get; private set; }

            public float CooldownAtStart { get; private set; } = -1f;

            public bool CanStartAbilityAttack(ComboDefinition attack) => CanStart;

            public bool StartAbilityAttack(ComboDefinition attack)
            {
                Started++;
                CooldownAtStart = _abilities.GetAbility(_activation).CooldownRemaining;
                return true;
            }
        }
    }
}
