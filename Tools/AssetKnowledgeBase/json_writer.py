"""JSON output writer for the Asset Knowledge Base."""

from __future__ import annotations

import json
import logging
import re
from datetime import UTC, datetime
from pathlib import Path
from typing import Any

from models import KnowledgeBaseAsset

logger = logging.getLogger(__name__)

STABLE_ID_PATTERN = re.compile(r"^[A-Z]{2,6}-\d{3}$")


def is_stable_id(value: str) -> bool:
    """Return True when value matches the PREFIX-NNN stable ID format."""
    return bool(STABLE_ID_PATTERN.match(value))


def load_existing_stable_ids(json_path: Path) -> dict[str, str]:
    """Load existing store_id -> stable_id mappings for stable ID preservation."""
    if not json_path.exists():
        return {}

    with json_path.open(encoding="utf-8") as handle:
        payload = json.load(handle)

    mapping: dict[str, str] = {}
    for asset in payload.get("assets", []):
        if not isinstance(asset, dict):
            continue
        store_id = str(asset.get("store_id") or asset.get("metadata", {}).get("id") or "")
        stable_id = str(asset.get("id") or asset.get("toychest", {}).get("stable_id") or "")
        if store_id and stable_id and is_stable_id(stable_id):
            mapping[store_id] = stable_id
    return mapping


def write_asset_knowledge_base_json(
    assets: list[KnowledgeBaseAsset],
    output_dir: Path,
) -> Path:
    """Write the canonical flat AssetKnowledgeBase.json file."""
    output_dir.mkdir(parents=True, exist_ok=True)
    output_path = output_dir / "AssetKnowledgeBase.json"

    sorted_assets = sorted(assets, key=lambda asset: asset.sort_key)
    payload = {
        "generated_at": datetime.now(UTC).isoformat(),
        "asset_count": len(sorted_assets),
        "assets": [asset.to_flat_dict() for asset in sorted_assets],
    }

    with output_path.open("w", encoding="utf-8") as handle:
        json.dump(payload, handle, indent=2, sort_keys=True)
        handle.write("\n")

    logger.info("Wrote %s (%d assets)", output_path, len(sorted_assets))
    return output_path


def load_asset_knowledge_base_json(json_path: Path) -> list[dict[str, Any]]:
    """Load canonical asset records from JSON."""
    with json_path.open(encoding="utf-8") as handle:
        payload = json.load(handle)
    assets = payload.get("assets", [])
    if not isinstance(assets, list):
        return []
    return sorted(
        [asset for asset in assets if isinstance(asset, dict)],
        key=lambda item: str(item.get("name", "")).casefold(),
    )
