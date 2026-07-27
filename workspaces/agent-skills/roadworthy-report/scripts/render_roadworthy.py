#!/usr/bin/env python3
"""Deterministic HS roadworthy report renderer.

The renderer owns field validation and DOCX token replacement only. It does not
invent the HS template. If the real template is absent, it fails closed.
"""

from __future__ import annotations

import argparse
import json
import re
import shutil
import sys
import tempfile
import zipfile
from dataclasses import dataclass
from datetime import date
from pathlib import Path
from typing import Any


SCRIPT_DIR = Path(__file__).resolve().parent
SKILL_ROOT = SCRIPT_DIR.parent
DEFAULT_TEMPLATE = SKILL_ROOT / "assets" / "HS_roadworthy_report_template.docx"

DATE_RE = re.compile(r"^\d{2}/\d{2}/\d{4}$")
REG_RE = re.compile(r"^[A-Z0-9]{2,8}$")

PLACEHOLDERS = {
    "word/header1.xml": {
        "{{our_ref}}": "our_ref",
        "{{your_ref}}": "your_ref",
        "{{header_date}}": "header_date",
    },
    "word/document.xml": {
        "{{accident_date}}": "accident_date",
        "{{registration}}": "registration",
        "{{instructions_received_date}}": "instructions_received_date",
        "{{make}}": "make",
        "{{model}}": "model",
        "{{vin}}": "vin",
        "{{status}}": "status",
        "{{cat_s}}": "cat_s",
        "{{passed_mot_taxi}}": "passed_mot_taxi",
        "{{legal_status}}": "legal_status",
        "{{damage_location}}": "damage_location",
    },
}


@dataclass
class ValidationResult:
    fields: dict[str, str]
    errors: list[str]
    warnings: list[str]


def _today() -> str:
    return date.today().strftime("%d/%m/%Y")


def _string(payload: dict[str, Any], key: str, default: str = "") -> str:
    value = payload.get(key, default)
    if value is None:
        return default
    return str(value).strip()


def _normalise_registration(value: str) -> str:
    return re.sub(r"\s+", "", value).upper()


def _cat_s(value: Any) -> str:
    if isinstance(value, bool):
        return "Yes" if value else "No"
    text = str(value or "").strip().lower()
    return "Yes" if text in {"yes", "y", "true", "1", "cat s", "category s"} else "No"


def validate_payload(payload: dict[str, Any]) -> ValidationResult:
    """Validate and normalise roadworthy input.

    Returns normalised fields plus errors/warnings. Fixed HS fields are forced
    to their controlled values rather than trusting model-supplied text.
    """

    errors: list[str] = []
    warnings: list[str] = []
    today = _today()

    registration = _normalise_registration(_string(payload, "registration"))
    if not registration:
        errors.append("registration is required")
    elif not REG_RE.fullmatch(registration):
        errors.append("registration must be 2-8 alphanumeric characters after removing spaces")

    make = _string(payload, "make")
    model = _string(payload, "model")
    if not make:
        errors.append("make is required")
    if not model:
        errors.append("model is required")

    header_date = _string(payload, "header_date") or _string(payload, "date") or today
    accident_date = _string(payload, "accident_date") or today
    instructions_received_date = _string(payload, "instructions_received_date") or today
    for key, value in {
        "header_date": header_date,
        "accident_date": accident_date,
        "instructions_received_date": instructions_received_date,
    }.items():
        if not DATE_RE.fullmatch(value):
            errors.append(f"{key} must be DD/MM/YYYY")

    fixed_values = {
        "status": "Repaired",
        "passed_mot_taxi": "TBC",
        "legal_status": "Roadworthy",
    }
    for key, fixed in fixed_values.items():
        supplied = _string(payload, key)
        if supplied and supplied != fixed:
            warnings.append(f"{key} supplied as {supplied!r}; renderer will use fixed value {fixed!r}")

    damage_location = _string(payload, "damage_location", "rear") or "rear"
    if len(damage_location) > 80:
        errors.append("damage_location is too long for the fixed body paragraph")

    fields = {
        "our_ref": registration,
        "your_ref": _string(payload, "your_ref"),
        "header_date": header_date,
        "accident_date": accident_date,
        "registration": registration,
        "instructions_received_date": instructions_received_date,
        "make": make.title() if make.isupper() else make,
        "model": model,
        "vin": _string(payload, "vin", "TBC") or "TBC",
        "status": fixed_values["status"],
        "cat_s": _cat_s(payload.get("cat_s")),
        "passed_mot_taxi": fixed_values["passed_mot_taxi"],
        "legal_status": fixed_values["legal_status"],
        "damage_location": damage_location,
    }
    return ValidationResult(fields=fields, errors=errors, warnings=warnings)


