"""Data models for the Asset Knowledge Base generator."""

from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum
from typing import Any


class Tier(str, Enum):
    """Asset priority tier for ToyChest integration."""

    TIER_1 = "Tier 1"
    TIER_2 = "Tier 2"
    TIER_3 = "Tier 3"

    @property
    def number(self) -> int:
        """Return numeric tier value."""
        return {Tier.TIER_1: 1, Tier.TIER_2: 2, Tier.TIER_3: 3}[self]


class Recommendation(str, Enum):
    """ToyChest recommendation classification."""

    CORE_CANDIDATE = "Core Candidate"
    PRODUCTION_CANDIDATE = "Production Candidate"
    PROTOTYPE_CANDIDATE = "Prototype Candidate"
    DEVELOPMENT_STANDARD = "Development Standard"
    REFERENCE = "Reference"
    NICE_TO_HAVE = "Nice to Have"


class AssetStatus(str, Enum):
    """Evaluation lifecycle status."""

    NOT_EVALUATED = "Not Evaluated"
    IMPORTED = "Imported"
    EVALUATED = "Evaluated"
    PROTOTYPE_READY = "Prototype Ready"
    PRODUCTION_APPROVED = "Production Approved"
    DEPRECATED = "Deprecated"


@dataclass
class AssetMetadata:
    """Metadata extracted from CSV and Asset Store pages."""

    store_id: str
    name: str
    publisher: str = ""
    description: str = ""
    purpose: str = ""
    features: list[str] = field(default_factory=list)
    unity_versions: list[str] = field(default_factory=list)
    render_pipelines: list[str] = field(default_factory=list)
    documentation_links: list[str] = field(default_factory=list)
    website: str = ""
    videos: list[str] = field(default_factory=list)
    asset_store_url: str = ""
    purchase_date: str = ""
    last_update: str = ""
    version: str = ""
    deprecated: bool = False
    store_category: str = ""
    store_category_path: str = ""
    release_date: str = ""
    latest_version: str = ""

    @property
    def id(self) -> str:
        """Backward-compatible alias for the Asset Store numeric ID."""
        return self.store_id

    @id.setter
    def id(self, value: str) -> None:
        self.store_id = value

    def to_dict(self) -> dict[str, Any]:
        """Serialize scraped metadata."""
        return {
            "store_id": self.store_id,
            "name": self.name,
            "publisher": self.publisher,
            "description": self.description,
            "purpose": self.purpose,
            "features": list(self.features),
            "unity_versions": list(self.unity_versions),
            "render_pipelines": list(self.render_pipelines),
            "documentation_links": list(self.documentation_links),
            "website": self.website,
            "videos": list(self.videos),
            "asset_store_url": self.asset_store_url,
            "purchase_date": self.purchase_date,
            "last_update": self.last_update,
            "version": self.version,
            "deprecated": self.deprecated,
            "store_category": self.store_category,
            "store_category_path": self.store_category_path,
            "release_date": self.release_date,
            "latest_version": self.latest_version,
        }

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> AssetMetadata:
        """Deserialize scraped metadata."""
        store_id = str(data.get("store_id") or data.get("id") or "")
        return cls(
            store_id=store_id,
            name=str(data.get("name", "")),
            publisher=str(data.get("publisher", "")),
            description=str(data.get("description", "")),
            purpose=str(data.get("purpose", "")),
            features=list(data.get("features", [])),
            unity_versions=list(data.get("unity_versions", [])),
            render_pipelines=list(data.get("render_pipelines", [])),
            documentation_links=list(data.get("documentation_links", [])),
            website=str(data.get("website", "")),
            videos=list(data.get("videos", [])),
            asset_store_url=str(data.get("asset_store_url", "")),
            purchase_date=str(data.get("purchase_date", "")),
            last_update=str(data.get("last_update", "")),
            version=str(data.get("version", "")),
            deprecated=bool(data.get("deprecated", False)),
            store_category=str(data.get("store_category", "")),
            store_category_path=str(data.get("store_category_path", "")),
            release_date=str(data.get("release_date", "")),
            latest_version=str(data.get("latest_version", "")),
        )


@dataclass
class ToyChestMetadata:
    """ToyChest-specific classification and evaluation metadata."""

    stable_id: str = ""
    category: str = "Utilities"
    subcategory: str = "General"
    tier: Tier = Tier.TIER_2
    recommendation: Recommendation = Recommendation.REFERENCE
    status: AssetStatus = AssetStatus.NOT_EVALUATED
    evaluation: str = ""
    reviewed: str = ""
    reviewer: str = ""
    notes: str = ""

    def to_dict(self) -> dict[str, Any]:
        """Serialize ToyChest metadata."""
        return {
            "stable_id": self.stable_id,
            "category": self.category,
            "subcategory": self.subcategory,
            "tier": self.tier.number,
            "recommendation": self.recommendation.value,
            "status": self.status.value,
            "evaluation": self.evaluation,
            "reviewed": self.reviewed,
            "reviewer": self.reviewer,
            "notes": self.notes,
        }


@dataclass
class AssetInsights:
    """ToyChest narrative fields for documentation."""

    why_we_own_it: str = ""
    potential_uses: str = ""
    potential_concerns: str = ""

    def to_dict(self) -> dict[str, str]:
        """Serialize insight fields."""
        return {
            "why_we_own_it": self.why_we_own_it,
            "potential_uses": self.potential_uses,
            "potential_concerns": self.potential_concerns,
        }


