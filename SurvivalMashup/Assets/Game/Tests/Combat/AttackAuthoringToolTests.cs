using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ToyChest.Editor;
using ToyChest.Gameplay.Combat;
using ToyChest.Gameplay.HitDetection;
using UnityEditor;
using UnityEngine;

namespace ToyChest.Tests.Combat
{
    /// <summary>
    /// The Attack Authoring tool's deterministic logic: the validator catches attack data that could never work in
    /// play, and the window proposer's RG5E fixes hold on the real Frank Fighter clips — a one-two jab proposes two
    /// blows, a leap that lands into a crouch finds its touchdown, and a strike frame on an overhead smash picks the
    /// fast hand rather than the off arm sweeping for balance. Proposals are still suggestions; these tests pin the
    /// analysis, not the feel.
    /// </summary>
    public sealed class AttackAuthoringToolTests
    {
        private const string ClipFolder = "Assets/Frank_Fighter/Assets/Animations/FBX Animation/Root_Motion/Frank_RPG_Fighter_";
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _created) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private AttackDefinition MakeAttack(float commitEnd, float cancelFrom, params HitWindow[] windows)
        {
            var attack = ScriptableObject.CreateInstance<AttackDefinition>();
            _created.Add(attack);
            typeof(AttackDefinition).GetField("_windows", Flags).SetValue(attack, new List<HitWindow>(windows));
            typeof(AttackDefinition).GetField("_commitEnd", Flags).SetValue(attack, commitEnd);
            typeof(AttackDefinition).GetField("_cancelFrom", Flags).SetValue(attack, cancelFrom);
            typeof(AttackDefinition).GetField("_clip", Flags).SetValue(attack, LoadClip("Combo04_1"));
            return attack;
        }

        private static HitWindow Verified(HitWindow w)
        {
            typeof(HitWindow).GetField("_verified", Flags).SetValue(w, true);
            return w;
        }

        private static AnimationClip LoadClip(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipFolder}{name}.FBX");

        private static bool HasError(List<AttackValidation.Issue> issues, string fragment) =>
            issues.Exists(i => i.Severity == AttackValidation.Severity.Error && i.Message.Contains(fragment));

        // --- Validator ------------------------------------------------------------------------------------

        [Test]
        public void Validator_AcceptsAWellFormedVerifiedAttack()
        {
            AttackDefinition attack = MakeAttack(0.9f, 0f, Verified(new HitWindow("Jab", 0.2f, 0.4f, StrikeLimbMask.RightHand)));
            List<AttackValidation.Issue> issues = AttackValidation.ValidateAttack(attack, new[] { StrikeLimb.RightHand });
            Assert.IsEmpty(issues, string.Join("\n", issues));
        }

        [Test]
        public void Validator_FlagsWindowsThatCanNeverStrike()
        {
            AttackDefinition attack = MakeAttack(0.9f, 0f,
                new HitWindow("Backwards", 0.5f, 0.4f, StrikeLimbMask.RightHand),
                new HitWindow("Nothing", 0.2f, 0.3f, StrikeLimbMask.None));
            List<AttackValidation.Issue> issues = AttackValidation.ValidateAttack(attack);
            Assert.IsTrue(HasError(issues, "never opens"));
            Assert.IsTrue(HasError(issues, "no limbs and no area"));
        }

        [Test]
        public void Validator_FlagsCommitmentEndingMidStrike()
        {
            AttackDefinition attack = MakeAttack(0.3f, 0f, new HitWindow("Late", 0.2f, 0.5f, StrikeLimbMask.RightHand));
            Assert.IsTrue(HasError(AttackValidation.ValidateAttack(attack), "before the last window closes"));
        }

        [Test]
        public void Validator_FlagsALimbTheCharacterDoesNotHave()
        {
            AttackDefinition attack = MakeAttack(0.9f, 0f, new HitWindow("Blade", 0.2f, 0.4f, StrikeLimbMask.Weapon1));
            List<AttackValidation.Issue> issues = AttackValidation.ValidateAttack(attack, new[] { StrikeLimb.RightHand, StrikeLimb.LeftHand });
            Assert.IsTrue(HasError(issues, "Weapon1"));
        }

