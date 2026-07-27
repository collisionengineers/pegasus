#!/usr/bin/env python3
"""Validate structured diminution rebuttal intake coverage."""

from __future__ import annotations

from typing import Any

from validate_cli import run_validator_main


ATTACK_LINE_IDS = set(range(1, 15))
STATUSES = {"include", "exclude", "needs_evidence"}
CE_ROLES = {
    "rebutting_third_party",
    "defending_ce_report",
    "solicitor_advice",
    "insurer_response",
    "part35_addendum",
}
OUTPUT_MODES = {
    "formal_rebuttal_report",
    "solicitor_facing_advice",
    "insurer_facing_response",
    "formal_addendum_report",
}


ATTACK_LINE_NAMES = {
    1: "Nature of the damage and repair",
    2: "Vehicle is not a diminution-sensitive variant",
    3: "No physical inspection",
    4: "Internal sourcing of underlying evidence",
    5: "Qualifications and standing of the author",
    6: "Stigma scale is not a recognised methodology",
    7: "The market multiplier",
    8: "Absence of supporting market evidence",
    9: "Internal inconsistency against ABI 20% benchmark",
    10: "Mischaracterisation of the repair",
    11: "Statement of Truth defects",
    12: "Floating-point / arithmetic artefacts",
    13: "Pre-existing condition",
    14: "Prior-history speculation and paint-depth burden shift",
}


def _non_empty_string(value: Any) -> bool:
    return isinstance(value, str) and bool(value.strip())


def validate_intake(payload: dict[str, Any]) -> tuple[list[str], list[str]]:
    errors: list[str] = []
    warnings: list[str] = []

    if not isinstance(payload, dict):
        return ["payload must be a JSON object"], warnings

    ce_role = payload.get("ce_role")
    if ce_role not in CE_ROLES:
        errors.append(f"ce_role must be one of {', '.join(sorted(CE_ROLES))}")

    output_mode = payload.get("output_mode")
    if output_mode not in OUTPUT_MODES:
        errors.append(f"output_mode must be one of {', '.join(sorted(OUTPUT_MODES))}")

    vehicle = payload.get("vehicle")
    if not isinstance(vehicle, dict):
        errors.append("vehicle must be an object")
    else:
        for key in ("registration", "make_model"):
            if not _non_empty_string(vehicle.get(key)):
                errors.append(f"vehicle.{key} must be a non-empty string")

    claimant_report = payload.get("claimant_report")
    if not isinstance(claimant_report, dict):
        errors.append("claimant_report must be an object")
    else:
        for key in ("assessor", "claimed_diminution", "formula_or_method"):
            if not _non_empty_string(claimant_report.get(key)):
                errors.append(f"claimant_report.{key} must be a non-empty string")

    evidence = payload.get("evidence")
    if not isinstance(evidence, dict):
        errors.append("evidence must be an object")

    assessments = payload.get("attack_line_assessments")
    seen: set[int] = set()
    included: set[int] = set()
    if not isinstance(assessments, list):
        errors.append("attack_line_assessments must be a list")
    else:
        for idx, item in enumerate(assessments):
            path = f"attack_line_assessments[{idx}]"
            if not isinstance(item, dict):
                errors.append(f"{path} must be an object")
                continue
            line_id = item.get("id")
            if not isinstance(line_id, int) or line_id not in ATTACK_LINE_IDS:
                errors.append(f"{path}.id must be an integer from 1 to 14")
                continue
            if line_id in seen:
                errors.append(f"{path}.id duplicates attack line {line_id}")
            seen.add(line_id)
            status = item.get("status")
            if status not in STATUSES:
                errors.append(f"{path}.status must be include, exclude, or needs_evidence")
            elif status == "include":
                included.add(line_id)
            if not _non_empty_string(item.get("rationale")):
                errors.append(f"{path}.rationale must be a non-empty string")
            refs = item.get("evidence_refs", [])
            if refs is not None and not isinstance(refs, list):
                errors.append(f"{path}.evidence_refs must be a list of strings")
            elif isinstance(refs, list):
                for ref_idx, ref in enumerate(refs):
                    if not isinstance(ref, str):
                        errors.append(f"{path}.evidence_refs[{ref_idx}] must be a string")

        missing = sorted(ATTACK_LINE_IDS - seen)
        if missing:
            errors.append("attack_line_assessments must cover every attack line; missing " + ", ".join(map(str, missing)))

    if ce_role == "rebutting_third_party" and 9 not in included:
        warnings.append("attack line 9 is normally included in every third-party formula rebuttal")
    if included and 3 in included and evidence and evidence.get("physical_inspection_evidence") is True:
        warnings.append("attack line 3 is included but intake says physical inspection evidence exists; check wording")

    return errors, warnings


def _list_attack_lines() -> None:
    for line_id in sorted(ATTACK_LINE_NAMES):
        print(f"{line_id}. {ATTACK_LINE_NAMES[line_id]}")


def main(argv: list[str] | None = None) -> int:
    return run_validator_main(
        validator=validate_intake,
        description="Validate a diminution rebuttal structured intake",
        usage_suffix="<intake.json>",
        ok_message="diminution intake OK",
        list_flag="list-attack-lines",
        list_callback=_list_attack_lines,
        argv=argv,
    )


if __name__ == "__main__":
    raise SystemExit(main())
