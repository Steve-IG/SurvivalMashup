"""ToyChest taxonomy, stable IDs, overrides, and insight generation."""

from __future__ import annotations

import logging
import re
from dataclasses import dataclass
from pathlib import Path

import yaml

from models import (
    AssetInsights,
    AssetMetadata,
    AssetStatus,
    KnowledgeBaseAsset,
    Recommendation,
    Tier,
    ToyChestMetadata,
)

logger = logging.getLogger(__name__)

TOP_LEVEL_CATEGORIES: tuple[str, ...] = (
    "Gameplay Systems",
    "Animation",
    "Networking",
    "Rendering",
    "Environment",
    "Terrain",
    "UI",
    "Audio",
    "Characters",
    "VFX",
    "Editor Tools",
    "Utilities",
    "AI",
    "World Generation",
)

ID_PREFIXES: dict[str, str] = {
    "Gameplay Systems": "GAME",
    "World Generation": "WORLD",
    "Animation": "ANIM",
    "UI": "UI",
    "Editor Tools": "EDIT",
    "Rendering": "RENDER",
    "Networking": "NET",
    "Audio": "AUDIO",
    "Environment": "ENV",
    "Terrain": "TERR",
    "Characters": "CHAR",
    "VFX": "VFX",
    "Utilities": "UTIL",
    "AI": "AI",
}

# Official Asset Store category labels mapped to ToyChest taxonomy.
STORE_CATEGORY_MAP: tuple[tuple[str, str, str], ...] = (
    ("fullscreen & camera effects", "Rendering", "Post Processing"),
    ("post processing", "Rendering", "Post Processing"),
    ("particles", "VFX", "General VFX"),
    ("fire & explosions", "VFX", "Combat VFX"),
    ("spells", "VFX", "Combat VFX"),
    ("shaders", "Rendering", "Shaders"),
    ("lighting", "Rendering", "Lighting"),
    ("icons", "UI", "Icons"),
    ("gui", "UI", "UI Kit"),
    ("user interfaces", "UI", "UI Kit"),
    ("animation", "Animation", "Character Animation"),
    ("audio", "Audio", "Audio Pack"),
    ("music", "Audio", "Audio Pack"),
    ("sound", "Audio", "Audio Pack"),
    ("terrain", "Terrain", "Terrain Tools"),
    ("landscape", "Terrain", "Terrain Tools"),
    ("environments", "Environment", "General"),
    ("historic", "Environment", "Historic"),
    ("fantasy", "Environment", "Fantasy"),
    ("nature", "Environment", "Nature"),
    ("sci-fi", "Environment", "Sci-Fi"),
    ("sci fi", "Environment", "Sci-Fi"),
    ("city", "Environment", "Urban"),
    ("urban", "Environment", "Urban"),
    ("tools/utilities", "Editor Tools", "Productivity"),
    ("utilities", "Editor Tools", "Productivity"),
    ("editor", "Editor Tools", "Productivity"),
    ("productivity", "Editor Tools", "Productivity"),
    ("project maintenance", "Editor Tools", "Project Maintenance"),
    ("asset management", "Editor Tools", "Project Maintenance"),
    ("modeling", "Editor Tools", "Modeling"),
    ("mesh", "Utilities", "Mesh Tools"),
    ("characters", "Characters", "Character Pack"),
    ("humanoids", "Characters", "Character Pack"),
    ("creatures", "Characters", "Enemy Pack"),
    ("artificial intelligence", "AI", "Behavior"),
    ("behavior", "AI", "Behavior"),
    ("networking", "Networking", "Multiplayer"),
    ("multiplayer", "Networking", "Multiplayer"),
    ("level design", "World Generation", "Level Design"),
    ("procedural", "World Generation", "Procedural Generation"),
    ("dungeon", "World Generation", "Procedural Generation"),
    ("inventory", "Gameplay Systems", "Inventory"),
    ("game toolkits", "Gameplay Systems", "General"),
    ("physics", "Gameplay Systems", "Physics"),
    ("input management", "Gameplay Systems", "Input"),
)

