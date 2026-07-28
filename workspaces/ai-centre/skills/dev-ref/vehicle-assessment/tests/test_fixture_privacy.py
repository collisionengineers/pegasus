"""Guard against real case identifiers leaking into the packaged skill or its fixtures.

The denylist holds identifiers known to appear in the raw source corpus (real registrations and
claim references from case documents). None of them may appear anywhere in the skill folder.
Fixture payloads must use registrations from the synthetic allowlist.
"""

import json
import re
from pathlib import Path
from unittest import TestCase


SKILL_ROOT = Path(__file__).resolve().parents[2]
FIXTURES_DIR = Path(__file__).resolve().parent / "fixtures"

# Real identifiers seen in the source corpus (toUse case documents). Never packaged.
DENYLIST = [
    "NC71TWK",
    "ST59PEP",
    "SJ17FKS",
    "KR69UHS",
    "WD63UPW",
    "QDOS231773",
    "EA64UBD",
    "BR19RZL",
    "EJ65UKE",
]

# Obviously-synthetic registrations approved for fixtures and examples.
SYNTHETIC_REG_ALLOWLIST = {"AB12CDE", "CE00AAA", "AA00AAA"}

TEXT_SUFFIXES = {".md", ".json", ".py", ".yaml", ".yml", ".txt"}


def iter_packaged_files():
    """Yield the text files that tools/pack_skill.py would ship (_dev and __pycache__ excluded).

    Fixtures under _dev/tests/fixtures are scanned separately by the fixture-specific tests;
    this denylist file itself holds the identifiers, so _dev must stay out of the sweep.
    """
    for path in sorted(SKILL_ROOT.rglob("*")):
        if not path.is_file():
            continue
        if "_dev" in path.parts or "__pycache__" in path.parts:
            continue
        if path.suffix.lower() in TEXT_SUFFIXES:
            yield path


class FixturePrivacyTests(TestCase):
    def test_no_denylisted_identifiers_anywhere(self):
        offenders = []
        scan_targets = list(iter_packaged_files()) + sorted(FIXTURES_DIR.glob("*.json"))
        for path in scan_targets:
            content = path.read_text(encoding="utf-8", errors="replace")
            compact = re.sub(r"[\s]", "", content).upper()
            for identifier in DENYLIST:
                if identifier.upper() in compact:
                    offenders.append(f"{path.relative_to(SKILL_ROOT)}: {identifier}")
        self.assertEqual(offenders, [], f"real identifiers found in packaged files: {offenders}")

    def test_fixture_registrations_are_synthetic(self):
        for fixture in sorted(FIXTURES_DIR.glob("*.json")):
            with fixture.open(encoding="utf-8") as handle:
                payload = json.load(handle)
            reg = payload.get("vehicle", {}).get("reg")
            if reg is not None:
                self.assertIn(
                    reg,
                    SYNTHETIC_REG_ALLOWLIST,
                    f"{fixture.name} uses registration '{reg}' not on the synthetic allowlist",
                )

    def test_fixture_vins_are_obviously_synthetic(self):
        for fixture in sorted(FIXTURES_DIR.glob("*.json")):
            with fixture.open(encoding="utf-8") as handle:
                payload = json.load(handle)
            vin = payload.get("vehicle", {}).get("vin")
            if vin is not None:
                self.assertTrue(
                    vin.startswith("SAMPLE") or vin.startswith("TEST"),
                    f"{fixture.name} VIN '{vin}' does not look synthetic",
                )
