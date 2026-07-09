using NUnit.Framework;
using ToyChest.Gameplay.Player;

namespace ToyChest.Tests.Player
{
    /// <summary>
    /// Verifies the deterministic ordering the interactor imposes on discovered candidates before
    /// handing them to the interaction system: nearest first, ties broken by stable id. Physics
    /// overlap order is nondeterministic, so this ordering is what makes contextual interaction
    /// selection reproducible (Engine Principle 17).
    /// </summary>
    public sealed class PlayerInteractorOrderingTests
    {
        [Test]
        public void Compare_OrdersByDistance_NearestFirst()
        {
            Assert.Less(PlayerInteractor.Compare(1f, "b", 4f, "a"), 0,
                "The nearer candidate sorts first regardless of id.");
        }

        [Test]
        public void Compare_EqualDistance_BreaksTieByOrdinalId()
        {
            Assert.Less(PlayerInteractor.Compare(2f, "a", 2f, "b"), 0);
            Assert.Greater(PlayerInteractor.Compare(2f, "b", 2f, "a"), 0);
        }

        [Test]
        public void Compare_SameDistanceAndId_IsEqual()
        {
            Assert.AreEqual(0, PlayerInteractor.Compare(2f, "same", 2f, "same"));
        }
    }
}
