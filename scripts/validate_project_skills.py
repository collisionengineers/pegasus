from __future__ import annotations

import re
import sys
from pathlib import Path


def fail(message: str) -> None:
    raise ValueError(message)


def frontmatter(skill_file: Path) -> dict[str, str]:
    text = skill_file.read_text(encoding="utf-8")
    lines = text.splitlines()
    if not lines or lines[0] != "---":
        fail(f"{skill_file}: missing opening frontmatter delimiter")

    try:
        closing = lines.index("---", 1)
    except ValueError as exc:
        raise ValueError(f"{skill_file}: missing closing frontmatter delimiter") from exc

    metadata: dict[str, str] = {}
    for line in lines[1:closing]:
        if not line.strip():
            continue
        key, separator, value = line.partition(":")
        if not separator:
            fail(f"{skill_file}: unsupported frontmatter line {line!r}")
        metadata[key.strip()] = value.strip().strip('"')

    if set(metadata) != {"name", "description"}:
        fail(f"{skill_file}: frontmatter must contain only name and description")
    if not metadata["description"] or "TODO" in text:
        fail(f"{skill_file}: description is missing or TODO remains")
    return metadata


def validate_skill(skill_dir: Path) -> None:
    skill_file = skill_dir / "SKILL.md"
    interface_file = skill_dir / "agents" / "openai.yaml"
    if not skill_file.is_file() or not interface_file.is_file():
        fail(f"{skill_dir}: SKILL.md or agents/openai.yaml is missing")

    metadata = frontmatter(skill_file)
    if metadata["name"] != skill_dir.name:
        fail(f"{skill_dir}: skill name does not match directory")

    interface = interface_file.read_text(encoding="utf-8")
    for field in ("display_name", "short_description", "default_prompt"):
        if not re.search(rf"^\s+{field}:\s+\".+\"\s*$", interface, re.MULTILINE):
            fail(f"{interface_file}: missing quoted {field}")
    if f"${skill_dir.name}" not in interface:
        fail(f"{interface_file}: default_prompt must mention ${skill_dir.name}")

    skill_text = skill_file.read_text(encoding="utf-8")
    for relative in re.findall(r"\]\((references/[^)#]+)", skill_text):
        if not (skill_dir / relative).is_file():
            fail(f"{skill_file}: missing linked reference {relative}")


def main() -> int:
    root = Path(sys.argv[1] if len(sys.argv) > 1 else ".codex/skills")
    skills = sorted(path.parent.parent for path in root.glob("*/agents/openai.yaml"))
    if not skills:
        fail(f"{root}: no project skills found")
    for skill in skills:
        validate_skill(skill)
    print(f"Validated {len(skills)} project skills.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except ValueError as error:
        print(error, file=sys.stderr)
        raise SystemExit(1) from error
