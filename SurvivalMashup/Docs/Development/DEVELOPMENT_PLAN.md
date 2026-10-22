DEVELOPMENT_PLAN.md (Version 1.2)
Status

Living Document

Version: 1.2 (Review Group A — Milestone 1 alignment and gap audit, 2026-10-21)

Owner: Technical Creative Director

Purpose: Operational source of truth for all implementation work.

Purpose

This document answers one question:

What should the team (human and AI) work on next?

Every implementation task should originate from this document.

Every completed review group should update this document.

Work proceeds through small, testable review groups. Each one must leave the game playable.

Documentation authority, terminology, and AI read order are defined in Docs/AI_AGENT_INDEX.md.

Current Phase
Phase

🟡 **Milestone 1 — The First Fun** (Foundations roadmap), in progress.

Milestone 0 (Foundation) is complete and architecturally frozen. The engine has been validated end-to-end, and movement and ordinary melee combat are built and verified. The rest of the First Fun loop is still missing: Hub, region, active ability, elite, crafting, companion, progression, world reaction, and in-game save. See the gap audit below.

The engine stays frozen unless a genuine architectural defect is discovered. New gameplay is built from authored content, existing systems, and thin adapters.

## Milestone Alignment (Review Group A)

`Docs/Foundations/PROJECT_ROADMAP.md` defines the intended player experience per milestone. This plan defines the operational order. Before Review Group A, this plan used the same milestone numbers for different work. The table reconciles them. The historical records below keep their original labels, unedited.

| Historical label in this plan | What it actually delivered | Where it sits on the Foundations roadmap |
|---|---|---|
| Milestone 0 — Foundation | Frozen engine: Gameplay Objects, Tags, Attributes, Resources, Abilities, Effects, Statuses, Items, Inventory, Equipment, Interactions, Events, Data Registry, Save | **Milestone 0 — Foundation.** Complete. |
| "Milestone 1 — Vertical Slice", Review Groups 3–8 (record: `MILESTONE_1_VERTICAL_SLICE.md`) | Engine validation: every M0 system exercised through authored content in `VerticalSlice.unity`; save/load determinism by test | **Engine validation**, foundational work completing Milestone 0. It is *not* the roadmap's Milestone 1 (The First Fun) or Milestone 2 (Vertical Slice). Referred to below as **EV-RG3…EV-RG8**. |
| "Milestone 2 — Gameplay Expansion", Review Groups 1–5G | Locomotion, combat feel, hit detection, attack authoring, combos, attack library, sword, aerial string, combat dojo | **First tranche of Milestone 1 — The First Fun**: the "player movement and ordinary combat" requirement plus combat authoring tooling. Referred to below as **GX-RG1…GX-RG5G**. |
| — | — | **Milestone 1 — The First Fun**: current milestone. New review groups are lettered: **Review Group A** (this audit), **B**, **C**, … |
| — | — | **Milestone 2 — Vertical Slice** (roadmap): the later, polished 30–60 minute experience. Not started. The scene is still named `VerticalSlice.unity`; that is a historical name, not this milestone. |

The `EV-` and `GX-` prefixes only disambiguate references to old records. They are not a new milestone taxonomy.

## Milestone 1 (The First Fun) — Verified Gap Audit (Review Group A)

Evidence is from the working tree at `e7702bd7`. This session ran the full EditMode suite (**485/485 pass**) and one new play-mode audit probe, booted through `Bootstrap.unity`. The probe reported **73 definitions, 27 scene participants injected, 23 objects composed, 0 console errors**, and its results are cited as "audit probe" below. Combat play-mode probes (`Tools/CoplayScripts/Verify*.cs`) were **not re-run** in this group; their results are as recorded in GX-RG5E–5G.

Status key: **Verified** (implementation plus test or play evidence) · **Unverified** (exists, not demonstrated) · **Partial** · **Missing** · **Blocked**.

| Requirement | Status | Evidence | Confidence |
|---|---|---|---|
| Canonical scene + bootstrap as a test path | **Verified** | `Assets/Editor/PlayFromBootstrap.cs` forces Play to start at `Bootstrap.unity`. `GameBootstrap` (`Assets/Game/Runtime/Boot/GameBootstrap.cs:40,116`) loads `VerticalSlice`. The audit probe confirms a clean boot, and the scene Player is a `Player.prefab` instance. `RuntimeBootstrapTests`, `GameplayObjectSpawnerTests`. | High |
| Hub World and return path | **Missing** | `Assets/Game/Scenes/Hub.unity` is an empty placeholder (`Scenes/README.md`). The only scene transition is Bootstrap → VerticalSlice. There is no travel or return code. | High |
| Handcrafted region and exploration | **Partial** | `VerticalSlice.unity` is a flat combat dojo plus EV-RG8 fixtures (audit probe: 8 dummies, 5 Grunts, Villager, 2 crates, cache, 4 hazards/fixtures, machine). It has no region identity, landmarks, points of interest, or navigation (Technical Debt: NPC/enemy navigation). | High |
| Player movement and ordinary combat | **Verified** | `LocomotionMotorTests`, `MeleeSwingTests`, `ComboSequencerTests`, `HitWindowTests`, `AerialAttackTests`, `CombatFeelTests`, `CombatContentTests`, `AttackLibraryContentTests`, `HitDetection/*` (all pass). Play probes are recorded in GX-RG5E–5G. The shipped combo is unarmed (`Combo_PlayerMelee`). | High (code); the feel is a reviewer judgment |
| One weapon | **Partial** | `SM_Sword_01` plus `Combo_Test_Sword` are selectable only from a dojo test pad (GX-RG5F, `VerifySwordAttack.cs`). There is no weapon item or equip path, and the default loadout is fists. | High |
| **Player-facing active Abilities** | **Missing** (player path) at audit; **Ground Slam implemented in Review Group B (awaiting review)** | See *Ability System — Player-Facing Status* below. Infrastructure is verified; no player-activated ability existed at the audit. Review Group B adds Ground Slam (see its record below). | High |
| Enemy faction | **Partial** | One archetype: Grunt (`Obj_Grunt`, `EnemyCombatant`, `EnemyAttackCycle`, `PursuitMotor`; `EnemyAttackCycleTests`, `PursuitMotorTests`, `CombatContentTests`). Its identity is only the `Actor.Enemy` tag plus the `HitFilter` faction. Targeting uses Unity tags (Technical Debt). Grunt waves come from `EnemySpawnTrigger`. | High |
| Elite encounter | **Missing** | No elite archetype, encounter, or reward. `Elite` has 0 hits in `Assets/Game/Runtime`. `HV_BossSlam` exists only as a hit-volume preset. | High |
| Gathering and inventory integration | **Partial** | Inventory is verified (`InventorySetTests`, `SupplyCrateLootContentTests`). Gathering is limited to one-shot crate loot and the Grunt's `item.monster_fang` drop (`LootPickup`). There are no harvestable nodes. Gathered items have no use, and there is no inventory UI (audit probe: 0 Canvas / 0 UIDocument; the only HUD is the IMGUI `PlayerHealthBar`). | High |
| Loot | **Partial** | Fixed authored drops only (`AddItemEffect`, `LootPickup`); no loot tables or variety. | High |
| Equipment | **Verified** | `EquipmentContentTests`, `InventoryEquipTests`, `EquipmentSetTests`. The audit probe equipped the Lucky Charm through `PlayerEquipmentController.Toggle()` live. UX is one toggle for all slots. | High |
| Simple progression | **Missing** | No XP, levels, unlocks, or upgrade paths (`Experience`/`Recipe` have 0 hits). Equipment is the only power source. | High |
| Crafting and useful recipes | **Missing** | No runtime code. `Docs/Systems/CRAFTING_SYSTEM.md` is a specification only. | High |
| One functional companion | **Missing** | No runtime code. `Docs/Systems/COMPANIONS.md` is a specification only. | High |
| Regional objective and completion | **Partial** | The Ancient Machine → Valley Relic gated interaction (`VerticalSliceIntegrationTests`) proves an authored, persisted objective. It is not a regional threat, has no on-screen objective, and changes nothing in the world. | High |
| Visible, persistent world reaction / restoration | **Missing** | No World Reaction / region-state runtime. Note that gameplay tags are Derived and do not persist (Technical Debt), so persistent world state must be authored as Resources or inventory. | High |
| Save/load and loop repeatability | **Partial** | `SaveManager` round-trips are verified by test (`SaveManagerTests`, `RehydrationTests`, `VerticalSliceIntegrationTests`). The **player cannot save or load**: `GameBootstrap` passes `saveJson: null` ("Save-slot selection is a future task"), and there is no save trigger or file I/O. Enemies are transient. Grunt waves can be re-fought, but crates are one-shot. | High |

**Summary.** The foundation, movement, ordinary melee, equipment, and the bootstrap test path are solid. Everything that makes the loop a loop is missing or partial: active ability, elite, progression, crafting, companion, regional objective, world reaction, Hub, and in-game save. **No prerequisite defect blocks the next task.**

## Ability System — Player-Facing Status (Review Group A)

| Layer | Status | Evidence |
|---|---|---|
| Generic infrastructure (`AbilitySet`: grant/revoke, tag gates, costs, cooldowns, Self/Provided, multi-target) | **Verified** | `AbilitySystemTests`, `AbilityCategoryTests`, `RehydrationTests` (cooldown restore). |
| Ordinary melee and combo steps | **Verified** | `ability.player_strike` / `combo2` / `combo3`: Provided target, **0 s cooldown**, granted by `Obj_Player`. They are activated per hit window by `PlayerCombat` (`PlayerCombat.cs:834,1055`). These are attacks, not active abilities. |
| Interactions as abilities | **Verified** | `InteractionSystem.cs:197-211` routes Loot, Greet, Pray, Activate Machine, and Open to object-owned abilities (`InteractionSystemTests`, content tests). |
| Enemy abilities | **Verified** | `ability.enemy_strike` via `EnemyCombatant.cs:400`. |
| **Distinct player-activated ability** | **Missing** | `Ability_SecondWind` (Utility, Self, 10 s cooldown, `HealEffect` 25) is granted when the Lucky Charm is equipped (`EquipmentContentTests`). Nothing activates it: no input action (`Player.inputactions` has only Move, Interact, Equip, Attack, Sprint, Jump, Roll), no adapter, no UI, and no test that activates it. The audit probe called `AbilitySet.TryActivate` directly. It returned **Activated**, health went 10 → 35 (max 75), the cooldown was 10 s, and an immediate retry returned **OnCooldown**. The ability works, but the player cannot reach it. |

Constraint found for the next task: `PlayerCombat.TryPress` (`PlayerCombat.cs:604-609`) checks `CanActivate` **only when a target is already in the arc**. This is correct for zero-cooldown melee. A cooldown-bearing ability routed through the same path, however, would replay its animation on a whiff while on cooldown and silently fail on a hit. The first active ability must therefore commit its cooldown at **press time**. Recorded in `ABILITY_SYSTEM.md`.

## Recommended First Active Ability — Ground Slam

**Recommendation: Ground Slam.** The player leaps or hammers into the ground, and a shockwave damages every enemy within about 2 m, including those behind or beside them, on its own cooldown. It answers crowding by Grunt waves and the elite. This is the "area attack" interplay BUILDCRAFT names, and the "first active ability" beat in FIRST_HOUR.

The repository already supports it:
- **Animation and timing:** `Attack_Fighter_Combo02_3.asset` has a verified "Hammer slam" window carrying the `HV_GroundShockwave` area (sphere, r 2.2 m, multi-target, max 8), commit end 0.95, and cancel from 0.49. GX-RG5E `VerifyAttackLibrary` recorded "the shockwave slam hits 3 of 3". The Animator state already exists on the player controller.
- **Targeting:** the Hit Detection vocabulary plus `HitFilter`. It is omnidirectional, so it differs clearly from the forward melee cone.
- **Effects:** `AbilitySet.TryActivate(id, IReadOnlyList<EffectTarget>)` commits once and applies effects per target (GX-RG5D). The existing `DamageEffect` is reused.
- **Feedback:** `PlayerCombat.Impacted` → `PlayerAttackFeedback` (VFX, hit-stop, camera impulse) on confirmed hits.
- **Real use:** the dojo's arc and pair dummy formations and the Grunt spawn pad.

Alternatives considered:
- **Second Wind:** cheapest, since the system path already works. It is a self-heal, though, so it does not change combat style. Once the activation slot exists it becomes a data-only binding; keep it as the second ability and a progression reward.
- **Dash or leap:** overlaps Roll.
- **Projectile or spell:** needs new projectile runtime.

**Reusable pattern the slam establishes** (no new framework):

`Ability input slot` → thin `PlayerAbilityActivator` (Player adapter, `PlayerInteractor`-shaped) → **activation ability** (Self; gates, cost, cooldown; committed at press) → authored **attack** (one-step `ComboDefinition` / `AttackDefinition` played through `PlayerCombat`) → `HitWindow` **area** → **impact ability** (Provided, 0 s cooldown, effects) applied to all hits via the multi-target overload → presentation from existing events.

A future ability is new authored data plus a binding: activation ability, impact ability, attack asset, and hit volume.

