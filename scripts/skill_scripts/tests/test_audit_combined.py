"""audit_combined.py on a temporary repository: exit codes, the PASS, FAIL and REVIEW goldens, spaced paths,
hand-resolved merges, normal()."""
import subprocess
import unittest

from skill_scripts_support import Repository, SharedGitCase, build_pull_requests, lines, load_script, run_main, short

audit = load_script("combine-prs", "audit_combined")

TABLE_HEAD = (
    "| PR | Head | Head in tip | Files | Byte-identical | Line-checked | Missing lines | Deletions kept |\n"
    "|---|---|---|---|---|---|---|---|\n")
FOOTER = ("\nTextual audit only: a clean result does not prove the union builds or behaves; "
          "build it and let CI test it.\n")


class AuditCombinedTests(SharedGitCase):
    @classmethod
    def setUpClass(cls):
        super().setUpClass()
        cls.repo = repo = Repository(cls.tmp / "repo")
        cls.c = c = build_pull_requests(repo)

        def integrate(name, *branches):
            """Merge the branches in order onto main; return the tip."""
            repo.git("checkout", "-q", "-b", name, "main")
            for branch in branches:
                repo.git("merge", "-q", "--no-ff", "-m", f"merge {branch}", branch)
            return repo.git("rev-parse", "HEAD")

        c["ok"] = integrate("ok", "pr1", "pr2", "pr3")
        c["stacked"] = integrate("stacked", "pr1", "pr6")
        # A fix commit on top of the clean tip drops the line pr2 added to shared.txt.
        repo.git("checkout", "-q", "-b", "dropped", "ok")
        c["dropped"] = repo.commit("drop pr2 shared line", {
            "shared.txt": lines("shared 1", "shared 2", "shared 3", "shared 4", "shared 5 (pr3)")})
        # The same for the file whose name holds a space.
        repo.git("checkout", "-q", "-b", "dropped-spaced", "ok")
        c["dropped-spaced"] = repo.commit("drop pr2 line from the spaced file", {
            "my file.txt": lines("m1", "m2", "m3", "m4", "m5 (pr3)")})
        # pr4 conflicts with pr2 in shared.txt; the merge is finished by hand, keeping both lines.
        integrate("resolved", "pr2")
        merge = subprocess.run(["git", "merge", "-q", "--no-ff", "-m", "merge pr4", "pr4"],
                               cwd=repo.root, capture_output=True, text=True)
        assert merge.returncode == 1, merge.stdout + merge.stderr
        repo.write("shared.txt", lines("shared 1 (pr2)", "shared 1 (pr4)", "shared 2", "shared 3", "shared 4",
                                       "shared 5"))
        repo.git("add", "shared.txt")
        repo.git("commit", "-q", "--no-edit")
        c["resolved"] = repo.git("rev-parse", "HEAD")

    def run_audit(self, tip, *numbers, target="target"):
        heads = [f"{n}={self.c[f'pr{n}']}" for n in numbers]
        return run_main(audit, ["--target", self.c[target], "--tip", self.c[tip], *heads], cwd=self.repo.root)

    def test_clean_union_passes_with_exit_0(self):
        code, out, _ = self.run_audit("ok", 1, 2, 3)
        c = self.c
        self.assertEqual(0, code)
        self.assertEqual(
            "## Lost-work audit: PASS\n"
            "\n"
            f"Target `{short(c['target'])}` → tip `{short(c['ok'])}`; target is an ancestor of the tip.\n"
            "\n"
            + TABLE_HEAD +
            f"| #1 | `{short(c['pr1'])}` | yes | 2 | 1 | 1 | 0 | 0 |\n"
            f"| #2 | `{short(c['pr2'])}` | yes | 3 | 1 | 2 | 0 | 0 |\n"
            f"| #3 | `{short(c['pr3'])}` | yes | 3 | 1 | 2 | 0 | 0 |\n"
            "\n"
            "Integration-only commits: 3 merge(s), 0 fix commit(s).\n"
            + FOOTER, out)

    def test_an_added_line_deleted_from_the_tip_fails_with_exit_1_and_the_line_named(self):
        code, out, _ = self.run_audit("dropped", 1, 2, 3)
        c = self.c
        self.assertEqual(1, code)
        self.assertEqual(
            "## Lost-work audit: FAIL\n"
            "\n"
            f"Target `{short(c['target'])}` → tip `{short(c['dropped'])}`; target is an ancestor of the tip.\n"
            "\n"
            + TABLE_HEAD +
            f"| #1 | `{short(c['pr1'])}` | yes | 2 | 1 | 1 | 0 | 0 |\n"
            f"| #2 | `{short(c['pr2'])}` | yes | 3 | 1 | 2 | 1 | 0 |\n"
            f"| #3 | `{short(c['pr3'])}` | yes | 3 | 1 | 2 | 0 | 0 |\n"
            "\n"
            "Integration-only commits: 3 merge(s), 1 fix commit(s).\n"
            f"- fix `{short(c['dropped'])}` drop pr2 shared line\n"
            "\n"
            "### FAIL (fix, or explain in the PR body)\n"
            "\n"
            "- #2: 1 added line(s) missing from `shared.txt`: `shared 1 (pr2)`\n"
            + FOOTER, out)

    def test_a_line_a_stacked_pr_removed_is_a_review_item_with_exit_3(self):
        code, out, _ = self.run_audit("stacked", 1, 6)
        c = self.c
        self.assertEqual(3, code)
        self.assertEqual(
            "## Lost-work audit: REVIEW\n"
            "\n"
            f"Target `{short(c['target'])}` → tip `{short(c['stacked'])}`; target is an ancestor of the tip.\n"
            "\n"
            + TABLE_HEAD +
            f"| #1 | `{short(c['pr1'])}` | yes | 2 | 0 | 2 | 0 | 0 |\n"
            f"| #6 | `{short(c['pr6'])}` | yes | 3 | 1 | 2 | 0 | 0 |\n"
            "\n"
            "Integration-only commits: 2 merge(s), 0 fix commit(s).\n"
            "\n"
            "### REVIEW\n"
            "\n"
            "- #1: 1 line(s) in `a.txt` removed by stacked #6: `alpha 4 (pr1)`\n"
            + FOOTER, out)

    def test_only_a_file_resolved_by_hand_is_listed_as_hand_resolved(self):
        # The goldens above print no such section: git merged those files itself. Here the pr2 merge
        # changed b.txt and 'my file.txt' cleanly too; only the pr4 merge's shared.txt was resolved.
        _, out, _ = self.run_audit("resolved", 2, 4)
        heading = "\nFiles hand-resolved in merge commits:\n"
        self.assertIn(heading, out)
        listed = out.split(heading, 1)[1].split("\n\n", 1)[0]
        self.assertEqual(f"- `shared.txt` in {short(self.c['resolved'])}", listed)

    def test_a_head_that_is_not_in_the_tip_fails(self):
        code, out, _ = self.run_audit("ok", 1, 2, 4)
        self.assertEqual(1, code)
        self.assertIn(f"- #4: head `{short(self.c['pr4'])}` is not in the tip (squashed, rebased or not merged)", out)
        self.assertIn(f"| #4 | `{short(self.c['pr4'])}` | NO |", out)

    def test_a_target_that_is_not_an_ancestor_of_the_tip_fails(self):
        code, out, _ = self.run_audit("pr3", 3, target="pr2")
        self.assertEqual(1, code)
        self.assertIn("is not an ancestor of the tip: the branch was not built on it", out)
        self.assertIn("is NOT an ancestor of the tip.", out)

    def test_a_line_lost_from_a_file_with_a_space_in_its_name_is_reported(self):
        # git ends the --- and +++ lines of such a path with a tab (issue 1526).
        code, out, _ = self.run_audit("dropped-spaced", 1, 2, 3)
        self.assertEqual(1, code)
        self.assertIn("- #2: 1 added line(s) missing from `my file.txt`: `m1 (pr2)`\n", out)
        self.assertIn(f"| #2 | `{short(self.c['pr2'])}` | yes | 3 | 1 | 2 | 1 | 0 |\n", out)


