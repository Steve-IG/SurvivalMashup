# Enemy System

**Architecture:** ToyChest v1.0
**Status:** Living Specification
**Owner:** Gameplay Design

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

Health

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

Health

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