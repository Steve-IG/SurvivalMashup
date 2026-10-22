# Ability System

**Status:** Living Specification  
**Version:** 1.0

---

## System Ownership

This system owns:
- Ability definitions, activation models, costs, cooldowns, targeting configuration, and ability loadouts.

This system does NOT own:
- Gameplay Effect execution, Resource values, Attribute math, Damage resolution, or actor identity.

Primary Responsibilities:
- Provide the reusable action framework for combat, movement, utility, companion, enemy, and interaction abilities.

Primary Data:
- Ability definitions, tags, categories, activation rules, costs, cooldowns, effects, unlocks, and evolution data.

Primary Runtime Objects:
- Ability instances, equipped loadouts, cooldown state, and activation state.

Published Events:
- Ability Granted, Ability Revoked, Ability Activated, Ability Activation Failed (with reason), Ability Cooldown Started, Ability Cooldown Ended.

Consumed Events:
- None.

---

# Milestone 0 Implementation (approved decisions)

Abilities are **orchestration**: Gameplay Effects answer *what happens*; abilities answer *when and why*. No gameplay mutation is implemented inside the Ability System — effects execute through the Gameplay Effect Runner, and costs mutate through the Resource System.

Abilities are **deterministic recipes**: for identical inputs (owner state, target, definition), an activation attempt produces identical outcomes — the same validation result, the same costs, the same effect sequence in the same order. No randomness, wall-clock time, or presentation state participates in activation; time enters only as injected tick deltas.

Ability Definitions contain **configuration, not gameplay behavior**: gates, costs, a cooldown, a target mode, and references to the Gameplay Effects that own every mutation. Engine code interprets that configuration; authoring a new ability is authoring data.

The implemented Milestone 0 subset (`ToyChest.Systems.Abilities`):

- **Definition/instance split:** `AbilityDefinition` (immutable asset: category, tags, target mode, tag gates, costs, cooldown, effect sequence) and `AbilityInstance` (runtime cooldown state), per Engine Principle 14.
- **AbilitySet capability:** composed onto every Gameplay Object by the factory; abilities declared on the object definition are granted at composition, and runtime grant/revoke supports progression and equipment.
- **Deterministic activation pipeline**, first failing check reported: granted → target validity → owner tag gates (required, then blocking; hierarchical match) → cooldown → costs. `CanActivate` runs the same validation without committing anything, for UI and AI.
- **Costs:** generic resource id + amount pairs, all-or-nothing; validation never throws for a missing resource — an actor lacking the resource simply cannot afford the ability.
- **Cooldowns:** fixed seconds, owned by the Ability System, advanced through the object's `Tick` with injected time. Charge-based, recharge, conditional, and shared-group cooldowns are future extensions.
- **Targeting contract:** the ability declares its target mode (`Self` or `Provided`); the activator (input, AI, interaction) selects and supplies the concrete target. Spatial modes (ground, area, projectile) arrive with the world systems able to answer them. **The canonical world system that answers "which objects are in range/arc/blast?" is the Hit Detection vocabulary** (`ToyChest.Gameplay.HitDetection`; see `COMBAT.md`): a `HitDetector` resolves an authored `HitVolume` into Hit Results, and the activator feeds each result to this `Provided` path. Target *selection* stays a caller concern — detection never activates abilities or applies effects — so the ability/effect separation is preserved. An attack is authored, not coded (RG5B): an animation event (`OnAttackContact`) + a shared `HitVolumeAsset` preset + a socket/bone anchor + this ability. **Multiple Targets is now implemented (RG5D):** a `TryActivate(DefinitionId, IReadOnlyList<EffectTarget>)` overload applies one activation's effects to every valid hit at once — see *Milestone 2 Implementation (Review Group 5D)* below.
- **AbilityActivated** is published when the activation is committed (validation passed, costs paid, cooldown started), before effect dispatch, so the event trace reads causally: activation → effect consequences.
- **AbilityCategory value type:** the organizational category is exposed as a lightweight string-backed `AbilityCategory` struct with ordinal equality (`AbilityCategory.None` for uncategorized abilities). Data-driven — a new category is an authored string, not code. Deliberately not an enum (categories are content) and not a Gameplay Tag (categories are organizational and never queried by gameplay logic). Category remains purely organizational and never affects behavior.

