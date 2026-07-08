# Tag System

**Architecture:** ToyChest v1.0
**Status:** Living Specification
**Owner:** Gameplay Systems

---

## System Ownership

This system owns:
- Tag vocabulary, tag definitions, the tag hierarchy, tag storage, tag query APIs, and aggregated active tag sets.

This system does NOT own:
- Behavior triggered by tags, Damage reactions, AI decisions, Ability rules, or presentation.

Primary Responsibilities:
- Provide a passive universal descriptor layer for identity, state, capability, and gameplay queries.

Primary Data:
- Tag definitions, hierarchical tag paths, tag categories, stable identifiers, and metadata.

Primary Runtime Objects:
- The Gameplay Tag Table (interned hierarchy), Gameplay Tag Containers, and aggregated active tag sets.

Published Events:
- `GameplayTagAdded`, `GameplayTagRemoved` (category Tag). Published by the object's Gameplay Tag Container — the Gameplay Tag capability — on the absent/present transitions only, attributed to the owning `GameplayObjectId`. This completes the bridge between tag changes and the Event Bus while keeping tag ownership with the Tag System.

Consumed Events:
- None.

---

# Purpose

The Tag System provides a universal vocabulary for describing gameplay objects, gameplay state, gameplay capabilities, and gameplay relationships.

Tags allow systems to communicate without knowing about specific gameplay objects.

Instead of asking:

"Is this a Fire Enemy?"

systems ask:

"Does this object have the Fire tag?"

This makes gameplay highly composable and dramatically reduces coupling.

---

# Design Philosophy

Tags describe gameplay.

They never implement gameplay.

Tags answer questions.

Systems determine behavior.

The Tag System is intentionally passive.

---

# Core Principles

## Universal

Every Gameplay Object may have tags.

Examples:

Player

Enemy

Boss

Weapon

Projectile

Companion

Item

Tree

Ore

Shrine

Portal

Adventure

Ability

Status Effect

Region

---

## Data Driven

Tags should be authored through data.

New gameplay should rarely require adding new engine logic.

Instead:

Create new tags.

Configure systems to react.

---

## Composable

Gameplay complexity emerges by combining tags.

Example:

Mechanical

+

Wet

+

Burning

↓

Lightning Damage

↓

Steam

↓

Explosion

↓

Stunned

No custom gameplay code required.

---

# Tag Hierarchy

Tags are hierarchical. This model was approved during the Milestone 0 architecture review and supersedes the earlier flat vocabulary.

A tag is a dot-separated path of segments:

```
Combat
Combat.Melee
Combat.Ranged

Element
Element.Fire
Element.Fire.Burning

Interaction
Interaction.Harvest
Interaction.Open
```

Registering a tag automatically registers every ancestor on its path. `Element.Fire.Burning` implies that `Element.Fire` and `Element` exist.

## Matching Semantics

Queries support ancestor matching:

- An object holding `Element.Fire.Burning` matches queries for `Element.Fire.Burning`, `Element.Fire`, and `Element`.
- Matching is directional. Holding `Element.Fire` does **not** match a query for `Element.Fire.Burning`. The child is more specific than what the object claims to be.
- Exact-match queries are available for the rare cases where hierarchy must be ignored.

## Hierarchy Design Guidance

- Parents express general families; leaves express specifics.
- Prefer deepening an existing family over inventing a new root when the meaning is related. `Element.Lightning` beats a new `Lightning` root.
- Keep depth practical — two to four segments cover nearly every case.
- Systems should query at the most general level that expresses their rule. Fire resistance cares about `Element.Fire`, not about every burning variant.

---

# Tag Categories

## Identity Tags

Identity Tags describe what something fundamentally is.

Examples:

Player

Enemy

Boss

Companion

NPC

Tree

Plant

Ore

Sword

Bow

Staff

Fire

Ice

Mechanical

Undead

Animal

Building

Portal

Merchant

Identity Tags are typically permanent.

---

## State Tags

State Tags describe temporary gameplay conditions.

Examples:

Burning

Frozen

Wet

Poisoned

Electrified

Invisible

Flying

Swimming

Harvesting

Dead

Shielded

Channeling

Rooted

Moving

Most State Tags change continuously during gameplay.

---

## Capability Tags

Capability Tags describe what an object can do or how it may be interacted with.

Examples:

Harvestable

Interactable

Craftable

Upgradeable

Breakable

Flammable

Conductive

