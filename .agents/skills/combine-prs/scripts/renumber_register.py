#!/usr/bin/env python3
"""Renumber one PR's register ids after its merge into the integration branch.

Several PRs written from the same target each append rows to an append-only
register (by default `docs/rulings.md`, rows `| Rnnn | … |`) with the next
free number, so when they are combined they claim the same ids. The rule
(references/conflict-patterns.md) is: the PR merged first keeps its numbers;
each later PR's new ids move to the next free numbers at the tip, and every
citation of those ids is rewritten on the lines that PR itself added, in any
file, and nowhere else.

Run it in the integration worktree straight after the PR's `--no-ff` merge,
with a clean tree. It edits the worktree and commits nothing; commit its edits
as the PR's renumber fix commit with the subject it prints.

Usage:
    python renumber_register.py --head <pinned sha> --target <target sha> [--label 1794]
    python renumber_register.py --head <sha> --base <parent head> --map R4=R6   # a stacked child
    python renumber_register.py --head <sha> --target <sha> --dry-run

How it decides:
  * The PR's rows are the register rows its diff from the base adds. Each is
    found at HEAD by its text, not its id: the merge has just put this PR's
    duplicate ids into the file, and a merge resolution may already have
    renumbered a row. Every other row at HEAD belongs to the target or an
    earlier PR.
  * Renumbering happens only when one of the PR's ids is also another row's
    id at HEAD. The PR's whole block then shifts, in order, to the next free
    numbers, so range citations such as `R561–R564` stay contiguous.
  * A line is rewritten only if the PR's diff added it, it is in the file at
    HEAD, and it was not in the tip before this PR's merge. Only the mapped
    tokens change; bytes, BOM and each line's own ending are kept.

Exit codes: 0 = applied, or nothing to do; 1 = a row or line cannot be
located, nothing written; 2 = a precondition failed; 3 = applied, but the
items listed need a hand edit (a shared line, a range only partly this PR's).
"""
from __future__ import annotations

import argparse
import os
import re
import subprocess
import sys
from collections import Counter, defaultdict

GIT = ["git", "-c", "core.quotePath=false"]
WORD = r"(?<![A-Za-z0-9_])(?:{})(?![A-Za-z0-9_])"


def git(*args: str, ok: tuple[int, ...] = (0,)) -> bytes:
    result = subprocess.run([*GIT, *args], capture_output=True)
    if result.returncode not in ok:
        raise SystemExit(f"error: git {' '.join(args)}: {result.stderr.decode('utf-8', 'replace').strip()}")
    return result.stdout


def text(*args: str, ok: tuple[int, ...] = (0,)) -> str:
    return git(*args, ok=ok).decode("utf-8", "replace")


def fail(code: int, message: str) -> None:
    print(f"error: {message}", file=sys.stderr)
    raise SystemExit(code)


class Ids:
    """The id pattern as bytes and text regexes, with the number as group 1."""

    def __init__(self, pattern: str):
        if not re.compile(pattern).groups:
            fail(2, "--id-pattern needs one capture group around the number")
        self.text = re.compile(WORD.format(pattern))
        self.bytes = re.compile(WORD.format(pattern).encode())
        # `R550–R558`, `R550 to R557`, `R550-R557`: a range whose ends must move together.
        self.range = re.compile(WORD.format(pattern) + r"\s*(?:–|-|to)\s*" + WORD.format(pattern))
        row = r"^\|\s*(" + WORD.format(pattern) + r")\s*\|"
        self.row = re.compile(row)

    @staticmethod
    def renumbered(match: re.Match, number: int) -> bytes | str:
        token = match.group(0)
        start, end = match.start(1) - match.start(0), match.end(1) - match.start(0)
        digits = str(number)
        return token[:start] + (digits.encode() if isinstance(token, bytes) else digits) + token[end:]

    def mask(self, line: str) -> str:
        return self.text.sub("#", line)

    def row_id(self, line: str) -> tuple[str, int] | None:
        match = self.row.match(line)
        if not match:
            return None
        inner = self.text.search(match.group(1))
        return inner.group(0), int(inner.group(1))


def strip_end(line: bytes) -> bytes:
    return line.rstrip(b"\r\n")


