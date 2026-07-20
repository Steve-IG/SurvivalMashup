# Player

**Status:** Living Specification
**Version:** 1.0
**Owner:** Lead Gameplay Designer
**Last Updated:** June 2026

---

## System Ownership

This system owns:
- Player fantasy, feel goals, player capability categories, control expectations, and build-expression goals.

This system does NOT own:
- Ability implementation, Equipment rules, Companion behavior, Interaction logic, Damage rules, or UI implementation.

Primary Responsibilities:
- Define how controlling the player should feel and how player capabilities should expand over time.

Primary Data:
- The `Obj_Player` `GameplayObjectDefinition` and the attribute/resource/tag definitions it composes (Movement Speed, Maximum Health, Current Health, `Actor.Player`). The player owns no bespoke data type — it is authored composition.

Primary Runtime Objects:
- Player Gameplay Object and its composed capability components. Player-specific Unity behavior lives in thin adapters (`ToyChest.Gameplay.Player`) that own no gameplay state.

Published Events:
- None owned by the Player system directly; player actions publish through the systems that own them (Ability, Interaction, Resource, Framework lifecycle).

Consumed Events:
- None required by the Milestone 1 slice; UI/presentation may observe the systems' events.

---

# Milestone 1 Implementation (approved decisions)

The playable player is built **entirely through composition and authored data on the frozen Milestone 0 engine** — there is no player-specific engine code, no `Player.cs` gameplay class. The player is a `GameplayObjectDefinition` (`Obj_Player`) composed of existing capabilities; Unity-facing behavior is thin adapters that read the composed object.