Freezable

Rideable

Climbable

Talkable

Tradable

Capability Tags rarely change.

---

# Semantic Tags vs State Tags

Tags fall into two broad kinds, and systems should treat them differently.

**Semantic Tags** describe what an object *is* or *can do*. Identity Tags and Capability Tags are semantic. They are typically permanent, usually authored on the definition, and change rarely if ever during play. Systems use semantic tags to decide whether an interaction is possible at all: a Harvest ability requires `Harvestable`; Fire damage looks for `Element.Fire` weakness.

**State Tags** describe an object's *current, temporary condition*. They change continuously during play and are almost always contributed by transient runtime sources (status effects, abilities, world reactions). Systems use state tags to decide what is happening right now: `State.Burning`, `State.Frozen`, `State.Channeling`.

The practical distinctions:

- **Volatility:** semantic tags are stable; state tags churn. Tooling and networking may treat them differently for this reason.
- **Source:** semantic tags usually come from the object's own definition; state tags usually come from other runtime systems.
- **Publication:** state-tag transitions are the interesting events for reactive systems (UI, audio, AI). Semantic-tag changes are rare enough to be treated as exceptional.

This distinction is a modeling guideline, not an engine branch. The Tag System stores and queries both kinds identically; only authors and reacting systems care which kind a given tag is. Prefer namespacing state tags under a `State.` root so the two kinds are visually obvious in data and traces.

---

# Tag Ownership

Tags may originate from many systems.

Examples:

Gameplay Object

Equipment

Status Effect

Ability

Region

Adventure

World Reaction

Difficulty

Companion

World Event

The active tag set is the union of all contributing sources.

## Runtime Ownership Rules

Runtime tag ownership is explicit and reference-counted. These rules are how the union above is actually maintained without sources corrupting one another:

- **Each source owns only the tags it added.** A source adds a tag when its contribution begins (equip, status apply, region enter) and removes exactly that tag when its contribution ends (unequip, status expire, region exit).
- **Tags are reference-counted per object.** If three sources contribute `State.Wet`, the tag is present with a count of three. It becomes absent only when the last source removes it. A source must never remove a tag it did not add.
- **Presence is a transition, not a toggle.** The container raises Tag Added only when a tag goes from absent to present, and Tag Removed only when it goes from present to absent. Intermediate count changes are silent. Reacting systems therefore see a clean on/off signal regardless of how many sources overlap.
- **No source may clear the whole set.** There is no "remove all tags" authority, because that would destroy other sources' contributions. Tearing down an object destroys its container outright instead.
- **The Gameplay Tag Container owns the aggregation.** Individual systems own their own add/remove calls; the container owns the counts, the ancestor roll-up, and the transition notifications. No system reads or mutates another system's counts.

---

# Tag Change Events

The Gameplay Tag Container is the Gameplay Tag capability component, and it owns publication of tag change facts to the Event Bus. On the absent-to-present transition it publishes `GameplayTagAdded`; on the present-to-absent transition it publishes `GameplayTagRemoved`. Both carry the owning `GameplayObjectId` and the tag.

- **Transitions only.** Because tags are reference-counted, intermediate count changes are silent. Reacting systems (UI, audio, AI, World Reaction) see a clean on/off signal regardless of how many sources overlap, matching the ownership rules above.
- **Ownership preserved.** The Tag System owns tag facts; no other system publishes them. This completes the bridge that earlier docs deferred, without moving tag ownership into the framework or the Event System.
- **Composed with a bus, or silent.** A container composed with an Event Bus publishes; a container built without one (isolated tests) still raises its local C# callbacks. Publication is additive over the existing transition callbacks.

Enumeration for serialization, debugging, and tooling (`CopyTagsTo`) returns the present tags in deterministic registration order (Engine Principle 17), so a container's serialized tag list is stable.

# Persistence Boundary

Per Engine Principle 25:

- **Authoritative:** per object, the set of tag paths currently present and, where it matters, the count and source of each contribution. In practice most state tags are contributed by other runtime systems (status effects, equipment, regions) and are reconstructed when those sources are reconstructed.
- **Derived:** the interned `GameplayTag` handles (session-local), the hierarchical ancestor counts, and all query results — rebuilt from the present tags and the tag hierarchy.
- **Serialized:** tag *paths* (stable, human-readable), never interned indices. Only genuinely authoritative tags — those not re-established by another reconstructing system — need saving.
- **Reconstructed:** the container is rebuilt empty; sources re-add their tags during their own reconstruction (a restored status re-grants its tags), and ancestor counts are recomputed on each add. Reconstruction adds tags through the normal path; broad event-quiet reconstruction is a Save Framework concern.

