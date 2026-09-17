#!/usr/bin/env python3
"""Collect bounded, read-only evidence about public GitHub repositories."""

from __future__ import annotations

import argparse
import base64
import binascii
import datetime as dt
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time
from typing import Any, Protocol
import urllib.error
import urllib.parse
import urllib.request


API_BASE = "https://api.github.com"
API_VERSION = "2022-11-28"
USER_AGENT = "codex-github-researcher/1.0"
TRANSIENT_STATUSES = {429, 500, 502, 503, 504}
REPOSITORY_RE = re.compile(r"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})/[A-Za-z0-9._-]{1,100}$")
PRIVATE_QUALIFIER_RE = re.compile(r"(?:^|\s)(?:is|visibility):(private|internal)(?:\s|$)", re.I)
TOPIC_RE = re.compile(r"^[A-Za-z0-9][A-Za-z0-9_.-]*$")

DEPTHS: dict[str, dict[str, int]] = {
    "quick": {
        "candidates": 10,
        "inspect": 3,
        "readme_chars": 12_000,
        "activity": 0,
        "releases": 1,
        "contributors": 0,
        "code_terms": 0,
    },
    "standard": {
        "candidates": 25,
        "inspect": 5,
        "readme_chars": 30_000,
        "activity": 10,
        "releases": 1,
        "contributors": 0,
        "code_terms": 0,
    },
    "deep": {
        "candidates": 50,
        "inspect": 10,
        "readme_chars": 60_000,
        "activity": 30,
        "releases": 10,
        "contributors": 20,
        "code_terms": 3,
    },
}


class APIError(RuntimeError):
    def __init__(self, status: int | None, message: str, *, rate_limited: bool = False) -> None:
        super().__init__(message)
        self.status = status
        self.rate_limited = rate_limited


class PublicOnlyError(RuntimeError):
    pass


class Client(Protocol):
    name: str
    authenticated: bool
    rate_limit: dict[str, str | int | None]

    def get(self, path: str, params: dict[str, object] | None = None, *, allow_404: bool = False) -> Any:
        ...


