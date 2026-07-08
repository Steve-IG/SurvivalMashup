"""HTTP scraping utilities for Unity Asset Store pages."""

from __future__ import annotations

import logging
import time
from dataclasses import dataclass

import requests
from requests.adapters import HTTPAdapter
from urllib3.util.retry import Retry

from utils import USER_AGENT

logger = logging.getLogger(__name__)


@dataclass
class AssetScraper:
    """Download Asset Store pages with rate limiting and retries."""

    rate_limit_seconds: float = 1.5
    timeout_seconds: float = 30.0
    max_retries: int = 3
    backoff_factor: float = 1.5
    _last_request_at: float = 0.0

    def __post_init__(self) -> None:
        self._session = requests.Session()
        retry = Retry(
            total=self.max_retries,
            connect=self.max_retries,
            read=self.max_retries,
            status=self.max_retries,
            backoff_factor=self.backoff_factor,
            status_forcelist=(429, 500, 502, 503, 504),
            allowed_methods=("GET",),
            raise_on_status=False,
        )
        adapter = HTTPAdapter(max_retries=retry)
        self._session.mount("https://", adapter)
        self._session.mount("http://", adapter)
        self._session.headers.update(
            {
                "User-Agent": USER_AGENT,
                "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
                "Accept-Language": "en-US,en;q=0.9",
            }
        )

    def fetch(self, url: str) -> tuple[str, str | None]:
        """Fetch a page and return HTML plus optional error message."""
        self._respect_rate_limit()
        try:
            response = self._session.get(url, timeout=self.timeout_seconds)
            response.raise_for_status()
            logger.debug("Fetched %s (%d bytes)", url, len(response.text))
            return response.text, None
        except requests.RequestException as exc:
            message = f"{type(exc).__name__}: {exc}"
            logger.warning("Failed to fetch %s: %s", url, message)
            return "", message

    def _respect_rate_limit(self) -> None:
        """Sleep between requests to respect rate limits."""
        now = time.monotonic()
        elapsed = now - self._last_request_at
        if elapsed < self.rate_limit_seconds:
            time.sleep(self.rate_limit_seconds - elapsed)
        self._last_request_at = time.monotonic()
