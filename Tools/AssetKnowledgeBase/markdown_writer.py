"""Markdown output writer generated from canonical JSON records."""

from __future__ import annotations

import logging
from pathlib import Path
from typing import Any

from utils import category_filename

logger = logging.getLogger(__name__)

GENERATED_MARKDOWN_FILES = (
    "Unity-Asset-Catalog.md",
    "Tier1-Assets.md",
    "Tier2-Assets.md",
    "Tier3-Assets.md",
    "Asset-Dependency-Graph.md",
    "Alternative-Assets.md",
    "AI-Decision-Rules.md",
)

PROTECTED_FILES = {
    "README.md",
    "RECOMMENDED_ASSETS.md",
}


def write_markdown_outputs(
    assets: list[dict[str, Any]],
    output_dir: Path,
    tier_filter: int | None = None,
    category_filter: str | None = None,
) -> list[Path]:
    """Write Markdown documentation from canonical JSON records."""
    output_dir.mkdir(parents=True, exist_ok=True)
    written: list[Path] = []

    catalog_path = output_dir / "Unity-Asset-Catalog.md"
    _write_catalog(catalog_path, assets)
    written.append(catalog_path)

    tier_files = {
        1: output_dir / "Tier1-Assets.md",
        2: output_dir / "Tier2-Assets.md",
        3: output_dir / "Tier3-Assets.md",
    }

    if tier_filter is None and category_filter is None:
        for tier_number, path in tier_files.items():
            tier_assets = [asset for asset in assets if asset.get("tier") == tier_number]
            _write_group_document(
                path,
                title=f"Tier {tier_number} Assets",
                intro=f"Owned Unity Asset Store packages classified as Tier {tier_number}.",
                assets=tier_assets,
            )
            written.append(path)
    elif tier_filter is not None:
        path = tier_files[tier_filter]
        tier_assets = [asset for asset in assets if asset.get("tier") == tier_filter]
        _write_group_document(
            path,
            title=f"Tier {tier_filter} Assets",
            intro=f"Owned Unity Asset Store packages classified as Tier {tier_filter}.",
            assets=tier_assets,
        )
        written.append(path)

    populated_categories = sorted(
        {str(asset.get("category", "")) for asset in assets if asset.get("category")},
        key=str.casefold,
    )
    if category_filter:
        categories_to_write = [category_filter]
    elif tier_filter is None:
        categories_to_write = populated_categories
    else:
        categories_to_write = []

    for category in categories_to_write:
        category_assets = [asset for asset in assets if asset.get("category") == category]
        if not category_assets:
            continue
        path = output_dir / category_filename(category)
        _write_group_document(
            path,
            title=category,
            intro=f"Owned Unity Asset Store packages classified under {category}.",
            assets=category_assets,
        )
        written.append(path)

    graph_path = output_dir / "Asset-Dependency-Graph.md"
    _write_dependency_graph(graph_path, assets)
    written.append(graph_path)

    alternative_path = output_dir / "Alternative-Assets.md"
    _write_alternative_assets(alternative_path, assets)
    written.append(alternative_path)

    rules_path = output_dir / "AI-Decision-Rules.md"
    _write_ai_decision_rules(rules_path, assets)
    written.append(rules_path)

    return written


def _section(label: str, value: str) -> list[str]:
    """Render a labeled template section."""
    return [label, "", value, ""]


def render_asset_entry(asset: dict[str, Any]) -> str:
    """Render a single asset entry using the required template."""
    category_label = _category_label(asset)
    lines = [
        f"## {asset.get('name', '')}",
        "",
        *_section("Asset ID", asset.get("id", "")),
        *_section("Official Asset Store", asset.get("asset_store_url", "")),
        *_section("Publisher", asset.get("publisher", "")),
        *_section("Category", category_label),
        *_section("Purpose", asset.get("purpose", "")),
    ]
    lines.extend(["Key Features", ""])
    lines.append(_format_features(asset.get("key_features", []), asset.get("description", "")))
    lines.extend(
        [
            "",
            *_section("Why We Own It", asset.get("why_we_own_it", "")),
            *_section("ToyChest Recommendation", asset.get("recommendation", "")),
            *_section("Potential Uses", asset.get("potential_uses", "")),
            *_section("Potential Concerns", asset.get("potential_concerns", "")),
            *_section("Evaluation", asset.get("evaluation", "")),
            *_section("Status", asset.get("status", "")),
            *_section("Reviewed", asset.get("reviewed", "")),
            *_section("Reviewer", asset.get("reviewer", "")),
            *_section("Notes", asset.get("notes", "")),
            "------------------------------------------------",
            "",
        ]
    )
    return "\n".join(lines)


