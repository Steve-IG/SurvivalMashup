# Gameplay

**Purpose:** Gameplay-specific implementations built on top of engine systems (Player, Companion, Enemy, NPC, Region, World, Camera, Interaction).

**Owner:** Core Gameplay.

**Assembly:** `ToyChest.Gameplay`

**May reference:** `ToyChest.Core`, `ToyChest.Framework`, `ToyChest.Systems.*`.

**Must never reference:** UI assemblies. UI observes gameplay state and sends player intent; it never owns gameplay rules.

Gameplay composes systems. It should rarely implement low-level functionality.