- **Player as data.** `Obj_Player` composes a Movement Speed and a Maximum Health `AttributeDefinition`, the shared bound Current Health `ResourceDefinition`, and an `Actor.Player` identity `TagDefinition`. Inventory, equipment, abilities, and interactions are added the same way — as authored capabilities — in later review groups.
- **Movement ownership.** Movement *speed* is an authored **Attribute** (one source of truth); *locomotion* is a thin `PlayerLocomotion` adapter that reads that attribute off the composed object and drives a `CharacterController`, with the direction math isolated in the pure, deterministic `LocomotionMotor`. Discrete traversal (jump, dash, dodge) is later, **Ability**-driven work.
- **Input.** `PlayerInputBridge` reads the authored `Player.inputactions` (Unity Input System) and forwards intent: the move vector to `PlayerLocomotion`, the interact button to `PlayerInteractor`. It makes no gameplay decisions.
- **Interaction.** `PlayerInteractor` performs only scene-facing proximity discovery, producing a deterministically ordered candidate list routed to the existing `InteractionSystem`; all validation and execution stay in the engine.
- **Camera.** A **Cinemachine 3** virtual camera (`CM ThirdPerson`) follows the player as a thin presentation layer (Milestone 2, RG2), driven by a `CinemachineBrain` on the main camera. Gameplay is unaffected — locomotion reads the active camera's facing, so the brain that drives the main camera is transparent to movement. The original hand-rolled `ThirdPersonCameraRig` is superseded (no camera framework was introduced into gameplay code). **RG3 stability fix:** the camera is now a **fixed-angle, position-only** follow — world-space `CinemachineFollow` with light symmetric damping and a constant downward pitch, and **no Aim component**. The RG2 rotation composer (with look-ahead) re-aimed as the player moved, which read as distracting rotational drift; removing it makes the camera translate with the player but never rotate, so input feels directly connected to movement. A deoccluder still handles obstacles.
- **Scene composition.** The player is a scene object composed by `GameplayObjectSpawner` (see `Docs/Architecture/PROJECT_ARCHITECTURE.md`, Scene composition), not by bespoke player spawning.
- **Death and respawn (Review Group 7).** Death is not a system: it is the existing **Resource Depleted** transition on Current Health. Respawn is intentionally minimal and manager-free — a pure, unit-tested `PlayerRespawn` helper (the respawn counterpart to `InventoryEquip`) restores a downed player to a clean state using only existing capability operations (refill resources through the Resource System, clear active statuses through the Status Effect System), and a thin `PlayerRespawnController` listens for the health-depleted signal and, deferred one frame so it never mutates the status set mid-tick, restores state and moves the player to an authored spawn-point Transform. A spawn point is just a scene `Transform`; there is no checkpoint system.
- **Melee attack (Milestone 2, Review Group 1).** The player's attack is an authored **Ability** (`ability.player_strike`) using the existing Ability → Gameplay Effect → Resource path: a `DamageEffect` on the target's health, gated only by a cooldown, so cadence is data, not code. `PlayerCombat` is a thin adapter that, on the Attack input, runs the same proximity query as `PlayerInteractor` to pick the nearest `Enemy`-tagged target, faces it, and activates the ability against it; whether the swing lands (cooldown) is the Ability System's decision. No weapon or combat framework — a weapon would later be authored equipment that grants a different strike ability.
- **No stamina (Milestone 2, Review Group 2).** A basic attack must never be gated by a resource — the player can always swing. Combat is balanced through attack cadence, recovery/animation timing, enemy behaviour, cooldowns, and positioning, not a stamina economy. The temporary Stamina resource from Review Group 1 was removed.
- **Movement feel (Milestone 2, Review Group 2).** Locomotion now integrates toward its target velocity with separate acceleration/deceleration (the pure `LocomotionMotor.Accelerate`), so movement has weight but stops crisply and stays responsive under rapid input changes. Turning is faster (higher turn speed). Speed is still the authored Movement Speed **Attribute**; the accel/decel and turn rates are feel tuning on the adapter, not gameplay.
- **Combat feel (Milestone 2, Review Group 2).** `PlayerCombat` adds **input buffering** (a press just before the cooldown ends is remembered for a short window and fires when ready) and **attack commitment** (a landed swing briefly damps movement via `PlayerLocomotion.ApplyMovementLock`, so attacks have weight without ever locking the player out). Neither is a new gameplay system — buffering and commitment are timing/feel on the existing ability activation.
- **Presentation (Milestone 2, Review Group 2).** Thin presentation adapters consume existing gameplay events only: `PlayerAnimatorDriver` reads locomotion speed and the `PlayerCombat.Attacked` / health-change / health-depleted signals to drive the Animator (see Animation below); a `HitFlash` tints the model on damage; a `CombatImpulse` (Cinemachine impulse) shakes the camera on hit (strong) and on swing (subtle). No gameplay logic lives in presentation.
- **Animation (Milestone 2, Review Group 2).** The player is a rigged **humanoid** (JC Stylized Warrior) with a real AnimatorController: an Idle → Speed-driven locomotion blend tree, plus Attack, Hit-reaction, and Death states driven by parameters the thin `PlayerAnimatorDriver` sets from gameplay. Clips are retargeted humanoid animations (Kevin Iglesias locomotion/idle/hit/death, Frank attack). The graph and parameter contract are the reusable foundation — swapping the character retargets clips without touching the graph. No gameplay logic lives in the state machine; `applyRootMotion` is off (the `CharacterController` owns motion).
- **Base speed & feel (Milestone 2, Review Group 3).** The authored Movement Speed attribute base was raised 5 → **7** for more energetic traversal (still the single gameplay source of truth). Locomotion feel was retuned (accel 60 / decel 80; turn 620°/s — lower than RG2 so directional animation reads during turns; gravity 25 for a snappier jump arc). Acceleration is reduced while airborne (`_airControl`) for momentum with control.
- **Sprint (Milestone 2, Review Group 3).** Hold-to-sprint (`PlayerLocomotion.SetSprint`), **not a toggle**, released instantly returns to normal speed. Sprint is a **multiplier on the authored Movement Speed attribute** (×1.5), not a new resource — no stamina. It blends through the same locomotion tree (the sprint tier of the blend).
- **Jump (Milestone 2, Review Group 3).** Single-press (`PlayerLocomotion.Jump`), grounded-gated, applies an upward impulse the existing gravity integrates; airborne planar control is retained (dampened). A `Jumped` event drives the jump animation; grounded state drives the fall→land transition for a clean landing. No double-jump/wall-jump/ledge/traversal — jump exists only for movement feel.
- **Roll (Milestone 2, Review Group 3).** Single-press (`PlayerLocomotion.Roll`) evasive roll: a brief movement burst in the input (or facing) direction that eases out to a natural recovery, with a `Rolled` event driving the roll animation. **No i-frames, no stamina, no dodge framework, no animation-cancel system** — it is pure movement feel.
- **Directional locomotion (Milestone 2, Review Group 3).** The RG2 1D speed blend was replaced by a **2D freeform-directional** blend tree (parameters `MoveX`/`MoveY` = velocity relative to facing, normalized to the run speed): idle at centre, an 8-way **walk** ring, an 8-way **run** ring, and a forward-biased **sprint** tier, plus jump/fall/land and roll states. The thin `PlayerAnimatorDriver` sets `MoveX`/`MoveY`/`Grounded` and the jump/roll triggers from `PlayerLocomotion`; **no gameplay logic moved into the Animator** (Gameplay → Animator Driver → Animator Controller preserved). Because the character turns to face its movement (correct for a fixed, non-orbit camera), forward locomotion dominates during steady running and the side/back animations play through turns and sharp reversals.
- **Momentum-based roll (Milestone 2, Review Group 4).** The roll no longer replaces movement with a fixed burst; it **inherits the character's current horizontal speed and adds an explosive impulse** on top (the pure, tested `LocomotionMotor.RollLaunchVelocity`), then bleeds that burst back toward the player's *ongoing* locomotion intent using the same `LocomotionMotor.Accelerate` the ground loop uses. Walking, running, and sprinting therefore produce progressively faster, longer rolls from a **single code path** (sprint naturally rolls farther), the beginning feels explosive and committed, and the tail recovers straight into a run — momentum is preserved rather than dropped to a stop the player must re-accelerate from. Tuned on the adapter (impulse, launch clamp, deceleration, duration); still **no i-frames, no stamina, no dodge framework, no animation-cancel** — pure movement feel.
- **Landing & roll recovery (Milestone 2, Review Group 4).** Foot sliding after landings and rolls was eliminated by improving the animation–gameplay relationship, **not** by reducing momentum. A read-only `Speed` float (set by `PlayerAnimatorDriver`) lets the Animator tell whether movement input continues: **Land** and **Roll** now yield to Locomotion the instant the player is still moving (immediate re-entry, no exit-time hold) and only play a brief settle pose when stationary. Combined with the momentum-preserving roll, the character blends continuously back into locomotion through every transition. No gameplay logic entered the Animator — the `Speed` parameter is downstream presentation only, preserving Gameplay → Animator Driver → Animator Controller.
- **Melee attack feel — phased swing (Milestone 2, Review Group 5).** The single melee attack (still the authored `ability.player_strike` → `DamageEffect`) was made to feel connected and responsive. `PlayerCombat` now runs a **wind-up → impact → recovery** swing (the pure, tested `MeleeSwing`), mirroring the approved enemy telegraph: the swing starts immediately on input (gated by the ability's own `CanActivate`, so cadence stays the authored cooldown), but the **damage lands on the swing's contact frame** rather than the input frame. Input buffering and attack commitment are preserved; movement is damped (never locked) during the swing and blends back to locomotion early when input continues (`Speed`-gated Attack transition); roll/jump still interrupt. Impact juice — camera impulse, an impact VFX burst at the hit point, a brief hit-stop, and sound hooks — lives in a new thin `PlayerAttackFeedback` adapter driven off `PlayerCombat.Attacked`/`Impacted` events. No combat/weapon/targeting/combo framework, no lock-on, no stamina; damage and cadence remain authored data. See `Docs/Systems/COMBAT.md` for the full record.

---

# Purpose

The player is the center of every gameplay system.

Every interaction with the world occurs through the player.

The objective of this document is to define what controlling the player should feel like rather than prescribing specific implementation details.

Throughout the game, the player should evolve from a capable adventurer into a legendary hero through progression, buildcraft, and mastery.

---

# Player Fantasy

The player is not defined by a character class.

Instead, the player gradually creates their own identity through equipment, companions, movement, abilities, and experimentation.

By endgame, two experienced players should rarely control the same.

The player's identity should emerge naturally from their build rather than from a predetermined archetype.

---

# Core Design Principles

## Responsive

Player input should always feel immediate and reliable.

The player should never question whether the game received an input.

Controls should feel responsive without appearing unnatural.

---

## Accessible

Basic combat should be easy to understand.

Players should quickly feel competent.

The depth of the game comes from combining systems rather than memorizing complex inputs.

---

## Expressive

Progression should expand what the player is capable of doing.

New movement options, new abilities, new companions, and new synergies should continually increase player expression.

---

## Adaptive

Players are encouraged to experiment.

Builds may be modified at any time.

Gameplay continues while menus are open.

Changing equipment during dangerous situations is a strategic risk rather than a restricted action.

---

## Cooperative

The player should always feel valuable during cooperative play.

Different builds should naturally complement one another.

---

# Player Capabilities

The player gradually gains access to a wide variety of capabilities.

## Movement

Core movement includes:

- Walk
- Run
- Sprint
- Jump
- Dodge
- Fall
- Swim (future)
- Climb / Mantle (where appropriate)

Movement should feel responsive and athletic.

The baseline movement should sit between the responsiveness of Fortnite and the weight of modern God of War.

Movement should always prioritize player intent.

---

## Combat

Combat combines two complementary systems.

### Weapon Combat

Weapons provide the player's primary moment-to-moment gameplay.

Combat should emphasize:

- Light attacks
- Heavy attacks
- Simple combo chains
- Sprint attacks
- Dodge attacks
- Air attacks
- Launchers
- Finishers

Execution should be straightforward while allowing room for mastery.

Weapon combat should feel somewhere between God of War and Diablo, emphasizing fluid hack-and-slash action with simple to moderately complex combo chains.

---

### Active Abilities

Abilities provide impactful tactical options.

Abilities should resemble hero abilities found in games such as Marvel Rivals or Overwatch:

- High impact
- Easy to activate
- Visually satisfying
- Distinct tactical purpose

Abilities may include:

- Burst damage
- Area control
- Movement
- Crowd control
- Defense
- Healing
- Utility
- World reactions

The complexity comes from deciding when to use abilities rather than how to execute them.

---

## Exploration

Players explore dangerous regions to:

- Gather resources
- Discover secrets
- Rescue companions
- Complete objectives
- Defeat regional threats
- Liberate regions

Exploration should reward curiosity.

---

## Gathering

Resources should feel integrated into gameplay.

Different resource types support different interaction styles.

Examples:

Small plants:

- Single interaction.

Trees:

- Multiple strikes.
- Fall apart physically.
- Drop resources.

Late-game upgrades may introduce:

- Automatic harvesting.
- Instant harvesting.
- Companion harvesting.
- Larger pickup radius.

Convenience should be earned through progression.

---

## Interaction

The player interacts with the world through a context-sensitive interaction system.

Interactions should always feel predictable.

Examples include:

- Gathering
- Talking
- Opening
- Crafting
- Activating
- Reviving
- Rescuing
- Entering portals

Interaction prompts should be clear without becoming intrusive.

---

# Progression

The player continually unlocks new capabilities.

Examples include:

- New weapons
- New companions
- New movement modules
- Passive ability progression
- Active abilities
- Relics
- Utility modules
- Equipment
- World reactions

Progression should primarily increase player expression rather than simply increasing numerical power.

---

# Movement Progression

Movement evolves throughout the game.

Potential unlocks include:

- Double Jump
- Air Dash
- Grappling Hook
- Glide
- Blink
- Charge Leap
- Ground Slam

Movement upgrades are build choices rather than mandatory progression.

Different players should move through the world differently.

---

# Combat Progression

Combat evolves in several dimensions.

Players gain access to:

- Additional weapon classes
- New combo opportunities
- Stronger abilities
- Better equipment
- Companion synergies
- Relics
- Elemental interactions

The player's increasing effectiveness should come from both stronger equipment and greater system mastery.

---

# Companion Relationship

Players begin the game without companions.

The first companion is earned early through gameplay.

Players gradually collect many companions.

Normally, only one companion accompanies the player.

A second active companion may become available as a late-game progression reward.

Companions fight autonomously, support exploration, and contribute unique synergies to the player's build.

Outside of combat, companions may perform utility actions such as transporting items back to the Hub World for storage, selling unwanted goods, or processing materials.

---

# Build Expression

A player's identity emerges from the combination of:

- Weapon
- Armor
- Companion
- Active Abilities
- Passive Ability Progression
- Movement Modules
- Utility Modules
- Relics
- Elemental Affinity

No single system should define the player's identity.

The combination of systems creates unique playstyles.

---

# Camera

The camera should prioritize readability, situational awareness, and cooperative gameplay.

Inspirations include:

- God of War
- LEGO action games
- Modern third-person action adventures

Lock-on targeting should remain optional and may evolve based on playtesting.

The camera should support both solo and cooperative play without sacrificing clarity.

---

# Input Philosophy

Every action should be intentional.

The player should never feel that controls are fighting their intentions.

The input system should support:

- Input buffering
- Responsive action queues where appropriate
- Predictable action priority
- Forgiving interaction detection

Player agency should always take priority over animation rigidity.

---

# Success Criteria

The Player system is successful when:

- Movement feels satisfying before any progression unlocks.
- Every progression reward expands player possibilities.
- Combat is immediately enjoyable but continues to deepen over time.
- Two endgame players rarely share identical builds.
- Cooperative play naturally rewards complementary builds.
- Players regularly experiment with new combinations.
- The player feels noticeably more capable after each region is liberated.
- By the end of the game, the player feels like a legendary hero while still looking forward to discovering new builds and combinations.

---

# Related Documents

- Docs/Foundations/GAME_VISION.md
- Docs/Foundations/DESIGN_PILLARS.md
- Docs/Foundations/BUILDCRAFT.md
- Docs/Systems/PLAYER_PROGRESSION.md
- Docs/Systems/COMBAT.md
- Docs/Systems/COMPANIONS.md
- Docs/Systems/WORLD_REACTION_SYSTEM.md
- Docs/Systems/ABILITY_SYSTEM.md
- Docs/Systems/EQUIPMENT.md