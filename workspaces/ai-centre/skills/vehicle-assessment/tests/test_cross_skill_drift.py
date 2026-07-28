"""Guard against silent drift between vehicle-assessment and total-loss-assessment.

The two skills share (copies of) the frozen Audatex generator, the payload validation
machinery, the ABP reference data, and several references. vehicle-assessment is the
content-canonical copy: sync flows VA -> TLA (byte-copy), never the other way.

Intentionally divergent files, NOT covered by this test:
- references/gotchas.md          (TLA keeps its own scoped gotchas list)
- references/damage-cataloguing.md (VA's version references VA-only files)
- SKILL.md                        (different skill boundaries by design)
- references/dispute-response-boundaries.md (TLA-only)
- _dev/ content                   (per-skill dev wrappers, never packaged)

Hashes are EOL-normalized (CRLF -> LF before sha256) because core.autocrlf=true makes
raw-byte equality environment-fragile across checkouts.
"""

import hashlib
from pathlib import Path
from unittest import TestCase, skipUnless


SKILL_ROOT = Path(__file__).resolve().parents[1]
SIBLING_ROOT = SKILL_ROOT.parent / "total-loss-assessment"

SHARED_FILES = [
    "scripts/audatex_gen_v4.py",
    "scripts/validate_assessment_payload.py",
    "scripts/validate_cli.py",
    "scripts/assessment_payload.schema.json",
    "scripts/requirements.txt",
    "scripts/validate_abp_reference_data.py",
    "references/abp-reference-data.2026.json",
    "references/labour-rates.md",
    "references/extras-package.md",
    "references/eva-routing.md",
]


def normalized_sha256(path: Path) -> str:
    content = path.read_bytes().replace(b"\r\n", b"\n")
    return hashlib.sha256(content).hexdigest()


@skipUnless(SIBLING_ROOT.is_dir(), "total-loss-assessment sibling skill not checked out")
class CrossSkillDriftTests(TestCase):
    def test_shared_files_are_content_identical(self):
        drifted = []
        for relative in SHARED_FILES:
            ours = SKILL_ROOT / relative
            theirs = SIBLING_ROOT / relative
            self.assertTrue(ours.is_file(), f"vehicle-assessment/{relative} is missing")
            self.assertTrue(theirs.is_file(), f"total-loss-assessment/{relative} is missing")
            if normalized_sha256(ours) != normalized_sha256(theirs):
                drifted.append(relative)
        self.assertEqual(
            drifted,
            [],
            "shared files drifted between skills (vehicle-assessment is canonical; "
            f"byte-copy VA -> TLA to resync): {drifted}",
        )