class AuditRenumbered(SharedGitCase):
    """A fix commit renumbered a register id on a PR's lines: REVIEW with --id-pattern, FAIL without."""

    REGISTER = "docs/rulings.md"
    ID_PATTERN = r"R[1-9]\d*"

    @classmethod
    def setUpClass(cls):
        super().setUpClass()
        cls.repo = repo = Repository(cls.tmp / "repo")
        cls.c = c = {}
        rows = lines("| Id | Q | A |", "|---|---|---|", "| R1 | q1 | a1 |", "| R2 | q2 | a2 |")
        c["target"] = repo.commit("base", {cls.REGISTER: rows, "a.md": lines("cites R2")})
        repo.branch("pr1", "main")
        c["pr1"] = repo.commit("pr1", {cls.REGISTER: rows + "| R3 | q3 | a3 |\n",
                                       "a.md": lines("cites R2", "pr1 says (R3)")})
        repo.branch("pr2", "main")
        c["pr2"] = repo.commit("pr2", {"a.md": lines("cites R2", "pr2 says (R3)", "pr2 says (R4)")})

        def tip(name, pr, files):
            repo.git("checkout", "-q", "-b", name, "main")
            repo.git("merge", "-q", "--no-ff", "-m", f"merge {pr}", pr)
            c[name] = repo.commit(f"fix: {name}", files)

        tip("renumbered", "pr1", {cls.REGISTER: rows + "| R4 | q3 | a3 |\n", "a.md": lines("cites R2", "pr1 says (R4)")})
        tip("deleted", "pr1", {"a.md": lines("cites R2")})
        tip("reworded", "pr1", {"a.md": lines("cites R2", "pr1 said (R4)")})
        tip("one", "pr2", {"a.md": lines("cites R2", "pr2 says (R5)")})

    def run_audit(self, tip, pr, *extra):
        return run_main(audit, ["--target", self.c["target"], "--tip", self.c[tip], *extra,
                                f"{pr[2:]}={self.c[pr]}"], cwd=self.repo.root)

    def test_without_the_pattern_a_renumbered_line_is_a_missing_line(self):
        code, out, _ = self.run_audit("renumbered", "pr1")
        self.assertEqual(1, code)
        self.assertIn("- #1: 1 added line(s) missing from `a.md`: `pr1 says (R3)`\n", out)
        self.assertIn("- #1: 1 added line(s) missing from `docs/rulings.md`: `| R3 | q3 | a3 |`\n", out)

    def test_with_the_pattern_a_renumbered_line_is_a_review_item_with_exit_3_and_no_missing_count(self):
        code, out, _ = self.run_audit("renumbered", "pr1", "--id-pattern", self.ID_PATTERN)
        self.assertEqual(3, code)
        self.assertIn("## Lost-work audit: REVIEW\n", out)
        self.assertIn(f"| #1 | `{short(self.c['pr1'])}` | yes | 2 | 0 | 2 | 0 | 0 |\n", out)
        self.assertIn("- #1: 1 added line(s) in `a.md` present with only ids changed (R3→R4): `pr1 says (R3)`\n", out)
        self.assertIn("- #1: 1 added line(s) in `docs/rulings.md` present with only ids changed (R3→R4): "
                      "`| R3 | q3 | a3 |`\n", out)
        self.assertNotIn("FAIL", out)

    def test_a_deleted_line_stays_a_failure(self):
        code, out, _ = self.run_audit("deleted", "pr1", "--id-pattern", self.ID_PATTERN)
        self.assertEqual(1, code)
        self.assertIn("- #1: 1 added line(s) missing from `a.md`: `pr1 says (R3)`\n", out)

    def test_an_id_change_with_any_other_change_stays_a_failure(self):
        code, out, _ = self.run_audit("reworded", "pr1", "--id-pattern", self.ID_PATTERN)
        self.assertEqual(1, code)
        self.assertIn("- #1: 1 added line(s) missing from `a.md`: `pr1 says (R3)`\n", out)

    def test_one_tip_line_vouches_for_one_missing_line(self):
        code, out, _ = self.run_audit("one", "pr2", "--id-pattern", self.ID_PATTERN)
        self.assertEqual(1, code)
        self.assertIn("- #2: 1 added line(s) in `a.md` present with only ids changed (R3→R5): `pr2 says (R3)`\n", out)
        self.assertIn("- #2: 1 added line(s) missing from `a.md`: `pr2 says (R4)`\n", out)


