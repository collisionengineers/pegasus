#!/usr/bin/env python3
"""Build or verify the versioned principal-identification evidence corpus."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import os
import re
import tempfile
import zipfile
from collections import defaultdict
from email import policy
from email.parser import BytesParser
from pathlib import Path
from typing import Any
from xml.etree import ElementTree


SCHEMA_VERSION = 1
VERSION = "principal-identification-corpus-v1"
PACKAGE_RELATIVE_PATH = Path(
    "reference/workproviders-and-repairers/principal-identification-corpus.v1.json"
)

ACCEPTED_BASELINE = ("QDOS",)
APPROVED_DOMAIN_CANDIDATES = (
    "AX", "BLACK", "DFD", "FW", "KBS", "MP", "OAK", "PCH", "QCL", "RJS"
)
DOCUMENT_PROFILE_CANDIDATES = (
    "ACSP", "ALISON", "ALS", "AMS", "BC", "KERR", "KMR", "SBL", "SWAN", "TEN", "YML"
)
OTHER_ACTIVE = (
    "GG", "SS", "AVI", "HTU", "CASTLE", "WLS", "ALL", "AS", "WIL", "TP",
    "MOTORX", "STALLION", "TA", "RELAY", "RL", "ABRAHAMS", "MATT", "ASLS"
)
DORMANT = ("R1AM", "MBH", "ROZZII", "LEX", "CW", "ZENITH", "FRAZ", "BAKER", "LPS")
PRINCIPAL_CODES = frozenset(
    ACCEPTED_BASELINE
    + APPROVED_DOMAIN_CANDIDATES
    + DOCUMENT_PROFILE_CANDIDATES
    + OTHER_ACTIVE
    + DORMANT
)

CANONICAL_NAME_OVERRIDES = {
    "ACSP": "Accident Specialists",
    "ALL": "Alliance & Cooper",
    "BLACK": "Blackstone Legal",
    "DFD": "Davison Flynn Duke Solicitors",
    "FW": "Fairway Legal",
    "KBS": "Knightsbridge Solicitors",
    "OAK": "Oakwood Solicitors",
    "PCH": "Performance Car Hire / Parkhouse",
    "QCL": "QC Law",
    "R1AM": "R1AM",
    "RL": "Regent Law",
    "YML": "YM Law / Network HD UK",
}

PROFILE_TO_PRINCIPAL = {
    "ALISON": "ALISON",
    "ALS": "ALS",
    "AMS": "AMS",
    "AX": "AX",
    "BC": "BC",
    "BLACK": "BLACK",
    "DFD": "DFD",
    "FW (Garage)": "FW",
    "FW (Solicitor)": "FW",
    "HDUK": "YML",
    "KBS": "KBS",
    "KERR": "KERR",
    "KMR": "KMR",
    "MP (Branded)": "MP",
    "MP (Simple)": "MP",
    "OAK": "OAK",
    "PCH (Lawshield)": "PCH",
    "PCH (Performance)": "PCH",
    "QCL": "QCL",
    "QDOS": "QDOS",
    "RJS": "RJS",
    "SBL": "SBL",
    "SWAN": "SWAN",
    "TEN": "TEN",
    "ACSP": "ACSP",
}

PROFILE_TO_SUPPORTING_IDENTITY = {
    "CNX (Engineers)": "connexus",
    "EVA (Engineers)": "eva-report-issuer",
    "CDQ": "cdq-claim-form",
    "Tractable": "tractable",
}

PEGASUS_ALIAS_CODES = {
    "FRZ": "FRAZ",
    "GGP": "GG",
    "HDUK": "YML",
    "PHOUSE": "PCH",
    "ZEN": "ZENITH",
}

SUPPORTING_IDENTITIES = (
    {
        "id": "connexus",
        "canonicalName": "Connexus Vehicle Assessors",
        "roles": ["intermediary", "report-issuer"],
        "senderDomains": ["connexus.co.uk"],
        "relationships": [{"principalCode": "PCH", "relationship": "instruction-intermediary"}],
    },
    {
        "id": "ensurance-claims",
        "canonicalName": "Ensurance Claims",
        "roles": ["intermediary"],
        "senderDomains": ["ensurance-claims.co.uk"],
        "relationships": [{"principalCode": "PCH", "relationship": "instruction-intermediary"}],
    },
    {
        "id": "hackney-solutions",
        "canonicalName": "Hackney Solutions",
        "roles": ["intermediary"],
        "senderDomains": ["hackneysolutions.co.uk"],
        "relationships": [
            {"principalCode": "QCL", "relationship": "instruction-intermediary"},
            {"principalCode": "LEX", "relationship": "instruction-intermediary"},
        ],
    },
    {
        "id": "complex-reports",
        "canonicalName": "Complex Reports",
        "roles": ["intermediary"],
        "senderDomains": ["complexreports.com"],
        "relationships": [{"principalCode": "QCL", "relationship": "instruction-intermediary"}],
    },
    {
        "id": "accident-specialists-intermediary",
        "canonicalName": "Accident Specialists",
        "roles": ["intermediary", "repairer"],
        "senderDomains": ["accidentspecialist.co.uk"],
        "relationships": [{"principalCode": "RJS", "relationship": "instruction-intermediary"}],
        "note": "This relationship is distinct from ACSP acting as a direct principal.",
    },
    {
        "id": "claim-specialists",
        "canonicalName": "Claim Specialists",
        "roles": ["intermediary", "repairer"],
        "senderDomains": ["claimspecialists.co.uk"],
        "relationships": [
            {"principalCode": "RJS", "relationship": "instruction-intermediary"},
            {"principalCode": "SWAN", "relationship": "instruction-intermediary"},
        ],
    },
    {
        "id": "kabir",
        "canonicalName": "Kabir",
        "roles": ["intermediary"],
        "senderDomains": [],
        "relationships": [{"principalCode": "KBS", "relationship": "instruction-intermediary"}],
    },
    {
        "id": "expert-claims",
        "canonicalName": "Expert Claims",
        "roles": ["intermediary", "repairer"],
        "senderDomains": [],
        "relationships": [{"principalCode": "KBS", "relationship": "instruction-intermediary"}],
    },
    {
        "id": "apex-hire",
        "canonicalName": "Apex Hire",
        "roles": ["intermediary", "repairer"],
        "senderDomains": [],
        "relationships": [{"principalCode": "KBS", "relationship": "instruction-intermediary"}],
    },
    {
        "id": "sixways",
        "canonicalName": "Sixways",
        "roles": ["intermediary", "repairer"],
        "senderDomains": ["sixwaysclaims.co.uk"],
        "relationships": [{"principalCode": "FW", "relationship": "instruction-intermediary"}],
    },
    {
        "id": "veetec",
        "canonicalName": "Veetec Motor Group",
        "roles": ["intermediary", "repairer"],
        "senderDomains": [],
        "relationships": [{"principalCode": "FW", "relationship": "instruction-intermediary"}],
    },
    {
        "id": "paul-mandy-garages",
        "canonicalName": "Paul Mandy / instructed garages",
        "roles": ["intermediary", "repairer"],
        "senderDomains": [],
        "relationships": [{"principalCode": "OAK", "relationship": "instruction-intermediary"}],
    },
    {
        "id": "eva-report-issuer",
        "canonicalName": "Exclusive Vehicle Assessors",
        "roles": ["report-issuer"],
        "senderDomains": [],
        "relationships": [],
    },
    {
        "id": "cdq-claim-form",
        "canonicalName": "CDQ claim-form source",
        "roles": ["other"],
        "senderDomains": [],
        "relationships": [],
    },
    {
        "id": "tractable",
        "canonicalName": "Tractable",
        "roles": ["image-source", "report-issuer"],
        "senderDomains": [],
        "relationships": [],
    },
)

QDOS_CANDIDATES = (
    ("final-repair-account-or-final-audit", "Final repair account or final audit request"),
    ("report-chase", "Report chase"),
    ("post-inspection-repair-authorisation", "Post-inspection repair authorisation"),
    ("pre-accident-value-dispute", "Pre-accident value dispute"),
    ("repair-total-loss-category-amendment", "Repair, total-loss, or category amendment"),
    ("additional-images-estimates-or-updates", "Additional images, estimates, or updates"),
    ("third-party-insurer-comments-or-query", "Third-party insurer comments or query"),
    ("automatic-reply-or-reply-thread-exclusion", "Automatic reply and reply-thread exclusion"),
)

QDOS_VOLUME_RESULTS = {
    "processed": 138,
    "unreadable": 10,
    "routes": {"Accepted": 47, "NeedsSorting": 8, "NoMatch": 73},
    "acceptedRouteClassifications": {
        "new-instruction-received/audit": 3,
        "new-instruction-received/inspection": 2,
        "pre-instruction-emails/triage-request": 3,
        "Unclassified": 39,
    },
    "matchedPredicates": {
        "attachment.audit-report-notification": 3,
        "attachment.engineer-notification": 2,
        "body.triage-only-request": 3,
        "subject.reply-prefix": 29,
    },
    "claimTokenCoverage": {"withToken": 47, "acceptedRoutes": 47},
}

QDOS_ACCEPTED_CLASSIFICATION = (
    ("subject.automatic-reply", "subject", "Automatic reply:", "General/autoreply"),
    ("subject.reply-prefix", "subject", "RE: family", "reply-context-only"),
    ("body.triage-only-request", "sender-authored-body", "Triage Only Request", "pre-instruction-emails/triage-request"),
    ("subject.engineer-triage", "subject", "Engineer Triage", "pre-instruction-emails/triage-request"),
    ("attachment.audit-report-notification", "instruction-document", "AUDIT REPORT NOTIFICATION", "new-instruction-received/audit"),
    ("attachment.engineer-notification", "instruction-document", "ENGINEER NOTIFICATION", "new-instruction-received/inspection"),
)

QDOS_ASSOCIATION_KEYS = (
    "label-anchored durable claim-reference tail",
    "label-anchored client vehicle registration excluding TP-prefixed labels",
    "label-anchored client name",
    "label-anchored incident date",
)

QDOS_EXTRACTION_LABELS = (
    "Our Ref:", "Our Client:", "Our Client's Vehicle:", "Claimant's Vehicle:",
    "Registration:", "Date of Accident:", "Accident Date:", "Mileage:", "Speedo:"
)

TRACKED_PEGASUS_SOURCES = (
    ("initial-domain-observations", "reference/workproviders-and-repairers/initial.xlsx", "spreadsheet", "raw-bytes"),
    ("approved-provider-domain-package", "src/Pegasus.Infrastructure/Persistence/ReferenceData/provider-domains.v1.json", "reference-package", "raw-bytes"),
    ("provider-case-export", "reference/workproviders-and-repairers/providers.xlsx", "spreadsheet", "raw-bytes"),
    ("reviewed-provider-workbook", "reference/workproviders-and-repairers/providers-worked-on.xlsx", "spreadsheet", "raw-bytes"),
    ("operator-job-sheet", "reference/workproviders-and-repairers/backup_of_ce_job_sheet_260429.xlsm", "office-document", "raw-bytes"),
    ("email-address-export", "reference/workproviders-and-repairers/email_addresses.csv", "dataset", "normalized-lf"),
    ("eva-contact-export", "reference/workproviders-and-repairers/contacts/contactseva_combined.csv", "dataset", "normalized-lf"),
)

TRACKED_COLLISIONSPIKE_SOURCES = (
    ("collision-provider-corpus", "database/seeds/data/provider-corpus.csv", "dataset", "normalized-lf"),
    ("collision-provider-profiles", "services/engine/cedocumentmapper_v2/providers.json", "profile-catalog", "normalized-lf"),
    ("collision-email-manifest", "scripts/evaluation/email/manifest.json", "manifest", "normalized-lf"),
    ("collision-provider-detector", "services/engine/cedocumentmapper_v2/src/cedocumentmapper_v2/detection/detector.py", "source-code", "normalized-lf"),
    ("collision-attachment-typing", "services/engine/cedocumentmapper_v2/src/cedocumentmapper_v2/detection/attachment_typing.py", "source-code", "normalized-lf"),
)

MAIN_NS = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"
DOCUMENT_REL_NS = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"
PACKAGE_REL_NS = "http://schemas.openxmlformats.org/package/2006/relationships"
CELL_REFERENCE = re.compile(r"^([A-Z]+)([1-9][0-9]*)$")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository-root", required=True, type=Path)
    parser.add_argument("--collision-spike-root", required=True, type=Path)
    parser.add_argument("--corpus-root", required=True, type=Path)
    parser.add_argument("--package-path", type=Path)
    parser.add_argument("--verify", action="store_true")
    return parser.parse_args()


def normalized_text_bytes(path: Path) -> bytes:
    return path.read_bytes().replace(b"\r\n", b"\n").replace(b"\r", b"\n")


def sha256_file(path: Path, hash_mode: str = "raw-bytes") -> str:
    digest = hashlib.sha256()
    if hash_mode == "normalized-lf":
        data = normalized_text_bytes(path)
        digest.update(data)
        return digest.hexdigest()
    if hash_mode != "raw-bytes":
        raise ValueError(f"unsupported hash mode: {hash_mode}")
    with path.open("rb") as stream:
        while chunk := stream.read(1024 * 1024):
            digest.update(chunk)
    return digest.hexdigest()


def sha256_text(value: str) -> str:
    return hashlib.sha256(value.encode("utf-8")).hexdigest()


def canonical_json_bytes(value: Any) -> bytes:
    return (
        json.dumps(value, ensure_ascii=True, sort_keys=True, separators=(",", ":"), allow_nan=False)
        + "\n"
    ).encode("utf-8")


def clean(value: Any) -> str:
    return "" if value is None else str(value).strip()


def state(*, observed: bool, accepted: bool = False, active: bool = False) -> dict[str, bool]:
    return {
        "observed": observed,
        "operatorAccepted": accepted,
        "runtimeActive": active,
    }


def snapshot(
    source_id: str,
    repository: str,
    root: Path,
    relative_path: str,
    media_kind: str,
    record_count: int | None = None,
    hash_mode: str = "raw-bytes",
) -> dict[str, Any]:
    path = root / Path(relative_path)
    result: dict[str, Any] = {
        "id": source_id,
        "repository": repository,
        "relativePath": Path(relative_path).as_posix(),
        "mediaKind": media_kind,
        "sha256": sha256_file(path, hash_mode),
        "hashMode": hash_mode,
        "bytes": len(normalized_text_bytes(path)) if hash_mode == "normalized-lf" else path.stat().st_size,
    }
    if record_count is not None:
        result["recordCount"] = record_count
    return result


def read_xml(archive: zipfile.ZipFile, member: str) -> ElementTree.Element:
    data = archive.read(member)
    lowered = data.lower()
    if b"<!doctype" in lowered or b"<!entity" in lowered:
        raise ValueError(f"unsafe XML in {member}")
    return ElementTree.fromstring(data)


def xlsx_sheet_rows(path: Path, sheet_name: str) -> list[dict[str, str]]:
    with zipfile.ZipFile(path, "r") as archive:
        workbook = read_xml(archive, "xl/workbook.xml")
        sheet = next(
            (
                item
                for item in workbook.findall(f"{{{MAIN_NS}}}sheets/{{{MAIN_NS}}}sheet")
                if item.get("name") == sheet_name
            ),
            None,
        )
        if sheet is None:
            raise ValueError(f"sheet not found: {sheet_name}")
        relationship_id = sheet.get(f"{{{DOCUMENT_REL_NS}}}id")
        relationships = read_xml(archive, "xl/_rels/workbook.xml.rels")
        relationship = next(
            item
            for item in relationships.findall(f"{{{PACKAGE_REL_NS}}}Relationship")
            if item.get("Id") == relationship_id
        )
        target = relationship.get("Target") or ""
        worksheet_member = target.lstrip("/") if target.startswith("/") else f"xl/{target}"
        worksheet_member = os.path.normpath(worksheet_member).replace("\\", "/")
        worksheet = read_xml(archive, worksheet_member)

        strings: list[str] = []
        if "xl/sharedStrings.xml" in archive.namelist():
            shared = read_xml(archive, "xl/sharedStrings.xml")
            strings = [
                "".join(text.text or "" for text in item.iter(f"{{{MAIN_NS}}}t"))
                for item in shared.findall(f"{{{MAIN_NS}}}si")
            ]

        rows: list[dict[str, str]] = []
        for row in worksheet.findall(f".//{{{MAIN_NS}}}sheetData/{{{MAIN_NS}}}row"):
            values: dict[str, str] = {"_row": row.get("r") or ""}
            for cell in row.findall(f"{{{MAIN_NS}}}c"):
                match = CELL_REFERENCE.fullmatch(cell.get("r") or "")
                if match is None:
                    continue
                column = match.group(1)
                cell_type = cell.get("t")
                if cell_type == "inlineStr":
                    inline = cell.find(f"{{{MAIN_NS}}}is")
                    value = "" if inline is None else "".join(
                        text.text or "" for text in inline.iter(f"{{{MAIN_NS}}}t")
                    )
                else:
                    raw = cell.findtext(f"{{{MAIN_NS}}}v") or ""
                    value = strings[int(raw)] if cell_type == "s" and raw else raw
                values[column] = value.strip()
            rows.append(values)
        return rows


def csv_rows(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as stream:
        return [dict(row) for row in csv.DictReader(stream)]


def initial_domain_rows(repository_root: Path) -> list[dict[str, Any]]:
    path = repository_root / "reference/workproviders-and-repairers/initial.xlsx"
    result: list[dict[str, Any]] = []
    for row in xlsx_sheet_rows(path, "Sheet1"):
        code = clean(row.get("A"))
        observations = clean(row.get("E"))
        if not code and not observations:
            continue
        addresses = sorted({item.strip().lower() for item in observations.split(";") if item.strip()})
        domains = sorted({item.rsplit("@", 1)[1] for item in addresses if "@" in item})
        result.append(
            {
                "sourceRow": int(row["_row"]),
                "principalCode": code,
                "observedAddresses": addresses,
                "observedDomains": domains,
            }
        )
    return result


def provider_workbook_rows(repository_root: Path) -> list[dict[str, Any]]:
    path = repository_root / "reference/workproviders-and-repairers/providers-worked-on.xlsx"
    rows = xlsx_sheet_rows(path, "Providers")
    headers = {column: clean(value) for column, value in rows[0].items() if column != "_row"}
    result: list[dict[str, Any]] = []
    for row in rows[1:]:
        if not any(clean(value) for key, value in row.items() if key != "_row"):
            continue
        raw = {headers[column]: clean(value) for column, value in row.items() if column in headers}
        code = clean(row.get("A")).upper()
        disposition, targets = historical_disposition(code, clean(row.get("B")), clean(row.get("C")))
        result.append(
            {
                "sourceRow": int(row["_row"]),
                "raw": raw,
                "disposition": disposition,
                "principalCodes": targets,
            }
        )
    return result


def job_sheet_rows(repository_root: Path) -> list[dict[str, Any]]:
    path = repository_root / "reference/workproviders-and-repairers/backup_of_ce_job_sheet_260429.xlsm"
    rows = xlsx_sheet_rows(path, "Principals")
    columns = (
        ("B", "providerName"),
        ("C", "evaCode"),
        ("D", "boxCode"),
        ("E", "inbox"),
        ("F", "instructionFormat"),
        ("G", "dragIntoEva"),
        ("H", "sentMino"),
        ("I", "imageSource"),
        ("J", "inspectionModeOrAddress"),
        ("K", "sendingReport"),
    )
    result: list[dict[str, Any]] = []
    for row in rows:
        row_number = int(row["_row"])
        if row_number <= 2 or not any(clean(row.get(column)) for column, _ in columns):
            continue
        raw = {name: clean(row.get(column)) for column, name in columns}
        code = raw["evaCode"].upper().strip()
        disposition, targets = historical_disposition(code, raw["providerName"], "operator-job-sheet")
        result.append(
            {
                "sourceRow": row_number,
                "raw": raw,
                "disposition": disposition,
                "principalCodes": targets,
            }
        )
    return result


def historical_disposition(code: str, name: str, group: str) -> tuple[str, list[str]]:
    normalized = code.strip().upper()
    if normalized in PRINCIPAL_CODES:
        return "principal", [normalized]
    if normalized in PEGASUS_ALIAS_CODES:
        return "alias", [PEGASUS_ALIAS_CODES[normalized]]
    if normalized in {"CS", "SIX", "HACKNEY", "ACC SP", "APEX", "EXPERT", "VEE"}:
        return "supporting-identity", []
    upper_name = name.upper()
    name_aliases = {
        "GRAHAM COFFEY": "GG",
        "FRAZ": "FRAZ",
        "ZENITH LAWYERS": "ZENITH",
    }
    for marker, target in name_aliases.items():
        if marker in upper_name:
            return "alias", [target]
    if normalized in {"", "N/A", "CREATE FOR EACH", "CHECK INSTRUCTIONS", "R1AM/MOTORX"}:
        return "unresolved", []
    if group.upper() in {"OTHER", "REPAIRER"}:
        return "archived-noise", []
    return "unresolved", []


def collision_crosswalk(rows: list[dict[str, str]]) -> list[dict[str, Any]]:
    support_code_map = {
        "CNX": "connexus",
        "EVA": "eva-report-issuer",
        "CDQ": "cdq-claim-form",
        "TRACTABLE": "tractable",
        "CS": "claim-specialists",
        "SIX": "sixways",
    }
    result: list[dict[str, Any]] = []
    for index, row in enumerate(rows, start=2):
        code = clean(row.get("principal_code")).upper()
        action = clean(row.get("recommended_action"))
        if code in PRINCIPAL_CODES:
            disposition = "principal"
            targets = [code]
            support_id = None
        elif code in PEGASUS_ALIAS_CODES:
            disposition = "alias"
            targets = [PEGASUS_ALIAS_CODES[code]]
            support_id = None
        elif code in support_code_map:
            disposition = "supporting-identity"
            targets = []
            support_id = support_code_map[code]
        elif action.startswith("ARCHIVE") or action.startswith("EXCLUDE"):
            disposition = "archived-noise"
            targets = []
            support_id = None
        else:
            disposition = "unresolved"
            targets = []
            support_id = None
        item: dict[str, Any] = {
            "sourceRow": index,
            "raw": row,
            "disposition": disposition,
            "principalCodes": targets,
        }
        if support_id is not None:
            item["supportingIdentityId"] = support_id
        result.append(item)
    return result


def load_profiles(collision_root: Path) -> tuple[list[dict[str, Any]], dict[str, list[dict[str, Any]]]]:
    path = collision_root / "services/engine/cedocumentmapper_v2/providers.json"
    with path.open("r", encoding="utf-8") as stream:
        document = json.load(stream)
    profiles = document["providers"]
    by_principal: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for profile in profiles:
        name = profile["name"]
        principal = PROFILE_TO_PRINCIPAL.get(name)
        if principal is not None:
            by_principal[principal].append(profile)
    return profiles, by_principal


def observed_instruction_labels(profile: dict[str, Any]) -> list[str]:
    labels: list[str] = []
    for rule in (profile.get("field_rules") or {}).values():
        if clean(rule.get("method")) in {"manual_input", "fixed_position"}:
            continue
        for candidate in re.split(r"\|\||;|\n", clean(rule.get("config"))):
            value = candidate.strip()
            if (
                len(value) >= 3
                and re.search(r"[A-Za-z]", value)
                and value.casefold() not in {"of", "yes", "no"}
                and value not in labels
            ):
                labels.append(value)
    return labels


def profile_fingerprint(profile: dict[str, Any]) -> dict[str, Any]:
    name = profile["name"]
    if "detect" in profile:
        detect = profile.get("detect") or {}
        required = list(detect.get("required_phrases") or [])
        optional = list(detect.get("optional_phrases") or [])
        negative = list(detect.get("negative_phrases") or [])
    else:
        required = list(profile.get("detect_phrases") or [])
        optional = []
        negative = []
    document_role = (
        "report" if name in {"CNX (Engineers)", "EVA (Engineers)", "Tractable"}
        else "correspondence" if name == "CDQ"
        else "instruction"
    )
    if name in PROFILE_TO_PRINCIPAL:
        labels = observed_instruction_labels(profile)
        if name == "ACSP" and "Accident Specialists branding" not in required:
            required.insert(0, "Accident Specialists branding")
        required.extend(value for value in labels[:2] if value not in required)
        optional.extend(value for value in labels[2:8] if value not in optional)
        for issuer_marker in ("Connexus Vehicle Assessors", "Exclusive Vehicle Assessors"):
            if issuer_marker not in negative:
                negative.append(issuer_marker)
    return {
        "id": f"collision-profile-{slug(name)}",
        "documentRole": document_role,
        "requiredSignals": required,
        "optionalSignals": optional,
        "negativeSignals": negative,
        "sourceMechanism": "CollisionSpike profile retained without numerical confidence, priority, or winner selection",
        "criterionState": state(observed=True),
        "evidenceRefs": ["collision-provider-profiles"],
    }


def extraction_labels(profile: dict[str, Any]) -> list[dict[str, Any]]:
    labels: list[dict[str, Any]] = []
    for field_name, rule in sorted((profile.get("field_rules") or {}).items()):
        labels.append(
            {
                "field": field_name,
                "observedMethod": clean(rule.get("method")),
                "observedConfiguration": clean(rule.get("config")),
                "criterionState": state(observed=True),
                "evidenceRefs": ["collision-provider-profiles"],
            }
        )
    return labels


def slug(value: str) -> str:
    return re.sub(r"[^a-z0-9]+", "-", value.lower()).strip("-")


def qdos_accepted_rules() -> list[dict[str, Any]]:
    return [
        {
            "id": rule_id,
            "direction": "received",
            "sourceRole": source_role,
            "signal": signal,
            "taxonomyTarget": target,
            "criterionState": state(observed=True, accepted=True, active=True),
            "evidenceRefs": ["qdos-runtime-policy-v5"],
        }
        for rule_id, source_role, signal, target in QDOS_ACCEPTED_CLASSIFICATION
    ]


def qdos_candidate_rules() -> list[dict[str, Any]]:
    return [
        {
            "id": f"qdos-candidate-{rule_id}",
            "direction": "received",
            "label": label,
            "taxonomyTarget": None,
            "criterionState": state(observed=True),
            "evidenceRefs": ["qdos-local-email-evidence"],
            "reviewRequirement": "Operator labels must map genuine positive and confusable-negative examples to the shared taxonomy before policy changes.",
        }
        for rule_id, label in QDOS_CANDIDATES
    ]


def dossier(
    code: str,
    canonical_name: str,
    seed_rows: list[dict[str, str]],
    domain_rows: list[dict[str, Any]],
    job_rows: list[dict[str, Any]],
    profiles: list[dict[str, Any]],
) -> dict[str, Any]:
    lifecycle = "dormant" if code in DORMANT else "active"
    cohort = (
        "accepted-baseline" if code in ACCEPTED_BASELINE
        else "approved-domain-candidate" if code in APPROVED_DOMAIN_CANDIDATES
        else "collision-document-profile-candidate" if code in DOCUMENT_PROFILE_CANDIDATES
        else "other-active" if code in OTHER_ACTIVE
        else "dormant-review-only"
    )
    names = {canonical_name}
    for row in seed_rows:
        if clean(row.get("principal_code")).upper() == code and clean(row.get("resolved_name")):
            names.add(clean(row["resolved_name"]))
    for row in job_rows:
        if code in row["principalCodes"] and row["raw"]["providerName"]:
            names.add(row["raw"]["providerName"])

    direct_sender_identities: list[dict[str, Any]] = []
    intermediary_relationships: list[dict[str, Any]] = []
    for row in domain_rows:
        if row["principalCode"] != code:
            continue
        for domain in row["observedDomains"]:
            support = next(
                (
                    item
                    for item in SUPPORTING_IDENTITIES
                    if domain in item.get("senderDomains", [])
                    and any(rel["principalCode"] == code for rel in item["relationships"])
                ),
                None,
            )
            criterion = {
                "domain": domain,
                "addresses": sorted(
                    address for address in row["observedAddresses"] if address.endswith(f"@{domain}")
                ),
                "criterionState": state(
                    observed=True,
                    accepted=code == "QDOS",
                    active=code == "QDOS",
                ),
                "evidenceRefs": ["initial-domain-observations", "approved-provider-domain-package"],
            }
            if code == "QDOS":
                criterion["evidenceRefs"].append("qdos-route-policy-v4")
            if support is None:
                direct_sender_identities.append(criterion)
            else:
                intermediary_relationships.append(
                    {
                        "supportingIdentityId": support["id"],
                        "observedSenderDomain": domain,
                        "requiresUniqueInstructionFingerprint": True,
                        "criterionState": state(observed=True),
                        "evidenceRefs": [
                            "initial-domain-observations",
                            "approved-provider-domain-package",
                            "operator-job-sheet",
                        ],
                    }
                )

    for support in SUPPORTING_IDENTITIES:
        for relationship in support["relationships"]:
            if relationship["principalCode"] != code:
                continue
            if any(item["supportingIdentityId"] == support["id"] for item in intermediary_relationships):
                continue
            intermediary_relationships.append(
                {
                    "supportingIdentityId": support["id"],
                    "observedSenderDomain": None,
                    "requiresUniqueInstructionFingerprint": True,
                    "criterionState": state(observed=True),
                    "evidenceRefs": ["operator-job-sheet"],
                }
            )

    instruction_formats: list[dict[str, Any]] = []
    for row in job_rows:
        if code not in row["principalCodes"]:
            continue
        raw = row["raw"]
        if raw["instructionFormat"]:
            instruction_formats.append(
                {
                    "description": raw["instructionFormat"],
                    "channel": raw["inbox"] or None,
                    "criterionState": state(observed=True),
                    "evidenceRefs": [f"operator-job-sheet#row-{row['sourceRow']}"],
                }
            )

    fingerprints = [profile_fingerprint(profile) for profile in profiles]
    observed_extraction = [item for profile in profiles for item in extraction_labels(profile)]
    if code == "ACSP":
        fingerprints.append(
            {
                "id": "acsp-review-fingerprint",
                "documentRole": "instruction",
                "requiredSignals": ["Accident Specialists branding", "Claim Form"],
                "optionalSignals": ["Owner Details", "Driver Details", "Vehicle Details", "Accident Details", "Private & Confidential"],
                "negativeSignals": ["Connexus Vehicle Assessors", "Exclusive Vehicle Assessors"],
                "criterionState": state(observed=True),
                "evidenceRefs": ["collision-fixture-acsp-scan-01"],
            }
        )
    if code == "SBL":
        fingerprints.append(
            {
                "id": "sbl-review-fingerprint",
                "documentRole": "instruction",
                "requiredSignals": ["SMART branding", "URGENT NEW INSTRUCTION", "Instruction Details"],
                "optionalSignals": ["From: Smart Business Link", "Claim & Policyholder", "Registration", "Incident Circumstances"],
                "negativeSignals": ["Connexus Vehicle Assessors", "Exclusive Vehicle Assessors"],
                "criterionState": state(observed=True),
                "evidenceRefs": ["collision-fixture-sbl-01"],
            }
        )

    accepted_rules = qdos_accepted_rules() if code == "QDOS" else []
    candidate_rules = qdos_candidate_rules() if code == "QDOS" else []
    association_keys = [
        {
            "description": value,
            "criterionState": state(observed=True, accepted=True, active=True),
            "evidenceRefs": ["qdos-case-match-policy-v1"],
        }
        for value in QDOS_ASSOCIATION_KEYS
    ] if code == "QDOS" else []
    extraction = [
        {
            "field": "accepted-qdos-grammar",
            "observedMethod": "label-anchored",
            "observedConfiguration": value,
            "criterionState": state(observed=True, accepted=True, active=True),
            "evidenceRefs": ["qdos-extraction-policy-v7"],
        }
        for value in QDOS_EXTRACTION_LABELS
    ] if code == "QDOS" else observed_extraction

    evidence_refs = {
        "collision-provider-corpus",
        "reviewed-provider-workbook",
        "operator-job-sheet",
    }
    if direct_sender_identities:
        evidence_refs.add("initial-domain-observations")
    if profiles:
        evidence_refs.add("collision-provider-profiles")
    if code == "QDOS":
        evidence_refs.update(
            {
                "qdos-route-policy-v4",
                "qdos-runtime-policy-v5",
                "qdos-case-match-policy-v1",
                "qdos-extraction-policy-v7",
                "qdos-local-email-evidence",
                "qdos-policy-v5-volume-evaluation",
            }
        )

    gaps: list[str] = []
    if code != "QDOS":
        gaps.extend(
            [
                "No operator-accepted Pegasus route policy.",
                "Received classification predicates are not operator accepted.",
                "Sent classification predicates are not operator accepted.",
                "Association and extraction criteria require genuine positive and confusable-negative evaluation before activation.",
            ]
        )
    if not direct_sender_identities:
        gaps.append("No reviewed direct sender identity in the approved-domain snapshot.")
    if not fingerprints:
        gaps.append("No reviewed instruction-document fingerprint.")
    if lifecycle == "dormant":
        gaps.append("Dormant principal must be explicitly reactivated; every match remains review-only.")

    return {
        "code": code,
        "canonicalName": canonical_name,
        "namesAndAliases": sorted(names, key=lambda value: (value.casefold(), value)),
        "evaBoxAliases": sorted(
            {
                value
                for row in job_rows
                if code in row["principalCodes"]
                for value in (row["raw"]["evaCode"], row["raw"]["boxCode"])
                if value
            },
            key=lambda value: (value.casefold(), value),
        ),
        "cohort": cohort,
        "lifecycle": lifecycle,
        "policyState": "runtime-active" if code == "QDOS" else "review-only",
        "directionCoverage": {
            "received": "runtime-active" if code == "QDOS" else "unclassified",
            "sent": "unclassified",
        },
        "directSenderIdentities": sorted(direct_sender_identities, key=lambda item: item["domain"]),
        "intermediaryRelationships": sorted(
            intermediary_relationships, key=lambda item: item["supportingIdentityId"]
        ),
        "instructionFormats": instruction_formats,
        "documentFingerprints": fingerprints,
        "sharedTaxonomyPredicates": accepted_rules,
        "candidateTaxonomyPredicates": candidate_rules,
        "exclusions": (
            [
                "Report branding is issuer evidence and never principal identity.",
                "Quoted or nested message content is not sender-authored current content.",
                "Unknown, conflicting, multiple, or dormant candidates require manual review.",
            ]
        ),
        "caseTypes": (
            [
                {"classification": "new-instruction-received/inspection", "caseType": "Inspection"},
                {"classification": "new-instruction-received/audit", "caseType": "Audit"},
                {"classification": "inspection plus REPORT + AUDIT REPORT marker", "caseType": "InspectionAndAudit"},
            ]
            if code == "QDOS" else []
        ),
        "associationKeys": association_keys,
        "extractionLabels": extraction,
        "negativeControls": [
            "near-domain sender",
            "report issuer without instruction fingerprint",
            "intermediary without one unique principal fingerprint",
            "sender and instruction-document conflict",
            "multiple principal candidates",
            "quoted-thread-only signal",
            "nested-message-only signal",
        ],
        "evidenceRefs": sorted(evidence_refs),
        "gaps": gaps,
    }


def normalized_subject(value: str) -> str:
    current = value.strip()
    prefix = re.compile(r"^(?:(?:re|fw|fwd)\s*:\s*)+", re.IGNORECASE)
    return re.sub(r"\s+", " ", prefix.sub("", current)).casefold()


def stable_case_token(subject: str) -> str | None:
    patterns = (
        r"\b(?:[A-Z]{2,6}[\/_-]*)?\d{4,6}[\/_-]\d+\b",
        r"\b[A-Z]{2}\d{2}\s?[A-Z]{3}\b",
    )
    upper = subject.upper()
    for pattern in patterns:
        match = re.search(pattern, upper)
        if match:
            return re.sub(r"[^A-Z0-9]", "", match.group(0))
    return None


def decoded_header(message: Any, name: str) -> str:
    value = message.get(name)
    return "" if value is None else str(value).strip()


def attachment_role(file_name: str, content_type: str) -> str:
    lower = file_name.casefold()
    compact = re.sub(r"[^a-z0-9]+", "", lower)
    if content_type.startswith("image/"):
        return "image"
    if compact.startswith(("acsp", "alisonword", "oakrtf", "sbl")):
        return "instruction"
    if compact.startswith(("cdq", "qdostriage")):
        return "correspondence"
    if compact.startswith("tractable"):
        return "report"
    if any(marker in lower for marker in ("estimate", "audatex", "repair cost")):
        return "estimate"
    if (
        any(marker in lower for marker in ("engineer report", "engineers report", "assessment report"))
        or "engineerreport" in compact
    ):
        return "report"
    if any(marker in lower for marker in ("instruction", "claim form", "triage")):
        return "instruction"
    if content_type == "message/rfc822":
        return "correspondence"
    return "unknown"


def email_metadata(path: Path) -> dict[str, Any]:
    try:
        message = BytesParser(policy=policy.default).parsebytes(path.read_bytes())
        subject = decoded_header(message, "Subject")
        references = decoded_header(message, "References")
        in_reply_to = decoded_header(message, "In-Reply-To")
        message_id = decoded_header(message, "Message-ID")
        if references:
            basis_kind = "thread-root"
            basis_value = references.split()[0].strip("<>").casefold()
        elif in_reply_to:
            basis_kind = "thread-root"
            basis_value = in_reply_to.split()[0].strip("<>").casefold()
        elif token := stable_case_token(subject):
            basis_kind = "case-key"
            basis_value = token
        elif normalized_subject(subject):
            basis_kind = "normalized-subject"
            basis_value = normalized_subject(subject)
        else:
            basis_kind = "source-hash"
            basis_value = sha256_file(path)
        attachments: list[dict[str, Any]] = []
        for part in message.walk():
            file_name = part.get_filename()
            disposition = part.get_content_disposition()
            if not file_name and disposition != "attachment":
                continue
            payload = part.get_payload(decode=True) or b""
            display_name = str(file_name or "unnamed-attachment")
            attachments.append(
                {
                    "fileName": display_name,
                    "contentType": part.get_content_type(),
                    "bytes": len(payload),
                    "sha256": hashlib.sha256(payload).hexdigest(),
                    "roleCandidate": attachment_role(display_name, part.get_content_type()),
                    "criterionState": state(observed=True),
                }
            )
        group_hash = sha256_text(f"{basis_kind}:{basis_value}")
        bucket = int(group_hash, 16) % 10
        return {
            "headers": {
                "from": decoded_header(message, "From"),
                "to": decoded_header(message, "To"),
                "cc": decoded_header(message, "Cc"),
                "date": decoded_header(message, "Date"),
                "subject": subject,
                "messageId": message_id,
                "inReplyTo": in_reply_to,
                "references": references,
            },
            "attachments": attachments,
            "grouping": {
                "basis": basis_kind,
                "keySha256": group_hash,
                "bucket": bucket,
                "cohort": "holdout" if bucket in (0, 1) else "development",
            },
        }
    except Exception as error:
        source_hash = sha256_file(path)
        group_hash = sha256_text(f"source-hash:{source_hash}")
        bucket = int(group_hash, 16) % 10
        return {
            "parseIssue": type(error).__name__,
            "headers": {},
            "attachments": [],
            "grouping": {
                "basis": "source-hash",
                "keySha256": group_hash,
                "bucket": bucket,
                "cohort": "holdout" if bucket in (0, 1) else "development",
            },
        }


def evidence_files(collision_root: Path, corpus_root: Path) -> list[dict[str, Any]]:
    locations: list[tuple[str, Path, Path]] = []
    for directory_name in ("emailevals", "qdos-email-corpus"):
        directory = corpus_root / directory_name
        if not directory.is_dir():
            continue
        for path in directory.rglob("*"):
            if path.is_file() and path.suffix.casefold() in {".eml", ".msg", ".pdf", ".doc", ".docx", ".rtf"}:
                locations.append(("pegasus-local-corpus", corpus_root, path))

    collision_email_root = collision_root / "emailevals"
    if collision_email_root.is_dir():
        for path in collision_email_root.rglob("*.eml"):
            locations.append(("collisionspike", collision_root, path))
    fixture_root = collision_root / "services/engine/cedocumentmapper_v2/tests/fixtures/instructions"
    if fixture_root.is_dir():
        for path in fixture_root.iterdir():
            if path.is_file():
                locations.append(("collisionspike", collision_root, path))

    deduplicated: dict[str, dict[str, Any]] = {}
    for repository, root, path in locations:
        content_hash = sha256_file(path)
        location = {
            "repository": repository,
            "relativePath": path.relative_to(root).as_posix(),
        }
        if content_hash in deduplicated:
            deduplicated[content_hash]["sourceLocations"].append(location)
            continue
        suffix = path.suffix.casefold()
        media_kind = "email" if suffix in {".eml", ".msg"} else "pdf" if suffix == ".pdf" else "office-document"
        item: dict[str, Any] = {
            "id": f"evidence-{content_hash[:16]}",
            "sha256": content_hash,
            "bytes": path.stat().st_size,
            "mediaKind": media_kind,
            "sourceLocations": [location],
            "roleCandidate": attachment_role(path.name, "message/rfc822" if suffix == ".eml" else "application/octet-stream"),
            "criterionState": state(observed=True),
            "evidenceRefs": [f"sha256:{content_hash}"],
        }
        if suffix == ".eml":
            item.update(email_metadata(path))
            for attachment in item["attachments"]:
                attachment["evidenceRefs"] = [item["id"]]
        else:
            group_hash = sha256_text(f"source-hash:{content_hash}")
            bucket = int(group_hash, 16) % 10
            item["grouping"] = {
                "basis": "source-hash",
                "keySha256": group_hash,
                "bucket": bucket,
                "cohort": "holdout" if bucket in (0, 1) else "development",
            }
        deduplicated[content_hash] = item

    result = list(deduplicated.values())
    for item in result:
        item["sourceLocations"].sort(key=lambda value: (value["repository"], value["relativePath"]))
    return sorted(result, key=lambda item: item["sha256"])


def cohort_source(corpus_root: Path, relative_path: str) -> dict[str, Any]:
    root = corpus_root / relative_path
    files = sorted(
        (path for path in root.rglob("*.eml") if path.is_file()),
        key=lambda path: (
            path.relative_to(corpus_root).as_posix().casefold(),
            path.relative_to(corpus_root).as_posix(),
        ),
    )
    inventory = [
        f"{path.relative_to(corpus_root).as_posix()}\t{sha256_file(path)}"
        for path in files
    ]
    return {
        "repository": "pegasus-local-corpus",
        "relativePath": relative_path,
        "fileCount": len(files),
        "aggregateSha256": sha256_text("\n".join(inventory)),
    }


def supporting_identity_profiles(profiles: list[dict[str, Any]]) -> list[dict[str, Any]]:
    by_id = {item["id"]: dict(item) for item in SUPPORTING_IDENTITIES}
    for item in by_id.values():
        item["criterionState"] = state(observed=True)
        item["evidenceRefs"] = ["operator-job-sheet"]
        item["documentFingerprints"] = []
    for profile in profiles:
        support_id = PROFILE_TO_SUPPORTING_IDENTITY.get(profile["name"])
        if support_id is None:
            continue
        by_id[support_id]["documentFingerprints"].append(profile_fingerprint(profile))
        by_id[support_id]["evidenceRefs"] = sorted(
            set(by_id[support_id]["evidenceRefs"] + ["collision-provider-profiles"])
        )
    return [by_id[key] for key in sorted(by_id)]


def build_package(repository_root: Path, collision_root: Path, corpus_root: Path) -> dict[str, Any]:
    domain_rows = initial_domain_rows(repository_root)
    provider_rows = provider_workbook_rows(repository_root)
    operator_rows = job_sheet_rows(repository_root)
    collision_rows = csv_rows(collision_root / "database/seeds/data/provider-corpus.csv")
    profiles, profiles_by_principal = load_profiles(collision_root)

    seed_rows = [
        row for row in collision_rows
        if clean(row.get("recommended_action")).startswith("SEED active")
    ]
    seed_names = {
        clean(row["principal_code"]).upper(): clean(row["resolved_name"])
        for row in seed_rows
    }
    seed_names["PCH"] = CANONICAL_NAME_OVERRIDES["PCH"]
    principals = [
        dossier(
            code,
            CANONICAL_NAME_OVERRIDES.get(code, seed_names[code]),
            seed_rows,
            domain_rows,
            operator_rows,
            profiles_by_principal.get(code, []),
        )
        for code in sorted(PRINCIPAL_CODES)
    ]

    pegasus_counts = {
        "initial-domain-observations": len(domain_rows),
        "approved-provider-domain-package": len(
            json.loads(
                (
                    repository_root
                    / "src/Pegasus.Infrastructure/Persistence/ReferenceData/provider-domains.v1.json"
                ).read_text(encoding="utf-8")
            )["providers"]
        ),
        "provider-case-export": 17737,
        "reviewed-provider-workbook": len(provider_rows),
        "operator-job-sheet": len(operator_rows),
        "email-address-export": len(csv_rows(repository_root / "reference/workproviders-and-repairers/email_addresses.csv")),
        "eva-contact-export": len(csv_rows(repository_root / "reference/workproviders-and-repairers/contacts/contactseva_combined.csv")),
    }
    source_snapshots = [
        snapshot(
            source_id,
            "pegasus",
            repository_root,
            path,
            kind,
            pegasus_counts[source_id],
            hash_mode,
        )
        for source_id, path, kind, hash_mode in TRACKED_PEGASUS_SOURCES
    ]
    collision_counts = {
        "collision-provider-corpus": len(collision_rows),
        "collision-provider-profiles": len(profiles),
    }
    for source_id, path, kind, hash_mode in TRACKED_COLLISIONSPIKE_SOURCES:
        source_snapshots.append(
            snapshot(
                source_id,
                "collisionspike",
                collision_root,
                path,
                kind,
                collision_counts.get(source_id),
                hash_mode,
            )
        )
    fixture_root = collision_root / "services/engine/cedocumentmapper_v2/tests/fixtures/instructions"
    for fixture_path in sorted(fixture_root.iterdir(), key=lambda path: (path.name.casefold(), path.name)):
        if not fixture_path.is_file():
            continue
        source_snapshots.append(
            snapshot(
                f"collision-fixture-{slug(fixture_path.stem)}",
                "collisionspike",
                collision_root,
                fixture_path.relative_to(collision_root).as_posix(),
                (
                    "pdf" if fixture_path.suffix.casefold() == ".pdf"
                    else "email" if fixture_path.suffix.casefold() in {".eml", ".msg"}
                    else "office-document"
                ),
            )
        )

    source_snapshots.extend(
        snapshot(source_id, "pegasus", repository_root, path, "source-code", hash_mode="normalized-lf")
        for source_id, path in [
            ("qdos-route-policy-v4", "src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailRoutePolicy.cs"),
            ("qdos-runtime-policy-v5", "src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailClassificationPolicy.cs"),
            ("qdos-case-match-policy-v1", "src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosCaseMatchPolicy.cs"),
            ("qdos-extraction-policy-v7", "src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs"),
            ("shared-mail-taxonomy", "src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs"),
        ]
    )

    evidence = evidence_files(collision_root, corpus_root)
    qdos_evidence_count = sum(
        1
        for item in evidence
        if any(
            location["repository"] == "pegasus-local-corpus"
            and location["relativePath"].startswith("qdos-email-corpus/")
            for location in item["sourceLocations"]
        )
    )
    source_snapshots.append(
        {
            "id": "qdos-local-email-evidence",
            "repository": "pegasus-local-corpus",
            "relativePath": "qdos-email-corpus",
            "mediaKind": "evidence-cohort",
            "recordCount": qdos_evidence_count,
            "aggregateSha256": sha256_text(
                "\n".join(
                    item["sha256"]
                    for item in evidence
                    if any(
                        location["repository"] == "pegasus-local-corpus"
                        and location["relativePath"].startswith("qdos-email-corpus/")
                        for location in item["sourceLocations"]
                    )
                )
            ),
        }
    )
    qdos_volume_source = cohort_source(corpus_root, "emailevals")
    if qdos_volume_source["fileCount"] != QDOS_VOLUME_RESULTS["processed"]:
        raise ValueError(
            "QDOS volume cohort count drift: "
            f"expected {QDOS_VOLUME_RESULTS['processed']}, found {qdos_volume_source['fileCount']}"
        )
    source_snapshots.append(
        {
            "id": "qdos-policy-v5-volume-source",
            "mediaKind": "evidence-cohort",
            **qdos_volume_source,
            "recordCount": qdos_volume_source["fileCount"],
        }
    )

    return {
        "schemaVersion": SCHEMA_VERSION,
        "version": VERSION,
        "purpose": "Review evidence and criteria for fail-closed principal identification and shared email categorization.",
        "runtimeContract": {
            "loadedByRuntime": False,
            "policyOwner": "Pegasus.Core explicit versioned policies",
            "activationRule": "Only an operator-selected principal with accepted development and untouched-holdout evidence may receive runtime policy.",
            "collisionSpikeBoundary": "Read-only historical evidence; numerical confidence, thresholds, priorities, and single-winner selection are not imported.",
        },
        "criterionStateModel": ["observed", "operator-accepted", "runtime-active"],
        "identificationMethod": {
            "attachmentRoles": ["instruction", "report", "estimate", "image", "correspondence", "junk", "unknown"],
            "steps": [
                "Resolve transport, forwarded-original, and effective sender evidence.",
                "Match exact accepted direct domains or addresses.",
                "Match typed intermediary relationships.",
                "Apply required, optional, and negative fingerprints only to instruction-role documents.",
                "Accept one direct match unless instruction evidence conflicts.",
                "Accept an intermediary only when exactly one accepted relationship and one unique instruction fingerprint agree.",
                "Route unknown, dormant, conflicting, and multiple candidates to Unidentified/manual review.",
                "After identity, classify only sender-authored current content and accepted attachment evidence into the shared taxonomy.",
            ],
            "classificationOutcomes": {
                "onePredicate": "Classified",
                "multiplePredicates": "Ambiguous",
                "noPredicate": "Unclassified",
            },
        },
        "sharedTaxonomy": {
            "sourceRef": "shared-mail-taxonomy",
            "received": {
                "General": ["autoreply", "undeliverable", "acknowledgement", "general-chase", "case-summary"],
                "billing": ["payment-notification", "remittance", "invoice-request", "billing-query", "general-billing"],
                "new-instruction-received": ["audit", "diminution", "inspection", "new-client", "website-enquiry"],
                "non-client-related": [],
                "in-progress-cases": ["cancellation", "case-update", "client-chasing-for-update", "provider-chasing-for-update", "ongoing-correspondence"],
                "post-report-emails": ["query", "dispute", "amendment-request"],
                "pre-instruction-emails": ["triage-request", "pre-formal-instruction-request", "images-received"],
                "internal-cc": [],
            },
            "sent": ["Report sent", "case-rejected", "query-sent", "additional-image-request"],
        },
        "coverage": {
            "principalCount": len(principals),
            "activeCount": sum(item["lifecycle"] == "active" for item in principals),
            "dormantCount": sum(item["lifecycle"] == "dormant" for item in principals),
            "cohorts": {
                "acceptedBaseline": list(ACCEPTED_BASELINE),
                "approvedDomainCandidates": list(APPROVED_DOMAIN_CANDIDATES),
                "collisionDocumentProfileCandidates": list(DOCUMENT_PROFILE_CANDIDATES),
                "otherActive": list(OTHER_ACTIVE),
                "dormantReviewOnly": list(DORMANT),
            },
        },
        "sourceSnapshots": sorted(source_snapshots, key=lambda item: item["id"]),
        "supportingIdentities": supporting_identity_profiles(profiles),
        "evaluationSummaries": [
            {
                "id": "qdos-policy-v5-volume-evaluation",
                "principalCode": "QDOS",
                "reader": "MimeKitPdfPigOpenXmlIntakeSourceReader",
                "routePolicy": {"key": "qdos_mail_route", "version": 4},
                "classificationPolicy": {"key": "qdos_mail_classification", "version": 5},
                "sourceRef": "qdos-policy-v5-volume-source",
                "results": QDOS_VOLUME_RESULTS,
                "criterionState": state(observed=True),
                "evidenceRefs": ["qdos-policy-v5-volume-source"],
            }
        ],
        "historicalCrosswalks": {
            "pegasusProviderRows": provider_rows,
            "pegasusOperatorJobSheetRows": operator_rows,
            "collisionSpikeProviderRows": collision_crosswalk(collision_rows),
        },
        "principals": principals,
        "evidenceItems": evidence,
    }


def publish(path: Path, package_bytes: bytes, verify: bool) -> str:
    if verify:
        if not path.is_file():
            raise FileNotFoundError(f"package missing: {path}")
        if path.read_bytes() != package_bytes:
            raise ValueError(f"package drift: {path}")
        return "verified"
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(prefix=path.name, suffix=".tmp", dir=path.parent)
    temporary_path = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(package_bytes)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary_path, path)
    finally:
        temporary_path.unlink(missing_ok=True)
    return "written"


def main() -> int:
    args = parse_args()
    repository_root = args.repository_root.resolve()
    collision_root = args.collision_spike_root.resolve()
    corpus_root = args.corpus_root.resolve()
    package_path = (
        args.package_path.resolve()
        if args.package_path
        else repository_root / PACKAGE_RELATIVE_PATH
    )
    package = build_package(repository_root, collision_root, corpus_root)
    package_bytes = canonical_json_bytes(package)
    status = publish(package_path, package_bytes, args.verify)
    print(
        f"status={status} package={package_path.relative_to(repository_root).as_posix()} "
        f"principals={len(package['principals'])} evidence={len(package['evidenceItems'])} "
        f"sha256={hashlib.sha256(package_bytes).hexdigest()}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
