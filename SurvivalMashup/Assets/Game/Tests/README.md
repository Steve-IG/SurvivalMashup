# Tests

**Purpose:** Automated tests for foundational systems, organized by system ownership (not by test type).

**Owner:** Each system owns its tests (`Tags/`, `Attributes/`, `Resources/`, `Events/`, ...).

**Assembly:** `ToyChest.Tests` (EditMode).

Business logic lives in plain C# classes, so systems are tested without scenes or PlayMode wherever practical.
