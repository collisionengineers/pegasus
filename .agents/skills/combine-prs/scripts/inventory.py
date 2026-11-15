#!/usr/bin/env python3
"""Inventory merge requests before combining them into one target branch.

Pins each merge request's head SHA, fetches its objects, and reports what
blocks or shapes the combination: state, draft, holds, conflicts, pipeline
jobs, unresolved discussions, closing issues, stacks, ancestry between heads,
and file overlap.

Usage:
    python inventory.py --target dev 1016 1018 1019 1020
    python inventory.py --target main --remote upstream --json-out inv.json 41 42

Exit codes: 0 = no blockers, 2 = blockers listed under "Stop and ask", 1 = error.
Read-only apart from `git fetch` (objects only; no local refs are created).
"""
from __future__ import annotations

import argparse
import itertools
import json
import re
import subprocess
import sys
from collections import defaultdict
from concurrent.futures import ThreadPoolExecutor
from urllib.parse import quote

# A FETCH_HEAD line: <oid> TAB <not-for-merge or empty> TAB <branch 'x' | 'ref'> of <url>.
FETCH_HEAD_LINE = re.compile(r"(?P<oid>\w+)\t[^\t]*\t(?P<branch>branch )?'(?P<ref>[^']+)' of ")

HOLD_PATTERN = re.compile(r"\b(hold|do[ -]?not[ -]?merge|dnm|wip|blocked)\b", re.IGNORECASE)
FAILED = {"failed", "canceled", "canceling"}
# GitLab's default closing pattern, for this project's issues: "Closes #12", "fixes: #3, #4 and #5".
CLOSING_KEYWORDS = re.compile(r"\b(?:clos(?:e[sd]?|ing)|fix(?:e[sd]|ing)?|resolv(?:e[sd]?|ing)|implement(?:s|ed|ing)?)"
                              r":?\s+(?:issues?\s+)?(#\d+(?:(?:\s*,?\s+and\s+|\s*,?\s*)#\d+)*)", re.IGNORECASE)


