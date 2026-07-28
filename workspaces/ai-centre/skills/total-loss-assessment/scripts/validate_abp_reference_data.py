#!/usr/bin/env python3
"""Validate the skill-local ABP structured reference data."""

from __future__ import annotations

from pathlib import Path
from typing import Any

from validate_cli import load_json


DEFAULT_DATA = Path(__file__).resolve().parents[1] / "references" / "abp-reference-data.2026.json"
VALID_TYPES = {"specialist_fixed", "specialist_wu"}
POSITIVE_RATE_KEYS = ["standard_per_hour", "prestige_per_hour", "manufacturer_approval_uplift_per_hour"]
MARQUE_LIST_KEYS = ["standard_marques", "prestige_marques", "above_prestige_if_engineer_specifies"]


def _is_positive_number(value: Any) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool) and value > 0


def _is_non_empty_string_list(value: Any) -> bool:
    return isinstance(value, list) and bool(value) and all(isinstance(item, str) and item.strip() for item in value)


def load(path: Path = DEFAULT_DATA) -> dict[str, Any]:
    data = load_json(path)
    if not isinstance(data, dict):
        raise ValueError("ABP data must be a JSON object")
    return data


def validate(data: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    if data.get("schema_version") != 1:
        errors.append("schema_version must be 1")
    rates = data.get("labour_rates", {})
    if not isinstance(rates, dict):
        errors.append("labour_rates must be an object")
        rates = {}
    for key in POSITIVE_RATE_KEYS:
        if not _is_positive_number(rates.get(key)):
            errors.append(f"labour_rates.{key} must be a positive number")
    for key in MARQUE_LIST_KEYS:
        if not _is_non_empty_string_list(rates.get(key)):
            errors.append(f"labour_rates.{key} must be a non-empty list of strings")

    materials = data.get("materials", {})
    if not isinstance(materials, dict):
        errors.append("materials must be an object")
        materials = {}
    if materials.get("sundry_parts_pct") != 3.5:
        errors.append("materials.sundry_parts_pct must be 3.5 percentage points")
    for key in ["sundry_paint_fixed", "pre_sundry_fixed"]:
        if not _is_positive_number(materials.get(key)):
            errors.append(f"materials.{key} must be a positive number")

    for section in ["always_include_extras", "conditional_extras"]:
        items = data.get(section)
        if not isinstance(items, list) or not items:
            errors.append(f"{section} must be a non-empty list")
            continue
        for index, item in enumerate(items):
            if not isinstance(item, dict):
                errors.append(f"{section}[{index}] must be an object")
                continue
            if item.get("type") not in VALID_TYPES:
                errors.append(f"{section}[{index}].type must be one of {sorted(VALID_TYPES)}")
            if not item.get("description"):
                errors.append(f"{section}[{index}].description is required")
            if not _is_positive_number(item.get("value")):
                errors.append(f"{section}[{index}].value must be a positive number")
    return errors


def main() -> int:
    errors = validate(load())
    if errors:
        for error in errors:
            print(f"error: {error}")
        return 1
    print("ABP reference data OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
