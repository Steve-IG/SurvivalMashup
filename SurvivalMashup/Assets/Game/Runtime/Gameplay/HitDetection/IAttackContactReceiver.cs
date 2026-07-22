namespace ToyChest.Gameplay.HitDetection
{
    /// <summary>
    /// Implemented by any attacker whose blow should land on its animation's contact frame — the player,
    /// an enemy, a companion, a boss. This is the character-agnostic half of the canonical
    /// <c>OnAttackContact</c> standard: an attack clip carries one event, a
    /// <see cref="AttackContactRelay"/> on the model forwards it, and whichever attacker owns the model
    /// resolves its own authored hit volume.
    ///
    /// It exists so the animation seam is not player-specific. A clip authored once ("place
    /// OnAttackContact on the contact frame") must work on <em>every</em> character that plays it; without
    /// a shared receiver the event would raise "has no receiver" on anything but the player, and enemy
    /// contact timing would be stuck on internal timers while the player's synced to animation.
    /// </summary>
    public interface IAttackContactReceiver
    {
        /// <summary>
        /// Called at the attack animation's contact frame. The implementer lands its blow now (resolving
        /// its authored hit volume) instead of on a timer. Must be safe to call at any time and more than
        /// once per swing: an implementation ignores the call when it is not mid-attack, so a stray or
        /// duplicated event can never produce a second hit.
        /// </summary>
        void NotifyAnimationContact();
    }
}