STORE_PATH_MAP: tuple[tuple[str, str, str], ...] = (
    ("/tools/utilities", "Editor Tools", "Productivity"),
    ("/tools/", "Editor Tools", "Productivity"),
    ("/environments/", "Environment", "General"),
    ("/particles/", "VFX", "General VFX"),
    ("/gui/", "UI", "UI Kit"),
    ("/audio/", "Audio", "Audio Pack"),
    ("/animation/", "Animation", "Character Animation"),
    ("/networking/", "Networking", "Multiplayer"),
    ("/level-design/", "World Generation", "Level Design"),
)

KEYWORD_RULES: tuple[tuple[str, str, tuple[str, ...]], ...] = (
    ("Gameplay Systems", "Inventory", ("vault inventory", "inventory system")),
    ("Gameplay Systems", "Character Controller", ("character controller", "third person", "topdown engine")),
    ("Gameplay Systems", "Combat", ("shooter gamekit", "tower defense", "moba")),
    ("World Generation", "Procedural Generation", ("dungeon architect", "procedural dungeon")),
    ("Networking", "Multiplayer", ("pun 2", "photon", "multiplayer")),
    ("Animation", "Lip Sync", ("salsa", "lip sync", "lipsync")),
    ("Animation", "Character Animation", ("motion matching", "human basic motions")),
    ("Editor Tools", "Project Maintenance", ("asset hunter", "unused asset")),
    ("Rendering", "Post Processing", ("beautify", "post processing", "post-processing")),
    ("UI", "UI Kit", ("ui kit", "game ui")),
    ("VFX", "Combat VFX", ("projectile", "explosion", "spell", "magic ability")),
    ("Environment", "Medieval", ("medieval", "castle", "village", "town")),
    ("Environment", "Nature", ("nature", "forest", "foliage")),
    ("Environment", "Sci-Fi", ("sci fi", "sci-fi", "futuristic")),
)

TIER_3_KEYWORDS: tuple[str, ...] = (
    "outfit",
    "props",
    "furniture",
    "texture pack",
    "material pack",
    "icon pack",
    "sound pack",
)


@dataclass
class TaxonomyOverride:
    """Manual override entry from taxonomy_overrides.yaml."""

    stable_id: str = ""
    category: str = ""
    subcategory: str = ""
    tier: int | None = None
    recommendation: str = ""
    status: str = ""
    evaluation: str = ""
    reviewed: str = ""
    reviewer: str = ""
    notes: str = ""


class StableIdRegistry:
    """Assign and preserve stable asset IDs across runs."""

    def __init__(self, existing_ids: dict[str, str] | None = None) -> None:
        self._by_store_id: dict[str, str] = dict(existing_ids or {})
        self._used_ids: set[str] = set(self._by_store_id.values())
        self._counters: dict[str, int] = {}
        for stable_id in self._used_ids:
            prefix, _, suffix = stable_id.partition("-")
            if suffix.isdigit():
                self._counters[prefix] = max(self._counters.get(prefix, 0), int(suffix))

    def get_or_assign(self, store_id: str, category: str, override_id: str = "") -> str:
        """Return a stable ID for an asset."""
        if override_id:
            self._by_store_id[store_id] = override_id
            self._used_ids.add(override_id)
            prefix = override_id.split("-", 1)[0]
            suffix = override_id.split("-", 1)[-1]
            if suffix.isdigit():
                self._counters[prefix] = max(self._counters.get(prefix, 0), int(suffix))
            return override_id

        existing = self._by_store_id.get(store_id, "")
        if existing and _is_stable_id(existing):
            return existing

        prefix = ID_PREFIXES.get(category, "UTIL")
        next_number = self._counters.get(prefix, 0) + 1
        while True:
            candidate = f"{prefix}-{next_number:03d}"
            next_number += 1
            if candidate not in self._used_ids:
                self._counters[prefix] = next_number - 1
                self._used_ids.add(candidate)
                self._by_store_id[store_id] = candidate
                return candidate


