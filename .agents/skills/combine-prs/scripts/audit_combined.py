#!/usr/bin/env python3
"""Prove that a combined branch kept every PR's work and the target's work.

Given the pinned target commit the integration branch was built on, the
integration tip, and each PR's pinned head, it checks:

  1. the target and every PR head are ancestors of the tip;
  2. a file only one PR changed (and nobody else touched) is byte-identical to
     that PR's head;
  3. in a file several parties changed, every non-blank line each PR added is
     still present (unless a PR stacked on top removed it on purpose), and lines
     a PR removed have not come back from nowhere;
  4. files a PR deleted are gone;
  5. the target's own content survived: no file changed that no PR or fix
     commit changed, and every target line that vanished was removed by a PR or
     fix commit;
  6. integration-only commits (fixes and conflict resolutions) are listed, with
     any line that no PR, fix or the target wrote.

Usage:
    python audit_combined.py --target <sha> [--tip HEAD] 1016=<sha> 1018=<sha> ...

FAIL items must be fixed or explained in the PR body; REVIEW items need a look.
Exit codes: 0 = pass, 1 = at least one FAIL, 3 = REVIEW items only.
Needs git >= 2.38. Changes no ref, index or worktree; redoing each merge
writes only unreferenced objects (garbage-collected later).
"""
from __future__ import annotations

import argparse
import subprocess
import sys
from collections import Counter, defaultdict

GIT = ["git", "-c", "core.quotePath=false"]


def git(*args: str, ok: tuple[int, ...] = (0,)) -> str:
    result = subprocess.run([*GIT, *args], capture_output=True, text=True, encoding="utf-8", errors="replace")
    if result.returncode not in ok:
        raise SystemExit(f"error: git {' '.join(args)}: {result.stderr.strip()}")
    return result.stdout


def is_ancestor(a: str, b: str) -> bool:
    return subprocess.run([*GIT, "merge-base", "--is-ancestor", a, b]).returncode == 0


def resolve(rev: str) -> str:
    return git("rev-parse", "--verify", f"{rev}^{{commit}}").strip()


_trees: dict[str, dict[str, str]] = {}


def tree(rev: str) -> dict[str, str]:
    """path -> blob id for every file at rev."""
    if rev not in _trees:
        entries = {}
        for record in git("ls-tree", "-r", "-z", rev).split("\0"):
            if record:
                meta, _, path = record.partition("\t")
                entries[path] = meta.split()[2]
        _trees[rev] = entries
    return _trees[rev]


_blobs: dict[str, Counter] = {}


def line_counts(blob: str | None) -> Counter:
    if blob is None:
        return Counter()
    if blob not in _blobs:
        _blobs[blob] = Counter(normal(line) for line in git("cat-file", "blob", blob).splitlines() if normal(line))
    return _blobs[blob]


def normal(line: str) -> str:
    """Compare lines ignoring indentation, whitespace runs and a trailing list separator,
    so re-indenting or appending to a list does not read as lost work."""
    return " ".join(line.split()).rstrip(",;")