class MaskedLines(unittest.TestCase):
    def test_ids_are_hidden_on_word_boundaries_and_the_changed_pairs_named(self):
        mask = audit.Mask(r"R[1-9]\d*")
        self.assertEqual("see # and #, not VR42 or R0001", mask.masked("see R550 and R12, not VR42 or R0001"))
        self.assertEqual("R550→R559", mask.pairs("R550 (R550)", "R559 (R559)"))
        self.assertEqual("R551→R560", mask.pairs("R2, R551", "R2, R560"))
        self.assertFalse(audit.Mask(None))
        self.assertEqual("as is R1", audit.Mask(None).masked("as is R1"))


class NormalLines(unittest.TestCase):
    def test_lines_compare_without_indentation_whitespace_runs_or_a_trailing_separator(self):
        rows = {
            "indent": ("    return value", "return value"),
            "tabs and runs": ("a \t  b", "a b"),
            "comma": ("    one,", "one"),
            "semicolon": ("x = 1;", "x = 1"),
            "both": ("list,;", "list"),
            "blank": ("   \t ", ""),
            "inner comma": ("a, b", "a, b"),
        }
        for name, (raw, expected) in rows.items():
            with self.subTest(name):
                self.assertEqual(expected, audit.normal(raw))


if __name__ == "__main__":
    unittest.main()