def added_lines(base: str, head: str, path: str | None = None) -> dict[str, list[bytes]]:
    """Path -> the lines `git diff base head` adds, as bytes without their line ending."""
    args = ["diff", "-U0", "--no-color", "--no-ext-diff", "--no-renames", base, head]
    if path is not None:
        args += ["--", path]
    added: dict[str, list[bytes]] = defaultdict(list)
    current = None
    header = False
    for line in git(*args).split(b"\n"):
        if line.startswith(b"diff --git "):
            header, current = True, None
        elif header and line.startswith(b"+++ b/"):
            current = line[6:].rstrip(b"\t").decode("utf-8", "surrogateescape")
        elif line.startswith(b"@@"):
            header = False
        elif not header and current is not None and line.startswith(b"+"):
            added[current].append(strip_end(line[1:]))
    return added


def removed_lines(base: str, head: str, path: str) -> list[bytes]:
    out = []
    header = False
    for line in git("diff", "-U0", "--no-color", "--no-ext-diff", "--no-renames", base, head, "--", path).split(b"\n"):
        if line.startswith(b"diff --git "):
            header = True
        elif line.startswith(b"@@"):
            header = False
        elif not header and line.startswith(b"-"):
            out.append(strip_end(line[1:]))
    return out


def binary_paths(base: str, head: str) -> set[str]:
    return {line.split("\t", 2)[2] for line in text("diff", "--numstat", "--no-renames", base, head).splitlines()
            if line.startswith("-\t-\t")}


def show(rev: str, path: str) -> bytes | None:
    result = subprocess.run([*GIT, "cat-file", "-e", f"{rev}:{path}"], capture_output=True)
    if result.returncode != 0:
        return None
    return git("show", f"{rev}:{path}")