def run(args: list[str], check: bool = True) -> str:
    result = subprocess.run(args, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if check and result.returncode != 0:
        raise SystemExit(f"error: {' '.join(args)}\n{result.stderr.strip()}")
    return result.stdout


def glab_json(args: list[str]):
    return json.loads(run(["glab", *args]))


def one_line(text: str, limit: int) -> str:
    text = " ".join((text or "").split())
    return text if len(text) <= limit else text[: limit - 1] + "…"


def summarize_checks(jobs: list[dict]) -> tuple[str, list[str]]:
    passed, pending, failed = 0, 0, []
    for job in jobs or []:
        status = job.get("status") or ""
        if status in FAILED:
            failed.append(job.get("name") or "?")
        elif status in {"success", "skipped"} or (status == "manual" and job.get("allow_failure")):
            passed += 1
        else:
            pending += 1  # created, waiting, preparing, pending, running, scheduled, or a blocking manual job
    if not jobs:
        return "none", []
    return f"{passed} ok / {pending} pending / {len(failed)} failed", failed


def closing_issues(description: str | None) -> list[int]:
    """The issue numbers the description closes with a closing keyword."""
    return [int(n) for m in CLOSING_KEYWORDS.finditer(description or "") for n in re.findall(r"#(\d+)", m.group(1))]


def pipeline_jobs(project: str, pipeline_id: int) -> list[dict]:
    """The pipeline's jobs, with each trigger job replaced by the jobs of the child pipeline it started."""
    jobs = glab_json(["api", f"{project}/pipelines/{pipeline_id}/jobs?per_page=100", "--paginate"])
    for bridge in glab_json(["api", f"{project}/pipelines/{pipeline_id}/bridges?per_page=100", "--paginate"]):
        child = bridge.get("downstream_pipeline")
        jobs.extend(pipeline_jobs(project, child["id"]) if child else [bridge])
    return jobs


def read_merge_request(project: str, number: int) -> dict:
    """The merge request as GitLab answers it, with its discussions, closing issues and pipeline jobs added."""
    path = f"{project}/merge_requests/{number}"
    mr = glab_json(["api", path])
    mr["discussions"] = glab_json(["api", f"{path}/discussions?per_page=100", "--paginate"])
    # closes_issues is empty for a merge request whose target is not the default branch, so read the description too.
    listed = {issue["iid"] for issue in glab_json(["api", f"{path}/closes_issues?per_page=100", "--paginate"])}
    mr["closingIssues"] = sorted(listed | set(closing_issues(mr.get("description"))))
    pipeline = mr.get("head_pipeline")
    mr["jobs"] = pipeline_jobs(project, pipeline["id"]) if pipeline else []
    return mr


def review_feedback(mr: dict) -> tuple[list[list[dict]], list[dict]]:
    """The unresolved discussions (each its notes) and the plain comments of anyone but the author."""
    author = (mr.get("author") or {}).get("username")
    threads, comments = [], []
    for discussion in mr.get("discussions") or []:
        notes = [n for n in discussion.get("notes") or [] if not n.get("system")]
        if any(n.get("resolvable") for n in notes):
            if any(n.get("resolvable") and not n.get("resolved") for n in notes):
                threads.append(notes)
        else:
            comments.extend(n for n in notes if (n.get("author") or {}).get("username") != author)
    return threads, comments


def is_fork(mr: dict) -> bool:
    return mr["source_project_id"] != mr["target_project_id"]


def fetched_oids() -> dict[str, str]:
    """Ref name -> object id for everything the last `git fetch` brought, read by name from FETCH_HEAD."""
    path = run(["git", "rev-parse", "--git-path", "FETCH_HEAD"]).strip()
    with open(path, encoding="utf-8", errors="replace") as handle:
        matches = [m for m in map(FETCH_HEAD_LINE.match, handle) if m]
    return {("refs/heads/" if m["branch"] else "") + m["ref"]: m["oid"] for m in matches}


def containment_pairs(mrs: list[dict]) -> list[tuple[dict, dict]]:
    """(contained, container) for each merge request whose head is inside the different head of another.

    One `git merge-base --independent` over the distinct heads names the ones no other head contains;
    only the rest can be contained, so only they are put to `--is-ancestor`, against the other heads.
    """
    heads = list(dict.fromkeys(mr["sha"] for mr in mrs))
    independent = set(run(["git", "merge-base", "--independent", *heads]).split())
    inside = {a: {b for b in heads if b != a
                  and subprocess.run(["git", "merge-base", "--is-ancestor", a, b]).returncode == 0}
              for a in heads if a not in independent}
    return [(a, b) for a in mrs for b in mrs if b["sha"] in inside.get(a["sha"], ())]


def containment(mrs: list[dict]) -> list[str]:
    """One line for each merge request whose head contains the different head of another."""
    return [f"!{b['iid']} already contains !{a['iid']}'s head" for a, b in containment_pairs(mrs)]


def register_claims(base: str, head: str, register: str, row: re.Pattern) -> list[str]:
    """The register ids the diff adds on rows it did not also remove (an edited row is not a claim)."""
    added, removed = set(), set()
    for line in run(["git", "diff", "-U0", "--no-renames", base, head, "--", register]).splitlines():
        if line.startswith(("+++", "---")):
            continue
        match = row.match(line[1:]) if line[:1] in "+-" else None
        if match:
            (added if line[0] == "+" else removed).add(match.group(1))
    return sorted(added - removed, key=lambda token: [int(part) if part.isdigit() else part
                                                     for part in re.split(r"(\d+)", token)])


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("prs", nargs="+", type=int, metavar="MR",
                        help="merge request numbers (!N), in the user's preferred order")
    parser.add_argument("--target", required=True, help="target branch name, e.g. dev")
    parser.add_argument("--remote", default="origin", help="remote that hosts the merge requests (default origin)")
    parser.add_argument("--repo", help="group/name; default is the GitLab project of the current directory")
    parser.add_argument("--json-out", help="also write the full inventory as JSON to this path")
    parser.add_argument("--register", default="docs/rulings.md",
                        help="append-only register whose row ids the merge requests may both claim "
                             "(default docs/rulings.md)")
    parser.add_argument("--register-row", default=r"^\|\s*(R[1-9]\d*)\s*\|",
                        help=r"regex of a register row with the id as group 1 (default ^\|\s*(R[1-9]\d*)\s*\|)")
    opts = parser.parse_args()
    row_pattern = re.compile(opts.register_row)

    project = "projects/" + (quote(opts.repo, safe="") if opts.repo else ":id")
    repo = glab_json(["api", project])
    full_name, default_branch = repo["path_with_namespace"], repo.get("default_branch")

    # The merge requests are read side by side, all before the fetch, so the fetch shows a head that
    # moved after its read; then one fetch of the target and every head.
    numbers = list(dict.fromkeys(opts.prs))
    with ThreadPoolExecutor(max_workers=8) as pool:
        read = dict(zip(numbers, pool.map(lambda number: read_merge_request(project, number), numbers)))
    target_ref = f"refs/heads/{opts.target}"
    run(["git", "fetch", "--quiet", opts.remote, target_ref, *(f"refs/merge-requests/{n}/head" for n in opts.prs)])
    oids = fetched_oids()
    target_sha = oids[target_ref]

    mrs: list[dict] = []
    blockers: list[str] = []
    for number in opts.prs:
        mr = dict(read[number])
        fetched = oids[f"refs/merge-requests/{number}/head"]
        if fetched != mr["sha"]:
            blockers.append(f"!{number}: head moved during inventory ({mr['sha'][:12]} -> {fetched[:12]}); rerun")
        head = mr["sha"]
        merge_base = run(["git", "merge-base", target_sha, head]).strip()
        behind, ahead = run(["git", "rev-list", "--left-right", "--count", f"{target_sha}...{head}"]).split()
        mr["mergeBase"] = merge_base
        mr["ahead"], mr["behind"] = int(ahead), int(behind)
        mr["changedFiles"] = [f for f in run(["git", "diff", "--name-only", "--no-renames", merge_base, head]).splitlines() if f]
        mr["checkSummary"], mr["failedChecks"] = summarize_checks(mr["jobs"])
        mr["registerIds"] = (register_claims(merge_base, head, opts.register, row_pattern)
                             if opts.register in mr["changedFiles"] else [])
        mrs.append(mr)

    by_head_ref = {mr["source_branch"]: mr for mr in mrs if not is_fork(mr)}
    for mr in mrs:
        n = mr["iid"]
        flags = []
        if mr["state"] != "opened":
            flags.append(mr["state"])
            blockers.append(f"!{n}: merge request is {mr['state']}")
        if mr["draft"]:
            flags.append("draft")
            blockers.append(f"!{n}: draft")
        labels = mr.get("labels") or []
        if any(HOLD_PATTERN.search(text) for text in labels + [mr["title"]]):
            flags.append("hold?")
            blockers.append(f"!{n}: title or label suggests a hold ({', '.join(labels) or mr['title']})")
        if is_fork(mr):
            flags.append(f"fork:project {mr['source_project_id']}")
        if mr.get("has_conflicts") or mr.get("detailed_merge_status") == "conflict":
            flags.append("conflicts with target")
        if mr["failedChecks"]:
            flags.append("failing checks")
        mr["unresolvedDiscussions"] = len(review_feedback(mr)[0])
        if mr["unresolvedDiscussions"]:
            flags.append("unresolved discussions")
        parent = by_head_ref.get(mr["target_branch"])
        mr["stackParent"] = parent["iid"] if parent else None
        if mr["target_branch"] != opts.target and not parent:
            flags.append(f"base {mr['target_branch']} outside set")
            blockers.append(f"!{n}: based on {mr['target_branch']}, which is neither {opts.target} "
                            "nor a listed merge request")
        mr["flags"] = flags

    # Stacked parents first, otherwise the order given.
    ordered: list[dict] = []
    placed: set[int] = set()

    def place(mr: dict, seen: frozenset = frozenset()) -> None:
        if mr["iid"] in placed or mr["iid"] in seen:
            return
        parent = mr["stackParent"]
        if parent is not None:
            place(next(m for m in mrs if m["iid"] == parent), seen | {mr["iid"]})
        placed.add(mr["iid"])
        ordered.append(mr)

    for mr in mrs:
        place(mr)

    pairs = containment_pairs(mrs)
    contains = [f"!{b['iid']} already contains !{a['iid']}'s head" for a, b in pairs]
    # A head that contains another's claims that one's rows too; they are the contained merge request's.
    for a, b in pairs:
        b["registerIds"] = [i for i in b["registerIds"] if i not in a["registerIds"]]

    out = []
    out.append(f"# Merge request inventory for `{opts.target}`\n")
    out.append(f"Target `{opts.target}` @ `{target_sha}` in {full_name}"
               f" (default branch: `{default_branch}`).\n")
    out.append("| Order | MR | Title | Base | Head ref | Head SHA | Ahead/behind | Merge status | Checks | Review | Flags |")
    out.append("|---|---|---|---|---|---|---|---|---|---|---|")
    for index, mr in enumerate(ordered, 1):
        review = f"{mr['unresolvedDiscussions']} unresolved" if mr["unresolvedDiscussions"] else "-"
        out.append(f"| {index} | !{mr['iid']} | {one_line(mr['title'], 60)} | {mr['target_branch']} | "
                   f"{mr['source_branch']} | `{mr['sha'][:12]}` | +{mr['ahead']}/-{mr['behind']} | "
                   f"{mr['detailed_merge_status']} | {mr['checkSummary']} | {review} | "
                   f"{', '.join(mr['flags']) or '-'} |")

    stacked = [mr for mr in ordered if mr["stackParent"]]
    if stacked or contains:
        out.append("\n## Stacks and ancestry\n")
        for mr in stacked:
            out.append(f"- !{mr['iid']} is stacked on !{mr['stackParent']} (base `{mr['target_branch']}`)")
        for line in contains:
            out.append(f"- {line}")

    out.append("\n## Review feedback to read before merging\n")
    for mr in ordered:
        threads, comments = review_feedback(mr)
        if mr["failedChecks"]:
            out.append(f"- !{mr['iid']} failing checks: {', '.join(mr['failedChecks'])}")
        if not (threads or comments):
            out.append(f"- !{mr['iid']}: no review feedback")
            continue
        out.append(f"- !{mr['iid']}: {len(threads)} unresolved discussion(s), {len(comments)} comment(s) from others")
        for notes in threads:
            first = notes[0]
            who = (first.get("author") or {}).get("username", "?")
            position = first.get("position") or {}
            path = position.get("new_path") or position.get("old_path")
            where = f" `{path}:{position.get('new_line') or position.get('old_line')}`" if path else ""
            out.append(f"  - discussion{where} {who}: {one_line(first.get('body', ''), 160)}")
        for c in comments[:10]:
            out.append(f"  - comment {(c.get('author') or {}).get('username', '?')}: {one_line(c['body'], 160)}")

    out.append("\n## Linked issues\n")
    for mr in ordered:
        issues = [f"#{i}" for i in mr["closingIssues"]]
        out.append(f"- !{mr['iid']}: {', '.join(issues) or 'none linked'}")
    if default_branch and opts.target != default_branch:
        out.append(f"\nClosing keywords only fire on `{default_branch}`; issues for `{opts.target}` need closing by hand.")

    out.append("\n## File overlap\n")
    overlaps = []
    for a, b in itertools.combinations(ordered, 2):
        shared = sorted(set(a["changedFiles"]) & set(b["changedFiles"]))
        if shared:
            overlaps.append((a, b, shared))
    if not overlaps:
        out.append("No two merge requests change the same file.")
    for a, b, shared in overlaps:
        shown = ", ".join(f"`{f}`" for f in shared[:8]) + (" …" if len(shared) > 8 else "")
        out.append(f"- !{a['iid']} × !{b['iid']}: {len(shared)} file(s): {shown}")

    if any(mr["registerIds"] for mr in ordered):
        out.append("\n## Register id collisions\n")
        target_ids = {m.group(1) for m in map(row_pattern.match,
                                               run(["git", "show", f"{target_sha}:{opts.register}"], check=False)
                                               .splitlines()) if m}
        claims: dict[str, list[str]] = defaultdict(list)
        for mr in ordered:
            for token in mr["registerIds"]:
                claims[token].append(f"!{mr['iid']}")
        for token in sorted(target_ids):
            if token in claims:
                claims[token].append("the target")
        collisions = {token: names for token, names in claims.items() if len(names) > 1}
        if not collisions:
            out.append(f"No two merge requests claim the same `{opts.register}` id.")
        for token in sorted(collisions, key=lambda t: [int(p) if p.isdigit() else p for p in re.split(r"(\d+)", t)]):
            names = collisions[token]
            holders = ", ".join(names[:-1]) + f" and {names[-1]}" if names[-1] == "the target" else ", ".join(names)
            out.append(f"- {token}: {holders} (first in order keeps it; renumber the rest after each merge with "
                       "renumber_register.py)")

    out.append("\n## Pinned arguments for simulate_merge.py and audit_combined.py\n")
    out.append("```")
    out.append(f"--target {target_sha} " + " ".join(f"{mr['iid']}={mr['sha']}" for mr in ordered))
    out.append("```")

    if blockers:
        out.append("\n## Stop and ask\n")
        out.extend(f"- {b}" for b in blockers)

    print("\n".join(out))
    if opts.json_out:
        with open(opts.json_out, "w", encoding="utf-8") as handle:
            json.dump({"target": opts.target, "targetSha": target_sha, "repo": full_name,
                       "defaultBranch": default_branch, "order": [m["iid"] for m in ordered],
                       "blockers": blockers, "mergeRequests": ordered}, handle, indent=2)
    return 2 if blockers else 0


if __name__ == "__main__":
    sys.exit(main())
