"""renumber_register.py on temporary repositories: the map, the lines it rewrites and leaves, bytes kept,
the exit codes, and the id pattern."""
import subprocess
import unittest

from skill_scripts_support import IsolatedGitCase, Repository, lines, load_script, run_main

renumber = load_script("combine-prs", "renumber_register")

REGISTER = "docs/rulings.md"
HEADER = ["| Id | Question | Answer |", "|---|---|---|"]
BASE_ROWS = ["| R1 | q1 | a1 |", "| R2 | q2 | a2 |", "| R3 | q3 | a3 |"]


def register(*rows: str) -> str:
    return lines(*HEADER, *BASE_ROWS, *rows)


class Combine(IsolatedGitCase):
    """A target with three rulings; PR A adds R4 and R5, PR B adds R4 and cites it in two files."""

    def setUp(self):
        super().setUp()
        self.repo = repo = Repository(self.tmp / "repo")
        self.base = repo.commit("base", {REGISTER: register(), "a.md": lines("cites R2")})
        repo.branch("prA", "main")
        self.a = repo.commit("prA", {REGISTER: register("| R4 | qA4 | aA4 |", "| R5 | qA5 | aA5 |"),
                                     "a.md": lines("cites R2", "A says R4–R5")})
        repo.branch("prB", self.base)
        self.b = repo.commit("prB", {REGISTER: register("| R4 | qB4 | aB4 |"),
                                     "a.md": lines("cites R2", "B says (R4)"),
                                     "b.cs": lines("// B (R4)", "// R2, R4")})
        repo.git("checkout", "-q", "main")

    def merge(self, head: str, resolved: dict[str, str] | None = None) -> str:
        result = subprocess.run(["git", "merge", "-q", "--no-ff", "-m", f"merge {head[:7]}", head],
                                cwd=self.repo.root, capture_output=True, text=True)
        if result.returncode != 0:
            assert resolved, result.stdout + result.stderr
            for path, text in resolved.items():
                self.repo.write(path, text)
            self.repo.git("add", "-A")
            self.repo.git("commit", "-q", "--no-edit")
        return self.repo.git("rev-parse", "HEAD")

    def merge_a_then_b(self, b_register_row="| R4 | qB4 | aB4 |", b_a_line="B says (R4)"):
        self.merge(self.a)
        return self.merge(self.b, {
            REGISTER: register("| R4 | qA4 | aA4 |", "| R5 | qA5 | aA5 |", b_register_row),
            "a.md": lines("cites R2", "A says R4–R5", b_a_line)})

    def run_renumber(self, head, *extra, target=None):
        return run_main(renumber, ["--head", head, "--target", target or self.base, "--label", "B", *extra],
                        cwd=self.repo.root)

    def read(self, path: str) -> str:
        return (self.repo.root / path).read_bytes().decode("utf-8")

    def status(self) -> str:
        return self.repo.git("status", "--porcelain")


