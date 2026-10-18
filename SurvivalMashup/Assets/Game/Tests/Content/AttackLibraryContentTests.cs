using System.Collections.Generic;
using NUnit.Framework;
using ToyChest.Editor;
using ToyChest.Gameplay.Combat;
using ToyChest.Gameplay.HitDetection;
using ToyChest.Gameplay.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ToyChest.Tests.Content
{
    /// <summary>
    /// Every authored combo — the shipped melee string and the RG5E attack library — validates against the real
    /// Player: each step's attack has a real clip, windows that can fire inside the commitment, limbs the Player's
    /// strike profile provides, an Animator state that plays that clip, is entered by its trigger and leaves on
    /// <c>!AttackCommitted</c>. Also pins the library's shape (distinct archetypes, Frank Fighter clips, the
    /// shockwave group hit, the weapon socket) and that every combo is reachable from a dojo loadout pad.
    /// Data validation only — animation feel is verified by play-mode probes and playtesting, not here.
    /// </summary>
    public sealed class AttackLibraryContentTests
    {
        private const string ControllerPath = "Assets/Game/Content/Animation/PlayerAnimator.controller";
        private const string PlayerPrefab = "Assets/Game/Content/Prefabs/Player.prefab";
        private static readonly string[] Library =
        {
            "Combo_Test_QuickStrikes", "Combo_Test_Sweeping", "Combo_Test_AerialHeavy", "Combo_Test_WeaponProxy",
        };

        private static ComboDefinition LoadCombo(string name) =>
            AssetDatabase.LoadAssetAtPath<ComboDefinition>($"Assets/Game/Content/Definitions/{name}.asset");

        private static HashSet<StrikeLimb> PlayerLimbs()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            return AttackValidation.AvailableLimbs(prefab.GetComponentInChildren<Animator>());
        }

        [Test]
        public void EveryComboValidatesAgainstThePlayerWithoutErrors()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            HashSet<StrikeLimb> limbs = PlayerLimbs();
            var errors = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:ComboDefinition"))
            {
                var combo = AssetDatabase.LoadAssetAtPath<ComboDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (AttackValidation.Issue issue in AttackValidation.ValidateCombo(combo, controller, limbs))
                {
                    if (issue.Severity == AttackValidation.Severity.Error) errors.Add($"{combo.name}: {issue.Message}");
                }
            }

            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void LibraryHasAtLeastThreeMultiStepCombosBuiltFromFrankFighterClips()
        {
            int multiStep = 0;
            foreach (string name in Library)
            {
                ComboDefinition combo = LoadCombo(name);
                Assert.IsNotNull(combo, name);
                if (combo.StepCount >= 2) multiStep++;
                foreach (ComboStep step in combo.Steps)
                {
                    Assert.IsNotNull(step.Attack, $"{name}: every library step is attack-asset driven");
                    string clipPath = AssetDatabase.GetAssetPath(step.Attack.Clip);
                    StringAssert.Contains("Frank_RPG_Fighter_", clipPath, $"{name}: {step.Attack.name}");
                    Assert.IsTrue(step.Attack.Clip.isHumanMotion, step.Attack.name);
                }
            }

            Assert.GreaterOrEqual(multiStep, 3);
        }

        [Test]
        public void LibraryClipsBakeRotationAndHeightSoThePreviewMatchesPlay()
        {
            // Root motion is applied as planar XZ only; a clip that leaves rotation/height as root motion would lose its
            // spin / leap in play while the authoring preview still showed them.
            foreach (string name in Library)
            {
                foreach (ComboStep step in LoadCombo(name).Steps)
                {
                    AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(step.Attack.Clip);
                    Assert.IsTrue(settings.loopBlendOrientation, $"{step.Attack.name}: bake root rotation into pose");
                    Assert.IsTrue(settings.loopBlendPositionY, $"{step.Attack.name}: bake root height into pose");
                    Assert.IsFalse(settings.loopBlendPositionXZ, $"{step.Attack.name}: XZ must stay root motion");
                }
            }
        }

        [Test]
        public void HeavyFinisherCarriesAMultiTargetShockwave()
        {
            ComboStep finisher = LoadCombo("Combo_Test_AerialHeavy").Steps[2];
            bool found = false;
            foreach (HitWindow w in finisher.Attack.Windows)
            {
                if (w.Area == null) continue;
                found = true;
                Assert.IsTrue(w.Area.Volume.MultiTarget);
                Assert.Greater(w.Area.Volume.MaxTargets, 1);
                Assert.AreEqual(0f, w.RehitInterval, "one hit per target per window");
            }

            Assert.IsTrue(found, "the slam must author a group-hit area");
        }

        [Test]
        public void WeaponProxyStrikesThroughAnAuthoredSocketShape()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            var profile = prefab.GetComponent<CharacterStrikeProfile>();
            Assert.IsNotNull(profile);
            StrikeShape weapon = null;
            foreach (StrikeShape s in profile.AuthoredShapes) if (s.Limb == StrikeLimb.Weapon1) weapon = s;
            Assert.IsNotNull(weapon);
            Assert.AreEqual(StrikeShapeSource.Sockets, weapon.Source);
            Assert.IsNotNull(weapon.StartSocket);
            Assert.IsNotNull(weapon.EndSocket);
            StringAssert.Contains("Hand", weapon.StartSocket.parent.name, "the sockets ride the right hand bone");
            Assert.AreSame(weapon.StartSocket.parent, weapon.EndSocket.parent);

            ComboStep step = LoadCombo("Combo_Test_WeaponProxy").Steps[0];
            Assert.AreEqual(StrikeLimbMask.Weapon1, step.Attack.Windows[0].Limbs);
        }

        [Test]
        public void EveryLibraryComboIsReachableFromADojoLoadoutPad()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/Game/Scenes/VerticalSlice.unity", UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                var equipped = new HashSet<ComboDefinition>();
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (ComboLoadoutPad pad in root.GetComponentsInChildren<ComboLoadoutPad>(true))
                    {
                        Assert.IsTrue(pad.GetComponent<Collider>().isTrigger, pad.name);
                        equipped.Add(pad.Combo);
                    }
                }

                foreach (string name in Library) Assert.IsTrue(equipped.Contains(LoadCombo(name)), name);
                Assert.IsTrue(equipped.Contains(LoadCombo("Combo_PlayerMelee")), "the shipped combo must be re-selectable");
            }
            finally
            {
                if (UnityEditor.SceneManagement.EditorSceneManager.sceneCount > 1)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }
}
