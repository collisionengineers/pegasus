import re
from pathlib import Path
from unittest import TestCase


SKILL_ROOT = Path(__file__).resolve().parents[2]
SKILL_MD = SKILL_ROOT / "SKILL.md"

ALLOWED_FRONTMATTER_KEYS = {"name", "description"}
MAX_DESCRIPTION_CHARS = 1024
REQUIRED_TRIGGER_TERMS = [
    "vehicle assessment",
    "photos",
    "repair",
    "ABP",
    "total-loss-assessment",
    "repair estimate",
]


def read_frontmatter() -> tuple[dict[str, str], str]:
    text = SKILL_MD.read_text(encoding="utf-8")
    match = re.match(r"^---\n(.*?)\n---\n(.*)$", text, re.DOTALL)
    assert match, "SKILL.md must start with a --- frontmatter block"
    block, body = match.group(1), match.group(2)

    fields: dict[str, str] = {}
    current_key = None
    lines: list[str] = []
    for line in block.splitlines():
        key_match = re.match(r"^([A-Za-z][A-Za-z0-9_-]*):(.*)$", line)
        if key_match:
            if current_key is not None:
                fields[current_key] = " ".join(part for part in lines if part).strip()
            current_key = key_match.group(1)
            value = key_match.group(2).strip()
            lines = [] if value in {">-", ">", "|", "|-"} else [value]
        elif current_key is not None:
            lines.append(line.strip())
    if current_key is not None:
        fields[current_key] = " ".join(part for part in lines if part).strip()
    return fields, body


class SkillMetadataTests(TestCase):
    def test_folder_and_name_match(self):
        fields, _ = read_frontmatter()

        self.assertEqual(fields.get("name"), "vehicle-assessment")
        self.assertEqual(SKILL_ROOT.name, "vehicle-assessment")

    def test_only_allowed_frontmatter_keys(self):
        fields, _ = read_frontmatter()

        self.assertEqual(set(fields), ALLOWED_FRONTMATTER_KEYS)

    def test_description_length_and_trigger_coverage(self):
        fields, _ = read_frontmatter()
        description = fields["description"]

        self.assertLessEqual(len(description), MAX_DESCRIPTION_CHARS, f"description is {len(description)} chars")
        for term in REQUIRED_TRIGGER_TERMS:
            self.assertIn(term.lower(), description.lower(), f"description missing trigger term: {term}")

    def test_every_referenced_file_exists(self):
        _, body = read_frontmatter()
        mentioned = set(re.findall(r"`((?:references|scripts)/[A-Za-z0-9_.\-]+)`", body))

        self.assertTrue(mentioned, "SKILL.md should reference its references/ and scripts/ files")
        missing = sorted(path for path in mentioned if not (SKILL_ROOT / path).is_file())
        self.assertEqual(missing, [], f"SKILL.md mentions missing files: {missing}")

    def test_all_reference_files_are_mentioned(self):
        _, body = read_frontmatter()
        mentioned = set(re.findall(r"`((?:references|scripts)/[A-Za-z0-9_.\-]+)`", body))
        on_disk = {f"references/{path.name}" for path in (SKILL_ROOT / "references").iterdir() if path.is_file()}

        unmentioned = sorted(on_disk - mentioned)
        self.assertEqual(unmentioned, [], f"reference files not mentioned in SKILL.md: {unmentioned}")
