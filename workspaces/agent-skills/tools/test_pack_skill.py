"""Tests for tools/pack_skill.py — the ONLY sanctioned skill-zip builder.

The exclusion predicate is the safety rule that keeps `_dev/` internals,
bytecode, and nested zips out of org-distributed skill zips (the 2026-07-02
zips shipped `_dev/` + `__pycache__` by accident). Run with:

    python -m pytest tools/test_pack_skill.py
    # or, stdlib-only:
    python -m unittest tools.test_pack_skill
"""

from __future__ import annotations

import tempfile
import unittest
import zipfile
from pathlib import Path

from pack_skill import pack


def build_fixture_skill(root: Path) -> Path:
    skill = root / "demo-skill"
    (skill / "references").mkdir(parents=True)
    (skill / "_dev" / "notes").mkdir(parents=True)
    (skill / "scripts" / "__pycache__").mkdir(parents=True)

    (skill / "SKILL.md").write_text("# demo\n", encoding="utf-8")
    (skill / "references" / "guide.md").write_text("guide\n", encoding="utf-8")
    (skill / "scripts" / "tool.py").write_text("print('hi')\n", encoding="utf-8")

    # Everything below must be excluded from the shipped zip.
    (skill / "_dev" / "AGENTS.md").write_text("internal\n", encoding="utf-8")
    (skill / "_dev" / "notes" / "todo.md").write_text("internal\n", encoding="utf-8")
    (skill / "scripts" / "__pycache__" / "tool.cpython-312.pyc").write_bytes(b"\x00")
    (skill / "scripts" / "tool.pyc").write_bytes(b"\x00")
    (skill / "stale-artifact.zip").write_bytes(b"PK\x05\x06" + b"\x00" * 18)
    (skill / ".DS_Store").write_bytes(b"\x00")
    return skill


class PackSkillTests(unittest.TestCase):
    def test_excludes_dev_pycache_pyc_zip_and_dsstore(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            skill = build_fixture_skill(root)
            out = root / "demo-skill.zip"
            pack(skill, out)

            with zipfile.ZipFile(out) as zf:
                names = set(zf.namelist())

            self.assertEqual(
                names,
                {"SKILL.md", "references/guide.md", "scripts/tool.py"},
            )

    def test_output_zip_inside_skill_dir_does_not_include_itself(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            skill = build_fixture_skill(root)
            out = skill / "references" / "self.zip"  # inside the tree being walked
            pack(skill, out)

            with zipfile.ZipFile(out) as zf:
                names = set(zf.namelist())
            self.assertNotIn("references/self.zip", names)
            self.assertIn("SKILL.md", names)

    def test_rejects_directory_without_skill_md(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "not-a-skill").mkdir()
            with self.assertRaises(SystemExit):
                pack(root / "not-a-skill", root / "out.zip")


if __name__ == "__main__":
    unittest.main()
