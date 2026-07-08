# Documentation/Assets/README.md

# Asset Knowledge Base

## Purpose

The Asset Knowledge Base is the authoritative reference for every Unity Asset Store package owned by the ToyChest project.

Its purpose is to:

- Prevent duplicate purchases.
- Prevent unnecessary custom implementations.
- Help AI assistants choose existing assets before suggesting new systems.
- Record evaluations and integration notes.
- Track package updates and production readiness.

This documentation is considered part of the project's architecture.

---

# Organization

```
Documentation/
└── Assets/
    ├── README.md
    ├── Unity-Asset-Catalog.md
    ├── Recommended-Assets.md
    ├── AI-Decision-Rules.md
    └── Assets/
```

---

# Document Overview

## Unity-Asset-Catalog.md

Master index of every owned asset.

Contains:

- Name
- Category
- Publisher
- Version
- Last Updated
- Purchase Date
- Status
- Evaluation Score

---

## Recommended-Assets.md

Maps common gameplay systems to preferred assets.

Examples:

- Inventory
- Character Controller
- Dialogue
- World Generation
- Multiplayer
- UI
- Save System
- AI
- Terrain

---

## AI-Decision-Rules.md

Rules that AI coding assistants should follow before implementing new systems.

---

## Assets/

Contains one page per asset.

Example:

```
Assets/
    Dungeon Architect.md
    Vault Inventory.md
    JU TPS 3.md
```

---

# Evaluation Workflow

Every newly purchased asset follows this lifecycle.

```
Purchased

↓

Imported

↓

Evaluated

↓

Prototype Ready

↓

Production Approved
```

---

# Status Definitions

## Not Evaluated

Purchased but never imported.

## Imported

Imported into a sandbox project.

## Evaluated

Core functionality tested.

## Prototype Ready

Approved for prototype work.

## Production Approved

Approved for production use.

## Deprecated

No longer recommended.

---

# Ownership

The Unity Asset Store CSV export is the source of truth for:

- Purchase Date
- Version
- Update Date
- Asset Store URL

Manual edits should only add project-specific information.

---

# Goals

The Asset Knowledge Base should allow developers and AI assistants to answer questions such as:

- Do we already own this functionality?
- Which package should be used?
- Which package is preferred?
- Which packages overlap?
- Which packages are production ready?