def load_overrides(path: Path) -> dict[str, TaxonomyOverride]:
    """Load taxonomy overrides keyed by lowercase asset name or store ID."""
    if not path.exists():
        logger.warning("Override file not found: %s", path)
        return {}

    raw = yaml.safe_load(path.read_text(encoding="utf-8")) or {}
    overrides: dict[str, TaxonomyOverride] = {}
    for key, value in raw.items():
        if not isinstance(value, dict):
            continue
        override = TaxonomyOverride(
            stable_id=str(value.get("id", "")),
            category=str(value.get("category", "")),
            subcategory=str(value.get("subcategory", "")),
            tier=value.get("tier"),
            recommendation=str(value.get("recommendation", "")),
            status=str(value.get("status", "")),
            evaluation=str(value.get("evaluation", "")),
            reviewed=str(value.get("reviewed", "")),
            reviewer=str(value.get("reviewer", "")),
            notes=str(value.get("notes", "")),
        )
        overrides[str(key).casefold()] = override
    return overrides


def find_override(metadata: AssetMetadata, overrides: dict[str, TaxonomyOverride]) -> TaxonomyOverride | None:
    """Find a manual override for an asset."""
    for key in (metadata.store_id, metadata.name):
        if key and key.casefold() in overrides:
            return overrides[key.casefold()]
    return None


def _is_stable_id(value: str) -> bool:
    """Return True when value matches the PREFIX-NNN stable ID format."""
    return bool(re.fullmatch(r"[A-Z]{2,6}-\d{3}", value))


def _should_include_render_pipelines(metadata: AssetMetadata, category: str) -> bool:
    """Only keep render pipeline data for rendering-related assets."""
    if category == "Rendering":
        return True
    haystack = f"{metadata.description} {' '.join(metadata.features)}".casefold()
    pipeline_markers = (
        "built-in pipeline",
        "builtin pipeline",
        "universal render",
        "render pipeline",
        "post processing",
        "for urp",
        "for hdrp",
        "for builtin",
    )
    return any(marker in haystack for marker in pipeline_markers)


def enrich_asset(
    metadata: AssetMetadata,
    overrides: dict[str, TaxonomyOverride],
    id_registry: StableIdRegistry,
) -> KnowledgeBaseAsset:
    """Apply taxonomy and generate ToyChest documentation fields."""
    override = find_override(metadata, overrides)
    category, subcategory = classify_asset(metadata, override)
    if not _should_include_render_pipelines(metadata, category):
        metadata.render_pipelines = []
    tier = assign_tier(metadata, category, subcategory, override)
    recommendation = assign_recommendation(tier, category, subcategory, override)
    stable_id = id_registry.get_or_assign(
        metadata.store_id,
        category,
        override.stable_id if override else "",
    )
    status = AssetStatus.DEPRECATED if metadata.deprecated else AssetStatus.NOT_EVALUATED
    if override and override.status:
        status = AssetStatus(override.status) if override.status in {s.value for s in AssetStatus} else status

    toychest = ToyChestMetadata(
        stable_id=stable_id,
        category=category,
        subcategory=subcategory,
        tier=tier,
        recommendation=recommendation,
        status=status,
        evaluation=override.evaluation if override else "",
        reviewed=override.reviewed if override else "",
        reviewer=override.reviewer if override else "",
        notes=override.notes if override else "",
    )
    insights = generate_insights(metadata, toychest)
    return KnowledgeBaseAsset(metadata=metadata, toychest=toychest, insights=insights)


