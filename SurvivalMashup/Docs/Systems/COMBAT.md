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

# Combat Arena, Combos & Multi-Target Combat (Review Group 5D)

RG5A–5C built one polished punch: how hits are found, how attacks are authored, and how the punch feels. RG5D turns the test area into a **combat dojo**, chains the punch into a **data-driven combo**, lets one attack hit **several targets**, and makes enemy attacks **interruptible** — all by composition on the existing HitDetection + Ability pipeline, with a single, reviewed extension to the Ability System (below). No new manager, no combat/combo/target/damage/encounter framework.

## Combat arena (the dojo)

The canonical `Assets/Game/Scenes/VerticalSlice.unity` is now a combat laboratory. Under an `Arena` root:

- **Permanent Training Dummies in formations** — the lone dummy ahead (single target), a close pair (two-target punch), a shallow arc (several adjacent targets), and staggered distances. They are ordinary enemies authored inert (zero aggro/attack range, no abilities) that never move, attack, or die (the `TrainingDummy` adapter restores health after each blow), so combo timing, reach, facing, and multi-target hits can be tested for minutes without a reset. Same composition as the Grunt — no bespoke dummy system.
- **Pursuing Grunts** — several ordinary Grunts within pursuit range so the player can fight a crowd, plus the existing far ones.
- **A visible spawn pad** — a bright floor trigger (`EnemySpawnTrigger`) the player steps onto to spawn a wave of Grunts at authored spawn points, re-arming when the player leaves. It reuses the canonical composition path (`GameplayObjectSpawner.Compose` with the injected `GameplaySceneContext`) rather than inventing a spawner — a thin trigger like `LootPickup`/`HazardVolume`, **not** an encounter/spawn manager.

## Combo architecture (data, not code)

A combo is **authored data describing an ordered sequence of existing attacks** — never a hardcoded `Punch1→Punch2→Punch3`. Three small pieces, all in `ToyChest.Gameplay.Combat`:

- **`ComboStep`** — one attack as data: its ability id, its `HitVolume`(s) + bone anchor, its animator trigger, and its timing (wind-up, recovery, continuation window). A step carries **no attack-specific behaviour** — punch, kick, or AoE ground slam is purely a function of which ability and which hit volume it references, so any of them occupies a slot identically.
- **`ComboDefinition`** — a `ScriptableObject` holding the ordered steps (Assets ▸ Create ▸ ToyChest ▸ Combat ▸ Combo). The player's `Combo_PlayerMelee` is three punch steps.
- **`ComboSequencer`** — pure, deterministic, scene-free ordering + continuation timing. It knows only *which step is active and when*; it applies no damage and has no attack-specific branches. A press starts step 0; a press during a step's wind-up (press-ahead) or its continuation window buffers the next step; at the contact frame a buffered continuation **chains immediately, skipping recovery** (no dead time); with none buffered the step recovers and the combo terminates. This is the RG5C responsiveness philosophy — buffer input, no frame-perfect press, no unnecessary recovery — generalized to a sequence.

`PlayerCombat` is the thin driver: it builds a per-step runtime (ability + bound emitters + animator trigger) and routes the sequencer's events — a step start snaps facing and raises `Attacked(animatorTrigger)`; a step's contact resolves *that step's* hit and activates *that step's* ability. With no `ComboDefinition` assigned it falls back to a single synthetic step from its inline fields, so pre-combo content is unchanged.

**Step lifecycle: committed, then a Release grace.** Each step runs `WindUp → (contact) → Recovery → (attack end) → Release`:

```
[ wind-up ] --OnAttackContact--> [ committed follow-through ] --OnAttackEnd--> [ Release grace ] --> combo ends
  committed       hit lands            committed                 free to move; a press still continues
```

- **Committed** (wind-up + recovery, `ComboSequencer.IsCommitted`): the attack owns the character. The player is rooted — input velocity is zero, momentum is dropped at the moment of commit, and only the clip's root motion translates the character (`PlayerModelEvents` forwards root motion only while committed). Any attack press buffers and chains on the first tick after contact.
- **`OnAttackEnd`** ends the commitment, and with the shipped default (`ContinuationWindow` = 0) it **also closes the combo**: the continuation window is exactly the committed attack — any press during it queues the next hit, and a press after `OnAttackEnd` starts over at hit 1. A positive `ContinuationWindow` is still supported as an optional per-step grace (the player is free to move while a press can still continue), but nothing uses it.
- **Contact windows.** A blow that spans part of a clip (the leaping finisher: flip kick → slam) uses `OnAttackContactStart` / `OnAttackContactEnd` instead of a single `OnAttackContact`. Opening the window counts as the step's contact; while it is open the step's hit volume is swept every frame and **each target is struck at most once per window**; it closes on `OnAttackContactEnd`, a chain, the commitment ending, or a cancel. Plain `OnAttackContact` is unchanged (an instant contact). `Tools/CoplayScripts/AuthorAttackContactWindows.cs` measures leaps from the clip's root height (`RootT.y`): takeoff → touchdown plus a 0.05s slam beat (the shipped finisher: 0.25s → 0.95s). An aerial window needs a body-anchored volume: the finisher uses `HV_LeapSlam` (cone 3.0m ±75°, multi-target) on the **Hips**, since the forearm-anchored punch volume rose out of its own reach mid-flip and caught the slam for only ~2 frames.
- **Commitment is a held state, never a timer.** `PlayerCombat` mirrors `IsCommitted` into `PlayerLocomotion.SetMovementCommitted` whenever it changes, so rooting can never outlive the attack (the old timed lock survived rolls and long fallbacks and stranded the player after the finisher). A roll or jump cancels the combo outright.
- **The Animator follows the same state.** `PlayerAnimatorDriver` sets an `AttackCommitted` bool; each attack state has a *single* exit to Locomotion on `AttackCommitted == false`, with no exit time. Exit-time transitions were the source of the post-attack slide: Unity tests an exit-time condition only at that normalized time, when the player was still rooted, so the "moving" exit never fired and the clip's tail played while the character walked. A fixed 80% "settle" exit also left the clip before late `OnAttackEnd` events could fire.

**Two animation events, not timing numbers — the scalable model.** The clip carries its own timing, so no attack needs hand-tuned numbers. `OnAttackContact` lands the blow; `OnAttackEnd` marks where the committed motion is done. Both flow through the shared `AttackContactRelay` / `IAttackContactReceiver` seam; enemies implement `NotifyAttackEnd` as a no-op, since their recovery is a deliberate timer-based vulnerability window.

Authoring tools (re-run after importing, trimming, or swapping a combo clip):
- `Tools/CoplayScripts/AuthorAttackEndEvents.cs` **measures** each clip's humanoid root motion (`RootT` curves) and places `OnAttackEnd` just after the root stops moving (never before contact). For example, the shipped leap clip moves until 1.88s, so it releases at 1.87s rather than after a stationary tail. Note that `ModelImporter` clip events use *normalized* time (0–1), unlike `AnimationClip` events (seconds), and pending import changes (e.g. a new trim) only take effect after reimport.
- `Tools/CoplayScripts/SyncComboTimingToClips.cs` sets each step's **fallback** timers so a timer never runs ahead of the event it backs up: wind-up just past an authored `OnAttackContact`, recovery just past the clip's end. Fallbacks only matter for clips with no events.

Adding hundreds of attacks therefore costs two events per clip (placeable automatically) and zero timing numbers. (RG5C had shortened the commitment to `wind-up + postImpactCommit` for single-punch snappiness, the original source of the slide; the `_postImpactCommit` fields are unused, kept only to avoid prefab/asset churn.)

> The animation-event path above still works for any combo step without an attack asset, but it is superseded by **limb hit windows** (next section), which the shipped combo now uses.

## Limb hit windows — the canonical melee model

Every melee attack is an **attack asset** whose damage comes from **hit windows** on the **character's own limbs**. This replaces "one hit area per attack + a single contact moment": attacks become accurate to the animation (a kick hits with the foot, a slam with the fists), one attack can hit a target several times (once per kick, once for the slam), and authoring stays data-only and largely automatic.

