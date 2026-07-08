# Core

**Purpose:** Project-wide infrastructure: bootstrap, service registration, configuration, logging.

**Owner:** Core Architecture.

**Assembly:** `ToyChest.Core`

**May reference:** Unity engine modules only.

**Must never reference:** Framework, Systems, Gameplay, UI, or content.

Core exposes capabilities. It owns no gameplay state and decides no gameplay outcomes. Keep this layer small and stable.
