"""cleanup_worktree.py on a real temporary repository and linked worktree.

git runs for real. Only `glab` is answered by a fake, patched in at run(): a fake
glab program on PATH is not found on Windows unless it is an .exe. The refusals
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
    def mr(self, **overrides):
        value = {"iid": 7, "state": "opened", "sha": self.head, "source_branch": BRANCH,
                 "web_url": "https://gitlab.com/acme/widgets/-/merge_requests/7"}
        value.update(overrides)
        return (0, json.dumps(value), "")

    def clean(self, *extra, worktree=None, glab=None, ignored_status=None, cwd=None):
        """ignored_status, when given, answers the `git status --ignored` call instead of git."""
        answer = glab if glab is not None else self.mr()

        def run(args, cwd=None):
            if args[0] == "glab":
                self.glab_calls.append(args)
                return answer
            if ignored_status is not None and "--ignored" in args:
                return ignored_status
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
        self.glab_calls = []


class SharedRepository(CleanupHelpers, SharedGitCase):
    @classmethod
    def setUpClass(cls):
        super().setUpClass()
        cls.repo, cls.worktree, cls.head = make_fixture(cls.tmp)

    def setUp(self):
        self.glab_calls = []


class CleanupWorktreeRefusals(SharedRepository):
    def test_missing_directory_is_refused(self):
        code, out, _ = self.clean(worktree=self.tmp / "absent")
        self.assertEqual(1, code)
        self.assertIn("is not a directory", out)
        self.assertEqual([], self.glab_calls)

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
        self.assertEqual([], self.glab_calls)

    def test_modified_tracked_file_is_refused(self):
        (self.worktree / "readme.txt").write_bytes(b"changed\n")
        self.addCleanup(self.repo.git, "checkout", "--", "readme.txt", cwd=self.worktree)
        code, out, _ = self.clean()
        self.assertEqual(1, code)
        self.assertIn("uncommitted or untracked changes", out)
        self.assertEqual([], self.glab_calls)

    def test_untracked_file_is_refused(self):
        scratch = self.worktree / "scratch.txt"
        scratch.write_bytes(b"x\n")
        self.addCleanup(scratch.unlink)
        code, out, _ = self.clean()
        self.assertEqual(1, code)
        self.assertIn("scratch.txt", out)
        self.assertEqual([], self.glab_calls)

    def test_merge_request_that_is_not_opened_or_merged_is_refused(self):
        code, out, _ = self.clean(glab=self.mr(state="closed"))
        self.assertEqual(1, code)
        self.assertIn("merge request !7 is closed", out)
        self.assertTrue(self.worktree.is_dir())

    def test_branch_mismatch_is_refused(self):
        code, out, _ = self.clean(glab=self.mr(source_branch="task/other"))
        self.assertEqual(1, code)
        self.assertIn(f"source branch is task/other, the worktree is on {BRANCH}", out)

    def test_head_mismatch_is_refused(self):
        code, out, _ = self.clean(glab=self.mr(sha="0" * 40))
        self.assertEqual(1, code)
        self.assertIn(f"merge request !7 head is {'0' * 12} but the worktree HEAD is {self.head[:12]}", out)
        self.assertTrue(self.worktree.is_dir())

    def test_failing_glab_exits_2(self):
        code, out, _ = self.clean(glab=(1, "", "glab: 401 Unauthorized\n"))
        self.assertEqual(2, code)
        self.assertIn("401 Unauthorized", out)
        self.assertTrue(self.worktree.is_dir())

    def test_a_glab_warning_on_stderr_beside_valid_json_does_not_change_the_result(self):
        code, out, _ = self.clean("--dry-run", glab=(0, self.mr()[1], "A new version of glab is available\n"))
        self.assertEqual(0, code)
        self.assertIn(f"Merge request !7 opened at {self.head[:12]} == worktree {BRANCH}; worktree clean.", out)

    def test_an_ignored_file_is_named_before_removal(self):
        # Linked worktrees read the main repository's info/exclude, so the commit stays unchanged.
        exclude = self.repo.root / ".git" / "info" / "exclude"
        exclude.parent.mkdir(exist_ok=True)
        before = exclude.read_bytes() if exclude.exists() else b""
        exclude.write_bytes(before + b"\n*.log\n")
        self.addCleanup(exclude.write_bytes, before)
        log = self.worktree / "x.log"
        log.write_bytes(b"x\n")
        self.addCleanup(log.unlink)
        code, out, _ = self.clean("--dry-run")
        self.assertEqual(0, code)
        self.assertIn("note: 1 ignored path(s) under the worktree will be deleted with it, e.g. x.log", out)

    def test_a_failing_ignored_status_exits_2(self):
        code, out, _ = self.clean("--dry-run", ignored_status=(128, "", "fatal: status refused\n"))
        self.assertEqual(2, code)
        self.assertIn("fatal: status refused", out)
        self.assertNotIn("dry run:", out)

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
        self.assertIn(f"Merge request !7 opened at {self.head[:12]} == worktree {BRANCH}; worktree clean.", out)
        self.assertIn("dry run: would run `git worktree remove", out)
        self.assertTrue(self.worktree.is_dir())
        self.assertTrue(self.branches())

    def test_a_merged_merge_request_is_accepted(self):
        code, out, _ = self.clean("--dry-run", glab=self.mr(state="merged"))
        self.assertEqual(0, code)
        self.assertIn("Merge request !7 merged at", out)

    def test_the_merge_request_is_read_from_the_current_project_or_the_encoded_repo(self):
        self.clean("--dry-run")
        self.clean("--dry-run", "--repo", "acme/widgets")
        self.assertEqual([["glab", "api", "projects/:id/merge_requests/7"],
                          ["glab", "api", "projects/acme%2Fwidgets/merge_requests/7"]], self.glab_calls)


class CleanupWorktreeRemoval(OwnRepository):
    def test_removal_deletes_the_folder_and_the_branch(self):
        code, out, _ = self.clean()
        self.assertEqual(0, code)
        self.assertIn(f"local branch {BRANCH}; origin/{BRANCH} remains for merge request !7", out)
        self.assertFalse(self.worktree.exists())
        self.assertEqual("", self.branches())
        self.assertEqual("main", self.repo.git("rev-parse", "--abbrev-ref", "HEAD"))

    def test_a_branch_the_main_checkout_does_not_contain_is_still_deleted(self):
        # git branch -d refuses it; the merge request holds its commits, so the script falls back to -D.
        self.head = Repository(self.worktree, initialise=False).commit("second", {"second.txt": "second\n"})
        code, out, _ = self.clean(glab=self.mr(sha=self.head))
        self.assertEqual(0, code)
        self.assertFalse(self.worktree.exists())
        self.assertEqual("", self.branches())


if __name__ == "__main__":
    unittest.main()
