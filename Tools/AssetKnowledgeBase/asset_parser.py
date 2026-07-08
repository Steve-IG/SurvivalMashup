"""CSV parsing and Asset Store HTML metadata extraction."""

from __future__ import annotations

import csv
import json
import logging
import re
from html import unescape
from pathlib import Path
from typing import Any
from urllib.parse import urljoin, urlparse

from bs4 import BeautifulSoup, Tag

from models import AssetMetadata
from utils import COLUMN_ALIASES, extract_asset_id, normalize_header, parse_bool

logger = logging.getLogger(__name__)

FEATURE_HEADINGS = (
    "what's inside",
    "whats inside",
    "key features",
    "features",
    "highlights",
    "package content",
    "technical details",
)

VIDEO_DOMAINS = ("youtube.com", "youtu.be", "vimeo.com")

SRP_TYPE_MAP = {
    "lightweight": "URP",
    "highdefinition": "HDRP",
    "standard": "Built-in",
}

BREADCRUMB_NAMES = {
    "home",
    "3d",
    "2d",
    "tools",
    "audio",
    "vfx",
    "templates",
    "essentials",
    "add-ons",
    "environments",
    "applications",
}


class ColumnMappingError(ValueError):
    """Raised when required CSV columns cannot be detected."""


def detect_column_map(headers: list[str]) -> dict[str, str]:
    """Map logical field names to actual CSV column headers."""
    normalized = {normalize_header(header): header for header in headers}
    mapping: dict[str, str] = {}

    for field_name, aliases in COLUMN_ALIASES.items():
        for alias in aliases:
            if alias in normalized:
                mapping[field_name] = normalized[alias]
                break

    missing = [field for field in ("name", "url") if field not in mapping]
    if missing:
        raise ColumnMappingError(
            f"Could not detect required CSV columns: {', '.join(missing)}. "
            f"Found headers: {headers}"
        )
    return mapping


