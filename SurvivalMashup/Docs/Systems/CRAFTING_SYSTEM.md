# Crafting System

**Architecture:** ToyChest v1.0
**Status:** Living Specification
**Owner:** Gameplay Design

---

# Purpose

The Crafting System allows players to transform collected resources into useful items, equipment, consumables, upgrades, and other gameplay rewards.

Crafting complements exploration and loot by giving players agency over their progression while preserving the excitement of discovery.

The Crafting System operates on Item Instances and Recipes.

---

# Design Philosophy

Crafting is intentional progression.

Loot rewards discovery.

Crafting rewards planning.

Players should use crafting to pursue desired builds, complete collections, and make meaningful use of the resources they gather throughout the world.

---

# Core Goals

- Reward exploration.
- Give value to harvested resources.
- Support buildcraft.
- Reduce frustration caused by random drops.
- Reinforce regional identity.
- Encourage long-term collection.

---

# Crafting Inputs

Crafting may consume:

Resources

Crafting Materials

Equipment

Consumables

Currencies

Quest Items (rare)

Special Components

Future systems should integrate naturally.

---

# Crafting Outputs

Crafting may produce:

Equipment

Weapons

Armor

Consumables

Tools

Companion Equipment

Upgrade Materials

Relics

Cosmetics

Housing Objects (future)

Quest Items

Recipes

---

# Recipes

Recipes define crafting requirements.

A recipe may require:

Specific Items

Gameplay Tags

Item Categories

Crafting Station

Region Unlock

Quest Completion

Companion Assistance (future)

Recipes remain data-driven.

---

# Recipe Discovery

Players discover recipes through gameplay.

Examples:

Exploration

Boss Rewards

NPCs

Merchants

Books

Treasure Maps

Regional Completion

Seasonal Events

Recipe discovery is part of progression.

---

# Crafting Stations

Crafting may occur at specialized stations.

Examples:

Workbench

Forge

Alchemy Table

Cooking Pot

Companion Workshop

Enchanting Table (future)

Stations may unlock as the Hub grows.

---

# Regional Materials

Every region introduces meaningful materials.

Examples:

Volcanic Region

Molten Ore

Ember Crystal

Ash Wood

Frozen Region

Ice Crystal

Ancient Ice

Frozen Bark

Forest Region

Living Wood

Bloom Flower

Nature Resin

Regional materials reinforce exploration.

---

# Material Identity

Materials should possess identity beyond rarity.

Examples:

Heat Resistant

Conductive

Organic

Explosive

Magical

Mechanical

These identities support multiple crafting paths.

---

# Buildcraft

Crafting should support intentional builds.

Examples:

Fire-focused equipment

Companion equipment

Harvesting gear

Movement gear

Support equipment

Players should feel empowered to pursue desired playstyles.

---

# Upgrading Equipment

Crafting may improve existing equipment.

Examples:

Increase Quality

Improve Affixes

Repair Durability

Add Sockets (future)

Unlock Evolution

Upgrade paths should preserve player investment.

---

# Economy Integration

Crafting interacts with:

Merchants

Trading

Resource gathering

Loot

Hub progression

Crafting should create meaningful economic decisions.

---

# Companion Integration

Future possibilities include:

Companions gathering materials.

Companions assisting with crafting.

Companion-specific recipes.

Companion equipment.

Companions should contribute outside combat.

---

# Multiplayer

Players may:

Craft together.

Share materials.

Share recipes.

Trade crafted items.

Crafting should reinforce cooperation.

---

# Future Expansion

Examples:

Enchanting

Rune Crafting

Item Evolution

Masterwork Crafting

Procedural Recipes

Seasonal Recipes

Housing Crafting

Vehicle Crafting

No architectural redesign should be required.

---

# Uses ToyChest Systems

Item System

Inventory System

Itemization

Equipment System

Loot System

Region System

Gameplay Tags

Gameplay Events

Economy

Hub World

---

# Success Criteria

The Crafting System succeeds when:

- Gathering resources always feels meaningful.
- Recipes encourage exploration.
- Players intentionally pursue desired builds.
- Regional materials remain valuable.
- Crafting complements rather than replaces loot.
- Long-term collection is rewarding.

---

# Implementation Notes

- Represent recipes as immutable data definitions.
- Consume and produce Item Instances.
- Validate crafting requirements through generic queries (items, tags, stations, progression).
- Keep crafting outcomes deterministic unless explicitly designed otherwise.
- Favor recipes that create new gameplay opportunities instead of only higher statistics.