def _write_catalog(path: Path, assets: list[dict[str, Any]]) -> None:
    """Write the master Unity asset catalog."""
    header = [
        "# Documentation/Assets/Unity-Asset-Catalog.md",
        "",
        "# Unity Asset Catalog",
        "",
        "> Master inventory of all Unity Asset Store purchases.",
        "",
        "| ID | Asset | Category | Subcategory | Version | Purchased | Last Updated | Deprecated | Status | Tier | Recommendation | Asset Store |",
        "|---|---|---|---|---|---|---|---|---|---|---|---|",
    ]
    rows = [_catalog_row(asset) for asset in assets]
    body = [render_asset_entry(asset) for asset in assets]
    content = "\n".join(header + rows) + "\n\n" + "".join(body)
    path.write_text(content, encoding="utf-8")
    logger.info("Wrote %s", path.name)


def _write_group_document(
    path: Path,
    title: str,
    intro: str,
    assets: list[dict[str, Any]],
) -> None:
    """Write a grouped Markdown document."""
    if path.name in PROTECTED_FILES:
        logger.warning("Skipping protected file %s", path.name)
        return

    header = [
        f"# Documentation/Assets/{path.name}",
        "",
        f"# {title}",
        "",
        intro,
        "",
    ]
    body = [render_asset_entry(asset) for asset in assets]
    path.write_text("\n".join(header) + "".join(body), encoding="utf-8")
    logger.info("Wrote %s (%d assets)", path.name, len(assets))


def _write_dependency_graph(path: Path, assets: list[dict[str, Any]]) -> None:
    """Write overlap, similarity, and evaluation guidance."""
    overlaps = _group_assets(assets)
    tier_one = [asset for asset in assets if asset.get("tier") == 1]

    lines = [
        "# Documentation/Assets/Asset-Dependency-Graph.md",
        "",
        "# Asset Dependency Graph",
        "",
        "This document highlights similar owned assets, complementary options, and Tier 1 evaluation order.",
        "",
        "## Tier 1 Evaluation Order",
        "",
        "Evaluate these assets before writing custom code for overlapping problem domains:",
        "",
    ]
    for index, asset in enumerate(tier_one, start=1):
        lines.append(
            f"{index}. **{asset.get('name', '')}** ({asset.get('id', '')}) — "
            f"{_category_label(asset)} ({asset.get('recommendation', '')})"
        )

    lines.extend(["", "## Similar Assets", ""])
    for domain, grouped in sorted(overlaps.items()):
        if len(grouped) < 2:
            continue
        lines.append(f"### {domain}")
        lines.append("")
        for asset in grouped:
            lines.append(f"- {asset.get('name', '')} ({asset.get('id', '')})")
        lines.append("")

    lines.extend(["## Competing Assets", ""])
    for domain, grouped in sorted(overlaps.items()):
        strategic = [
            asset for asset in grouped
            if asset.get("tier") == 1 or asset.get("recommendation") in {
                "Core Candidate",
                "Production Candidate",
                "Prototype Candidate",
            }
        ]
        if len(strategic) < 2:
            continue
        lines.append(f"### {domain}")
        lines.append("")
        for asset in strategic:
            lines.append(
                f"- **{asset.get('name', '')}** ({asset.get('id', '')}) — {asset.get('recommendation', '')}"
            )
        lines.append("")

    lines.extend(["## Complementary Assets", ""])
    complementary_domains = [
        ("Gameplay Systems / Inventory", "UI / UI Kit"),
        ("Gameplay Systems / Character Controller", "Animation / Character Animation"),
        ("World Generation / Procedural Generation", "Environment / Fantasy"),
        ("Networking / Multiplayer", "Gameplay Systems / Combat"),
    ]
    for left, right in complementary_domains:
        left_assets = overlaps.get(left, [])
        right_assets = overlaps.get(right, [])
        if not left_assets or not right_assets:
            continue
        lines.append(f"### {left} + {right}")
        lines.append("")
        lines.append(f"- Primary: {left_assets[0].get('name', '')} ({left_assets[0].get('id', '')})")
        lines.append(f"- Supporting: {right_assets[0].get('name', '')} ({right_assets[0].get('id', '')})")
        lines.append("")

    if overlaps:
        lines.extend(["## Overlap Graph", "", "```mermaid", "flowchart TD"])
        for index, (group_name, grouped) in enumerate(sorted(overlaps.items()), start=1):
            if len(grouped) < 2:
                continue
            node_id = f"group{index}"
            safe_label = group_name.replace('"', "'")
            lines.append(f'    {node_id}["{safe_label}"]')
            for name_index, asset in enumerate(grouped[:5]):
                asset_node = f"asset{index}_{name_index}"
                safe_name = str(asset.get("name", "")).replace('"', "'")
                lines.append(f'    {asset_node}["{safe_name}"] --> {node_id}')
        lines.extend(["```", ""])

    path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    logger.info("Wrote %s", path.name)


