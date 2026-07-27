#!/usr/bin/env python3
"""Pack a Codex/Claude skill directory into a clean zip."""

from __future__ import annotations

import sys
import zipfile
from pathlib import Path


EXCLUDED_DIRS = {"_dev", "__pycache__"}
EXCLUDED_NAMES = {".DS_Store"}
EXCLUDED_SUFFIXES = {".pyc", ".zip"}


def excluded(path: Path, root: Path, output: Path) -> bool:
    if path.resolve() == output.resolve():
        return True
    rel = path.relative_to(root)
    if any(part in EXCLUDED_DIRS for part in rel.parts):
        return True
    if path.name in EXCLUDED_NAMES:
        return True
    return path.suffix.lower() in EXCLUDED_SUFFIXES


def pack(skill_dir: Path, out_zip: Path) -> None:
    skill_dir = skill_dir.resolve()
    out_zip = out_zip.resolve()
    if not skill_dir.is_dir():
        raise SystemExit(f"skill-dir is not a directory: {skill_dir}")
    if not (skill_dir / "SKILL.md").is_file():
        raise SystemExit(f"skill-dir does not contain SKILL.md: {skill_dir}")

    out_zip.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(out_zip, "w", compression=zipfile.ZIP_DEFLATED) as zf:
        for path in sorted(p for p in skill_dir.rglob("*") if p.is_file()):
            if excluded(path, skill_dir, out_zip):
                continue
            zf.write(path, path.relative_to(skill_dir).as_posix())


def main(argv: list[str]) -> int:
    if len(argv) != 3:
        print("usage: python tools/pack_skill.py <skill-dir> <out.zip>", file=sys.stderr)
        return 2
    pack(Path(argv[1]), Path(argv[2]))
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
