using System;
using ToyChest.Framework.Data;
using ToyChest.Gameplay.Combat;
using ToyChest.Systems.Abilities;

namespace ToyChest.Gameplay.Player
{
    /// <summary>
    /// Performs an ability's authored attack once its activation has committed. Implemented by
    /// <see cref="PlayerCombat"/>; a seam so the activation rules are testable without a scene.
    /// </summary>
    public interface IAbilityAttackPerformer
    {
        /// <summary>Whether the character could start <paramref name="attack"/> right now (movement and attack state).</summary>
        bool CanStartAbilityAttack(ComboDefinition attack);

        /// <summary>Starts <paramref name="attack"/>. Called only right after <see cref="CanStartAbilityAttack"/> returned true.</summary>
        bool StartAbilityAttack(ComboDefinition attack);
    }

    /// <summary>The outcome of one press (or buffered retry) of an ability input slot.</summary>
    public enum AbilitySlotPressResult
    {
        /// <summary>Nothing happened this tick (no press pending).</summary>
        None,

        /// <summary>The activation committed (cost and cooldown) and the attack started.</summary>
        Activated,

        /// <summary>The character is busy (mid-attack, rolling, airborne); the press is held for the buffer window.</summary>
        Buffered,

        /// <summary>The Ability System refused the activation (on cooldown, not granted, gated, unaffordable). Nothing was committed.</summary>
        Refused,

        /// <summary>A buffered press expired before the character could act. Nothing was committed.</summary>
        Expired,
    }

    /// <summary>
    /// The pure rules of one player ability input slot: <c>press → activation ability → (optional) authored attack</c>.
    /// The slot binds an <b>activation ability</b> (Self target: tag gates, costs, the ability's cooldown, and any
    /// effects of its own) to an optional authored attack that performs it, whose hit windows apply separate
    /// <b>impact abilities</b> to every target hit. It holds no ability-specific code — a slot is two authored
    /// references. Two shapes:
    /// <list type="bullet">
    /// <item><b>Attack ability</b> (Ground Slam): the activation commits and the attack plays; damage comes from the
    /// attack's windows.</item>
    /// <item><b>Attack-less ability</b> (a Self heal or buff such as Second Wind): no attack is authored, so the
    /// activation's own Self effects are the whole ability. It needs nothing from the character's attack state, so it
    /// is never buffered behind a swing or a roll.</item>
    /// </list>
    ///
    /// <para><b>Cooldown is committed at press.</b> The activation ability commits (paying its cost and starting its
    /// cooldown through <see cref="AbilitySet"/>) before the attack starts and before any target is detected, so a
    /// slam into empty air costs its cooldown exactly like one that hits. Order per press:</para>
    /// <list type="number">
    /// <item>The Ability System validates first (<see cref="AbilitySet.CanActivate(DefinitionId)"/>). A refusal is
    /// reported through <see cref="AbilitySet.TryActivate(DefinitionId)"/>, which publishes
    /// <c>AbilityActivationFailed</c> and commits nothing; no attack plays.</item>
    /// <item>If the character cannot act yet (mid-attack before its cancel point, rolling, airborne) the press is
    /// held for the buffer window and retried every <see cref="Tick"/> — the same responsiveness the melee press
    /// has. Nothing is committed while buffered; an expired press is dropped.</item>
    /// <item>Otherwise the activation commits and the attack starts in the same call, so a committed cooldown always
    /// has a slam to go with it.</item>
    /// </list>
    /// </summary>
    public sealed class AbilitySlotActivation
    {
        private readonly float _bufferWindow;
        private float _bufferTimer;

        public AbilitySlotActivation(DefinitionId activationAbility, ComboDefinition attack, float bufferWindow)
        {
            ActivationAbility = activationAbility;
            Attack = attack;
            _bufferWindow = Math.Max(0f, bufferWindow);
        }

        /// <summary>The Self-mode ability whose activation owns this slot's gates, cost, and cooldown.</summary>
        public DefinitionId ActivationAbility { get; }

        /// <summary>The authored attack performed when the activation commits, or null for an attack-less ability.</summary>
        public ComboDefinition Attack { get; }

        /// <summary>Whether a press is being held until the character can act.</summary>
        public bool IsBuffered => _bufferTimer > 0f;

        /// <summary>The Ability System's answer to the most recent activation attempt (Activated when none was refused).</summary>
        public AbilityActivationResult LastResult { get; private set; } = AbilityActivationResult.Activated;

        /// <summary>The slot's input was pressed.</summary>
        public AbilitySlotPressResult Press(AbilitySet abilities, IAbilityAttackPerformer performer)
        {
            AbilitySlotPressResult result = TryActivate(abilities, performer);
            _bufferTimer = result == AbilitySlotPressResult.Buffered ? _bufferWindow : 0f;
            return result;
        }

        /// <summary>
        /// Retries a buffered press. Returns <see cref="AbilitySlotPressResult.None"/> when nothing is buffered,
        /// <see cref="AbilitySlotPressResult.Buffered"/> while still waiting, and <see cref="AbilitySlotPressResult.Expired"/>
        /// when the buffer runs out without the character becoming able to act.
        /// </summary>
        public AbilitySlotPressResult Tick(float deltaSeconds, AbilitySet abilities, IAbilityAttackPerformer performer)
        {
            if (_bufferTimer <= 0f)
            {
                return AbilitySlotPressResult.None;
            }

            AbilitySlotPressResult result = TryActivate(abilities, performer);
            if (result != AbilitySlotPressResult.Buffered)
            {
                _bufferTimer = 0f;
                return result;
            }

            _bufferTimer -= Math.Max(0f, deltaSeconds);
            if (_bufferTimer > 0f)
            {
                return AbilitySlotPressResult.Buffered;
            }

            _bufferTimer = 0f;
            return AbilitySlotPressResult.Expired;
        }

        /// <summary>Drops a buffered press without acting on it.</summary>
        public void ClearBuffer()
        {
            _bufferTimer = 0f;
        }

        private AbilitySlotPressResult TryActivate(AbilitySet abilities, IAbilityAttackPerformer performer)
        {
            if (abilities == null || (Attack != null && performer == null))
            {
                LastResult = AbilityActivationResult.NotGranted;
                return AbilitySlotPressResult.Refused;
            }

            // The Ability System's refusals win over a busy character: a press on cooldown is refused now, not
            // buffered. TryActivate repeats the failed validation only to publish AbilityActivationFailed; a failing
            // validation commits nothing.
            if (abilities.CanActivate(ActivationAbility) != AbilityActivationResult.Activated)
            {
                LastResult = abilities.TryActivate(ActivationAbility);
                return AbilitySlotPressResult.Refused;
            }

            if (Attack != null && !performer.CanStartAbilityAttack(Attack))
            {
                return AbilitySlotPressResult.Buffered;
            }

            // Commit at press: cost and cooldown are taken here, before any hit detection, so the outcome of the
            // attack (any number of targets, or none) can never skip or double the cooldown.
            LastResult = abilities.TryActivate(ActivationAbility);
            if (LastResult != AbilityActivationResult.Activated)
            {
                return AbilitySlotPressResult.Refused;
            }

            // An attack-less ability is complete here: TryActivate already ran its own Self effects.
            if (Attack != null)
            {
                performer.StartAbilityAttack(Attack);
            }

            return AbilitySlotPressResult.Activated;
        }
    }
}
