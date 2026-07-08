# Documentation/Assets/AI-Decision-Rules.md

# AI Decision Rules

Before implementing new gameplay, rendering, networking, or tooling functionality:

1. Search `AssetKnowledgeBase.json`.
2. Prefer owned assets listed below.
3. Reuse existing frameworks through adapters.
4. Avoid duplicate functionality.

---

## Primary Rule

If ToyChest already owns a suitable asset, evaluate that asset before proposing custom implementation.

---

## Generated Recommendation Rules

IF icons is requested

Recommend **500 Resource Icons** (UI-004) before suggesting custom implementation.

IF multiplayer prototype is requested

Recommend **Auto-Battle Framework** (NET-002) before suggesting custom implementation.

IF UI framework is requested

Recommend **Better UI** (UI-003) before suggesting custom implementation.

IF procedural generation is requested

Recommend **Dungeon Architect** (WORLD-001) before suggesting custom implementation.

IF UI framework is requested

Recommend **EnhancedScroller** (UI-002) before suggesting custom implementation.

IF icons is requested

Recommend **Flat Icons Megapack** (UI-006) before suggesting custom implementation.

IF character controller is requested

Recommend **JU TPS 3 - Third Person Shooter GameKit + Vehicle Physics** (GAME-002) before suggesting custom implementation.

IF UI framework is requested

Recommend **Modular Game UI Kit** (UI-001) before suggesting custom implementation.

IF character animation is requested

Recommend **Motion Matching for Unity** (ANIM-001) before suggesting custom implementation.

IF multiplayer prototype is requested

Recommend **PUN 2 - FREE** (NET-001) before suggesting custom implementation.

IF UI framework is requested

Recommend **RPG & MMO UI X** (UI-005) before suggesting custom implementation.

IF lip sync is requested

Recommend **SALSA LipSync Suite** (ANIM-002) before suggesting custom implementation.

IF procedural generation is requested

Recommend **Space Graphics Toolkit** (WORLD-002) before suggesting custom implementation.

IF character controller is requested

Recommend **Starter Assets - ThirdPerson | URP** (GAME-003) before suggesting custom implementation.

IF character controller is requested

Recommend **TopDown Engine** (GAME-004) before suggesting custom implementation.

IF inventory is requested

Recommend **Vault Inventory** (GAME-001) before suggesting custom implementation.


---

## Never

Do not:

- Reimplement inventory systems when Vault Inventory is available.
- Reimplement character controllers before evaluating owned controller assets.
- Replace production-approved packages unless explicitly instructed.
- Modify Asset Store package source directly.

---

## Preferred Architecture

```
Gameplay

↓

Adapter Layer

↓

Asset Store Package
```

---

## Evaluation Order

1. Read the asset documentation.
2. Import into a sandbox project.
3. Evaluate extension points.
4. Determine production readiness.
5. Record findings in the knowledge base.

