# Asset Knowledge Base Generator

Production tool for transforming a Unity Asset Store CSV export into structured documentation for humans and AI coding assistants on the ToyChest project.

## Features

- Reads Unity Asset Store CSV exports with automatic column detection
- Scrapes official Asset Store pages with rate limiting, retries, and exponential backoff
- Caches raw HTML and parsed metadata for fast regeneration
- Generates Markdown catalogs, tier docs, category docs, and a dependency graph
- Generates `AssetKnowledgeBase.json` optimized for LLM retrieval

## Requirements

- Python 3.12+
- Network access to `assetstore.unity.com`

## Installation

```bash
cd Tools/AssetKnowledgeBase
pip install -r requirements.txt
```

Optional editable install:

```bash
pip install -e .
```

## Usage

From `Tools/AssetKnowledgeBase`:

```bash
python build_asset_kb.py ../../SurvivalMashup/Docs/Assets/unity_assets_138.csv
```

### CLI Options

| Flag | Description |
|------|-------------|
| `--refresh` | Force re-download of all Asset Store pages |
| `--tier 1` | Only generate tier-filtered Markdown docs |
| `--category "Gameplay Systems"` | Only generate one category Markdown doc |
| `--output-dir PATH` | Override output directory |
| `--cache-dir PATH` | Override cache directory |
| `--rate-limit SECONDS` | Delay between HTTP requests (default: 1.5) |
| `--overrides PATH` | Path to `taxonomy_overrides.yaml` |
| `--verbose` | Enable debug logging |

Examples:

```bash
python build_asset_kb.py ../../SurvivalMashup/Docs/Assets/unity_assets_138.csv --refresh
python build_asset_kb.py ../../SurvivalMashup/Docs/Assets/unity_assets_138.csv --tier 1
python build_asset_kb.py ../../SurvivalMashup/Docs/Assets/unity_assets_138.csv --category Environment
```

## Canonical JSON

`AssetKnowledgeBase.json` is the source of truth. All Markdown files are generated from this JSON on every run.

Each asset record uses a stable ID such as `GAME-001`, `WORLD-001`, or `EDIT-001`. IDs are preserved across runs unless overridden in `taxonomy_overrides.yaml`.

## Manual Overrides

Curated metadata lives in `taxonomy_overrides.yaml`. When an asset is listed there, its ID, category, subcategory, tier, and recommendation override automatic classification.

Example:

```yaml
"Vault Inventory":
  id: GAME-001
  category: Gameplay Systems
  subcategory: Inventory
  tier: 1
  recommendation: Core Candidate
```

## Editing Taxonomy Rules

Automatic classification rules live in `taxonomy.py`:

- `STORE_CATEGORY_MAP` — official Asset Store category to ToyChest taxonomy
- `STORE_PATH_MAP` — Asset Store URL path hints
- `KEYWORD_RULES` — conservative keyword fallback only

Edit `taxonomy_overrides.yaml` for important assets instead of hand-editing generated Markdown.

## Output

By default, files are written to `SurvivalMashup/Docs/Assets/`:

| File | Description |
|------|-------------|
| `AssetKnowledgeBase.json` | Canonical structured knowledge base for AI tools |
| `Unity-Asset-Catalog.md` | Master catalog table and all asset entries |
| `Tier1-Assets.md` | Tier 1 assets |
| `Tier2-Assets.md` | Tier 2 assets |
| `Tier3-Assets.md` | Tier 3 assets |
| `{Category}.md` | One file per populated category |
| `Asset-Dependency-Graph.md` | Similar, competing, and complementary assets |
| `Alternative-Assets.md` | Strategic alternatives within owned library |
| `AI-Decision-Rules.md` | Generated AI recommendation rules |

Protected files that are never overwritten:

- `README.md`
- `RECOMMENDED_ASSETS.md`

## Regeneration

Normal regeneration uses the cache and completes in seconds:

```bash
python build_asset_kb.py ../../SurvivalMashup/Docs/Assets/unity_assets_138.csv
```

Force a full re-scrape after Asset Store page changes or parser updates:

```bash
python build_asset_kb.py ../../SurvivalMashup/Docs/Assets/unity_assets_138.csv --refresh
```

First full run for 138 assets takes about 3–4 minutes with the default 1.5 second rate limit.

## Cache

Cached scrape data is stored in `Tools/AssetKnowledgeBase/cache/`:

```
cache/
  dungeon_architect_53895.json
  vault_inventory_93933.json
```

Each cache file contains:

- `scrape_timestamp`
- `raw_html`
- `parsed_metadata`
- `csv_fields`
- `scrape_error` (if any)

The cache directory is gitignored. Delete individual cache files or use `--refresh` to re-download specific assets.

## Folder Layout

```
Tools/AssetKnowledgeBase/
  README.md
  requirements.txt
  pyproject.toml
  build_asset_kb.py
  asset_parser.py
  asset_scraper.py
  taxonomy.py
  markdown_writer.py
  json_writer.py
  cache_manager.py
  models.py
  utils.py
  taxonomy_overrides.yaml
  cache/
```

## CSV Input

The parser auto-detects columns using normalized header aliases. Supported fields:

- Asset Name
- Asset Store URL
- Version
- Purchase Date
- Last Updated
- Deprecated

Required columns: name and URL.

## Entry Template

Each generated asset entry follows this structure:

```
## Asset Name

Asset ID

Official Asset Store URL

Publisher

Category / Subcategory

Purpose

Key Features

Why We Own It

ToyChest Recommendation

Potential Uses

Potential Concerns

Evaluation

Status

Reviewed

Reviewer

Notes
```

Purpose and Key Features come from the Asset Store when available. Empty fields remain blank rather than using placeholders.

## Development Notes

- Deterministic output: assets are sorted alphabetically and JSON keys are sorted
- Scrape failures fall back to CSV-only metadata and continue processing
- Asset Store package source must never be modified in the Unity project; wrap and adapt instead
