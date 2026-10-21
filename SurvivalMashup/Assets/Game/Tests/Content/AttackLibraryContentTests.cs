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
        public void ARollPressedMidRollRestartsTheRollClip()
        {
            // Without a self-transition a chained roll moves the body while the previous roll's get-up still plays.
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            AnimatorStateTransition entry = null;
            foreach (AnimatorStateTransition t in controller.layers[0].stateMachine.anyStateTransitions)
            {
                if (t.destinationState != null && t.destinationState.name == "Roll") entry = t;
            }

            Assert.IsNotNull(entry, "No Any State → Roll transition.");
            Assert.IsTrue(entry.canTransitionToSelf, "Any State → Roll must be able to restart the Roll state.");
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

        // RG5F follow-up: the first real weapon attack — a JC Stylized Weapons sword prop in the right hand, the blade's
        // own sockets as the Weapon2 strike shape, and a Frank RPG Dual humanoid sword clip imported like the library.
        [Test]
        public void SwordStrikesThroughTheBladeOfARealPropOnAFrankDualClip()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            StrikeShape blade = null;
            foreach (StrikeShape s in prefab.GetComponent<CharacterStrikeProfile>().AuthoredShapes) if (s.Limb == StrikeLimb.Weapon2) blade = s;
            Assert.IsNotNull(blade, "Weapon2 = the sword blade");
            Assert.AreEqual(StrikeShapeSource.Sockets, blade.Source);
            Assert.AreEqual("Sword_Prop", blade.StartSocket.parent.name);
            Assert.AreSame(blade.StartSocket.parent, blade.EndSocket.parent);
            StringAssert.Contains("Hand", blade.StartSocket.parent.parent.name, "the prop rides the right hand bone");
            Assert.AreEqual("Assets/JC_StylizedWeapons/Prefabs/SM_Sword_01.prefab",
                AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(blade.StartSocket.parent.gameObject)));
            float length = Vector3.Distance(blade.StartSocket.position, blade.EndSocket.position);
            Assert.That(length, Is.InRange(0.7f, 0.9f), "hilt → tip spans the blade");

            ComboDefinition combo = LoadCombo("Combo_Test_Sword");
            Assert.IsNotNull(combo);
            AttackDefinition attack = combo.Steps[0].Attack;
            Assert.AreEqual(1, attack.Windows.Count);
            Assert.AreEqual(StrikeLimbMask.Weapon2, attack.Windows[0].Limbs, "the blade strikes, not the fist");
            Assert.IsTrue(attack.Windows[0].IsVerified, "verified by contact sheet + live hit");

            string clipPath = AssetDatabase.GetAssetPath(attack.Clip);
            StringAssert.Contains("Frank_RPG_Dual_", clipPath);
            var importer = (ModelImporter)AssetImporter.GetAtPath(clipPath);
            Assert.AreEqual(ModelImporterAnimationType.Human, importer.animationType);
            foreach (ModelImporterClipAnimation c in importer.clipAnimations)
            {
                Assert.IsTrue(c.lockRootRotation && c.lockRootHeightY && !c.lockRootPositionXZ, $"{clipPath}: rotation + Y baked, XZ root motion");
            }
        }

        // Aerial combos, stage 1: every step flies on a ZeroHeight (_0H) clip whose height the controller supplies from
        // the attack's profile; the string is genuinely airborne whenever it chains, and only the finisher comes down,
        // landing on its slam. Heights are accumulated exactly as PlayerCombat applies them (per-step deltas).
        [Test]
        public void TheAirStringFliesOnZeroHeightClipsAndLandsOnlyOnTheFinisher()
        {
            ComboDefinition combo = LoadCombo("Combo_Test_AirString");
            Assert.IsNotNull(combo);
            Assert.AreEqual(4, combo.StepCount);
            float height = 0f;
            for (int i = 0; i < combo.StepCount; i++)
            {
                AttackDefinition attack = combo.Steps[i].Attack;
                Assert.IsTrue(attack.IsAerial, $"step {i + 1} carries a height profile");
                string clipPath = AssetDatabase.GetAssetPath(attack.Clip);
                StringAssert.EndsWith("_0H.FBX", clipPath, $"step {i + 1} uses a ZeroHeight clip");
                foreach (ModelImporterClipAnimation c in ((ModelImporter)AssetImporter.GetAtPath(clipPath)).clipAnimations)
                {
                    Assert.IsTrue(c.lockRootRotation && c.lockRootHeightY && c.keepOriginalPositionY && !c.lockRootPositionXZ,
                        $"{clipPath}: rotation + Y baked (Original), XZ root motion");
                }

                if (i < combo.StepCount - 1)
                {
                    height += attack.AirHeightDelta(0f, attack.LastContactEnd); // the chain point: last window closes
                    Assert.Greater(height, 1.0f, $"the string is airborne when step {i + 2} begins ({height:0.00}m)");
                }
                else
                {
                    HitWindow slam = null;
                    foreach (HitWindow w in attack.Windows) if (w.Area != null) slam = w;
                    Assert.IsNotNull(slam, "the finisher lands on an area slam");
                    float atSlam = height + attack.AirHeightDelta(0f, slam.Start);
                    // The dive comes down at ~15 m/s and the window opens a beat (0.04s) before touchdown, so "at the
                    // floor" is within a metre; the defect this guards against opened the slam 2.6m up, mid-dive.
                    Assert.Less(atSlam, 1.0f, $"the finisher's slam opens at the floor ({atSlam:0.00}m above it)");
                }
            }
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

        // RG5F regression: the pads were 0.1m floor slabs, and a CharacterController rests its skin width above the
        // floor, so the capsule only grazed each trigger by ~2cm and the physics step never reported the contact —
        // walking onto a pad did nothing. Each pad's trigger must reach well into the resting capsule (past the
        // capsule's rounded tip, i.e. at least one radius deep) over the pad's whole footprint.
        [Test]
        public void EveryLoadoutPadTriggerReachesIntoTheRestingPlayerCapsule()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/Game/Scenes/VerticalSlice.unity", UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                CharacterController player = null;
                var pads = new List<ComboLoadoutPad>();
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    pads.AddRange(root.GetComponentsInChildren<ComboLoadoutPad>(true));
                    var combat = root.GetComponentInChildren<PlayerCombat>(true);
                    if (combat != null) player = combat.GetComponent<CharacterController>();
                }

                Assert.IsNotNull(player, "the dojo Player has a CharacterController");
                Assert.AreEqual(7, pads.Count, "seven loadout pads (shipped, four RG5E library combos, sword, air string)");
                foreach (ComboLoadoutPad pad in pads)
                {
                    var box = pad.GetComponent<BoxCollider>();
                    Assert.IsNotNull(box, pad.name);
                    Assert.IsTrue(box.enabled && box.isTrigger && pad.gameObject.activeInHierarchy, pad.name);

                    Transform t = pad.transform;
                    float floor = t.position.y - t.lossyScale.y * 0.5f; // the slab sits on the floor
                    float capsuleBottom = floor + player.skinWidth;
                    float triggerBottom = t.TransformPoint(box.center - Vector3.up * box.size.y * 0.5f).y;
                    float triggerTop = t.TransformPoint(box.center + Vector3.up * box.size.y * 0.5f).y;
                    Assert.LessOrEqual(triggerBottom, capsuleBottom, $"{pad.name}: trigger starts below the capsule");
                    Assert.GreaterOrEqual(triggerTop - capsuleBottom, player.radius,
                        $"{pad.name}: trigger reaches only {triggerTop - capsuleBottom:0.000}m into the capsule");

                    Vector3 footprint = Vector3.Scale(box.size, t.lossyScale);
                    Assert.GreaterOrEqual(Mathf.Min(footprint.x, footprint.z), 1f, $"{pad.name}: footprint is walkable");
                }
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