## Future activation extension points

The Milestone 0 activation model is Instant: validation and commit happen in one deterministic step. The following extension points are reserved; each extends the pipeline **between validation and commit** without changing the definition/instance split, the targeting contract, or the effect dispatch contract:

- **Cast Time** — a validated activation enters a casting window before committing; commit happens when the window completes.
- **Channeling** — the commit sustains over time, executing effect sequences while the channel holds.
- **Interruptibility** — data-driven rules for what cancels a casting or channeling window (damage, movement, tags), publishing an interruption fact.
- **Multi-stage activation** — activations that progress through authored stages (charge → aim → release), each stage with its own gates and effects.

Evolution trees, unlock requirements, loadouts, AI usage metadata, and the remaining activation models are future work and remain specified below.

---

# Milestone 2 Implementation (Review Group 5D — Multiple Targets)

The one reviewed, accepted extension to the frozen Milestone 0 engine. Before RG5D the activation model applied a single activation's effects to exactly **one** target: `Activate` committed cost + cooldown once and ran the effect sequence against one `EffectTarget`. Applying one attack to several hits (true AoE / cleave) could not be expressed — per-target re-activation would double-charge costs and collide on cooldown. The Milestone 0 review gate was followed: the missing contract was reported, the smallest possible change identified, and it was accepted by the Technical Creative Director (2026-08-08).

**The change** — a new overload alongside the existing single-target one:

```
AbilityActivationResult TryActivate(DefinitionId ability, IReadOnlyList<EffectTarget> targets)
```

- **Commit exactly once.** It validates once (Provided mode, tag gates, cooldown, costs), then commits once — one cost payment, one cooldown, one `AbilityActivated` event — through a shared private `Commit(instance)` the single-target path now also uses, so one activation always means one cost and one cooldown regardless of how many targets its effects resolve against.
- **Apply effects per target.** After committing, it runs the definition's authored effect sequence once per **non-empty** target (`EffectContext` with the shared owner source and that target). Empty entries are skipped; a null/empty/all-empty set is `InvalidTarget`. Self-mode rejects a target set, exactly as the single-target overload rejects an explicit target for a Self ability.
- **Separation preserved.** Targeting stays a caller concern: the caller (Hit Detection via `PlayerCombat`) supplies the targets it found; the ability answers *what happens* to each. No DamageManager/AoEManager/HitApplicationManager was introduced. This realizes the **Multiple Targets** mode the targeting contract reserved.

Validation order for the multi-target path mirrors the single-target path (granted → Self rejects set → at least one valid target → tag gates → cooldown → costs), so the first-failing-check contract is unchanged. See `COMBAT.md` (RG5D) for how player melee uses it to hit several enemies with one punch.

---

# Player Activation Status (Review Group A audit, 2026-10-21)

The Ability System has no gap. The gap is in its **callers**. These are the only callers that activate abilities today:

- `InteractionSystem`: interactions as abilities.
- `PlayerCombat`: per-hit-window melee and combo-step abilities. They are Provided-target with 0 s cooldown, and combo cadence lives in `ComboSequencer`.
- `EnemyCombatant`: enemy strike.

**No caller activates a player ability from deliberate input.** `ability.second_wind` is granted by equipment but cannot be used by the player.

Constraint for the first player ability: `PlayerCombat` checks `CanActivate` only when a target is already in the arc, which suits zero-cooldown melee. An ability with a real cooldown must commit its cooldown **at press time** through a Self-mode activation. Otherwise a whiff ignores the cooldown and a later hit fails silently.

The proposed composition (see `DEVELOPMENT_PLAN.md`, *Recommended First Active Ability*) adds no new framework:
- a thin input-slot activator;
- a Self **activation ability** that owns the gates, cost, and cooldown;
- an authored attack whose hit window applies a Provided **impact ability** to every hit through the Multiple Targets overload.

The reserved *Multi-stage activation* extension point remains the engine-level alternative. It requires approval.

---

# Player Activation Pattern (Review Group B — Ground Slam)

The first player-activated ability is built from the composition above, with **no Ability System change**. Core and Framework are unchanged.

