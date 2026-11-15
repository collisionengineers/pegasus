#!/usr/bin/env python3
"""Gather everything GitLab and git know about one issue, as markdown.

Usage:
    python issue_context.py 1121
    python issue_context.py 1121 --base dev --repo group/name

Prints: the issue (state, labels, author, dates, description), its comments,
the merge requests that mention it (GitLab's related merge requests) and the
issues that mention it (its "mentioned in issue" system notes), and the commits
on the base branch since the issue was opened that touch any path, file or type
the issue names. The last section is what tells you whether the issue's
file:line claims still describe the code.

Exit codes: 0 = investigate; 2 = stop and ask (issue closed, or an open merge
request already references it); 1 = error. Read-only apart from `git fetch`.
"""
from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from urllib.parse import quote

PATH_PATTERN = re.compile(r"`?((?:[\w.-]+/)+[\w.-]+\.\w{1,10})`?")
# A backticked span may hold spaces; it is a path only when the base tip tracks it.
SPACED_PATH_PATTERN = re.compile(r"`((?:[\w. -]+/)+[\w. -]+\.\w{1,10})`")
BARE_FILE_PATTERN = re.compile(r"\b([A-Z]\w+\.(?:cs|cshtml|csproj|ps1|py|ts|js|css|json|md|bicep|sql))\b")
IDENTIFIER_PATTERN = re.compile(r"`([A-Z][A-Za-z0-9]{7,})`")
SOURCE_GLOBS = ["*.cs", "*.cshtml", "*.ps1", "*.py", "*.ts", "*.js", "*.bicep"]
GREP_HIT = re.compile(r"([^\0]*)\0([^\n]*)\n")
# The system note GitLab adds to an issue that another issue of the project mentions.
MENTIONED_IN_ISSUE = re.compile(r"mentioned in issue #(\d+)")