def load_csv_rows(csv_path: Path) -> list[dict[str, str]]:
    """Load and normalize rows from a Unity Asset Store CSV export."""
    with csv_path.open(encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        if not reader.fieldnames:
            raise ColumnMappingError("CSV file has no headers.")

        column_map = detect_column_map(list(reader.fieldnames))
        rows: list[dict[str, str]] = []

        for row in reader:
            normalized_row = {
                "name": row.get(column_map["name"], "").strip(),
                "url": row.get(column_map["url"], "").strip(),
                "version": row.get(column_map.get("version", ""), "").strip()
                if "version" in column_map
                else "",
                "purchase_date": row.get(column_map.get("purchase_date", ""), "").strip()
                if "purchase_date" in column_map
                else "",
                "last_update": row.get(column_map.get("last_update", ""), "").strip()
                if "last_update" in column_map
                else "",
                "deprecated": row.get(column_map.get("deprecated", ""), "").strip()
                if "deprecated" in column_map
                else "",
            }
            if normalized_row["name"] and normalized_row["url"]:
                rows.append(normalized_row)

    logger.info("Loaded %d assets from %s", len(rows), csv_path)
    return rows


def metadata_from_csv_row(row: dict[str, str]) -> AssetMetadata:
    """Create baseline metadata from a CSV row."""
    url = row["url"]
    return AssetMetadata(
        store_id=extract_asset_id(url),
        name=row["name"],
        asset_store_url=url,
        purchase_date=row.get("purchase_date", ""),
        last_update=row.get("last_update", ""),
        version=row.get("version", ""),
        deprecated=parse_bool(row.get("deprecated", "")),
    )


def parse_asset_store_html(html: str, csv_row: dict[str, str]) -> AssetMetadata:
    """Parse Asset Store HTML into structured metadata."""
    metadata = metadata_from_csv_row(csv_row)
    if not html.strip():
        return metadata

    asset_id = metadata.id or extract_asset_id(metadata.asset_store_url)
    embedded = _extract_embedded_product(html, asset_id)
    if embedded:
        _apply_embedded_product(metadata, embedded, html)
    else:
        soup = BeautifulSoup(html, "lxml")
        metadata.name = _extract_name(soup, metadata.name)
        metadata.publisher = _extract_publisher(soup)
        metadata.description = _extract_description(soup)
        metadata.features = _extract_features(soup)
        metadata.unity_versions, metadata.render_pipelines = _extract_pipeline_table(soup)
        metadata.latest_version = _extract_labeled_value(
            soup, ("latest version", "current version")
        )
        metadata.release_date = _extract_labeled_value(
            soup, ("latest release date", "release date")
        )
        original_unity = _extract_labeled_value(soup, ("original unity version",))
        if original_unity and original_unity not in metadata.unity_versions:
            metadata.unity_versions.append(original_unity)
        metadata.store_category = _extract_store_category(soup, metadata.name)
        metadata.documentation_links = _extract_documentation_links(soup)
        metadata.website = _extract_website(soup, metadata.publisher)
        metadata.videos = _extract_video_links(soup)

    if not metadata.documentation_links or not metadata.videos or not metadata.website:
        soup = BeautifulSoup(html, "lxml")
        if not metadata.documentation_links:
            metadata.documentation_links = _extract_documentation_links(soup)
        if not metadata.videos:
            metadata.videos = _extract_video_links(soup)
        if not metadata.website and metadata.publisher:
            metadata.website = _extract_website(soup, metadata.publisher)

    if metadata.latest_version:
        metadata.version = metadata.latest_version
    if metadata.release_date and not metadata.last_update:
        metadata.last_update = metadata.release_date

    metadata.purpose = _derive_purpose(metadata.description)
    metadata.features = _finalize_features(metadata.description, metadata.features)
    metadata.videos = _clean_videos(metadata.videos)
    metadata.documentation_links = _clean_links(metadata.documentation_links)
    _promote_video_links(metadata)
    if metadata.website and "unity.com" in metadata.website.casefold():
        metadata.website = ""

    return metadata


def _extract_embedded_product(html: str, asset_id: str) -> dict[str, Any] | None:
    """Extract the embedded product JSON object from Asset Store page HTML."""
    if not asset_id:
        return None

    marker = f'"{asset_id}":{{'
    marker_index = html.find(marker)
    if marker_index < 0:
        return None

    brace_start = html.find("{", marker_index + len(asset_id) + 3)
    if brace_start < 0:
        return None

    depth = 0
    in_string = False
    escape = False
    for index in range(brace_start, len(html)):
        char = html[index]
        if in_string:
            if escape:
                escape = False
            elif char == "\\":
                escape = True
            elif char == '"':
                in_string = False
            continue
        if char == '"':
            in_string = True
        elif char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                try:
                    return json.loads(html[brace_start : index + 1])
                except json.JSONDecodeError:
                    logger.debug("Failed to decode embedded product JSON for asset %s", asset_id)
                    return None
    return None


def _extract_named_section(html: str, section_name: str) -> dict[str, Any]:
    """Extract a named JSON object section such as ProductPublisher."""
    marker = f'"{section_name}":{{'
    marker_index = html.find(marker)
    if marker_index < 0:
        return {}

    brace_start = html.find("{", marker_index + len(section_name) + 3)
    if brace_start < 0:
        return {}

    depth = 0
    in_string = False
    escape = False
    for index in range(brace_start, len(html)):
        char = html[index]
        if in_string:
            if escape:
                escape = False
            elif char == "\\":
                escape = True
            elif char == '"':
                in_string = False
            continue
        if char == '"':
            in_string = True
        elif char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                try:
                    parsed = json.loads(html[brace_start : index + 1])
                    return parsed if isinstance(parsed, dict) else {}
                except json.JSONDecodeError:
                    return {}
    return {}


def _apply_embedded_product(
    metadata: AssetMetadata,
    product: dict[str, Any],
    html: str,
) -> None:
    """Apply fields from embedded product JSON to metadata."""
    metadata.name = str(product.get("name", metadata.name) or metadata.name)
    metadata.description = _html_to_text(str(product.get("description", ""))) or metadata.description
    metadata.features = _extract_features_from_html_fields(
        str(product.get("description", "")),
        str(product.get("keyFeatures", "")),
    )
    metadata.store_category = _category_from_product(product, html)
    metadata.store_category_path = _extract_store_category_path(html)
    metadata.unity_versions = list(product.get("supportedUnityVersions", []) or [])
    metadata.render_pipelines = _render_pipelines_from_srps(product.get("srps", []))
    metadata.latest_version = _resolve_version_name(product, html)
    metadata.release_date = _format_published_date(product.get("firstPublishedDate", ""))

    publisher = _resolve_publisher(product.get("publisher"), html)
    metadata.publisher = publisher.get("name", metadata.publisher)
    metadata.website = publisher.get("url", metadata.website)

    docs: list[str] = []
    docs.extend(_extract_links_from_html(str(product.get("publishNotes", ""))))
    docs.extend(_extract_links_from_html(str(product.get("description", ""))))
    docs.extend(_extract_links_from_html(str(product.get("keyFeatures", ""))))
    if docs:
        metadata.documentation_links = _clean_links(docs)

    support_url = publisher.get("supportUrl") or publisher.get("url", "")
    if support_url and support_url.startswith("http"):
        metadata.website = metadata.website or support_url


def _resolve_publisher(publisher_field: Any, html: str) -> dict[str, str]:
    """Resolve publisher reference to name and URL."""
    if isinstance(publisher_field, dict) and publisher_field.get("name"):
        return {
            "name": str(publisher_field["name"]),
            "url": str(publisher_field.get("url", "")),
        }

    publisher_id = ""
    if isinstance(publisher_field, dict):
        ref = publisher_field.get("id")
        if isinstance(ref, list) and len(ref) >= 2:
            publisher_id = str(ref[1])

    if publisher_id:
        publishers = _extract_named_section(html, "ProductPublisher")
        record = publishers.get(publisher_id, {})
        if isinstance(record, dict):
            return {
                "name": str(record.get("name", "")),
                "url": str(record.get("url", "")),
                "supportUrl": str(record.get("supportUrl", "")),
            }

    return {"name": "", "url": "", "supportUrl": ""}


def _resolve_version_name(product: dict[str, Any], html: str) -> str:
    """Resolve current version reference to a version string."""
    current_version = product.get("currentVersion")
    if isinstance(current_version, dict) and current_version.get("name"):
        return str(current_version["name"])

    version_id = ""
    if isinstance(current_version, dict):
        ref = current_version.get("id")
        if isinstance(ref, list) and len(ref) >= 2:
            version_id = str(ref[1])

    if version_id:
        versions = _extract_named_section(html, "ProductVersion")
        record = versions.get(version_id, {})
        if isinstance(record, dict) and record.get("name"):
            return str(record["name"])

    return ""


def _render_pipelines_from_srps(srps: Any) -> list[str]:
    """Convert SRP compatibility entries to render pipeline names."""
    pipelines: list[str] = []
    if not isinstance(srps, list):
        return pipelines

    for entry in srps:
        if not isinstance(entry, dict):
            continue
        types = entry.get("types", [])
        if not isinstance(types, list):
            continue
        for pipeline_type in types:
            mapped = SRP_TYPE_MAP.get(str(pipeline_type).casefold())
            if mapped and mapped not in pipelines:
                pipelines.append(mapped)
    return pipelines


def _category_from_product(product: dict[str, Any], html: str) -> str:
    """Resolve store category label from embedded product data."""
    category_field = product.get("category")
    category_id = ""
    if isinstance(category_field, dict):
        ref = category_field.get("id")
        if isinstance(ref, list) and len(ref) >= 2:
            category_id = str(ref[1])

    if category_id:
        categories = _extract_named_section(html, "Category")
        record = categories.get(category_id, {})
        if isinstance(record, dict) and record.get("name"):
            return str(record["name"])

    slug = str(product.get("slug", ""))
    if slug:
        parts = slug.split("-")
        if len(parts) > 2:
            return parts[1].replace("_", " ").title()
    return metadata_store_category_from_title(html)


def _extract_store_category_path(html: str) -> str:
    """Extract the official Asset Store category path from embedded page state."""
    match = re.search(r'"currentUrl"\s*:\s*"(/packages/[^"]+)"', html)
    if not match:
        return ""
    path = match.group(1)
    parts = [part for part in path.split("/") if part and part != "packages"]
    if not parts:
        return ""
    return " / ".join(part.replace("-", " ").title() for part in parts[1:])


PURPOSE_INDICATORS = (
    " is a ",
    " is an ",
    " lets you ",
    " allows you ",
    " allows ",
    " helps ",
    " provides ",
    " enables ",
    " designed to ",
    " tool for ",
    " generator",
    " framework",
    " solution for ",
    " solution that ",
)

PROMOTIONAL_MARKERS = (
    "new feature in version",
    "*** new",
    "now available",
    "get the new version",
    "winner of the unity awards",
    "winner of",
    "latest changelog",
    "follow us on",
    "check the documentation",
)


def _derive_purpose(description: str) -> str:
    """Extract a concise purpose statement from the official description."""
    if not description or _is_generic_store_description(description):
        return ""

    paragraphs = [part.strip() for part in description.split("\n\n") if part.strip()]
    for paragraph in paragraphs:
        if _is_promotional_paragraph(paragraph) or _is_generic_store_description(paragraph):
            continue
        lowered = paragraph.casefold()
        if any(indicator in lowered for indicator in PURPOSE_INDICATORS) and len(paragraph) >= 30:
            return paragraph[:500]

    for paragraph in paragraphs:
        if _is_promotional_paragraph(paragraph) or _is_generic_store_description(paragraph):
            continue
        if len(paragraph) >= 60:
            return paragraph[:500]
    return ""


def _is_promotional_paragraph(text: str) -> bool:
    """Detect announcement or marketing paragraphs unsuitable as purpose."""
    lowered = text.casefold().strip()
    if len(lowered) < 20:
        return True
    if any(marker in lowered for marker in PROMOTIONAL_MARKERS):
        return True
    if lowered.endswith("?") and len(lowered) < 80:
        return True
    if text.count("🔥") >= 1:
        return True
    return False


def _is_generic_store_description(text: str) -> bool:
    """Detect Asset Store boilerplate descriptions."""
    lowered = text.casefold()
    generic_markers = (
        "elevate your workflow",
        "find this & other",
        "find this and other",
        "on the unity asset store",
        "use the ",
        " asset from ",
        " on your next project",
    )
    return any(marker in lowered for marker in generic_markers)


def _finalize_features(description: str, features: list[str]) -> list[str]:
    """Ensure feature bullets are usable or derive a concise fallback."""
    cleaned = [_clean_feature_text(item) for item in features]
    cleaned = [item for item in cleaned if item]
    if cleaned:
        return cleaned[:20]

    if not description:
        return []

    for paragraph in description.split("\n\n"):
        for line in paragraph.splitlines():
            stripped = line.strip().lstrip("-•").strip()
            if stripped.startswith("- ") or stripped.startswith("• "):
                stripped = stripped.lstrip("-•").strip()
            if len(stripped) >= 20 and not _is_generic_store_description(stripped):
                cleaned.append(stripped)
        if cleaned:
            return cleaned[:8]

    if description and not _is_generic_store_description(description):
        first = description.split("\n\n")[0].strip()
        if len(first) >= 40:
            return [first[:240]]
    return []


LIMITATION_PREFIXES = (
    "does not",
    "doesn't",
    "does only",
    "only supports",
    "only works",
    "will not",
    "not support",
    "not find",
    "not necessarily",
    "not automatically",
)


def _clean_feature_text(text: str) -> str:
    """Remove navigation and placeholder feature text."""
    stripped = text.strip()
    lowered = stripped.casefold()
    if lowered in BREADCRUMB_NAMES:
        return ""
    if lowered.startswith("link to"):
        return ""
    if lowered in {"key features", "technical details", "description", "additional integrated effects:"}:
        return ""
    if stripped.startswith("http"):
        return ""
    if any(lowered.startswith(prefix) for prefix in LIMITATION_PREFIXES):
        return ""
    if lowered in {"asset hunter pro", "rewritten from scratch", "is this a tool for me?"}:
        return ""
    return stripped


def _clean_videos(videos: list[str]) -> list[str]:
    """Remove generic Asset Store video links."""
    cleaned: list[str] = []
    for url in videos:
        lowered = url.casefold()
        if "youtube.com/user/assetstore" in lowered:
            continue
        if "youtu.be" in lowered or "youtube.com" in lowered or "vimeo.com" in lowered:
            cleaned.append(url)
    return cleaned[:10]


SOCIAL_HOSTS = ("facebook.com", "twitter.com", "x.com", "instagram.com", "linkedin.com")
DOC_HOST_MARKERS = ("docs.", "gitbook", "documentation", "manual", "wiki", "api-doc", "readme")


def _clean_links(links: list[str]) -> list[str]:
    """Keep likely documentation links and remove social, store, and video URLs."""
    cleaned: list[str] = []
    for link in links:
        if not link.startswith("http"):
            continue
        host = urlparse(link).netloc.casefold()
        lowered = link.casefold()
        if any(social in host for social in SOCIAL_HOSTS):
            continue
        if "assetstore.unity.com" in host:
            continue
        if any(domain in host for domain in VIDEO_DOMAINS):
            continue
        if "forum.unity" in host:
            continue
        if not any(marker in lowered for marker in DOC_HOST_MARKERS):
            continue
        if link not in cleaned:
            cleaned.append(link)
    return cleaned[:10]


def _promote_video_links(metadata: AssetMetadata) -> None:
    """Move video URLs out of documentation links into the videos list."""
    docs: list[str] = []
    for link in metadata.documentation_links:
        host = urlparse(link).netloc.casefold()
        if any(domain in host for domain in VIDEO_DOMAINS):
            if link not in metadata.videos:
                metadata.videos.append(link)
            continue
        docs.append(link)
    metadata.documentation_links = docs


def metadata_store_category_from_title(html: str) -> str:
    """Extract category from the HTML title tag."""
    match = re.search(r"\|\s*([^|]+?)\s*\|\s*Unity Asset Store", html)
    return match.group(1).strip() if match else ""


def _html_to_text(value: str) -> str:
    """Convert Asset Store HTML content to plain text."""
    if not value:
        return ""
    soup = BeautifulSoup(unescape(value), "lxml")
    paragraphs: list[str] = []
    for element in soup.find_all(["p", "li"]):
        text = element.get_text(" ", strip=True)
        if text:
            paragraphs.append(text)
    if paragraphs:
        return "\n\n".join(paragraphs[:8])
    return soup.get_text("\n\n", strip=True)


def _extract_features_from_html_fields(description_html: str, key_features_html: str) -> list[str]:
    """Extract bullet features from embedded HTML fields."""
    features: list[str] = []
    for html_value in (description_html, key_features_html):
        if not html_value:
            continue
        soup = BeautifulSoup(unescape(html_value), "lxml")
        for item in soup.find_all("li"):
            text = _clean_feature_text(item.get_text(" ", strip=True))
            if text and text not in features:
                features.append(text)
        for strong in soup.find_all("strong"):
            text = _clean_feature_text(strong.get_text(" ", strip=True))
            if text and len(text) >= 8 and text not in features:
                features.append(text)
        if features:
            break

    if not features and key_features_html:
        text = _html_to_text(key_features_html)
        if text:
            cleaned_lines = [
                line.strip()
                for line in text.splitlines()
                if line.strip()
                and not line.strip().casefold().startswith("link to")
                and line.strip().casefold() not in {"key features", "technical details"}
            ]
            features.extend(cleaned_lines[:8])

    return features[:20]


def _extract_links_from_html(value: str) -> list[str]:
    """Extract documentation links from HTML notes."""
    if not value:
        return []
    soup = BeautifulSoup(unescape(value), "lxml")
    links: list[str] = []
    for anchor in soup.find_all("a", href=True):
        href = anchor["href"]
        if href.startswith("http") and href not in links:
            links.append(href)
    return links[:10]


def _format_published_date(value: Any) -> str:
    """Format ISO publish dates to YYYY-MM-DD."""
    if not value:
        return ""
    text = str(value)
    return text[:10] if len(text) >= 10 else text


def _extract_name(soup: BeautifulSoup, fallback: str) -> str:
    """Extract asset name from page title or headings."""
    og_title = soup.find("meta", attrs={"property": "og:title"})
    if og_title and og_title.get("content"):
        title = str(og_title["content"]).split("|")[0].strip()
        if title:
            return title

    for heading in soup.find_all(["h1", "h2"]):
        text = heading.get_text(" ", strip=True)
        if text and len(text) > 3 and "asset store" not in text.casefold():
            return text

    return fallback


def _extract_publisher(soup: BeautifulSoup) -> str:
    """Extract publisher name from publisher info section or links."""
    publisher_heading = soup.find(
        lambda tag: isinstance(tag, Tag)
        and tag.name in {"h2", "h3", "div", "span"}
        and "publisher info" in tag.get_text(" ", strip=True).casefold()
    )
    if publisher_heading:
        link = publisher_heading.find_next("a", href=True)
        if link:
            return link.get_text(" ", strip=True)

    title_tag = soup.find("title")
    if title_tag:
        match = re.search(r"\|\s*([^|]+?)\s*\|\s*Unity Asset Store", title_tag.get_text())
        if match:
            return match.group(1).strip()

    for anchor in soup.find_all("a", href=True):
        href = anchor["href"]
        if "/publishers/" in href:
            text = anchor.get_text(" ", strip=True)
            if text:
                return text

    return ""


def _extract_description(soup: BeautifulSoup) -> str:
    """Extract the main description section."""
    description_heading = _find_heading(soup, ("description",))
    if description_heading:
        paragraphs: list[str] = []
        for sibling in description_heading.find_all_next(["p", "li"], limit=20):
            if _is_section_boundary(sibling):
                break
            text = sibling.get_text(" ", strip=True)
            if text and text not in paragraphs:
                paragraphs.append(text)
        if paragraphs:
            return "\n\n".join(paragraphs[:6])

    og_desc = soup.find("meta", attrs={"property": "og:description"})
    if og_desc and og_desc.get("content"):
        return str(og_desc["content"]).strip()

    meta_desc = soup.find("meta", attrs={"name": "description"})
    if meta_desc and meta_desc.get("content"):
        return str(meta_desc["content"]).strip()

    return ""


def _extract_features(soup: BeautifulSoup) -> list[str]:
    """Extract bullet features from known feature sections."""
    features: list[str] = []
    for heading in soup.find_all(["h2", "h3", "h4", "strong", "span", "div"]):
        heading_text = heading.get_text(" ", strip=True).casefold()
        if not any(token in heading_text for token in FEATURE_HEADINGS):
            continue
        for item in heading.find_all_next(["li"], limit=30):
            if _is_section_boundary(item):
                break
            text = item.get_text(" ", strip=True)
            if text and text not in features:
                features.append(text)
        if features:
            break

    if not features:
        for paragraph in soup.find_all("p"):
            text = paragraph.get_text(" ", strip=True)
            if text.startswith("- ") or text.startswith("• "):
                cleaned = text.lstrip("-• ").strip()
                if cleaned:
                    features.append(cleaned)

    return features[:20]


def _extract_pipeline_table(soup: BeautifulSoup) -> tuple[list[str], list[str]]:
    """Extract Unity versions and supported render pipelines from compatibility table."""
    unity_versions: list[str] = []
    render_pipelines: list[str] = []

    for table in soup.find_all("table"):
        headers = [cell.get_text(" ", strip=True).casefold() for cell in table.find_all("th")]
        if not headers or "unity version" not in headers[0]:
            continue

        pipeline_columns: dict[int, str] = {}
        for index, header in enumerate(headers[1:], start=1):
            if header in {"built-in", "urp", "hdrp"}:
                pipeline_columns[index] = header.upper() if header != "built-in" else "Built-in"

        for row in table.find_all("tr")[1:]:
            cells = row.find_all(["td", "th"])
            if not cells:
                continue
            version = cells[0].get_text(" ", strip=True)
            if version and version not in unity_versions:
                unity_versions.append(version)
            for index, pipeline_name in pipeline_columns.items():
                if index >= len(cells):
                    continue
                status = cells[index].get_text(" ", strip=True).casefold()
                if "compatible" in status and "not compatible" not in status:
                    if pipeline_name not in render_pipelines:
                        render_pipelines.append(pipeline_name)

    return unity_versions, render_pipelines


def _extract_labeled_value(soup: BeautifulSoup, labels: tuple[str, ...]) -> str:
    """Extract a value that follows a known label in page text."""
    page_text = soup.get_text("\n", strip=True)
    for label in labels:
        pattern = re.compile(rf"{re.escape(label)}\s*\n?\s*([^\n]+)", re.IGNORECASE)
        match = pattern.search(page_text)
        if match:
            return match.group(1).strip()
    return ""


def _extract_store_category(soup: BeautifulSoup, asset_name: str) -> str:
    """Extract store category breadcrumb from title metadata."""
    title_tag = soup.find("title")
    if title_tag:
        match = re.search(
            rf"{re.escape(asset_name)}\s*\|\s*([^|]+?)\s*\|\s*Unity Asset Store",
            title_tag.get_text(),
            re.IGNORECASE,
        )
        if match:
            return match.group(1).strip()
    return ""


def _extract_documentation_links(soup: BeautifulSoup) -> list[str]:
    """Extract documentation and manual links."""
    links: list[str] = []
    for anchor in soup.find_all("a", href=True):
        href = anchor["href"]
        text = anchor.get_text(" ", strip=True).casefold()
        if any(token in text for token in ("documentation", "manual", "readme", "wiki")):
            absolute = urljoin("https://assetstore.unity.com", href)
            if absolute not in links:
                links.append(absolute)
        elif any(token in href.casefold() for token in ("docs", "documentation", "manual", "wiki")):
            absolute = urljoin("https://assetstore.unity.com", href)
            if absolute not in links:
                links.append(absolute)
    return links[:10]


def _extract_website(soup: BeautifulSoup, publisher: str) -> str:
    """Extract publisher website link when available."""
    for anchor in soup.find_all("a", href=True):
        href = anchor["href"]
        text = anchor.get_text(" ", strip=True)
        if not href.startswith("http"):
            continue
        host = urlparse(href).netloc.casefold()
        if "unity.com" in host or any(domain in host for domain in VIDEO_DOMAINS):
            continue
        if publisher and publisher.casefold() in text.casefold():
            return href
        if "website" in text.casefold() or "publisher" in text.casefold():
            return href
    return ""


def _extract_video_links(soup: BeautifulSoup) -> list[str]:
    """Extract linked demo or trailer videos."""
    videos: list[str] = []
    for anchor in soup.find_all("a", href=True):
        href = anchor["href"]
        if not href.startswith("http"):
            continue
        if any(domain in href.casefold() for domain in VIDEO_DOMAINS):
            videos.append(href)
    return _clean_videos(videos)


def _find_heading(soup: BeautifulSoup, labels: tuple[str, ...]) -> Tag | None:
    """Find the first heading matching one of the labels."""
    for tag in soup.find_all(["h1", "h2", "h3", "h4", "strong", "span", "div"]):
        text = tag.get_text(" ", strip=True).casefold()
        if text in labels:
            return tag
    return None


def _is_section_boundary(tag: Tag) -> bool:
    """Detect whether a tag begins a new major section."""
    if tag.name in {"h1", "h2", "h3"}:
        return True
    text = tag.get_text(" ", strip=True).casefold()
    return text in {
        "publisher info",
        "asset quality",
        "reviews",
        "releases",
        "related keywords",
        "license agreement",
        "package content",
    }
