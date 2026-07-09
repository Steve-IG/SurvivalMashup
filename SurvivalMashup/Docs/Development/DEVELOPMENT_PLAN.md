DEVELOPMENT_PLAN.md (Version 1.1)
Status

Living Document

Version: 1.1

Owner: Technical Creative Director

Purpose: Operational source of truth for all implementation work.

Purpose

This document answers one question:

What should the team (human and AI) work on next?

Every implementation task should originate from this document.

Every completed review group should update this document.

Milestone 1 proceeds through small, testable, vertical slices that continuously validate the existing engine.

Documentation authority, terminology, and AI read order are defined in Docs/AI_AGENT_INDEX.md.

Current Phase
Phase

🟢 Milestone 1 — Vertical Slice — Complete (awaiting certification)

Milestone 0 is complete, reviewed, tagged, merged, and considered architecturally frozen.

All Review Groups 3–8 are complete. The vertical slice is integrated on the canonical scene `Assets/Game/Scenes/VerticalSlice.unity` and validated end-to-end; the milestone required no engine architecture changes. Awaiting final certification before Milestone 2 planning.

Milestone 1 validates that engine through gameplay built almost entirely from:

authored content
existing systems
composition

Architecture changes are only acceptable when a genuine architectural defect is discovered.

Milestone 1 Progress
Review Group	Focus	Status
Review Group 3	Player Foundation	✅ Complete
Review Group 4	Interactive Gameplay Loop	✅ Complete
Review Group 5	Autonomous World Actors	✅ Complete
Review Group 6	Equipment & Passive Character Progression	✅ Complete
Review Group 7	Health, Damage & Environmental Hazards	✅ Complete
Review Group 8	Vertical Slice Integration & Polish	✅ Complete (awaiting certification)
Active Sprint
Sprint Goal

Complete the Vertical Slice by validating every major gameplay system already implemented during Milestone 0.

The objective is engine validation, not feature expansion.

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
Review Group 8 — complete, paused for final Milestone 1 certification.

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
Remaining Milestone Roadmap

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
- **Respawn clears equipment-granted passive statuses (Review Group 8, minor; by design).** `PlayerRespawn.Restore` clears *all* active statuses to guarantee a clean spawn, which also removes an equipment-granted passive status (e.g. the Lucky Charm's Lucky Regen) even though the item stays equipped; the equipment's attribute modifiers, tags, and granted ability are unaffected (they are held by the Equipment System, not as statuses), so only the passive tick stops until the item is re-equipped. Acceptable for the slice. If a future respawn must preserve equipment passives, restore should clear only hostile/among a set rather than all statuses, or re-sync equipment grants after restore — a small caller change, no engine impact.
- **Hazard application is a caller concern; `HazardVolume` is a thin adapter, not a trigger framework (Review Group 7, by design; not a defect).** Applying a Status Effect deliberately has no Gameplay Effect: a generic "apply status" effect would make `GameplayEffects → StatusEffects` circular (StatusEffects already references GameplayEffects), the same cycle that kept equipping a caller concern in Review Group 6. `HazardVolume` therefore calls `StatusEffectSet.Apply` directly from a trigger callback — one authored status, applied on contact, no conditions/responses/registry. It re-applies on `OnTriggerStay` only once the prior application has ended, so a lingering visitor keeps taking the effect without refreshing (and re-publishing) the status every physics frame; a status whose duration outlasts the visit expires while the visitor still stands inside (acceptable for the slice). If world sources ever need richer triggering (conditions, one-shot arming, shapes), the correct path is to extend the thin adapter behind the same seam, not to add an `ApplyStatus` effect (which would break the dependency rule) or a generic trigger/hazard manager.
- **NPC movement has no navigation or obstacle avoidance (Review Group 5, by design; not a defect).** `NpcWanderLocomotion`/`WanderMotor` steer purely locally toward a random point within a radius of the spawn anchor; there is no navmesh, pathfinding, or obstacle avoidance, so an NPC can walk into props or geometry. This is the intended minimal footprint ("no navigation frameworks beyond what is minimally required") and is a non-issue on the flat vertical-slice ground. If NPCs later need to navigate real geometry, the correct path is to introduce a navigation capability/adapter behind the same thin-adapter seam (the motor already isolates the steering decision), not to grow the wander motor into a pathfinder. Related minor notes: NPC facing follows movement direction (the optional "turn to face nearby interactables" behaviour was not implemented); NPCs reuse the player's Movement Speed attribute base value, so a distinct NPC pace would be authored as its own attribute; and multiple NPCs sharing a wander seed would trace identical walks (author distinct seeds for variety).

Continue recording technical debt intentionally.

Do not redesign architecture during Milestone 1 unless a genuine architectural defect is demonstrated.
---

# Milestone 2 Candidate Improvements (documentation only)

Deferred from Milestone 1 by design. Each is a candidate, not a commitment; none is required to certify Milestone 1, and each should still prefer composition/authored data over new framework where possible.

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

🟢 Excellent Progress

The Vertical Slice now demonstrates:

authored GameplayObjects
player control
inventory
equipment foundation
interaction
gameplay effects
persistence
autonomous NPCs

Remaining work focuses on validating the final gameplay systems rather than expanding the engine.

Technical Debt

🟢 Low

AI Context Quality

🟢 Excellent

Playable Build

🟢 Complete Vertical Slice

The project demonstrates a complete, cohesive, end-to-end gameplay experience composed almost entirely through authored data running on the frozen Milestone 0 engine: spawn → explore → meet the Villager → loot → equip → complete an equipment-gated objective → navigate hazards → heal → die → respawn → save/reload into an identical state. Milestone 1 required no engine architecture change.

Milestone 1 is functionally complete and awaiting certification. Future work (Milestone 2) extends the runtime through the same composition/authored-data approach rather than redesigning it.