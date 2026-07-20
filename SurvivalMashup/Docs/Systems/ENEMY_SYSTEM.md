# Enemy System

**Architecture:** ToyChest v1.0
**Status:** Living Specification
**Owner:** Gameplay Design

---

## System Ownership

This system owns:
- Enemy identity, enemy roles, encounter composition goals, regional enemy ecology, and enemy definition guidance.

This system does NOT own:
- AI framework behavior, Ability rules, Damage resolution, Loot tables, or Region state.

Primary Responsibilities:
- Define hostile Gameplay Objects through composition so enemies reinforce challenge and regional identity.

Primary Data:
- Enemy definitions, roles, tags, attributes, resources, ability references, and loot hooks.

Primary Runtime Objects:
- Enemy Gameplay Object instances and their composed components.

Published Events:
- TBD

Consumed Events:
- TBD

---

# Purpose

The Enemy System defines how hostile Gameplay Objects are created, configured, and behave.

Enemies provide challenge, reinforce regional identity, encourage build experimentation, and create memorable combat encounters.

The Enemy System composes existing ToyChest systems rather than introducing unique gameplay rules.

---

# Design Philosophy

Enemies are defined by behaviors, abilities, and interactions—not simply by statistics.

Interesting encounters emerge from combinations of enemy types, environmental hazards, and player choices.

Combat should emphasize high enemy density, varied behaviors, and fast-paced action rather than prolonged fights against damage sponges.

---

# Core Goals

- Create varied combat encounters.
- Encourage player adaptation.
- Reinforce regional identity.
- Support cooperative gameplay.
- Enable emergent interactions.
- Scale naturally into procedural content.

---

# Enemy Architecture

Enemies are Gameplay Objects.

Typical capabilities include:

- Attributes
- Resources
- Gameplay Tags
- Relationships
- Abilities
- Gameplay Effects
- Inventory (optional)
- Loot
- World Properties
- AI Controller
- Presentation

Enemy behavior emerges through composition.

---

# Milestone 2 Implementation (Review Group 1 — Core Action Foundation)