def merge_commit_of(head: str) -> str | None:
    """The commit on HEAD's first-parent chain that merged `head` in."""
    for entry in text("rev-list", "--first-parent", "--parents", "HEAD").splitlines():
        parts = entry.split()
        if head in parts[1:]:
            return parts[0]
    return None


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--head", required=True, help="the PR's pinned head")
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--target", help="the pinned target; the base is its merge base with the head")
    group.add_argument("--base", help="the base to diff from instead (a stacked child passes its parent's head)")
    parser.add_argument("--label", help="the PR number for the summary (default: the head's short sha)")
    parser.add_argument("--register", default="docs/rulings.md", help="the register file (default docs/rulings.md)")
    parser.add_argument("--id-pattern", default=r"R([1-9]\d*)",
                        help=r"id regex with the number as group 1 (default R([1-9]\d*)); matched on word boundaries")
    parser.add_argument("--map", action="append", default=[], metavar="OLD=NEW",
                        help="force a pair (e.g. R4=R6), for a row the text search cannot settle or a parent's renumber")
    parser.add_argument("--dry-run", action="store_true", help="print the summary; write nothing")
    opts = parser.parse_args()

    ids = Ids(opts.id_pattern)
    head = text("rev-parse", "--verify", f"{opts.head}^{{commit}}").strip()
    label = opts.label or head[:12]
    base = (text("merge-base", opts.target, head).strip() if opts.target
            else text("rev-parse", "--verify", f"{opts.base}^{{commit}}").strip())

    # Preconditions: the head is merged, the tree is clean, and the register is a text file at HEAD.
    if subprocess.run([*GIT, "merge-base", "--is-ancestor", head, "HEAD"]).returncode != 0:
        fail(2, f"head {head[:12]} is not an ancestor of HEAD; merge it first")
    if subprocess.run([*GIT, "diff", "--quiet", "HEAD"]).returncode != 0 or text("ls-files", "--others", "--exclude-standard").strip():
        fail(2, "the worktree is not clean; commit or stash first")
    merge = merge_commit_of(head)
    if merge is None:
        fail(2, f"no commit on HEAD's first-parent chain merges {head[:12]}; run this right after the PR's merge")
    before = f"{merge}^1"
    if not os.path.isfile(opts.register):
        fail(2, f"register {opts.register} is not a file at HEAD")

    # 1. The PR's own rows: register rows its diff adds, minus ids it merely edits or that exist at the base.
    register_added = [l.decode("utf-8", "replace") for l in added_lines(base, head, opts.register).get(opts.register, [])]
    register_removed = {ids.row_id(l.decode("utf-8", "replace")) for l in removed_lines(base, head, opts.register)}
    removed_ids = {r[1] for r in register_removed if r}
    base_bytes = show(base, opts.register) or b""
    base_ids = {r[1] for r in (ids.row_id(l) for l in base_bytes.decode("utf-8", "replace").splitlines()) if r}
    own_rows: list[tuple[str, int, str]] = []  # (token, number, row text)
    for row in register_added:
        found = ids.row_id(row)
        if found and found[1] not in removed_ids and found[1] not in base_ids:
            own_rows.append((found[0], found[1], row))

    # 2. Find each of the PR's rows at HEAD by its text.
    with open(opts.register, "rb") as handle:
        head_register = handle.read().decode("utf-8", "replace").splitlines()
    head_rows = [(ids.row_id(l), l) for l in head_register]
    head_rows = [(r, l) for r, l in head_rows if r]
    exact = Counter(l for _, l in head_rows)
    masked = Counter(ids.mask(l) for _, l in head_rows)
    forced: dict[str, str] = {}
    for item in opts.map:
        old, sep, new = item.partition("=")
        if not sep or not ids.text.fullmatch(old) or not ids.text.fullmatch(new):
            fail(2, f"--map {item}: expected OLD=NEW id tokens such as R4=R6")
        forced[old] = new
    problems: list[str] = []
    own_at_head: dict[int, str] = {}   # the PR's number -> the row's current text at HEAD
    already: dict[str, str] = {}       # token -> token: pairs that need no free id (renumbered earlier, or forced)
    own_tokens = {token for token, _, _ in own_rows}
    for old, new in forced.items():
        # A pair for an id that is not this PR's row: a parent's renumber a stacked child cites.
        if old not in own_tokens:
            if not any(r[0] == new for r, _ in head_rows):
                fail(1, f"--map {old}={new}: no row {new} at HEAD")
            already[old] = new
    for token, number, row in own_rows:
        if token in forced:
            new = forced[token]
            if not any(r[0] == new for r, _ in head_rows):
                fail(1, f"--map {token}={new}: no row {new} at HEAD")
            already[token] = new
            continue
        if exact[row] == 1:
            own_at_head[number] = row
        elif exact[row] == 0 and masked[ids.mask(row)] == 1:
            current = next(r for r, l in head_rows if ids.mask(l) == ids.mask(row))
            already[token] = current[0]
        elif exact[row] > 1 or masked[ids.mask(row)] > 1:
            problems.append(f"row {token} appears more than once at HEAD; settle it with --map {token}=<new>")
        else:
            problems.append(f"row {token} is not at HEAD under any id; restore it or pass --map {token}=<new>")
    if problems:
        print("\n".join(f"error: {p}" for p in problems), file=sys.stderr)
        return 1

    # 3. The map: shift the PR's still-unrenumbered block when any of its ids is another row's id.
    own_texts = set(own_at_head.values())
    other_numbers = [r[1] for r, l in head_rows if l not in own_texts]
    mapping: dict[str, str] = dict(already)
    clash = sorted(n for n in own_at_head if n in other_numbers)
    if clash:
        next_free = max(other_numbers + [int(ids.text.search(v).group(1)) for v in already.values()]) + 1
        for number in sorted(own_at_head):
            token = ids.text.search(own_at_head[number]).group(0)
            mapping[token] = ids.renumbered(ids.text.search(token), next_free)
            next_free += 1
    if not mapping:
        print(f"# Renumber #{label}: nothing to do\n\nNo id of #{label}'s {len(own_rows)} register row(s) is claimed by "
              f"another row at HEAD.")
        return 0
    taken = {r[0] for r, l in head_rows if l not in own_texts}
    if len(set(mapping.values())) != len(mapping) or any(v in taken for k, v in mapping.items() if k not in already):
        fail(1, f"the map is not one-to-one or a new id is already taken: {mapping}")
    bytes_map = {k.encode(): v.encode() for k, v in mapping.items()}

    def rewrite(line: bytes) -> bytes:
        return ids.bytes.sub(lambda m: bytes_map.get(m.group(0), m.group(0)), line)

    def partial_range(line: bytes) -> bool:
        """A range whose ids are only partly this PR's must not move with it."""
        for m in ids.range.finditer(line.decode("utf-8", "replace")):
            lo, hi = int(m.group(1)), int(m.group(2))
            span = [ids.renumbered(ids.text.search(m.group(0)), n) for n in range(min(lo, hi), max(lo, hi) + 1)]
            inside = [s in mapping for s in span]
            if any(inside) and not all(inside):
                return True
        return False

    # 4. The lines: every line the PR added that cites a mapped id, found at HEAD and absent before the merge.
    binaries = binary_paths(base, head)
    skipped_binary = sorted(f"`{p}`" for p in binaries
                            if any(k in (show(head, p) or b"") for k in bytes_map))
    plans: dict[str, dict[bytes, bytes]] = {}   # path -> {old line: new line}
    counts: dict[str, int] = {}
    per_id: dict[str, Counter] = defaultdict(Counter)
    shared: list[str] = []
    ranges: list[str] = []
    missing: list[str] = []
    for path, lines in added_lines(base, head).items():
        if path in binaries:
            continue
        candidates = [l for l in dict.fromkeys(lines) if any(k in l for k in bytes_map) and ids.bytes.search(l)]
        candidates = [l for l in candidates if rewrite(l) != l]
        if not candidates:
            continue
        current = show("HEAD", path)
        if current is None:
            missing.extend(f"`{path}`: the file is gone at HEAD" for _ in candidates[:1])
            continue
        head_lines = Counter(strip_end(l) for l in current.splitlines(keepends=True))
        earlier = show(before, path) or b""
        before_lines = Counter(strip_end(l) for l in earlier.splitlines(keepends=True))
        plan: dict[bytes, bytes] = {}
        for old in candidates:
            new = rewrite(old)
            if head_lines[old] == 0:
                if head_lines[new] >= 1:
                    continue  # already rewritten (an earlier run or the merge resolution)
                missing.append(f"`{path}`: `{old.decode('utf-8', 'replace').strip()[:100]}`")
                continue
            if before_lines[old] >= 1:
                shared.append(f"`{path}`: `{old.decode('utf-8', 'replace').strip()[:100]}` (also in the tip before the merge)")
                continue
            if partial_range(old):
                ranges.append(f"`{path}`: `{old.decode('utf-8', 'replace').strip()[:100]}`")
                continue
            plan[old] = new
            counts[path] = counts.get(path, 0) + head_lines[old]
            for token in {m.group(0) for m in ids.bytes.finditer(old)} & bytes_map.keys():
                per_id[token.decode()][path] += head_lines[old]
        if plan:
            plans[path] = plan
    if missing:
        print("\n".join(f"error: line not found at HEAD: {m}" for m in missing), file=sys.stderr)
        return 1

    # 5. Rewrite, keeping each line's own ending and every other byte.
    if not opts.dry_run:
        for path, plan in plans.items():
            with open(path, "rb") as handle:
                data = handle.read()
            out = []
            for raw in data.splitlines(keepends=True):
                body = strip_end(raw)
                out.append(plan[body] + raw[len(body):] if body in plan else raw)
            with open(path, "wb") as handle:
                handle.write(b"".join(out))

    # 6. Summary for the PR body.
    pairs = ", ".join(f"{k}={v}" for k, v in mapping.items())
    print(f"# Renumber #{label}: {pairs}{' (dry run)' if opts.dry_run else ''}\n")
    print("| PR | Was | Now | Files | Lines |")
    print("|---|---|---|---|---|")
    for old, new in mapping.items():
        files = per_id.get(old, Counter())
        print(f"| #{label} | {old} | {new} | {len(files)} | {sum(files.values())} |")
    print("\nFiles:")
    for path in sorted(counts):
        print(f"- `{path}`: {counts[path]} line(s)")
    if not counts:
        print("- none (every citation was already in its renumbered form)")
    for title, items in (("Binary files skipped", skipped_binary), ("Shared lines left for a hand edit", shared),
                         ("Ranges only partly this PR's, left for a hand edit", ranges)):
        if items:
            print(f"\n{title}:")
            print("\n".join(f"- {i}" for i in items))
    print(f"\nSuggested commit subject: fix: renumber #{label}'s register ids ({pairs})")
    print("Commit messages and PR titles that cite the old ids are not rewritten.")
    return 3 if shared or ranges else 0


if __name__ == "__main__":
    sys.exit(main())