# Queries

Systems should query tags rather than gameplay classes.

Examples:

HasTag() — hierarchical; matches the tag or any descendant the object holds.

HasTagExact() — ignores hierarchy.

HasAllTags() — hierarchical; every queried tag must match.

HasAnyTag() — hierarchical; at least one queried tag must match.

HasNone() — hierarchical; no queried tag may match.

TagCount()

Querying tags should be inexpensive. Hierarchical queries are O(1) lookups against pre-aggregated ancestor counts, not tree walks.

---

# Gameplay Usage

Tags may influence:

Ability Activation

Gameplay Effects

Damage

AI

Loot

Dialogue

Adventure Progress

Crafting

World Reaction

Movement

Animation

Audio

UI

Saving

Networking

The Tag System should be usable everywhere.

---

# Ability Examples

Fireball

Requires:

Magic

Projectile

Deals Bonus Damage To:

Plant

Frozen

Cannot Target:

Friendly

Invisible

---

Harvest

Requires:

Harvestable

Produces Bonus Loot From:

Rare

Magical

---

# Damage Examples

Fire Damage

Bonus vs:

Plant

Weak vs:

Fire Resistant

Ignored by:

Fire Immune

---

Lightning Damage

Bonus vs:

Wet

Mechanical

---

# AI Examples

Enemy evaluates:

Player

Visible

Burning

LowHealth

Flying

Boss

Rather than specific gameplay classes.

---

# Status Effect Examples

Burning adds:

Burning

Fire

Hot

Frozen adds:

Frozen

Cold

Wet removes:

Burning

Applies:

Wet

Cold

The Tag System enables interactions without hardcoded knowledge.

---

# Equipment Examples

Sword grants:

Melee

Steel

Weapon

Legendary Sword grants:

Legendary

Holy

Sword

Weapon

---

# Region Examples

Volcano

Adds:

Hot

Fire

Lava

Mountain

Frozen Tundra

Adds:

Snow

Cold

Ice

These tags influence gameplay systems naturally.

---

# Multiplayer

Tags must support:

Replication

Prediction

Rollback

Deterministic evaluation

Tags should always remain synchronized.

---

# AI

Tags provide semantic understanding.

AI reasons about tags rather than object types.

This enables generic decision making.

---

# Editor Tooling

Extension points for tag tooling (editor-only, layered on the runtime system without changing it):

- **Gameplay Tag Browser (future):** an editor window that displays the full interned tag hierarchy as a tree, shows each tag's definition, description, and category, and lets designers pick tags for authoring fields instead of typing raw paths. It reads the Gameplay Tag Table and the registered `TagDefinition` assets; it introduces no runtime behavior. This is the authoring counterpart to the Milestone 0 runtime Gameplay Tag viewer.
- **Tag validation pass (future):** flags authored tag paths that reference unregistered tags, duplicate definitions, or naming-convention violations.

---

# Future Expansion

Future systems should prefer adding tags over creating custom logic.

The Tag vocabulary should expand as content expands.

The engine should remain stable.

---

# Success Criteria

The Tag System succeeds when:

- Systems communicate using tags.
- New gameplay requires mostly new tags and data.
- AI understands gameplay semantically.
- Multiplayer remains deterministic.
- Designers create complex interactions without programming.

---

# Implementation Notes

- Tags are authored as immutable `TagDefinition` assets (stable ID = the tag path) and registered through the Data Registry at startup; code may also register paths directly for tests and tooling.
- Tag paths are interned once per session into the Gameplay Tag Table. Runtime identity is an interned index (`GameplayTag`), never a string. Paths appear only at authoring, serialization, and debugging boundaries.
- Tag Containers maintain counted tag multisets: the same tag added by several sources (equipment, status effects, region) is reference-counted, and the tag disappears only when every source removes it.
- Containers additionally maintain aggregated ancestor counts, making hierarchical `HasTag` queries O(1) dictionary lookups with zero allocation.
- The table and containers are plain C# and fully testable outside Unity.
- Tags remain lightweight descriptors and never contain gameplay logic.
- Serialization stores tag paths (human-readable and stable across sessions); the interned indices are session-local and never persisted.