```
Ability1 input → PlayerInputBridge → PlayerAbilityActivator (slot 0)
  → AbilitySlotActivation.Press
      1. AbilitySet.CanActivate(ability.ground_slam)    refused → TryActivate publishes AbilityActivationFailed; nothing plays
      2. PlayerCombat.CanStartAbilityAttack(attack)     busy    → press buffered 0.18 s; nothing committed
      3. AbilitySet.TryActivate(ability.ground_slam)    commits: 6 s cooldown, AbilityCooldownStarted, AbilityActivated
      4. PlayerCombat.StartAbilityAttack(Combo_Ability_GroundSlam)
  → "Hammer slam" hit window → HitDetector (HV_GroundShockwave)
  → AbilitySet.TryActivate(ability.ground_slam_impact, targets)   one activation, damage per target
  → PlayerCombat.Impacted → PlayerAttackFeedback
```

**Two definitions.**
- `ability.ground_slam` is the **activation ability**: Self target, 6 s cooldown, no cost, no effects. It owns every rule about *whether* the slam may happen.
- `ability.ground_slam_impact` is the **impact ability**: Provided target, 0 s cooldown, `Fx_Damage_GroundSlam`. It owns *what happens* to each target hit.
- `Obj_Player` grants both. Progression may move the grant later (Review Group D).

**Cooldown semantics.** The cooldown is the Ability System's ordinary fixed cooldown on the activation ability. It is committed at press, before any hit detection. As a result:
- A slam that hits nothing still costs its cooldown.
- A press the Ability System refuses (OnCooldown, NotGranted, tag gate, cost) commits nothing, publishes `AbilityActivationFailed`, and plays no animation.
- A press made while the character is busy (mid-attack before the cancel point, rolling, or airborne) is buffered like a melee press. It commits nothing unless the slam actually starts. These movement-state refusals happen in the adapter, before the Ability System is asked, so **they publish no `AbilityActivationFailed`**. The player has no movement-state tags the activation could gate on, and adding them would be new framework state.
- The cooldown saves and restores through the existing `SaveManager` capture and `RestoreCooldown` (covered by `GroundSlamContentTests`).

**Adding the next ability** takes data plus one slot: an activation ability, an impact ability, an attack asset (a `ComboDefinition` plus an `AttackDefinition`), and a `PlayerAbilityActivator` slot bound to a new input action listed in `PlayerInputBridge._abilitySlotActions`. A Self ability that carries its own effects and has no attack, such as Second Wind, does not fit a slot yet, because a slot requires an attack. Review Group D decides whether to allow an attack-less slot.

**Pure rules, testable without a scene.** `AbilitySlotActivation` holds the press, buffer, and commit order. `IAbilityAttackPerformer` is the seam: `PlayerCombat` implements it, and tests fake it. `PlayerAbilityActivator` and `PlayerAbilityHud` are thin Unity adapters.

---

# Purpose

The Ability System is the universal framework that defines actions performed by actors within the game.

Abilities are not limited to combat.

Every meaningful action an actor performs should be represented as an ability whenever practical.

The objective is to create a highly modular, data-driven system that can support player abilities, companion behaviors, enemy attacks, traversal mechanics, interaction systems, and future gameplay without requiring architectural changes.

---

# Design Philosophy

Abilities define what an actor can do.

Actors define who performs those abilities.

Abilities should never be hardcoded to a specific actor type.

The same Fireball ability should be usable by:

- Players
- Companions
- Enemies
- Bosses
- NPCs

Differences should emerge through configuration rather than duplicated implementations.

---

# Core Principles

## Everything Is An Ability

Examples include:

Combat

- Fireball
- Shield Slam
- Meteor

Movement

- Dash
- Double Jump
- Grapple
- Glide

Utility

- Harvest
- Treasure Detection
- Auto Pickup
- Portable Crafting

Companion

- Return To Hub
- Store Inventory
- Scout Area

Interaction

- Open Portal
- Activate Shrine
- Revive Ally

Future systems should extend this framework rather than introducing parallel ability implementations.

---

## Data Driven

Abilities should be created primarily through data.

Engine code defines behavior.

Content defines configuration.

