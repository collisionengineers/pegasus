#!/usr/bin/env python3
"""Build the deterministic, review-only provider reference-data package."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import os
import re
import shutil
import sys
import tempfile
import unicodedata
from collections import Counter, defaultdict
from dataclasses import dataclass
from datetime import date, datetime, time, timedelta
from pathlib import Path
from typing import Any, Iterable, Sequence

SCHEMA_VERSION = 1
GENERATOR_VERSION = "CollisionSpike.ProviderReferenceData/0.1.0-alpha.1"


@dataclass(frozen=True)
class SourceDefinition:
    path: str
    role: str
    expected_content_sha256: str | None = None


# These are source declarations and accepted evidence hashes. Provider and
# location values are read from the supplied workbooks, never embedded here.
SOURCE_SET: tuple[SourceDefinition, ...] = (
    SourceDefinition("providers.xlsx", "provider-master", "25f7e2c6893f741a743f5c22fdf619032dc63d6b7aa92d24b3f842cc04e40e5f"),
    SourceDefinition("providers-worked-on.xlsx", "provider-location-history", "555a3f3ba5b81ce54af491b22fd49724d49d77b01f5b3c0a0fa8b758a03b4a33"),
    SourceDefinition("backup_of_ce_job_sheet_260429.xlsm", "field-default-evidence", "a52b5df2a131c1b00866f478ebba20150070a3af25915acd8c05a41b2d0b983b"),
    SourceDefinition("contacts/providers.xlsx", "provider-master-copy", "25f7e2c6893f741a743f5c22fdf619032dc63d6b7aa92d24b3f842cc04e40e5f"),
    SourceDefinition("contacts/aALL.xls", "contact-evidence"),
    SourceDefinition("contacts/agent.xls", "contact-evidence"),
    SourceDefinition("contacts/broker.xls", "contact-evidence"),
    SourceDefinition("contacts/client.xls", "contact-evidence"),
    SourceDefinition("contacts/contactseva_combined.csv", "contact-evidence"),
    SourceDefinition("contacts/legal.xls", "contact-evidence"),
    SourceDefinition("contacts/other.xls", "contact-evidence"),
    SourceDefinition("contacts/private.xls", "contact-evidence"),
    SourceDefinition("contacts/REPAIRER.xls", "repairer-evidence"),
)

CONTACT_CANDIDATE_SOURCE_PATHS = frozenset(
    source.path for source in SOURCE_SET
    if source.path.startswith("contacts/") and source.path != "contacts/providers.xlsx"
)

# Assert every known accepted baseline before staging output. Organization
# candidate volume deliberately remains source-derived rather than hard-coded.
EXPECTED_COUNTS = {
    "sourceCases": 17_737,
    "unmappedCaseIds": 410,
    "providers": 88,
    "providerLocationRelationships": 1_638,
    "physicalRelationships": 1_555,
    "imageBasedAssessmentRelationships": 66,
    "notSuppliedRelationships": 17,
    "physicalMissingPostcodeRelationships": 74,
    "uniqueNormalizedLocationCandidates": 649,
    "activeOrganizations": 88,
}

EXIT_CODES = {
    "missing-input": 20,
    "office-lock": 21,
    "hash-drift": 22,
    "unreadable-workbook": 23,
    "count-drift": 24,
    "output-collision": 25,
    "dependency": 26,
}


class AuthoringError(RuntimeError):
    def __init__(self, category: str, message: str) -> None:
        super().__init__(message)
        self.category = category
        self.exit_code = EXIT_CODES[category]


@dataclass(frozen=True)
class SourceArtifact:
    definition: SourceDefinition
    absolute_path: Path
    id: str
    path: str
    content_sha256: str

    def as_json(self) -> dict[str, str]:
        return {
            "id": self.id,
            "path": self.path,
            "contentSha256": self.content_sha256,
            "role": self.definition.role,
        }


@dataclass(frozen=True)
class ParsedSheet:
    name: str
    rows: tuple[tuple[Any, ...], ...]


@dataclass(frozen=True)
class ParsedArtifact:
    artifact: SourceArtifact
    sheets: tuple[ParsedSheet, ...]


@dataclass(frozen=True)
class SourceRecord:
    source_artifact_id: str
    source_sheet: str
    source_row: int
    raw_fields: dict[str, Any]


def canonical_json_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
        allow_nan=False,
    ).encode("utf-8")


def stable_id(prefix: str, identity: Any) -> str:
    return f"{prefix}:{hashlib.sha256(canonical_json_bytes(identity)).hexdigest()}"


def artifact_id(repository_relative_path: str) -> str:
    normalized = repository_relative_path.replace("\\", "/")
    return "artifact:" + hashlib.sha256(normalized.encode("utf-8")).hexdigest()


def normalized_repository_path(path: Path, repository_root: Path) -> str:
    try:
        return path.resolve().relative_to(repository_root.resolve()).as_posix()
    except ValueError as error:
        raise AuthoringError(
            "missing-input",
            "The workbook root must remain inside the repository so provenance is portable.",
        ) from error


def hash_file(path: Path) -> str:
    digest = hashlib.sha256()
    try:
        with path.open("rb") as stream:
            while chunk := stream.read(1024 * 1024):
                digest.update(chunk)
    except OSError as error:
        raise AuthoringError("missing-input", f"Cannot read declared input '{path.name}': {error}") from error
    return digest.hexdigest()


def ensure_no_office_locks(workbook_root: Path) -> None:
    try:
        locks = sorted(
            path for path in workbook_root.rglob("*")
            if path.is_file() and re.match(r"^~\$.*\.xls.*$", path.name, re.IGNORECASE)
        )
    except OSError as error:
        raise AuthoringError(
            "unreadable-workbook",
            f"Could not inspect the workbook root for Office locks: {error}",
        ) from error
    if locks:
        names = ", ".join(path.relative_to(workbook_root).as_posix() for path in locks)
        raise AuthoringError(
            "office-lock",
            "Close every Office workbook and remove its lock file before authoring. "
            f"Detected: {names}",
        )


def ensure_output_paths_are_safe(
    workbook_root: Path, staging_root: Path, package_path: Path, manifest_path: Path
) -> None:
    if package_path.resolve() == manifest_path.resolve():
        raise AuthoringError("output-collision", "The package and manifest paths must be distinct.")

    immutable_root = workbook_root.resolve()
    for label, path in (
        ("staging root", staging_root),
        ("package path", package_path),
        ("manifest path", manifest_path),
    ):
        try:
            path.resolve().relative_to(immutable_root)
        except ValueError:
            continue
        raise AuthoringError(
            "output-collision",
            f"The {label} must not be inside the immutable workbook root.",
        )

    for label, path in (("package path", package_path), ("manifest path", manifest_path)):
        if path.exists() and path.is_dir():
            raise AuthoringError(
                "output-collision",
                f"The {label} is an existing directory and cannot be atomically replaced.",
            )


def discover_and_hash_sources(workbook_root: Path, repository_root: Path) -> dict[str, SourceArtifact]:
    artifacts: dict[str, SourceArtifact] = {}
    for definition in SOURCE_SET:
        absolute_path = workbook_root / definition.path
        if not absolute_path.is_file():
            raise AuthoringError(
                "missing-input", f"Required Step 2 input is missing: '{definition.path}'."
            )
        content_sha256 = hash_file(absolute_path)
        if (
            definition.expected_content_sha256 is not None
            and content_sha256 != definition.expected_content_sha256
        ):
            raise AuthoringError(
                "hash-drift",
                f"Input hash drift for '{definition.path}': expected "
                f"{definition.expected_content_sha256}, observed {content_sha256}.",
            )
        portable_path = normalized_repository_path(absolute_path, repository_root)
        artifacts[definition.path] = SourceArtifact(
            definition=definition,
            absolute_path=absolute_path,
            id=artifact_id(portable_path),
            path=portable_path,
            content_sha256=content_sha256,
        )

    if artifacts["providers.xlsx"].content_sha256 != artifacts["contacts/providers.xlsx"].content_sha256:
        raise AuthoringError(
            "hash-drift",
            "The declared duplicate provider masters no longer have identical content hashes.",
        )
    return artifacts


def to_json_scalar(value: Any) -> Any:
    if value is None or isinstance(value, (str, bool, int)):
        return value
    if isinstance(value, float):
        return value if math.isfinite(value) else str(value)
    if isinstance(value, (datetime, date, time, timedelta)):
        return value.isoformat()
    return str(value)


def parse_workbook(artifact: SourceArtifact) -> ParsedArtifact:
    try:
        from python_calamine import CalamineWorkbook
    except ImportError as error:
        raise AuthoringError(
            "dependency",
            "python-calamine==0.8.2 is unavailable from the artifacts-local tool directory.",
        ) from error

    try:
        with CalamineWorkbook.from_path(str(artifact.absolute_path)) as workbook:
            sheets = tuple(
                ParsedSheet(
                    name=sheet_name,
                    rows=tuple(
                        tuple(to_json_scalar(value) for value in row)
                        for row in workbook.get_sheet_by_name(sheet_name).to_python(
                            skip_empty_area=False
                        )
                    ),
                )
                for sheet_name in workbook.sheet_names
            )
    except Exception as error:
        raise AuthoringError(
            "unreadable-workbook",
            f"Could not parse workbook '{artifact.definition.path}': {error}",
        ) from error
    return ParsedArtifact(artifact=artifact, sheets=sheets)


def parse_csv(artifact: SourceArtifact) -> ParsedArtifact:
    try:
        with artifact.absolute_path.open("r", encoding="utf-8-sig", newline="") as stream:
            rows = tuple(tuple(row) for row in csv.reader(stream))
    except (OSError, UnicodeError, csv.Error) as error:
        raise AuthoringError(
            "unreadable-workbook", f"Could not parse CSV '{artifact.definition.path}': {error}"
        ) from error
    return ParsedArtifact(artifact=artifact, sheets=(ParsedSheet(name="CSV", rows=rows),))


def parse_all_sources(artifacts: dict[str, SourceArtifact]) -> dict[str, ParsedArtifact]:
    parsed: dict[str, ParsedArtifact] = {}
    for definition in SOURCE_SET:
        artifact = artifacts[definition.path]
        parsed[definition.path] = (
            parse_csv(artifact)
            if artifact.absolute_path.suffix.casefold() == ".csv"
            else parse_workbook(artifact)
        )
    return parsed


def display_text(value: Any) -> str:
    return "" if value is None else str(value).strip()


def normalized_key(value: Any) -> str:
    return re.sub(
        r"[^a-z0-9]+", "", unicodedata.normalize("NFKC", display_text(value)).casefold()
    )


def normalized_text(value: Any) -> str | None:
    text = unicodedata.normalize("NFKC", display_text(value))
    return re.sub(r"\s+", " ", text).casefold() if text else None


def normalized_postcode(value: Any) -> str | None:
    text = normalized_text(value)
    return re.sub(r"\s+", "", text).upper() if text else None


def normalized_provider_code(value: Any) -> str | None:
    text = unicodedata.normalize("NFKC", display_text(value)).upper()
    compact = "".join(character for character in text if character.isalpha())
    return compact or None


def is_empty_row(row: Sequence[Any]) -> bool:
    return not any(display_text(value) for value in row)


def header_names(row: Sequence[Any]) -> tuple[str, ...]:
    seen: Counter[str] = Counter()
    headers: list[str] = []
    for index, value in enumerate(row, start=1):
        base = display_text(value) or f"column{index:02d}"
        seen[base] += 1
        headers.append(base if seen[base] == 1 else f"{base} ({seen[base]})")
    return tuple(headers)


def row_fields(headers: Sequence[str], row: Sequence[Any]) -> dict[str, Any]:
    return {header: row[index] if index < len(row) else None for index, header in enumerate(headers)}


def value_for(fields: dict[str, Any], *names: str) -> Any:
    indexed = {normalized_key(key): value for key, value in fields.items()}
    for name in names:
        value = indexed.get(normalized_key(name))
        if value is not None:
            return value
    return None


def find_sheet(parsed: ParsedArtifact, expected_name: str) -> ParsedSheet:
    expected = normalized_key(expected_name)
    for sheet in parsed.sheets:
        if normalized_key(sheet.name) == expected:
            return sheet
    raise AuthoringError(
        "unreadable-workbook",
        f"Workbook '{parsed.artifact.definition.path}' does not contain the required '{expected_name}' sheet.",
    )


def records_from_sheet(artifact: SourceArtifact, sheet: ParsedSheet, header_row: int) -> list[SourceRecord]:
    headers = header_names(sheet.rows[header_row])
    return [
        SourceRecord(
            source_artifact_id=artifact.id,
            source_sheet=sheet.name,
            source_row=row_index,
            raw_fields=row_fields(headers, row),
        )
        for row_index, row in enumerate(sheet.rows[header_row + 1 :], start=header_row + 2)
        if not is_empty_row(row)
    ]


def find_final_records(parsed: ParsedArtifact) -> list[SourceRecord]:
    sheet = find_sheet(parsed, "Final")
    required_headers = {"principalcode", "principalname", "inspectionlocation", "inspectiontype", "appearances"}
    for row_index, row in enumerate(sheet.rows):
        if required_headers.issubset({normalized_key(header) for header in header_names(row)}):
            records = records_from_sheet(parsed.artifact, sheet, row_index)
            if records:
                return records
    raise AuthoringError(
        "unreadable-workbook", "The Final sheet has no provider/location relationship header row."
    )


def raw_export_records(parsed: ParsedArtifact) -> list[SourceRecord]:
    sheet = find_sheet(parsed, "raw_export")
    header_row = next((index for index, row in enumerate(sheet.rows) if not is_empty_row(row)), None)
    if header_row is None:
        raise AuthoringError("unreadable-workbook", "The raw_export sheet has no header row.")
    return records_from_sheet(parsed.artifact, sheet, header_row)

def provider_codes_from_source(parsed: ParsedArtifact) -> list[str]:
    sheet = find_sheet(parsed, "Providers")
    header_row = next((index for index, row in enumerate(sheet.rows) if not is_empty_row(row)), None)
    if header_row is None:
        raise AuthoringError("unreadable-workbook", "The Providers sheet has no header row.")

    provider_codes: list[str] = []
    for record in records_from_sheet(parsed.artifact, sheet, header_row):
        code = normalized_provider_code(value_for(record.raw_fields, "Code", "Provider Code"))
        if code is None:
            code = normalized_provider_code(next(iter(record.raw_fields.values()), None))
        if code is None:
            raise AuthoringError(
                "unreadable-workbook",
                "A Providers row does not contain a usable provider code.",
            )
        provider_codes.append(code)
    return provider_codes


def normalized_location_fields(record: SourceRecord) -> tuple[str | None, dict[str, Any]]:
    fields = record.raw_fields
    source_type = normalized_text(value_for(fields, "Inspection Type"))
    if source_type == "image based":
        inspection_type = "Image Based Assessment"
    elif source_type == "not supplied":
        inspection_type = "Not supplied"
    elif source_type == "physical":
        inspection_type = "Physical"
    else:
        # Unrecognised and missing source labels remain review evidence rather
        # than being coerced into a selectable physical location.
        inspection_type = source_type

    normalized = {
        "principalCode": normalized_provider_code(value_for(fields, "Principal Code")),
        "inspectionLocation": normalized_text(value_for(fields, "Inspection Location")),
        "address": normalized_text(value_for(fields, "Address")),
        "cityArea": normalized_text(value_for(fields, "City / Area", "City/Area")),
        "county": normalized_text(value_for(fields, "County")),
        "postcode": normalized_postcode(value_for(fields, "Postcode")),
        "inspectionType": inspection_type,
        "appearances": value_for(fields, "Appearances"),
        "principalCases": value_for(fields, "Principal Cases"),
    }
    normalized["locationIdentity"] = {
        key: normalized[key]
        for key in ("inspectionLocation", "address", "cityArea", "county", "postcode", "inspectionType")
    }
    return inspection_type, normalized


def source_occurrence(record: SourceRecord, normalized_fields: dict[str, Any]) -> dict[str, Any]:
    return {
        "sourceArtifactId": record.source_artifact_id,
        "sourceSheet": record.source_sheet,
        "sourceRow": record.source_row,
        "rawFields": record.raw_fields,
        "normalizedFields": normalized_fields,
    }


def occurrence_sort_key(occurrence: dict[str, Any]) -> tuple[str, str, int, bytes]:
    return (
        occurrence["sourceArtifactId"],
        occurrence["sourceSheet"].casefold(),
        occurrence["sourceRow"],
        canonical_json_bytes(occurrence["rawFields"]),
    )


def build_organizations(
    final_records: Iterable[SourceRecord], final_artifact: SourceArtifact
) -> tuple[list[dict[str, Any]], dict[str, str]]:
    records_by_code: dict[str, list[SourceRecord]] = defaultdict(list)
    for record in final_records:
        code = normalized_provider_code(value_for(record.raw_fields, "Principal Code"))
        name = display_text(value_for(record.raw_fields, "Principal Name"))
        if code is None or not name:
            raise AuthoringError(
                "unreadable-workbook",
                "A Final provider/location row is missing its Principal Code or Principal Name.",
            )
        records_by_code[code].append(record)

    organizations: list[dict[str, Any]] = []
    organization_id_by_code: dict[str, str] = {}
    for code in sorted(records_by_code):
        records = sorted(records_by_code[code], key=lambda item: (item.source_sheet.casefold(), item.source_row))
        canonical_name = display_text(value_for(records[0].raw_fields, "Principal Name"))
        organization_id = stable_id(
            "organization", {"providerCode": code, "sourceArtifactId": final_artifact.id}
        )
        organization_id_by_code[code] = organization_id

        names: dict[str, list[SourceRecord]] = defaultdict(list)
        for record in records:
            names[display_text(value_for(record.raw_fields, "Principal Name"))].append(record)
        aliases = [
            {
                "value": name,
                "sourceOccurrences": [
                    {
                        "sourceArtifactId": item.source_artifact_id,
                        "sourceSheet": item.source_sheet,
                        "sourceRow": item.source_row,
                        "sourceColumn": "Principal Name",
                    }
                    for item in sorted(items, key=lambda item: (item.source_sheet.casefold(), item.source_row))
                ],
            }
            for name, items in sorted(names.items(), key=lambda item: (item[0].casefold(), item[0]))
            if name != canonical_name
        ]
        occurrences = [
            source_occurrence(
                record,
                {
                    "providerCode": code,
                    "providerName": normalized_text(value_for(record.raw_fields, "Principal Name")),
                },
            )
            for record in records
        ]
        organizations.append(
            {
                "id": organization_id,
                "canonicalName": canonical_name,
                "aliases": aliases,
                "roles": ["Principal"],
                "sourceOccurrences": sorted(occurrences, key=occurrence_sort_key),
            }
        )
    return organizations, organization_id_by_code


def build_provider_records(organization_ids: Iterable[str]) -> list[dict[str, Any]]:
    return [
        {
            "organizationId": organization_id,
            "defaults": {
                # Evidence is not promoted to a runtime default or selector.
                "inspectionLocationCandidateId": None,
                "provenance": [],
            },
        }
        for organization_id in sorted(organization_ids)
    ]


def build_location_candidates(
    final_records: Iterable[SourceRecord],
    organization_id_by_code: dict[str, str],
    final_artifact: SourceArtifact,
) -> list[dict[str, Any]]:
    candidates: list[dict[str, Any]] = []
    for record in final_records:
        code = normalized_provider_code(value_for(record.raw_fields, "Principal Code"))
        if code is None or code not in organization_id_by_code:
            raise AuthoringError(
                "unreadable-workbook", "A Final provider/location row cannot be linked to a declared provider."
            )
        location_kind, normalized_fields = normalized_location_fields(record)
        duplicate_group_id = stable_id(
            "location-duplicate-group",
            {
                "sourceArtifactId": final_artifact.id,
                "normalizedLocation": normalized_fields["locationIdentity"],
            },
        )
        identity = {
            "providerOrganizationId": organization_id_by_code[code],
            "normalizedLocation": normalized_fields["locationIdentity"],
            "provenance": {
                "sourceArtifactId": record.source_artifact_id,
                "sourceSheet": record.source_sheet,
                "sourceRow": record.source_row,
            },
        }
        candidates.append(
            {
                "id": stable_id("location-candidate", identity),
                "providerOrganizationId": organization_id_by_code[code],
                "sourceArtifactId": record.source_artifact_id,
                "sourceSheet": record.source_sheet,
                "sourceRow": record.source_row,
                "rawFields": record.raw_fields,
                "normalizedFields": normalized_fields,
                "reviewState": "Unreviewed",
                "duplicateGroupId": duplicate_group_id,
                "sourceOccurrences": [source_occurrence(record, normalized_fields)],
                "_locationKind": location_kind,
            }
        )
    # Final rows are candidate relationships, not automatic business merges.
    candidates.sort(key=lambda candidate: candidate["id"])
    return candidates


def build_organization_candidates(parsed: dict[str, ParsedArtifact]) -> list[dict[str, Any]]:
    candidates: list[dict[str, Any]] = []
    for source_path in sorted(CONTACT_CANDIDATE_SOURCE_PATHS):
        parsed_artifact = parsed[source_path]
        for sheet in parsed_artifact.sheets:
            header_row = next((index for index, row in enumerate(sheet.rows) if not is_empty_row(row)), None)
            if header_row is None:
                continue
            for record in records_from_sheet(parsed_artifact.artifact, sheet, header_row):
                normalized_fields = {
                    normalized_key(field_name): normalized_text(value)
                    for field_name, value in record.raw_fields.items()
                }
                normalized_identity = {
                    key: value for key, value in normalized_fields.items() if value is not None
                }
                if not normalized_identity:
                    continue
                provenance = {
                    "sourceArtifactId": record.source_artifact_id,
                    "sourceSheet": record.source_sheet,
                    "sourceRow": record.source_row,
                }
                candidates.append(
                    {
                        "id": stable_id(
                            "organization-candidate",
                            {"normalizedFields": normalized_identity, "provenance": provenance},
                        ),
                        **provenance,
                        "rawFields": record.raw_fields,
                        "normalizedFields": normalized_fields,
                        "reviewState": "Unreviewed",
                        "duplicateGroupId": stable_id(
                            "organization-duplicate-group", {"normalizedFields": normalized_identity}
                        ),
                    }
                )
    candidates.sort(key=lambda candidate: candidate["id"])
    return candidates


def count_unmapped_cases(raw_records: Iterable[SourceRecord], provider_codes: Iterable[str]) -> int:
    ordered_provider_ids = sorted(
        (normalized_provider_code(code) for code in provider_codes),
        key=lambda provider_id: -len(provider_id or ""),
    )
    unmapped = 0
    for record in raw_records:
        case_id = normalized_provider_code(value_for(record.raw_fields, "Case ID")) or ""
        if not any(provider_id and provider_id in case_id for provider_id in ordered_provider_ids):
            unmapped += 1
    return unmapped


def source_type_anomalies(location_candidates: Sequence[dict[str, Any]]) -> dict[str, int]:
    known_types = {"image based", "not supplied", "physical"}
    labels = Counter(
        display_text(value_for(candidate["rawFields"], "Inspection Type"))
        for candidate in location_candidates
        if normalized_text(value_for(candidate["rawFields"], "Inspection Type")) not in known_types
    )
    return dict(sorted((label, count) for label, count in labels.items() if label))


def observed_counts(
    organizations: Sequence[dict[str, Any]],
    providers: Sequence[dict[str, Any]],
    organization_candidates: Sequence[dict[str, Any]],
    location_candidates: Sequence[dict[str, Any]],
    raw_source_cases: int,
    unmapped_case_ids: int,
) -> dict[str, int]:
    by_type = Counter(candidate["_locationKind"] for candidate in location_candidates)
    physical_missing_postcode = sum(
        1
        for candidate in location_candidates
        if candidate["_locationKind"] == "Physical"
        and candidate["normalizedFields"]["postcode"] is None
    )
    return {
        "sourceCases": raw_source_cases,
        "unmappedCaseIds": unmapped_case_ids,
        "providers": len(providers),
        "providerLocationRelationships": len(location_candidates),
        "physicalRelationships": by_type["Physical"],
        "imageBasedAssessmentRelationships": by_type["Image Based Assessment"],
        "notSuppliedRelationships": by_type["Not supplied"],
        "physicalMissingPostcodeRelationships": physical_missing_postcode,
        "uniqueNormalizedLocationCandidates": len(
            {candidate["duplicateGroupId"] for candidate in location_candidates}
        ),
        "activeOrganizations": len(organizations),
        "organizationCandidates": len(organization_candidates),
    }


def assert_expected_counts(observed: dict[str, int]) -> None:
    mismatches = [
        f"{name}: expected {expected}, observed {observed.get(name)}"
        for name, expected in EXPECTED_COUNTS.items()
        if observed.get(name) != expected
    ]
    if mismatches:
        raise AuthoringError(
            "count-drift", "Accepted Step 2 evidence counts changed: " + "; ".join(mismatches)
        )


def remove_internal_fields(candidates: Sequence[dict[str, Any]]) -> list[dict[str, Any]]:
    return [{key: value for key, value in candidate.items() if not key.startswith("_")} for candidate in candidates]


def write_bytes(path: Path, data: bytes) -> None:
    with path.open("xb") as stream:
        stream.write(data)
        stream.flush()
        os.fsync(stream.fileno())


def copy_to_output_directory(staged_path: Path, output_path: Path) -> Path:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(prefix=f".{output_path.name}.", suffix=".tmp", dir=output_path.parent)
    os.close(descriptor)
    temporary_path = Path(temporary_name)
    try:
        with staged_path.open("rb") as source, temporary_path.open("wb") as target:
            shutil.copyfileobj(source, target, length=1024 * 1024)
            target.flush()
            os.fsync(target.fileno())
        return temporary_path
    except Exception:
        temporary_path.unlink(missing_ok=True)
        raise


def backup_existing_output(output_path: Path) -> Path | None:
    if not output_path.exists():
        return None
    descriptor, backup_name = tempfile.mkstemp(prefix=f".{output_path.name}.", suffix=".rollback", dir=output_path.parent)
    os.close(descriptor)
    backup_path = Path(backup_name)
    try:
        shutil.copyfile(output_path, backup_path)
        return backup_path
    except Exception:
        backup_path.unlink(missing_ok=True)
        raise


def restore_output(output_path: Path, backup_path: Path | None) -> None:
    if backup_path is None:
        output_path.unlink(missing_ok=True)
    else:
        os.replace(backup_path, output_path)


def atomic_publish(
    staging_root: Path,
    package_path: Path,
    manifest_path: Path,
    package_bytes: bytes,
    manifest_bytes: bytes,
) -> None:
    try:
        staging_root.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix="provider-reference-data-", dir=staging_root) as staging_directory:
            stage = Path(staging_directory)
            staged_package = stage / package_path.name
            staged_manifest = stage / manifest_path.name
            write_bytes(staged_package, package_bytes)
            write_bytes(staged_manifest, manifest_bytes)

            package_replacement = copy_to_output_directory(staged_package, package_path)
            manifest_replacement = copy_to_output_directory(staged_manifest, manifest_path)
            package_backup = backup_existing_output(package_path)
            manifest_backup = backup_existing_output(manifest_path)
            package_replaced = False
            manifest_replaced = False
            try:
                os.replace(package_replacement, package_path)
                package_replaced = True
                os.replace(manifest_replacement, manifest_path)
                manifest_replaced = True
            except OSError as error:
                try:
                    if package_replaced:
                        restore_output(package_path, package_backup)
                        package_backup = None
                    if manifest_replaced:
                        restore_output(manifest_path, manifest_backup)
                        manifest_backup = None
                except OSError as rollback_error:
                    raise AuthoringError(
                        "output-collision",
                        "Atomic output replacement failed and the prior output could not be restored: "
                        f"{rollback_error}",
                    ) from rollback_error
                raise AuthoringError("output-collision", f"Atomic output replacement failed: {error}") from error
            finally:
                package_replacement.unlink(missing_ok=True)
                manifest_replacement.unlink(missing_ok=True)
                if package_backup is not None:
                    package_backup.unlink(missing_ok=True)
                if manifest_backup is not None:
                    manifest_backup.unlink(missing_ok=True)
    except AuthoringError:
        raise
    except OSError as error:
        raise AuthoringError("output-collision", f"Could not stage or publish the reference package: {error}") from error


def build_package(
    repository_root: Path,
    workbook_root: Path,
    staging_root: Path,
    package_path: Path,
    manifest_path: Path,
) -> None:
    # Must remain before hashing/parsing inputs, creating staging paths, or
    # writing output. The PowerShell wrapper does this before dependency install.
    ensure_no_office_locks(workbook_root)
    ensure_output_paths_are_safe(workbook_root, staging_root, package_path, manifest_path)

    artifacts = discover_and_hash_sources(workbook_root, repository_root)
    parsed = parse_all_sources(artifacts)
    worked_on = parsed["providers-worked-on.xlsx"]
    final_records = find_final_records(worked_on)
    raw_records = raw_export_records(worked_on)
    provider_codes = provider_codes_from_source(worked_on)

    organizations, organization_id_by_code = build_organizations(final_records, artifacts["providers-worked-on.xlsx"])
    providers = build_provider_records(organization_id_by_code.values())
    locations_with_internal_fields = build_location_candidates(
        final_records, organization_id_by_code, artifacts["providers-worked-on.xlsx"]
    )
    organization_candidates = build_organization_candidates(parsed)
    counts = observed_counts(
        organizations,
        providers,
        organization_candidates,
        locations_with_internal_fields,
        raw_source_cases=len(raw_records),
        unmapped_case_ids=count_unmapped_cases(raw_records, provider_codes),
    )
    assert_expected_counts(counts)

    source_artifacts = [
        artifacts[definition.path].as_json()
        for definition in sorted(SOURCE_SET, key=lambda definition: definition.path)
    ]
    package = {
        "schemaVersion": SCHEMA_VERSION,
        "sourceArtifacts": source_artifacts,
        "organizations": sorted(organizations, key=lambda organization: organization["id"]),
        "providers": providers,
        "organizationCandidates": organization_candidates,
        "locationCandidates": remove_internal_fields(locations_with_internal_fields),
    }
    package_bytes = canonical_json_bytes(package)
    manifest = {
        "schemaVersion": SCHEMA_VERSION,
        "generatorVersion": GENERATOR_VERSION,
        "inputs": source_artifacts,
        "packageSha256": hashlib.sha256(package_bytes).hexdigest(),
        "counts": counts,
        "anomalies": {
            "unrecognisedInspectionTypeLabels": source_type_anomalies(locations_with_internal_fields)
        },
    }
    manifest_bytes = canonical_json_bytes(manifest)

    # No output bytes are emitted until every hash, parse, count, and invariant
    # check above has passed.
    atomic_publish(staging_root, package_path, manifest_path, package_bytes, manifest_bytes)


def parse_arguments(argv: Sequence[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build deterministic review-only provider reference data.")
    parser.add_argument("--repository-root", required=True, type=Path)
    parser.add_argument("--workbook-root", required=True, type=Path)
    parser.add_argument("--staging-root", required=True, type=Path)
    parser.add_argument("--package-path", required=True, type=Path)
    parser.add_argument("--manifest-path", required=True, type=Path)
    return parser.parse_args(argv)


def main(argv: Sequence[str]) -> int:
    args = parse_arguments(argv)
    repository_root = args.repository_root.resolve()
    workbook_root = args.workbook_root.resolve()
    staging_root = args.staging_root.resolve()
    package_path = args.package_path.resolve()
    manifest_path = args.manifest_path.resolve()
    if not repository_root.is_dir():
        raise AuthoringError("missing-input", "The repository root does not exist.")
    if not workbook_root.is_dir():
        raise AuthoringError("missing-input", "The workbook root does not exist.")
    build_package(repository_root, workbook_root, staging_root, package_path, manifest_path)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main(sys.argv[1:]))
    except AuthoringError as error:
        print(f"ERROR[{error.category}] {error}", file=sys.stderr)
        raise SystemExit(error.exit_code)
