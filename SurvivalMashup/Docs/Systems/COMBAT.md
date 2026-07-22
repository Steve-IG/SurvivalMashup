# Combat

**Status:** Living Specification  
**Version:** 1.0  
**Owner:** Lead Combat Designer  
**Last Updated:** June 2026

---

## System Ownership

This system owns:
- Combat design goals, pacing, encounter feel, combat roles, and build-expression expectations.

This system does NOT own:
- Damage resolution, Ability execution, AI behavior, loot generation, or enemy runtime implementation.

Primary Responsibilities:
- Define how combat should feel and how combat supports exploration, progression, cooperation, and liberation.

Primary Data:
- TBD

Primary Runtime Objects:
- TBD

Published Events:
- TBD

Consumed Events:
- TBD

---

# Milestone 2 Implementation (Review Group 5 — Player Combat Foundation)

One polished melee attack was built to set the quality bar, entirely by composition on the frozen engine — **no combat/weapon/targeting/attack/combo framework, no lock-on, no stamina**. The attack remains the authored ability (`ability.player_strike` → `DamageEffect`); this review group made *swinging it* feel connected, weighty, and responsive, and never leaves the player stuck.

- **Phased swing (feel, not framework).** `PlayerCombat` now runs a small **wind-up → impact → recovery** swing, the player-side mirror of the already-approved `EnemyCombatant` telegraph. A press **always** starts a swing so the attack never feels dead: with no enemy in range the player whiffs (swing + whoosh, no impact juice); with a live enemy in range the start is gated on the ability's own validation (`AbilitySet.CanActivate` against the found target — cooldown/gates checked without committing) so a fighting player's cadence stays the authored cooldown (an off-cadence press buffers). It faces the target, plays the swing animation immediately, and commits the player (damps but never zeroes movement). The blow's **damage lands at the impact frame**, not the input frame: at impact it re-acquires the nearest enemy and activates the authored ability (which applies its `DamageEffect` and starts its cooldown). This is what makes the hit read as connected. The impact timing is the pure, unit-tested `MeleeSwing`; its phase durations are authored on the adapter and tuned to the attack clip's contact frame. (Animation-event sync is the noted future refinement — the same tech debt already recorded for the enemy telegraph.)
- **Responsiveness & control preserved.** Input buffering remains (a press just before the swing is ready still lands); movement is only damped (not locked) during the swing, so the player can keep nudging; roll and jump still interrupt a swing through the shared locomotion path; and the Attack animation blends back to locomotion early when movement continues (a `Speed`-gated animator transition), so the player never has to "finish posing." Cadence stays the authored 0.4s cooldown; damage stays the authored effect.
- **Impact presentation (juice, off event seams).** A new thin `PlayerAttackFeedback` adapter consumes `PlayerCombat`'s `Attacked` (swing) and `Impacted(hitPoint)` (connect) events only: a stronger camera impulse on connect, a one-shot impact VFX burst at the hit point (authored Epic Toon FX prefab), a brief hit-stop for weight, and swing/impact **sound hooks** (authored, optional). The enemy's existing hit flash now fires naturally on the contact frame because damage lands at impact. No presentation logic lives in gameplay; the player is an unarmed warrior, so no weapon trail applies.
- **Data ownership.** Damage, cooldown, and the effect stay authored on `ability.player_strike`; swing phase durations, buffer window, commitment, impulse, VFX, hit-stop, and sounds are authored serialized fields on the two thin adapters. Nothing combat-tuning is hardcoded in gameplay logic.

The Grunt is used only to evaluate the attack; enemy improvements are the next review group.

**Feel iteration (post-review, same review group).** Hands-on playtesting surfaced four issues, all fixed as feel/presentation, no new systems:
- **Swing retimed to the clip.** The attack clip is 1.2s but the swing was committing for only 0.32s and landing the blow at 0.1s — so the hit fired well before the fist connected and the player was free long before the animation ended. Wind-up is now 0.4s (≈ the contact frame) and recovery 0.35s, so the blow lands on contact and the player is committed for the punch.
- **Attack root motion.** The attack animation carries a forward step (a root-motion clip). `applyRootMotion` is now on, and a thin `PlayerModelEvents` relay on the model forwards the attack's root motion to the `CharacterController` (`PlayerLocomotion.ApplyRootMotion`) — so the character keeps the step instead of the model snapping back to where the clip started. Root motion is consumed only during the attack; locomotion stays velocity-driven (the relay discards root motion off-attack), so the two never fight.
  - **Invariant: exactly one `CharacterController.Move` per frame.** Root motion is *accumulated* by `ApplyRootMotion` and folded into locomotion's single gravity-carrying `Move`. It must never be applied as its own `Move`: `isGrounded` is recomputed by every `Move` call from that call's collisions alone, and because `OnAnimatorMove` runs after `Update` a second, vertical-less move would be the frame's last — clearing `isGrounded`, so a root-motion attack clip read as airborne and drove the animator `Grounded=false → Fall`, then a spurious **Land** when the swing ended. (Surfaced by the `Frank_RPG_Fighter_Combo03_1` attack clip, whose strong forward root motion triggered it nearly every swing.) Any future motion source — knockback, dashes, lunges, hazard shoves — must add into the same accumulated move rather than calling `Move` itself.