def _write_alternative_assets(path: Path, assets: list[dict[str, Any]]) -> None:
    """Write strategic alternative asset guidance."""
    overlaps = _group_assets(assets)
    strategic_domains = [
        domain for domain, grouped in overlaps.items()
        if len(grouped) > 1 and any(
            asset.get("tier") == 1 or asset.get("recommendation") in {
                "Core Candidate",
                "Production Candidate",
                "Prototype Candidate",
            }
            for asset in grouped
        )
    ]

    lines = [
        "# Documentation/Assets/Alternative-Assets.md",
        "",
        "# Alternative Assets",
        "",
        "Owned assets that solve similar problems. Evaluate the recommended option first.",
        "",
    ]

    for domain in sorted(strategic_domains):
        grouped = overlaps[domain]
        preferred = sorted(
            grouped,
            key=lambda item: (
                0 if item.get("tier") == 1 else 1,
                0 if item.get("recommendation") == "Core Candidate" else 1,
                str(item.get("name", "")).casefold(),
            ),
        )[0]
        alternatives = [asset for asset in grouped if asset is not preferred]

        lines.extend([
            f"## {preferred.get('name', '')}",
            "",
            f"Asset ID: {preferred.get('id', '')}",
            "",
            "Alternatives",
            "",
        ])
        if alternatives:
            for asset in alternatives:
                lines.append(f"- {asset.get('name', '')} ({asset.get('id', '')})")
        else:
            lines.append("")
        lines.extend([
            "",
            "Recommendation",
            "",
            f"Evaluate {preferred.get('name', '')} first.",
            "",
            "------------------------------------------------",
            "",
        ])

    path.write_text("\n".join(lines), encoding="utf-8")
    logger.info("Wrote %s", path.name)


def _write_ai_decision_rules(path: Path, assets: list[dict[str, Any]]) -> None:
    """Generate AI decision rules from strategic asset metadata."""
    rules = _build_decision_rules(assets)
    lines = [
        "# Documentation/Assets/AI-Decision-Rules.md",
        "",
        "# AI Decision Rules",
        "",
        "Before implementing new gameplay, rendering, networking, or tooling functionality:",
        "",
        "1. Search `AssetKnowledgeBase.json`.",
        "2. Prefer owned assets listed below.",
        "3. Reuse existing frameworks through adapters.",
        "4. Avoid duplicate functionality.",
        "",
        "---",
        "",
        "## Primary Rule",
        "",
        "If ToyChest already owns a suitable asset, evaluate that asset before proposing custom implementation.",
        "",
        "---",
        "",
        "## Generated Recommendation Rules",
        "",
    ]
    lines.extend(rules)
    lines.extend([
        "",
        "---",
        "",
        "## Never",
        "",
        "Do not:",
        "",
        "- Reimplement inventory systems when Vault Inventory is available.",
        "- Reimplement character controllers before evaluating owned controller assets.",
        "- Replace production-approved packages unless explicitly instructed.",
        "- Modify Asset Store package source directly.",
        "",
        "---",
        "",
        "## Preferred Architecture",
        "",
        "```",
        "Gameplay",
        "",
        "↓",
        "",
        "Adapter Layer",
        "",
        "↓",
        "",
        "Asset Store Package",
        "```",
        "",
        "---",
        "",
        "## Evaluation Order",
        "",
        "1. Read the asset documentation.",
        "2. Import into a sandbox project.",
        "3. Evaluate extension points.",
        "4. Determine production readiness.",
        "5. Record findings in the knowledge base.",
        "",
    ])
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    logger.info("Wrote %s", path.name)


