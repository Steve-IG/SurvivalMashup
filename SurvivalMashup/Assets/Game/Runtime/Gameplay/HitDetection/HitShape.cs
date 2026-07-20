namespace ToyChest.Gameplay.HitDetection
{
    /// <summary>
    /// The geometric form of a <see cref="HitVolume"/> — the one authored knob that makes the same
    /// hit-detection vocabulary describe a melee arc, an explosion, a hazard field, or a projectile's
    /// swept path. Deliberately small: new shapes are added only when a real attack needs one
    /// (Composition Over Specialization), not speculatively.
    /// </summary>
    public enum HitShape
    {
        /// <summary>
        /// Omnidirectional: everything within <see cref="HitVolume.Radius"/> of the origin, regardless of
        /// facing. Radial abilities — explosions, shockwaves, auras, persistent hazard fields, area heals.
        /// </summary>
        Sphere,

        /// <summary>
        /// A frontal arc: within <see cref="HitVolume.Radius"/> and inside
        /// <see cref="HitVolume.ConeHalfAngleDegrees"/> of the origin's forward. Directional strikes —
        /// sword swings, punches, kicks, thrusts, tail whips, monster swipes — where facing must matter,
        /// so a blow cannot connect with something behind the attacker.
        /// </summary>
        Cone,
    }
}