**The pieces (all in the existing `ToyChest.Gameplay` assembly; no engine change):**

- **`CharacterStrikeProfile`** (per character, `HitDetection`) — one strike capsule per limb role (`StrikeLimb`: right/left hand, right/left foot, head, weapon 1/2). With nothing authored it **generates the limbs from the humanoid avatar** (forearm→fist, shin→toe, neck→head), so any humanoid works with zero setup; authored shapes or weapon sockets override per limb. `_reachPadding` (default 0.1m) is the feel knob: limb hitboxes slightly larger than the visual limb so grazing contacts count, as in most action games. `PlayerCombat` adds one automatically if a character lacks it. It records each limb's pose once per frame, keeping the previous frame's.
- **Swept limbs** — `HitDetector.DetectLimbSweep` tests the capsule along its motion from last frame to this one (sub-sampled by the pure `LimbSweep`), so a fast kick cannot pass through a target between frames.
- **`AttackDefinition`** (asset, `Combat`; Assets ▸ Create ▸ ToyChest ▸ Combat ▸ Attack) — the clip, a default ability, the **commitment end**, and a list of **`HitWindow`s**. Each window: id, start/end (normalized clip time), which limbs (`StrikeLimbMask`), an optional authored area (a shockwave or spin radius, on a bone), its own ability, and a **re-hit interval**. Times are read live from the Animator state's normalized time, so they are immune to the FBX-event pitfalls (normalized-vs-seconds, trims that apply only on reimport) and do not depend on clip events at all.
- **Hit instances, not ticks** — each window keeps its own record of who it has struck (`HitWindowLedger`): a target is hit **once per window**, so three windows can hit the same enemy three times while a limb lingering inside a target never multi-hits. A window's `RehitInterval` (> 0) opts into repeated hits for genuinely continuous attacks (spins, grinders, flurries).
- **`AttackTimeline`** (pure) — turns each frame's normalized time into the open windows (span-based, so a short window is never skipped by a long frame), the step's **contact = its last window closing** (the moment a queued continuation may chain, so pressing ahead never cuts a blow off mid-strike), and the commitment end.
- **Cancel point (`AttackDefinition._cancelFrom`)** — from this normalized time to the commit end the recovery tail (getting up after a slam, resetting a stance) is optional: **moving** ends the combo and the animator blends into locomotion (the 0.12s `!AttackCommitted` exit), and **attacking** on the last step starts a fresh combo. It never applies before the last window closes (`AttackTimeline.IsCancelable`), so it can't cut a strike. A press on the last step before the cancel point is buffered (`_bufferWindow`) and fires the moment it opens. 0 = not cancellable. The proposer sets it 0.08s after the last window when the remaining tail is ≥ 0.6s (so only long recoveries; the punches stay fully committed); the authoring window shows it as a green "cancellable" bar. Shipped: Combo3 cancellable from 1.03s (was committed to 1.87s) — verified live by `VerifyFinisherCancel.cs`: stick held → running at 1.08s; attack mashed → new combo at 1.04s; all three finisher hits still land.
- **Re-entering the same state** — a fresh combo pressed right after one releases starts while the previous instance of the same Animator state is still blending out. `PlayerCombat` records that instance's normalized time at step start (`StepRuntime.StaleTime`) and ignores readings from it until the new instance begins; otherwise the new attack inherited a finished clip's time and ended on its first frame (a dead press).
- **`ComboStep._attack`** — a step with an attack asset is driven entirely by it (`PlayerCombat.TickAttackAsset`, in `LateUpdate` after animation poses the limbs): windows sweep their limbs/areas, each new target receives the window's ability through the multi-target activation, `Impacted` fires per target at its real contact point, and the new `WindowStruck(id)` event names the window. The step's legacy hit region and any clip `OnAttack*` events are ignored. Steps without an asset keep the previous behaviour.

**Authoring a new attack — Tools ▸ ToyChest ▸ Attack Authoring:** New attack from clip → pick a preview character (or *Use Player*) → **Propose windows** → scrub the timeline (the character is posed at that moment in the Scene view, active limbs drawn as red capsules) → adjust → assign the asset to a combo step. `AttackWindowProposer` analyses the clip on the character:
- **Strike frame (`AttackDefinition._strikeHint`, authoring only)** — the moment the blow lands. When set (scrub to the impact, press **Set strike frame**; drawn as a red "strike" marker), or when the clip still carries an `OnAttackContact`, the proposer picks **the limb that is mid-drive at that frame** (a second hand only if it too is mid-drive there, for genuine two-handed blows) and reports the other drives as "not the strike". This is the authoritative path: motion alone cannot always tell a sweep, parry or feint from the blow. `MigrateComboToAttackAssets` carries a clip's contact event into the strike frame before it strips the events.
- *Ground strikes without a strike frame* are scored by **snap** — how far a limb drives out in front of the hips plus how far it retracts right after. The best hand is the punch; the other hand counts only if it snaps nearly as well (merged if simultaneous, its own window if a deliberate one-two). When the other hand drives just as far but snaps less, the report flags **⚠ AMBIGUOUS** instead of guessing silently — set the strike frame. A strike's window ends at most 0.08s after full extension (a fist that lands and is then held out in guard is a strike only at impact). Feet count only if they leave the ground (a kick lifts the foot, a step doesn't).
  *Why the strike frame exists:* Combo 1 (`Attack`) is a left straight — the right hand first sweeps 0.6m across the body (0.03–0.23s), then the left drives 0.57m to centre and holds (0.25–0.30s). Snap scoring rewards the sweep (it retracts) and penalises the held straight, so the unhinted proposal was "Right punch" — wrong. The original authored `OnAttackContact` (0.276s) pointed at the left hand; the migration had discarded it. With the strike frame restored the proposal is "Left punch 0.25–0.40s".
- *Leaps* (root height rises ≥ 0.5m) — mid-flip every limb whips around the hips, so feet strike by speed while airborne and the hands at the landing slam.
- *Commitment end* — where the root stops travelling.
Every decision is written to a report in the window. Proposals are a starting point; designers adjust in the tool.

**Shipped combo** (`Assets/Game/Content/Attacks/`, migrated by `Tools/CoplayScripts/MigrateComboToAttackAssets.cs`, fallback timers by `SyncComboTimingToClips.cs`): `Attack_Player_Attack` — left straight (strike frame 0.276s); `Attack_Player_Combo2` — right uppercut; `Attack_Player_Combo3` — right kick + left kick (airborne) + slam (both fists at landing). Verified live (`VerifyAttackWindows.cs`): the finisher hits one dummy three times (right kick, left kick, slam 0.6s later) and no window ever hits twice.