**Review question:** the activation/impact split uses two definitions per ability. The engine-level alternative is the reserved **Multi-stage activation** extension (`ABILITY_SYSTEM.md`). That would change the frozen Ability System and needs approval. The recommendation is the composition approach now, with the extension revisited only if the pattern proves awkward across several abilities.

## Minimum Enjoyable Loop — Sequenced Plan

The minimum loop is: Hub → region → discover and gather → encounter (combat plus active ability) → reward and progression → regional objective → visible consequence → return to Hub. This is a minimum interpretation of the roadmap, not the full FIRST_HOUR narrative. Each step below is one testable review group, listed in dependency order. Abilities stay central: B introduces the first ability, D rewards with the second, and F gates the objective on combat.

| RG | Player-facing experience | Reuse | Missing work | Depends on | Acceptance (summary) |
|---|---|---|---|---|---|
| **B — First Active Ability (Ground Slam)** | Press a dedicated button; slam hits the crowd around you; clear cooldown readout | Combo02_3 slam attack, `HV_GroundShockwave`, multi-target activation, `PlayerAttackFeedback`, dojo | Input action, `PlayerAbilityActivator`, 2 ability definitions, one-step attack, minimal cooldown HUD | — | See *Next Review Group* below |
| **C — Elite Encounter** | A tougher, readable Grunt elite (bigger, telegraphed AoE slam, more health) guarding something; defeating it is an event | `Obj_Grunt` composition, `EnemyAttackCycle` interrupt windows, `HV_BossSlam`, `EnemyCombatant` | Elite definition and prefab (data); persistent "defeated" state as an authoritative Resource on an encounter object (per the tag-persistence rule); elite drop | B (slam is the counterplay) | Elite composes from data only; its slam is telegraphed and dodgeable; death persists through save; drops its reward |
| **D — Reward and Simple Progression** | Elite and Grunt materials unlock something you feel: equip a reward and gain **Second Wind** on a second ability slot; one upgrade raises slam power | Equipment grants (abilities, attributes), `AddItemEffect`, ability slot from B | Bind slot 2 (data); one authored upgrade path (equipment tier or ability grant); a simple feedback toast | B, C | Second Wind activates from input; progression grant survives save; old Grunts measurably easier |
| **E — Gathering and Basic Crafting** | Harvest 2–3 resource nodes; craft the D reward at a workbench from gathered and elite materials | Interaction → ability → `AddItemEffect`; inventory; one-shot Resource charges | Harvest nodes (data); a minimal craft interaction consuming items (needs the anticipated **Remove Item** effect, already listed as a planned extension); 1–3 recipes | D | Crafted item is the progression reward; recipes are data; inventory consumption is all-or-nothing |
| **F — Handcrafted Region and Regional Objective** | A small authored region with landmarks; objective: defeat the elite and activate the regional machine; on-screen objective text | VerticalSlice fixtures, Ancient Machine pattern, `GameplayObjectSpawner` | Region layout pass (separate from the dojo); objective gating on elite-defeated state; minimal objective HUD | C, E | Objective completes only after the elite falls; completion persists |
| **G — Visible World Reaction** | Completing the objective visibly restores the region: corruption gone, Villager returns or opens a service | Authoritative Resource state, scene composition, Villager | Region-state object (Resource-based) and thin scene adapters toggling presentation; a returning NPC | F | Restoration is visible, survives save/load, and enemy waves stop |
| **H — Hub, Return Path, and In-Game Save** | Travel Hub ↔ region; Hub reflects restoration; save and continue | Bootstrap → scene path, `SaveManager`, `BootstrapConfiguration.SaveJson` | `Hub.unity` content; travel interaction; save trigger and file slot (fills the `saveJson: null` gap); scene-to-scene persistence | G | Full loop playable twice from a fresh boot and from a reloaded save |
| **I — First Companion** | One companion follows and fights with an ability (e.g. the same slam pattern) | Enemy behaviour pattern, ability pattern from B, `HitFilter` factions | Companion definition, follow and assist adapter, relationship-based targeting (replaces Unity-tag targeting debt) | B, H | Companion uses the ability framework unchanged; persists across Hub ↔ region |

Ordering rationale: B then C complete the combat core, and the elite needs the slam as counterplay. D and E give the encounter a meaningful reward, and progression comes before crafting so crafting has something worth making. F and G turn the dojo into a region with consequence. H closes the loop. The companion is last because it depends on the most systems. It could move before H if the reviewer prefers. Re-evaluate fun after B, C, and D, per the roadmap ("if this milestone is not fun, revisit the design").

## Review Group B: First Player Active Ability (Ground Slam)

**Implemented; awaiting review.** The acceptance results are in the *Review Group B* entry under *Recently Completed*. Two criteria were met differently from the text below:
- Criterion 2: movement-state refusals (rolling, airborne, or committed mid-attack) are buffered and then dropped by the adapter. They publish no `AbilityActivationFailed`. Only the Ability System's own refusals (cooldown, not granted, gates, cost) publish it.
- Criterion 7: the "arc formation" check placed three dummies front, side, and behind, to prove the slam is omnidirectional.

Scope: one ability, one input slot, minimal HUD. There is no ability catalogue, no stamina or new resource, no evolution, and no new framework. Engine Core and Framework are unchanged.

Acceptance criteria:
1. **Input and activation:** a new `Ability1` action (proposed: Mouse Right / Keyboard R / Gamepad Right Shoulder) on `Player.inputactions` reaches a thin `PlayerAbilityActivator` through `PlayerInputBridge`. The activator binds slot → ability id by authored data, with no ability-specific branches.
2. **Validation:** activation is refused (with `AbilityActivationFailed`) while on cooldown, rolling, or committed mid-attack. Airborne is refused unless authored otherwise. A refused press plays no animation. A press during melee recovery buffers exactly as `TryAttack` does.
3. **Cooldown:** authored on the activation ability (starting value 6 s) and **committed at press**, so a whiff costs the cooldown too. It ticks through `AbilitySet`, survives save/load via the existing `RestoreCooldown`, and has no resource cost.
4. **Targeting:** the slam window's `HV_GroundShockwave` hits every live enemy in radius regardless of facing. The Player and Villager are excluded by `HitFilter`; Training Dummies and Grunts are hit. A single target is never struck twice per slam.
5. **Effects:** one commit applies the impact ability's `DamageEffect` to every hit via `TryActivate(id, targets)`. The damage is authored, starting at about 1.5× a punch.
6. **Presentation:** the slam animation plays. A ground VFX burst always appears at the slam frame. Hit-stop and shake occur only on a confirmed hit. A minimal HUD shows the ability's name or icon and remaining cooldown, alongside `PlayerHealthBar`.
7. **Real in-game use:** from `Bootstrap.unity`, a new `VerifyGroundSlam.cs` probe drives real input. Checks: a slam hits all 3 dummies of the arc formation, damages a Grunt wave, an immediate re-press is refused on cooldown, it works again after the cooldown, and ordinary combos are unaffected.
8. **Tests:** EditMode content test (definitions, grant, cooldown commit on whiff, multi-target damage) plus an activator unit test. Full suite green; existing combat probes still pass.
9. **Docs:** `ABILITY_SYSTEM.md` (player activation pattern), `COMBAT.md` (slam), and this plan.

Grant source for B: `Obj_Player` grants it initially. D moves or extends ability grants into progression.

---

# Historical Review Group Records

The tables and records below are kept as written at the time. Their milestone labels are historical; see *Milestone Alignment* above.

Gameplay Expansion progress (historical label "Milestone 2"; GX-RG*)
Review Group	Focus	Status
Review Group 1	Core Action Foundation (combat sandbox)	✅ Complete (awaiting review)
Review Group 2	Core Game Feel & Combat Polish	✅ Complete (awaiting review)
Review Group 3	Core Locomotion & Character Feel	✅ Complete (awaiting review)
Review Group 4	Locomotion Feel Iteration	✅ Complete (awaiting review)
Review Group 5	Player Combat Foundation	✅ Complete (awaiting review)
Review Group 5A	Hit Detection Architecture	✅ Complete (awaiting review)
Review Group 5B	Asset-Driven Hit Detection & Authoring Workflow	✅ Complete (awaiting review)
Review Group 5C	Combat Feel Iteration & Debug Tooling	✅ Complete (awaiting review)
Review Group 5D	Combat Arena, Combos & Multi-Target Combat	✅ Complete (awaiting review)
Review Group 5E	Attack Authoring Validation & Combo Library	✅ Complete (awaiting review)
Review Group 5F	Combat Test Controls & Weapon Validation	✅ Complete (reviewed; follow-ups done)
Review Group 5G	Aerial Combos — Stage 1 (airborne player steps)	✅ Complete (awaiting review)

Architecture changes are only acceptable when a genuine architectural defect is discovered.

Engine Validation progress (historical label "Milestone 1 — Vertical Slice"; EV-RG*)
Review Group	Focus	Status
Review Group 3	Player Foundation	✅ Complete
Review Group 4	Interactive Gameplay Loop	✅ Complete
Review Group 5	Autonomous World Actors	✅ Complete
Review Group 6	Equipment & Passive Character Progression	✅ Complete
Review Group 7	Health, Damage & Environmental Hazards	✅ Complete
Review Group 8	Vertical Slice Integration & Polish	✅ Complete (awaiting certification)
Engine Validation Sprint (historical, complete)
Sprint Goal (historical)

Complete the Vertical Slice by validating every major gameplay system already implemented during Milestone 0.

The objective is engine validation, not feature expansion.

(The current sprint goal is Milestone 1 — The First Fun; see *Next Review Group* above.)

Priority 0 — Project Setup

(unchanged)

Priority 1 — Engine Runtime

(unchanged except update Current Status where appropriate)

All runtime foundation work is considered complete.

Future work extends the runtime rather than redesigning it.

Priority 2 — Vertical Slice Implementation

Milestone 1 implementation proceeds through focused review groups.

Each review group must:

validate existing engine capabilities
avoid speculative framework work
remain narrowly scoped
pause for architectural review before continuing
Review Group 3 — Player Foundation

Status

✅ Complete

Validated:

Player GameplayObject
GameplayObjectSpawner
Input bridge
Basic movement
Third-person camera
Scene composition
Player authored assets
Review Group 4 — Interactive Gameplay Loop

Status

✅ Complete

Validated:

Interaction
Gameplay Effects
Inventory
Resources
Save / Load
Authored loot loop
Review Group 5 — Autonomous World Actors

Status

✅ Complete

Validated:

NPC GameplayObjects
Autonomous movement
NPC interaction
NPC persistence
Deterministic behavior
Thin Unity AI adapters
Review Group 6 — Equipment & Passive Character Progression

Status

✅ Complete (awaiting review)

Objective:

Validate that Equipment modifies gameplay entirely through existing capability systems.

Validated:

Equipment pickup (Equipment Cache reuses the loot loop)
Equip / Unequip (player equip caller: InventoryEquip + PlayerEquipmentController)
Equipment persistence (equipped loadout + all grants round-trip through Save)
Equipment-granted attributes (+Movement Speed, +Maximum Health → bound Current Health max)
Equipment-granted gameplay tags (Equipment.Swift, Equipment.Lucky)
Equipment-granted abilities (Second Wind)
Passive status effects (Lucky Regen, infinite periodic heal)

Constraints (honored):

No combat
No new managers
No framework redesign
Review Group 7 — Health, Damage & Environmental Hazards

Status

✅ Complete (awaiting review)

Objective:

Validate health, damage, healing, status effects, and environmental gameplay without introducing combat systems.

Validated (entirely through existing systems + authored data):

Damage — periodic DamageEffect via a timed status reduces Current Health correctly
Healing — instant HealEffect (Healing Shrine) and periodic HealEffect (Campfire) restore Current Health
Maximum Health interactions — heals clamp at the attribute-bound maximum; no overshoot
Death — the existing Resource Depleted transition is the death signal (no death system)
Respawn — a thin caller restores the player (health refilled, statuses cleared) at an authored spawn point
Damage-over-time — Poison / Spikes periodic statuses expire correctly
Healing-over-time — Warmth periodic status expires correctly
Timed status effects — begin (granted tag applied) and end (tag revoked, ticking stops) correctly
Save/load — health reduced by a hazard and a mid-duration status (remaining duration + periodic accumulator) round-trip deterministically and keep ticking to the same outcome after reload

Constraints (honored):

No enemy AI combat
No weapons
No animation systems
No combat framework
No CombatManager / DamageManager / HealthManager / RespawnManager / HazardManager
No trigger framework and no new gameplay loops
Review Group 8 — Vertical Slice Integration & Polish

Status

✅ Complete (awaiting certification)

Objective:

Demonstrate the complete gameplay experience using only the validated engine.

Validated (one cohesive slice, canonical scene `Assets/Game/Scenes/VerticalSlice.unity`):

