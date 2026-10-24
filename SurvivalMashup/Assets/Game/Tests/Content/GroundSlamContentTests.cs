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
    /// Targets path. The attack plays the existing Combo02_3 hammer-slam clip through its own Attack_GroundSlam asset: one
/// narrow window on the fist's landing frame, whose HV_GroundSlam area (r 3.3 m) is centred on the player.
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
        private const string SecondWindId = "ability.second_wind";
        private const float BufferWindow = 0.18f;

        private static readonly DefinitionId Activation = new DefinitionId(ActivationId);
        private static readonly DefinitionId Impact = new DefinitionId(ImpactId);

        private RuntimeServices _services;

        // Read from the authored asset (Ability_GroundSlam → Cooldown Seconds) so designers can tune it freely; the
        // tests prove the contract (committed at press, ticks down, survives save) for whatever value is authored.
        private float _slamCooldown;
        private ComboDefinition _slamAttack;
        private readonly List<AbilityActivationFailed> _failures = new List<AbilityActivationFailed>();
        private readonly List<AbilityCooldownStarted> _cooldownsStarted = new List<AbilityCooldownStarted>();

        [SetUp]
        public void SetUp()
        {
            _services = Boot();
            _slamCooldown = _services.DataRegistry.Get<AbilityDefinition>(Activation).CooldownSeconds;
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
            Assert.Greater(activation.CooldownSeconds, 0f,
                "A real cooldown; the exact value is a designer's tuning choice (the tests tick fractions of it).");
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
        public void SlamAttack_IsOneStep_OnTheHammerSlamClip_WithADedicatedImpactWindow()
        {
            Assert.AreEqual(1, _slamAttack.StepCount);
            ComboStep step = _slamAttack.Steps[0];
            Assert.AreEqual(ImpactId, step.AttackAbilityId);
            Assert.AreEqual("Attack_GroundSlam", step.Attack.name);
            Assert.AreEqual(Combo02_3().Clip, step.Attack.Clip, "The slam plays the existing Combo02_3 hammer-slam clip.");
            Assert.IsTrue(string.IsNullOrEmpty(step.Attack.AbilityId), "The attack defers to the step's impact ability.");
            Assert.AreEqual(1, step.Attack.Windows.Count);

            HitWindow slam = step.Attack.Windows[0];
            Assert.IsTrue(string.IsNullOrEmpty(slam.AbilityId), "The window defers to the step's impact ability.");
            Assert.AreEqual(StrikeLimbMask.None, slam.Limbs, "Area only: no limb sweep can land before the shockwave.");
        }

        [Test]
        public void SlamWindow_IsTheFistLandingFrame_NotTheWindUp()
        {
            // Measured in play (nt): the fist is still raised at 0.379, mid-drop at 0.388, on the ground by 0.396.
            HitWindow slam = _slamAttack.Steps[0].Attack.Windows[0];
            Assert.GreaterOrEqual(slam.Start, 0.388f, "The window must not open while the fist is still in the air.");
            Assert.LessOrEqual(slam.End, 0.41f, "A narrow impact window: damage, hit effects, and the burst share one frame.");
            Assert.Greater(slam.End, slam.Start);
        }

        [Test]
        public void SlamArea_IsCentredOnThePlayer_WithOneAndAHalfTimesTheShockwaveRadius()
        {
            HitWindow slam = _slamAttack.Steps[0].Attack.Windows[0];
            Assert.IsNotNull(slam.Area);
            Assert.AreEqual("HV_GroundSlam", slam.Area.name);
            Assert.AreEqual(HitAnchorSpace.Owner, slam.AreaSpace, "Centred on the player (above its root bone), not on a hand.");
            Assert.AreEqual(HitShape.Sphere, slam.Area.Volume.Shape, "Omnidirectional.");
            Assert.IsTrue(slam.Area.Volume.MultiTarget);

            HitVolumeAsset shockwave = AssetDatabase.LoadAssetAtPath<HitVolumeAsset>("Assets/Game/Content/HitVolumes/HV_GroundShockwave.asset");
            Assert.AreEqual(shockwave.Volume.Radius * 1.5f, slam.Area.Volume.Radius, Tolerance);
        }

        [Test]
        public void SharedCombo02_3Attack_IsUnchanged_ForTheMeleeAndAerialCombos()
        {
            HitWindow hammer = Combo02_3().Windows[0];
            Assert.AreEqual("Hammer slam", hammer.Id);
            Assert.AreEqual("HV_GroundShockwave", hammer.Area.name);
            Assert.AreEqual(2.2f, hammer.Area.Volume.Radius, Tolerance);
            Assert.AreEqual(HitAnchorSpace.HumanoidBone, hammer.AreaSpace, "Existing windows keep their bone-anchored area.");
        }

        [Test]
        public void EveryAbilityAttackStep_WindUpFallback_EndsAfterItsStrikes()
        {
            // The step's wind-up timer is only a fallback for the clip's own timing. If it fires before the attack's last
            // window closes, the sequencer counts contact early and a following step can chain before that window is swept
            // — a multi-step ability would silently skip a pulse. Ability attacks follow the authoring convention: the
            // fallback ends after the strikes (strikes done + 0.05 s).
            string[] guids = AssetDatabase.FindAssets("Combo_Ability_ t:ComboDefinition", new[] { DefinitionsFolder });
            Assert.Greater(guids.Length, 0);
            foreach (string guid in guids)
            {
                var combo = AssetDatabase.LoadAssetAtPath<ComboDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                for (int i = 0; i < combo.StepCount; i++)
                {
                    ComboStep step = combo.Steps[i];
                    if (step.Attack == null || step.Attack.Clip == null) continue;
                    float strikesDone = step.Attack.LastContactEnd * step.Attack.Clip.length;
                    Assert.GreaterOrEqual(step.WindUpDuration, strikesDone,
                        $"{combo.name} step {i}: wind-up fallback {step.WindUpDuration:0.000}s fires before its strikes end ({strikesDone:0.000}s).");
                }
            }
        }

        [Test]
        public void AreaBurst_IsAuthoredOnTheSlamWindow_NotSharedByEveryAbility()
        {
            HitWindow slam = _slamAttack.Steps[0].Attack.Windows[0];
            Assert.IsNotNull(slam.AreaEffect, "Each area window brings its own burst; the slam's lives on its window.");
            Assert.AreEqual("ToonBodySlam", slam.AreaEffect.name);
            Assert.Greater(slam.AreaEffectRadius, 0f);
            Assert.IsNull(Combo02_3().Windows[0].AreaEffect, "Existing windows author no burst, so the melee combos are unchanged.");
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
            Assert.AreEqual(_slamCooldown, performer.CooldownAtStart, Tolerance,
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
            Assert.AreEqual(_slamCooldown, abilities.GetAbility(Activation).CooldownRemaining, Tolerance);
        }

        [Test]
        public void ImmediateRepeat_IsRefusedOnCooldown_WithoutASecondSlam_OrCooldownReset()
        {
            AbilitySet abilities = Spawn(PlayerId).Get<AbilitySet>();
            var performer = new FakePerformer(abilities, Activation);
            AbilitySlotActivation slot = NewSlot();
            slot.Press(abilities, performer);
            abilities.Tick(_slamCooldown * 0.25f);

            Assert.AreEqual(AbilitySlotPressResult.Refused, slot.Press(abilities, performer));
            Assert.AreEqual(AbilityActivationResult.OnCooldown, slot.LastResult);
            Assert.AreEqual(1, performer.Started, "No second slam plays.");
            Assert.IsFalse(slot.IsBuffered, "A press on cooldown is refused outright, not buffered.");
            Assert.AreEqual(_slamCooldown * 0.75f, abilities.GetAbility(Activation).CooldownRemaining, Tolerance,
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
            abilities.Tick(_slamCooldown);

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

        // ------------------------------------------------------------------ attack-less abilities (scaling beyond the slam)

        [Test]
        public void AttackLessSlot_RunsTheActivationsOwnEffects_AndCommitsItsCooldown()
        {
            // Second Wind (Self heal, 10 s) is the real attack-less ability: a slot with no attack is the whole ability.
            GameplayObject player = Spawn(PlayerId);
            AbilitySet abilities = player.Get<AbilitySet>();
            abilities.Grant(_services.DataRegistry.Get<AbilityDefinition>(new DefinitionId(SecondWindId)));
            Health(player).Consume(30f);
            float before = Health(player).Current;
            var slot = new AbilitySlotActivation(new DefinitionId(SecondWindId), null, BufferWindow);

            Assert.AreEqual(AbilitySlotPressResult.Activated, slot.Press(abilities, performer: null));
            Assert.AreEqual(before + 25f, Health(player).Current, Tolerance, "The activation's own Fx_Heal_SecondWind ran.");
            Assert.AreEqual(10f, abilities.GetAbility(new DefinitionId(SecondWindId)).CooldownRemaining, Tolerance);

            Assert.AreEqual(AbilitySlotPressResult.Refused, slot.Press(abilities, performer: null));
            Assert.AreEqual(AbilityActivationResult.OnCooldown, slot.LastResult);
            Assert.AreEqual(before + 25f, Health(player).Current, Tolerance, "A refused press heals nothing.");
        }

        [Test]
        public void AttackLessSlot_IsNotBufferedBehindABusyCharacter()
        {
            GameplayObject player = Spawn(PlayerId);
            AbilitySet abilities = player.Get<AbilitySet>();
            abilities.Grant(_services.DataRegistry.Get<AbilityDefinition>(new DefinitionId(SecondWindId)));
            var busy = new FakePerformer(abilities, Activation) { CanStart = false };
            var slot = new AbilitySlotActivation(new DefinitionId(SecondWindId), null, BufferWindow);

            Assert.AreEqual(AbilitySlotPressResult.Activated, slot.Press(abilities, busy),
                "No animation to wait for: a heal mid-swing or mid-roll goes off at once.");
            Assert.AreEqual(0, busy.Started, "An attack-less ability never asks the performer to play anything.");
        }

        [Test]
        public void AttackSlot_WithoutAPerformer_IsRefused_WithoutCommitting()
        {
            AbilitySet abilities = Spawn(PlayerId).Get<AbilitySet>();
            Assert.AreEqual(AbilitySlotPressResult.Refused, NewSlot().Press(abilities, performer: null));
            Assert.IsFalse(abilities.GetAbility(Activation).IsOnCooldown);
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
            abilities.Tick(_slamCooldown * 0.5f);

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
            Assert.AreEqual(_slamCooldown * 0.5f, restoredPlayer.Get<AbilitySet>().GetAbility(Activation).CooldownRemaining, Tolerance);
        }

        // ------------------------------------------------------------------ helpers

        private static AttackDefinition Combo02_3() =>
            AssetDatabase.LoadAssetAtPath<AttackDefinition>("Assets/Game/Content/Attacks/Attack_Fighter_Combo02_3.asset");

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
