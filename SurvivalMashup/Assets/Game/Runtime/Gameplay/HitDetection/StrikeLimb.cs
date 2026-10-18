using System;

namespace ToyChest.Gameplay.HitDetection
{
    /// <summary>
    /// A part of a character that can deliver a blow. Every attacking character carries a strike shape per
    /// limb (see <see cref="CharacterStrikeProfile"/>); an attack's hit windows name which limbs are live.
    /// Because attacks reference limbs by role rather than by bone, one authored attack works on any
    /// character whose profile provides those limbs.
    /// </summary>
    public enum StrikeLimb
    {
        RightHand = 0,
        LeftHand = 1,
        RightFoot = 2,
        LeftFoot = 3,
        Head = 4,
        Weapon1 = 5,
        Weapon2 = 6,
    }

    /// <summary>A set of <see cref="StrikeLimb"/>s, as authored on a hit window ("both fists", "right foot").</summary>
    [Flags]
    public enum StrikeLimbMask
    {
        None = 0,
        RightHand = 1 << StrikeLimb.RightHand,
        LeftHand = 1 << StrikeLimb.LeftHand,
        RightFoot = 1 << StrikeLimb.RightFoot,
        LeftFoot = 1 << StrikeLimb.LeftFoot,
        Head = 1 << StrikeLimb.Head,
        Weapon1 = 1 << StrikeLimb.Weapon1,
        Weapon2 = 1 << StrikeLimb.Weapon2,
        BothHands = RightHand | LeftHand,
        BothFeet = RightFoot | LeftFoot,
    }

    /// <summary>Conversions between <see cref="StrikeLimb"/> and <see cref="StrikeLimbMask"/>.</summary>
    public static class StrikeLimbs
    {
        /// <summary>Number of limb roles.</summary>
        public const int Count = 7;

        /// <summary>The mask bit for one limb.</summary>
        public static StrikeLimbMask ToMask(StrikeLimb limb) => (StrikeLimbMask)(1 << (int)limb);

        /// <summary>Whether <paramref name="mask"/> includes <paramref name="limb"/>.</summary>
        public static bool Contains(StrikeLimbMask mask, StrikeLimb limb) => (mask & ToMask(limb)) != 0;
    }
}