Handcrafted slice composed entirely by `GameplayObjectSpawner`
End-to-end gameplay objective (equipment-gated Ancient Machine → Valley Relic), authored, no quest framework
Exploration, Villager greet, loot (Supply Crate, Wooden Crate), Equipment Cache
Equipment (equip → attributes, tags, ability, passive status)
Environmental hazards (Spike Trap, Poison Pool), Healing (Campfire, Healing Shrine)
Damage, Death, Respawn
Save / Load determinism across the whole run
Manual QA (play-mode boot: 64 definitions, 12 participants, zero errors)
Documentation cleanup and technical-debt review
Duplicate `VerticalSlice` scene removed (drift resolved)

No new engine architecture. No speculative systems. No new framework concepts.

Current AI Task
Milestone 1 (The First Fun), Review Group A (Milestone 1 Alignment & Verified Gameplay Gap Audit): complete, paused for review. Documentation and audit only; no gameplay changed.

- Milestone terminology reconciled (see *Milestone Alignment*). The operational docs now follow the Foundations roadmap numbering, and historical records are kept verbatim.
- Verified gap matrix, Ability System player-facing status, Ground Slam recommendation, and sequenced loop plan B–I are recorded above.
- EditMode **485/485**. A play-mode audit probe booted from Bootstrap: clean boot, 23 objects composed, 0 errors. Second Wind activates when called directly, but no player input reaches it.
- Proposed next: **Review Group B — First Player Active Ability (Ground Slam)**.

Historical review-group task log (newest first; labels as written at the time):

Milestone 2, Review Group 5G (Aerial Combos — Stage 1) — complete, paused for review.

- **Real airborne attack steps.** `AttackDefinition._airHeight` (authored as the full clip's root height minus its `_0H` twin's) drives the controller's height while an aerial step is committed. `PlayerLocomotion.SetAerialHold` / `ApplyVerticalMotion` (gravity held), with `LocomotionMotor.VerticalVelocity` pure. An aerial opener may start in the air.
- **`Combo_Test_AirString`** (Fighter AirCombo Launcher → 1 → 2 → 3 on `_0H` clips, seventh pad) via `AuthorAirString.cs`. The proposer ran on the full-height twins; windows were corrected from limb-speed peaks, and the finisher's slam is timed from the accumulated string height.
- **Validated live** (`VerifyAirString.cs`): peak 3.49 m; airborne at every chain with 0.00 m drop at step changes; gravity held exactly while committed; lands and moves; air windows hit a target at altitude; the shockwave hits ground dummies; a jump opens the string; ground combos still refuse to start in the air.
- **Not built (stage 2):** enemy launch and juggle. `Combo_Test_AerialHeavy` is unchanged (its steps 2–3 are ground clips).
- EditMode 480/480; all combat probes pass.

See `COMBAT.md` (RG5G). Awaiting review.

Milestone 2, Review Group 5F (Combat Test Controls & Weapon Validation) — complete, paused for review.

- **Loadout pads fixed.** The pad triggers were 0.1 m slabs, and the CharacterController capsule rests 0.08 m above the floor (its skin width), so the capsule only grazed them and the physics step never reported a contact. They are now 1 m trigger columns (scene + `AuthorAttackLibraryContent`), guarded by a content test. `PlayerCombat.SetCombo` now **defers** a swap requested mid-attack until the combo ends, so a strike is never cut; the overlay shows "Next combo".
- **Pads verified in play mode** with `VerifyComboPads.cs`, which drives real keyboard input. All five pads select from four directions, and the next combo executes that combo's own steps and windows. Re-entry, two pads in sequence, mid-attack touch and a fresh session also pass (37/37).
- **`VerifyAttackEnd` rewritten** for the attack-asset termination path:
  - exact release frame at `CommitEnd` or the cancel point;
  - stray clip events are ignored;
  - chaining happens at the last window;
  - it has a fault-injection self-check. **PASS.**
- **Real weapon test not built.** Humanoid Frank RPG Dual sword clips exist, but no standalone weapon prop does: the swords are skinned into the Dual character, and JC Stylized Weapons is not imported. **Decided at review:** the next review group imports the JC Stylized Weapons unitypackage and builds one real sword attack from a JC sword prop and a Frank RPG Dual humanoid sword clip. Until then the proxy remains and validates only the socket path.
- EditMode **471/471**. All combat verification probes pass.

See `COMBAT.md` (RG5F). Awaiting review.

RG5F follow-ups (after review):
- **Hit-stop stutter fixed.** Measured: no frame drop; per-target hit-stop and shake chained into 6 separate freezes in 0.4 s on a multi-target sweep. Hit-stop and camera impulse now fire once per beat (one window of one step); VFX and sound stay per target. `VerifyHitStopBeats.cs`.
- **First real weapon attack.** JC Stylized Weapons imported. `SM_Sword_01` is in the right hand with the grip measured from Frank Dual's own sword, and `Weapon2` uses blade sockets. Frank Dual `Combo03_1` gives a verified 'Sword thrust' window. `Combo_Test_Sword` has a sixth pad. Validated live with `VerifySwordAttack.cs` (anchor, trajectory, window alignment, reach, facing, once per window, Grunt damage).
- **Aerial combos and juggling: proposal only** (`COMBAT.md`). Stage 1 is real airborne player steps using the existing `_0H`/`ZeroHeight` air clips. Stage 2 is enemy launch and juggle, the first enemy hit-reaction mechanic, which needs architecture review.
- EditMode 472/472; all combat probes pass.


Milestone 2, Review Group 5E (Attack Authoring Validation & Combo Library) — complete, paused for review.

Validated the attack-authoring workflow by building a test library of **nine attacks in four data-authored combos** from real Frank RPG Fighter clips, with **no per-attack gameplay code**:
- **Quick Strikes**: Combo04_1 → 04_2 → 03_1.
- **Sweeping**: Combo02_1 → 06_1.
- **Aerial/Heavy**: AirCombo_Launcher → 02_2 → 02_3, ending in a shockwave group hit.
- **Weapon Proxy**: a Weapon1 socket strike shape.

They're selectable in the dojo through five **loadout pads**. `ComboLoadoutPad` is a thin trigger calling the one new runtime seam, `PlayerCombat.SetCombo`.

Attack Authoring tool fixes, each driven by a concrete failure in this exercise:
- **suggested vs verified** windows;
- a shared **validation + timing panel** (`AttackValidation`), also enforced by content tests;
- one-two jabs no longer merged into one blow;
- hooks and spins proposed through a new **sweep** path (fastest limb at the strike frame), or flagged when unhinted;
- leap touchdown found for clips that land above their start height;
- a prefab-asset limb lookup bug.

Library clips got the shipped clips' import settings (rotation and height baked into the pose; XZ stays root motion), because planar-only root motion otherwise silently drops spins and leap height in play.

Results:
- **470/470 EditMode tests** pass (+17).
- The live `VerifyAttackLibrary` probe found:
  - no window hit a target twice;
  - nothing hit from behind;
  - a roll cancels cleanly;
  - the sweeps and shockwave hit 3/3 of an arc;
  - Grunts die normally.
- Shipped-combo regression probes pass.

Outstanding:
- a true weapon test (no weapon clip or mesh exists);
- the dojo Player is an unpacked scene object, not a `Player.prefab` instance (`_reachPadding` 0.3 vs 0.1), so the authoring preview can differ from play;
- one Animator trigger/state per clip will need a CrossFade-by-name path at hundreds of attacks.

See `COMBAT.md` (RG5E). Awaiting review.

Milestone 2, Review Group 5D (Combat Arena, Combos & Multi-Target Combat) — complete, paused for review.

Four goals, all composed on the existing HitDetection + Ability pipeline with **one reviewed engine extension**. (1) **Combat dojo:** `VerticalSlice.unity` now holds permanent Training Dummies in formations (single / close pair / arc / staggered distances), several pursuing Grunts, and a visible `EnemySpawnTrigger` floor pad that spawns Grunt waves by reusing the canonical `GameplayObjectSpawner.Compose` path — no encounter/spawn manager. (2) **Data-driven combo:** a `ComboDefinition`/`ComboStep` + pure `ComboSequencer` (`ToyChest.Gameplay.Combat`) express an ordered sequence of existing attacks; a press starts hit 1 and a buffered continuation chains at the contact frame with no dead time; `PlayerCombat` drives it (single-swing fallback preserved). No hardcoded `Punch1/2/3` — the sequencer has no attack-specific branches, so `Punch → Punch → Kick → Ground Slam` is authoring, not code. Shipped `Combo_PlayerMelee` = 3 punches; cadence moved into the sequencer (combo-step abilities zero-cooldown). (3) **Multi-target:** the **one reviewed, accepted** engine extension — `AbilitySet.TryActivate(id, IReadOnlyList<EffectTarget>)` commits once (one cost/cooldown/event) and applies effects per target; `PlayerCombat` now applies a step's ability to every enemy in the volume, `Impacted` raised per confirmed target. HitDetector still applies no damage. (4) **Enemy interruption:** pure `EnemyAttackCycle` with **authored per-phase interrupt windows** (data, not `if elapsed<`); a hit during an interruptible phase cancels the pending strike → existing hit reaction → resume, all gameplay-side (Animator only presents). Suite green at **429** EditMode (+30: multi-target activation, combo sequencer, enemy attack cycle, and end-to-end content). Docs: `COMBAT.md` (arena, combo, multi-target, per-target feedback), `ABILITY_SYSTEM.md` (the accepted Multiple Targets extension), `ENEMY_SYSTEM.md` (interruption), `PLAYER.md` updated. **Known follow-up:** `Combo2`/`Combo3` animator states reuse the Attack punch clip — point them at distinct clips + author per-clip `OnAttackContact` events for variety (Inspector/content pass). Awaiting review.

Milestone 2, Review Group 5C (Combat Feel Iteration & Debug Tooling) — complete, paused for review.