def utc_now() -> str:
    return dt.datetime.now(dt.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def eprint(message: str) -> None:
    print(message, file=sys.stderr)


def rate_limit_from_headers(headers: Any) -> dict[str, str | int | None]:
    def value(name: str) -> str | None:
        if headers is None:
            return None
        return headers.get(name)

    remaining = value("X-RateLimit-Remaining")
    limit = value("X-RateLimit-Limit")
    return {
        "limit": int(limit) if limit and limit.isdigit() else limit,
        "remaining": int(remaining) if remaining and remaining.isdigit() else remaining,
        "reset_epoch": value("X-RateLimit-Reset"),
        "resource": value("X-RateLimit-Resource"),
        "retry_after": value("Retry-After"),
    }


def extract_error_message(body: bytes, fallback: str) -> str:
    try:
        value = json.loads(body.decode("utf-8", errors="replace"))
    except (json.JSONDecodeError, UnicodeDecodeError):
        return fallback
    if isinstance(value, dict) and isinstance(value.get("message"), str):
        return value["message"]
    return fallback


class RestClient:
    name = "rest"

    def __init__(self, token: str | None = None, *, sleeper: Any = time.sleep) -> None:
        self.token = token if token is not None else (os.environ.get("GH_TOKEN") or os.environ.get("GITHUB_TOKEN"))
        self.authenticated = bool(self.token)
        self.rate_limit: dict[str, str | int | None] = {}
        self._cache: dict[str, Any] = {}
        self._sleep = sleeper

    def get(self, path: str, params: dict[str, object] | None = None, *, allow_404: bool = False) -> Any:
        path = path.lstrip("/")
        query = urllib.parse.urlencode(params or {}, doseq=True)
        url = f"{API_BASE}/{path}"
        if query:
            url = f"{url}?{query}"
        if url in self._cache:
            return self._cache[url]

        headers = {
            "Accept": "application/vnd.github+json",
            "X-GitHub-Api-Version": API_VERSION,
            "User-Agent": USER_AGENT,
        }
        if self.token:
            headers["Authorization"] = f"Bearer {self.token}"

        for attempt in range(3):
            request = urllib.request.Request(url, headers=headers, method="GET")
            try:
                with urllib.request.urlopen(request, timeout=45) as response:
                    self.rate_limit = rate_limit_from_headers(response.headers)
                    value = json.load(response)
                    self._cache[url] = value
                    return value
            except urllib.error.HTTPError as exc:
                body = exc.read()
                self.rate_limit = rate_limit_from_headers(exc.headers)
                if exc.code == 404 and allow_404:
                    self._cache[url] = None
                    return None
                remaining = self.rate_limit.get("remaining")
                message = extract_error_message(body, exc.reason or f"HTTP {exc.code}")
                rate_limited = exc.code == 429 or (exc.code == 403 and (remaining == 0 or "rate limit" in message.lower()))
                if exc.code in TRANSIENT_STATUSES and not rate_limited and attempt < 2:
                    self._sleep(0.25 * (2**attempt))
                    continue
                raise APIError(exc.code, message, rate_limited=rate_limited) from exc
            except urllib.error.URLError as exc:
                if attempt < 2:
                    self._sleep(0.25 * (2**attempt))
                    continue
                raise APIError(None, f"Request failed: {exc.reason}") from exc
        raise AssertionError("unreachable")


class GhClient:
    name = "gh"
    authenticated = True

    def __init__(self, executable: str = "gh") -> None:
        self.executable = executable
        self.rate_limit: dict[str, str | int | None] = {}
        self._cache: dict[str, Any] = {}

    def get(self, path: str, params: dict[str, object] | None = None, *, allow_404: bool = False) -> Any:
        path = path.lstrip("/")
        cache_key = json.dumps([path, params or {}], sort_keys=True)
        if cache_key in self._cache:
            return self._cache[cache_key]
        command = [
            self.executable,
            "api",
            "--method",
            "GET",
            "-H",
            f"Accept: application/vnd.github+json",
            "-H",
            f"X-GitHub-Api-Version: {API_VERSION}",
            path,
        ]
        for key, value in (params or {}).items():
            values = value if isinstance(value, list) else [value]
            for item in values:
                command.extend(["-f", f"{key}={item}"])
        completed = subprocess.run(command, capture_output=True, text=True, timeout=60, check=False)
        if completed.returncode != 0:
            message = completed.stderr.strip() or "GitHub CLI request failed"
            status_match = re.search(r"HTTP\s+(\d{3})", message, re.I)
            status = int(status_match.group(1)) if status_match else None
            if allow_404 and (status == 404 or "not found" in message.lower()):
                self._cache[cache_key] = None
                return None
            rate_limited = status == 429 or "rate limit" in message.lower()
            raise APIError(status, message, rate_limited=rate_limited)
        try:
            value = json.loads(completed.stdout)
        except json.JSONDecodeError as exc:
            raise APIError(None, "GitHub CLI returned invalid JSON") from exc
        self._cache[cache_key] = value
        return value


def gh_is_authenticated(executable: str) -> bool:
    completed = subprocess.run(
        [executable, "auth", "status", "--hostname", "github.com"],
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        timeout=15,
        check=False,
    )
    return completed.returncode == 0


def make_client(backend: str) -> Client:
    executable = shutil.which("gh")
    if backend == "gh":
        if not executable or not gh_is_authenticated(executable):
            raise RuntimeError("The gh backend requires an installed, authenticated GitHub CLI")
        return GhClient(executable)
    if backend == "rest":
        return RestClient()
    if executable and gh_is_authenticated(executable):
        return GhClient(executable)
    return RestClient()


def validate_repository(value: str) -> str:
    value = value.strip()
    if not REPOSITORY_RE.fullmatch(value):
        raise argparse.ArgumentTypeError("repository must be in owner/name form")
    return value


def positive_int(value: str) -> int:
    parsed = int(value)
    if parsed < 1 or parsed > 100:
        raise argparse.ArgumentTypeError("value must be between 1 and 100")
    return parsed


def iso_date(value: str) -> str:
    try:
        dt.date.fromisoformat(value)
    except ValueError as exc:
        raise argparse.ArgumentTypeError("date must use YYYY-MM-DD") from exc
    return value


def topic(value: str) -> str:
    if not TOPIC_RE.fullmatch(value):
        raise argparse.ArgumentTypeError("topic may contain letters, digits, dots, underscores, and hyphens")
    return value


def quote_qualifier(value: str) -> str:
    value = value.strip()
    if not value or "\n" in value or "\r" in value:
        raise ValueError("empty or multiline qualifier")
    return '"' + value.replace('"', '\\"') + '"'


def build_query(
    query: str,
    *,
    language: str | None = None,
    topics: list[str] | None = None,
    min_stars: int | None = None,
    updated_after: str | None = None,
    include_forks: bool = False,
    include_archived: bool = False,
) -> str:
    query = query.strip()
    if not query:
        raise ValueError("search query must not be empty")
    if PRIVATE_QUALIFIER_RE.search(query):
        raise ValueError("private and internal repository qualifiers are not allowed")
    parts = [query, "is:public"]
    if not include_forks:
        parts.append("fork:false")
    if not include_archived:
        parts.append("archived:false")
    if language:
        parts.append(f"language:{quote_qualifier(language)}")
    for item in topics or []:
        parts.append(f"topic:{item}")
    if min_stars is not None:
        parts.append(f"stars:>={min_stars}")
    if updated_after:
        parts.append(f"pushed:>={updated_after}")
    return " ".join(parts)


def public_repository(data: dict[str, Any]) -> bool:
    visibility = data.get("visibility")
    return data.get("private") is not True and visibility in (None, "public")


def normalize_repository(data: dict[str, Any]) -> dict[str, Any]:
    owner = data.get("owner") if isinstance(data.get("owner"), dict) else {}
    license_data = data.get("license") if isinstance(data.get("license"), dict) else {}
    return {
        "full_name": data.get("full_name"),
        "name": data.get("name"),
        "owner": owner.get("login"),
        "url": data.get("html_url"),
        "description": data.get("description"),
        "homepage": data.get("homepage"),
        "language": data.get("language"),
        "topics": sorted(data.get("topics") or []),
        "license": {
            "spdx_id": license_data.get("spdx_id"),
            "name": license_data.get("name"),
        },
        "stars": data.get("stargazers_count"),
        "forks": data.get("forks_count"),
        "watchers": data.get("subscribers_count", data.get("watchers_count")),
        "open_issues": data.get("open_issues_count"),
        "default_branch": data.get("default_branch"),
        "is_fork": data.get("fork"),
        "is_archived": data.get("archived"),
        "is_disabled": data.get("disabled"),
        "visibility": data.get("visibility"),
        "size_kb": data.get("size"),
        "created_at": data.get("created_at"),
        "updated_at": data.get("updated_at"),
        "pushed_at": data.get("pushed_at"),
    }


def search_repositories(client: Client, query: str, limit: int) -> dict[str, Any]:
    repositories: list[dict[str, Any]] = []
    seen: set[str] = set()
    page = 1
    total_count: int | None = None
    incomplete = False
    while len(repositories) < limit:
        per_page = min(100, limit - len(repositories))
        response = client.get(
            "search/repositories",
            {"q": query, "per_page": per_page, "page": page, "sort": "stars", "order": "desc"},
        )
        if not isinstance(response, dict):
            raise APIError(None, "Repository search returned an unexpected response")
        if total_count is None and isinstance(response.get("total_count"), int):
            total_count = response["total_count"]
        incomplete = incomplete or bool(response.get("incomplete_results"))
        items = response.get("items")
        if not isinstance(items, list) or not items:
            break
        for item in items:
            if not isinstance(item, dict) or not public_repository(item):
                continue
            full_name = item.get("full_name")
            if not isinstance(full_name, str) or full_name.lower() in seen:
                continue
            seen.add(full_name.lower())
            repositories.append(normalize_repository(item))
            if len(repositories) >= limit:
                break
        if len(items) < per_page:
            break
        page += 1
    return {
        "query": query,
        "total_count": total_count,
        "incomplete_results": incomplete,
        "returned": len(repositories),
        "repositories": repositories,
    }


def encoded_repo(repository: str) -> str:
    owner, name = repository.split("/", 1)
    return f"{urllib.parse.quote(owner, safe='')}/{urllib.parse.quote(name, safe='')}"


def short_message(value: object) -> str | None:
    if not isinstance(value, str):
        return None
    return value.splitlines()[0][:300]


def inspect_repository(client: Client, repository: str, depth: str, code_terms: list[str] | None = None) -> dict[str, Any]:
    config = DEPTHS[depth]
    slug = encoded_repo(repository)
    errors: list[dict[str, Any]] = []
    gaps: list[str] = []

    detail = client.get(f"repos/{slug}")
    if not isinstance(detail, dict):
        raise APIError(None, f"Repository inspection returned an unexpected response for {repository}")
    if not public_repository(detail):
        raise PublicOnlyError(f"Refusing to inspect non-public repository: {repository}")
    normalized = normalize_repository(detail)
    canonical = normalized.get("full_name") or repository

    result: dict[str, Any] = {
        "repository": normalized,
        "readme": None,
        "latest_release": None,
        "root_entries": [],
        "community_profile": None,
        "recent_commits": [],
        "recent_issues": [],
        "recent_pull_requests": [],
        "releases": [],
        "contributors": [],
        "code_matches": [],
        "sources": [detail.get("html_url")] if detail.get("html_url") else [],
        "gaps": gaps,
        "errors": errors,
    }

    def fetch(label: str, path: str, params: dict[str, object] | None = None, *, allow_404: bool = True) -> Any:
        try:
            return client.get(path, params, allow_404=allow_404)
        except APIError as exc:
            errors.append({"source": label, "status": exc.status, "message": str(exc), "rate_limited": exc.rate_limited})
            if exc.rate_limited:
                gaps.append("GitHub rate limiting stopped further complete evidence collection")
            return None

    readme = fetch("readme", f"repos/{slug}/readme")
    if isinstance(readme, dict):
        content = readme.get("content")
        decoded: str | None = None
        if isinstance(content, str) and readme.get("encoding") == "base64":
            try:
                decoded = base64.b64decode(content, validate=False).decode("utf-8", errors="replace")
            except (binascii.Error, ValueError):
                errors.append({"source": "readme", "status": None, "message": "README content was not valid base64", "rate_limited": False})
        result["readme"] = {
            "name": readme.get("name"),
            "path": readme.get("path"),
            "sha": readme.get("sha"),
            "url": readme.get("html_url"),
            "text": decoded[: config["readme_chars"]] if decoded is not None else None,
            "truncated": bool(decoded is not None and len(decoded) > config["readme_chars"]),
        }
        if readme.get("html_url"):
            result["sources"].append(readme["html_url"])
    else:
        gaps.append("No README was available through the GitHub API")

    latest_release = fetch("latest_release", f"repos/{slug}/releases/latest")
    if isinstance(latest_release, dict):
        result["latest_release"] = normalize_release(latest_release)
        if latest_release.get("html_url"):
            result["sources"].append(latest_release["html_url"])
    else:
        gaps.append("No published latest release was found")

    if depth in {"standard", "deep"}:
        root = fetch("root_contents", f"repos/{slug}/contents")
        if isinstance(root, list):
            result["root_entries"] = [
                {"name": item.get("name"), "path": item.get("path"), "type": item.get("type"), "url": item.get("html_url")}
                for item in root
                if isinstance(item, dict)
            ]

        community = fetch("community_profile", f"repos/{slug}/community/profile")
        if isinstance(community, dict):
            files = community.get("files") if isinstance(community.get("files"), dict) else {}
            normalized_files: dict[str, Any] = {}
            for key, value in files.items():
                if isinstance(value, dict):
                    normalized_files[key] = {"url": value.get("html_url"), "path": value.get("path")}
                    if value.get("html_url"):
                        result["sources"].append(value["html_url"])
                else:
                    normalized_files[key] = None
            result["community_profile"] = {
                "health_percentage": community.get("health_percentage"),
                "description": community.get("description"),
                "documentation": community.get("documentation"),
                "files": normalized_files,
            }

        activity_limit = config["activity"]
        commits = fetch("commits", f"repos/{slug}/commits", {"per_page": activity_limit})
        if isinstance(commits, list):
            result["recent_commits"] = [normalize_commit(item) for item in commits if isinstance(item, dict)]

        issues = fetch(
            "issues",
            f"repos/{slug}/issues",
            {"state": "all", "sort": "updated", "direction": "desc", "per_page": activity_limit},
        )
        if isinstance(issues, list):
            result["recent_issues"] = [normalize_issue(item) for item in issues if isinstance(item, dict) and "pull_request" not in item]

        pulls = fetch(
            "pull_requests",
            f"repos/{slug}/pulls",
            {"state": "all", "sort": "updated", "direction": "desc", "per_page": activity_limit},
        )
        if isinstance(pulls, list):
            result["recent_pull_requests"] = [normalize_issue(item) for item in pulls if isinstance(item, dict)]

    if depth == "deep":
        releases = fetch("releases", f"repos/{slug}/releases", {"per_page": config["releases"]})
        if isinstance(releases, list):
            result["releases"] = [normalize_release(item) for item in releases if isinstance(item, dict)]

        contributors = fetch("contributors", f"repos/{slug}/contributors", {"per_page": config["contributors"], "anon": "true"})
        if isinstance(contributors, list):
            result["contributors"] = [
                {
                    "login": item.get("login") or item.get("name"),
                    "contributions": item.get("contributions"),
                    "url": item.get("html_url"),
                }
                for item in contributors
                if isinstance(item, dict)
            ]

        requested_terms = [term.strip() for term in (code_terms or []) if term.strip()][: config["code_terms"]]
        if requested_terms and not client.authenticated:
            gaps.append("Authenticated GitHub access was unavailable, so requested code search was skipped")
        elif requested_terms:
            for term in requested_terms:
                matches = fetch("code_search", "search/code", {"q": f"{term} repo:{canonical}", "per_page": 10})
                if isinstance(matches, dict) and isinstance(matches.get("items"), list):
                    for item in matches["items"]:
                        if isinstance(item, dict):
                            result["code_matches"].append(
                                {
                                    "term": term,
                                    "name": item.get("name"),
                                    "path": item.get("path"),
                                    "sha": item.get("sha"),
                                    "url": item.get("html_url"),
                                }
                            )

    result["sources"] = sorted({url for url in result["sources"] if isinstance(url, str)})
    result["gaps"] = list(dict.fromkeys(gaps))
    return result


def normalize_commit(item: dict[str, Any]) -> dict[str, Any]:
    commit = item.get("commit") if isinstance(item.get("commit"), dict) else {}
    author = commit.get("author") if isinstance(commit.get("author"), dict) else {}
    github_author = item.get("author") if isinstance(item.get("author"), dict) else {}
    return {
        "sha": item.get("sha"),
        "date": author.get("date"),
        "author": github_author.get("login") or author.get("name"),
        "message": short_message(commit.get("message")),
        "url": item.get("html_url"),
    }


def normalize_issue(item: dict[str, Any]) -> dict[str, Any]:
    return {
        "number": item.get("number"),
        "title": item.get("title"),
        "state": item.get("state"),
        "created_at": item.get("created_at"),
        "updated_at": item.get("updated_at"),
        "closed_at": item.get("closed_at"),
        "url": item.get("html_url"),
    }


def normalize_release(item: dict[str, Any]) -> dict[str, Any]:
    return {
        "tag": item.get("tag_name"),
        "name": item.get("name"),
        "published_at": item.get("published_at"),
        "is_draft": item.get("draft"),
        "is_prerelease": item.get("prerelease"),
        "url": item.get("html_url"),
    }


def base_document(command: str, client: Client, depth: str) -> dict[str, Any]:
    return {
        "metadata": {
            "schema_version": 1,
            "command": command,
            "generated_at": utc_now(),
            "backend": client.name,
            "authenticated": client.authenticated,
            "depth": depth,
            "public_only": True,
        }
    }


def rate_limit_snapshot(client: Client) -> dict[str, Any]:
    if client.rate_limit:
        return dict(client.rate_limit)
    try:
        response = client.get("rate_limit")
    except APIError as exc:
        return {"available": False, "message": str(exc)}
    if not isinstance(response, dict) or not isinstance(response.get("resources"), dict):
        return {"available": False}
    snapshot: dict[str, Any] = {}
    for name in ("core", "search", "code_search"):
        resource = response["resources"].get(name)
        if isinstance(resource, dict):
            snapshot[name] = {
                key: resource.get(key)
                for key in ("limit", "remaining", "used", "reset")
                if key in resource
            }
    return snapshot or {"available": False}


def filters_from_args(args: argparse.Namespace) -> dict[str, Any]:
    return {
        "language": getattr(args, "language", None),
        "topics": getattr(args, "topic", None),
        "min_stars": getattr(args, "min_stars", None),
        "updated_after": getattr(args, "updated_after", None),
        "include_forks": getattr(args, "include_forks", False),
        "include_archived": getattr(args, "include_archived", False),
    }


def run_search(args: argparse.Namespace, client: Client) -> dict[str, Any]:
    query = build_query(args.query, **filters_from_args(args))
    limit = args.limit or DEPTHS[args.depth]["candidates"]
    result = base_document("search", client, args.depth)
    result["search"] = search_repositories(client, query, limit)
    result["rate_limit"] = rate_limit_snapshot(client)
    return result


def run_inspect(args: argparse.Namespace, client: Client) -> dict[str, Any]:
    result = base_document("inspect", client, args.depth)
    result["result"] = inspect_repository(client, args.repository, args.depth, args.code_term)
    result["rate_limit"] = rate_limit_snapshot(client)
    return result


def run_collect(args: argparse.Namespace, client: Client) -> dict[str, Any]:
    result = base_document("collect", client, args.depth)
    candidates: list[dict[str, Any]] = []
    search_result: dict[str, Any] | None = None
    if args.query:
        query = build_query(args.query, **filters_from_args(args))
        limit = args.limit or DEPTHS[args.depth]["candidates"]
        search_result = search_repositories(client, query, limit)
        candidates = search_result["repositories"]

    explicit = list(dict.fromkeys(args.repo or []))
    selected = explicit[:50]
    seen = {repository.lower() for repository in selected}
    target_count = max(DEPTHS[args.depth]["inspect"], len(selected))
    for candidate in candidates:
        full_name = candidate.get("full_name")
        if isinstance(full_name, str) and full_name.lower() not in seen:
            selected.append(full_name)
            seen.add(full_name.lower())
        if len(selected) >= target_count:
            break

    inspected: list[dict[str, Any]] = []
    errors: list[dict[str, Any]] = []
    for repository in selected:
        try:
            inspected.append(inspect_repository(client, repository, args.depth, args.code_term))
        except (APIError, PublicOnlyError) as exc:
            errors.append(
                {
                    "repository": repository,
                    "status": exc.status if isinstance(exc, APIError) else None,
                    "message": str(exc),
                    "rate_limited": exc.rate_limited if isinstance(exc, APIError) else False,
                }
            )
            if isinstance(exc, APIError) and exc.rate_limited:
                break

    result["search"] = search_result
    result["candidates"] = candidates
    result["inspected"] = inspected
    result["errors"] = errors
    result["coverage"] = {
        "candidate_count": len(candidates),
        "requested_inspections": len(selected),
        "completed_inspections": len(inspected),
    }
    result["rate_limit"] = rate_limit_snapshot(client)
    return result


def add_common_options(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("--backend", choices=("auto", "gh", "rest"), default="auto")
    parser.add_argument("--depth", choices=tuple(DEPTHS), default="standard")
    parser.add_argument("--output", type=Path, help="write the JSON result to this path as well as stdout")


def add_search_options(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("--limit", type=positive_int, help="candidate limit, from 1 to 100")
    parser.add_argument("--language")
    parser.add_argument("--topic", action="append", type=topic, help="GitHub topic; repeat for multiple topics")
    parser.add_argument("--min-stars", type=int)
    parser.add_argument("--updated-after", type=iso_date)
    parser.add_argument("--include-forks", action="store_true")
    parser.add_argument("--include-archived", action="store_true")


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)

    search_parser = subparsers.add_parser("search", help="discover public repositories")
    search_parser.add_argument("query")
    add_common_options(search_parser)
    add_search_options(search_parser)

    inspect_parser = subparsers.add_parser("inspect", help="collect evidence for one public repository")
    inspect_parser.add_argument("repository", type=validate_repository)
    inspect_parser.add_argument("--code-term", action="append", default=[], help="deep-only authenticated code search term")
    add_common_options(inspect_parser)

    collect_parser = subparsers.add_parser("collect", help="search and inspect leading public repositories")
    collect_parser.add_argument("query", nargs="?")
    collect_parser.add_argument("--repo", action="append", type=validate_repository, help="explicit repository; repeat as needed")
    collect_parser.add_argument("--code-term", action="append", default=[], help="deep-only authenticated code search term")
    add_common_options(collect_parser)
    add_search_options(collect_parser)
    return parser


def emit(value: dict[str, Any], output: Path | None) -> None:
    rendered = json.dumps(value, indent=2, sort_keys=True, ensure_ascii=False) + "\n"
    sys.stdout.write(rendered)
    if output is not None:
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(rendered, encoding="utf-8")


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    if args.command == "collect" and not args.query and not args.repo:
        parser.error("collect requires a query or at least one --repo")
    if getattr(args, "min_stars", None) is not None and args.min_stars < 0:
        parser.error("--min-stars must be zero or greater")
    try:
        client = make_client(args.backend)
        if client.name == "rest" and not client.authenticated:
            eprint("Using unauthenticated public GitHub REST access; rate limits are lower")
        if args.command == "search":
            result = run_search(args, client)
        elif args.command == "inspect":
            result = run_inspect(args, client)
        else:
            result = run_collect(args, client)
        emit(result, args.output)
        return 0
    except (APIError, PublicOnlyError, RuntimeError, ValueError) as exc:
        status = exc.status if isinstance(exc, APIError) else None
        error = {
            "error": {
                "type": type(exc).__name__,
                "status": status,
                "message": str(exc),
                "rate_limited": exc.rate_limited if isinstance(exc, APIError) else False,
            }
        }
        emit(error, args.output)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
