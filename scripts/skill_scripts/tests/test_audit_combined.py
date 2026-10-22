"""audit_combined.py on a temporary repository: exit codes, the PASS, FAIL and REVIEW goldens, spaced paths, normal()."""
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
            """Merge the branches in order onto main; return the tip and each merge commit."""
            repo.git("checkout", "-q", "-b", name, "main")
            merges = []
            for branch in branches:
                repo.git("merge", "-q", "--no-ff", "-m", f"merge {branch}", branch)
                merges.append(repo.git("rev-parse", "HEAD"))
            return merges[-1], merges

        c["ok"], cls.ok_merges = integrate("ok", "pr1", "pr2", "pr3")
        c["stacked"], cls.stacked_merges = integrate("stacked", "pr1", "pr6")
        # A fix commit on top of the clean tip drops the line pr2 added to shared.txt.
        repo.git("checkout", "-q", "-b", "dropped", "ok")
        c["dropped"] = repo.commit("drop pr2 shared line", {
            "shared.txt": lines("shared 1", "shared 2", "shared 3", "shared 4", "shared 5 (pr3)")})
        # The same for the file whose name holds a space.
        repo.git("checkout", "-q", "-b", "dropped-spaced", "ok")
        c["dropped-spaced"] = repo.commit("drop pr2 line from the spaced file", {
            "my file.txt": lines("m1", "m2", "m3", "m4", "m5 (pr3)")})

    def hand_resolved(self):
        """The list git diff-tree --cc gives for the merges of the clean tip: files both sides changed."""
        first, _, third = self.ok_merges
        return ("\nFiles hand-resolved in merge commits:\n"
                f"- `a.txt` in {short(first)}\n"
                f"- `my file.txt` in {short(third)}\n"
                f"- `shared.txt` in {short(third)}\n")

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
            + self.hand_resolved() + FOOTER, out)

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
            + self.hand_resolved() +
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
            "Files hand-resolved in merge commits:\n"
            f"- `a.txt` in {short(self.stacked_merges[1])}, {short(self.stacked_merges[0])}\n"
            "\n"
            "### REVIEW\n"
            "\n"
            "- #1: 1 line(s) in `a.txt` removed by stacked #6: `alpha 4 (pr1)`\n"
            + FOOTER, out)

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