No new mechanics: this group made the one punch snappy and its impact unambiguous, and built the tooling to iterate on feel. **Three attack stages** (`Attacked` → `Contacted` → `Impacted`) now separate "swung" from "contact frame" from "hit confirmed", and **all** impact feedback is wired to `Impacted` alone — an empty swing produces no camera impulse, VFX, hit-stop, or sound (the old swing-start camera shake was removed). **Latency removed:** post-impact commitment became its own knob (0.05s) separate from swing recovery (0.35s → 0.12s), so movement returns right after contact — measured commitment 0.75s → **0.53s**; buffering and cadence unchanged, no combos, no animation cancelling. **Impact position** now uses the real `HitResult.ContactPoint` (`Collider.ClosestPoint` from the attacking hand bone) instead of a chest-height/inset approximation. **Player hit reactions disabled** as an intentional milestone decision (presentation only; health/enemy attacks unaffected). **Training Dummy** authored (`object.training_dummy` + prefab, placed in VerticalSlice): an ordinary enemy composition, authored inert (no abilities, zero ranges) with a thin keep-alive adapter so it never dies — no manager, no bespoke damage path. **Debug tooling:** `CombatDebugOverlay` (F2, dev-only assembly) draws the live HitVolume arc/radius, facing, and confirmed-hit markers in **both** Scene and Game view, plus a HUD (phase, contact, hit confirmed, targets hit, cooldown, volume shape) — strictly read-only, with a test asserting gameplay never references the debug assembly. Suite green at **399** EditMode (+6). Live verification against the dummy: empty swing = 1/1/**0** (no impact feedback), confirmed hit fires exactly one `Impacted`, sustained punching leaves the dummy alive. Docs: `COMBAT.md` (stages, feel methodology, debugging workflow, dummy), `PLAYER.md` updated. **Known follow-up:** the Attack state plays at speed 2 and the clip changed to `Frank_RPG_Fighter_Combo03_1`, so the 0.4s wind-up may no longer match the clip's true contact frame — author the `OnAttackContact` event on the clip (the RG5B standard) rather than re-guessing the timer. Awaiting review.

Milestone 2, Review Group 5B (Asset-Driven Hit Detection & Authoring Workflow) — complete, paused for review.

Established the **permanent attack-authoring pipeline** on top of RG5A's hit-detection vocabulary, so every future attack (player/enemy/companion/boss melee, projectiles, grenades, AOE spells, explosions, beams, hazards, traps) is authored the same way — **animation + one `OnAttackContact` event + a shared HitVolume preset + an Ability** — with no gameplay code per attack and the engine frozen. `HitVolume` was split into geometry (the reusable preset) + `HitFilter` (attacker's faction/layers) + `HitVolumeAnchor` (socket/humanoid-bone + local offset + facing), composed by a `HitVolumeEmitter` (one authored hit region). Multi-hit falls out of firing several contact events; multi-volume falls out of authoring an emitter array — no new system. Added `HitVolumeAsset` ScriptableObject presets and a starter library under `Assets/Game/Content/HitVolumes/` (Punch, Sword Slash Small/Wide, Kick, Claw, Cone Short/Wide, Boss Slam, Explosion Small/Large, Projectile Impact). `PlayerCombat` now anchors its punch to the RightHand bone through an emitter; `EnemyCombatant`'s claw uses the *identical* construction (proving character independence — same authoring, different humanoid, no character-specific code). Non-goals honored (no weapon/combo/lock-on/equipment/damage/manager/trigger-framework). Suite green at **393** EditMode (+8: anchor math, offsets, character-independent reuse, multi-volume union, multi-contact statelessness, deterministic ordering, no-allocation). Live from `Bootstrap.unity` (`VerifyAuthoringWorkflow.cs`): PASS — player punch, sword swing, enemy claw, giant attack, explosion, and projectile impact all resolve through the one vocabulary; RG5A facing guarantee still holds. Docs: `COMBAT.md` carries the permanent workflow with diagrams; `ABILITY_SYSTEM.md` updated. **Note:** applying one activation's effects to *many* hits (true AoE) is intentionally deferred to the Ability System's documented Multiple-Targets extension, not solved with a damage pipeline. Awaiting review before the next Milestone 2 review group.

Milestone 2, Review Group 5A (Hit Detection Architecture) — complete, paused for review.

This review group stopped adding combat features and instead defined the **canonical hit-detection architecture** every future gameplay interaction will use — the single answer to "how does gameplay determine which Gameplay Objects were actually hit?" It is a query vocabulary, not a new framework, that composes with the frozen engine and feeds the existing Ability → Gameplay Effect pipeline. A new `ToyChest.Gameplay.HitDetection` module (`HitShape`, `HitVolume`, `HitQuery`, `HitResult`, `HitDetector`) cleanly separates **Hit Detection** (what was hit) from **Hit Results** from **Gameplay Effects** (what happens). Player melee was rewired onto it (fixing the root defect: a swing can no longer connect with something behind the player, because the hit is a directional `Cone` resolved at the animation contact frame), and the enemy strike now shares the *same* path — player and enemy no longer have two hit code paths. The vocabulary is proven universal (player melee, enemy melee, explosion/hazard, and future projectile) by pure engine-independent tests. Persistent hazards (`HazardVolume`) and interactions (`PlayerInteractor`) are documented as the sphere/omnidirectional instances of the same model. No Framework or Core change; no combat/weapon/damage/targeting/hit manager. Suite green at **385** EditMode tests (+16). Canonical architecture documented in `COMBAT.md` (decision, rejected alternatives, authoring workflow, extension strategy, tech debt, framework confirmation); `ENEMY_SYSTEM.md` and `ABILITY_SYSTEM.md` updated. Awaiting review before the next Milestone 2 review group.

Milestone 2, Review Group 5 (Player Combat Foundation) — complete, paused for review.

One polished melee attack was built to establish the combat quality bar, entirely by composition on the frozen engine — no combat/weapon/targeting/attack/combo framework, no lock-on, no stamina. The attack is still the authored `ability.player_strike` → `DamageEffect`; the work was making *swinging it* feel connected and responsive. **Phased swing**: `PlayerCombat` now runs wind-up → impact → recovery (the pure, unit-tested `MeleeSwing`), mirroring the approved enemy telegraph — the swing starts immediately on input (gated by the ability's `CanActivate`, so cadence stays the authored cooldown) but **damage lands on the swing's contact frame**, not the input frame, which is what makes the hit read as connected. **Control preserved**: input buffering and attack commitment kept; movement is damped (never locked) during the swing and blends back to locomotion early when movement continues (a `Speed`-gated Attack transition); roll/jump still interrupt. **Impact juice** (new thin `PlayerAttackFeedback` adapter, off `Attacked`/`Impacted` events only): stronger camera impulse on connect, a one-shot impact VFX burst at the hit point (authored Epic Toon FX prefab), a brief hit-stop, and swing/impact sound hooks; the enemy's existing hit flash now syncs to the contact frame because damage lands at impact. **Data ownership**: damage/cooldown/effect stay authored on the ability; swing phases, buffer, commitment, impulse, VFX, hit-stop, sounds are authored serialized fields on the two thin adapters. New EditMode tests cover the pure `MeleeSwing` timing; the existing combat content tests (which exercise the ability path directly) are untouched and green. No Framework or Core change. Awaiting review before the next Milestone 2 review group.

**Feel iteration (post-review playtest).** Steve playtested and flagged: an input dead-feel (root cause: playing a gameplay scene directly skips `GameBootstrap` composition — fixed with the editor-only `Assets/Editor/PlayFromBootstrap.cs` so Play always boots from Bootstrap; input/bindings were fine), plus four feel issues, all fixed as feel/presentation with no new systems: **swing retimed** to the 1.2s clip (wind-up 0.4s = contact, recovery 0.35s) so the blow lands when the fist connects and the player is committed for the punch; **attack root motion** consumed into the `CharacterController` via a thin `PlayerModelEvents` relay (`applyRootMotion` on, attack-only) so the character keeps the forward step instead of snapping back; **no gliding** (movement fully committed during the swing; the Attack state returns to locomotion the instant movement resumes); **impact reads as connecting** (hit VFX at a computed chest-height point on the target's near surface, on the contact frame, plus an `OnAttackContact` animation-event seam for frame-exact sync); and **enemy hit reaction** (a brief `Hit` flinch on the Grunt, animation-only so it never stunlocks the timer-driven attack). Suite green at 369 (+2 for the animation-contact hook). Bindings also updated per request: Attack = Mouse Left / Keyboard Q / Gamepad West; Equip moved to Gamepad North.

Made the core movement set feel cohesive, on the frozen engine, with no new gameplay systems or framework concepts — pure feel/animation tuning within the validated Gameplay → Animator Driver → Animator Controller pipeline. **Momentum-based roll**: the roll now inherits the character's current horizontal speed and adds an explosive impulse on top (pure `LocomotionMotor.RollLaunchVelocity`), then bleeds that burst back toward the player's *ongoing* locomotion intent via the same `Accelerate` the ground loop uses. Walking, running, and sprinting therefore produce progressively faster, longer rolls with **no separate code path**, and the roll recovers straight into a run (momentum preserved) instead of decaying to a dead stop the player must re-accelerate from — which also removes the recovery foot-slide at the source. Starting tuning: impulse 8, launch clamp 20, roll decel 18, duration 0.5. **Landing & roll-recovery polish** (animator only, momentum untouched): added a `Speed` float the driver sets so **Land** and **Roll** yield to locomotion the instant movement input continues — Land re-enters Locomotion immediately when moving (no exit time) and only plays a brief settle when stationary (exit time 0.55 → 0.3); Roll recovers early into locomotion when moving (exit time 0.85 → 0.5) and settles later (0.8) when the stick is released, ending the "finish posing" slide. No gameplay logic entered the Animator; the `Speed` parameter is read-only presentation. Suite green (359 EditMode tests, +5 covering the pure roll-launch math); runtime-verified live (roll inherits 7 → launches 15, jump/land/grounded correct, clean boot). No Framework or Core change; no i-frames, stamina, dodge system, or new traversal. Awaiting review before the next Milestone 2 review group.

Made controlling the character significantly more satisfying, on the frozen engine, with no new gameplay systems or framework concepts. **Sprint**: hold-to-sprint (not a toggle) as a ×1.5 multiplier on the authored Movement Speed attribute — no stamina/resource. **Jump**: single-press, grounded-gated vertical impulse with retained airborne control and a clean fall→land; no double/wall/ledge traversal. **Roll**: single-press evasive burst easing to a natural recovery; no i-frames, stamina, or dodge framework. **Directional locomotion**: replaced the 1D speed blend with a 2D freeform-directional blend tree (MoveX/MoveY relative to facing) — 8-way walk, 8-way run, forward-biased sprint, plus jump/fall/land and roll states — driven by the thin `PlayerAnimatorDriver` (Gameplay → Animator Driver → Animator Controller preserved; no gameplay logic in the Animator). **Speed**: authored Movement Speed base raised 5 → 7 for energetic traversal. **Camera**: fixed the distracting rotational drift — removed the re-aiming rotation composer/look-ahead and made the vcam a stable fixed-angle, position-only follow (constant pitch, light damping), so input feels directly connected to movement. **Feel**: retuned accel/decel/turn/gravity and added reduced air-control. New EditMode tests cover the pure directional helper (`LocomotionMotor.LocalMoveVector`). No Framework or Core change; no movement manager, traversal/dodge framework, or stamina. Awaiting review before the next Milestone 2 review group.

Milestone 2, Review Group 2 (Core Game Feel & Combat Polish) — complete, paused for review.

The combat sandbox was transformed from mechanically-correct into enjoyable to play, on the frozen engine, with no new gameplay systems or framework concepts. **Stamina removed** (the immediate correction): the basic attack is never gated by a resource. **Movement** now uses acceleration/deceleration (pure `LocomotionMotor.Accelerate`) for weighty-but-crisp control. **Combat feel**: attack input buffering + brief attack commitment (movement damped during a swing via a locomotion lock). **Enemy**: the Grunt now telegraphs — Ready → Wind-up → Strike → Recover — so its attack is readable and dodgeable (cadence moved from the ability cooldown into the behaviour adapter). **Camera**: adopted Cinemachine 3 as a thin presentation layer (world-space follow, damping, look-ahead, deoccluder) replacing the hand-rolled rig; gameplay unaffected. **Animation**: player and enemy are now rigged humanoids (JC Stylized Warrior / Ranger) with real AnimatorControllers — Idle → Speed-driven locomotion blend tree → Attack / Hit / Death — driven by thin animator-driver adapters from gameplay events, using retargeted humanoid clips (Kevin Iglesias, Frank). **Presentation** consumes existing events only: hit flash on damage, and Cinemachine camera-shake impulses on hit/swing. No Framework or Core change; no new managers or framework concepts. Awaiting review before the next Milestone 2 review group.

Milestone 2, Review Group 1 (Core Action Foundation) — complete, paused for review.

A combat sandbox — player melee (stamina + cooldown), one Grunt archetype with readable Idle→Pursue→Attack behaviour, hit/damage/death/loot — was built entirely from authored content and thin adapters on the frozen engine, with no combat/AI/targeting/weapon manager and no Damage System pipeline. Suite green (349 EditMode tests, +14). Awaiting review before the next Milestone 2 review group. (Milestone 1 certification history is retained below.)

Review Group 8 — complete, Milestone 1 certified.

The vertical slice is integrated end-to-end on the canonical scene and validated entirely through the frozen Milestone 0 engine and authored data. The full loop — greet → loot → equip → complete an equipment-gated objective → survive a hazard → recover → die → respawn → save/reload — is proven by a certification integration test and a clean play-mode boot. Suite is green (335 EditMode tests, +3). Awaiting certification before Milestone 2.

Milestone 1 exit criteria (all met):

Launch → reach playable scene → control player ✅
Explore, interact, pick up items ✅
Equip items; experience attribute/tag/ability/status grants ✅
Use abilities; experience gameplay effects ✅
Gain and lose health; death and respawn ✅
Apply and remove status effects (timed, periodic) ✅
Complete a simple authored objective ✅
Save, quit, reload into identical state ✅
No engine architecture changes were required ✅

Constraints (honored):

No combat, weapons, animation, or enemy AI
No new managers, no trigger framework, no quest framework, no new gameplay loops
No Framework or Core change
Pause after implementation
Remaining Milestone Roadmap (historical, Engine Validation era; superseded by *Minimum Enjoyable Loop — Sequenced Plan*)

Review Group 6

Equipment & Passive Character Progression

↓

Review Group 7

Health, Damage & Environmental Hazards

↓

Review Group 8

Vertical Slice Integration

↓

Milestone 1 Review

↓

Milestone 2 Planning

---

# Recently Completed

Continue maintaining this section chronologically.

Each completed review group should include:

concise implementation summary
architectural validation
documentation updated
tests added
manual verification
technical debt discovered

Avoid allowing this section to become the operational task list.

Its purpose is project history.

- First Player-Activated Ability — Ground Slam (Milestone 1 — The First Fun, Review Group B). **Awaiting review.**
  - **Player path:** a new `Ability1` action (R / Mouse Right / Gamepad Right Shoulder). `PlayerInputBridge` maps it to slot 0 of the new thin `PlayerAbilityActivator` on `Player.prefab`. The slot is authored data: the activation ability id plus the attack asset. It reaches the player through the normal Bootstrap → VerticalSlice composition, and `Obj_Player` grants both definitions.
  - **Two definitions:** `ability.ground_slam` (Self, 6 s cooldown, no cost or effects) and `ability.ground_slam_impact` (Provided, 0 s, `Fx_Damage_GroundSlam` 30). The attack, `Combo_Ability_GroundSlam`, is a one-step combo that reuses `Attack_Fighter_Combo02_3` and its `HV_GroundShockwave` window unchanged.
  - **Cooldown committed at press:** the pure `AbilitySlotActivation` validates through the Ability System first. Refusals publish `AbilityActivationFailed` and play nothing. A busy character buffers the press for 0.18 s with nothing committed. Otherwise the activation commits, and the attack starts in the same call. This uses ordinary `AbilitySet` cooldowns, with no second mechanism.
  - **PlayerCombat:** a new `CanStartAbilityAttack`/`StartAbilityAttack` plays the attack through the existing step pipeline by parking the combo's runtime. `BuildSteps` now returns its array. The melee press path, its arc check, and melee cooldowns are unchanged.
  - **Presentation:** an `_abilityContactVfx` ground burst on the contact frame, hit or miss. Hit-stop, shake, and impact VFX remain on confirmed hits only. A minimal IMGUI `PlayerAbilityHud` sits above the health bar and shows the name, key, READY or seconds remaining, and a red flash when a press is refused.
  - **Architecture:** no Core, Framework, or Ability System change; no stamina or new resource; no ability bar. Two compromises are recorded in `ABILITY_SYSTEM.md`. First, movement-state refusals publish no `AbilityActivationFailed`, because the player has no state tags. Second, a slot requires an attack, so attack-less Self abilities such as Second Wind need a decision in Review Group D.
  - **Tests:** EditMode **499/499** (485 existing plus 14 new `GroundSlamContentTests`). These cover definitions and grant, the attack wiring, commit-before-attack, a whiff still costing the cooldown, refusal of an immediate repeat, reactivation after the cooldown, an ungranted press committing nothing, buffering and expiry, multi-target damage, melee isolation, and the cooldown surviving save/reload.
  - **Runtime probe (automated, not a human playtest):** `Tools/CoplayScripts/VerifyGroundSlam.cs`, run from `Bootstrap.unity` with real queued keyboard input, passed all 20 checks: 3/3 dummies front, side, and behind hit once each for 30; re-press refused OnCooldown with no slam; Q melee unchanged during the cooldown; a no-target slam played, burst, and took the cooldown; an ungranted press was refused NotGranted with no cooldown; a press during a roll was dropped with no cooldown; 3/3 Grunts hit. The existing `VerifyComboPads` passed. `VerifyAttackLibrary` (descriptive, no verdict) ran to completion with every combo playing and its Grunt waves killed.
  - **Manual verification:** no human playtest has been performed yet. Slam feel, cooldown length, damage, and the HUD's readability are reviewer judgments.
  - **Technical debt found:** input-driven probes need Game-view focus (`PointersAndKeyboardsRespectGameViewFocus`), or every press is silently dropped (`COMBAT.md`). `Player.prefab` now serializes `PlayerLocomotion._rollMinSpeed` at its code default of 6, with no behaviour change.
  - **Docs:** this plan, `ABILITY_SYSTEM.md` (*Player Activation Pattern*), and `COMBAT.md` (*Ground Slam*).

- Milestone 1 Alignment & Verified Gameplay Gap Audit (Milestone 1 — The First Fun, Review Group A). This group was documentation and audit only, with no gameplay or code change.
  - Reconciled this plan's historical milestone labels with the Foundations roadmap. The engine-validation slice (EV-RG3–8) is foundational work. Gameplay Expansion (GX-RG1–5G) is the first tranche of The First Fun. New groups are lettered.
  - Audited every First Fun requirement against the working tree with evidence.
  - Traced the Ability System end to end. Second Wind is granted and works when called directly, but no input, adapter, or UI reaches it, so no player-active ability exists. `PlayerCombat` checks cooldowns only when a target is in the arc.
  - Recommended Ground Slam as the first ability, plus the activation/impact composition pattern, and sequenced review groups B–I.
  - Tests: EditMode 485/485. A play-mode audit probe (session scratch, not committed) booted from Bootstrap: 73 definitions, 27 participants, 23 composed objects, 0 errors.
  - Docs: this plan, `MILESTONE_1_VERTICAL_SLICE.md` (relationship note), `ABILITY_SYSTEM.md` (player activation status), and `AI_AGENT_INDEX.md` (read-order label).
- Core Locomotion & Character Feel (Milestone 2, Review Group 3). Made the character satisfying to control even in an empty scene, on the frozen engine, with **no new gameplay systems and no framework concepts** (no movement manager, traversal/dodge framework, or stamina). **Sprint** — `PlayerLocomotion.SetSprint`, hold-not-toggle, releases instantly; a ×1.5 multiplier on the authored **Movement Speed attribute** (the gameplay source of truth), no resource. **Jump** — `PlayerLocomotion.Jump`, single-press, grounded-gated upward impulse integrated by the existing gravity; airborne planar control retained but dampened (`_airControl`); `Jumped` event + grounded flag drive a clean jump→fall→land; no double/wall/ledge/traversal. **Roll** — `PlayerLocomotion.Roll`, single-press evasive burst in the input/facing direction that eases out to a natural recovery; `Rolled` event drives the animation; **no i-frames, stamina, dodge framework, or animation-cancel**. **Directional locomotion** — the RG2 1D speed blend was replaced by a **2D freeform-directional** blend tree keyed on `MoveX`/`MoveY` (velocity relative to facing, normalized to run speed via the new pure, tested `LocomotionMotor.LocalMoveVector`): idle centre, an 8-way **walk** ring, an 8-way **run** ring, a forward-biased **sprint** tier, plus jump/fall/land and roll states — all driven by the thin `PlayerAnimatorDriver` setting parameters from `PlayerLocomotion` (Gameplay → Animator Driver → Animator Controller preserved; no gameplay logic in the state machine). Because the character turns to face movement (correct for a fixed non-orbit camera), forward locomotion dominates while running and the side/back clips play through turns and reversals; a lower turn speed (620°/s) keeps those directional animations visible. **Speed** — authored Movement Speed base raised 5 → 7 for energetic-but-controllable traversal. **Camera** — fixed the distracting rotational drift the reviewer flagged: it was the `CinemachineRotationComposer` (with look-ahead) re-aiming as the player moved; removed it and made the vcam a **stable fixed-angle, position-only** follow (world-space binding, constant downward pitch, light symmetric damping, deoccluder retained), so input feels directly connected to movement. **Feel** — retuned acceleration/deceleration/turn/gravity and added reduced airborne acceleration. New input actions (Sprint/Jump/Roll, keyboard + gamepad) wired through `PlayerInputBridge`. Adds pure `LocomotionMotor.LocalMoveVector` EditMode tests. No Framework or Core change.
- Core Game Feel & Combat Polish (Milestone 2, Review Group 2). Turned the RG1 combat sandbox from mechanically correct into enjoyable, on the frozen engine, entirely through feel tuning, thin presentation adapters, and authored humanoid content — **no new gameplay systems and no framework concepts**. **Immediate correction — Stamina removed completely**: cleared the strike ability's resource cost, removed the resource from the player, deleted `Res_Stamina` + its Addressables entry, and purged it from tests and docs; a basic attack can never be blocked by a resource. **Movement**: added acceleration/deceleration to locomotion via the new pure, unit-tested `LocomotionMotor.Accelerate` (weighty but crisp, responsive under rapid input reversal); faster turning. **Combat feel** (`PlayerCombat`): attack **input buffering** (a press just before cooldown-end is remembered and fires when ready) and **attack commitment** (a landed swing briefly damps movement through `PlayerLocomotion.ApplyMovementLock`) — timing/feel, not a new system, and never a lockout. **Enemy** (`EnemyCombatant`): the Grunt now **telegraphs** its attack — Ready → Wind-up (rooted anticipation, dodgeable) → Strike → Recover (rooted, vulnerable) — moving cadence out of the ability cooldown into the behaviour adapter; death teardown is deferred so the death animation plays. **Camera**: adopted **Cinemachine 3** as a thin presentation layer (world-space follow, position/rotation damping, look-ahead, deoccluder) replacing the hand-rolled `ThirdPersonCameraRig`; gameplay is unaffected because movement reads the active camera's facing. **Animation**: player and enemy are now rigged **humanoids** (JC Stylized Warrior / Ranger, retained as a child `Model` under each gameplay root) with real **AnimatorControllers** — Idle → Speed-driven locomotion blend tree → Attack / Hit / Death — driven by thin `PlayerAnimatorDriver` / `EnemyAnimatorDriver` adapters purely from gameplay events (locomotion speed, `Attacked` / `WindUpStarted` / health-change / depleted); clips are retargeted humanoid animations (Kevin Iglesias locomotion/idle/hit/death, Frank attack). The graph + parameter contract is the reusable foundation — swapping the character retargets clips without touching the graph; no gameplay logic in the state machine, root motion off. **Presentation** (new thin `ToyChest.Gameplay.Presentation` assembly, consumes existing events only): `HitFlash` tints a character on damage, `CombatImpulse` fires Cinemachine camera-shake on player hit (strong) and swing (subtle). Player/enemy prefabs gained the model, animator, driver, hit flash, and (player) impulse source; the vcam gained an impulse listener. Adds pure `LocomotionMotor.Accelerate` EditMode tests and updates the combat content tests for the stamina removal. No Framework or Core change; no new managers, no camera/animation/combat framework in gameplay code.
- Core Action Foundation — combat sandbox (Milestone 2, Review Group 1). First gameplay expansion on the frozen engine: a minute-to-minute combat sandbox built **entirely from authored content and thin adapters, with no new framework** — no Combat/AI/Targeting/Weapon manager, no animation framework, no Damage System pipeline (the existing `DamageEffect` is reused, as its own doc anticipates). **Player attack:** an authored `ability.player_strike` (Provided target) using the existing Ability → Gameplay Effect → Resource path — a `DamageEffect` (20) paid with a new **Stamina** resource (cost 25, regen 30/s) and gated by a 0.4s cooldown, so cadence/economy are data. A thin `PlayerCombat` adapter runs the same proximity query as `PlayerInteractor` to pick the nearest `Enemy`-tagged target and activate the ability; a new **Attack** input action (`<Mouse>/leftButton`) drives it through `PlayerInputBridge`. **Enemy:** one excellent archetype, the **Grunt** (`object.grunt`), an ordinary Gameplay Object composing the shared Maximum Health + bound health, an authored `attribute.enemy_speed`, an `Actor.Enemy` tag, and `ability.enemy_strike` (claw, 8 damage, 1.5s cooldown). Its behaviour is `EnemyCombatant` (new `ToyChest.Gameplay.Enemy` assembly), the enemy counterpart to `NpcWanderLocomotion`: it reads speed off the object, asks the pure deterministic `PursuitMotor` for the readable **Idle → Pursue → Attack** phase, drives a `CharacterController`, and activates its attack ability in range (cadence = the ability cooldown). **Death** is the existing Resource-depleted signal driving the normal lifecycle teardown; on death the Grunt drops a `LootPickup` (thin trigger adapter, the pickup counterpart to `HazardVolume`) that grants an authored `item.monster_fang` via `InventorySet.TryAdd` when the player walks over it. Targeting is scene-native via Unity tags (built-in `Player`, added `Enemy`). A combat arena of three Grunts is integrated into the canonical scene beyond the objective; a `Grunt` and `LootPickup` prefab were authored. Adds 14 EditMode tests (pure `PursuitMotor` phases/determinism; player strike damages + costs stamina + honours cooldown + cannot-afford + kills in 3 hits with death detection; enemy strike damages the player; Grunt composes as an ordinary enemy) — full suite at **349 passing**. No Framework or Core change; no new managers or framework concepts. The player gained a stamina resource and the strike ability; `ToyChest.Gameplay.Player` gained Abilities/GameplayEffects references.
- Vertical Slice Integration & Certification (Milestone 1, Review Group 8). Milestone 1's final pass: integrated every system from Review Groups 3–7 into one cohesive, playable slice on the **canonical** scene `Assets/Game/Scenes/VerticalSlice.unity`, and proved the Milestone 0 engine supports a complete data-driven game with **no architectural change and no new framework concepts**. Brought the canonical scene up to date (it had drifted — missing the RG6 Equipment Cache and all RG7 hazards) by composing the full slice through the existing `GameplayObjectSpawner`: Player (with `PlayerRespawnController` now on the Player prefab, wired to an authored `PlayerSpawnPoint`), Villager, Supply Crate, Wooden Crate, Equipment Cache, the four RG7 hazards/fixtures, and a new completion objective. **Authored one small objective with no quest framework:** an **Ancient Machine** (`object.ancient_machine`) advertises `interaction.activate_machine`, gated on the interactor carrying `Equipment.Lucky` (the tag the Lucky Charm grants when equipped), whose ability has a one-shot `resource.machine_charge` cost and rewards the **Valley Relic** (`item.relic`) via the existing Add Item effect. Completion is therefore authoritative (relic in inventory + machine charge spent) and persists through Save/Load — reusing interaction, ability tag-gating, resource cost, Add Item, and inventory exactly as the loot loop does. The objective naturally chains the whole experience: loot the Equipment Cache → equip the Lucky Charm (→ `Equipment.Lucky`) → activate the Machine → recover the Relic. **Quality pass:** removed the obsolete duplicate `Assets/VerticalSlice.unity` scene (the RG7 drift, now resolved — build settings and the canonical scene agree). Added a single certification integration test that walks the entire loop against real authored content through the real interaction, equipment, status, and save systems (greet → loot → gated objective → hazard damage composing with the Charm's passive regen → shrine recovery → death → respawn → save/reload determinism); it also surfaced a benign emergent interaction (poison and Lucky Regen compose to a net-negative tick). Suite at **335 passing** (+3). Manual QA: play-mode boot loads and registers all 64 authored definitions, injects all 12 scene participants, and composes the whole slice with **zero errors**. No Framework or Core change; no new managers, no trigger/quest framework, no new gameplay effects.
- Health, Damage & Environmental Hazards validated (Milestone 1, Review Group 7). Proved gameplay state changes correctly over time — damage, healing, death, respawn, damage/healing-over-time, and timed status effects — entirely through the frozen engine and authored data, with **no combat and no new framework concepts** (no Combat/Damage/Health/Respawn/Hazard manager, no trigger framework, no new gameplay loop). Hazards are ordinary Gameplay Objects. Authored four world fixtures: a **Spike Trap** (`object.spike_trap`) applies `Status_Spikes` (short, harsh DoT), a **Poison Pool** (`object.poison_pool`) applies `Status_Poison` (long DoT that grants the derived `State.Poisoned` tag), a **Campfire** (`object.campfire`) applies `Status_Warmth` (heal-over-time), and a **Healing Shrine** (`object.healing_shrine`) reuses the interaction→ability→effect loop (`interaction.pray` → `ability.healing_shrine`, Provided target = interactor → instant `HealEffect` clamped at the attribute-bound Maximum Health). Damage-over-time and heal-over-time are just existing periodic Status Effects executing existing `DamageEffect`/`HealEffect` sequences; timed statuses begin (granted tag applied) and end (tag revoked, ticking stops) through the existing lifecycle. **Key architectural decision (parallels Review Group 6's equip decision):** applying a status is a **caller** concern, not a Gameplay Effect — a generic "apply status" effect would make `GameplayEffects → StatusEffects` circular (StatusEffects already depends on GameplayEffects). So a hazard's scene-facing behaviour is a thin `HazardVolume` adapter (`ToyChest.Gameplay.Hazards`) that, while a Gameplay Object stands in its trigger collider, calls `StatusEffectSet.Apply` — the environmental analogue of `PlayerInteractor`, not a trigger framework. **Death** is the existing `Resource Depleted` transition (no death system). **Respawn** is deliberately minimal: a pure, unit-tested `PlayerRespawn` helper (the respawn counterpart to `InventoryEquip`) refills resources and clears statuses through existing capability operations, and a thin `PlayerRespawnController` listens for the health-depleted signal and, deferred one frame to avoid mutating the status set mid-tick, restores state and moves the player to an authored spawn-point Transform — no respawn/checkpoint manager. `ToyChest.Gameplay.Player` gained references to Resources and StatusEffects. All 16 new definitions carry the Addressables `definitions` label; a play-mode boot loads and registers all of them with zero errors (57 definitions total). Adds 9 focused EditMode tests driving real authored content through the real boot + save pipeline (DoT/HoT + expiry, poison tag begin/end, shrine instant heal + Maximum-Health clamp, death detection, deterministic save/load continuation across a mid-duration status, and the pure respawn restore) — full suite at **332 passing**. No Framework or Core change; no new managers, stats, or gameplay effects.
- Equipment System validated (Milestone 1, Review Group 6). Proved equipment modifies gameplay entirely through the existing Gameplay Object architecture using authored data, with no new framework concepts and no engine redesign. Two authored equippables exercise the whole capability surface: **Boots of Swiftness** (`item.boots_of_swiftness`, slot `slot.boots`) grant a `+3` Movement Speed attribute modifier and the `Equipment.Swift` tag; the **Lucky Charm** (`item.lucky_charm`, slot `slot.charm`) grants a `+25` Maximum Health attribute modifier (Current Health's maximum tracks it through the existing attribute-bound resource), the `Equipment.Lucky` tag, the `Second Wind` self-heal ability (Ability System), and the infinite periodic `Lucky Regen` status (Status Effect System). **Equipment pickup reuses the Review Group 4 loot loop**: an **Equipment Cache** (`object.equipment_cache`) advertises `interaction.loot_equipment` → crate-owned `ability.loot_equipment_cache` → two `AddItemEffect`s deposit both items into the bag — no new pickup mechanism. **Equip/unequip is a caller concern** (Capability Independence: Equipment never touches inventories), implemented as a thin player caller in the existing `ToyChest.Gameplay.Player` assembly: a pure, unit-tested `InventoryEquip` helper that moves whole Item Instances between `InventorySet` and `EquipmentSet` (validates before removing; all-or-nothing so no item is lost), and a thin `PlayerEquipmentController` MonoBehaviour (shape of `PlayerInteractor`) toggled by a new `Equip` input action on `PlayerInputBridge`. A generic equip Gameplay Effect was deliberately rejected — it would create a `GameplayEffects → Equipment` dependency cycle. `Obj_Player` gained the two slots; the `Player` prefab carries `PlayerEquipmentController` and an `EquipmentCache` sits in `VerticalSlice`. All new definitions carry the Addressables `definitions` label. Adds focused EditMode tests (pure equip/unequip logic; authored-content validation; equip activates every contribution; unequip revokes every contribution and returns items; full equipped loadout + all grants round-trip through Save/Load) — full suite at **317 passing**. Verified end-to-end by a deterministic in-editor playtest: loot → equip (Movement Speed 5→8, Maximum Health 50→75 with Current Health max following, both tags, the ability, and the passive status all active) → save → reload restores the loadout and every grant identically. No Framework or Core change; no new managers, stats, buff, progression, or passive-ability systems.
- Autonomous World Actors — NPCs implemented (Milestone 1, Review Group 5). Validated that the frozen engine supports autonomous, non-combat world actors as ordinary Gameplay Objects, with no new framework concepts. A **Villager** is composed entirely from authored data (`Obj_Villager`): it reuses the shared Movement Speed / Maximum Health attributes and the bound Current Health resource, adds an `Actor.Npc` identity tag and a one-shot `resource.gift_charge`, and advertises a `Greet` interaction. Greeting reuses the exact loot-loop path — `interaction.greet` → NPC-owned `ability.greet_traveler` (Provided target) → Add Item effect gives the player `item.field_ration` while the ability cost consumes the NPC's gift charge (so the gift is one-shot). The NPC's authoritative state (consumed gift charge, attribute-bound health) round-trips through the existing Save System deterministically. Autonomous movement (idle → wander within a radius of the spawn anchor → idle) is a thin `NpcWanderLocomotion` adapter (new `ToyChest.Gameplay.Npc` assembly) driving a `CharacterController` from a pure, deterministic, seeded `WanderMotor` and reading movement speed off the composed object — the NPC counterpart to `PlayerLocomotion`/`LocomotionMotor`, driven by `Update` rather than any manager, scheduler, or global update service. The NPC is placed in `VerticalSlice.unity` and composed by the existing `GameplayObjectSpawner`; new authored assets (`Tag_Actor_Npc`, `Res_GiftCharge`, `Item_FieldRation`, `Fx_AddItem_FieldRation`, `Ability_GreetTraveler`, `Interaction_Greet`, `Obj_Villager`) carry the Addressables `definitions` label. Deliberately excluded per scope: combat, damage, targeting, aggro, navigation frameworks, behavior trees, GOAP, utility AI, state-machine frameworks, animation, dialogue trees, quest systems, and any NPC/AI/World manager. Adds focused EditMode tests (deterministic wander math, NPC composition, greet outcome + one-shot depletion, save/reload determinism against real authored content, authored-content validation); full suite at 305 passing. Verified in play mode end-to-end: boot → VerticalSlice → the Villager composes, activates, joins the live registry, and wanders autonomously — zero errors. No Framework or Core change.
- Interactive Gameplay Loop implemented (Milestone 1, Review Group 4). A complete, authored loot loop validates the frozen engine end-to-end: a **Supply Crate** advertises an `interaction.loot`; the Interaction System routes it to the crate-owned `ability.loot_supply_crate` (Provided target = the interactor); the ability's Add Item effect deposits an authored `item.scrap_metal` into the player's inventory, and its cost consumes the crate's single `resource.loot_charge`, so the crate depletes and cannot be re-looted. The Save System round-trips the whole world deterministically — the player's inventory item and the crate's depleted charge are authoritative and restore exactly (reloaded crate stays empty). Built by composition and authored content only: `Item_ScrapMetal`, `Res_LootCharge`, `Fx_AddItem_ScrapMetal`, `Ability_LootSupplyCrate`, `Interaction_Loot`, `Inventory_Player`, `Obj_SupplyCrate`, plus an inventory added to `Obj_Player`; all carry the Addressables `definitions` label and a `SupplyCrate` instance is placed in `VerticalSlice.unity`. Engine additions were minimal and in-vocabulary: one new atomic effect `AddItemEffect` (the Inventory → Add Item effect the Gameplay Effect docs already anticipate) and extending the `EffectTarget` capability view to carry the existing `InventorySet` (Abilities/GameplayEffects assemblies now reference Inventory) — no managers, no singletons, no new framework concepts, and the player remains an ordinary Gameplay Object. Adds focused EditMode tests (loot outcome, crate depletion, save/reload determinism against real authored content, and Add Item unit tests incl. clear failure when the target has no inventory); full suite at 289 passing. Verified in play mode end-to-end: boot → VerticalSlice → the player loots the crate through its own `PlayerInteractor` (scrap 0→1, charge 1→0), the depleted crate is no longer offered, and the captured save payload carries the item and depleted charge — zero errors. GAMEPLAY_EFFECT_SYSTEM.md synced (Add Item atomic effect; inventory added to the effect capability view). Discovered and documented (not redesigned): gameplay tags are Derived per Engine Principle 25, so the Wooden Crate's runtime `Object.State.Open` tag does not persist — depletable object state that must survive save is expressed as a Resource.
- Player Controller + Input Foundation implemented (Milestone 1, Review Group 3). The Player is composed entirely from existing capabilities through an authored `Obj_Player` `GameplayObjectDefinition` (Maximum Health + Movement Speed `AttributeDefinition`s, the existing bound Current Health `ResourceDefinition`, an `Actor.Player` `TagDefinition`) — no player-specific engine code, proving the framework supports a controllable character through data alone. New generic scene-composition adapter `GameplayObjectSpawner` (`ToyChest.Gameplay`) is the canonical path for scene-authored objects: it runs the existing `GameplayObjectFactory` and binds the result to the sibling `GameplayObjectBehaviour`; it is injected once at scene load through a small `GameplaySceneContext` / `IGameplaySceneParticipant` seam driven by the Boot layer (no globals, no service locator). `GameBootstrap` now performs engine initialization only and transitions into the dedicated `VerticalSlice.unity` gameplay scene (Bootstrap → VerticalSlice; Build Settings ordered Bootstrap first). Player Unity behavior is thin adapters in a new `ToyChest.Gameplay.Player` assembly: `PlayerLocomotion` (reads the Movement Speed attribute off the composed object and moves a `CharacterController`; direction math extracted to the pure, tested `LocomotionMotor`), `PlayerInputBridge` (Unity Input System, authored `Player.inputactions` with Move + Interact), `PlayerInteractor` (proximity discovery producing a deterministically ordered candidate list routed to the existing `InteractionSystem`), and `ThirdPersonCameraRig` (minimal follow; Cinemachine-ready). Authored assets carry the Addressables `definitions` label, so the runtime loads them with no code change. Adds 14 EditMode tests (locomotion math, spawner composition/binding, candidate ordering, Player content smoke). Verified in play mode end-to-end: boot → transition → inject 3 participants → Player and Wooden Crate compose, activate, and join the live registry, zero errors. No new framework concepts; no Framework or Core change.
- First Playable World Foundation implemented (Milestone 1, Review Group 2). Authored the first gameplay content — a **Wooden Crate** — as real ScriptableObject definitions under `Assets/Game/Content/Definitions` (a Maximum Health `AttributeDefinition`, a Current Health `ResourceDefinition` bound to it, `Object.Container.Crate` and `Object.State.Open` `TagDefinition`s, an `AddTagEffect`, an Open `AbilityDefinition`, an Open `InteractionDefinition`, a crate `InventoryDefinition`, and the `GameplayObjectDefinition` composing them). All nine assets carry the Addressables `definitions` label, so `AddressablesDefinitionSource` loads them with no code change. This exercises the whole pipeline: Authoring → Addressables → Definition Source → Data Registry → GameplayObjectFactory → Gameplay Object → GameplayObjectRegistry → Interaction. `GameBootstrap` gained a serialized startup-definition-id list and, after `RuntimeBootstrap.Run`, composes each through the existing `GameplayObjectFactory` and activates it (the same `Create` → `Activate` path the runtime is built on — no new system, manager, or startup path); the Bootstrap scene spawns `object.wooden_crate` on Play. A dev-only `GameplayDebugOverlay` (new top-of-stack `ToyChest.Debugging` assembly, referenced by nothing, IMGUI, read-only) inspects the live registry — object identity, tags, attributes, resources, abilities, inventory, interactions — serving as the Gameplay Object Inspector, Tag Viewer, and Attribute/Resource Viewer. Scene layout formalized under `Assets/Game/Scenes`: Bootstrap (functional), Hub and MissionPrototype (placeholders), Bootstrap ordered first in Build Settings. `AttributeSet` gained a deterministic `Attributes` enumeration for parity with `ResourceSet.Resources`/`AbilitySet.Abilities` and to back the overlay. Adds 4 EditMode tests: three drive the real `RuntimeBootstrap` over the authored crate assets (registry population, full-capability composition + activation + registry membership, interaction discovery + execution) and one covers the `AttributeSet` enumeration. No Framework or Core change; no new framework concepts.
- Runtime Bootstrap implemented (Milestone 1, first task). New top-of-stack assembly `ToyChest.Boot` (Assets/Game/Runtime/Boot) owns the permanent runtime startup path per Docs/Architecture/ENGINE_STARTUP.md. `RuntimeBootstrap` (plain C#, engine-agnostic) drives startup phases 2–6: Service Creation (EventBus, DataRegistry, GameplayTagTable, GameplayObjectRegistry, GameplayObjectContext, GameplayObjectFactory, SaveManager — all injected, no globals), Registry Population (enumerates definition sources, registers into the Data Registry, interns TagDefinitions into the Tag Table), Save Loading, Reconstruction, and Runtime Initialization (save reconstruction + activation through the composition root). `GameBootstrap` is the thin MonoBehaviour scene entry (Bootstrap.unity) that supplies production configuration. `AddressablesDefinitionSource` confines all Addressables coupling to the definition source (tolerates an empty/absent label so the project boots before content exists); `DirectDefinitionSource` serves scenes/tools/tests. `RuntimeServices` holds the assembled services (an injected holder, not a service locator). No new framework concepts, managers, service locators, or parallel registries — the bootstrap only connects existing systems. Doc conflict resolved: the application bootstrap sits above Core (not inside it, which PROJECT_ARCHITECTURE listed) so the one-directional dependency rule holds; PROJECT_ARCHITECTURE clarified. Test suite at 267 passing EditMode tests (10 added).
- Final Architecture Refinement + Save Framework implemented. Lifecycle refinement: Engine Principle 26 (Construction Before Participation) — gameplay objects are fully constructed and internally consistent before they participate; construction and restoration are event-quiet, activation is the single observable fact. Enforced by a per-object `GameplayObjectEventGate` (closed through construction/restoration, opened at activation, closed again before teardown disposal) rather than "quiet mode" flags, unifying spawning, loading, and future streaming under one lifecycle. The factory gained an `IGameplayObjectReconstructor` (Framework-level) reconstruction path composing persisted objects with their saved id. Save Framework (ToyChest.Systems.Save): `SaveManager` captures authoritative state of every live object through the Gameplay Object Registry and restores by reconstructing through the composition root, restoring authoritative leaf values in dependency order (equipment → statuses → resources → cooldowns → inventory) and activating; only authoritative state is serialized (attributes and tags carry none — derived), definitions referenced by DefinitionId, stable serialization contract with versioning via `UnityEngine.JsonUtility`. New APIs: `ResourceSet.Resources` / `AbilitySet.Abilities` / `StatusEffectSet.ActiveStatuses` enumeration, `InventorySet.RestoreStack`, `ItemInstance.Restore`. Docs/Architecture/SAVE_SYSTEM.md authored and registered in AI_AGENT_INDEX.md; GAMEPLAY_FRAMEWORK.md lifecycle/registry/rehydration sections refined; ENGINE_PRINCIPLES.md Principle 26 added; ENGINE_STARTUP.md updated. Test suite at 257 passing EditMode tests (9 added). No gameplay behavior changed beyond the lifecycle refinement.
- Review Group 7.5 (Architecture Hardening) implemented, ahead of the Save Framework: Persistence Boundary engineering principle (Engine Principle 25: Authoritative / Derived State / Reconstruction Over Serialization) with a Persistence Boundary block added to every core system doc; Gameplay Object Registry (ToyChest.Framework.Objects.GameplayObjectRegistry — plain-C#, deterministic registration-order enumeration, lifecycle-driven membership via Activate/Destroy); event-quiet rehydration APIs (ResourceValue.RestoreCurrent, AbilitySet.RestoreCooldown, StatusEffectSet.Restore + StatusEffectInstance.PeriodAccumulator); deterministic iteration (ResourceSet regeneration and GameplayTagContainer enumeration now registration-ordered); Tag Event Bridge completed (GameplayTagAdded / GameplayTagRemoved published by the Gameplay Tag Container on transitions, category Tag); canonical Engine Startup lifecycle doc (Docs/Architecture/ENGINE_STARTUP.md, registered in AI_AGENT_INDEX.md). No gameplay behavior changed. Test suite at 248 passing EditMode tests (16 added).
- Review Group 7 implemented: Item foundation (ToyChest.Systems.Items: ItemDefinition + Definition Components, ItemInstance with stable ids), Inventory System (ToyChest.Systems.Inventory: slot-based InventorySet, deterministic all-or-nothing stack management, transactional transfer, five inventory events), Equipment System (ToyChest.Systems.Equipment: data-driven slot layouts, EquippableDefinition item component, transactional equip activating tags/attribute modifiers/abilities/statuses through their owning systems), Interaction Framework (ToyChest.Systems.Interactions: interactions route to interactable-owned abilities, priority-based selection, interaction events). AbilityCategory value type introduced; ABILITY_SYSTEM.md refined (deterministic recipes, configuration-only definitions, activation extension points). Milestone 0 sections added to ITEM_SYSTEM.md, INVENTORY_SYSTEM.md, EQUIPMENT.md, INTERACTION_SYSTEM.md.
- Ability Framework implemented (ToyChest.Systems.Abilities): AbilityDefinition/AbilityInstance, AbilitySet capability composed on every object, deterministic activation pipeline (tag gates, all-or-nothing generic resource costs, fixed cooldowns, Self/Provided targeting contract), effects through the Gameplay Effect Runner, six Ability events. ABILITY_SYSTEM.md Milestone 0 section added; Context over Ownership documented in GAMEPLAY_EFFECT_SYSTEM.md; GameplayObjectId event guideline added to EVENT_SYSTEM.md. Test suite at 176 passing EditMode tests.
- Gameplay Object Framework implemented (ToyChest.Framework.Objects + ToyChest.Gameplay.Objects composition root); 128 passing EditMode tests. Runtime Framework Architecture documented in GAMEPLAY_FRAMEWORK.md; Engine Principles 22 (Composition Root) and 23 (Framework System Template) added.
- Shared Modifier Stack (ToyChest.Framework.Modifiers), Attribute System, and Resource System implemented; 105 passing EditMode tests. Resources support attribute-bound maximums with immediate clamp on decrease.
- Hierarchical Gameplay Tags implemented (ToyChest.Systems.Tags); TAG_SYSTEM.md updated to the approved model.
- DATA_REGISTRY.md authored; Data Registry implemented (ToyChest.Framework.Data).
- Addressables settings initialized (Assets/AddressableAssetsData).
- Test suite at 69 passing EditMode tests.
- Milestone 0 Architecture Review completed and approved (July 2026).
- Project structure and assembly definitions established per PROJECT_ARCHITECTURE.md.
- Addressables 2.6.0 installed.
- EVENT_SYSTEM.md authored (canonical event architecture).
- Event System implemented in ToyChest.Framework.Events with 20 passing EditMode tests.
- Documentation repository created.
- Core Architecture completed.
- World Reaction System specification completed.
- Combat specification completed.
- Companion specification completed.

---

# Upcoming Documentation Work

Current documentation focus:

1. Keep `Docs/AI_AGENT_INDEX.md` current.
2. Keep system ownership sections current.
3. Add new documents only when a real ownership gap appears.
4. Prefer updating existing canonical docs over creating parallel docs.

---

# Technical Debt

- **`GameplayDebugOverlay` scene residence (Review Group 3 follow-up, maintenance only).** The dev-only `GameplayDebugOverlay` is currently authored in `Bootstrap.unity`, attached to the same GameObject as `GameBootstrap`. It functions correctly in the vertical slice — that GameObject is `DontDestroyOnLoad`, so the overlay survives the `Bootstrap → VerticalSlice` transition and inspects the live registry as intended. The debt is one of authored home, not behavior: a gameplay debugging tool lives in the boot scene, coupling its residence to engine startup rather than to gameplay. Recommendation (future maintenance, no implementation required now): relocate it into `VerticalSlice.unity` (or any gameplay scene), or give it a dedicated persistent debug object, so its lifetime follows gameplay explicitly instead of depending on the Bootstrap object's `DontDestroyOnLoad`. Not a blocker for Review Group 3.
- **Gameplay-tag state is not persisted (Review Group 4 finding, by design; no redesign).** Per Engine Principle 25 and `SAVE_SYSTEM.md`, gameplay tags are Derived and re-established by composition, so a runtime state tag is not serialized. The existing Wooden Crate expresses "opened" as an `Object.State.Open` tag added by an effect; that opened state therefore does not survive save/reload (the crate reopens after load). This is a deliberate persistence-boundary property, not a defect. Guidance for content: object state that must persist should be expressed as authoritative state — a Resource (as the Supply Crate's `resource.loot_charge` does), inventory contents, equipment, cooldowns, or statuses — rather than as a bare tag. If persistent tag-shaped state is ever genuinely required, the correct path is a versioned, intentional extension of the save contract (a per-object persisted-tag record), not an ad-hoc workaround.
- **Equipment: shared grant refcounting + resource modifiers + caller reuse (Review Group 6, by design; not defects).** Three intentional seams surfaced. (1) **Shared grants** — two equipped items granting the *same* ability or status share one grant, so the first unequip removes it (carried from Milestone 0). The authored content avoids this (each grant is unique); the fix, if authoring ever needs it, is a refcounted grant with no architectural change. (2) **Resource modifiers via attributes** — "+Maximum Health" is authored as a Maximum Health *attribute* modifier and Current Health's maximum follows it through the existing attribute-bound resource; direct equipment *resource* modifiers remain future work as the Equipment spec already lists. (3) **Equip caller reuse** — there is deliberately no generic equip Gameplay Effect (it would create a `GameplayEffects → Equipment` cycle), so non-player equippers (companions, AI) will each need a thin caller or a shared interaction when they arrive. Also: `PlayerEquipmentController.Toggle()` equips/unequips all managed slots at once — a single-button demonstration of the caller, not final equip UX (slot-by-slot selection belongs to a future inventory/equipment UI). See `Docs/Systems/EQUIPMENT.md`, "Milestone 1 Integration (Review Group 6)".
- **RESOLVED (Review Group 8): the divergent `VerticalSlice` scenes.** Review Group 7 discovered two drifted `VerticalSlice.unity` files with Build Settings booting the stale one. In Review Group 8 the reviewer designated `Assets/Game/Scenes/VerticalSlice.unity` as canonical; that scene was completed with all current content (Equipment Cache, hazards, objective, respawn) and the duplicate `Assets/VerticalSlice.unity` was deleted. Build Settings, `GameBootstrap`, and the canonical scene now agree, verified by a clean play-mode boot. No residual drift.
- **Combat is behavioural-feedback only; no presentation juice yet (Milestone 2, Review Group 1; by design).** The sandbox conveys feel through readable enemy phases (Idle→Pursue→Attack), attack cadence (cooldown + stamina), and a physical loot drop, but there is deliberately **no VFX/SFX, hit flash, knockback, floating damage, or attack animation** — presentation is out of scope for the action *foundation* and belongs to a later polish/presentation pass (it should hang off existing events — Resource Changed, Ability Activated, Resource Depleted — not new gameplay code). Attacks are instantaneous (no wind-up/telegraph); a telegraph would be authored as a brief pre-attack status or animation event later.
- **Combat uses Unity tags for targeting, not the Relationship System (Milestone 2, Review Group 1; by design).** `PlayerCombat` targets `Enemy`-tagged colliders and `EnemyCombatant` finds the built-in `Player` tag; there is no friendly-fire/relationship evaluation (the Relationship and full Damage systems are future work). Consequence: identification is single-player and scene-native. When enemies must judge allies/targets generically (companions, factions, multiplayer), targeting should move to gameplay tags/relationships resolved through the engine rather than Unity tags — behind the same thin-adapter seam, no new manager.
- **Enemy runtime state is not persisted (Milestone 2, Review Group 1; by design).** Grunts are transient scene actors composed by scene composition and destroyed on death; they are not captured by the Save System, so a reload restores the arena to its authored starting state (all grunts alive, drops gone). Acceptable for a sandbox. Persisting mid-fight enemy state (or cleared-arena state) is a future decision — likely authoritative encounter state expressed as resources/flags on a persistent encounter object, not ad-hoc enemy serialization. Enemies also have no navmesh/pathfinding (they steer straight at the player, like the NPC wander adapter), so the arena is flat and open by design.
- **Respawn clears equipment-granted passive statuses (Review Group 8, minor; by design).** `PlayerRespawn.Restore` clears *all* active statuses to guarantee a clean spawn, which also removes an equipment-granted passive status (e.g. the Lucky Charm's Lucky Regen) even though the item stays equipped; the equipment's attribute modifiers, tags, and granted ability are unaffected (they are held by the Equipment System, not as statuses), so only the passive tick stops until the item is re-equipped. Acceptable for the slice. If a future respawn must preserve equipment passives, restore should clear only hostile/among a set rather than all statuses, or re-sync equipment grants after restore — a small caller change, no engine impact.
- **Hazard application is a caller concern; `HazardVolume` is a thin adapter, not a trigger framework (Review Group 7, by design; not a defect).** Applying a Status Effect deliberately has no Gameplay Effect: a generic "apply status" effect would make `GameplayEffects → StatusEffects` circular (StatusEffects already references GameplayEffects), the same cycle that kept equipping a caller concern in Review Group 6. `HazardVolume` therefore calls `StatusEffectSet.Apply` directly from a trigger callback — one authored status, applied on contact, no conditions/responses/registry. It re-applies on `OnTriggerStay` only once the prior application has ended, so a lingering visitor keeps taking the effect without refreshing (and re-publishing) the status every physics frame; a status whose duration outlasts the visit expires while the visitor still stands inside (acceptable for the slice). If world sources ever need richer triggering (conditions, one-shot arming, shapes), the correct path is to extend the thin adapter behind the same seam, not to add an `ApplyStatus` effect (which would break the dependency rule) or a generic trigger/hazard manager.
- **NPC movement has no navigation or obstacle avoidance (Review Group 5, by design; not a defect).** `NpcWanderLocomotion`/`WanderMotor` steer purely locally toward a random point within a radius of the spawn anchor; there is no navmesh, pathfinding, or obstacle avoidance, so an NPC can walk into props or geometry. This is the intended minimal footprint ("no navigation frameworks beyond what is minimally required") and is a non-issue on the flat vertical-slice ground. If NPCs later need to navigate real geometry, the correct path is to introduce a navigation capability/adapter behind the same thin-adapter seam (the motor already isolates the steering decision), not to grow the wander motor into a pathfinder. Related minor notes: NPC facing follows movement direction (the optional "turn to face nearby interactables" behaviour was not implemented); NPCs reuse the player's Movement Speed attribute base value, so a distinct NPC pace would be authored as its own attribute; and multiple NPCs sharing a wander seed would trace identical walks (author distinct seeds for variety).

- **Presentation juice is flash + shake; no VFX particles yet (Milestone 2, Review Group 2; by design).** RG2 delivered hit flashes (material tint) and Cinemachine camera-shake on hit/swing, all hanging off existing events (health change, `Attacked`, `WindUpStarted`, depleted). Impact/attack **particle** effects and SFX were deliberately deferred — the VFX packs are imported (EffectCore, Epic Toon FX, GabrielAguiarProductions) and a future pass can spawn a burst on the same events (a thin pooled spawner keyed to the hit position), no new gameplay. This partially supersedes the RG1 "no juice" debt.
- **Animation is forward-locomotion + one attack/hit/death clip (Milestone 2, Review Group 2; by design).** The locomotion blend tree is 1D (idle→walk→run by speed magnitude); the character turns to face movement, so a forward set reads correctly, but there is no strafe/directional (2D) locomotion, no combo/attack variety, and no separate jump/fall. Root motion is off (the `CharacterController` owns motion). The Frank attack clip imports under the default take name `Take 001` (cosmetic). Directional locomotion, attack variety, and animation-event-driven damage timing are natural extensions on the same graph.
- **Enemy telegraph/animation timing is approximate (Milestone 2, Review Group 2; by design).** The attack animation is triggered at wind-up start and the damage lands at wind-up end; the two are tuned by seconds on the adapter, not synced by an animation event on the exact contact frame. Using an `AnimationEvent` (or `StateMachineBehaviour`) to land the hit on the swing's contact frame is a future refinement — still keeping gameplay logic out of the state machine (the event would just call the existing adapter).
- **`ThirdPersonCameraRig.cs` is superseded but retained (Milestone 2, Review Group 2).** Cinemachine replaced it on the gameplay scene's camera; the script file remains (unused, still compiles) to avoid leaving missing-script references on any other scene's camera that may still carry it. Delete it once no scene references it. The one-shot `Assets/Editor/ToyChestCameraSetup` authoring tool likewise remains for re-running the camera setup; it can be removed since the result is saved in the scene.
- **Source animation FBX were switched to Humanoid import (Milestone 2, Review Group 2).** The specific Kevin Iglesias / Frank clips used were set to `Humanoid` rig (with looping on locomotion) so they retarget to the JC avatar; these settings live in the clips' `.meta` files. Re-importing those packs preserves the settings; adding new clips requires the same Humanoid import to retarget.
- **Directional locomotion is face-movement, not strafe (Milestone 2, Review Group 3; by design).** The character turns to face its movement direction, which is correct for the fixed, non-orbit camera (a strafe scheme would face a constant world direction and read as moonwalking without camera orbit or lock-on). Consequently the 2D blend tree's forward clips dominate during steady running, and the side/back (8-way) clips play mainly through turns and sharp reversals. If an orbit or lock-on camera is added later, the same blend tree supports full strafe locomotion by decoupling facing from movement — no graph change, just the facing rule in `PlayerLocomotion`.
- **Jump/roll are minimal by design (Milestone 2, Review Groups 3–4).** Jump is a single gravity-based impulse with no coyote-time, jump-buffering, variable jump height, or air-jump; roll (RG4) is momentum-based — it inherits current speed plus an impulse and decays toward ongoing locomotion — but still has no i-frames and no cancel windows. These are intentional per the constraints (movement feel only, no traversal/dodge framework). Coyote-time and a short jump/roll input buffer are natural future polish behind the same adapter. Jump shares the gamepad South button with Interact (both handlers run; benign) — a dedicated scheme/rebind is future UX work.
- **Locomotion/air animation transition timings are feel-tuned (Milestone 2, Review Groups 3–4).** RG4 removed the landing/roll-recovery foot-slide by having Land/Roll yield to Locomotion the instant movement input continues (a driver-set `Speed` parameter gates immediate re-entry; a brief settle plays only when stationary) and by preserving gameplay momentum through the roll rather than reducing it. The remaining timings (jump→fall→land, the roll's own exit points) are still time-based rather than synced to animation events, and the exact landing contact frame is approximate. Syncing land/recovery to animation events (still calling the existing adapter, no gameplay in the state machine) is the future refinement. **Remaining observation for future tuning:** the roll's `Speed>0.15` early-recovery threshold and exit-time values (Roll 0.5/0.8, Land 0.3) are first-pass and want a human playtest pass; the roll clip's own length is not retimed to the gameplay roll duration, so on very short (standing) rolls the clip may still slightly outlast the burst.

- **Player attack impact defaults to an adapter timer; an animation-event seam exists (Milestone 2, Review Group 5).** The swing lands the blow at the authored wind-up duration (tuned to the attack clip's contact frame), OR exactly on an `OnAttackContact` animation event if one is authored on the clip (`PlayerModelEvents.OnAttackContact → PlayerCombat.NotifyAnimationContact`, guarded by `MeleeSwing.CompleteWindUp`). The seam is wired but the event is not yet authored on the Frank attack clip — adding it (frame at contact) makes impact frame-exact and robust to clip re-timing/swaps. Remaining feel-tuning is authored: wind-up/recovery, hit-point height/inset, and the attack root-motion magnitude (currently whatever the clip carries).
- **Attack root motion is consumed only during the attack (Milestone 2, Review Group 5).** `applyRootMotion` is on and `PlayerModelEvents` forwards `animator.deltaPosition` to the controller while attacking; off-attack it discards root motion so locomotion stays velocity-driven. If a future animation set wants root-motion locomotion too, this relay is the seam to extend. The current attack clip's forward travel is modest; increase it in the clip (or scale it in the relay) if a bigger lunge is desired.
- **Attack always swings; damage is gated at impact (Milestone 2, Review Group 5).** A press always starts a swing — with no enemy in range the player whiffs (swing animation + whoosh, no impact juice), so attacking never feels dead in the open. When a live enemy *is* in range, the swing start is gated on the ability's `CanActivate` so a fighting player's cadence stays the authored cooldown (an off-cadence press buffers instead of playing a swing that could not land). Whiff cadence is the authored swing duration (wind-up + recovery). The only remaining nuance: while mashing at a live enemy, cadence is the cooldown (buffered), which is intended. No target-independent cooldown query on `AbilitySet` was needed.
- **Hit-stop uses global `Time.timeScale` (Milestone 2, Review Group 5).** `PlayerAttackFeedback`'s hit-pause briefly scales global time, which is a local single-player feel effect; it is short, self-restoring (including on disable), and authored-optional (set to 0 to disable). It must become non-global for multiplayer (freeze only local presentation, or drop hit-stop server-side). Impact VFX are `Instantiate`+`Destroy` one-shots, not pooled — fine at melee cadence, poolable later on the same event seam.

- **Equipment-granted Second Wind is unreachable (Review Group A finding).** `Ability_SecondWind` is granted by the Lucky Charm but has no input, adapter, UI, or activation test, so the documented "equipment-granted ability" is real only at the system level. It is resolved by the ability slot from RG B and the binding in RG D. Until then, do not describe it as a player-usable ability.
- **Scene README and bootstrap startup list drifted (Review Group A finding, cosmetic).** `Assets/Game/Scenes/README.md` says Bootstrap spawns the Wooden Crate on Play. The live boot logs "Live objects: 0" after engine init, and all objects are composed by `VerticalSlice`'s spawners. Update the README (or the startup list) during a future scene pass.

Continue recording technical debt intentionally.

Do not redesign architecture during Milestone 1 unless a genuine architectural defect is demonstrated.
---

# Engine Validation Follow-up Candidates (documentation only; historical heading "Milestone 2 Candidate Improvements")

Deferred from the Engine Validation slice (EV-RG3–8) by design. Where one of these becomes necessary for a First Fun review group (e.g. the Remove Item effect for crafting in RG E), that group adopts it explicitly. Each is a candidate, not a commitment; none is required to certify Milestone 1, and each should still prefer composition/authored data over new framework where possible.

- **Combat vocabulary.** Milestone 1 deliberately shipped without combat. Milestone 2's Damage System (see `Docs/Systems/DAMAGE_SYSTEM.md`) would let `DamageEffect` route through resistances/mitigation without changing authored content, and add enemies as ordinary Gameplay Objects with abilities.
- **Refcounted shared grants.** Two sources granting the same ability/status share one grant, so the first revoke removes it (carried from Milestone 0; authored content avoids it). A refcounted grant fixes it with no architectural change.
- **Direct equipment/effect resource modifiers.** "+Maximum Health" is currently an attribute modifier the bound resource follows; direct resource modifiers (e.g. +Max Mana) remain future work the Resource/Equipment specs already anticipate.
- **Respawn/checkpoint policy.** Respawn is intentionally minimal (refill + clear statuses + move to a spawn Transform). A future checkpoint system, selective status clearing, and equipment-passive re-sync on respawn are natural extensions behind the same caller seam.
- **Status application via a caller seam for non-player sources.** Companions/AI applying statuses (as hazards do) will each need a thin caller, or a shared one, since status application is deliberately not a Gameplay Effect (avoids the `GameplayEffects → StatusEffects` cycle).
- **Equip/interaction UX.** `PlayerEquipmentController.Toggle()` equips/unequips all managed slots at once; slot-by-slot selection and a real inventory/equipment UI belong to Milestone 2. Likewise a contextual interaction prompt and objective/quest presentation (the current objective has no on-screen UI; completion is the persisted relic).
- **Navigation for actors.** NPC/hazard placement assumes flat ground; real geometry would want a navigation capability behind the existing thin-adapter seam, not a wander-motor pathfinder.
- **Additional atomic effects as needed.** Remove Item, Transfer, and probabilistic/scalable effects are anticipated extension points (see `GAMEPLAY_EFFECT_SYSTEM.md`, Planned Extension Points); add each only when a documented gameplay need arrives.

---

# Open Design Questions

Maintain a short list of unresolved design decisions.

Examples:

- Final movement slot count
- Number of weapon classes
- Companion evolution model
- Procedural region generation algorithm

---

Definition of Done

A review group is complete only when:

Architecture guidelines followed
Existing systems reused
Documentation updated
Tests added
Manual verification completed
No unnecessary coupling introduced
No speculative framework concepts introduced
The review group demonstrates an existing engine capability primarily through authored gameplay rather than new engine code
Cursor / Claude implementation summarized
Next review group identified
AI Workflow
Read AI_AGENT_INDEX.md
Read DEVELOPMENT_PLAN.md
Read referenced specifications
Read CORE_ARCHITECTURE.md
Read ENGINE_PRINCIPLES.md
Read CODING_PRINCIPLES.md
Read AI_CODING_STANDARDS.md
Implement one review group only
Self-review implementation
Update DEVELOPMENT_PLAN.md
Pause for architectural review

Never begin another review group without approval.

Project Health

Architecture

🟢 Excellent

Documentation

🟢 Excellent

Gameplay

🟡 Foundation and combat strong; the First Fun loop is not yet playable

Verified: authored Gameplay Objects, movement, ordinary melee combat and combos, equipment, interaction, gameplay effects, hazards, NPCs, and save/load (by test).

Missing for Milestone 1 (The First Fun): player-activated abilities, elite, progression, crafting, companion, region identity, world reaction, Hub/return, and in-game save. See the gap audit.

Technical Debt

🟢 Low

AI Context Quality

🟢 Excellent

Playable Build

🟡 Combat dojo plus engine-validation fixtures

From Bootstrap, the player can explore the dojo, fight dummies and Grunt waves with combos, loot, equip, complete the equipment-gated objective, survive hazards, die, and respawn. The engine-validation run was proven save/reload-deterministic by test (EV-RG8). It is not yet a repeatable First Fun loop: there is no Hub, no active ability, no progression, and the player cannot trigger save/load.