#!/usr/bin/env python3
"""Evaluate a salvage-category input against the skill's versioned rule table."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any


SCRIPT_DIR = Path(__file__).resolve().parent
SKILL_ROOT = SCRIPT_DIR.parent
DEFAULT_TABLE = SKILL_ROOT / "references" / "salvage-decision-table.v1.json"


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as fh:
        data = json.load(fh)
    if not isinstance(data, dict):
        raise ValueError(f"{path} must contain a JSON object")
    return data


def _bool_or_none(value: Any) -> bool | None:
    if value is None or isinstance(value, bool):
        return value
    if isinstance(value, str):
        clean = value.strip().lower()
        if clean in {"true", "yes", "y", "1"}:
            return True
        if clean in {"false", "no", "n", "0"}:
            return False
    raise ValueError(f"expected boolean/null value, got {value!r}")


def _present(value: Any) -> bool:
    return isinstance(value, str) and bool(value.strip())


def _normalise_special_factor(value: Any) -> str:
    clean = str(value).strip().lower()
    clean = clean.replace("-", "_").replace(" ", "_")
    while "__" in clean:
        clean = clean.replace("__", "_")
    return clean


def _as_factor_list(value: Any) -> list[Any]:
    """Coerce special_factors into a list.

    A bare scalar such as "fire" must be treated as one factor, never iterated
    character by character (which would silently drop its review trigger and let
    the result be marked final). None means no factors.
    """
    if value is None:
        return []
    if isinstance(value, list):
        return value
    return [value]


def _matches(rule_when: dict[str, Any], data: dict[str, Any]) -> bool:
    for key, expected in rule_when.items():
        actual = data.get(key)
        if expected == "present":
            if not _present(actual):
                return False
        elif actual != expected:
            return False
    return True


def evaluate(data: dict[str, Any], table: dict[str, Any]) -> dict[str, Any]:
    normalised = {
        "entire_vehicle_destroy_required": _bool_or_none(data.get("entire_vehicle_destroy_required", False)),
        "bodyshell_crush_required": _bool_or_none(data.get("bodyshell_crush_required", False)),
        "repairable": _bool_or_none(data.get("repairable", False)),
        "structural_damage": _bool_or_none(data.get("structural_damage")),
        "non_structural_salvage_reason": str(data.get("non_structural_salvage_reason", "")).strip(),
        "special_factors": [_normalise_special_factor(v) for v in _as_factor_list(data.get("special_factors")) if str(v).strip()],
        "evidence_quality": str(data.get("evidence_quality", "unknown")).strip().lower() or "unknown",
    }
    if normalised["evidence_quality"] not in {"strong", "weak", "unknown"}:
        raise ValueError("evidence_quality must be strong, weak, or unknown")

    matched = None
    for rule in table.get("rules", []):
        if _matches(rule.get("when", {}), normalised):
            matched = rule
            break

    review_triggers: list[str] = []
    if normalised["structural_damage"] is None:
        review_triggers.append("structural_damage_unknown")
    if normalised["entire_vehicle_destroy_required"] or normalised["bodyshell_crush_required"]:
        review_triggers.append("cat_a_or_b_candidate")
    for factor in normalised["special_factors"]:
        if factor in set(table.get("review_triggers", [])):
            review_triggers.append(factor)
    if normalised["evidence_quality"] != "strong":
        review_triggers.append("weak_evidence")

    category = matched.get("category") if matched else None
    confidence = "final" if matched and not review_triggers else "provisional"
    if not matched:
        confidence = "unresolved"

    return {
        "schema_version": table.get("schema_version"),
        "category": category,
        "confidence": confidence,
        "matched_rule": matched.get("id") if matched else None,
        "reason": matched.get("reason") if matched else "No rule matched; more evidence is required before recommending a category.",
        "review_required": bool(review_triggers or (matched and matched.get("review_required"))),
        "review_triggers": sorted(set(review_triggers)),
        "normalised_inputs": normalised,
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Evaluate salvage category decision inputs")
    parser.add_argument("input_json", type=Path)
    parser.add_argument("--table", type=Path, default=DEFAULT_TABLE)
    args = parser.parse_args(argv)

    result = evaluate(load_json(args.input_json), load_json(args.table))
    print(json.dumps(result, indent=2))
    return 0 if result["category"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