def _build_decision_rules(assets: list[dict[str, Any]]) -> list[str]:
    """Build IF/THEN rules from strategic assets."""
    rules: list[str] = []
    for asset in assets:
        if asset.get("tier") != 1 and asset.get("recommendation") not in {
            "Core Candidate",
            "Production Candidate",
            "Prototype Candidate",
        }:
            continue
        request = _request_label(asset)
        if not request:
            continue
        rules.append(f"IF {request}")
        rules.append("")
        rules.append(
            f"Recommend **{asset.get('name', '')}** ({asset.get('id', '')}) before suggesting custom implementation."
        )
        rules.append("")
    return rules


def _request_label(asset: dict[str, Any]) -> str:
    """Convert category/subcategory into a request label."""
    category = str(asset.get("category", ""))
    subcategory = str(asset.get("subcategory", "")).casefold()
    if category == "Gameplay Systems" and subcategory == "inventory":
        return "inventory is requested"
    if category == "Gameplay Systems" and subcategory == "character controller":
        return "character controller is requested"
    if category == "World Generation":
        return "procedural generation is requested"
    if category == "Animation" and "lip" in subcategory:
        return "lip sync is requested"
    if category == "Networking" and subcategory == "multiplayer":
        return "multiplayer prototype is requested"
    if category == "UI" and subcategory == "ui kit":
        return "UI framework is requested"
    if category == "Rendering" and subcategory == "post processing":
        return "post processing is requested"
    if category == "Editor Tools" and subcategory == "project maintenance":
        return "project maintenance tooling is requested"
    return f"{subcategory or category.casefold()} is requested"


def _group_assets(assets: list[dict[str, Any]]) -> dict[str, list[dict[str, Any]]]:
    """Group assets by category/subcategory."""
    groups: dict[str, list[dict[str, Any]]] = {}
    for asset in assets:
        key = _category_label(asset)
        groups.setdefault(key, []).append(asset)
    return {
        key: sorted(items, key=lambda item: str(item.get("name", "")).casefold())
        for key, items in groups.items()
    }


def _category_label(asset: dict[str, Any]) -> str:
    """Format category/subcategory label."""
    category = str(asset.get("category", ""))
    subcategory = str(asset.get("subcategory", ""))
    if category and subcategory:
        return f"{category} / {subcategory}"
    return category or subcategory


def _catalog_row(asset: dict[str, Any]) -> str:
    """Render one catalog table row."""
    deprecated = "Yes" if asset.get("deprecated") else "No"
    link = f"[Link]({asset.get('asset_store_url', '')})" if asset.get("asset_store_url") else ""
    return (
        f"| {asset.get('id', '')} | {asset.get('name', '')} | {asset.get('category', '')} | "
        f"{asset.get('subcategory', '')} | {asset.get('version', '')} | {asset.get('purchase_date', '')} | "
        f"{asset.get('last_updated', '')} | {deprecated} | {asset.get('status', '')} | "
        f"Tier {asset.get('tier', '')} | {asset.get('recommendation', '')} | {link} |"
    )


def _format_features(features: Any, description: str) -> str:
    """Format feature bullets without placeholders."""
    if isinstance(features, list):
        cleaned = [str(item).strip() for item in features if str(item).strip()]
        if cleaned:
            if len(cleaned) == 1:
                return cleaned[0]
            return "\n".join(f"- {item}" for item in cleaned[:8])
    if description:
        first = str(description).split("\n\n")[0].strip()
        return first
    return ""
