# AI Coding Standards

---

# Purpose

These standards define how software is built on this project.

The goal is consistency, maintainability, scalability, and rapid iteration.

These standards apply equally to human developers and AI assistants.

If a proposed implementation conflicts with these standards, the standards take precedence unless a documented architectural decision says otherwise.

---

# Core Engineering Principles

Always optimize for:

1. Readability
2. Simplicity
3. Maintainability
4. Extensibility
5. Performance

Never sacrifice readability for cleverness.

---

# AI Responsibilities

Before writing code:

- Read `Docs/AI_AGENT_INDEX.md`.
- Read the AI Playbook.
- Read Current Sprint.
- Read Decision Log.
- Read relevant documentation.
- Search the existing project.
- Explain the proposed solution.

Never immediately generate code for large systems.

---

# Architecture Rules

Prefer composition over inheritance.

Use interfaces when appropriate.

Separate data from behavior.

MonoBehaviours should coordinate behavior rather than contain business logic.

Business logic should be testable without Unity.

Avoid God Objects.

Prefer dependency injection over global access.

Avoid tight coupling.

---

# Unity Standards

Unity Version

Unity 6

Rendering

URP

Input

New Input System only

Addressables

Required

Resources folder

Never use Resources for runtime content.

FindObjectOfType

Avoid.

Use dependency injection, serialized references, or service locators where appropriate.

SendMessage

Never.

Reflection

Avoid unless necessary.

Coroutines

Prefer async/await (UniTask if adopted) for asynchronous workflows where appropriate.

---

# Script Standards

One public class per file.

One responsibility per class.

Keep classes focused.

Prefer private fields with SerializeField.

Use readonly whenever possible.

Avoid public mutable fields.

Use explicit access modifiers.

Use XML documentation for public APIs.

---

# Naming

Classes

PascalCase

Methods

PascalCase

Private Fields

_camelCase

Interfaces

IInterface

Constants

UPPER_CASE only when truly constant.

Booleans

Use verbs.

Examples:

isAlive

hasWeapon

canJump

---

# Performance

Never allocate memory inside Update.

Pool frequently instantiated objects.

Avoid LINQ inside gameplay loops.

Cache component references.

Profile before optimizing.

Measure before making assumptions.

---

# Logging

Never leave excessive Debug.Log calls.

Use a centralized logging service if one exists.

Error messages should explain both:

What happened

What to check

---

# Error Handling

Fail early.

Fail clearly.

Never silently ignore errors.

Use assertions during development.

---

# Asset Store Integration

Before writing custom systems:

Search available project assets.

Prefer extending proven assets over rewriting them.

Document third-party dependencies.

Avoid modifying vendor assets directly.

Wrap third-party APIs behind project interfaces whenever practical.

---

# Documentation

Every major system should include:

Purpose

Responsibilities

Dependencies

Public API

Known limitations

Future improvements

---

# Code Review Checklist

Is this the simplest solution?

Does it duplicate existing functionality?

Is the architecture consistent?

Can this be tested?

Will another developer understand this in six months?

---

# Definition of Done

A feature is complete only when:

✔ Code compiles.

✔ No warnings.

✔ Documentation updated.

✔ Decision Log updated (if needed).

✔ Performance reviewed.

✔ Naming follows standards.

✔ Architecture remains consistent.

✔ Code reviewed.

✔ Feature tested.