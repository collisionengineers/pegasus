"""cleanup_worktree.py on a real temporary repository and linked worktree.

git runs for real. Only `gh` is answered by a fake, patched in at run(): a fake
gh program on PATH is not found on Windows unless it is an .exe. The refusals
leave the repository as they found it and share one; each removal gets its own.
"""
import json
import os
import unittest
from unittest import mock

from skill_scripts_support import IsolatedGitCase, Repository, SharedGitCase, load_script, run_main

cleanup = load_script("resolve-issue", "cleanup_worktree")
real_run = cleanup.run

BRANCH = "task/example"


def make_fixture(tmp):
    """A main checkout with one commit and a linked worktree on BRANCH at the same commit."""
    repo = Repository(tmp / "main")
    repo.commit("first", {"readme.txt": "first\n"})
    worktree = tmp / "wt"
    repo.git("worktree", "add", "-q", "-b", BRANCH, str(worktree))
    return repo, worktree, repo.git("rev-parse", "HEAD", cwd=worktree)


class CleanupHelpers:
    def pr(self, **overrides):
        value = {"state": "OPEN", "headRefOid": self.head, "headRefName": BRANCH,
                 "url": "https://github.com/acme/widgets/pull/7"}
        value.update(overrides)
        return (0, json.dumps(value))

    def clean(self, *extra, worktree=None, gh=None, cwd=None):
        answer = gh if gh is not None else self.pr()

        def run(args, cwd=None):
            if args[0] == "gh":
                self.gh_calls.append(args)
                return answer
            return real_run(args, cwd=cwd)

        with mock.patch.object(cleanup, "run", run):
            return run_main(cleanup, ["--pr", "7", "--worktree", str(worktree or self.worktree), *extra],
                            cwd=cwd or self.repo.root)

    def branches(self):
        return self.repo.git("branch", "--list", BRANCH)


class OwnRepository(CleanupHelpers, IsolatedGitCase):
    def setUp(self):
        super().setUp()
        self.repo, self.worktree, self.head = make_fixture(self.tmp)
        self.gh_calls = []


class SharedRepository(CleanupHelpers, SharedGitCase):
    @classmethod
    def setUpClass(cls):
        super().setUpClass()
        cls.repo, cls.worktree, cls.head = make_fixture(cls.tmp)

    def setUp(self):
        self.gh_calls = []


class CleanupWorktreeRefusals(SharedRepository):
    def test_missing_directory_is_refused(self):
        code, out, _ = self.clean(worktree=self.tmp / "absent")
        self.assertEqual(1, code)
        self.assertIn("is not a directory", out)
        self.assertEqual([], self.gh_calls)

    def test_unregistered_directory_is_refused(self):
        (self.tmp / "plain").mkdir(exist_ok=True)
        code, out, _ = self.clean(worktree=self.tmp / "plain")
        self.assertEqual(1, code)
        self.assertIn("is not a registered worktree", out)

    def test_main_checkout_is_refused(self):
        code, out, _ = self.clean(worktree=self.repo.root)
        self.assertEqual(1, code)
        self.assertIn("that is the main checkout", out)
        self.assertTrue(self.repo.root.is_dir())

    def test_detached_worktree_is_refused(self):
        detached = self.tmp / "detached"
        self.repo.git("worktree", "add", "-q", "--detach", str(detached))
        self.addCleanup(self.repo.git, "worktree", "remove", "--force", str(detached))
        code, out, _ = self.clean(worktree=detached)
        self.assertEqual(1, code)
        self.assertIn("the worktree is detached", out)
        self.assertEqual([], self.gh_calls)

    def test_modified_tracked_file_is_refused(self):
        (self.worktree / "readme.txt").write_bytes(b"changed\n")
        self.addCleanup(self.repo.git, "checkout", "--", "readme.txt", cwd=self.worktree)
        code, out, _ = self.clean()
        self.assertEqual(1, code)
        self.assertIn("uncommitted or untracked changes", out)
        self.assertEqual([], self.gh_calls)

    def test_untracked_file_is_refused(self):
        scratch = self.worktree / "scratch.txt"
        scratch.write_bytes(b"x\n")
        self.addCleanup(scratch.unlink)
        code, out, _ = self.clean()
        self.assertEqual(1, code)
        self.assertIn("scratch.txt", out)
        self.assertEqual([], self.gh_calls)

    def test_pr_that_is_not_open_or_merged_is_refused(self):
        code, out, _ = self.clean(gh=self.pr(state="CLOSED"))
        self.assertEqual(1, code)
        self.assertIn("PR #7 is CLOSED", out)
        self.assertTrue(self.worktree.is_dir())

    def test_branch_mismatch_is_refused(self):
        code, out, _ = self.clean(gh=self.pr(headRefName="task/other"))
        self.assertEqual(1, code)
        self.assertIn(f"head branch is task/other, the worktree is on {BRANCH}", out)

    def test_head_mismatch_is_refused(self):
        code, out, _ = self.clean(gh=self.pr(headRefOid="0" * 40))
        self.assertEqual(1, code)
        self.assertIn(f"PR #7 head is {'0' * 12} but the worktree HEAD is {self.head[:12]}", out)
        self.assertTrue(self.worktree.is_dir())

    def test_failing_gh_exits_2(self):
        code, out, _ = self.clean(gh=(1, "gh: authentication required\n"))
        self.assertEqual(2, code)
        self.assertIn("authentication required", out)
        self.assertTrue(self.worktree.is_dir())

    def test_failing_git_exits_2(self):
        outside = self.tmp / "outside"
        outside.mkdir(exist_ok=True)
        with mock.patch.dict(os.environ, {"GIT_CEILING_DIRECTORIES": str(self.tmp)}):
            code, out, _ = self.clean(worktree=outside, cwd=outside)
        self.assertEqual(2, code)
        self.assertIn("not a git repository", out)

    def test_dry_run_exits_0_and_removes_nothing(self):
        code, out, _ = self.clean("--dry-run")
        self.assertEqual(0, code)
        self.assertIn(f"PR #7 OPEN at {self.head[:12]} == worktree {BRANCH}; worktree clean.", out)
        self.assertIn("dry run: would run `git worktree remove", out)
        self.assertTrue(self.worktree.is_dir())
        self.assertTrue(self.branches())

    def test_a_merged_pr_is_accepted(self):
        code, out, _ = self.clean("--dry-run", gh=self.pr(state="MERGED"))
        self.assertEqual(0, code)
        self.assertIn("PR #7 MERGED at", out)


class CleanupWorktreeRemoval(OwnRepository):
    def test_removal_deletes_the_folder_and_the_branch(self):
        code, out, _ = self.clean()
        self.assertEqual(0, code)
        self.assertIn(f"local branch {BRANCH}; origin/{BRANCH} remains for PR #7", out)
        self.assertFalse(self.worktree.exists())
        self.assertEqual("", self.branches())
        self.assertEqual("main", self.repo.git("rev-parse", "--abbrev-ref", "HEAD"))

    def test_a_branch_the_main_checkout_does_not_contain_is_still_deleted(self):
        # git branch -d refuses it; the PR holds its commits, so the script falls back to -D.
        self.head = Repository(self.worktree, initialise=False).commit("second", {"second.txt": "second\n"})
        code, out, _ = self.clean(gh=self.pr(headRefOid=self.head))
        self.assertEqual(0, code)
        self.assertFalse(self.worktree.exists())
        self.assertEqual("", self.branches())


if __name__ == "__main__":
    unittest.main()
