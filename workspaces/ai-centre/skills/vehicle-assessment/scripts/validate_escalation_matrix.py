#!/usr/bin/env python3
"""Validate the skill-local post-impact escalation matrix."""

from __future__ import annotations

import sys
from pathlib import Path
from typing import Any

from validate_cli import load_json


DEFAULT_DATA = Path(__file__).resolve().parents[1] / "references" / "post-impact-escalation-matrix.v1.json"
ZONE_LIST_KEYS = ["typical_contact_damage", "systems_at_risk", "required_evidence", "escalations"]


def _is_non_empty_string(value: Any) -> bool:
    return isinstance(value, str) and bool(value.strip())


def _is_non_empty_string_list(value: Any) -> bool:
    return isinstance(value, list) and bool(value) and all(_is_non_empty_string(item) for item in value)


def load(path: Path = DEFAULT_DATA) -> dict[str, Any]:
    data = load_json(path)
    if not isinstance(data, dict):
        raise ValueError("escalation matrix must be a JSON object")
    return data


def validate(data: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    if data.get("schema_version") != 1:
        errors.append("schema_version must be 1")

    codes = data.get("escalation_codes")
    if not isinstance(codes, dict) or not codes:
        errors.append("escalation_codes must be a non-empty object")
        codes = {}
    else:
        for key, value in codes.items():
            if not _is_non_empty_string(value):
                errors.append(f"escalation_codes.{key} must be a non-empty string")

    triggers = data.get("global_triggers")
    if not isinstance(triggers, list) or not triggers:
        errors.append("global_triggers must be a non-empty list")
        triggers = []
    for index, trigger in enumerate(triggers):
        if not isinstance(trigger, dict):
            errors.append(f"global_triggers[{index}] must be an object")
            continue
        for key in ["trigger", "meaning"]:
            if not _is_non_empty_string(trigger.get(key)):
                errors.append(f"global_triggers[{index}].{key} must be a non-empty string")
        if not _is_non_empty_string_list(trigger.get("escalations")):
            errors.append(f"global_triggers[{index}].escalations must be a non-empty list of strings")
        else:
            for code in trigger["escalations"]:
                if code not in codes:
                    errors.append(f"global_triggers[{index}].escalations contains unknown code '{code}'")

    zones = data.get("impact_zones")
    if not isinstance(zones, list) or not zones:
        errors.append("impact_zones must be a non-empty list")
        zones = []
    seen_zone_ids: set[str] = set()
    for index, zone in enumerate(zones):
        if not isinstance(zone, dict):
            errors.append(f"impact_zones[{index}] must be an object")
            continue
        zone_id = zone.get("zone")
        if not _is_non_empty_string(zone_id):
            errors.append(f"impact_zones[{index}].zone must be a non-empty string")
        else:
            if zone_id in seen_zone_ids:
                errors.append(f"impact_zones[{index}].zone '{zone_id}' is duplicated")
            seen_zone_ids.add(zone_id)
            if zone_id != zone_id.lower() or " " in zone_id:
                errors.append(f"impact_zones[{index}].zone '{zone_id}' must be lower snake_case")
        if not _is_non_empty_string(zone.get("description")):
            errors.append(f"impact_zones[{index}].description must be a non-empty string")
        for key in ZONE_LIST_KEYS:
            if not _is_non_empty_string_list(zone.get(key)):
                errors.append(f"impact_zones[{index}].{key} must be a non-empty list of strings")
        for code in zone.get("escalations") or []:
            if isinstance(code, str) and code not in codes:
                errors.append(f"impact_zones[{index}].escalations contains unknown code '{code}'")
    return errors


def main(argv: list[str] | None = None) -> int:
    args = sys.argv[1:] if argv is None else argv
    path = Path(args[0]) if args else DEFAULT_DATA
    errors = validate(load(path))
    if errors:
        for error in errors:
            print(f"error: {error}")
        return 1
    print("escalation matrix OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