def classify_asset(
    metadata: AssetMetadata,
    override: TaxonomyOverride | None,
) -> tuple[str, str]:
    """Assign category and subcategory using official data first."""
    if override and override.category and override.subcategory:
        return override.category, override.subcategory

    store_match = _match_store_category(metadata.store_category, metadata.store_category_path)
    if store_match:
        return store_match

    haystack = _search_text(metadata)
    keyword_match = _match_keyword_rules(haystack)
    if keyword_match:
        return keyword_match

    if metadata.store_category_path:
        return _infer_from_path(metadata.store_category_path)

    if any(token in haystack for token in ("environment", "modular town", "village", "dungeon pack")):
        return "Environment", _infer_environment_subcategory(haystack)
    if any(token in haystack for token in ("vfx", "particle", "projectile", "explosion")):
        return "VFX", "General VFX"
    if any(token in haystack for token in ("animation", "motus", "motion")):
        return "Animation", "Character Animation"
    if any(token in haystack for token in ("character", "outfit", "hero", "warrior")):
        return "Characters", "Character Pack"

    return "Utilities", "General"


def assign_tier(
    metadata: AssetMetadata,
    category: str,
    subcategory: str,
    override: TaxonomyOverride | None,
) -> Tier:
    """Assign tier using overrides and conservative heuristics."""
    if override and override.tier in {1, 2, 3}:
        return Tier.TIER_1 if override.tier == 1 else Tier.TIER_3 if override.tier == 3 else Tier.TIER_2

    haystack = _search_text(metadata)
    if category == "World Generation" and subcategory in {"Procedural Generation", "Dungeon", "Level Design"}:
        if any(token in haystack for token in ("dungeon architect", "procedural", "vault inventory")):
            return Tier.TIER_1
    if category == "Gameplay Systems" and subcategory in {"Inventory", "Character Controller", "Combat"}:
        if any(token in haystack for token in ("vault inventory", "tps 3", "topdown engine", "starter assets")):
            return Tier.TIER_1
    if category == "Networking" and subcategory == "Multiplayer" and "pun 2" in haystack:
        return Tier.TIER_1
    if category == "UI" and subcategory == "UI Kit" and "modular game ui kit" in haystack:
        return Tier.TIER_1
    if category == "Animation" and subcategory == "Character Animation" and "motion matching" in haystack:
        return Tier.TIER_1

    if any(keyword in haystack for keyword in TIER_3_KEYWORDS):
        return Tier.TIER_3
    if category in {"Environment", "VFX", "Characters", "Animation", "Audio"}:
        return Tier.TIER_2
    return Tier.TIER_2


def assign_recommendation(
    tier: Tier,
    category: str,
    subcategory: str,
    override: TaxonomyOverride | None,
) -> Recommendation:
    """Map tier and category to a recommendation enum value."""
    if override and override.recommendation:
        value = override.recommendation
        if value in {item.value for item in Recommendation}:
            return Recommendation(value)

    if tier == Tier.TIER_1 and category == "Gameplay Systems":
        return Recommendation.CORE_CANDIDATE
    if tier == Tier.TIER_1:
        return Recommendation.PRODUCTION_CANDIDATE
    if category in {"World Generation", "Networking", "UI"} and tier == Tier.TIER_2:
        return Recommendation.PROTOTYPE_CANDIDATE
    if category == "Editor Tools":
        return Recommendation.DEVELOPMENT_STANDARD
    if category in {"Environment", "VFX", "Characters", "Animation", "Audio", "Rendering"}:
        return Recommendation.REFERENCE
    return Recommendation.NICE_TO_HAVE


def generate_insights(metadata: AssetMetadata, toychest: ToyChestMetadata) -> AssetInsights:
    """Generate ToyChest-specific narrative fields without inventing facts."""
    why = (
        f"Owned in the ToyChest Unity Asset Store library for {toychest.category} / "
        f"{toychest.subcategory}."
    )
    uses_parts: list[str] = []
    if toychest.tier == Tier.TIER_1:
        uses_parts.append(
            f"Evaluate {metadata.name} before implementing custom {toychest.subcategory.lower()} functionality."
        )
    elif toychest.recommendation == Recommendation.CORE_CANDIDATE:
        uses_parts.append(f"Preferred owned option for {toychest.subcategory.lower()}.")
    else:
        uses_parts.append(f"Reference owned option for {toychest.subcategory.lower()}.")

    concerns: list[str] = [
        "Requires sandbox import and architecture review before production use.",
        "Do not modify Asset Store package source directly; wrap or adapt instead.",
    ]
    if toychest.status == AssetStatus.NOT_EVALUATED:
        concerns.append("Not yet evaluated in the ToyChest project.")
    if metadata.deprecated:
        concerns.append("Marked deprecated in the Asset Store export.")

    return AssetInsights(
        why_we_own_it=why,
        potential_uses=" ".join(uses_parts),
        potential_concerns=" ".join(concerns),
    )