@dataclass
class CachedAssetRecord:
    """On-disk cache record for a scraped asset."""

    scrape_timestamp: str
    raw_html: str
    parsed_metadata: dict[str, Any]
    csv_fields: dict[str, str]
    scrape_error: str = ""

    def to_dict(self) -> dict[str, Any]:
        """Serialize cache record."""
        return {
            "scrape_timestamp": self.scrape_timestamp,
            "raw_html": self.raw_html,
            "parsed_metadata": self.parsed_metadata,
            "csv_fields": self.csv_fields,
            "scrape_error": self.scrape_error,
        }

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> CachedAssetRecord:
        """Deserialize cache record."""
        return cls(
            scrape_timestamp=str(data.get("scrape_timestamp", "")),
            raw_html=str(data.get("raw_html", "")),
            parsed_metadata=dict(data.get("parsed_metadata", {})),
            csv_fields={str(k): str(v) for k, v in data.get("csv_fields", {}).items()},
            scrape_error=str(data.get("scrape_error", "")),
        )


@dataclass
class KnowledgeBaseAsset:
    """Complete enriched asset record."""

    metadata: AssetMetadata
    toychest: ToyChestMetadata
    insights: AssetInsights

    def to_flat_dict(self) -> dict[str, Any]:
        """Serialize to the canonical flat JSON schema."""
        meta = self.metadata
        toy = self.toychest
        ins = self.insights
        unity_versions = ", ".join(meta.unity_versions) if meta.unity_versions else ""
        searchable_parts = [
            toy.stable_id,
            meta.name,
            meta.publisher,
            meta.purpose,
            meta.description,
            " ".join(meta.features),
            meta.store_category,
            meta.store_category_path,
            toy.category,
            toy.subcategory,
            toy.recommendation.value,
            toy.status.value,
            ins.why_we_own_it,
            ins.potential_uses,
            ins.potential_concerns,
        ]
        return {
            "id": toy.stable_id,
            "store_id": meta.store_id,
            "name": meta.name,
            "publisher": meta.publisher,
            "category": toy.category,
            "subcategory": toy.subcategory,
            "tier": toy.tier.number,
            "recommendation": toy.recommendation.value,
            "status": toy.status.value,
            "purpose": meta.purpose,
            "description": meta.description,
            "key_features": list(meta.features),
            "unity_versions": unity_versions,
            "render_pipelines": list(meta.render_pipelines),
            "documentation_links": list(meta.documentation_links),
            "videos": list(meta.videos),
            "website": meta.website,
            "asset_store_url": meta.asset_store_url,
            "purchase_date": meta.purchase_date,
            "last_updated": meta.last_update,
            "version": meta.version,
            "deprecated": meta.deprecated,
            "store_category": meta.store_category,
            "store_category_path": meta.store_category_path,
            "why_we_own_it": ins.why_we_own_it,
            "potential_uses": ins.potential_uses,
            "potential_concerns": ins.potential_concerns,
            "evaluation": toy.evaluation,
            "reviewed": toy.reviewed,
            "reviewer": toy.reviewer,
            "notes": toy.notes,
            "searchable_text": " ".join(part for part in searchable_parts if part),
        }

    @property
    def sort_key(self) -> str:
        """Deterministic sort key."""
        return self.metadata.name.casefold()

    @classmethod
    def from_flat_dict(cls, data: dict[str, Any]) -> KnowledgeBaseAsset:
        """Deserialize from canonical flat JSON."""
        unity_raw = data.get("unity_versions", "")
        unity_versions = (
            [part.strip() for part in str(unity_raw).split(",") if part.strip()]
            if unity_raw
            else []
        )
        metadata = AssetMetadata(
            store_id=str(data.get("store_id", "")),
            name=str(data.get("name", "")),
            publisher=str(data.get("publisher", "")),
            description=str(data.get("description", "")),
            purpose=str(data.get("purpose", "")),
            features=list(data.get("key_features", [])),
            unity_versions=unity_versions,
            render_pipelines=list(data.get("render_pipelines", [])),
            documentation_links=list(data.get("documentation_links", [])),
            website=str(data.get("website", "")),
            videos=list(data.get("videos", [])),
            asset_store_url=str(data.get("asset_store_url", "")),
            purchase_date=str(data.get("purchase_date", "")),
            last_update=str(data.get("last_updated", "")),
            version=str(data.get("version", "")),
            deprecated=bool(data.get("deprecated", False)),
            store_category=str(data.get("store_category", "")),
            store_category_path=str(data.get("store_category_path", "")),
        )
        tier_value = data.get("tier", 2)
        recommendation_value = str(data.get("recommendation", Recommendation.REFERENCE.value))
        status_value = str(data.get("status", AssetStatus.NOT_EVALUATED.value))
        toychest = ToyChestMetadata(
            stable_id=str(data.get("id", "")),
            category=str(data.get("category", "Utilities")),
            subcategory=str(data.get("subcategory", "General")),
            tier=Tier.TIER_1 if tier_value == 1 else Tier.TIER_3 if tier_value == 3 else Tier.TIER_2,
            recommendation=(
                Recommendation(recommendation_value)
                if recommendation_value in {item.value for item in Recommendation}
                else Recommendation.REFERENCE
            ),
            status=(
                AssetStatus(status_value)
                if status_value in {item.value for item in AssetStatus}
                else AssetStatus.NOT_EVALUATED
            ),
            evaluation=str(data.get("evaluation", "")),
            reviewed=str(data.get("reviewed", "")),
            reviewer=str(data.get("reviewer", "")),
            notes=str(data.get("notes", "")),
        )
        insights = AssetInsights(
            why_we_own_it=str(data.get("why_we_own_it", "")),
            potential_uses=str(data.get("potential_uses", "")),
            potential_concerns=str(data.get("potential_concerns", "")),
        )
        return cls(metadata=metadata, toychest=toychest, insights=insights)