def run(args: list[str], check: bool = True) -> str:
    result = subprocess.run(args, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if check and result.returncode != 0:
        raise SystemExit(f"error: {' '.join(args)}\n{result.stderr.strip()}")
    return result.stdout


def glab_json(args: list[str]):
    return json.loads(run(["glab", *args]))


def named_paths(text: str, tree: set[str]) -> list[str]:
    """The paths the text names: dir/file.ext anywhere, and a backticked span that holds spaces and is tracked."""
    spaced = {m.group(1) for m in SPACED_PATH_PATTERN.finditer(text) if " " in m.group(1) and m.group(1) in tree}
    rest = SPACED_PATH_PATTERN.sub(lambda m: " " if m.group(1) in spaced else m.group(0), text)
    return sorted({m.group(1) for m in PATH_PATTERN.finditer(rest)} | spaced)


def identifier_files(identifiers: list[str], base_ref: str) -> dict[str, list[str]]:
    """Identifier -> the source files at base_ref that hold it as a whole word, in tree order, from one git grep."""
    patterns = [arg for ident in identifiers for arg in ("-e", ident)]
    output = run(["git", "grep", "-o", "-z", "-w", *patterns, base_ref, "--", *SOURCE_GLOBS], check=False)
    files: dict[str, dict[str, None]] = {}
    for hit, ident in GREP_HIT.findall(output):  # each hit is `<ref>:<path>`, NUL, the match, newline
        files.setdefault(ident, {})[hit.split(":", 1)[1]] = None
    return {ident: list(paths) for ident, paths in files.items()}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("issue", type=int)
    parser.add_argument("--repo", help="group/name; defaults to the current repository")
    parser.add_argument("--remote", default="origin")
    parser.add_argument("--base", help="integration branch; defaults to the repository default branch")
    parser.add_argument("--no-fetch", action="store_true")
    opts = parser.parse_args()

    project = "projects/" + (quote(opts.repo, safe="") if opts.repo else ":id")
    base = opts.base or glab_json(["api", project])["default_branch"]

    issue_path = f"{project}/issues/{opts.issue}"
    issue = glab_json(["api", issue_path])
    notes = glab_json(["api", f"{issue_path}/notes?sort=asc&order_by=created_at&per_page=100", "--paginate"])
    related = glab_json(["api", f"{issue_path}/related_merge_requests?per_page=100", "--paginate"])

    if not opts.no_fetch:
        run(["git", "fetch", opts.remote, base], check=False)
    base_ref = f"{opts.remote}/{base}"
    base_sha = run(["git", "rev-parse", "--verify", "-q", base_ref], check=False).strip()

    comments = [n for n in notes if not n.get("system")]
    mentions = []  # (issue number, when) from the system notes
    for note in notes:
        match = MENTIONED_IN_ISSUE.fullmatch((note.get("body") or "").strip()) if note.get("system") else None
        if match:
            mentions.append((int(match.group(1)), note.get("created_at")))

    stop = []
    if issue["state"] != "opened":
        stop.append(f"issue is {issue['state']} (closed {issue.get('closed_at')})")
    for mr in (m for m in related if m.get("state") == "opened"):
        stop.append(f"open merge request !{mr['iid']} already references this issue: {mr['title']}")

    out: list[str] = []
    out.append(f"# Issue #{issue['iid']}: {issue['title']}\n")
    out.append(f"- URL: {issue['web_url']}")
    out.append(f"- State: {issue['state']}")
    out.append(f"- Author: {(issue.get('author') or {}).get('username', '?')}; opened {issue['created_at']}; updated {issue['updated_at']}")
    out.append(f"- Labels: {', '.join(issue.get('labels') or []) or 'none'}")
    out.append(f"- Assignees: {', '.join(a['username'] for a in issue.get('assignees') or []) or 'none'}")
    out.append(f"- Base for investigation: `{base_ref}` @ `{base_sha or 'unresolved'}`")
    if stop:
        out.append("\n## Stop and ask\n")
        out.extend(f"- {s}" for s in stop)

    out.append("\n## Body\n")
    out.append(issue.get("description") or "(empty)")

    out.append("\n## Comments\n")
    if not comments:
        out.append("None.")
    for c in comments:
        who = (c.get("author") or {}).get("username", "?")
        out.append(f"### {who} at {c.get('created_at')}\n")
        out.append(c.get("body") or "")
        out.append("")

    out.append("\n## Cross-references\n")
    if not (related or mentions):
        out.append("Nothing references this issue.")
    for mr in related:
        out.append(f"- merge request !{mr['iid']} ({mr.get('state')}): {mr.get('title', '')} {mr.get('web_url', '')}")
    for number, when in mentions:
        out.append(f"- issue #{number} (mentioned {when})")

    text = (issue.get("description") or "") + "\n" + "\n".join(c.get("body") or "" for c in comments)
    listing = run(["git", "ls-tree", "-r", "--name-only", "-z", base_ref], check=False) if base_sha else ""
    tree = [p for p in listing.split("\0") if p]
    tracked = set(tree)
    by_name: dict[str, list[str]] = {}
    for path in tree:
        by_name.setdefault(path.rpartition("/")[2], []).append(path)
    paths = named_paths(text, tracked)
    existing = [p for p in paths if p in tracked]
    missing = [p for p in paths if p not in tracked]

    # Issues often name a bare file (`Foo.cs`) or a type (`FooQueries`) rather than a path.
    # Resolve those at the base tip so the commit log below covers them too.
    resolved: dict[str, list[str]] = {}
    for name in sorted({m.group(1) for m in BARE_FILE_PATTERN.finditer(text)} - set(paths)):
        if name in by_name:
            resolved[name] = by_name[name][:5]
    identifiers = sorted({m.group(1) for m in IDENTIFIER_PATTERN.finditer(text)})[:15]
    if identifiers and base_sha:
        found = identifier_files(identifiers, base_ref)
        for ident in identifiers:
            if ident in found:
                resolved[ident] = found[ident][:8]
    existing = list(dict.fromkeys([*existing, *(hit for hits in resolved.values() for hit in hits)]))

    out.append(f"\n## Paths the issue names, and commits on `{base_ref}` since it was opened\n")
    if not paths and not resolved:
        out.append("The issue names no file paths or known identifiers. Find the owning code from the docs index and the symptoms.")
    if missing:
        out.append("Named but not present at the base tip (moved, renamed or never existed): " + ", ".join(f"`{p}`" for p in missing))
    for name, hits in resolved.items():
        out.append(f"- `{name}` resolves to: " + ", ".join(f"`{h}`" for h in hits))
    if existing and base_sha:
        log = run(["git", "log", base_ref, f"--since={issue['created_at']}", "--no-merges", "--format=%h %ad %s", "--date=short", "--",
                   *existing], check=False).strip()
        out.append("")
        if log:
            out.append("These commits touched the named code after the issue was written, so its file:line claims may be stale:\n")
            out.append("```")
            out.append(log)
            out.append("```")
        else:
            out.append("No commit on the base has touched the named code since the issue was opened.")
        out.append("\nNamed code present at the tip: " + ", ".join(f"`{p}`" for p in existing))

    print("\n".join(out))
    return 2 if stop else 0


if __name__ == "__main__":
    sys.exit(main())
