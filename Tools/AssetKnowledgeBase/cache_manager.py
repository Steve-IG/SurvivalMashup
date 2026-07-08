"""Cache management for scraped Asset Store metadata."""

from __future__ import annotations

import json
import logging
from datetime import UTC, datetime
from pathlib import Path

from asset_parser import metadata_from_csv_row, parse_asset_store_html
from asset_scraper import AssetScraper
from models import AssetMetadata, CachedAssetRecord
from utils import extract_asset_id, parse_bool, slugify

logger = logging.getLogger(__name__)


class CacheManager:
    """Read and write per-asset scrape caches."""

    def __init__(self, cache_dir: Path, scraper: AssetScraper) -> None:
        self.cache_dir = cache_dir
        self.scraper = scraper
        self.cache_dir.mkdir(parents=True, exist_ok=True)

    def cache_path_for(self, asset_name: str, asset_id: str) -> Path:
        """Return the cache file path for an asset."""
        slug = slugify(asset_name)
        if asset_id:
            return self.cache_dir / f"{slug}_{asset_id}.json"
        return self.cache_dir / f"{slug}.json"

    def load_or_fetch(
        self,
        csv_row: dict[str, str],
        refresh: bool = False,
    ) -> tuple[AssetMetadata, CachedAssetRecord]:
        """Load metadata from cache or scrape the Asset Store page."""
        baseline = metadata_from_csv_row(csv_row)
        cache_path = self.cache_path_for(baseline.name, baseline.id)

        if cache_path.exists() and not refresh:
            record = self._read_cache(cache_path)
            if record.raw_html:
                metadata = parse_asset_store_html(record.raw_html, csv_row)
            else:
                metadata = AssetMetadata.from_dict(record.parsed_metadata)
            metadata = self._merge_csv_fields(metadata, csv_row)
            record.parsed_metadata = metadata.to_dict()
            self._write_cache(cache_path, record)
            return metadata, record

        html, error = self.scraper.fetch(csv_row["url"])
        if html:
            metadata = parse_asset_store_html(html, csv_row)
        else:
            metadata = self._merge_csv_fields(baseline, csv_row)

        record = CachedAssetRecord(
            scrape_timestamp=datetime.now(UTC).isoformat(),
            raw_html=html,
            parsed_metadata=metadata.to_dict(),
            csv_fields=dict(csv_row),
            scrape_error=error or "",
        )
        self._write_cache(cache_path, record)
        return metadata, record

    def reparse_from_cache(self, cache_path: Path, csv_row: dict[str, str]) -> AssetMetadata:
        """Re-parse cached HTML without downloading again."""
        record = self._read_cache(cache_path)
        if record.raw_html:
            metadata = parse_asset_store_html(record.raw_html, csv_row)
        else:
            metadata = metadata_from_csv_row(csv_row)
        metadata = self._merge_csv_fields(metadata, csv_row)
        record.parsed_metadata = metadata.to_dict()
        self._write_cache(cache_path, record)
        return metadata

    def _merge_csv_fields(self, metadata: AssetMetadata, csv_row: dict[str, str]) -> AssetMetadata:
        """Ensure CSV-owned fields remain authoritative."""
        metadata.name = csv_row.get("name", metadata.name) or metadata.name
        metadata.asset_store_url = csv_row.get("url", metadata.asset_store_url)
        metadata.id = extract_asset_id(metadata.asset_store_url) or metadata.store_id
        if csv_row.get("purchase_date"):
            metadata.purchase_date = csv_row["purchase_date"]
        if csv_row.get("last_update"):
            metadata.last_update = csv_row["last_update"]
        if csv_row.get("version"):
            metadata.version = csv_row["version"]
        if csv_row.get("deprecated"):
            metadata.deprecated = metadata.deprecated or parse_bool(csv_row["deprecated"])
        return metadata

    def _read_cache(self, cache_path: Path) -> CachedAssetRecord:
        """Load a cache record from disk."""
        with cache_path.open(encoding="utf-8") as handle:
            data = json.load(handle)
        return CachedAssetRecord.from_dict(data)

    def _write_cache(self, cache_path: Path, record: CachedAssetRecord) -> None:
        """Persist a cache record to disk."""
        with cache_path.open("w", encoding="utf-8") as handle:
            json.dump(record.to_dict(), handle, indent=2, sort_keys=True)
            handle.write("\n")
        logger.debug("Wrote cache %s", cache_path.name)