class Delta:
    """Per-path line additions and removals between two commits."""

    def __init__(self, a: str, b: str):
        self.status: dict[str, str] = {}
        parts = git("diff", "--name-status", "--no-renames", "-z", a, b).split("\0")
        for i in range(0, len(parts) - 1, 2):
            self.status[parts[i + 1]] = parts[i][0]
        self.binary = {line.split("\t", 2)[2] for line in git("diff", "--numstat", "--no-renames", a, b).splitlines()
                       if line.startswith("-\t-\t")}
        self.added: dict[str, Counter] = defaultdict(Counter)
        self.removed: dict[str, Counter] = defaultdict(Counter)
        path, header = None, False
        for line in git("diff", "-U0", "--no-color", "--no-renames", "--no-ext-diff", a, b).splitlines():
            if line.startswith("diff --git "):
                header, path = True, None
            elif header and line.startswith("--- "):
                # git ends the ---/+++ line of a path that holds a space with a tab.
                path = line[6:].rstrip("\t") if line.startswith("--- a/") else path
            elif header and line.startswith("+++ "):
                path = line[6:].rstrip("\t") if line.startswith("+++ b/") else path
            elif line.startswith("@@"):
                header = False
            elif not header and path is not None and line[:1] in "+-":
                text = normal(line[1:])
                if text:
                    (self.added if line[0] == "+" else self.removed)[path][text] += 1

    @property
    def paths(self) -> set[str]:
        return set(self.status)


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--target", required=True, help="pinned target commit the integration branch contains")
    parser.add_argument("--tip", default="HEAD", help="integration branch tip (default HEAD)")
    parser.add_argument("heads", nargs="+", help="LABEL=REV for each PR, e.g. 1016=3f2a9c1")
    parser.add_argument("--max-examples", type=int, default=5, help="example lines shown per finding")
    opts = parser.parse_args()

    target, tip = resolve(opts.target), resolve(opts.tip)
    prs = []
    for item in opts.heads:
        label, sep, rev = item.partition("=")
        if not sep:
            label, rev = item[:12], item
        head = resolve(rev)
        base = git("merge-base", target, head).strip()
        prs.append({"label": label, "head": head, "base": base, "delta": Delta(base, head),
                    "target_delta": Delta(base, target)})

    fails: list[str] = []
    reviews: list[str] = []
    shown = opts.max_examples

    def sample(lines) -> str:
        lines = list(lines)
        text = "; ".join(f"`{l.strip()[:100]}`" for l in lines[:shown])
        return text + (f" (+{len(lines) - shown} more)" if len(lines) > shown else "")

    if not is_ancestor(target, tip):
        fails.append(f"target `{target[:12]}` is not an ancestor of the tip: the branch was not built on it")

    # Integration-only commits: fixes and merge resolutions that belong to no PR.
    exclude = [target] + [p["head"] for p in prs]
    fix_commits = git("rev-list", "--no-merges", tip, "--not", *exclude).split()
    merge_commits = git("rev-list", "--merges", tip, "--not", *exclude).split()
    fix_deltas = [(c, Delta(f"{c}^", c)) for c in fix_commits]
    fix_paths = set().union(*(d.paths for _, d in fix_deltas)) if fix_deltas else set()
    resolved_paths: dict[str, list[str]] = defaultdict(list)
    for m in merge_commits:
        parents = git("rev-list", "--parents", "-n", "1", m).split()[1:]
        if len(parents) == 2:
            # Redo git's automatic merge of the parents in memory (exit 1 = it conflicted): a path
            # the merge commit holds differently was resolved or edited by hand.
            auto_tree = git("merge-tree", "--write-tree", *parents, ok=(0, 1)).split("\n", 1)[0]
            changed = git("diff-tree", "-r", "--name-only", auto_tree, m)
        else:
            changed = git("diff-tree", "--cc", "--name-only", "--no-commit-id", "-r", m)
        for path in changed.split("\n"):
            if path.strip():
                resolved_paths[path.strip()].append(m)

    tip_tree, target_tree = tree(tip), tree(target)
    touchers: dict[str, list[str]] = defaultdict(list)
    for p in prs:
        for path in p["delta"].paths:
            touchers[path].append(p["label"])

    rows = []
    for p in prs:
        label, head, delta = p["label"], p["head"], p["delta"]
        head_tree, base_tree = tree(head), tree(p["base"])
        in_tip = is_ancestor(head, tip)
        if not in_tip:
            fails.append(f"#{label}: head `{head[:12]}` is not in the tip (squashed, rebased or not merged)")
        descendants = [q for q in prs if q is not p and q["head"] != head and is_ancestor(head, q["head"])]
        exact = line_checked = missing_total = deleted_ok = 0
        for path, status in sorted(delta.status.items()):
            others = [t for t in touchers[path] if t != label]
            target_changed = target_tree.get(path) != base_tree.get(path)
            if status == "D":
                if path not in tip_tree:
                    deleted_ok += 1
                elif others or path in fix_paths:
                    reviews.append(f"#{label} deleted `{path}` but it exists at the tip; also changed by "
                                   f"{', '.join('#' + o for o in others) or 'a fix commit'}")
                else:
                    fails.append(f"#{label} deleted `{path}` but it is back at the tip")
                continue
            if path not in tip_tree:
                (reviews if others or path in fix_paths else fails).append(
                    f"#{label} added/changed `{path}` but it is missing at the tip")
                continue
            if not others and path not in fix_paths and not target_changed:
                if tip_tree[path] == head_tree.get(path):
                    exact += 1
                else:
                    fails.append(f"#{label}: `{path}` was changed only by this PR but differs from its head"
                                 + (" (edited in a merge commit)" if path in resolved_paths else ""))
                continue
            if path in delta.binary:
                if tip_tree[path] != head_tree.get(path):
                    reviews.append(f"#{label}: binary `{path}` is shared and differs from this PR's head")
                continue
            line_checked += 1
            tip_lines = line_counts(tip_tree[path])
            missing = [l for l in delta.added[path] if tip_lines[l] == 0]
            superseded = [l for l in missing
                          if any(line_counts(tree(q["head"]).get(path))[l] == 0 for q in descendants)]
            missing = [l for l in missing if l not in superseded]
            if missing:
                missing_total += len(missing)
                fails.append(f"#{label}: {len(missing)} added line(s) missing from `{path}`: {sample(missing)}")
            if superseded:
                names = ", ".join("#" + q["label"] for q in descendants)
                reviews.append(f"#{label}: {len(superseded)} line(s) in `{path}` removed by stacked {names}: "
                               f"{sample(superseded)}")
            other_added = Counter()
            for q in prs:
                if q is not p:
                    other_added.update(q["delta"].added.get(path, {}))
            other_added.update(p["target_delta"].added.get(path, {}))
            for _, fd in fix_deltas:
                other_added.update(fd.added.get(path, {}))
            head_lines = line_counts(head_tree.get(path))
            back = [l for l in delta.removed[path]
                    if len(l.strip()) > 3 and tip_lines[l] > head_lines[l] and other_added[l] == 0]
            if back:
                reviews.append(f"#{label}: {len(back)} line(s) this PR removed are back in `{path}`: {sample(back)}")
        rows.append((label, head, in_tip, len(delta.status), exact, line_checked, missing_total, deleted_ok))

    # The target's own work: every change from target to tip must be explained.
    pr_paths = set(touchers)
    combined = Delta(target, tip)
    for path in sorted(combined.paths - pr_paths - fix_paths):
        fails.append(f"`{path}` changed between the target and the tip, but no PR or fix commit changed it")
    explained_removed, explained_added = defaultdict(Counter), defaultdict(Counter)
    for p in prs:
        for path, counts in p["delta"].removed.items():
            explained_removed[path].update(counts)
        for path, counts in p["delta"].added.items():
            explained_added[path].update(counts)
    for _, fd in fix_deltas:
        for path, counts in fd.removed.items():
            explained_removed[path].update(counts)
        for path, counts in fd.added.items():
            explained_added[path].update(counts)
    for path in sorted(combined.paths & (pr_paths | fix_paths)):
        if path in combined.binary:
            continue
        lost = [l for l in combined.removed[path] if explained_removed[path][l] == 0]
        if lost:
            fails.append(f"`{path}`: {len(lost)} target line(s) gone that no PR or fix removed: {sample(lost)}")
        novel = [l for l in combined.added[path] if explained_added[path][l] == 0]
        if novel:
            reviews.append(f"`{path}`: {len(novel)} line(s) written only in a merge resolution: {sample(novel)}")

    verdict = "FAIL" if fails else ("REVIEW" if reviews else "PASS")
    print(f"## Lost-work audit: {verdict}\n")
    print(f"Target `{target[:12]}` → tip `{tip[:12]}`; "
          f"target {'is' if is_ancestor(target, tip) else 'is NOT'} an ancestor of the tip.\n")
    print("| PR | Head | Head in tip | Files | Byte-identical | Line-checked | Missing lines | Deletions kept |")
    print("|---|---|---|---|---|---|---|---|")
    for label, head, in_tip, files, exact, checked, missing, deleted in rows:
        print(f"| #{label} | `{head[:12]}` | {'yes' if in_tip else 'NO'} | {files} | {exact} | {checked} | "
              f"{missing} | {deleted} |")

    print(f"\nIntegration-only commits: {len(merge_commits)} merge(s), {len(fix_commits)} fix commit(s).")
    for c, _ in fix_deltas:
        print(f"- fix `{c[:12]}` {git('log', '-1', '--format=%s', c).strip()}")
    if resolved_paths:
        print("\nFiles hand-resolved in merge commits:")
        for path, commits in sorted(resolved_paths.items()):
            print(f"- `{path}` in {', '.join(c[:12] for c in commits)}")

    if fails:
        print("\n### FAIL (fix, or explain in the PR body)\n")
        print("\n".join(f"- {f}" for f in fails))
    if reviews:
        print("\n### REVIEW\n")
        print("\n".join(f"- {r}" for r in reviews))
    print("\nTextual audit only: a clean result does not prove the union builds or behaves; build it and let CI test it.")
    return 1 if fails else (3 if reviews else 0)


if __name__ == "__main__":
    sys.exit(main())
