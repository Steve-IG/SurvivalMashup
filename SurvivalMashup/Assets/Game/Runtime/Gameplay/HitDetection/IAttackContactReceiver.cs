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

        /// <summary>
        /// Called when the attack animation's <em>committed motion</em> has ended (the authored
        /// <c>OnAttackEnd</c> event), i.e. the follow-through with meaningful root motion is complete and
        /// the remaining clip is an interruptible tail. The implementer may release the attack's movement
        /// commitment here so the player can immediately flow into the next action (locomotion, another
        /// attack, jump, roll) if they are inputting one. Per-clip and animator-authored, so no attack needs
        /// a hand-tuned recovery time; a timer fallback still ends the attack for clips without the event.
        /// Must be safe to call at any time and more than once: ignored unless the attack is past its
        /// contact frame.
        /// </summary>
        void NotifyAttackEnd();

        /// <summary>
        /// Opens a <em>contact window</em> (the authored <c>OnAttackContactStart</c> event): from now until
        /// <see cref="NotifyContactWindowEnd"/> the attack's hit volume stays live and strikes anything that
        /// enters it — each target at most once per window. For attacks whose damage spans a stretch of the
        /// clip (a flip kick that lands in a slam) rather than one frame. Opening the window also counts as the
        /// attack's contact. Plain <see cref="NotifyAnimationContact"/> remains the instantaneous form.
        /// </summary>
        void NotifyContactWindowStart();

        /// <summary>Closes the contact window opened by <see cref="NotifyContactWindowStart"/> (<c>OnAttackContactEnd</c>).</summary>
        void NotifyContactWindowEnd();
    }
}