- **No more gliding.** Movement is now fully committed during the swing (commit scale 0), so the character no longer slides with planted feet; its only movement during the attack is the animation's own root motion. The Attack animator state returns to locomotion the instant movement resumes after the commitment (a low-exit-time, `Speed`-gated transition) and settles to idle otherwise.
- **Impact reads as connecting.** The hit VFX now spawns at a computed hit point on the target's near surface at chest height (not its feet), and lands on the contact frame. For frame-exact sync the swing exposes an `OnAttackContact` animation-event seam (`PlayerCombat.NotifyAnimationContact`, relayed by `PlayerModelEvents`) — author that event on the attack clip and it overrides the wind-up timer; absent it, the timer lands the blow.
- **Enemies react.** The Grunt now plays a brief hit flinch on damage (a `Hit` state driven by `EnemyAnimatorDriver` off its existing `Damaged` event). It is animation only — the enemy's attack cadence is still driven by `EnemyCombatant`'s own timers, not the animator — so a flinch never stunlocks the encounter (see `ENEMY_SYSTEM.md`).

---

# Hit Detection Architecture (Review Group 5A — canonical)

This section is the **canonical answer to one question**: *how does gameplay determine which Gameplay Objects were actually hit?* Every gameplay interaction that reaches out into the world — player melee, enemy melee, companions, projectiles, explosions, persistent hazards, cones, beams, radial effects, future boss attacks — resolves that question through the single vocabulary defined here. It is a long-term engine decision, not a combat feature. It composes with the frozen Milestone 0 engine and introduces **no new gameplay framework** (no combat/weapon/damage/targeting/hit manager): it is a small, stateless *query vocabulary* that feeds the existing Ability → Gameplay Effect pipeline.

## The problem it fixes

Before this review group, each action invented its own way to find targets: the player melee ran an omnidirectional `OverlapSphere` and hit the nearest enemy (so a blow could **connect while facing away**), the enemy strike blindly activated its ability against the one known player, hazards used Unity trigger callbacks, and interactions used yet another proximity query. Four duplicated answers to the same question. The symptoms called out in the brief — hitting while facing away, impact feeling disconnected from the animation, arbitrary timing, presentation hard to sync, and every future projectile/explosion needing its own code path — are all consequences of that missing seam. This architecture supplies the seam once.

## The canonical pipeline

The architecture separates three responsibilities that must never blur together:

```
Hit Detection            →   Hit Results            →   Gameplay Effects
(which objects, and where)    (the objects that were hit)   (what happens to them)
   HitVolume + HitDetector       IReadOnlyList<HitResult>       the ability's authored effects
```

- **Hit detection determines *what* was hit.** A `HitVolume` (authored data) describes the spatial test; a `HitDetector` resolves it, at a chosen instant, into the live Gameplay Objects inside it.
- **Gameplay effects determine *what happens*.** The caller turns each `HitResult` into an `EffectTarget` and activates the authored ability (`AbilityTargetMode.Provided`). The ability's `DamageEffect`/`HealEffect`/status/etc. decide the outcome, through the frozen Gameplay Effect pipeline. Hit detection applies **no** damage and owns **no** rule.

This is the whole model. Everything downstream is the existing engine, unchanged.

## The vocabulary (`ToyChest.Gameplay.HitDetection`)