Adding a new ability should rarely require writing gameplay code.

---

## Composition Over Inheritance

Abilities should be assembled from reusable components.

Examples include:

Activation

+

Cost

+

Cooldown

+

Targeting

+

Effects

+

Animation

+

Audio

+

Visual Effects

+

World Reaction Effects

Rather than:

FireballAbility.cs

IceballAbility.cs

LightningBallAbility.cs

---

# Ability Structure

Every ability contains:

Identity

Description

Tags

Category

Activation Model

Resource Cost

Cooldown

Effects

Animation

Audio

Visual Effects

World Reaction Interactions

Evolution Tree

AI Usage Rules

Unlock Requirements

---

# Ability Categories

Combat

Movement

Utility

Companion

Ultimate

Interaction

Future categories should be added without architectural changes.

---

# Activation Models

Abilities define how they are activated.

Examples include:

Instant

Charged

Channeled

Projectile

Beam

Ground Target

Area Around Self

Dash

Leap

Summon

Toggle

Passive

Triggered

The activation model should be independent of the ability's gameplay effect.

---

# Resource System

Abilities may consume one or more resources.

The Ability System should not distinguish between Mana, Ammo, Energy, Rage, Charges, or future resource types.

All costs should use the same generic interface.

Examples:

Mana

Energy

Ammo

Arrows

Heat

Charges

Companion Bond

Future resources

Adding a new resource should not require changing the Ability System.

---

# Cooldowns

Every active ability may define its own cooldown behavior.

Cooldowns may include:

Fixed

Charge Based

Recharge Over Time

Conditional

Shared Cooldown Groups

Cooldown behavior should be configurable rather than hardcoded.

---

# Effects

Abilities may contain multiple effects.

Examples include:

Damage

Healing

Movement

Spawn Actor

Apply Status

Remove Status

Grant Buff

Create Hazard

Modify World Properties

Generate Loot

Play Cinematic

Reveal Area

No assumptions should be made about effect order.

Effects should be modular.

---

# Targeting

Abilities may target:

Self

Enemy

Ally

Ground

Object

Region

Direction

Area

Multiple Targets

Targeting should be independent from effects.

---

# Tags

Abilities should expose descriptive tags.

Examples:

Fire

Ice

Lightning

Nature

Projectile

Melee

Movement

Healing

Support

Summon

Explosion

Harvest

Traversal

Companion

Boss

Tags drive interactions rather than inheritance.

---

# Evolution

Every ability owns its own evolution tree.

Players improve individual abilities independently.

Evolution should primarily unlock new mechanics rather than increase numerical values.

Examples:

Fireball

↓

Explosion Radius

↓

Burning Ground

↓

Chain Explosion

↓

Lightning Conversion

↓

Meteor Impact

Different players should evolve identical abilities differently.

---

# AI Compatibility

The system should support AI-controlled actors.

AI should evaluate abilities using metadata such as:

Preferred Range

Priority

Target Type

Threat Value

Cooldown

Resource Cost

Situation Tags

AI behavior should not require ability-specific code.

---

# Multiplayer

The Ability System must support multiplayer from the beginning.

Authority

Prediction

Replication

Synchronization

Interruptions

Cooldown synchronization

Resource synchronization

should all be considered during implementation.

---

# Persistence Boundary

Per Engine Principle 25:

- **Authoritative:** which abilities are granted from a persistent source, and each granted ability's remaining cooldown.
- **Derived:** `IsOnCooldown` and every validation result (affordability, tag gates, targeting).
- **Serialized:** per persistently granted ability — definition id plus remaining cooldown. Grants that originate from the object definition, equipment, or progression are reconstructed by those sources rather than saved here.
- **Reconstructed:** abilities are re-granted (through the definition, equipment, or progression), then each cooldown is restored with the event-quiet `AbilitySet.RestoreCooldown`. Reconstruction publishes no ability facts.

---

# Success Criteria

The Ability System succeeds when:

- New abilities rarely require engine changes.
- New resource types require no architectural changes.
- Players regularly experiment with new abilities.
- Enemy abilities reuse the same framework.
- Companion abilities reuse the same framework.
- Designers can create most new abilities through data.
- AI can understand abilities through metadata.