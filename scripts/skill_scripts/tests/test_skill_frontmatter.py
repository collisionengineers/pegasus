"""Every imported SKILL.md must give the Skill tool its whole description.

A plain YAML scalar ends at " #", so a description that holds "#N" is cut there
without any error (issue 1511). The only rule asserted is that the frontmatter
parses and that the parsed description is whole.
"""
import unittest

import yaml

from skill_scripts_support import SKILLS


def frontmatter(text: str) -> tuple[str, str]:
    """Return (the frontmatter block, the raw text after 'description:' on its line)."""
    lines = text.splitlines()
    if not lines or lines[0] != "---":
        raise AssertionError("no frontmatter")
    end = lines.index("---", 1)
    block = lines[1:end]
    raw = next(line[len("description:"):].strip() for line in block if line.startswith("description:"))
    return "\n".join(block), raw


class SkillFrontmatterTests(unittest.TestCase):
    def test_every_skill_has_a_whole_description(self):
        paths = sorted(SKILLS.glob("*/SKILL.md"))
        self.assertTrue(paths, "no SKILL.md found under .agents/skills")
        for path in paths:
            with self.subTest(skill=path.parent.name):
                block, raw = frontmatter(path.read_text(encoding="utf-8"))
                description = yaml.safe_load(block)["description"]
                self.assertIsInstance(description, str)
                self.assertTrue(description.strip())
                if raw[:1] not in {'"', "'", ">", "|"}:
                    self.assertEqual(raw, description, f"{path.parent.name}: the plain description parsed short")


if __name__ == "__main__":
    unittest.main()