- **`HitShape`** — the one authored knob that makes the same vocabulary describe every attack. `Sphere` (omnidirectional: explosions, hazards, auras, radial effects) and `Cone` (a frontal arc: swings, punches, kicks, thrusts, tail whips, monster swipes, beams). New shapes (capsule/swept, box) are added only when a real attack needs one — Composition Over Specialization, not speculation.
- **`HitVolume`** — the authored, reusable description of a spatial test: shape, reach (`Radius`), frontal `ConeHalfAngleDegrees`, single-vs-multi target with a `MaxTargets` cap, an optional Unity tag filter, and physics layers. Pure serializable data; it owns no outcome and no timing. Authored inline on a thin adapter today; promotable to a shared `ScriptableObject` "attack shape" asset with no change to the vocabulary once many attacks reuse one (see *Authoring workflow*).
- **`HitQuery`** — the pure, deterministic geometry (`Contains`, `WithinCone`), testable with no scene, no physics, no MonoBehaviour — the same engine-independent core `LocomotionMotor` and `MeleeSwing` have. Reach is a true 3D distance (so a tall blast or a giant's overhead slam catches a target above the origin); facing is measured on the horizontal plane the characters turn within. This is where "you cannot hit what is behind you" lives.
- **`HitResult`** — one object the volume found, plus the world contact point (for impact presentation) and squared distance. It carries the object and the geometry of the contact, nothing else. Provides the deterministic nearest-first ordering (ties broken by ordinal id — the same contract `PlayerInteractor` uses) that multi-target resolution depends on, so results never depend on physics query order (Engine Principle 17).
- **`HitDetector`** — the one thin Unity probe every action shares instead of each writing its own overlap loop: a broad-phase `Physics.OverlapSphereNonAlloc` by reach, a tag/self/liveness filter, the `HitQuery` narrow phase, dedup by object, and deterministic ordering. Allocation-free (a pooled buffer and reused list), stateless, **not** ticked and **not** a manager — a query object the caller invokes at the moment it matters.

## Animation is the activation point

The instant the query runs is the "contact frame," and that instant should come from the **animation**, not an arbitrary timer. The player swing already exposes an `OnAttackContact` animation-event seam (`PlayerCombat.NotifyAnimationContact`, relayed by `PlayerModelEvents`); hit detection runs there, so the blade's hit test fires exactly when the blade passes through its arc. When a clip has no authored contact event, the pure `MeleeSwing` wind-up timer lands the blow instead — a tuned fallback, and the documented tech debt (shared with the enemy telegraph). This is why the hit now reads as *connected*: detection, damage, hit-flash, and impact VFX all fire on the same animation frame.

## Universal reach (why one vocabulary covers everything)

The same `HitVolume` + `HitDetector`, varied only by authored data, expresses every case the brief requires — with no unique gameplay path:

| Interaction | Shape | Facing | Targets | Origin / activation |
|---|---|---|---|---|
| Player / enemy melee | Cone | frontal arc | nearest | socket/hand, on the animation contact frame |
| Cleave / spin / roundhouse | Cone (wide) | wide arc | multi | socket, on contact frame |
| Thrust / beam | Cone (narrow, long) | narrow | multi | weapon tip |
| Explosion / shockwave / radial | Sphere | omnidirectional | multi | blast centre, on detonation |
| Persistent hazard / aura | Sphere | omnidirectional | multi | volume centre, on a periodic tick |
| Projectile | Sphere (small) | — | nearest | the **projectile's moving position** each step |
| Giant / boss slam | Sphere or Cone (large) | either | multi | bone/socket, on contact frame |

A projectile needs no new concept: it is a small sphere whose origin advances along its path; when the swept origin reaches a target, the same query reports the hit (validated in tests). A persistent hazard is the same sphere, queried on an interval instead of once.

## Prototype (this review group)

Kept deliberately minimal — enough to prove the architecture on live gameplay, per the brief ("one melee attack is sufficient"):

- **Player melee now resolves through the vocabulary.** `PlayerCombat` no longer contains a bespoke overlap loop; it authors a `Cone` `HitVolume` (its reach and a frontal half-angle) and resolves it with a shared `HitDetector` at the contact frame. Because the cone is directional, the swing **can no longer connect with something behind the player** — the exact defect the brief named, fixed at the root rather than tuned around. Feel, buffering, commitment, root motion, and the impact-juice events are all unchanged.
- **Enemy melee now shares that one path.** `EnemyCombatant`'s strike resolves through the *same* `HitDetector` and a `Cone` volume, so an enemy blow is directional and misses a player who slipped out of the arc during the telegraph. The enemy is no longer privileged with a guaranteed hit on a known target; player and enemy run one hit path, not two.
- **Hazards and interactions are already the same model.** `HazardVolume` (a persistent status field) and `PlayerInteractor` (proximity discovery with the identical deterministic ordering) are the sphere/omnidirectional instances of this vocabulary; they are documented as such and were not rewritten (no need was exposed).

## Authoring workflow

The intended content pipeline is:

```
Character (Gameplay Object definition)
      ↓  owns an attack ability (authored: DamageEffect, cooldown, tags)
Animation clip (authored OnAttackContact event at the contact frame)
      ↓
Attack = ability  +  HitVolume (shape, reach, arc, filter, anchor socket)
      ↓  HitDetector resolves the volume → HitResults at the contact frame
Gameplay Effects (the ability's authored effects) apply to each hit
```

rather than "character → unique gameplay code → unique collision logic → unique effects." Reuse comes from three shared axes so hundreds of future attacks cost data, not code:

- **Shared shapes.** A handful of `HitVolume` presets ("short arc", "wide cleave", "thrust", "4 m blast") are reused across many attacks. When reuse is real, promote `HitVolume` to a shared `ScriptableObject` asset referenced by many abilities — the "weapon-defined volume" / "reusable attack definition" pattern — with no vocabulary change.
- **Shared sockets / bones.** A volume is anchored to a named transform on the rig (hand, weapon tip, tail bone), so the *same* volume follows different animations and characters. Artists reuse sockets rather than authoring a unique collision mesh per attack.
- **Shared activation seam.** One animation-event convention (`OnAttackContact`) times every melee attack; designers author the event, not code.

## Extension strategy

- **New shapes** (capsule/swept for fast blades, box for walls) extend `HitShape` + `HitQuery` — pure geometry, fully unit-tested, no downstream change.
- **New attacks** are authored data: an ability (effects) + a `HitVolume` + a socket + a contact event. No new class.
- **Projectiles / hazards / AoE** reuse `HitDetector` by moving the origin (projectile) or ticking the query (hazard); the effect side is the ability's existing effects.
- **Multiplayer / determinism.** Ordering is deterministic (distance, then ordinal id); the geometry is pure and reproducible. Server-authoritative detection replicates results; clients reconstruct through the same pure query. No wall-clock, no hash-order dependence.

## Rejected alternatives

- **Per-attack authored collider volumes (animated hitboxes).** Powerful but violates the primary objective: it forces artists/designers to build and keyframe a unique collision volume for every one of hundreds of future attacks. Rejected as an unacceptable content-authoring cost; a small library of shared, socket-anchored `HitVolume`s gives most of the fidelity at a fraction of the authoring burden. (A future `Capsule`/swept shape recovers fast-blade fidelity without per-attack volumes.)
- **Physics trigger colliders per swing (`OnTriggerEnter` weapon hitboxes).** Ties hit timing to the physics step (not the animation frame), leaks gameplay into scene collision setup, is non-deterministic in ordering, and multiplies GameObjects. Kept for *persistent* volumes (hazards) where "while inside" is the natural semantic; rejected as the *canonical* melee mechanism.
- **A Targeting/Hit/Combat Manager.** A central manager owning hit resolution would be a new gameplay framework — exactly what the brief forbids and what the frozen engine avoids. The chosen vocabulary is stateless and composed at the thin-adapter seam, like every other Milestone 1/2 behaviour.
- **Keep bespoke per-feature queries (do nothing).** Rejected: it is the status quo that produced the facing bug and four duplicated code paths, and it does not scale to projectiles/explosions/companions.
- **Resolve targets inside Gameplay Effects.** Rejected on principle: effects "should not determine targeting themselves" (`GAMEPLAY_EFFECT_SYSTEM.md`). Detection stays a caller concern feeding `AbilityTargetMode.Provided`, preserving the frozen separation.

## Technical debt & remaining risks

- **Contact-event coverage.** The player attack has an `OnAttackContact` seam but authoring the event on every clip is outstanding; until then the wind-up timer is the fallback (shared debt with the enemy telegraph).
- **Shape coverage.** Only `Sphere` and `Cone` exist. Fast/large weapons that pass a thin target between frames want a swept `Capsule`; deliberately deferred until an attack needs it.
- **Shared-asset promotion.** `HitVolume` is authored inline today; promoting it to a shared `ScriptableObject` is deferred until multiple attacks share one (avoids a speculative asset type).
- **Socket anchoring.** The prototype queries from the character transform; per-bone/socket origins are the documented next step for weapon-tip and giant-limb attacks.
- **Broad-phase shape.** Detection broad-phases with a sphere overlap sized to reach; correct for all current shapes, revisited only if a very long thin volume makes the broad phase wasteful.

## Framework confirmation

No Framework or Core change. No new manager, no new gameplay loop, no new activation model. The Gameplay Object, Ability, Gameplay Effect, Status Effect, Resource, Tag, and Event systems are untouched; hit detection is a thin, stateless query vocabulary composed at the adapter seam that feeds the existing `AbilityTargetMode.Provided` path. The Milestone 0 engine remains frozen.

---

# Attack Authoring Pipeline (Review Group 5B — canonical)

Review Group 5A defined *how gameplay finds what was hit*. This section defines *how a designer authors an attack* — the **permanent, reference workflow every future combat feature follows**. The goal is production scale: hundreds of characters, thousands of attacks, procedural and AI-generated content, years of iteration — all authored the same way, with **no gameplay code change per attack** and the Milestone 0 engine frozen. It realizes the socket anchoring, offsets, and shared preset assets that RG5A recorded as the next steps.

## The permanent workflow

Creating a new attack is four authoring steps and zero code:

```
1. Create / retarget an animation
        ↓
2. Place ONE animation event on the contact frame:  OnAttackContact
        ↓
3. Choose a HitVolume preset  (HV_Punch, HV_SwordSlashWide, HV_Explosion…)   ← shared asset, reused
        ↓
4. Author the Ability          (its Gameplay Effects = what happens on hit)
        ↓                       + anchor the volume to a socket/bone (RightHand, WeaponTip…) with an offset
      Done.
```

The engine already knows how to do the rest: on the contact event it resolves the authored volume(s) through the shared `HitDetector` (RG5A) and activates the ability against each `HitResult`. Nothing about swords, fists, claws, or magic is special — a weapon simply contributes a different authored volume.

## The vocabulary this adds (`ToyChest.Gameplay.HitDetection`)

- **`HitVolume` is now geometry only** — shape, reach, arc, target count. The **filter** (faction tag + layers) moved to a `HitFilter` supplied by the attacker, and the **anchor** (socket + offset) moved to `HitVolumeAnchor`. Splitting these three concerns is what makes one shape reusable by player, enemy, and boss without duplicated data.
- **`HitVolumeAsset`** — a shared `ScriptableObject` **preset**. One asset captures a shape once and is referenced by many attacks. A starter library lives under `Assets/Game/Content/HitVolumes/` (`HV_Punch`, `HV_SwordSlashSmall/Wide`, `HV_Kick`, `HV_ClawSwipe`, `HV_ConeShort/Wide`, `HV_BossSlam`, `HV_ExplosionSmall/Large`, `HV_ProjectileImpact`). Create more via **Assets ▸ Create ▸ ToyChest ▸ Hit Volume**.
- **`HitVolumeAnchor`** — authored placement: origin space (`Owner` transform, a humanoid `Bone`, or an assigned `Socket`), a **local offset** (in the anchor's space, so "+5 cm forward" reused across characters lands on each rig), and a facing source (`Owner` for stable melee, `Anchor` for weapon tips / muzzles). Bound once at startup, it then reads the live bone pose every query, so **the volume follows the animation automatically** — no per-attack hand-positioning.
- **`HitVolumeEmitter`** — one authored hit region = a preset (or inline shape) + an anchor + a filter. It is the unit the workflow assembles. It composes in two directions with no new system:
  - **Multi-hit** — an animation fires several `OnAttackContact` events; each re-resolves the emitter. The emitter holds no per-swing state, so double slashes, spins, whirlwinds, tail swipes, and rapid punches are *authored on the timeline*, not coded.
  - **Multi-volume** — an attack authors an **array** of emitters (a dragon's bite + two claws, a two-handed overhead smash covering two regions, a huge sword sweeping a wide + a narrow zone). Resolving the attack resolves each emitter and unions the results.
- **`HitAnchor`** — the pure, no-scene math (`ResolveOrigin`/`ResolveForward`) behind anchoring, unit-tested like `HitQuery`.

## Bone / socket anchoring and offsets

```
HitVolumeAnchor { space: HumanoidBone(RightHand), offset: (0,0,+0.1), facing: Owner }
        │  Bind(owner, animator)  →  animator.GetBoneTransform(RightHand)
        ▼
Origin  = handPosition + handRotation * offset     (follows the hand through the animation)
Forward = owner.forward                            (stable aim for melee)
```

If a rig lacks the bone (non-humanoid, unrigged prop, projectile), the anchor falls back to the owner transform — the RG5A behavior — so every object still works. `ProjectileOrigin`/`ExplosionOrigin`/`WeaponTip` are just `Socket`-space anchors pointing at an assigned child transform.

## The animation event standard

There is exactly **one** canonical melee animation event: **`OnAttackContact`**. It carries no arguments and no per-clip scripting; it means only *"resolve this attack's authored HitVolume(s) now."* Authoring a melee attack's timing is placing this one event on the contact frame. `PlayerCombat.NotifyAnimationContact` (relayed by `PlayerModelEvents`) is its seam; absent the event, the `MeleeSwing` wind-up timer is the fallback. Multiple events on one clip = multi-hit, for free.

## Weapon & character independence

- **Weapon independence.** The vocabulary names no sword, axe, spear, claw, fist, bow, or gun. A weapon contributes an authored `HitVolume` (and a socket, e.g. `WeaponTip`); a fist contributes a `Cone` from `RightHand`; a gun/bow contributes a projectile emitter. The hit code is identical.
- **Character independence.** The player's punch and the enemy's claw run the *same* construction — a cone anchored to `RightHand`, resolved by the shared detector — differing only in model, rig, animation, and filter tag. `EnemyCombatant` and `PlayerCombat` share one hit path and one authoring shape; there is no character-specific hit code. Verified live (below) and in tests.

## AI-generated content pipeline

The workflow is deliberately shaped so generated assets drop in with no hand-authoring:

```
Generate humanoid  →  Retarget a humanoid attack animation  →  Place one OnAttackContact event
      →  Assign a reusable HitVolume preset + RightHand anchor  →  Author (or reuse) an Ability  →  Done
```

No hand-authored collision meshes, no custom trigger scripts, no bespoke attack code. Because anchoring keys off **humanoid bones** (not per-model sockets) and shapes are **shared presets**, a freshly generated humanoid with a retargeted swing is immediately combat-ready. This is the property that lets the content pipeline scale to AI-generated and procedural characters.

## Future gameplay validation

Every listed future interaction is the same vocabulary, varied only by authored data (shape, anchor, filter, activation moment) — no new gameplay path:

| Interaction | Shape preset | Anchor | Activation |
|---|---|---|---|
| Player / enemy / companion melee | Cone (Punch, ClawSwipe, Slash) | hand / weapon-tip socket | `OnAttackContact` |
| Boss / giant attack | Cone or Sphere (BossSlam) | limb bone / body | contact event(s) |
| Projectile / grenade | Sphere (ProjectileImpact) | the projectile's moving transform | per motion step |
| AOE spell / explosion | Sphere (Explosion*) | blast origin | on detonation |
| Beam weapon | Cone (narrow, long) | muzzle socket, `Anchor` facing | while firing (tick) |
| Persistent hazard / trap | Sphere | volume transform | periodic tick |
| Environmental interaction | Sphere/Cone | interactable transform | on trigger |

## Non-goals honored

No weapon system, combo system, lock-on, inventory weapons, equipment overhaul, damage pipeline, combat/target/hit manager, physics-trigger framework, animation-state-machine logic, new gameplay loop, or new engine system was added. This review group added only authoring vocabulary (presets, anchors, emitters) and reusable content (a preset library); applying effects to each hit remains the existing `AbilityTargetMode.Provided` activation.

## Tests & manual verification

EditMode suite green at **393** (+8): socket-anchor origin & offset math, offset applied in anchor-local space, character-independent reuse (same offset → different origins; shared preset → same geometry, different anchors), multi-volume union, repeated (multi-contact) resolution is stateless and identical, deterministic ordering, and **no GC allocation across repeated detection**. Live, from `Bootstrap.unity` (`Tools/CoplayScripts/VerifyAuthoringWorkflow.cs`): **PASS** — player punch, player sword swing, enemy claw, giant attack, explosion, and projectile impact all resolve through the one vocabulary (`punch=1 sword=1 claw=1 giant=1 explosion=2 projHit=1 projMiss=0 multiHit=True`), and the RG5A facing guarantee still holds (`landsWhenFacing=True missesWhenFacingAway=True`).

## Technical debt & remaining risks

- **Contact-event authoring** on every clip is still outstanding (timer fallback active) — the same debt as the enemy telegraph.
- **Shape coverage** is `Sphere`/`Cone`; a swept `Capsule` for fast/large blades is deferred until an attack needs it.
- **Multi-target effect application.** The prototype activators are single-target (nearest hit). Applying an ability's effects to *many* hits at once (true AoE damage) awaits the Ability System's documented **Multiple Targets** extension or an author-set 0-cooldown per-hit activation; hit *detection* already returns the full multi-hit/multi-volume set. This is intentionally left to the frozen Ability System rather than solved with a damage pipeline here.
- **Socket assets.** Anchoring uses humanoid bones today; named non-humanoid sockets (`WeaponTip`, `ProjectileOrigin`) are supported via the `Socket` space but not yet authored on content prefabs.

## Framework confirmation

No Framework or Core change. The pipeline is authoring data (preset assets, serialized anchors/emitters) and thin reusable adapters composed at the existing seam; it feeds the frozen `AbilityTargetMode.Provided` path. The Milestone 0 engine remains frozen.

---

# Combat Feel & Debug Tooling (Review Group 5C)

RG5A defined *how* hits are found and RG5B *how* attacks are authored. This review group adds **no mechanics**: it makes the one punch feel responsive and readable, and builds the tooling needed to iterate on feel quickly. The engine remains frozen.

## The three attack stages (and what may react to each)

The single most important rule for impact readability. `PlayerCombat` reports three distinct stages, and presentation is wired to exactly one of them:

```
Attacked   — a swing started (every press, hit or miss)   → swing animation, whoosh
Contacted  — the contact frame arrived (hit or miss)      → debug tooling ONLY
Impacted   — a hit was CONFIRMED (ability committed)      → ALL impact feedback
```

**Impact feedback fires only on `Impacted`**: camera impulse, impact VFX, hit-stop, impact sound, and the target's own hit flash (which follows naturally from the damage it took). Swinging into empty air therefore produces no impact cue whatsoever. The swing-start camera shake that previously fired on every press was removed — starting a swing is not impact feedback. When wiring any new cue, ask which of the three stages it belongs to; if it represents *connecting*, it belongs on `Impacted`.

## Responsiveness

The punch was committed far too long after the blow landed. The fix separates two things that were wrongly fused:

- **Swing recovery** (`_recoveryDuration`, 0.35s → **0.12s**) — the follow-through before another swing may begin.
- **Post-impact commitment** (`_postImpactCommit`, **0.05s**, new) — how long the player stays planted *after* contact. The movement lock is now `windUp + postImpactCommit` (≈0.45s) instead of `windUp + recovery` (0.75s), so **movement returns almost immediately after the contact frame** if input continues.

Input buffering (0.18s) and cadence (the authored ability cooldown) are unchanged; the animator already exits Attack early when moving (`Speed`-gated, exit time 0.1). No combos and no animation cancelling were added — only latency was removed. Note these are *authored* values on the Player prefab: changing the C# default does nothing to an existing prefab, which must be re-authored (this bit us once — the prefab kept 0.35 until updated).

## Impact position accuracy

Impact VFX used to spawn at an *approximation* — the target's pivot, nudged up to chest height and back toward the player by authored offsets — so it drifted relative to where the fist actually landed. `HitResult.ContactPoint` now carries the **real contact point**: `Collider.ClosestPoint` from the query origin, i.e. the point on the target's surface nearest the attacking hand (and since RG5B the origin *is* the hand bone). `PlayerCombat` passes that straight through to `Impacted`, and the `_hitHeight`/`_hitInset` approximation is deleted. Non-convex mesh colliders, which do not support `ClosestPoint`, fall back to the bounding box. The hit-detection architecture is unchanged — detection simply reports better contact information.

## Player hit reactions — intentional milestone decision

**Player hit reactions are OFF.** When the player is damaged there is no flinch animation, no hit flash, and no camera shake; movement continues uninterrupted. Health still changes normally, enemy attacks still function, and this is **presentation only** (`PlayerAnimatorDriver._playerHitReactions`, default false). The reason: a flinch both interrupted movement and produced a screen cue that read like *the player's own attack connecting*, which is exactly the signal this review group is trying to make unambiguous. Re-enable the flag once damage-taken readability is designed as its own thing.

## Training Dummy

An authored Gameplay Object (`object.training_dummy`, prefab `Assets/Game/Content/Prefabs/TrainingDummy.prefab`) that exists so feel can be judged over minutes of uninterrupted punching. It is **an ordinary enemy, not a special case**:

- Same composition as the Grunt (attributes, health resource, `Actor.Enemy` tag), so it takes damage through the ordinary Ability → `DamageEffect` → Resource path and reacts exactly like an enemy: hit flash, hit animation, impact VFX, camera feedback, damage events.
- **Never attacks or moves** because it is *authored* inert — zero aggro/attack range and no abilities on its definition — not because of code.
- **Never dies** via the thin `TrainingDummy` adapter, which restores its health after each blow (plus a restore-on-depleted safety net). Its authored health pool is far larger than any single blow, so depletion cannot occur.
- No `TrainingDummyManager`, no bespoke damage path, no special-case combat code.

## Combat debugging workflow

`CombatDebugOverlay` (dev-only `ToyChest.Debugging` assembly) lives on the **`CombatDebug`** object in `VerticalSlice` and is enabled on start; press **F2** to toggle (or tick it in the Inspector; the switch is the global `CombatDebugOverlay.Enabled`). *The component must exist in the scene* — a scene without it has nothing for F2 to toggle:

- **HitVolume visualization** — the live cone arc (or sphere radius), its reach, and a facing ray, drawn from the volume's real anchored origin. Rendered in the **Scene view** (Gizmos) *and* the **Game view** (an immediate-mode GL pass, since Gizmos do not draw there).
- **Confirmed hit markers** — the last N contact points, so you can see exactly where blows landed and for how long they persisted.
- **HUD** — swing phase, contact frame, hit confirmed, targets hit, cooldown remaining, and the active hit volume's shape.

It is **strictly debug-only and read-only**: gameplay has no dependency on it (asserted by a test that the gameplay assemblies never reference `ToyChest.Debugging`), it mutates nothing, and deleting it changes no behaviour.

## The contact seam is character-agnostic (corrected)

The `OnAttackContact` standard was **only half shared**, which broke the "author an attack once, any character uses it" promise at the animation layer. The ability, effects, and hit-detection vocabulary were genuinely shared — but the component that *received* the event, `PlayerModelEvents`, was player-only, so an authored clip raised `AnimationEvent 'OnAttackContact' ... has no receiver!` on every enemy, and enemy contact timing stayed stuck on an internal timer while the player's synced to animation.

Corrected with one shared seam:

```
attack clip (one authored OnAttackContact event)
        ↓  Unity dispatches on the Animator's GameObject
AttackContactRelay            (on every character's model — player, enemy, dummy, future boss)
        ↓  GetComponentInParent<IAttackContactReceiver>()
PlayerCombat / EnemyCombatant (each resolves its own authored hit volume, now)
```

`IAttackContactReceiver.NotifyAnimationContact()` is implemented by `PlayerCombat` and `EnemyCombatant`; both ignore the call unless mid-swing, so a stray or duplicated event can never add a second blow, and with no event authored each falls back to its own wind-up timer. `PlayerModelEvents` no longer handles the event (it would have given the player model two receivers and dispatched twice) and keeps only its root-motion relay. **Add `AttackContactRelay` to the model of any new attacking character.**

## Attack direction commits instantly

Starting a swing now **snaps** the character to the direction the player is steering, with no turn-rate easing. Previously the swing eased toward its facing at `_turnSpeedDegrees`, so a swing begun while holding a new direction — especially a reversal, or a second swing fired the instant the first ended — rotated only a few degrees and struck the old way. The rule is: **the stick wins.** With movement input held, the swing commits to that direction at its start and is not re-aimed at a target on impact; with no input, the swing snaps to the target instead (aim assist for a standing attack); with neither, facing is unchanged. Verified live at a full 135° reversal landing exactly on the stick direction within a single frame.

## Presentation fixes surfaced by playtesting

- **Hit flash removed.** `HitFlash` tinted characters' renderers on damage. It never worked with the project's characters: it bound only materials exposing `_BaseColor` (URP Lit) while the stylized characters use the *Autodesk Interactive* shader (`_Color`), so it bound 0 renderers and was a silent no-op since RG2. Probing multiple colour properties made it bind, but it still did not read on these materials, so the component was **deleted** rather than kept as a cost with no benefit. Hit readability now comes from the impact cues that do work — impact VFX at the true contact point, camera impulse, hit-stop — plus the target's hit animation, and for damage *taken* the new health-bar flash. Do not reintroduce a character tint without first confirming it is visible on the actual shaders in use.
- **Attacks never interrupt jumps or rolls.** A swing cannot start while the player is rolling or airborne; the press buffers instead and fires as soon as they land or the roll recovers, so movement keeps its commitment without losing input.
- **Dead enemies are unhittable immediately.** Teardown is deliberately deferred so the death animation can play, but the corpse used to keep absorbing blows for that ~1.3s. `EnemyCombatant` now disables its colliders the moment health depletes — since hit detection resolves colliders, the corpse simply stops being found. Detection stays ignorant of health (no "is it alive?" test leaked into the query); the enemy withdraws itself from the world instead.
- **Attack bone is authored, not hardcoded.** `EnemyCombatant` hardcoded `HumanBodyBones.RightHand`, which silently mismatched any non-punch attack animation. The bone and its local offset are now serialized fields (`_attackBone`, `_attackOffset`), so **changing the attack animation to a kick means pointing the bone at a foot in the Inspector** — no code change. The player already had this (`PlayerCombat._attackBone`). Rule of thumb: whenever an attack animation changes, check that its hit volume's bone still matches the limb that strikes.

## Feel tuning methodology

The loop this tooling is built for: **punch the dummy → watch one cue → change one authored number → punch again.** Concretely — press F2 and confirm the volume sits where the fist is and the hit markers land on the surface; judge *impact timing* by whether the marker appears on the frame the fist connects (if not, author the `OnAttackContact` event on the clip rather than re-tuning the wind-up timer); judge *responsiveness* by how soon you can move and re-punch after contact (tune `_postImpactCommit`, then `_recoveryDuration`); judge *spacing* by walking to the edge of the drawn arc. Tune one value at a time, and prefer authored data over code every time.

> **Note for the current clip.** The Attack animator state plays at speed 2 and the attack clip was changed to `Frank_RPG_Fighter_Combo03_1`, so the authored 0.4s wind-up is no longer guaranteed to coincide with that clip's true contact frame. The robust fix is the RG5B standard — place one `OnAttackContact` event on the clip's contact frame, which overrides the timer — rather than guessing a new wind-up value.

## Non-goals honored

No combos, weapons, lock-on, blocking, parrying, stagger, stun, enemy AI changes, progression, skills, or new combat mechanics. No new gameplay system and no new combat framework.

## Tests & manual verification

EditMode suite green at **399** (+6): a press lands exactly one blow however short the recovery; the retimed swing returns control by 0.54s where the old timing was still committed; an animation contact event lands the blow only once and the timer cannot add a second; an unbegun swing never reports impact; the training dummy's keep-alive contract never depletes across 200 blows; and gameplay assemblies never reference the debug assembly. Live, from `Bootstrap.unity` (`Tools/CoplayScripts/VerifyCombatFeel.cs`) against the dummy: empty swing = 1 attacked / 1 contacted / **0 impacted** (no impact feedback on a whiff), a confirmed hit raises exactly one `Impacted`, sustained punching leaves the dummy alive and still registering blows.

---

# Purpose

Combat is the primary gameplay activity through which players reclaim dangerous regions.

Combat should be fast, responsive, expressive, and rewarding while supporting many different playstyles.

Combat exists to support exploration, progression, cooperation, and liberation—not as an isolated system.

---

# Combat Philosophy

Combat should reward:

- Skill
- Preparation
- Experimentation
- Teamwork
- Build diversity

Players should gradually evolve from surviving encounters to completely dominating previously dangerous enemies.

The player's increasing mastery should be both mechanical and statistical.

Combat challenge should primarily come from battlefield complexity rather than enemy durability.

Players should frequently fight groups of enemies that are individually fragile but collectively dangerous due to their numbers, positioning, and combined behaviors.

As players become stronger, they should be capable of defeating large groups of enemies quickly while still needing to prioritize threats and maintain situational awareness.

---

# Combat Pillars

## Fast and Responsive

Player input should feel immediate.

Combat should emphasize fluid movement, quick decision making, and satisfying feedback.

Responsiveness always takes priority over animation realism.

---

## Build Expression

Combat should allow many viable playstyles.

Examples include:

- Heavy melee
- Agile melee
- Archery
- Elemental magic
- Summoner
- Companion-focused
- Hybrid builds

No single build should dominate all others.

---

## Preparation Matters

Players are rewarded for preparing before dangerous expeditions.

Preparation may include:

- Equipment selection
- Companion selection
- Elemental loadouts
- Consumables
- Ability choices

Preparation should provide meaningful advantages without becoming mandatory micromanagement.

---

## Battlefield Awareness

The environment should influence combat.

Examples:

- Elevation
- Narrow passages
- Environmental hazards
- Destructible objects
- Cover
- Interactive elements

Combat arenas should encourage movement and adaptation.

---

## Cooperative Synergy

Up to four players should naturally create powerful combinations.

Examples:

- Combining elemental effects.
- Coordinating crowd control.
- Supporting allies.
- Tanking dangerous enemies.
- Finishing combo opportunities.

Solo players should never feel disadvantaged.

---

# Combat Density

Combat should generally favor:

- High enemy counts
- Low time-to-kill for common enemies
- Frequent encounters
- Continuous movement
- Ability chaining
- Crowd control
- Area-of-effect attacks

Large groups of enemies should create excitement rather than frustration.

Difficulty should come from battlefield management, enemy combinations, and encounter composition rather than excessive enemy health pools.

Elite enemies and Regional Threats intentionally break this rhythm by demanding greater focus, mechanical execution, and preparation.

---

# Combat Pacing

The game alternates between distinct combat rhythms.

Skirmishes
- Fast
- High density
- Low durability
- Momentum focused

Elite Encounters
- Moderate duration
- Tactical
- Mechanically demanding

Strongholds
- Sustained combat
- Multiple encounter types
- Resource management

Regional Threats
- Climactic encounters
- Multi-phase battles
- Ultimate test of player mastery

---

# Combat Loop

Observe

↓

Position

↓

Attack

↓

React

↓

Adapt

↓

Defeat Enemy

↓

Collect Rewards

↓

Continue Exploration

---

# Combat Expression

Player expression should expand throughout the game.

Early Game

- Basic attacks
- Dodge
- Simple abilities

Mid Game

- Weapon abilities
- Companion abilities
- Elemental interactions
- Combo attacks

Late Game

- Complex ability chains
- Advanced movement
- Crowd control
- Ultimate abilities
- Cooperative combos

The player should continuously discover new ways to solve encounters.

---

# Encounter Types

## Skirmish

Small groups of common enemies.

Purpose:

- Maintain pacing.
- Reward exploration.
- Teach mechanics.

---

## Elite Encounter

Powerful enemies requiring strategy.

Purpose:

- Test builds.
- Encourage preparation.
- Reward mastery.

---

## Stronghold

Large enemy-controlled locations.

Purpose:

- Regional progression.
- Meaningful objectives.
- Multi-stage combat.

---

## Regional Threat

The climactic encounter for a region.

Purpose:

- Conclude regional progression.
- Test everything the player has learned.
- Deliver memorable victories.

---

# Build Diversity

Players should customize combat through combinations of:

- Weapons
- Armor
- Abilities
- Passive bonuses
- Companions
- Consumables
- Elemental effects

Experimentation should always be encouraged.

---

# Progression Through Combat

Combat should naturally reinforce progression.

Players earn:

- Experience
- Loot
- Resources
- Crafting materials
- Companion growth
- Regional progress

Combat should always feel meaningful.

---

# Design Principles

Combat should:

- Feel responsive.
- Reward player skill.
- Reward preparation.
- Support many builds.
- Encourage experimentation.
- Create memorable moments.
- Scale naturally into cooperative play.

---

# Engineering Considerations

Combat systems should be modular and data-driven.

Major systems should include:

- Weapons
- Abilities
- Status effects
- Damage types
- AI behaviors
- Companion behaviors
- Buffs and debuffs
- Combo systems

New weapons, abilities, and enemies should be addable primarily through data rather than new code.

---

# Open Questions

- How many weapon classes?
- ~~Is there a stamina system?~~ **Resolved (Milestone 2, RG2): No.** A basic attack is never gated by a resource; combat is paced through cadence, recovery/animation timing, enemy behaviour, and cooldowns.
- How are ultimate abilities earned?
- How do elemental reactions work?
- How much vertical mobility should combat include?
- How should enemy aggro behave in multiplayer?

---

# Related Documents

- Docs/Systems/PLAYER.md
- Docs/Systems/PROGRESSION.md
- Docs/Systems/COMPANIONS.md
- Docs/Systems/ENEMY_SYSTEM.md
- Docs/Systems/EQUIPMENT.md
- Docs/Systems/ITEMIZATION.md
- Docs/Systems/DAMAGE_SYSTEM.md
- Docs/Systems/STATUS_EFFECT_SYSTEM.md
- Docs/Systems/ABILITY_SYSTEM.md