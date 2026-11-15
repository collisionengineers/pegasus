#!/usr/bin/env python3
"""Remove a finished task worktree and its local branch, only when it is safe.

Usage:
    python cleanup_worktree.py --pr 1137 --worktree ../pegasus-worktrees/my-slug
    python cleanup_worktree.py --pr 1137 --worktree ../pegasus-worktrees/my-slug --dry-run

Run it from the main checkout. It refuses unless all of these hold:
- the merge request is opened (or merged), its source branch is the worktree's
  branch and its head SHA equals the worktree's HEAD, so the commits being
  thrown away locally are exactly the ones GitLab holds;
- the worktree has no tracked or untracked changes;
- the worktree is a linked worktree, not the main checkout;
- the branch is not checked out anywhere else.

Then: `git worktree remove <path>` and `git branch -d <branch>`. The branch
stays on the remote for the merge request. Ignored files under the worktree
(for example `artifacts/`) are deleted with it, so copy out anything worth
keeping first.

Exit codes: 0 = removed (or would be, with --dry-run); 1 = refused; 2 = error.
"""
from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
from urllib.parse import quote


def run(args: list[str], cwd: str | None = None) -> tuple[int, str, str]:
    result = subprocess.run(args, capture_output=True, text=True, encoding="utf-8", errors="replace", cwd=cwd)
    return result.returncode, result.stdout or "", result.stderr or ""


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--pr", type=int, required=True, help="merge request number (!N)")
    parser.add_argument("--worktree", required=True, help="path of the worktree to remove")
    parser.add_argument("--repo", help="group/name; defaults to the current repository")
    parser.add_argument("--dry-run", action="store_true")
    opts = parser.parse_args()
    project = "projects/" + (quote(opts.repo, safe="") if opts.repo else ":id")

    path = os.path.abspath(opts.worktree)
    if not os.path.isdir(path):
        print(f"refused: {path} is not a directory")
        return 1

    code, out, err = run(["git", "worktree", "list", "--porcelain"])
    if code != 0:
        print(out + err)
        return 2
    entries = [block.splitlines() for block in out.strip().split("\n\n") if block.strip()]
    main_path = os.path.normcase(os.path.abspath(entries[0][0].split(" ", 1)[1]))
    target = None
    for block in entries:
        wt = os.path.normcase(os.path.abspath(block[0].split(" ", 1)[1]))
        if wt == os.path.normcase(path):
            target = {line.split(" ", 1)[0]: (line.split(" ", 1)[1] if " " in line else "") for line in block}
            break
    if target is None:
        print(f"refused: {path} is not a registered worktree")
        return 1
    if os.path.normcase(path) == main_path:
        print("refused: that is the main checkout")
        return 1
    branch = target.get("branch", "").replace("refs/heads/", "")
    head = target.get("HEAD", "")
    if not branch:
        print("refused: the worktree is detached; remove it by hand if that is intended")
        return 1

    code, status, err = run(["git", "status", "--porcelain=v2", "--untracked-files=all"], cwd=path)
    if code != 0:
        print(status + err)
        return 2
    if status.strip():
        print("refused: the worktree has uncommitted or untracked changes:")
        print(status.strip())
        return 1

    code, out, err = run(["glab", "api", f"{project}/merge_requests/{opts.pr}"])
    if code != 0:
        print(out + err)
        return 2
    mr = json.loads(out)
    if mr["state"] not in {"opened", "merged"}:
        print(f"refused: merge request !{opts.pr} is {mr['state']}")
        return 1
    if mr["source_branch"] != branch:
        print(f"refused: merge request !{opts.pr} source branch is {mr['source_branch']}, the worktree is on {branch}")
        return 1
    if mr["sha"] != head:
        print(f"refused: merge request !{opts.pr} head is {mr['sha'][:12]} but the worktree HEAD is {head[:12]}; push or reconcile first")
        return 1

    # Traditional --ignored names a wholly ignored directory once (`!! bin/`), not every file in it.
    code, ignored, err = run(["git", "status", "--porcelain", "--ignored"], cwd=path)
    if code != 0:
        print(ignored + err)
        return 2
    ignored_lines = [l for l in ignored.splitlines() if l.startswith("!!")]
    if ignored_lines:
        print(f"note: {len(ignored_lines)} ignored path(s) under the worktree will be deleted with it, e.g. {ignored_lines[0][3:]}")

    print(f"Merge request !{opts.pr} {mr['state']} at {head[:12]} == worktree {branch}; worktree clean.")
    if opts.dry_run:
        print(f"dry run: would run `git worktree remove {path}` and `git branch -d {branch}`")
        return 0

    code, out, err = run(["git", "worktree", "remove", path])
    if code != 0:
        print(out + err)
        return 2
    code, out, err = run(["git", "branch", "-d", branch])
    if code != 0:
        # The branch's commits are on the remote and in the merge request; -d refuses only
        # because the integration branch does not contain them yet.
        code, out, err = run(["git", "branch", "-D", branch])
        if code != 0:
            print(out + err)
            return 2
    print(f"removed worktree {path} and local branch {branch}; origin/{branch} remains for merge request !{opts.pr}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