        [Test]
        public void Validator_ReportsSuggestedWindowsUntilVerified()
        {
            AttackDefinition attack = MakeAttack(0.9f, 0f, new HitWindow("Jab", 0.2f, 0.4f, StrikeLimbMask.RightHand));
            List<AttackValidation.Issue> issues = AttackValidation.ValidateAttack(attack);
            Assert.IsTrue(issues.Exists(i => i.Severity == AttackValidation.Severity.Warning && i.Message.Contains("unverified")));
        }

        [Test]
        public void TimingSummary_KeepsTheTimingConceptsSeparate()
        {
            AttackDefinition attack = MakeAttack(0.8f, 0.6f, new HitWindow("Jab", 0.2f, 0.4f, StrikeLimbMask.RightHand));
            string summary = AttackValidation.TimingSummary(attack);
            foreach (string concept in new[] { "Startup", "Contact 'Jab'", "Chain point", "Combo input", "Cancel point", "Recovery", "Commitment end", "Clip end" })
            {
                StringAssert.Contains(concept, summary);
            }

            StringAssert.Contains("? suggested", summary);
        }

        // --- Proposer regressions (real clips) ---------------------------------------------------------------

        [Test]
        public void Proposer_FindsTouchdownOfALeapThatLandsAboveItsStartHeight()
        {
            AnimationClip clip = LoadClip("Combo06_2"); // flip that settles ~0.05–0.10m above its start height
            Assert.IsTrue(AttackWindowProposer.TryMeasureLeap(clip, out _, out float landing, out float apex));
            Assert.Greater(apex, 1.5f);
            Assert.Less(landing, clip.length - 0.2f, "touchdown must not default to the clip end");
        }

        [Test]
        public void Proposer_ProposesAOneTwoJabAsTwoBlows()
        {
            AttackWindowProposer.Proposal p = ProposeOnPlayer("Combo04_1", -1f);
            Assert.AreEqual(2, p.Windows.Count, p.Report);
            Assert.AreEqual(StrikeLimbMask.RightHand, p.Windows[0].Limbs, p.Report);
            Assert.AreEqual(StrikeLimbMask.LeftHand, p.Windows[1].Limbs, p.Report);
            Assert.IsFalse(p.Windows[0].IsVerified, "proposals are suggestions");
        }

        [Test]
        public void Proposer_StrikeFrameOnAnOverheadSmashPicksTheFastHand()
        {
            AnimationClip clip = LoadClip("Combo02_2");
            AttackWindowProposer.Proposal p = ProposeOnPlayer("Combo02_2", 0.28f / clip.length);
            Assert.AreEqual(1, p.Windows.Count, p.Report);
            Assert.AreEqual(StrikeLimbMask.RightHand, p.Windows[0].Limbs, p.Report);
        }

        [Test]
        public void Proposer_StrikeFrameOnASpinSweepProposesBothFeet()
        {
            AnimationClip clip = LoadClip("Combo06_1");
            AttackWindowProposer.Proposal p = ProposeOnPlayer("Combo06_1", 0.57f / clip.length);
            StrikeLimbMask limbs = StrikeLimbMask.None;
            foreach (HitWindow w in p.Windows) limbs |= w.Limbs;
            Assert.AreEqual(StrikeLimbMask.BothFeet, limbs, p.Report);
        }

        [Test]
        public void Proposer_FlagsAnUnhintedSweepItCannotJudge()
        {
            AttackWindowProposer.Proposal p = ProposeOnPlayer("Combo06_1", -1f);
            StringAssert.Contains("SWEEP NOT PROPOSED", p.Report);
        }

        private AttackWindowProposer.Proposal ProposeOnPlayer(string clipName, float strikeHint)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Content/Prefabs/Player.prefab");
            GameObject preview = Object.Instantiate(prefab);
            _created.Add(preview);
            foreach (MonoBehaviour b in preview.GetComponentsInChildren<MonoBehaviour>()) b.enabled = false;
            return AttackWindowProposer.Propose(preview.GetComponentInChildren<Animator>(), LoadClip(clipName), 1f, strikeHint);
        }
    }
}