def _match_store_category(store_category: str, store_category_path: str) -> tuple[str, str] | None:
    """Map official Asset Store categories to ToyChest taxonomy."""
    hints = " ".join(part for part in (store_category, store_category_path) if part).casefold()
    if not hints.strip():
        return None

    best: tuple[str, str] | None = None
    best_len = 0
    for needle, category, subcategory in STORE_CATEGORY_MAP:
        if needle in hints and len(needle) > best_len:
            best = (category, subcategory)
            best_len = len(needle)
    return best


def _infer_from_path(path: str) -> tuple[str, str]:
    """Infer taxonomy from the Asset Store URL path."""
    lowered = path.casefold()
    for needle, category, subcategory in STORE_PATH_MAP:
        if needle in lowered:
            return category, subcategory
    return "Utilities", "General"


def _match_keyword_rules(haystack: str) -> tuple[str, str] | None:
    """Keyword fallback when official category mapping is unavailable."""
    best_score = 0
    best_match: tuple[str, str] | None = None
    for category, subcategory, keywords in KEYWORD_RULES:
        score = sum(len(keyword.split()) + 1 for keyword in keywords if keyword in haystack)
        if score > best_score:
            best_score = score
            best_match = (category, subcategory)
    return best_match


def _infer_environment_subcategory(haystack: str) -> str:
    """Infer environment subcategory from keywords."""
    if any(token in haystack for token in ("medieval", "castle", "gothic", "village", "town")):
        return "Medieval"
    if any(token in haystack for token in ("sci fi", "sci-fi", "futuristic", "orbital", "industrial")):
        return "Sci-Fi"
    if any(token in haystack for token in ("nature", "forest", "grove", "foliage")):
        return "Nature"
    if any(token in haystack for token in ("horror", "graveyard", "haunted")):
        return "Horror"
    return "General"


def _search_text(metadata: AssetMetadata) -> str:
    """Build normalized searchable text for an asset."""
    parts = [
        metadata.name,
        metadata.publisher,
        metadata.description,
        metadata.store_category,
        metadata.store_category_path,
        " ".join(metadata.features),
    ]
    return " ".join(part for part in parts if part).casefold()


def group_overlaps(assets: list[KnowledgeBaseAsset]) -> dict[str, list[KnowledgeBaseAsset]]:
    """Group assets that solve similar problems."""
    groups: dict[str, list[KnowledgeBaseAsset]] = {}
    for asset in assets:
        key = f"{asset.toychest.category} / {asset.toychest.subcategory}"
        groups.setdefault(key, []).append(asset)
    return {
        key: sorted(items, key=lambda item: item.sort_key)
        for key, items in groups.items()
        if len(items) > 1
    }


def strategic_assets(assets: list[KnowledgeBaseAsset]) -> list[KnowledgeBaseAsset]:
    """Return strategic assets used for decision rules and alternatives."""
    selected: list[KnowledgeBaseAsset] = []
    for asset in assets:
        if asset.toychest.tier == Tier.TIER_1:
            selected.append(asset)
            continue
        if asset.toychest.recommendation in {
            Recommendation.CORE_CANDIDATE,
            Recommendation.PRODUCTION_CANDIDATE,
            Recommendation.PROTOTYPE_CANDIDATE,
        }:
            selected.append(asset)
    return sorted(selected, key=lambda item: item.sort_key)
