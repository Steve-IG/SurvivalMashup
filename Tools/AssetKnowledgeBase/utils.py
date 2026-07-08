"""Shared utilities for the Asset Knowledge Base generator."""

from __future__ import annotations

import logging
import re
from pathlib import Path


COLUMN_ALIASES: dict[str, tuple[str, ...]] = {
    "name": ("asset name", "assetname", "name", "title"),
    "url": ("asset store url", "asset url", "url", "link", "assetstoreurl"),
    "version": ("version", "asset version"),
    "purchase_date": ("purchase date", "purchased date", "date purchased", "purchasedate"),
    "last_update": ("last updated", "last update", "updated", "lastupdated"),
    "deprecated": ("deprecated", "is deprecated"),
}

USER_AGENT = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
    "AppleWebKit/537.36 (KHTML, like Gecko) "
    "Chrome/124.0.0.0 Safari/537.36"
)


def setup_logging(level: int = logging.INFO) -> None:
    """Configure root logger for CLI output."""
    logging.basicConfig(
        level=level,
        format="%(asctime)s [%(levelname)s] %(message)s",
        datefmt="%Y-%m-%d %H:%M:%S",
    )


def normalize_header(header: str) -> str:
    """Normalize a CSV header for fuzzy matching."""
    return re.sub(r"[^a-z0-9]+", " ", header.strip().casefold()).strip()


def slugify(value: str, fallback: str = "asset") -> str:
    """Convert a string to a filesystem-safe slug."""
    slug = re.sub(r"[^a-z0-9]+", "_", value.strip().casefold()).strip("_")
    return slug or fallback


def extract_asset_id(url: str) -> str:
    """Extract numeric asset ID from an Asset Store URL."""
    match = re.search(r"/(?:slug|packages/[^/]+)/(\d+)(?:[/?#]|$)", url)
    if match:
        return match.group(1)
    match = re.search(r"(\d{4,})", url)
    return match.group(1) if match else ""


def resolve_paths(
    tool_dir: Path,
    output_dir: Path | None = None,
    cache_dir: Path | None = None,
) -> tuple[Path, Path]:
    """Resolve default output and cache directories relative to the tool."""
    resolved_output = output_dir or (tool_dir / ".." / ".." / "SurvivalMashup" / "Docs" / "Assets")
    resolved_cache = cache_dir or (tool_dir / "cache")
    return resolved_output.resolve(), resolved_cache.resolve()


def category_filename(category: str) -> str:
    """Convert a category name to a Markdown filename."""
    return f"{category.replace(' ', '-')}.md"


def parse_bool(value: str) -> bool:
    """Parse a boolean from CSV text."""
    normalized = value.strip().casefold()
    return normalized in {"1", "true", "yes", "y", "deprecated"}
