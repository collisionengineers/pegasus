#!/usr/bin/env python3
"""Validate the sanitized manufacturer method index."""

from __future__ import annotations

import re
import sys
from pathlib import Path, PurePosixPath, PureWindowsPath
from typing import Any

from validate_cli import load_json


ROOT = Path(__file__).resolve().parents[1]
INDEX_PATH = ROOT / "references" / "method-index.json"
ALLOWED_SOURCE_STATUS = {"pointer_only_current_source_required"}
REQUIRED_ENTRY_FIELDS = {
    "id",
    "make",
    "model",
    "model_years",
    "components",
    "methods",
    "source_family",
    "reference_file",
    "source_status",
    "verification_required",
    "safe_pointer",
    "lookup_terms",
}
PROPRIETARY_DETAIL_PATTERNS = [
    re.compile(r"\b\d+(?:\.\d+)?\s*mm\b", re.IGNORECASE),
    re.compile(r"\b\d+\s*welds?\b", re.IGNORECASE),
    re.compile(r"\bstep\s+\d+\b", re.IGNORECASE),
    re.compile(r"\btorque\s+(?:to\s+)?\d+", re.IGNORECASE),
]


def _fail(message: str) -> None:
    print(f"method-index invalid: {message}", file=sys.stderr)
    raise SystemExit(1)


def _require_string(entry: dict[str, Any], field: str) -> str:
    value = entry.get(field)
    if not isinstance(value, str) or not value.strip():
        _fail(f"{entry.get('id', '<unknown>')} field {field!r} must be a non-empty string")
    return value


def _require_string_list(entry: dict[str, Any], field: str) -> list[str]:
    value = entry.get(field)
    if not isinstance(value, list) or not value:
        _fail(f"{entry.get('id', '<unknown>')} field {field!r} must be a non-empty list")
    for item in value:
        if not isinstance(item, str) or not item.strip():
            _fail(f"{entry.get('id', '<unknown>')} field {field!r} contains an empty/non-string item")
    return value


def _check_no_proprietary_detail(entry: dict[str, Any]) -> None:
    text_parts: list[str] = []
    for value in entry.values():
        if isinstance(value, str):
            text_parts.append(value)
        elif isinstance(value, list):
            text_parts.extend(item for item in value if isinstance(item, str))
    text = "\n".join(text_parts)
    for pattern in PROPRIETARY_DETAIL_PATTERNS:
        if pattern.search(text):
            _fail(f"{entry['id']} appears to contain procedure-level proprietary detail")


def _resolve_reference_path(entry_id: str, reference_file: str) -> Path:
    posix_path = PurePosixPath(reference_file)
    windows_path = PureWindowsPath(reference_file)

    if "\\" in reference_file:
        _fail(f"{entry_id} reference_file must use POSIX-style relative paths")
    if posix_path.is_absolute() or windows_path.is_absolute():
        _fail(f"{entry_id} reference_file must be relative to the skill root")
    if ".." in posix_path.parts or ".." in windows_path.parts:
        _fail(f"{entry_id} reference_file must not contain parent-directory escapes")

    root = ROOT.resolve()
    reference_path = (root / Path(*posix_path.parts)).resolve()
    try:
        reference_path.relative_to(root)
    except ValueError:
        _fail(f"{entry_id} reference_file resolves outside the skill root")
    return reference_path


def validate() -> None:
    payload = load_json(INDEX_PATH)

    if payload.get("schema_version") != 1:
        _fail("schema_version must be 1")

    entries = payload.get("entries")
    if not isinstance(entries, list) or not entries:
        _fail("entries must be a non-empty list")

    seen_ids: set[str] = set()
    for entry in entries:
        if not isinstance(entry, dict):
            _fail("each entry must be an object")
        missing = REQUIRED_ENTRY_FIELDS - set(entry)
        extra = set(entry) - REQUIRED_ENTRY_FIELDS
        if missing or extra:
            _fail(f"{entry.get('id', '<unknown>')} fields mismatch; missing={sorted(missing)} extra={sorted(extra)}")

        entry_id = _require_string(entry, "id")
        if not re.fullmatch(r"[a-z0-9]+(?:-[a-z0-9]+)*", entry_id):
            _fail(f"{entry_id!r} must be lowercase kebab-case")
        if entry_id in seen_ids:
            _fail(f"duplicate id {entry_id!r}")
        seen_ids.add(entry_id)

        for field in ["make", "model", "model_years", "source_family", "reference_file", "source_status", "safe_pointer"]:
            _require_string(entry, field)
        for field in ["components", "methods", "lookup_terms"]:
            _require_string_list(entry, field)

        if entry["source_status"] not in ALLOWED_SOURCE_STATUS:
            _fail(f"{entry_id} has unsupported source_status {entry['source_status']!r}")
        if entry.get("verification_required") is not True:
            _fail(f"{entry_id} must require current-source verification")

        reference_path = _resolve_reference_path(entry_id, entry["reference_file"])
        if not reference_path.is_file():
            _fail(f"{entry_id} reference_file does not exist: {entry['reference_file']}")

        _check_no_proprietary_detail(entry)

    print(f"method-index valid: {len(entries)} entries")


if __name__ == "__main__":
    validate()
