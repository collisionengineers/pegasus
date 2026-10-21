#!/usr/bin/env python3
"""Dry-run a merge train without touching any worktree, index or ref.

Chains `git merge-tree --write-tree` and `git commit-tree` from the target
through each PR head in order, reporting at which step and in which files the
train conflicts. Optionally checks every PR against the target alone and every
pair, so you can tell an order problem from a genuine collision.

Usage:
    python simulate_merge.py --target <sha> 1016=<sha> 1018=<sha> ...
    python simulate_merge.py --target origin/dev --pairwise 41=<sha> 42=<sha>

Needs git >= 2.38. Writes only unreferenced objects (garbage-collected later).
Exit codes: 0 = clean train, 1 = at least one conflict, 3 = usage/git error.
"""
from __future__ import annotations

import argparse
import itertools
import os
import subprocess
import sys

ENV = {**os.environ,
       "GIT_AUTHOR_NAME": "combine-prs simulation", "GIT_AUTHOR_EMAIL": "simulation@localhost",
       "GIT_COMMITTER_NAME": "combine-prs simulation", "GIT_COMMITTER_EMAIL": "simulation@localhost"}


def git(*args: str) -> subprocess.CompletedProcess:
    return subprocess.run(["git", *args], capture_output=True, text=True, encoding="utf-8",
                          errors="replace", env=ENV)


def resolve(rev: str) -> str:
    result = git("rev-parse", "--verify", f"{rev}^{{commit}}")
    if result.returncode != 0:
        raise SystemExit(f"error: cannot resolve {rev}: {result.stderr.strip()}")
    return result.stdout.strip()


def merge(ours: str, theirs: str) -> tuple[str, list[str], list[str]]:
    """Return (tree, conflicted files, conflict messages)."""
    result = git("merge-tree", "--write-tree", "--name-only", ours, theirs)
    if result.returncode not in (0, 1):
        raise SystemExit(f"error: git merge-tree failed (git >= 2.38 needed): {result.stderr.strip()}")
    head, _, messages = result.stdout.partition("\n\n")
    lines = head.splitlines()
    tree, files = lines[0], list(dict.fromkeys(lines[1:]))
    notes = [m for m in messages.splitlines() if m.startswith("CONFLICT")]
    return tree, files, notes


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--target", required=True, help="pinned target commit (or ref) the branch starts from")
    parser.add_argument("heads", nargs="+", help="LABEL=REV in merge order, e.g. 1016=3f2a9c1")
    parser.add_argument("--pairwise", action="store_true", help="also test each head alone and every pair")
    opts = parser.parse_args()

    target = resolve(opts.target)
    heads = []
    for item in opts.heads:
        label, sep, rev = item.partition("=")
        if not sep:
            label, rev = item[:12], item
        heads.append((label, resolve(rev)))

    print(f"# Merge simulation from `{target[:12]}`\n")
    print("| Step | Merge | Result | Files |")
    print("|---|---|---|---|")
    current, conflicted_once, details = target, False, []
    for step, (label, sha) in enumerate(heads, 1):
        tree, files, notes = merge(current, sha)
        if files:
            result = "CONFLICT" if not conflicted_once else "CONFLICT (after an unresolved step; may be inflated)"
            conflicted_once = True
            details.append((step, label, notes))
        else:
            result = "clean" if not conflicted_once else "clean (after an unresolved step)"
        print(f"| {step} | #{label} `{sha[:12]}` | {result} | {', '.join(f'`{f}`' for f in files) or '-'} |")
        commit = git("commit-tree", tree, "-p", current, "-p", sha, "-m", f"simulate {label}")
        if commit.returncode != 0:
            raise SystemExit(f"error: git commit-tree failed: {commit.stderr.strip()}")
        current = commit.stdout.strip()

    for step, label, notes in details:
        print(f"\nStep {step} (#{label}):")
        for note in notes:
            print(f"- {note}")

    any_conflict = conflicted_once
    if opts.pairwise:
        print("\n## Each PR against the target alone\n")
        for label, sha in heads:
            _, files, _ = merge(target, sha)
            any_conflict |= bool(files)
            print(f"- #{label}: {'conflicts in ' + ', '.join(files) if files else 'clean'}")
        print("\n## Pairs (each pair merged onto the target)\n")
        for (la, a), (lb, b) in itertools.combinations(heads, 2):
            tree_a, files_a, _ = merge(target, a)
            if files_a:
                print(f"- #{la} × #{lb}: skipped (#{la} conflicts with the target alone)")
                continue
            base = git("commit-tree", tree_a, "-p", target, "-p", a, "-m", "pair").stdout.strip()
            _, files, _ = merge(base, b)
            if files:
                print(f"- #{la} × #{lb}: conflicts in {', '.join(files)}")
        print("(pairs not listed merge cleanly)")

    print(f"\nFinal simulated tree: `{git('rev-parse', current + '^{tree}').stdout.strip()}`"
          + (" (contains conflict markers)" if conflicted_once else ""))
    return 1 if any_conflict else 0


if __name__ == "__main__":
    sys.exit(main())
