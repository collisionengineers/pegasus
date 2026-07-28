#!/usr/bin/env python3
"""Validate total-loss assessment payloads before audatex_gen_v4 renders them."""

from __future__ import annotations

from typing import Any

from validate_cli import run_validator_main


OPERATION_TYPES = {
    "new_part",
    "repair",
    "rnr",
    "check_labour",
    "paint_new",
    "paint_repair",
    "paint_blend",
    "paint_prep",
    "specialist_fixed",
    "specialist_wu",
}

WU_OPERATIONS = {
    "repair",
    "rnr",
    "check_labour",
    "paint_new",
    "paint_repair",
    "paint_blend",
    "paint_prep",
    "specialist_wu",
}

PRICE_OPERATIONS = {"new_part", "specialist_fixed"}
DESC_OPERATIONS = OPERATION_TYPES - {"paint_prep"}

TOP_LEVEL_REQUIRED = [
    "assessment_number",
    "version",
    "printed",
    "calc_date",
    "price_valid",
    "claim_ref",
    "inspection_date",
    "rates",
    "vehicle",
    "operations",
]

RATE_REQUIRED = [
    "labour_rate",
    "paint_rate",
    "sundry_parts_pct",
    "sundry_paint",
    "pre_sundry",
]

VEHICLE_REQUIRED = [
    "manufacturer",
    "model",
    "model_sheet",
    "engine",
    "reg",
    "vin",
    "reg_month",
    "reg_year",
    "colour",
    "paint_code",
    "build_date",
    "fuel",
    "specs",
]


RULES = [
    "Payload must be a JSON object with the top-level keys consumed by audatex_gen_v4.py.",
    "rates.labour_rate, paint_rate, sundry_parts_pct, sundry_paint, and pre_sundry are required non-negative numbers.",
    "vehicle must include the fields audatex_gen_v4.py indexes directly, including specs as a list of strings.",
    "operations must be a non-empty list using one of the generator routing types.",
    "repair/rnr/check_labour/paint_*/specialist_wu operations require positive wu.",
    "new_part and specialist_fixed operations require non-negative price.",
    "All operations except paint_prep require a non-empty desc.",
    "continuations, where present, must be a list of strings.",
    "The validator does not alter audatex_gen_v4.py or render a PDF; it gates bad input before rendering.",
]


def _is_number(value: Any) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool)


def _path(prefix: str, key: str) -> str:
    return f"{prefix}.{key}" if prefix else key


def _require_object(value: Any, path: str, errors: list[str]) -> dict[str, Any] | None:
    if not isinstance(value, dict):
        errors.append(f"{path} must be an object")
        return None
    return value


def _require_keys(obj: dict[str, Any], keys: list[str], prefix: str, errors: list[str]) -> None:
    for key in keys:
        if key not in obj:
            errors.append(f"{_path(prefix, key)} is required")


def _check_non_empty_string(obj: dict[str, Any], key: str, prefix: str, errors: list[str]) -> None:
    value = obj.get(key)
    if not isinstance(value, str) or not value.strip():
        errors.append(f"{_path(prefix, key)} must be a non-empty string")


def _check_non_negative_number(obj: dict[str, Any], key: str, prefix: str, errors: list[str]) -> None:
    value = obj.get(key)
    if not _is_number(value) or value < 0:
        errors.append(f"{_path(prefix, key)} must be a non-negative number")


def validate_payload(payload: dict[str, Any]) -> tuple[list[str], list[str]]:
    """Return (errors, warnings) for an assessment payload."""

    errors: list[str] = []
    warnings: list[str] = []

    if not isinstance(payload, dict):
        return ["payload must be a JSON object"], warnings

    _require_keys(payload, TOP_LEVEL_REQUIRED, "", errors)
    for key in [
        "assessment_number",
        "version",
        "printed",
        "calc_date",
        "price_valid",
        "inspection_date",
    ]:
        if key in payload:
            _check_non_empty_string(payload, key, "", errors)

    rates = _require_object(payload.get("rates"), "rates", errors)
    if rates is not None:
        _require_keys(rates, RATE_REQUIRED, "rates", errors)
        for key in RATE_REQUIRED + ["paint_material_base"]:
            if key in rates:
                _check_non_negative_number(rates, key, "rates", errors)
        if rates.get("sundry_parts_pct", 0) > 10:
            warnings.append("rates.sundry_parts_pct is above 10%; confirm this is intentional")
        if rates.get("labour_rate") != rates.get("paint_rate"):
            warnings.append("rates.labour_rate and rates.paint_rate differ; confirm the job really needs split rates")

    vehicle = _require_object(payload.get("vehicle"), "vehicle", errors)
    if vehicle is not None:
        _require_keys(vehicle, VEHICLE_REQUIRED, "vehicle", errors)
        for key in VEHICLE_REQUIRED:
            if key == "specs":
                continue
            if key in vehicle:
                _check_non_empty_string(vehicle, key, "vehicle", errors)
        specs = vehicle.get("specs")
        if not isinstance(specs, list):
            errors.append("vehicle.specs must be a list of strings")
        else:
            for idx, value in enumerate(specs):
                if not isinstance(value, str):
                    errors.append(f"vehicle.specs[{idx}] must be a string")

    operations = payload.get("operations")
    if not isinstance(operations, list):
        errors.append("operations must be a list")
    elif not operations:
        errors.append("operations must contain at least one operation")
    else:
        for idx, op in enumerate(operations):
            op_path = f"operations[{idx}]"
            if not isinstance(op, dict):
                errors.append(f"{op_path} must be an object")
                continue
            op_type = op.get("type")
            if op_type not in OPERATION_TYPES:
                errors.append(f"{op_path}.type must be one of {', '.join(sorted(OPERATION_TYPES))}")
                continue
            if op_type in DESC_OPERATIONS:
                _check_non_empty_string(op, "desc", op_path, errors)
            if op_type in WU_OPERATIONS:
                value = op.get("wu")
                if not _is_number(value) or value <= 0:
                    errors.append(f"{op_path}.wu must be a positive number for {op_type}")
            if op_type in PRICE_OPERATIONS:
                value = op.get("price")
                if not _is_number(value) or value < 0:
                    errors.append(f"{op_path}.price must be a non-negative number for {op_type}")
            if "continuations" in op:
                continuations = op["continuations"]
                if not isinstance(continuations, list):
                    errors.append(f"{op_path}.continuations must be a list of strings")
                else:
                    for cont_idx, value in enumerate(continuations):
                        if not isinstance(value, str):
                            errors.append(f"{op_path}.continuations[{cont_idx}] must be a string")
            if op_type == "specialist_wu":
                warnings.append(f"{op_path} uses specialist_wu; confirm it should price WU x labour_rate, not fixed price")
            if op_type == "new_part" and op.get("unpriced") and op.get("price", 0) > 0:
                warnings.append(f"{op_path} is marked unpriced but also has a positive price")

    return errors, warnings


def _list_rules() -> None:
    for idx, rule in enumerate(RULES, 1):
        print(f"{idx}. {rule}")


def main(argv: list[str] | None = None) -> int:
    return run_validator_main(
        validator=validate_payload,
        description="Validate an audatex_gen_v4 assessment payload",
        usage_suffix="<payload.json>",
        ok_message="assessment payload OK",
        list_flag="list-rules",
        list_callback=_list_rules,
        argv=argv,
    )


if __name__ == "__main__":
    raise SystemExit(main())
