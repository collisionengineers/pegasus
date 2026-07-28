#!/usr/bin/env python3
"""Build immutable cumulative provider-domain packages from one approved XLSX source."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import posixpath
import re
import sys
import tempfile
import zipfile
from pathlib import Path
from typing import Any
from xml.etree import ElementTree

SCHEMA_VERSION = 1
SHEET_NAME = "Sheet1"
BOOTSTRAP_SOURCE = Path("docs/reference/workproviders-and-repairers/initial.xlsx")
BOOTSTRAP_SOURCE_SHA256 = "e4bf89b0aeef3f1106bf34ed50f74dffc44c5ed748e0ad0811b66ee099b6cd29"
BOOTSTRAP_VERSION = "provider-domains-v1"
BOOTSTRAP_OUTPUT = Path(
    "src/Pegasus.Infrastructure/Persistence/ReferenceData/provider-domains.v1.json"
)

MAIN_NS = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"
DOCUMENT_REL_NS = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"
PACKAGE_REL_NS = "http://schemas.openxmlformats.org/package/2006/relationships"
WORKSHEET_REL_TYPE = "/worksheet"

CODE_PATTERN = re.compile(r"^[A-Z0-9]+(?:-[A-Z0-9]+)*$")
VERSION_PATTERN = re.compile(r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
CELL_REFERENCE_PATTERN = re.compile(r"^([A-Z]+)([1-9][0-9]*)$")

EXIT_CODES = {
    "missing-input": 20,
    "source-locked": 21,
    "hash-drift": 22,
    "source-contract": 23,
    "count-drift": 24,
    "output-collision": 25,
    "python-version": 26,
    "verification-missing": 27,
    "verification-drift": 28,
    "immutable-output": 29,
    "non-monotonic-source": 30,
    "previous-required": 31,
}


class AuthoringError(RuntimeError):
    def __init__(
        self,
        category: str,
        issue: str,
        *,
        source: str | None = None,
        row: int | None = None,
        column: str | None = None,
        code: str | None = None,
    ) -> None:
        fields = [f"issue={issue}"]
        if source is not None:
            fields.append(f"source={source}")
        fields.append(f"sheet={SHEET_NAME}")
        if row is not None:
            fields.append(f"row={row}")
        if column is not None:
            fields.append(f"column={column}")
        if code is not None and CODE_PATTERN.fullmatch(code) and len(code) <= 20:
            fields.append(f"code={code}")
        super().__init__(" ".join(fields))
        self.category = category
        self.exit_code = EXIT_CODES[category]


class DuplicateJsonMember(ValueError):
    pass


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository-root", required=True, type=Path)
    parser.add_argument("--source-path", required=True, type=Path)
    parser.add_argument("--version", required=True)
    parser.add_argument("--package-path", required=True, type=Path)
    parser.add_argument("--previous-package-path", type=Path)
    parser.add_argument("--staging-root", required=True, type=Path)
    parser.add_argument("--verify", action="store_true")
    return parser.parse_args()


def canonical_json_bytes(value: Any) -> bytes:
    return (
        json.dumps(
            value,
            ensure_ascii=True,
            sort_keys=True,
            separators=(",", ":"),
            allow_nan=False,
        )
        + "\n"
    ).encode("utf-8")


def hash_file(path: Path, source_name: str) -> str:
    digest = hashlib.sha256()
    try:
        with path.open("rb") as stream:
            while chunk := stream.read(1024 * 1024):
                digest.update(chunk)
    except OSError as error:
        raise AuthoringError(
            "missing-input", "source-unreadable", source=source_name
        ) from error
    return digest.hexdigest()


def is_within(path: Path, parent: Path) -> bool:
    try:
        path.resolve().relative_to(parent.resolve())
        return True
    except ValueError:
        return False


def repository_relative(path: Path, repository_root: Path) -> str:
    try:
        return path.resolve().relative_to(repository_root.resolve()).as_posix()
    except ValueError as error:
        raise AuthoringError("output-collision", "path-outside-repository") from error


def ensure_safe_paths(
    repository_root: Path,
    source_path: Path,
    package_path: Path,
    previous_package_path: Path | None,
    staging_root: Path,
) -> tuple[str, str]:
    source_name = repository_relative(source_path, repository_root)
    package_name = repository_relative(package_path, repository_root)
    repository_relative(staging_root, repository_root)
    if previous_package_path is not None:
        repository_relative(previous_package_path, repository_root)

    immutable_root = (repository_root / "docs/reference").resolve()
    if is_within(package_path, immutable_root) or is_within(staging_root, immutable_root):
        raise AuthoringError("output-collision", "output-under-reference", source=source_name)

    resolved_paths = [source_path.resolve(), package_path.resolve(), staging_root.resolve()]
    if previous_package_path is not None:
        resolved_paths.append(previous_package_path.resolve())
    if len(set(resolved_paths)) != len(resolved_paths):
        raise AuthoringError("output-collision", "path-overlap", source=source_name)
    if is_within(package_path, staging_root):
        raise AuthoringError("output-collision", "package-under-staging", source=source_name)
    if package_path.exists() and package_path.is_dir():
        raise AuthoringError("output-collision", "package-is-directory", source=source_name)

    return source_name, package_name


def read_xml_member(archive: zipfile.ZipFile, member: str, source_name: str) -> ElementTree.Element:
    try:
        data = archive.read(member)
    except KeyError as error:
        raise AuthoringError(
            "source-contract", "missing-xlsx-member", source=source_name
        ) from error
    lowered = data.lower()
    if b"<!doctype" in lowered or b"<!entity" in lowered:
        raise AuthoringError("source-contract", "unsafe-xml", source=source_name)
    try:
        return ElementTree.fromstring(data)
    except ElementTree.ParseError as error:
        raise AuthoringError("source-contract", "invalid-xml", source=source_name) from error


def worksheet_member(archive: zipfile.ZipFile, source_name: str) -> str:
    workbook = read_xml_member(archive, "xl/workbook.xml", source_name)
    sheet_matches = [
        sheet
        for sheet in workbook.findall(f"{{{MAIN_NS}}}sheets/{{{MAIN_NS}}}sheet")
        if sheet.get("name") == SHEET_NAME
    ]
    if len(sheet_matches) != 1:
        raise AuthoringError("source-contract", "sheet-not-unique", source=source_name)
    relationship_id = sheet_matches[0].get(f"{{{DOCUMENT_REL_NS}}}id")
    if not relationship_id:
        raise AuthoringError("source-contract", "sheet-relationship-missing", source=source_name)

    relationships = read_xml_member(archive, "xl/_rels/workbook.xml.rels", source_name)
    relationship_matches = [
        relationship
        for relationship in relationships.findall(f"{{{PACKAGE_REL_NS}}}Relationship")
        if relationship.get("Id") == relationship_id
    ]
    if len(relationship_matches) != 1:
        raise AuthoringError("source-contract", "sheet-relationship-invalid", source=source_name)
    relationship = relationship_matches[0]
    if relationship.get("TargetMode") == "External" or not (
        relationship.get("Type") or ""
    ).endswith(WORKSHEET_REL_TYPE):
        raise AuthoringError("source-contract", "sheet-relationship-invalid", source=source_name)

    target = relationship.get("Target")
    if not target:
        raise AuthoringError("source-contract", "sheet-target-missing", source=source_name)
    if target.startswith("/"):
        member = posixpath.normpath(target.lstrip("/"))
    else:
        member = posixpath.normpath(posixpath.join("xl", target))
    if member.startswith("../") or member not in archive.namelist():
        raise AuthoringError("source-contract", "sheet-target-invalid", source=source_name)
    return member


def shared_strings(archive: zipfile.ZipFile, source_name: str) -> tuple[str, ...]:
    member = "xl/sharedStrings.xml"
    if member not in archive.namelist():
        return ()
    root = read_xml_member(archive, member, source_name)
    values: list[str] = []
    for item in root.findall(f"{{{MAIN_NS}}}si"):
        values.append("".join(text.text or "" for text in item.iter(f"{{{MAIN_NS}}}t")))
    return tuple(values)


def literal_cell_value(
    cell: ElementTree.Element,
    shared: tuple[str, ...],
    source_name: str,
    row_number: int,
    column: str,
) -> str | None:
    if cell.find(f"{{{MAIN_NS}}}f") is not None:
        raise AuthoringError(
            "source-contract",
            "formula-not-allowed",
            source=source_name,
            row=row_number,
            column=column,
        )

    cell_type = cell.get("t")
    value_element = cell.find(f"{{{MAIN_NS}}}v")
    inline_element = cell.find(f"{{{MAIN_NS}}}is")
    if value_element is None and inline_element is None:
        return None

    if cell_type == "s":
        try:
            index = int(value_element.text or "") if value_element is not None else -1
            return shared[index]
        except (ValueError, IndexError) as error:
            raise AuthoringError(
                "source-contract",
                "shared-string-invalid",
                source=source_name,
                row=row_number,
                column=column,
            ) from error
    if cell_type == "inlineStr":
        if inline_element is None:
            return ""
        return "".join(
            text.text or "" for text in inline_element.iter(f"{{{MAIN_NS}}}t")
        )
    if cell_type == "str":
        return value_element.text or "" if value_element is not None else ""

    raise AuthoringError(
        "source-contract",
        "literal-string-required",
        source=source_name,
        row=row_number,
        column=column,
    )


def is_canonical_provider_code(value: str) -> bool:
    return len(value) <= 20 and CODE_PATTERN.fullmatch(value) is not None


def is_canonical_version(value: str) -> bool:
    return len(value) <= 64 and VERSION_PATTERN.fullmatch(value) is not None


def is_lowercase_sha256(value: Any) -> bool:
    return isinstance(value, str) and re.fullmatch(r"[0-9a-f]{64}", value) is not None


def is_canonical_source_path(value: Any) -> bool:
    if (
        not isinstance(value, str)
        or not value
        or len(value) > 512
        or value.startswith("/")
        or value.endswith("/")
        or "\\" in value
        or ":" in value
        or "//" in value
        or any(ord(character) < 32 or ord(character) == 127 for character in value)
    ):
        return False
    return all(segment not in ("", ".", "..") for segment in value.split("/"))


def is_canonical_domain_suffix(value: Any) -> bool:
    if not isinstance(value, str) or len(value) > 254 or not value.startswith("@"):
        return False
    domain = value[1:]
    if "@" in domain:
        return False
    labels = domain.split(".")
    if len(labels) < 2:
        return False
    for label in labels:
        if (
            not 1 <= len(label) <= 63
            or label.startswith("-")
            or label.endswith("-")
            or re.fullmatch(r"[a-z0-9-]+", label) is None
        ):
            return False
    return True


def extract_domain_suffix(token: str, source_name: str, row_number: int, code: str) -> str:
    if "@" not in token:
        raise AuthoringError(
            "source-contract",
            "email-observation-invalid",
            source=source_name,
            row=row_number,
            column="E",
            code=code,
        )
    local_part, domain = token.rsplit("@", 1)
    suffix = f"@{domain.lower()}"
    if not local_part or not is_canonical_domain_suffix(suffix):
        raise AuthoringError(
            "source-contract",
            "email-observation-invalid",
            source=source_name,
            row=row_number,
            column="E",
            code=code,
        )
    return suffix


def parse_source(
    source_path: Path, source_name: str, source_hash: str, version: str
) -> dict[str, Any]:
    try:
        with zipfile.ZipFile(source_path, "r") as archive:
            members = archive.namelist()
            if len(members) != len(set(members)):
                raise AuthoringError(
                    "source-contract", "duplicate-zip-member", source=source_name
                )
            sheet_member = worksheet_member(archive, source_name)
            shared = shared_strings(archive, source_name)
            worksheet = read_xml_member(archive, sheet_member, source_name)
    except AuthoringError:
        raise
    except (OSError, zipfile.BadZipFile) as error:
        raise AuthoringError(
            "source-contract", "invalid-xlsx", source=source_name
        ) from error

    providers: list[dict[str, Any]] = []
    provider_codes: set[str] = set()
    source_rows: set[int] = set()
    highest_contract_row = 0

    for row in worksheet.findall(f".//{{{MAIN_NS}}}sheetData/{{{MAIN_NS}}}row"):
        try:
            row_number = int(row.get("r") or "")
        except ValueError as error:
            raise AuthoringError(
                "source-contract", "row-number-invalid", source=source_name
            ) from error
        if row_number <= 0 or row_number in source_rows:
            raise AuthoringError(
                "source-contract", "row-number-invalid", source=source_name, row=row_number
            )
        source_rows.add(row_number)

        contract_cells: dict[str, ElementTree.Element] = {}
        for cell in row.findall(f"{{{MAIN_NS}}}c"):
            reference = cell.get("r") or ""
            match = CELL_REFERENCE_PATTERN.fullmatch(reference)
            if match is None or int(match.group(2)) != row_number:
                raise AuthoringError(
                    "source-contract", "cell-reference-invalid", source=source_name, row=row_number
                )
            column = match.group(1)
            if column not in ("A", "E"):
                continue
            if column in contract_cells:
                raise AuthoringError(
                    "source-contract",
                    "duplicate-contract-cell",
                    source=source_name,
                    row=row_number,
                    column=column,
                )
            contract_cells[column] = cell

        values: dict[str, str] = {}
        for column in ("A", "E"):
            cell = contract_cells.get(column)
            raw_value = (
                literal_cell_value(cell, shared, source_name, row_number, column)
                if cell is not None
                else None
            )
            values[column] = (raw_value or "").strip()

        if not values["A"] and not values["E"]:
            continue
        highest_contract_row = max(highest_contract_row, row_number)
        if not values["A"] or not values["E"]:
            raise AuthoringError(
                "source-contract", "partial-contract-row", source=source_name, row=row_number
            )

        code = values["A"]
        if not is_canonical_provider_code(code):
            raise AuthoringError(
                "source-contract",
                "provider-code-invalid",
                source=source_name,
                row=row_number,
                column="A",
            )
        if code in provider_codes:
            raise AuthoringError(
                "source-contract",
                "provider-code-duplicate",
                source=source_name,
                row=row_number,
                column="A",
                code=code,
            )
        provider_codes.add(code)

        suffixes: set[str] = set()
        for observation in values["E"].split(";"):
            token = observation.strip()
            if not token:
                raise AuthoringError(
                    "source-contract",
                    "email-observation-empty",
                    source=source_name,
                    row=row_number,
                    column="E",
                    code=code,
                )
            suffixes.add(extract_domain_suffix(token, source_name, row_number, code))
        if not suffixes:
            raise AuthoringError(
                "source-contract",
                "domain-suffix-missing",
                source=source_name,
                row=row_number,
                column="E",
                code=code,
            )

        providers.append(
            {
                "code": code,
                "sourceRow": row_number,
                "domainSuffixes": sorted(suffixes),
            }
        )

    if not providers or highest_contract_row <= 0:
        raise AuthoringError("count-drift", "empty-provider-package", source=source_name)

    providers.sort(key=lambda provider: provider["code"])
    return {
        "schemaVersion": SCHEMA_VERSION,
        "version": version,
        "source": {
            "path": source_name,
            "contentSha256": source_hash,
            "sheet": SHEET_NAME,
            "rowCount": highest_contract_row,
        },
        "providers": providers,
    }


def reject_duplicate_members(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise DuplicateJsonMember(key)
        result[key] = value
    return result


def require_exact_keys(value: Any, expected: set[str]) -> bool:
    return isinstance(value, dict) and set(value) == expected


def validate_package_object(package: Any, source_name: str) -> None:
    if not require_exact_keys(package, {"schemaVersion", "version", "source", "providers"}):
        raise AuthoringError("source-contract", "previous-package-contract", source=source_name)
    if package["schemaVersion"] != SCHEMA_VERSION or not is_canonical_version(package["version"]):
        raise AuthoringError("source-contract", "previous-package-identity", source=source_name)

    source = package["source"]
    if not require_exact_keys(source, {"path", "contentSha256", "sheet", "rowCount"}):
        raise AuthoringError("source-contract", "previous-source-contract", source=source_name)
    if (
        not is_canonical_source_path(source["path"])
        or not is_lowercase_sha256(source["contentSha256"])
        or not isinstance(source["sheet"], str)
        or not source["sheet"]
        or len(source["sheet"]) > 31
        or any(ord(character) < 32 or ord(character) == 127 for character in source["sheet"])
        or not isinstance(source["rowCount"], int)
        or isinstance(source["rowCount"], bool)
        or source["rowCount"] <= 0
    ):
        raise AuthoringError("source-contract", "previous-source-invalid", source=source_name)

    providers = package["providers"]
    if not isinstance(providers, list) or not providers:
        raise AuthoringError("source-contract", "previous-providers-invalid", source=source_name)
    codes: set[str] = set()
    rows: set[int] = set()
    for provider in providers:
        if not require_exact_keys(provider, {"code", "sourceRow", "domainSuffixes"}):
            raise AuthoringError("source-contract", "previous-provider-contract", source=source_name)
        code = provider["code"]
        row = provider["sourceRow"]
        suffixes = provider["domainSuffixes"]
        if not isinstance(code, str) or not is_canonical_provider_code(code) or code in codes:
            raise AuthoringError("source-contract", "previous-provider-code", source=source_name)
        if (
            not isinstance(row, int)
            or isinstance(row, bool)
            or row <= 0
            or row > source["rowCount"]
            or row in rows
        ):
            raise AuthoringError("source-contract", "previous-source-row", source=source_name, code=code)
        if (
            not isinstance(suffixes, list)
            or not suffixes
            or len(suffixes) != len(set(suffixes))
            or any(not is_canonical_domain_suffix(suffix) for suffix in suffixes)
        ):
            raise AuthoringError("source-contract", "previous-domain-suffix", source=source_name, code=code)
        codes.add(code)
        rows.add(row)


def load_previous_package(path: Path, repository_root: Path) -> tuple[dict[str, Any], bytes]:
    source_name = repository_relative(path, repository_root)
    try:
        data = path.read_bytes()
        package = json.loads(data, object_pairs_hook=reject_duplicate_members)
    except (OSError, UnicodeDecodeError, json.JSONDecodeError, DuplicateJsonMember) as error:
        raise AuthoringError(
            "source-contract", "previous-package-invalid-json", source=source_name
        ) from error
    validate_package_object(package, source_name)
    if canonical_json_bytes(package) != data:
        raise AuthoringError(
            "source-contract", "previous-package-not-canonical", source=source_name
        )
    return package, data


def provider_pairs(package: dict[str, Any]) -> set[tuple[str, str]]:
    return {
        (provider["code"], suffix)
        for provider in package["providers"]
        for suffix in provider["domainSuffixes"]
    }


def enforce_growth(
    package: dict[str, Any], previous: dict[str, Any], source_name: str
) -> None:
    if package["version"] == previous["version"]:
        raise AuthoringError("source-contract", "version-not-new", source=source_name)
    if (
        package["source"]["path"] == previous["source"]["path"]
        or package["source"]["contentSha256"] == previous["source"]["contentSha256"]
    ):
        raise AuthoringError("source-contract", "new-source-required", source=source_name)
    if not provider_pairs(previous).issubset(provider_pairs(package)):
        raise AuthoringError("non-monotonic-source", "prior-pair-removed", source=source_name)


def stage_package(staging_root: Path, package_bytes: bytes) -> Path:
    staging_root.mkdir(parents=True, exist_ok=True)
    descriptor, name = tempfile.mkstemp(prefix="provider-domains-", suffix=".json", dir=staging_root)
    path = Path(name)
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(package_bytes)
            stream.flush()
            os.fsync(stream.fileno())
    except Exception:
        path.unlink(missing_ok=True)
        raise
    return path


def publish(
    stage_path: Path,
    package_path: Path,
    package_bytes: bytes,
    verify: bool,
    source_name: str,
) -> str:
    if verify:
        if not package_path.is_file():
            raise AuthoringError("verification-missing", "package-missing", source=source_name)
        if package_path.read_bytes() != package_bytes:
            raise AuthoringError("verification-drift", "package-bytes-differ", source=source_name)
        return "verified"

    package_path.parent.mkdir(parents=True, exist_ok=True)
    if package_path.exists():
        if not package_path.is_file():
            raise AuthoringError("output-collision", "package-is-not-file", source=source_name)
        if package_path.read_bytes() == package_bytes:
            return "no-op"
        raise AuthoringError("immutable-output", "package-already-differs", source=source_name)

    try:
        os.link(stage_path, package_path)
        return "published"
    except FileExistsError:
        if package_path.is_file() and package_path.read_bytes() == package_bytes:
            return "no-op"
        raise AuthoringError("immutable-output", "package-already-differs", source=source_name)
    except OSError as error:
        raise AuthoringError("output-collision", "atomic-publication-failed", source=source_name) from error


def main() -> int:
    args = parse_args()
    if sys.version_info < (3, 11):
        raise AuthoringError("python-version", "python-3.11-required")

    repository_root = args.repository_root.resolve()
    source_path = args.source_path.resolve()
    package_path = args.package_path.resolve()
    previous_package_path = (
        args.previous_package_path.resolve() if args.previous_package_path is not None else None
    )
    staging_root = args.staging_root.resolve()

    source_name, package_name = ensure_safe_paths(
        repository_root,
        source_path,
        package_path,
        previous_package_path,
        staging_root,
    )
    if not is_canonical_version(args.version):
        raise AuthoringError("source-contract", "version-invalid", source=source_name)
    if args.verify and not package_path.is_file():
        raise AuthoringError("verification-missing", "package-missing", source=source_name)

    source_hash = hash_file(source_path, source_name)
    bootstrap = (
        source_path == (repository_root / BOOTSTRAP_SOURCE).resolve()
        and args.version == BOOTSTRAP_VERSION
        and package_path == (repository_root / BOOTSTRAP_OUTPUT).resolve()
    )
    if previous_package_path is None:
        if not bootstrap:
            raise AuthoringError("previous-required", "previous-package-required", source=source_name)
        if source_hash != BOOTSTRAP_SOURCE_SHA256:
            raise AuthoringError("hash-drift", "bootstrap-source-hash", source=source_name)
    elif bootstrap:
        raise AuthoringError("source-contract", "bootstrap-does-not-accept-previous", source=source_name)

    package = parse_source(source_path, source_name, source_hash, args.version)
    validate_package_object(package, source_name)

    if previous_package_path is not None:
        previous, _ = load_previous_package(previous_package_path, repository_root)
        enforce_growth(package, previous, source_name)

    package_bytes = canonical_json_bytes(package)
    stage_path = stage_package(staging_root, package_bytes)
    try:
        status = publish(stage_path, package_path, package_bytes, args.verify, source_name)
    finally:
        stage_path.unlink(missing_ok=True)

    association_count = sum(len(provider["domainSuffixes"]) for provider in package["providers"])
    package_hash = hashlib.sha256(package_bytes).hexdigest()
    print(
        " ".join(
            (
                f"status={status}",
                f"package={package_name}",
                f"version={package['version']}",
                f"providers={len(package['providers'])}",
                f"associations={association_count}",
                f"sha256={package_hash}",
            )
        )
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except AuthoringError as error:
        print(f"ERROR[{error.category}] {error}", file=sys.stderr)
        raise SystemExit(error.exit_code)
    except Exception:
        print("ERROR[source-contract] issue=unexpected-authoring-failure", file=sys.stderr)
        raise SystemExit(EXIT_CODES["source-contract"])