The first enemy, the **Grunt** (`object.grunt`), is an ordinary Gameplay Object with **no enemy-specific engine code and no combat/AI/targeting manager** — it composes the same capabilities as every other object: the shared Maximum Health attribute + bound Current Health resource, its own authored pursuit-speed attribute (`attribute.enemy_speed`), an `Actor.Enemy` identity tag, and an authored attack ability (`ability.enemy_strike`, a `DamageEffect` on the target's health).

- **Behaviour is a thin adapter, not a framework.** `EnemyCombatant` (`ToyChest.Gameplay.Enemy`) is the enemy counterpart to `NpcWanderLocomotion`: it reads speed off the composed object, asks the pure, deterministic `PursuitMotor` for the readable phase — **Idle → Pursue → Attack** — drives a `CharacterController`, and in striking range activates the enemy's authored attack ability against the player. Attack **cadence is the ability's cooldown**, not adapter logic. It is driven by `Update`, not by any AI/combat manager.
- **Damage is the existing `DamageEffect`.** Enemies and the player use the *same* Ability → Gameplay Effect → Resource path. When the full Damage System (`DAMAGE_SYSTEM.md`) arrives, `DamageEffect` routes through it with no content change.
- **Death is the existing lifecycle.** Health depletion (the Resource System's `Depleted` signal) tears the enemy down through the normal Gameplay Object lifecycle; no death system.
- **Loot uses the existing inventory pipeline.** On death the enemy drops a `LootPickup` (a thin trigger adapter, the pickup counterpart to `HazardVolume`) that the player walks over; it adds an authored item (`item.monster_fang`) via `InventorySet.TryAdd` — the same add the loot-crate effect uses. No loot-table framework.
- **Targeting is scene-native.** The enemy finds the player by the built-in `Player` Unity tag; the player's melee (`PlayerCombat`) finds enemies by the `Enemy` Unity tag via the same proximity query `PlayerInteractor` uses. No `TargetingManager`.

One excellent archetype was built rather than several partial ones. Elites, ranged/varied roles, relationships/friendly-fire, and the full Damage System remain future work and require no redesign of this composition.

---

# Milestone 2 Implementation (Review Group 2 — Game Feel & Combat Polish)

The Grunt was made **readable and fair to fight** without adding mechanics or archetypes. All changes are behaviour/feel and presentation on the same composed object; no new framework, no Damage/AI/combat manager.

- **Telegraphed attack (readability + fairness).** `EnemyCombatant` now runs a small self-paced attack cycle in the adapter: **Ready → Wind-up → Strike → Recover**. On entering strike range the Grunt roots and *anticipates* for a wind-up window (the player can see it coming and dodge — leaving range during wind-up cancels the strike), the blow lands at the end of the wind-up, then the Grunt is briefly committed (rooted, vulnerable) during recovery. Cadence now lives in this behaviour, not in the ability cooldown (the `ability.enemy_strike` cooldown was lowered to a non-blocking floor); the strike is still the same authored `DamageEffect`.
- **Animation.** The Grunt is a rigged **humanoid** (JC Stylized Ranger — a distinct silhouette from the player) with an AnimatorController: Idle → Speed-driven locomotion blend tree, an Attack state (played on wind-up, so the anticipation the player reads *is* the animation), and a Death state. The thin `EnemyAnimatorDriver` sets parameters from `EnemyCombatant`'s speed and its `WindUpStarted` / `Died` events. Damage feedback is a hit flash. *(RG2 originally omitted a hit-reaction animation to avoid stunlock; Milestone 2 RG5 added a brief hit flinch — see below — because it is animation-only and does not touch the timer-driven attack cadence, so it cannot stunlock.)*
- **Death presentation.** Death still is the Resource-depleted signal, but teardown is deferred by a short linger (`_deathDelay`) so the death animation plays before the object is destroyed and the loot drops.
- **Presentation.** A `HitFlash` tints the Grunt on damage (driven by a new `Damaged` event). Camera shake is player-centric (the enemy carries no impulse source).

Fairness knobs are authored on the adapter (wind-up seconds, recovery seconds, aggro/attack ranges, turn speed), so encounter tuning stays data.

---

# Milestone 2 Implementation (Review Group 5 — Player Combat Foundation)

The Grunt is used only to evaluate the player's melee attack; no enemy mechanics were added. One presentation change: the Grunt now plays a **brief hit flinch** when struck — a `Hit` state on the enemy AnimatorController driven by `EnemyAnimatorDriver` off the existing `Damaged` event (alongside the hit flash), so blows read as connecting. It is **animation only**: the enemy's attack cadence is driven by `EnemyCombatant`'s own timers, not the animator, so a flinch never interrupts the gameplay attack and cannot stunlock the encounter — resolving the RG2 concern that kept the reaction out. Broader enemy improvements (readability, variety, roles) remain the next review group.

---

# Milestone 2 Implementation (Review Group 5A — Hit Detection Architecture)

The enemy strike now resolves *which* object it lands on through the **canonical hit-detection vocabulary** (`ToyChest.Gameplay.HitDetection`; the architecture lives in `COMBAT.md`), the same one the player melee uses — player and enemy no longer have two separate hit paths. `EnemyCombatant.TryAttack` used to blindly activate its authored attack ability against the one known player; it now queries a frontal `Cone` `HitVolume` with the shared `HitDetector` at the moment of impact and activates the ability only against what the query returns. Consequences:

- The strike is **directional**: a player who slips out of the arc during the wind-up telegraph is not hit — the enemy has no privileged guaranteed hit.
- Cadence, telegraph (Ready → Wind-up → Strike → Recover), damage, death, and loot are **unchanged** — only target resolution moved onto the shared vocabulary. Still no AI/combat/targeting manager; the strike volume's reach and arc are authored fairness knobs on the adapter.

This confirms the hit-detection vocabulary is universal across attacker types (Enemy System, Player) with no enemy-specific hit code.

---

# Enemy Identity

Every enemy should have a clear gameplay identity.

Examples:

Swarm

Brute

Ranged

Support

Summoner

Controller

Ambusher

Assassin

Tank

Artillery

An enemy's role should be recognizable within seconds.

---

# Regional Identity

Each region develops a unique ecosystem.

Examples:

Forest

- Wolves
- Treants
- Vines
- Spiders

Volcanic

- Lava Golems
- Fire Sprites
- Molten Beetles

Frozen

- Ice Wolves
- Crystal Golems
- Frost Witches

Enemy themes reinforce exploration and progression.

---

# Encounter Design

Challenge comes from combinations.

Examples:

Brute + Archer

Shield Bearer + Mage

Summoner + Swarm

Healer + Elite

Exploder + Fast Melee

The encounter should be more interesting than its individual enemies.

---

# Combat Philosophy

Combat favors:

- High enemy density.
- Low time-to-kill for common enemies.
- Distinct enemy behaviors.
- Frequent player decision-making.
- Continuous movement.

Elite enemies and bosses increase complexity rather than simply increasing health.

---

# Difficulty Scaling

Difficulty may scale through:

Enemy composition

Enemy abilities

AI coordination

Environmental hazards

World modifiers

Regional modifiers

Difficulty should avoid excessive health inflation whenever possible.

---

# Elite Enemies

Elite enemies introduce additional mechanics.

Examples:

Additional abilities

Elemental modifiers

Rare affixes

Unique loot

Improved AI

Elite encounters should feel immediately different.

---

# Bosses

Bosses are handcrafted gameplay moments.

Bosses should emphasize:

Pattern recognition

Movement mastery

Ability usage

Companion synergy

Environmental awareness

Bosses unlock significant progression.

---

# Enemy Abilities

Enemies use the same Ability System as players.

Examples:

Projectile attacks

Area attacks

Charges

Teleports

Summons

Healing

Buffs

Debuffs

This promotes architectural consistency.

---

# Enemy Resources

Enemies may use resources.

Examples:

Current Health

Energy

Mana

Shield

Ammo

Cooldowns

Bosses may introduce additional resources when appropriate.

---

# World Interaction

Enemies participate in the World Reaction System.

Examples:

Burning

Freezing

Electrifying water

Destroying structures

Creating hazards

Harvesting nearby resources (future)

Players and enemies follow the same environmental rules.

---

# AI

Enemy AI reasons through gameplay systems.

AI evaluates:

Targets

Distance

Threat

Current Health

Status Effects

Environment

Objectives

Nearby allies

Enemy behavior should emerge from reusable decision-making systems.

---

# Cooperative Play

Enemies should naturally scale for cooperative play.

Scaling may include:

Composition

Coordination

Ability frequency

Objectives

Environmental interactions

Avoid simply multiplying health.

---

# Loot Integration

Enemies may reward:

Equipment

Resources

Crafting materials

Currencies

Recipes

Rare drops

Loot should reinforce enemy and regional identity.

---

# Progression

As players become stronger:

- Earlier enemies become easier.
- Larger enemy groups appear.
- New mechanics are introduced.
- Regional challenges increase.

Players should periodically feel overpowered before entering a more dangerous region.

---

# Procedural Compatibility

Enemy groups should support procedural generation.

Procedural encounters use:

Enemy roles

Regional themes

Difficulty budgets

Environmental modifiers

Objectives

Encounter generation remains data-driven.

---

# Future Expansion

Examples:

Mounted enemies

Flying enemies

Burrowing enemies

Faction warfare

Enemy evolution

Seasonal enemy variants

Dynamic invasions

None should require redesigning the Enemy System.

---

# Uses ToyChest Systems

Gameplay Object

Ability System

Attribute System

Resource System

Damage System

Gameplay Effects

Gameplay Tags

Relationship System

World Reaction System

Loot System

Region System

AI System

Gameplay Events

---

# Success Criteria

The Enemy System succeeds when:

- Enemy variety comes primarily from behavior rather than statistics.
- Encounters remain engaging through combinations and interactions.
- Regions develop recognizable enemy ecosystems.
- Cooperative play creates new tactical opportunities.
- Players are encouraged to adapt their builds and strategies.
- Combat remains fast, expressive, and highly replayable.

---

# Implementation Notes

- Author enemies as data-driven Gameplay Object Definitions.
- Reuse the same gameplay systems available to players whenever possible.
- Build encounters using role composition and difficulty budgets.
- Favor new mechanics and behaviors over inflated statistics.
- Treat enemy interactions with the world as first-class gameplay systems rather than scripted exceptions.