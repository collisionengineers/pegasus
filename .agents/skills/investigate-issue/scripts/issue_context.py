#!/usr/bin/env python3
"""Gather everything GitHub and git know about one issue, as markdown.

Usage:
    python issue_context.py 1121
    python issue_context.py 1121 --base dev --repo owner/name

Prints: the issue (state, labels, author, dates, body), its comments, the PRs
and issues that reference it (timeline cross-references), and the commits on
the base branch since the issue was opened that touch any path, file or type
the issue names. The last section is what tells you whether the issue's
file:line claims still describe the code.

Exit codes: 0 = investigate; 2 = stop and ask (issue closed, or an open PR
already references it); 1 = error. Read-only apart from `git fetch`.
"""
from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys

PATH_PATTERN = re.compile(r"`?((?:[\w.-]+/)+[\w.-]+\.\w{1,10})`?")
BARE_FILE_PATTERN = re.compile(r"\b([A-Z]\w+\.(?:cs|cshtml|csproj|ps1|py|ts|js|css|json|md|bicep|sql))\b")
IDENTIFIER_PATTERN = re.compile(r"`([A-Z][A-Za-z0-9]{7,})`")
SOURCE_GLOBS = ["*.cs", "*.cshtml", "*.ps1", "*.py", "*.ts", "*.js", "*.bicep"]


def run(args: list[str], check: bool = True) -> str:
    result = subprocess.run(args, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if check and result.returncode != 0:
        raise SystemExit(f"error: {' '.join(args)}\n{result.stderr.strip()}")
    return result.stdout


def gh_json(args: list[str]):
    return json.loads(run(["gh", *args]))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("issue", type=int)
    parser.add_argument("--repo", help="owner/name; defaults to the current repository")
    parser.add_argument("--remote", default="origin")
    parser.add_argument("--base", help="integration branch; defaults to the repository default branch")
    parser.add_argument("--no-fetch", action="store_true")
    opts = parser.parse_args()

    repo_args = ["--repo", opts.repo] if opts.repo else []
    repo = opts.repo or run(["gh", "repo", "view", "--json", "nameWithOwner", "--jq", ".nameWithOwner"]).strip()
    default_branch = run(["gh", "repo", "view", *repo_args, "--json", "defaultBranchRef",
                          "--jq", ".defaultBranchRef.name"]).strip()
    base = opts.base or default_branch

    issue = gh_json(["issue", "view", str(opts.issue), *repo_args, "--json",
                     "number,title,state,stateReason,url,author,createdAt,updatedAt,closedAt,labels,assignees,body,comments"])

    if not opts.no_fetch:
        run(["git", "fetch", opts.remote, base], check=False)
    base_ref = f"{opts.remote}/{base}"
    base_sha = run(["git", "rev-parse", "--verify", "-q", base_ref], check=False).strip()

    timeline = gh_json(["api", f"repos/{repo}/issues/{opts.issue}/timeline", "--paginate"])
    xrefs = []
    for event in timeline:
        if event.get("event") != "cross-referenced":
            continue
        src = (event.get("source") or {}).get("issue") or {}
        if not src:
            continue
        is_pr = bool(src.get("pull_request"))
        state = src.get("state", "").upper()
        if is_pr and (src.get("pull_request") or {}).get("merged_at"):
            state = "MERGED"
        xrefs.append({"kind": "PR" if is_pr else "issue", "number": src.get("number"),
                      "title": src.get("title", ""), "state": state, "url": src.get("html_url", "")})

    stop = []
    if issue["state"] != "OPEN":
        stop.append(f"issue is {issue['state']} ({issue.get('stateReason') or 'no reason'}, closed {issue.get('closedAt')})")
    for pr in (x for x in xrefs if x["kind"] == "PR" and x["state"] == "OPEN"):
        stop.append(f"open PR #{pr['number']} already references this issue: {pr['title']}")

    out: list[str] = []
    out.append(f"# Issue #{issue['number']}: {issue['title']}\n")
    out.append(f"- URL: {issue['url']}")
    out.append(f"- State: {issue['state']}" + (f" ({issue['stateReason']})" if issue.get("stateReason") else ""))
    out.append(f"- Author: {(issue.get('author') or {}).get('login', '?')}; opened {issue['createdAt']}; updated {issue['updatedAt']}")
    out.append(f"- Labels: {', '.join(l['name'] for l in issue.get('labels') or []) or 'none'}")
    out.append(f"- Assignees: {', '.join(a['login'] for a in issue.get('assignees') or []) or 'none'}")
    out.append(f"- Base for investigation: `{base_ref}` @ `{base_sha or 'unresolved'}`")
    if stop:
        out.append("\n## Stop and ask\n")
        out.extend(f"- {s}" for s in stop)

    out.append("\n## Body\n")
    out.append(issue.get("body") or "(empty)")

    out.append("\n## Comments\n")
    comments = issue.get("comments") or []
    if not comments:
        out.append("None.")
    for c in comments:
        who = (c.get("author") or {}).get("login", "?")
        out.append(f"### {who} at {c.get('createdAt')}\n")
        out.append(c.get("body") or "")
        out.append("")

    out.append("\n## Cross-references\n")
    if not xrefs:
        out.append("Nothing references this issue.")
    for x in xrefs:
        out.append(f"- {x['kind']} #{x['number']} ({x['state']}): {x['title']} {x['url']}")

    text = (issue.get("body") or "") + "\n" + "\n".join(c.get("body") or "" for c in comments)
    paths = sorted({m.group(1) for m in PATH_PATTERN.finditer(text)})
    tree = run(["git", "ls-tree", "-r", "--name-only", base_ref], check=False).split() if base_sha else []
    existing = [p for p in paths if p in tree]
    missing = [p for p in paths if p not in tree]

    # Issues often name a bare file (`Foo.cs`) or a type (`FooQueries`) rather than a path.
    # Resolve those at the base tip so the commit log below covers them too.
    resolved: dict[str, list[str]] = {}
    for name in sorted({m.group(1) for m in BARE_FILE_PATTERN.finditer(text)} - set(paths)):
        hits = [p for p in tree if p.endswith("/" + name) or p == name]
        if hits:
            resolved[name] = hits[:5]
    for ident in sorted({m.group(1) for m in IDENTIFIER_PATTERN.finditer(text)})[:15]:
        if base_sha:
            hits = run(["git", "grep", "-l", "-w", ident, base_ref, "--", *SOURCE_GLOBS], check=False).split()
            hits = [h.split(":", 1)[1] for h in hits if ":" in h]
            if hits:
                resolved[ident] = hits[:8]
    for hits in resolved.values():
        existing.extend(h for h in hits if h not in existing)

    out.append(f"\n## Paths the issue names, and commits on `{base_ref}` since it was opened\n")
    if not paths and not resolved:
        out.append("The issue names no file paths or known identifiers. Find the owning code from the docs index and the symptoms.")
    if missing:
        out.append("Named but not present at the base tip (moved, renamed or never existed): " + ", ".join(f"`{p}`" for p in missing))
    for name, hits in resolved.items():
        out.append(f"- `{name}` resolves to: " + ", ".join(f"`{h}`" for h in hits))
    if existing and base_sha:
        log = run(["git", "log", base_ref, f"--since={issue['createdAt']}", "--no-merges", "--format=%h %ad %s", "--date=short", "--",
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
