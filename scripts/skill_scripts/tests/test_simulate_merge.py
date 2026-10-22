"""simulate_merge.py on a temporary repository: exit codes, the conflict report and a pairwise golden."""
import os
import unittest
from unittest import mock

from skill_scripts_support import Repository, SharedGitCase, build_pull_requests, load_script, run_main, short

simulate = load_script("combine-prs", "simulate_merge")


class SimulateMergeTests(SharedGitCase):
    @classmethod
    def setUpClass(cls):
        super().setUpClass()
        env = mock.patch.object(simulate, "ENV", {**simulate.ENV, **{
            name: os.environ[name] for name in ("GIT_CONFIG_GLOBAL", "GIT_CONFIG_NOSYSTEM")}})
        env.start()
        cls.addClassCleanup(env.stop)
        cls.repo = Repository(cls.tmp / "repo")
        cls.commits = build_pull_requests(cls.repo)
        # The same three pull requests merged for real, as the oracle for the simulated tree.
        cls.repo.git("checkout", "-q", "-b", "real", "main")
        for number in (1, 2, 3):
            cls.repo.git("merge", "-q", "--no-ff", "-m", f"merge {number}", f"pr{number}")
        cls.real_tree = cls.repo.git("rev-parse", "HEAD^{tree}")

    def simulate(self, *args):
        return run_main(simulate, list(args), cwd=self.repo.root)

    def heads(self, *numbers):
        return [f"{n}={self.commits[f'pr{n}']}" for n in numbers]

    def test_clean_train_exits_0_and_builds_the_tree_a_real_merge_builds(self):
        code, out, _ = self.simulate("--target", self.commits["target"], *self.heads(1, 2, 3))
        c = self.commits
        self.assertEqual(0, code)
        self.assertEqual(
            f"# Merge simulation from `{short(c['target'])}`\n"
            "\n"
            "| Step | Merge | Result | Files |\n"
            "|---|---|---|---|\n"
            f"| 1 | #1 `{short(c['pr1'])}` | clean | - |\n"
            f"| 2 | #2 `{short(c['pr2'])}` | clean | - |\n"
            f"| 3 | #3 `{short(c['pr3'])}` | clean | - |\n"
            "\n"
            f"Final simulated tree: `{self.real_tree}`\n", out)

    def test_conflicting_head_exits_1_and_names_the_file(self):
        code, out, _ = self.simulate("--target", self.commits["target"], *self.heads(2, 4))
        self.assertEqual(1, code)
        self.assertIn(f"| 1 | #2 `{short(self.commits['pr2'])}` | clean | - |", out)
        self.assertIn(f"| 2 | #4 `{short(self.commits['pr4'])}` | CONFLICT | `shared.txt` |", out)
        self.assertIn("Step 2 (#4):\n- CONFLICT (content): Merge conflict in shared.txt\n", out)
        self.assertTrue(out.rstrip().endswith("(contains conflict markers)"))

    def test_steps_after_a_conflict_are_marked(self):
        code, out, _ = self.simulate("--target", self.commits["target"], *self.heads(2, 4, 1))
        self.assertEqual(1, code)
        self.assertIn("| 3 | #1 ", out)
        self.assertIn("clean (after an unresolved step)", out)

    def test_pairwise_over_three_heads_is_kept_as_a_golden(self):
        code, out, _ = self.simulate("--target", self.commits["target"], "--pairwise", *self.heads(1, 2, 3))
        c = self.commits
        self.assertEqual(0, code)
        self.assertEqual(
            f"# Merge simulation from `{short(c['target'])}`\n"
            "\n"
            "| Step | Merge | Result | Files |\n"
            "|---|---|---|---|\n"
            f"| 1 | #1 `{short(c['pr1'])}` | clean | - |\n"
            f"| 2 | #2 `{short(c['pr2'])}` | clean | - |\n"
            f"| 3 | #3 `{short(c['pr3'])}` | clean | - |\n"
            "\n"
            "## Each PR against the target alone\n"
            "\n"
            "- #1: clean\n"
            "- #2: clean\n"
            "- #3: clean\n"
            "\n"
            "## Pairs (each pair merged onto the target)\n"
            "\n"
            "(pairs not listed merge cleanly)\n"
            "\n"
            f"Final simulated tree: `{self.real_tree}`\n", out)

    def test_pairwise_prints_the_conflicting_pair(self):
        code, out, _ = self.simulate("--target", self.commits["target"], "--pairwise", *self.heads(2, 3, 4))
        self.assertEqual(1, code)
        self.assertIn("- #2: clean\n- #3: clean\n- #4: clean\n", out)
        self.assertIn("\n- #2 × #4: conflicts in shared.txt\n(pairs not listed merge cleanly)\n", out)
        self.assertNotIn("#2 × #3", out)
        self.assertNotIn("#3 × #4", out)

    def test_pairwise_skips_pairs_that_start_with_a_head_that_conflicts_with_the_target(self):
        code, out, _ = self.simulate("--target", self.commits["target"], "--pairwise", *self.heads(5, 1))
        self.assertEqual(1, code)
        self.assertIn("- #5: conflicts in a.txt\n- #1: clean\n", out)
        self.assertIn("- #5 × #1: skipped (#5 conflicts with the target alone)\n", out)

    def test_a_bare_rev_is_labelled_by_its_first_twelve_characters(self):
        code, out, _ = self.simulate("--target", self.commits["target"], self.commits["pr1"])
        self.assertEqual(0, code)
        self.assertIn(f"| 1 | #{self.commits['pr1'][:12]} `{short(self.commits['pr1'])}` | clean | - |", out)


if __name__ == "__main__":
    unittest.main()
