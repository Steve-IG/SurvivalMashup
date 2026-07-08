#!/usr/bin/env python3
"""CLI entry point for the ToyChest Asset Knowledge Base generator."""

from __future__ import annotations

import argparse
import logging
import sys
from pathlib import Path

from asset_parser import load_csv_rows
from asset_scraper import AssetScraper
from cache_manager import CacheManager
from json_writer import (
    load_asset_knowledge_base_json,
    load_existing_stable_ids,
    write_asset_knowledge_base_json,
)
from markdown_writer import write_markdown_outputs
from models import KnowledgeBaseAsset
from taxonomy import TOP_LEVEL_CATEGORIES, StableIdRegistry, enrich_asset, load_overrides
from utils import resolve_paths, setup_logging

logger = logging.getLogger(__name__)


def build_parser() -> argparse.ArgumentParser:
    """Create the CLI argument parser."""
    parser = argparse.ArgumentParser(
        description="Generate the ToyChest Unity Asset Knowledge Base from an Asset Store CSV export.",
    )
    parser.add_argument("csv_path", type=Path, help="Path to the Unity Asset Store CSV export")
    parser.add_argument(
        "--refresh",
        action="store_true",
        help="Force re-download of all Asset Store pages",
    )
    parser.add_argument(
        "--tier",
        type=int,
        choices=(1, 2, 3),
        help="Only generate Markdown docs for the specified tier",
    )
    parser.add_argument(
        "--category",
        choices=TOP_LEVEL_CATEGORIES,
        help="Only generate Markdown docs for the specified category",
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        help="Output directory for generated documentation",
    )
    parser.add_argument(
        "--cache-dir",
        type=Path,
        help="Cache directory for scraped Asset Store pages",
    )
    parser.add_argument(
        "--overrides",
        type=Path,
        help="Path to taxonomy_overrides.yaml",
    )
    parser.add_argument(
        "--rate-limit",
        type=float,
        default=1.5,
        help="Seconds to wait between Asset Store requests (default: 1.5)",
    )
    parser.add_argument(
        "--verbose",
        action="store_true",
        help="Enable debug logging",
    )
    return parser


def main(argv: list[str] | None = None) -> int:
    """Run the Asset Knowledge Base generator."""
    parser = build_parser()
    args = parser.parse_args(argv)
    setup_logging(logging.DEBUG if args.verbose else logging.INFO)

    tool_dir = Path(__file__).resolve().parent
    output_dir, cache_dir = resolve_paths(tool_dir, args.output_dir, args.cache_dir)
    overrides_path = args.overrides or (tool_dir / "taxonomy_overrides.yaml")

    if not args.csv_path.exists():
        logger.error("CSV file not found: %s", args.csv_path)
        return 1

    logger.info("CSV input: %s", args.csv_path.resolve())
    logger.info("Output directory: %s", output_dir)
    logger.info("Cache directory: %s", cache_dir)
    logger.info("Overrides file: %s", overrides_path)

    rows = load_csv_rows(args.csv_path)
    scraper = AssetScraper(rate_limit_seconds=args.rate_limit)
    cache_manager = CacheManager(cache_dir=cache_dir, scraper=scraper)
    overrides = load_overrides(overrides_path)

    json_path = output_dir / "AssetKnowledgeBase.json"
    id_registry = StableIdRegistry(load_existing_stable_ids(json_path))

    enriched_assets: list[KnowledgeBaseAsset] = []
    failures = 0

    for index, row in enumerate(rows, start=1):
        logger.info("[%d/%d] Processing %s", index, len(rows), row["name"])
        try:
            metadata, record = cache_manager.load_or_fetch(row, refresh=args.refresh)
            if record.scrape_error:
                failures += 1
                logger.warning("Scrape issue for %s: %s", row["name"], record.scrape_error)
            enriched_assets.append(enrich_asset(metadata, overrides, id_registry))
        except Exception as exc:  # noqa: BLE001 - CLI should continue on per-asset failures
            failures += 1
            logger.exception("Failed to process %s: %s", row["name"], exc)

    write_asset_knowledge_base_json(enriched_assets, output_dir)
    json_assets = load_asset_knowledge_base_json(json_path)
    write_markdown_outputs(
        json_assets,
        output_dir,
        tier_filter=args.tier,
        category_filter=args.category,
    )

    logger.info(
        "Completed generation for %d assets (%d scrape issues).",
        len(enriched_assets),
        failures,
    )
    return 0 if enriched_assets else 1


if __name__ == "__main__":
    sys.exit(main())
