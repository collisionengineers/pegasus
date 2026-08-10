from __future__ import annotations

import copy
import hashlib
import importlib.util
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
import zipfile
from xml.sax.saxutils import escape


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
GENERATOR_PATH = REPOSITORY_ROOT / "scripts/reference_data/build_provider_reference_data.py"
WRAPPER_PATH = REPOSITORY_ROOT / "scripts/Build-ProviderReferenceData.ps1"
SPEC = importlib.util.spec_from_file_location("provider_reference_generator", GENERATOR_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError("Provider-domain generator module could not be loaded.")
GENERATOR = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GENERATOR)


class ProviderReferenceGeneratorTests(unittest.TestCase):
    def test_additive_workbook_growth_preserves_v1_and_rejects_mapping_removal(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            inputs = root / "inputs"
            outputs = root / "outputs"
            staging = root / "staging"
            inputs.mkdir()
            outputs.mkdir()

            source_v1 = inputs / "source-v1.xlsx"
            source_v2 = inputs / "source-v2.xlsx"
            source_removed = inputs / "source-removed.xlsx"
            source_extra = inputs / "source-extra.xlsx"
            self._write_workbook(source_v1, [("ALPHA", self._address("seed", "alpha.example"))])
            self._write_workbook(
                source_v2,
                [
                    ("ALPHA", self._address("seed", "alpha.example")),
                    ("BETA", self._address("route", "beta.example")),
                ],
            )
            self._write_workbook(
                source_removed,
                [
                    ("ALPHA", self._address("seed", "changed.example")),
                    ("BETA", self._address("route", "beta.example")),
                ],
            )
            self._write_workbook(
                source_extra,
                [
                    ("ALPHA", self._address("seed", "alpha.example")),
                    ("BETA", self._address("route", "beta.example")),
                    ("GAMMA", self._address("route", "gamma.example")),
                ],
            )

            previous_path = outputs / "provider-domains.v1.json"
            previous_package = self._package_from_source(root, source_v1, "provider-domains-v1")
            previous_bytes = GENERATOR.canonical_json_bytes(previous_package)
            previous_path.write_bytes(previous_bytes)

            output_path = outputs / "provider-domains.v2.json"
            additive = self._run_generator(
                root, source_v2, "provider-domains-v2", output_path, previous_path, staging
            )
            self.assertEqual(0, additive.returncode, additive.stderr)
            self.assertIn("status=published", additive.stdout)
            self.assertEqual(previous_bytes, previous_path.read_bytes())

            no_op = self._run_generator(
                root, source_v2, "provider-domains-v2", output_path, previous_path, staging
            )
            self.assertEqual(0, no_op.returncode, no_op.stderr)
            self.assertIn("status=no-op", no_op.stdout)

            version_two_bytes = output_path.read_bytes()
            removed_output = outputs / "provider-domains.removed.json"
            removed = self._run_generator(
                root,
                source_removed,
                "provider-domains-v3",
                removed_output,
                output_path,
                staging,
            )
            self.assertEqual(GENERATOR.EXIT_CODES["non-monotonic-source"], removed.returncode)
            self.assertIn("ERROR[non-monotonic-source]", removed.stderr)
            self.assertFalse(removed_output.exists())
            self.assertEqual(version_two_bytes, output_path.read_bytes())
            self.assertEqual(previous_bytes, previous_path.read_bytes())

            overwrite = self._run_generator(
                root, source_extra, "provider-domains-v2", output_path, previous_path, staging
            )
            self.assertEqual(GENERATOR.EXIT_CODES["immutable-output"], overwrite.returncode)
            self.assertIn("ERROR[immutable-output]", overwrite.stderr)
            self.assertEqual(version_two_bytes, output_path.read_bytes())
            self.assertEqual(previous_bytes, previous_path.read_bytes())

            missing_previous_output = outputs / "provider-domains.missing-previous.json"
            missing_previous = self._run_generator(
                root,
                source_v2,
                "provider-domains-v3",
                missing_previous_output,
                None,
                staging,
            )
            self.assertEqual(
                GENERATOR.EXIT_CODES["previous-required"], missing_previous.returncode
            )
            self.assertIn("ERROR[previous-required]", missing_previous.stderr)
            self.assertFalse(missing_previous_output.exists())

            source_before_collision = source_v2.read_bytes()
            overlap = self._run_generator(
                root, source_v2, "provider-domains-v3", source_v2, output_path, staging
            )
            self.assertEqual(GENERATOR.EXIT_CODES["output-collision"], overlap.returncode)
            self.assertIn("ERROR[output-collision]", overlap.stderr)
            self.assertEqual(source_before_collision, source_v2.read_bytes())
            self.assertEqual(version_two_bytes, output_path.read_bytes())

    def test_opaque_columns_do_not_change_provider_domain_associations(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            first = root / "first.xlsx"
            second = root / "second.xlsx"
            providers = [
                ("ALPHA", self._address("seed", "alpha.example")),
                ("BETA", self._address("route", "beta.example")),
            ]
            self._write_workbook(first, providers, opaque_marker="first")
            self._write_workbook(second, providers, opaque_marker="second")

            first_package = self._package_from_source(root, first, "provider-domains-v2")
            second_package = self._package_from_source(root, second, "provider-domains-v2")
            self.assertNotEqual(
                first_package["source"]["contentSha256"],
                second_package["source"]["contentSha256"],
            )
            self.assertEqual(
                GENERATOR.provider_pairs(first_package),
                GENERATOR.provider_pairs(second_package),
            )

    def test_growth_contract_rejects_same_version_and_changed_historical_mapping(self) -> None:
        previous = {
            "version": "provider-domains-v1",
            "source": {"path": "inputs/v1.xlsx", "contentSha256": "1" * 64},
            "providers": [{"code": "ALPHA", "domainSuffixes": ["@alpha.example"]}],
        }
        additive = copy.deepcopy(previous)
        additive["version"] = "provider-domains-v2"
        additive["source"] = {"path": "inputs/v2.xlsx", "contentSha256": "2" * 64}
        additive["providers"].append(
            {"code": "BETA", "domainSuffixes": ["@beta.example"]}
        )
        GENERATOR.enforce_growth(additive, previous, "inputs/v2.xlsx")

        same_version = copy.deepcopy(additive)
        same_version["version"] = previous["version"]
        with self.assertRaises(GENERATOR.AuthoringError) as same_version_error:
            GENERATOR.enforce_growth(same_version, previous, "inputs/v2.xlsx")
        self.assertEqual("source-contract", same_version_error.exception.category)

        changed = copy.deepcopy(additive)
        changed["providers"][0]["domainSuffixes"] = ["@changed.example"]
        with self.assertRaises(GENERATOR.AuthoringError) as changed_error:
            GENERATOR.enforce_growth(changed, previous, "inputs/v2.xlsx")
        self.assertEqual("non-monotonic-source", changed_error.exception.category)

    def test_wrapper_rejects_lock_before_source_read_python_or_output_write(self) -> None:
        powershell = shutil.which("pwsh")
        if powershell is None:
            self.skipTest("PowerShell 7 is not available.")

        artifacts = REPOSITORY_ROOT / "artifacts"
        artifacts.mkdir(exist_ok=True)
        with tempfile.TemporaryDirectory(dir=artifacts) as temporary_directory:
            root = Path(temporary_directory)
            source_path = root / "selected.xlsx"
            lock_path = root / "~$selected.xlsx"
            package_path = root / "provider-domains.json"
            lock_path.write_bytes(b"locked")
            package_path.write_bytes(b"unchanged")

            environment = os.environ.copy()
            environment["PATH"] = str(root / "no-tools")
            result = subprocess.run(
                [
                    powershell,
                    "-NoProfile",
                    "-File",
                    str(WRAPPER_PATH),
                    "-SourcePath",
                    str(source_path),
                    "-Version",
                    "provider-domains-lock-test",
                    "-PackagePath",
                    str(package_path),
                ],
                cwd=REPOSITORY_ROOT,
                env=environment,
                capture_output=True,
                text=True,
                check=False,
            )

            self.assertEqual(21, result.returncode)
            self.assertIn("ERROR[source-locked]", result.stderr)
            self.assertEqual(b"unchanged", package_path.read_bytes())
            self.assertFalse(source_path.exists())

    @staticmethod
    def _address(local_part: str, domain: str) -> str:
        return local_part + chr(64) + domain

    @staticmethod
    def _write_workbook(
        path: Path,
        providers: list[tuple[str, str]],
        opaque_marker: str | None = None,
    ) -> None:
        rows = []
        for row_number, (code, observations) in enumerate(providers, start=2):
            opaque_cells = ""
            if opaque_marker is not None:
                opaque_cells = "".join(
                    f'<c r="{column}{row_number}" t="inlineStr"><is><t>'
                    f'{escape(f"{opaque_marker}-{column}-{row_number}")}</t></is></c>'
                    for column in ("B", "C", "D", "F")
                )
            rows.append(
                f'<row r="{row_number}">'
                f'<c r="A{row_number}" t="inlineStr"><is><t>{escape(code)}</t></is></c>'
                f"{opaque_cells}"
                f'<c r="E{row_number}" t="inlineStr"><is><t>{escape(observations)}</t></is></c>'
                "</row>"
            )
        worksheet = (
            '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">'
            f'<sheetData>{"".join(rows)}</sheetData></worksheet>'
        )
        workbook = (
            '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" '
            'xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">'
            '<sheets><sheet name="Sheet1" sheetId="1" r:id="rId1"/></sheets></workbook>'
        )
        relationships = (
            '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
            '<Relationship Id="rId1" '
            'Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" '
            'Target="worksheets/sheet1.xml"/></Relationships>'
        )
        content_types = (
            '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">'
            '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>'
            '<Default Extension="xml" ContentType="application/xml"/>'
            '<Override PartName="/xl/workbook.xml" '
            'ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>'
            '<Override PartName="/xl/worksheets/sheet1.xml" '
            'ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>'
            '</Types>'
        )
        package_relationships = (
            '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
            '<Relationship Id="rId1" '
            'Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" '
            'Target="xl/workbook.xml"/></Relationships>'
        )
        with zipfile.ZipFile(path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
            archive.writestr("[Content_Types].xml", content_types)
            archive.writestr("_rels/.rels", package_relationships)
            archive.writestr("xl/workbook.xml", workbook)
            archive.writestr("xl/_rels/workbook.xml.rels", relationships)
            archive.writestr("xl/worksheets/sheet1.xml", worksheet)

    @staticmethod
    def _package_from_source(root: Path, source: Path, version: str) -> dict[str, object]:
        source_name = source.relative_to(root).as_posix()
        source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
        package = GENERATOR.parse_source(source, source_name, source_hash, version)
        GENERATOR.validate_package_object(package, source_name)
        return package

    @staticmethod
    def _run_generator(
        root: Path,
        source: Path,
        version: str,
        output: Path,
        previous: Path | None,
        staging: Path,
    ) -> subprocess.CompletedProcess[str]:
        arguments = [
            sys.executable,
            str(GENERATOR_PATH),
            "--repository-root",
            str(root),
            "--source-path",
            str(source),
            "--version",
            version,
            "--package-path",
            str(output),
            "--staging-root",
            str(staging),
        ]
        if previous is not None:
            arguments.extend(("--previous-package-path", str(previous)))
        return subprocess.run(
            arguments,
            cwd=root,
            capture_output=True,
            text=True,
            check=False,
        )


if __name__ == "__main__":
    unittest.main()