class RenumberALaterPullRequest(Combine):
    maxDiff = None

    SUMMARY = (
        "# Renumber #B: R4=R6\n"
        "\n"
        "| PR | Was | Now | Files | Lines |\n"
        "|---|---|---|---|---|\n"
        "| #B | R4 | R6 | 3 | 4 |\n"
        "\n"
        "Files:\n"
        "- `a.md`: 1 line(s)\n"
        "- `b.cs`: 2 line(s)\n"
        "- `docs/rulings.md`: 1 line(s)\n"
        "\n"
        "Suggested commit subject: fix: renumber #B's register ids (R4=R6)\n"
        "Commit messages and PR titles that cite the old ids are not rewritten.\n")

    def test_the_later_pr_moves_to_the_next_free_id_on_its_own_lines_only(self):
        self.merge_a_then_b()
        code, out, err = self.run_renumber(self.b)
        self.assertEqual((0, ""), (code, err))
        self.assertEqual(self.SUMMARY, out)
        self.assertEqual(register("| R4 | qA4 | aA4 |", "| R5 | qA5 | aA5 |", "| R6 | qB4 | aB4 |"),
                         self.read(REGISTER))
        self.assertEqual(lines("cites R2", "A says R4–R5", "B says (R6)"), self.read("a.md"))
        self.assertEqual(lines("// B (R6)", "// R2, R6"), self.read("b.cs"))

    def test_a_dry_run_prints_the_same_summary_and_writes_nothing(self):
        self.merge_a_then_b()
        code, out, _ = self.run_renumber(self.b, "--dry-run")
        self.assertEqual(0, code)
        self.assertEqual(self.SUMMARY.replace("R4=R6\n", "R4=R6 (dry run)\n", 1), out)
        self.assertEqual("", self.status())

    def test_a_second_run_after_the_commit_changes_nothing(self):
        self.merge_a_then_b()
        self.run_renumber(self.b)
        self.repo.git("add", "-A")
        self.repo.git("commit", "-q", "-m", "fix: renumber")
        code, out, _ = self.run_renumber(self.b)
        self.assertEqual(0, code)
        self.assertIn("# Renumber #B: R4=R6\n", out)
        self.assertIn("- none (every citation was already in its renumbered form)\n", out)
        self.assertEqual("", self.status())

    def test_a_row_the_merge_resolution_already_renumbered_is_found_by_its_text(self):
        # The resolution wrote B's row as R6; only the citations still say R4 (1808's #1794).
        self.merge_a_then_b(b_register_row="| R6 | qB4 | aB4 |")
        code, out, _ = self.run_renumber(self.b)
        self.assertEqual(0, code)
        self.assertIn("| #B | R4 | R6 | 2 | 3 |\n", out)
        self.assertEqual(lines("cites R2", "A says R4–R5", "B says (R6)"), self.read("a.md"))
        self.assertEqual(lines("// B (R6)", "// R2, R6"), self.read("b.cs"))

    def test_nothing_to_do_when_no_id_clashes(self):
        self.merge(self.b)   # B alone: R4 is free
        code, out, _ = self.run_renumber(self.b)
        self.assertEqual(0, code)
        self.assertEqual("# Renumber #B: nothing to do\n\nNo id of #B's 1 register row(s) is claimed by another "
                         "row at HEAD.\n", out)
        self.assertEqual("", self.status())

    def test_a_citing_line_the_resolution_dropped_exits_1_and_writes_nothing(self):
        self.merge_a_then_b(b_a_line="B says nothing")
        code, _, err = self.run_renumber(self.b)
        self.assertEqual(1, code)
        self.assertIn("error: line not found at HEAD: `a.md`: `B says (R4)`", err)
        self.assertEqual("", self.status())

    def test_a_line_also_in_the_tip_before_the_merge_is_left_for_a_hand_edit(self):
        # A and B both added the same line and the resolution kept one copy: nobody can say whose it is.
        self.repo.git("checkout", "-q", "prB")
        b = self.repo.commit("prB shared", {"a.md": lines("cites R2", "B says (R4)", "both say (R4)")})
        self.repo.git("checkout", "-q", "prA")
        a = self.repo.commit("prA shared", {"a.md": lines("cites R2", "A says R4–R5", "both say (R4)")})
        self.repo.git("checkout", "-q", "main")
        self.merge(a)
        self.merge(b, {REGISTER: register("| R4 | qA4 | aA4 |", "| R5 | qA5 | aA5 |", "| R4 | qB4 | aB4 |"),
                       "a.md": lines("cites R2", "A says R4–R5", "both say (R4)", "B says (R4)")})
        code, out, _ = self.run_renumber(b)
        self.assertEqual(3, code)
        self.assertIn("Shared lines left for a hand edit:\n- `a.md`: `both say (R4)` (also in the tip before the "
                      "merge)\n", out)
        self.assertEqual(lines("cites R2", "A says R4–R5", "both say (R4)", "B says (R6)"), self.read("a.md"))

    def test_a_dirty_tree_or_an_unmerged_head_is_a_precondition_failure(self):
        self.merge_a_then_b()
        self.repo.write("a.md", "edited\n")
        code, _, err = self.run_renumber(self.b)
        self.assertEqual((2, True), (code, "not clean" in err))
        self.repo.git("checkout", "-q", "--", "a.md")
        self.repo.git("checkout", "-q", self.a)
        code, _, err = self.run_renumber(self.b)
        self.assertEqual((2, True), (code, "not an ancestor of HEAD" in err))