**Accuracy is now real** (`VerifyLimbReach.cs` measures each limb's closest approach per attack). The old frontal cone reached 2.2m regardless of the animation; limb hits land only where the animation actually goes — e.g. the uppercut reaches a dummy at 0.9m but misses one at 1.3m by ~5cm, and a target closer than ~1m is leapt over by the finisher. Tune feel with `_reachPadding`, the windows, or the clips; a lunge/magnetism toward the target is the usual complement if engagement range should grow.

Because the sequencer operates on an abstract step, the architecture already represents `Punch → Punch → Kick → Ground Slam`: a later step simply references a kick clip + foot anchor, or an AoE ability + `HV_Explosion` volume. No projectile/AoE gameplay was built here — only the architecture that can express it.

**Cadence moved into the combo.** Because the attack ability's cooldown would otherwise block a chained hit from landing, combo-step abilities are **zero-cooldown** and cadence is owned by the sequencer's step timing. `ability.player_strike` is now 0-cooldown (reused as step 1) and `ability.player_combo2/3` were added (Provided, 0-cooldown). Each step keeps its own ability.

## Animation architecture (presentation only)

The Animator presents state; it never sequences combos. Each step names its own animator trigger (`Attack`, `Combo2`, `Combo3`), driven from `Gameplay → PlayerAnimatorDriver → Animator`. The `Combo2`/`Combo3` states mirror `Attack` (AnyState trigger in, `Speed`-gated exit to Locomotion) and currently reuse the Attack punch motion; a designer can point them at distinct clips (e.g. `Frank_RPG_Fighter_Combo01_2/_3`) in the Inspector for variety. Each step's contact still flows through the shared `OnAttackContact` / `AttackContactRelay` / `IAttackContactReceiver` seam; the sequencer knows which step is live, so the parameterless relay is unchanged.

## Multi-target hit application (the one reviewed engine extension)

Detection has always returned the full set of objects in a volume; the prototype activators only applied the ability to the nearest (RG5B tech debt). RG5D closes that:

```
HitVolume → HitDetector → HitResults [A, B, C] → apply the attack's ability to EACH valid target
```

`PlayerCombat` at a step's contact collects **all** hits across the step's emitters, **deduped cross-emitter** (one target is hit once per contact event even inside two overlapping volumes) and deterministically ordered (`HitResult.Compare`), and activates the ability against all of them. `HV_Punch` is authored `multiTarget` (cap 3), so one punch can legitimately clip two or three adjacent enemies.

**HitDetector still applies no damage** — it owns only detection, filtering, dedup, contact points, and ordering. What changed is the *targeting/activation contract*: the frozen Ability System could express only single-target activation (`AbilitySet.Activate` commits cost + cooldown once and runs the effect sequence against one `EffectTarget`), and per-target activation would have collided on the strike's cooldown. Per the Milestone 0 architectural review gate, the **smallest possible extension** was reviewed and accepted (Technical Creative Director, 2026-08-08): a new overload

```
AbilitySet.TryActivate(DefinitionId ability, IReadOnlyList<EffectTarget> targets)
```

that **validates and commits exactly once** (one cost payment, one cooldown, one `AbilityActivated` event) and then runs the definition's authored effect sequence **once per non-empty target**. This is the "Multiple Targets" extension the `AbilityTargetMode` comment already foreshadowed; it preserves the detection→effect separation and adds **no** DamageManager/AoEManager/HitApplicationManager. Self-mode rejects a target set; an empty/all-empty set is `InvalidTarget`. See `ABILITY_SYSTEM.md`.

## Per-target impact feedback

`PlayerCombat.Impacted` (still `Action<Vector3>`) is now raised **once per confirmed target**, each carrying that target's own surface contact point — so impact VFX and juice appear on every struck body, at the right place. Existing consumers (`PlayerAttackFeedback`, `CombatDebugOverlay`) needed no change, since they already spawn per point and record per call. The canonical relationship is unchanged: `Attacked` = a step began, `Contacted` = the contact frame arrived, `Impacted` = a real target was hit (now emitted per hit). No attack-start feedback was reintroduced.

## Non-goals honored

No CombatManager, ComboManager, WeaponManager, TargetingManager, DamageManager, EncounterManager, StunManager, poise/stagger system, lock-on, weapon inventory, progression, RPG stats, or new animation framework. The combo is authored data over existing abilities; multi-target is a single activation-contract extension; the arena reuses the existing spawner; enemy interruption (see `ENEMY_SYSTEM.md`) is authored attack data.

## Tests & manual verification

EditMode suite green at **429** (+30): the multi-target activation (per-target application, commit-once cost/cooldown/event, empty-skip, none-valid `InvalidTarget`, Self rejects a set, on-cooldown applies nothing); the pure `ComboSequencer` (first attack starts, continuation inside the window chains with no dead time, a press outside the window does not, deterministic 0→1→2 progression, termination after the final step, arbitrary content-agnostic step counts, the animation-contact seam); the pure `EnemyAttackCycle` (interrupt windows — see `ENEMY_SYSTEM.md`); and end-to-end content (one strike damages two Grunts in a single activation; the strike is now cooldown-free; the combo abilities are granted and zero-cooldown). Manual: play `Bootstrap.unity` into the dojo — punch a single dummy, punch a pair/arc for a two-or-three-target hit with separate contact feedback, chain Punch→Punch→Punch with early/ideal/late/no-continuation timing, fight several Grunts, step on the pad to spawn a wave, and interrupt a winding-up Grunt.

## Technical debt & remaining risks

- **Combo animation variety.** `Combo2`/`Combo3` reuse the Attack punch clip; distinct clips (and per-clip `OnAttackContact` events tuned to each contact frame) are an Inspector/content pass, deferred for visual review.
- **Contact-event authoring** on every clip remains outstanding (wind-up timer fallback active) — the standing debt since RG5A.
- **Combo cadence lives on the adapter/steps**, not yet a shared "attack cadence" asset; fine for one combo, revisit if many combos need shared pacing.
- **Enemy combos.** Only the player has a `ComboDefinition`; the model is character-agnostic, so an enemy combo is authoring, not code — not built here.

## Framework confirmation

One reviewed, minimal extension to the Ability System (the multi-target activation overload) was accepted into the otherwise-frozen Milestone 0 engine. Everything else — the combo model, the arena, the spawn trigger, enemy interruption, per-target feedback — is authoring data and thin adapters composed at the existing seams. Core and Framework are untouched.

---

# Attack Library & Authoring Validation (Review Group 5E)

RG5E asked one question: **can we keep adding varied melee attacks by authoring clips, hit windows and combo data, without gameplay code per attack?** To find out, we built a small library of distinct attacks from real Frank RPG Fighter clips through the RG5D pipeline (attack asset → combo step → Animator state). The tool was improved only where this exercise exposed a concrete gap. **Answer: yes for unarmed melee.** Nine attacks across four combos needed **zero gameplay code**. The one runtime addition is `PlayerCombat.SetCombo`, a loadout swap that is not per-attack logic. Every attack needed designer review: the proposer was right outright on 3 of 9 and partly right on 2 more (see the table).

## The library (test content, selectable in the dojo)

| Combo (`Assets/Game/Content/Definitions/`) | Steps → clip (`Frank_RPG_Fighter_*`) | Exercises |
|---|---|---|
| `Combo_PlayerMelee` (shipped, unchanged) | Combo05_1 → 05_2 → 05_3 | regression baseline |
| `Combo_Test_QuickStrikes` | Combo04_1 (R-L jabs) → Combo04_2 (L-R jabs) → Combo03_1 (right straight) | **A.** short windows, hand reach, tight arcs |
| `Combo_Test_Sweeping` | Combo02_1 (wide left hook) → Combo06_1 (inverted helicopter leg sweep, 2.7m travel) | **B.** limb travel through space, swept capsules, near/far edges |
| `Combo_Test_AerialHeavy` | AirCombo_Launcher (rising back-flip kick) → Combo02_2 (overhead smash) → Combo02_3 (diving hammer-fist + **ground shockwave**) | **C.** pose height, landing contact, group hit |
| `Combo_Test_WeaponProxy` | Combo02_2 via the **Weapon1 socket** | **D.** socket anchoring (proxy — see gaps) |

Attack assets: `Assets/Game/Content/Attacks/Attack_Fighter_*.asset`. Each step uses an existing zero-cooldown ability (`player_strike` / `combo2` / `combo3`). No stamina or resource cost. New preset: `HV_GroundShockwave` (sphere, r 2.2m, multi-target, max 8), anchored to the right hand in the slam window.

**Selecting a combo:** walk onto one of the five coloured **loadout pads** behind the spawn, at z = −4: Shipped Melee, QuickStrikes, Sweeping, AerialHeavy, WeaponProxy. `ComboLoadoutPad` (`ToyChest.Gameplay.Player`) is the loadout counterpart of `EnemySpawnTrigger`: a thin trigger that calls `PlayerCombat.SetCombo`. It is not a selection system. The F2 overlay now shows the active combo and step (read-only). *(RG5F: as shipped in RG5E the pads never fired — see "Combat Test Controls & Weapon Validation (Review Group 5F)" below for the fix and the selection rules.)* Every combo then runs the normal path: input → combo → Animator state → limb/area windows → `HitDetector` → `AbilitySet.TryActivate(id, targets)` → `Impacted` per target.

## Authoring workflow & what the exercise found

Each attack was made the tool's way: `CreateAttackAsset` → strike frame → **Propose** → `ApplyProposal` (all windows land *unverified*). Then the designer reviewed the frames and applied corrections, and the windows were played against dummies. The batch scripts call the same code paths as the window's buttons (`Tools/CoplayScripts/AuthorAttackLibrary.cs`, `AuthorAttackLibraryContent.cs`). `RenderClipContactSheet.cs` renders a clip on the Player rig as top and side frame strips, with limb-tip markers and window highlights, for reviewing proposals by eye.

| Attack | Tool result | Designer correction | Key adjustment |
|---|---|---|---|
| Combo04_1 / 04_2 | correct (two blows each) | none | — |
| Combo03_1 | correct (strike frame from the clip's own contact event) | none | — |
| Combo02_1 hook | right hand, 0.05s "punch" | widened to the full fast arc 0.18–0.32s | make the sweep's edges live |
| Combo02_2 smash | right hand 0.22–0.32s (after fix) | renamed | — |
| Combo06_1 sweep | both feet, sweep path (after fix) | ends extended 0.62 → 0.78/0.80s | legs keep kicking speed past half-peak |
| AirCombo_Launcher | air kicks **+ a hand slam** | slam removed; feet merged into one window | the clip lands in a crouch, no slam |
| Combo02_3 | air kicks + slam | kicks removed; slam = right hand + shockwave | trailing feet are not kicks |
| Weapon proxy | right hand | limb → Weapon1, ±0.02s | the baton tip travels further than the fist |

**Tool deficiencies found and fixed** (focused changes to the existing tool; no new tool):
1. **Suggested vs verified.** `HitWindow._verified` is authoring-only and never read at runtime. Proposals always land unverified. The timeline draws suggestions as outlines labelled "? suggested" and verified windows as solid bars labelled "✓". Validation counts unverified windows. Library windows were marked verified only after passing both a contact-sheet check and a live hit. The weapon proxy stays *suggested*, because the virtual baton has no mesh to check against.
2. **Validation + timing panel** (`AttackValidation`, shared by the window and the content tests).
   - It flags:
     - windows that never open, or that name no limb and no area;
     - limbs the character's strike profile lacks;
     - a commitment that ends mid-strike;
     - cancel points that do nothing;
     - unverified windows.
   - For each combo step using the attack, it also checks the Animator:
     - the state exists and plays this clip;
     - it is entered by its trigger;
     - it exits on `!AttackCommitted`;
     - consecutive steps do not share a state.
   - The **timing summary** shows startup, each contact window, the chain point, the combo-input span, the cancel point, recovery, commitment end and clip end separately.
3. **One-two jabs were merged into one blow.** "Simultaneous hands" tolerance went from 0.15s to 0.08s. The jabs here are about 0.13s apart.
4. **Hooks and spins were invisible to forward-drive scoring.** A *sweep* path was added: a limb peaking at 15 m/s or more while travelling an arc of 120° or more around the hips at real reach, with feet required to lift. With a strike frame set, a sweep counts only if its limb is the **fastest limb at that frame**. Without one, the proposer reports **⚠ SWEEP NOT PROPOSED** instead of guessing. Lesson: an off arm swinging for balance can sweep wide too, so speed at the strike frame decides which limb is the blow.
5. **Leap landing needed an exact return to start height.** Touchdown now means within 0.12m of the start height, or the bottom of the fall. Combo06_2 used to report its landing at the clip end.
6. `AvailableLimbs` missed components on prefab assets (`GetComponentInParent(true)`). The content test caught this.

**Setup the tool could not see:** the eight library clips used Unity's default root settings, which send root **rotation and height** into root motion. `PlayerLocomotion` applies root motion as planar XZ only, so in play a spin's turn and a leap's height would be silently discarded while the tool's preview, which samples the full motion, still showed them. `ConfigureAttackClipImport.cs` gives them the shipped clips' settings: bake rotation (Original) and Y (Feet) into the pose, and keep XZ as root motion. A content test enforces this. Consequence: **leaps are pose-only.** The character controller stays grounded, so jump, roll and grounded state are unaffected, and limb hit detection follows the posed (airborne) limbs.

## Verified behaviour (play mode, `VerifyAttackLibrary.cs`)

Every combo was equipped via `SetCombo` and driven through `TryAttack`. Placements: dummies ahead at 0.9 / 1.4 / 2.0 / 3.0m, behind, on the right flank, in a three-dummy arc, plus a roll at first contact.
- **No window ever struck the same target twice**, in any combo or placement.
- **Nothing hits from behind**, in any combo.
- A roll at first contact cancels with no later hits.
- The arc shows the archetypes apart:
  - jabs and the smash hit 2 of 3;
  - the hook and both sweep kicks hit 3 of 3;
  - the shockwave slam hits 3 of 3.
- Reach is honest: the hook misses at 3.0m; the sweep kick reaches 3.0m only by carrying the player there (root travel), which also covers targets *entering* the path mid-window.
- Real Grunts: three died in two Aerial/Heavy combos, with no errors.
- Measured flow (press-ahead), each step starting as the previous step's last window closes:

| Combo | Step 2 starts | Step 3 starts | Free |
|---|---|---|---|
| QuickStrikes | 0.47s | 1.23s | 2.09s |
| Sweeping | 0.33s | — | 1.46s |
| AerialHeavy | 0.41s | 0.73s | 2.15s |
| Shipped melee (unchanged) | 0.27s | 0.48s | 2.36s |

Shipped-combo regressions all pass: `VerifyAttackWindows`, `VerifyComboFixes`, `VerifyComboWindow`, `VerifyComboRooting`, `VerifyFinisherCancel`, `VerifyRoll`, `VerifyDeadEnemyUnhittable`. `VerifyAttackEnd` fails as before this group: it drives the clip-event `NotifyAttackEnd()`, which attack-asset steps intentionally ignore since the limb-window migration. It is obsolete, not a regression. *(RG5F rewrote it for the attack-asset path; it passes.)*

## Remaining gaps

- **True weapon test outstanding.** The Frank Fighter set has no weapon swings and the Player holds no weapon. The proxy proves the socket path: a `Sockets` strike shape (`WeaponProxy_Hilt` → `WeaponProxy_Tip`, 0.75m) under the right-hand bone, which follows the animation and extends reach (2.0m to 3.0m). It cannot prove a real blade's orientation or trajectory. Next: a weapon mesh with authored hilt/tip sockets and a weapon clip.
- **The dojo Player is not a `Player.prefab` instance.** It is an unpacked scene object with its own `CharacterStrikeProfile` (`_reachPadding` 0.3 versus the prefab's default 0.1). The Attack Authoring "Use Player" preview and the content tests use the prefab, so reach in preview can differ from play. Both now carry the Weapon1 socket. Re-linking or syncing them is a decision for review.
- **The proposer does not propose areas** (shockwaves) and does not analyse Weapon limbs. Both are authored by hand in the window.
- **One trigger and one state per clip.** Fine for tens of attacks; for hundreds, `PlayerAnimatorDriver` could `CrossFade` to the step's state by name instead of a trigger per clip. That is the smallest next scaling change, not built.
- Too close (0.9m), the overhead smash can land past the target (one of two runs). This is the clip's reach, not a detection fault.
- The probe scenarios use static dummies. Moving targets crossing a sweep were covered only by root travel, not by moving the targets.

---

# Combat Test Controls & Weapon Validation (Review Group 5F)

A maintenance group: make the RG5E test library dependable. No new systems, managers or attack code.

## Loadout pads — root cause and fix

**Symptom.** Walking onto any coloured pad did nothing; the combo never changed and `[ComboLoadoutPad]` never logged.

**Root cause (traced live, not assumed).** Every other link was sound: pads active, enabled `BoxCollider` triggers on Default (Default×Default collides), player tagged `Player`, `GetComponentInParent<PlayerCombat>` resolves, `SetCombo` binds correctly, nothing overwrites the combo afterwards, and the overlay reads `PlayerCombat.Combo` live. The trigger **geometry** was the fault. Each pad was a 0.1 m slab (world y 0 → 0.1), and the player's `CharacterController` rests its **skin width (0.08 m) above the floor**, so its capsule grazed the trigger by ~2 cm at its rounded tip. `Physics.OverlapCapsule` saw the overlap on every frame across the pad, but the physics step never delivered `OnTriggerEnter`. The `EnemySpawnPad` works because it is 0.3 m tall, and the same walk does fire it.

**Fix.** The visual stays a thin slab; each pad's `BoxCollider` became a **1 m-tall trigger column** (world y 0 → 1.0) over the same 1.6 × 1.6 m footprint. This is set in the scene and in `AuthorAttackLibraryContent.BuildPads` (`PadTriggerHeight`), so re-running the authoring script keeps it. A content test (`EveryLoadoutPadTriggerReachesIntoTheRestingPlayerCapsule`) requires each trigger to start at or below the resting capsule's bottom and reach at least one capsule radius into it. The old geometry fails that test (0.02 m against 0.5 m).

**Selection rules** (`PlayerCombat.SetCombo`, the one runtime seam; the pad stays a thin trigger):
- **Idle:** the combo applies at once. The next attack press uses it.
- **Mid-attack:** the swap is **deferred** until the current combo finishes or is cancelled, so the strike in flight completes intact. Previously `SetCombo` cancelled the combo, so a sweep whose root motion carried the player onto a pad would have been cut mid-blow. The latest request wins, and re-selecting the active combo clears a pending swap. The swap is applied only at idle points (the idle `Update` branch, or just before a fresh combo starts), never mid-tick. `HasPendingCombo` / `PendingCombo` expose it read-only. The F2 overlay shows `Next combo: … (after this attack)`.
- **Persistence:** the selection holds after stepping off, until another pad (or an explicit `SetCombo`) changes it. Re-entering the active combo's pad is a no-op (no rebuild, no log). Each play session starts on the Player's authored combo (`Combo_PlayerMelee`).
- The pad still calls only `SetCombo`. It goes through the normal Bootstrap composition and needs no debug path. The debug overlay only observes.

## Pad acceptance (play mode, `VerifyComboPads.cs`)

This runs entirely on the normal path. Keyboard state (W/A/S/D, Q) is queued into the Input System, so `PlayerInputBridge` → `PlayerLocomotion` walks the CharacterController onto each pad, and the attack presses run the whole combo against the lone Training Dummy at 0.9 m. Only the start of each walk is placed directly.

| Pad | Combo | Approach | Selected on pad | Kept after stepping off | Next attack played this combo's steps | Windows that struck the dummy |
|---|---|---|---|---|---|---|
| QuickStrikes | `Combo_Test_QuickStrikes` | from the south (moving N) | ✓ | ✓ | Combo04_1 → 04_2 → 03_1 | Right/Left punch ×5 |
| Sweeping | `Combo_Test_Sweeping` | from the west (moving E) | ✓ | ✓ | Combo02_1 → 06_1 | Left hook, both sweep kicks |
| AerialHeavy | `Combo_Test_AerialHeavy` | from the east (moving W) | ✓ | ✓ | AirCombo_Launcher → 02_2 → 02_3 | Rising kick, Overhead smash, Hammer slam |
| WeaponProxy | `Combo_Test_WeaponProxy` | from the south (moving N) | ✓ | ✓ | Combo02_2 (Weapon1 socket) | Weapon chop |
| Shipped Melee | `Combo_PlayerMelee` | from the north (moving S) | ✓ | ✓ | Attack → Combo2 → Combo3 | Left/Right punch, both kicks |

Also passing:
- **Re-entering** the active pad twice: no change, nothing pending.
- **Two pads in one walk** (QuickStrikes → Sweeping): the last pad wins, and Sweeping then executes.
- **A pad touched mid-attack:** the Sweeping combo's root travel carries the player across the AerialHeavy pad. `HasPendingCombo` is set, Sweeping runs to its last step uninterrupted, AerialHeavy becomes active when it ends, and the next combo plays AerialHeavy's steps.
- **Fresh session:** the session starts on `Combo_PlayerMelee` with nothing pending, and the pads work.

37/37 checks. Every check also verifies that each struck window belongs to the selected combo. The F2 overlay was checked visually on the pad, mid-attack ("Next combo") and after the attack. Note: the probe ends by restoring the combo it started with, because the shipped-combo probes assume `Combo_PlayerMelee`.

## Attack termination — `VerifyAttackEnd` rewritten

**The old probe** (RG5D) started a punch, held a movement input, called the clip-event seam `PlayerCombat.NotifyAttackEnd()`, and expected the attack to end that frame and movement to resume. **It was obsolete, as RG5E reported (confirmed):** every shipped step now has an attack asset, `NotifyAttackEnd`/`NotifyAnimationContact` are gated by `!ActiveStepUsesAttack()`, and so the call was a no-op. Re-run unchanged in RG5F, it failed: `endedOnEventFrame=False`.

**Current termination model.** `AttackTimeline` reads the step's Animator state time each `LateUpdate`. The step's contact is its **last window closing**, which is when a buffered press chains, skipping recovery. **`CommitEnd`** raises `ComboSequencer.NotifyAttackEnd`, which ends the commitment and, with no press buffered (ContinuationWindow 0), the combo. Past a **cancel point** (`CancelFrom`, never before the last window), a held move input ends the recovery early. The step's fallback recovery timer is only a backstop and must never run ahead of the clip.

**The new probe** keeps the old purpose (an exit that is exact and snappy) and tests the supported path. It runs on step 1 of all five combos, with a move input held:
- the player stays rooted through the commitment;
- a stray clip `NotifyAttackEnd()` mid-commitment is **ignored**;
- the release lands on **the exact frame** the clip crosses `CommitEnd`, or one frame after crossing the cancel point for cancellable attacks;
- the release is never before the last window and never on the timer;
- the combo ends with the commitment, and movement resumes at full speed.

It then chains the shipped combo by pressing ahead. Each step must start after the previous step's last window and before its `CommitEnd`, and the finisher must release at its `CommitEnd`.

Finally a **fault injection** run equips an in-memory clone of the shipped combo with a 0.05 s recovery timer, the documented "timer runs ahead of the event" regression. The probe must catch it, and it does: released at 0.63 against the expected 0.95.

Results: shipped 0.950 → released 0.952 on the crossing frame; QuickStrikes 0.950; Sweeping hook cancellable at 0.343 → released one frame later at 0.350; AerialHeavy 0.950; WeaponProxy 0.863. Chain: step 2 started at step 1 t=0.46, step 3 at step 2 t=0.22, finisher released at 0.952. **PASS.**

## Real weapon validation — assets assessed, not built

- **Available:** humanoid (`animationType: 3`) **sword** clips in Frank RPG Dual (`Assets/Frank_RPG_Dual/Assets/Animations/FBX Animation/Root_Motion/`: `Combo01_1`–`Combo07_*`, `Skill03_PowerAttack`, air combos; 92 of 93 clips humanoid).
- **Not available as a prop:** the only imported weapon meshes are `Sword_R` / `Sword_L`, **skinned into** `Frank_Dual_Unity_Skin.FBX` (bound to the pack's extra sword bones, which humanoid retargeting does not carry). No standalone, hand-attachable weapon prefab exists. `JC_StylizedWeapons` is present only as an unimported `.unitypackage`. Kevin Iglesias has no weapon attacks. Frank Fighter clips are unarmed.
- **Decision deferred to review.** A real test needs one of two things: deriving a prop from the baked sword mesh (with an unverified pivot/bind offset), or importing JC Stylized Weapons. Either way it also needs new Player Animator states, clip import configuration, and authored hilt/tip sockets aligned to the blade. That is content work beyond a maintenance group, so it was not invented here. **Decided at review (2026-10-19):** the next review group imports `JC_StylizedWeapons` and builds one real sword attack from a JC sword prop (on the RightHand, with authored hilt/tip sockets) and a Frank RPG Dual humanoid sword clip, through the existing attack-authoring pipeline.
- **The proxy remains the only weapon test.** It proves only the socket-shaped hit-volume path (a `Sockets` strike shape on the right hand that follows the animation). **True blade orientation and trajectory validation remains outstanding.** The proxy is unchanged.

## Regression results

- EditMode **471/471** (+1, the pad-geometry test).
- Play mode from `Bootstrap.unity`, all PASS: `VerifyComboPads`, `VerifyAttackEnd`, `VerifyAttackWindows`, `VerifyComboFixes`, `VerifyComboWindow`, `VerifyComboRooting`, `VerifyFinisherCancel`, `VerifyRoll` (launch 15, jump 12), `VerifyDeadEnemyUnhittable`, and `VerifyLeapCoverage` (the dummy is inside the finisher volume for the whole window).
- `VerifyAttackLibrary` reproduces RG5E exactly: no duplicate window hits, nothing from behind, identical flow timings, and Grunts 3/3.
- **Probes are order-sensitive.** Run the shipped-combo probes on `Combo_PlayerMelee`, and run `VerifyDeadEnemyUnhittable` early in a session. It targets the first non-dummy enemy, which earlier probes can kill.

## Follow-up 1 — multi-target hit-stop stutter (fixed)

**Report.** The Sweeping combo's second step (the leg sweep) stuttered badly when it hit several enemies.

**Measured, not guessed** (`VerifyHitStopBeats.cs`, Sweeping into one dummy, then into a three-dummy arc):
- It was **not a frame-rate drop**. The worst real frame was 7.7 ms.
- It was overlapping hit feedback. `PlayerCombat.Impacted` fires once per target, and `PlayerAttackFeedback` restarted its near-frozen global hit-stop (`timeScale` 0.06 for 0.05 s) and fired a camera impulse on every call.
- A swept limb reaches each body on a different frame. Step 2 into three dummies therefore produced **6 separate freezes in ~0.4 s** (9 for the whole combo, 81 frozen frames), with a few normal frames between each. That freeze → run → freeze pattern is the stutter. The six summed Cinemachine impulses added a jolt on top.

**Fix** (presentation only, `PlayerAttackFeedback`; gameplay and events unchanged):
- The weighty juice (hit-stop and camera impulse) fires once per **beat**: one hit window of one step, or one contact event for steps without an attack asset. The beat key comes from the existing `Attacked` and `WindowStruck` events.
- Every target still gets its own impact VFX and sound at its own contact point.
- Two kicks still read as two beats; a crowd no longer multiplies them.

**After the fix:** 3 freezes with 1 dummy and **3 with 3 dummies** (one each for the hook, right kick and left kick); 27 frozen frames. The probe passes when the arc produces no more freezes than a single target and no real frame exceeds 50 ms. (One run caught a single 54 ms Editor hitch on a frame with no hit; two reruns peaked at 8.1 and 22.7 ms.)

## Follow-up 2 — the first real weapon attack (Combo_Test_Sword)

**Assets.** `JC_StylizedWeapons` (URP) was imported from the bundle's `.unitypackage` into `Assets/JC_StylizedWeapons/`: 8 models and prefabs (sword, axe, dagger, hammer, stave, 2 bows, shield), materials, textures, and a demo scene. Nothing outside that folder changed. The clip is Frank RPG Dual `Combo03_1`, humanoid.

**Built through the existing pipeline, with no gameplay code** (`Tools/CoplayScripts/AuthorSwordAttack.cs`, idempotent):
- **Prop.** `SM_Sword_01` is a nested prefab named `Sword_Prop` under the Player's RightHand bone (`Player.prefab`; the dojo Player is an instance of it). Its colliders are removed.
- **Grip.** The grip is measured, not guessed. Frank Dual's own skinned `Sword_R` was posed on its own rig by the same clip, and the blade axis, edge and grip point were expressed in the hand's anatomical frame (wrist→knuckles, pinky→index, palm normal). The result: blade = 0.56 fingers + 0.82 across + 0.12 palm, and the grip sits at half the knuckle span. That frame is rebuilt from the Warrior rig's finger bones, so the sword sits the way the clips were animated even though the skeletons differ. An earlier guess (blade straight out of the thumb side) visibly pointed wrong on the contact sheet.
- **Strike shape.** `Weapon2` uses sockets `Sword_Hilt` (blade base) → `Sword_Tip`: 0.80 m, radius 0.07. `Weapon1` (the RG5E proxy) is untouched.
- **Clip import.** The same settings as the library clips: rotation and Y baked into the pose, XZ as root motion.
- **Attack.** `Attack_Dual_Combo03_1_Sword` was created through the Attack Authoring tool's own `CreateAttackAsset → Propose → ApplyProposal`. The proposer independently picked the right hand at 0.35–0.48 s as the striking limb and set the commit end at 0.95.
- **Window correction.** Measured from the blade tip on the Player: a slow wind-up raises the sword at the right side (0–0.30 s, ≤ 11 m/s), then the thrust drives the tip from beside the body to 1.81 m in front between 0.32 and 0.40 s (peak 45 m/s). The window is **'Sword thrust' 0.32–0.48 s on Weapon2**: the drive plus 0.08 s past full extension, the proposer's own strike-end rule. It was marked **verified** only after the contact sheet showed the window covering exactly the swing-into-thrust frames and a live hit landed.
- **Content.** The `Dual_Combo03_1` Animator state (AnyState trigger in, exit on `!AttackCommitted`), `Combo_Test_Sword` (one step, `ability.player_strike`), and a sixth loadout pad, **Sword** at x = 9 (1 m trigger column).
- **Tooling.** `RenderClipContactSheet` now accepts a full clip path, so it can render non-Fighter clips.

**Validated live** (`VerifySwordAttack.cs`, Bootstrap, the pad walked onto and every attack pressed with real keyboard input):

| Check | Result |
|---|---|
| Anchor follows the socket | `Weapon2` resolves to `Sword_Hilt → Sword_Tip` under `Sword_Prop` under `RightHand` (0.80 m) |
| Hit volume tracks the weapon | every contact point lies within 0.29 m of the hilt→tip segment (radius 0.07 + the dojo's 0.3 m reach padding) |
| Window matches the visible motion | tip at 9.6 m/s when the window opens → peak 55.7 m/s → 1.8 m/s after it closes; the tip reaches 1.76 m ahead |
| Reach | 0.9 / 1.4 / 2.0 / 2.6 m hit, 3.2 m misses |
| Facing | 0 hits behind and 0 on the left flank (the cut comes from the right) |
| At most once per window | three-dummy arc: 2 targets hit, once each |
| Shared pipeline | a real Grunt went 50 → 30 health through `ability.player_strike`, with one `Impacted` (impact VFX and hit-stop via `PlayerAttackFeedback`) |

**Scope limits.**
- The sword is **always held**, including during the unarmed combos. Hiding it per combo would need loadout-driven visuals, which don't exist and weren't added; the visible sword never strikes outside `Combo_Test_Sword`, whose window alone names `Weapon2`.
- `Combo03_1` is one step. The rest of the Dual set (mostly whirlwind spins with tip speeds of 80–120 m/s) is available for more sword attacks through the same script.
- The proposer still does not analyse weapon limbs. The blade window was placed from the measured tip curve (`AuthorSwordAttack.Analyse` / `Curve`).

## Proposal — aerial combos and juggling (stage 1 approved and built as RG5G, below; stage 2 not built)

**Why AerialHeavy steps 2 and 3 snap to the ground.** The library clips bake root **height** into the pose (RG5E): `PlayerLocomotion.ApplyRootMotion` keeps only XZ, and the CharacterController never leaves the ground. The launcher's leap is therefore pose-only. Step 2 chains when the launcher's last window closes, which is mid-flight in the pose. Its clip (`Combo02_2`, a *ground* overhead smash) starts with the feet on the floor, so the 0.06 s blend drops the character to the ground. The gameplay also forbids air attacks outright: `PlayerCombat.CanStartCombo` refuses to start a combo while `!IsGrounded`.

**What juggling needs.** Each piece builds on the existing pipeline:
1. **Real airborne attack steps (player only; the smallest first step).**
   - Add a data flag on `AttackDefinition`, for example `Aerial`: apply the clip's vertical root motion and suspend or scale gravity while the step is committed.
   - `PlayerLocomotion` would then apply root Y during such a step, so the launcher genuinely lifts the controller, and the following steps start in the air from the real position.
   - Gravity resumes when the combo ends, or on a slam step's landing window.
   - Lift the "no attack while airborne" gate for aerial steps, so a jump can open an air string.
   - **Content already exists:** Frank Fighter `AirCombo_Launcher/1/2/3` and Frank Dual `Air_Combo_Start/1/2/3`, each with a **`_0H` / `ZeroHeight`** variant. Those clips are made for exactly this: the controller supplies the height and the clip supplies the body motion. A reworked Aerial/Heavy would be Launcher → AirCombo_1 → AirCombo_2 → slam, with the sword set getting the Dual air clips the same way.
   - This touches `PlayerLocomotion`, `PlayerCombat`'s start gate and one data field: a locomotion and combat contract change, so it needs review.
2. **Juggling enemies (the bigger step).**
   - A **Launch** Gameplay Effect (vertical knock-up applied through the existing ability → effect pipeline) plus a **Juggled** status effect.
   - While juggled, the enemy's controller holds a reduced-gravity hang, its AI and attacks pause, and each air hit refreshes the hang. It recovers on landing.
   - This is the project's **first enemy hit-reaction mechanic** (RG5D excluded poise, stagger and knockback). It belongs in `ENEMY_SYSTEM.md` and the Status Effect system, and it needs explicit architecture approval.
3. **Air-hit tuning** that comes with it:
   - air-friendly hit volumes (aerial clips swing below the hips);
   - hit-stop per beat (already in place);
   - a camera that frames vertical action;
   - and an answer to whether a launched enemy can be hit by other enemies or hazards.

**Recommendation.** Build stage 1 first as its own review group. It fixes the snap and makes air strings real using authored clips, and it is testable against static dummies placed in the air. Treat stage 2 (launch and juggle) as a separate, reviewed enemy-system extension.

## Regression results (follow-ups)

- EditMode **472/472**. That includes `SwordStrikesThroughTheBladeOfARealPropOnAFrankDualClip` and the pad tests, now expecting six pads.
- Play mode (fresh Bootstrap session), results below. `VerifyComboPads` now also walks onto the Sword pad and checks that the next attack plays the sword step and window.

```
VerifyDeadEnemyUnhittable   hitsWhileAlive=1 corpseStillPresent=True hitsWhileDead=0 => PASS
VerifyAttackWindows         noWindowHitTwice=True finisherKicks=True finisherSlam=True | d=0.9m: hit1:Left punch@0.00s hit2:Right punch@0.
VerifyComboFixes            (A) pressAfterReleaseRestartsAtHit1=True | (B) finisherCommitted=1.88s freeAfterRoll=True | (C) animatorHandOf
VerifyComboWindow           (A) endsAtAttackEnd=True pressAfterEnd->hit1=True pressDuring->hit2=True | (B) finisher hits at [0.29s 0.29s 0
VerifyComboRooting          maxInputSpeedWhileAttacking=0.00 (want ~0) | speedAfterCombo=7.00 (want >3) | rooted=True resumed=True => PASS
VerifyFinisherCancel        (A) stick: released=1.04s running=1.08s animatorLeftCombo3=1.18s (was committed to 1.87s) hits=3 | (B) mash: n
VerifyRoll                  grounded=True entrySpeed=7.0 rolling=True rollLaunchSpeed=15.0 (expect ~15) jumpVVel=12.0
VerifyLeapCoverage          dummy inside finisher volume during: [0.01-end] (window 0.25-0.95s) | volume origin peaked 2.09m above the cha
VerifyComboPads             => PASS
VerifyHitStopBeats          freezes 1 dummy=3 3 dummies=3 worst real frame=7.8ms => PASS
VerifySwordAttack           => PASS
VerifyAttackEnd             => PASS
VerifyAttackLibrary         0 duplicate window hits; Grunts vs Combo_Test_AerialHeavy: 3/3 dead after 2 combos, 8 impacts, 4.6s
```

---

---

# Aerial Combos — Stage 1 (Review Group 5G)

Stage 1 of the aerial proposal above was approved: **real airborne player attack steps** on the packs' existing ZeroHeight air clips. **No enemy juggling.** It was built by composition on the existing pipeline (attack asset → combo step → Animator state → limb windows → HitDetector → ability), with one data field and two small locomotion seams. No new system, no per-attack code.

## The model: the clip supplies the body, the controller supplies the height

The Frank packs ship every air move twice:
- the full clip, whose root height (`RootT.y`) carries the jump;
- a **`_0H` / ZeroHeight** twin, with identical body motion and timing but the root held at standing height.

The difference between the two *is* the authored jump, so it is captured as data:

- **`AttackDefinition._airHeight`** (`AnimationCurve`: metres over normalized clip time; empty means a ground attack). `AuthorAirString` writes it as *full-twin root height − `_0H` root height*. `IsAerial` is true when a profile is present, and `AirHeightDelta(from, to)` gives the change over a span, never running backwards.
- **`PlayerCombat`** applies `AirHeightDelta(Timeline.Previous, Timeline.Current)` in `TickAttackAsset` each frame of a committed aerial step. Because it is a delta, a chained air step continues from wherever the player is, with no snap. The aerial hold is mirrored in `SyncCommitment` as *committed and aerial*, a held state like the rooting, so it can't outlive the attack.
- **`PlayerLocomotion`**:
  - `SetAerialHold(bool)`: gravity is held and vertical velocity zeroed while true.
  - `ApplyVerticalMotion(dy)`: folded into the frame's single `CharacterController.Move`, like root motion, so floors and ceilings still collide.
  - Gravity is now the pure `LocomotionMotor.VerticalVelocity(current, gravity, dt, grounded, held)`.
  - When the hold ends, the player falls from rest and lands through the normal Fall/Land path.
- **Start gate.** `CanStartCombo` still refuses a combo in the air *unless its first step is aerial*, so a jump can open an air string (the jump's remaining vertical speed is caught at the hold). Ground combos pressed mid-air still wait for the floor, as before.

The `_0H` clips use the library's root settings, except height is baked **based upon Original**. Feet-based baking would plant tucked air poses on the floor.

## Content: `Combo_Test_AirString` (pad **AirString**, x = 12)

`Fighter_AirCombo_Launcher_0H → _1_0H → _2_0H → _3_0H`, authored by `Tools/CoplayScripts/AuthorAirString.cs` (two phases: `Propose`, then `Execute`). The attacks went through `CreateAttackAsset → Propose → ApplyProposal`, with the proposer run on the **full-height twin** so its leap analysis sees the real height. Windows were then corrected from `SurveyLimbSweeps` limb-speed peaks and contact sheets, and marked verified after the live hits below.

| Step | Windows | Chain point / height |
|---|---|---|
| Launcher | Rising kick 0.30–0.48 s (both feet, 61 m/s) | 0.48 s, ~1.6 m up |
| AirCombo_1 | Left strike 0.36–0.45 · left spin strike 0.72–0.88 (72 m/s) · right spin strike 1.02–1.20 (hand + foot) | 1.20 s |
| AirCombo_2 | Spin kick 0.22–0.38 (both feet, 77 m/s) · right kick 0.58–0.73 | 0.73 s |
| AirCombo_3 (finisher) | Overhead swing 0.48–0.58 · **landing slam 1.21–1.33 s** with the `HV_GroundShockwave` area | lands |

**What the proposer got wrong, and why.**
- **Landing slams on standalone clips.** Each air clip is a standalone move that ends by landing, so the proposer, correctly for a lone clip, puts a landing slam last. In a string that would make every step land before chaining, so non-final steps keep only their airborne blows. This is the same call RG5E made on the Launcher.
- **Air-to-air blows went unseen.** AirCombo_1's hand strikes were missed: the leap path scores only feet, at ≥ 18 m/s.
- **"Landing" is relative to the clip.** For the finisher, the proposer's landing (when the clip returns to its *own* start height) is still 2.6 m up mid-dive, because the step begins high. `AuthorAirString` therefore times the slam from the **accumulated string height** (computed as the runtime applies it) reaching the floor. A content test caught the original 2.6 m.

**Altitude.** The string chains at 1.63 / 2.56 / 2.72 m and peaks around 3.5 m. It rides slightly higher than the original continuous take, because the chain points cut each clip's descent. The finisher's dive and gravity close the gap.

## Verified (play mode, `VerifyAirString.cs`)

The pad was walked onto and every press was a real keyboard event (Q, Space).

| Check | Result |
|---|---|
| The controller leaves the ground | full string peaks at **3.49 m**; launcher alone 1.72 m |
| No snap between steps | airborne at every chain (1.63 / 2.56 / 2.72 m); largest drop at a step change **0.00 m** |
| Gravity held exactly while an aerial step is committed | 0 mismatched frames |
| Lands, then moves | lands 0.05–0.14 s after the combo ends; moves 1.7–1.8 m in the next 0.3 s |
| Air strikes connect at altitude (a target held level with the player, just ahead, standing in for a launched enemy) | steps 2–4: left strike, left spin strike, right spin strike, spin kick, right kick, overhead swing, landing slam |
| Ground payoff | the launcher's kick hits a ground dummy; the finisher's shockwave hits 2 of 3 ground dummies around the landing |
| Stopping after the launcher | lands normally (nothing strands in the air) |
| A jump opens the string | airborne at the press, launcher starts, peak 2.12 m |
| Ground combos in the air | still refused mid-air (buffered for the floor, as before) |

Visual check: Game view captures mid-string show the character about 2 m and 3.6 m up in tucked or kicking air poses above the Grunts, not a standing pose floating.

## Limits (stage 1 by design)

- **No juggling.** Ground enemies aren't lifted, so mid-air windows only meet airborne targets. Against grounded enemies the string pays off only through the launcher kick and the finisher's shockwave. Stage 2 (Launch effect + Juggled status) remains a separate, reviewed enemy-system extension.
- **`Combo_Test_AerialHeavy` is unchanged** as the RG5E baseline. Its steps 2–3 are *ground* clips (the overhead smash, the diving hammer-fist), so they can't be made aerial without air clips, and the snap the reviewer saw is inherent to that combo. `Combo_Test_AirString` is the real air string.
- **Nothing cancels an air step except its cancel point.** Roll and jump need the floor; none of the air steps has a cancel point.
- **Hit-stop during air steps** freezes time globally, as on the ground. It is per beat (RG5F), so it doesn't accumulate.

## Tests

- EditMode **480/480** (+8):
  - `AerialAttackTests` (7 pure tests): aerial means a profile is present; ground attacks feed no height; per-frame deltas sum to the profile's change however the frames fall; time never runs backwards into height; the timeline span drives the delta; held means no gravity, and a jump's momentum is caught; released falls from rest, landing settles, and a jump still rises.
  - `TheAirStringFliesOnZeroHeightClipsAndLandsOnlyOnTheFinisher`: every step aerial on a `_0H` clip with the right import; airborne above 1 m entering each chained step; the finisher's slam opens within 1 m of the floor.
  - The pad tests now expect seven pads.
- Play mode (fresh Bootstrap session), results below:

```
VerifyDeadEnemyUnhittable   hitsWhileAlive=1 corpseStillPresent=True hitsWhileDead=0 => PASS  (rerun after the probe fix; see note)
VerifyAttackWindows         noWindowHitTwice=True finisherKicks=True finisherSlam=True | d=0.9m: hit1:Left punch@0.00s hit2:Right punch@0.00s |
VerifyComboFixes            (A) pressAfterReleaseRestartsAtHit1=True | (B) finisherCommitted=1.88s freeAfterRoll=True | (C) animatorHandOffAfte
VerifyComboWindow           (A) endsAtAttackEnd=True pressAfterEnd->hit1=True pressDuring->hit2=True | (B) finisher hits at [0.29s 0.29s 0.89s]
VerifyComboRooting          maxInputSpeedWhileAttacking=0.00 (want ~0) | speedAfterCombo=7.00 (want >3) | rooted=True resumed=True => PASS
VerifyFinisherCancel        (A) stick: released=1.04s running=1.08s animatorLeftCombo3=1.17s (was committed to 1.87s) hits=3 | (B) mash: newCom
VerifyRoll                  grounded=True entrySpeed=7.0 rolling=True rollLaunchSpeed=15.0 (expect ~15) jumpVVel=12.0
VerifyLeapCoverage          dummy inside finisher volume during: [0.01-end] (window 0.25-0.95s) | volume origin peaked 2.10m above the characte
VerifyComboPads             => PASS
VerifyHitStopBeats          freezes 1 dummy=3 3 dummies=3 worst real frame=8.1ms => PASS
VerifySwordAttack           => PASS
VerifyAirString             => PASS
VerifyAttackEnd             => PASS
VerifyAttackLibrary         0 duplicate window hits; Grunts vs Combo_Test_AerialHeavy: 3/3 dead after 2 combos, 8 impacts, 4.6s
```

**Probe fix: `VerifyDeadEnemyUnhittable`.** In one fresh session it reported `hitsWhileAlive=2 … hitsWhileDead=1`. The probe takes "the first Grunt" from an unordered `FindObjectsByType` and sweeps a 3 m sphere around it. That run's target had a live Grunt within 3 m, so the neighbour was counted both times. Exactly the target disappeared after the kill, and a direct check confirmed the corpse is not detectable. The probe now counts only hits on its own target object. Its purpose (a corpse is unhittable) is unchanged.

**Other probe hygiene fixed during RG5F/RG5G:**
- Dummies are parked on the ±25 m floor, never off it.
- `Physics.SyncTransforms()` is called right after moving CharacterController actors (`autoSyncTransforms` is off).
- Facing is set with real input, because `PlayerLocomotion` turns back toward its last move direction.
- Live Grunts are parked away from the probe origin so they can't kill the player before the next probe.

## Fix — mashing a single-step combo froze the player

**Symptom.** Mashing attack with `Combo_Test_WeaponProxy` or `Combo_Test_Sword` left the player rooted on the clip's last pose for about a second.

**Cause.** Both combos have one step, so each new press restarts the same Animator state. Every Any State → attack transition had **Can Transition To Self** off. A press that landed while the previous swing's clip was still playing set the trigger, but the Animator could not re-enter the state. The old clip ran out, and `PlayerCombat` correctly rejected its time as stale (`StepRuntime.StaleTime`). So the new step never saw its windows or its `CommitEnd`, and it stayed committed until the wind-up + recovery fallback timers ran out.

**Fix.** Any State → every attack state (and → `Roll`) now has `canTransitionToSelf = true`, so a re-press restarts the clip. `AttackValidation.ValidateStep` reports an **error** when a step's entry can't restart itself, which also covers `AttackLibraryContentTests.EveryComboValidatesAgainstThePlayerWithoutErrors`. The `Author*` content scripts now create the entries this way. Multi-step combos use distinct states, so their behaviour is unchanged.

**Verified.** The new probe `Tools/CoplayScripts/VerifyMashAndRoll.cs` mashes at 12 presses/s for 4 s. Worst committed stall while the clip isn't advancing went from 3.0 s (WeaponProxy) and 1.8 s (Sword) to 0.000 s.

**Probe note.** In an unfocused Editor the Game view doesn't render, so `WaitForEndOfFrame` never resumes and `VerifyAttackEnd` stalls. Run it with the Editor focused. `VerifyMashAndRoll` only yields `null`.

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