def _replace_xml_tokens(xml_path: Path, replacements: dict[str, str]) -> list[str]:
    raw = xml_path.read_text(encoding="utf-8")
    missing: list[str] = []
    updated = raw
    for token, value in replacements.items():
        if token not in updated:
            missing.append(token)
            continue
        updated = updated.replace(token, value)
    if not missing:
        xml_path.write_text(updated, encoding="utf-8")
    return missing


def render_docx(payload: dict[str, Any], output_dir: Path, template_path: Path) -> dict[str, Any]:
    result = validate_payload(payload)
    if result.errors:
        raise ValueError("validation failed:\n- " + "\n- ".join(result.errors))
    if not template_path.exists():
        raise FileNotFoundError(
            f"Missing HS roadworthy template: {template_path}. "
            "Add the real HS_roadworthy_report_template.docx before rendering; "
            "the renderer refuses to invent fixed HS wording."
        )

    output_dir.mkdir(parents=True, exist_ok=True)
    out_path = output_dir / f"HS_roadworthy_{result.fields['registration']}.docx"

    with tempfile.TemporaryDirectory(prefix="roadworthy_docx_") as tmp_name:
        tmp = Path(tmp_name)
        with zipfile.ZipFile(template_path, "r") as zf:
            zf.extractall(tmp)

        missing: list[str] = []
        for xml_rel, token_map in PLACEHOLDERS.items():
            xml_path = tmp / xml_rel
            if not xml_path.exists():
                missing.append(xml_rel)
                continue
            replacements = {
                token: result.fields[field_name]
                for token, field_name in token_map.items()
            }
            missing.extend(f"{xml_rel}: {token}" for token in _replace_xml_tokens(xml_path, replacements))
        if missing:
            raise ValueError(
                "template is missing required roadworthy placeholders:\n- "
                + "\n- ".join(missing)
            )

        with zipfile.ZipFile(out_path, "w", compression=zipfile.ZIP_DEFLATED) as zf:
            for path in sorted(tmp.rglob("*")):
                if path.is_file():
                    zf.write(path, path.relative_to(tmp).as_posix())

    return {
        "output_path": str(out_path),
        "fields": result.fields,
        "warnings": result.warnings,
    }


def load_payload(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as fh:
        data = json.load(fh)
    if not isinstance(data, dict):
        raise ValueError("input JSON must be an object")
    return data


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Render an HS roadworthy DOCX from a 14-field payload")
    parser.add_argument("input_json", type=Path)
    parser.add_argument("--template", type=Path, default=DEFAULT_TEMPLATE)
    parser.add_argument("--output-dir", type=Path, default=Path.cwd())
    parser.add_argument("--validate-only", action="store_true")
    args = parser.parse_args(argv)

    try:
        payload = load_payload(args.input_json)
        validation = validate_payload(payload)
        if validation.warnings:
            for warning in validation.warnings:
                print(f"warning: {warning}", file=sys.stderr)
        if validation.errors:
            for error in validation.errors:
                print(f"error: {error}", file=sys.stderr)
            return 1
        if args.validate_only:
            print(json.dumps({"ok": True, "fields": validation.fields, "warnings": validation.warnings}, indent=2))
            return 0
        rendered = render_docx(payload, args.output_dir, args.template)
    except Exception as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2

    print(json.dumps(rendered, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