class RangesStacksAndBytes(Combine):
    def test_a_block_shifts_together_and_a_range_only_partly_the_prs_is_left(self):
        self.repo.git("checkout", "-q", "-b", "prC", self.base)
        c = self.repo.commit("prC", {REGISTER: register("| R4 | qC4 | aC4 |", "| R5 | qC5 | aC5 |"),
                                     "c.md": lines("C says R4–R5", "C spans R2 to R4", "C alone R5")})
        self.repo.git("checkout", "-q", "main")
        self.merge(self.a)
        self.merge(c, {REGISTER: register("| R4 | qA4 | aA4 |", "| R5 | qA5 | aA5 |", "| R4 | qC4 | aC4 |",
                                          "| R5 | qC5 | aC5 |")})
        code, out, _ = run_main(renumber, ["--head", c, "--target", self.base, "--label", "C"], cwd=self.repo.root)
        self.assertEqual(3, code)
        self.assertIn("# Renumber #C: R4=R6, R5=R7\n", out)
        self.assertIn("Ranges only partly this PR's, left for a hand edit:\n- `c.md`: `C spans R2 to R4`\n", out)
        self.assertEqual(lines("C says R6–R7", "C spans R2 to R4", "C alone R7"), self.read("c.md"))
        self.assertIn("| R6 | qC4 | aC4 |\n| R7 | qC5 | aC5 |\n", self.read(REGISTER))

    def test_a_stacked_child_takes_the_next_free_id_and_inherits_its_parents_pair_through_map(self):
        self.repo.git("checkout", "-q", "-b", "prC", "prB")
        c = self.repo.commit("prC", {REGISTER: register("| R4 | qB4 | aB4 |", "| R5 | qC5 | aC5 |"),
                                     "c.md": lines("C cites R4 and R5")})
        self.repo.git("checkout", "-q", "main")
        self.merge_a_then_b()
        self.run_renumber(self.b)
        self.repo.git("add", "-A")
        self.repo.git("commit", "-q", "-m", "fix: renumber B")
        self.merge(c, {REGISTER: register("| R4 | qA4 | aA4 |", "| R5 | qA5 | aA5 |", "| R6 | qB4 | aB4 |",
                                          "| R5 | qC5 | aC5 |")})
        code, out, _ = run_main(renumber, ["--head", c, "--base", self.b, "--label", "C", "--map", "R4=R6"],
                                cwd=self.repo.root)
        self.assertEqual(0, code)
        self.assertIn("# Renumber #C: R4=R6, R5=R7\n", out)
        self.assertEqual(lines("C cites R6 and R7"), self.read("c.md"))
        self.assertIn("| R6 | qB4 | aB4 |\n| R7 | qC5 | aC5 |\n", self.read(REGISTER))

    def test_crlf_bom_and_non_ascii_bytes_are_kept_and_a_binary_file_is_skipped(self):
        raw = "﻿// B (R4) – note\r\n// R2, R4\r\n".encode("utf-8")
        self.repo.git("checkout", "-q", "prB")
        (self.repo.root / "b.cs").write_bytes(raw)
        (self.repo.root / "blob.bin").write_bytes(b"R4\x00\xff\x00")
        self.repo.git("add", "-A")
        self.repo.git("commit", "-q", "-m", "prB bytes")
        b = self.repo.git("rev-parse", "HEAD")
        self.repo.git("checkout", "-q", "main")
        self.merge(self.a)
        self.merge(b, {REGISTER: register("| R4 | qA4 | aA4 |", "| R5 | qA5 | aA5 |", "| R4 | qB4 | aB4 |"),
                       "a.md": lines("cites R2", "A says R4–R5", "B says (R4)")})
        code, out, _ = self.run_renumber(b)
        self.assertEqual(0, code)
        self.assertEqual("﻿// B (R6) – note\r\n// R2, R6\r\n".encode("utf-8"),
                         (self.repo.root / "b.cs").read_bytes())
        self.assertEqual(b"R4\x00\xff\x00", (self.repo.root / "blob.bin").read_bytes())
        self.assertIn("Binary files skipped:\n- `blob.bin`\n", out)


class IdPattern(unittest.TestCase):
    def test_ids_match_on_word_boundaries_without_leading_zeros(self):
        ids = renumber.Ids(r"R([1-9]\d*)")
        self.assertEqual(["4", "5", "12"], ids.text.findall("VR42 R4a R0001 R4 (R5) R12."))
        self.assertEqual(("R7", 7), ids.row_id("| R7 | q | a |"))
        self.assertIsNone(ids.row_id("| Id | q | a |"))
        self.assertEqual("| # | q cites # |", ids.mask("| R7 | q cites R2 |"))
        self.assertEqual("R12", ids.renumbered(ids.text.search("R7"), 12))


if __name__ == "__main__":
    unittest.main()
