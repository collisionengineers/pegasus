#!/usr/bin/env python3
"""Inventory pull requests before combining them into one target branch.

Pins each PR's head SHA, fetches its objects, and reports what blocks or shapes
the combination: state, draft, holds, mergeability, checks, review feedback,
linked issues, stacks, ancestry between heads, and file overlap.

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

# Every field the report reads, for all the pull requests in one request.
PULL_REQUEST_FIELDS = """
fragment PullRequestFields on PullRequest {
  number title url state isDraft baseRefName headRefName headRefOid isCrossRepository
  headRepositoryOwner { login } mergeable mergeStateStatus reviewDecision author { login }
  labels(first: 100) { nodes { name } }
  closingIssuesReferences(first: 100) { nodes { number } }
  commits(last: 1) { nodes { commit { statusCheckRollup { contexts(first: 100) { nodes {
    ... on CheckRun { name status conclusion }
    ... on StatusContext { context state }
  } } } } } }
  reviewThreads(first: 100) {
    nodes { isResolved isOutdated path line
            comments(first: 1) { nodes { author { login } body } } }
  }
  reviews(first: 100) { nodes { author { login } state body } }
  comments(first: 100) { nodes { author { login } body } }
}
"""
FEEDBACK_KEYS = ("reviewThreads", "reviews", "comments")

# A FETCH_HEAD line: <oid> TAB <not-for-merge or empty> TAB <branch 'x' | 'ref'> of <url>.
FETCH_HEAD_LINE = re.compile(r"(?P<oid>\w+)\t[^\t]*\t(?P<branch>branch )?'(?P<ref>[^']+)' of ")

HOLD_PATTERN = re.compile(r"\b(hold|do[ -]?not[ -]?merge|dnm|wip|blocked)\b", re.IGNORECASE)
FAILED = {"FAILURE", "TIMED_OUT", "CANCELLED", "ACTION_REQUIRED", "STARTUP_FAILURE", "ERROR"}


def run(args: list[str], check: bool = True) -> str:
    result = subprocess.run(args, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if check and result.returncode != 0:
        raise SystemExit(f"error: {' '.join(args)}\n{result.stderr.strip()}")
    return result.stdout


def gh_json(args: list[str]):
    return json.loads(run(["gh", *args]))


def one_line(text: str, limit: int) -> str:
    text = " ".join((text or "").split())
    return text if len(text) <= limit else text[: limit - 1] + "…"


def summarize_checks(rollup: list[dict]) -> tuple[str, list[str]]:
    passed, pending, failed = 0, 0, []
    for item in rollup or []:
        name = item.get("name") or item.get("context") or "?"
        conclusion = (item.get("conclusion") or item.get("state") or "").upper()
        status = (item.get("status") or "").upper()
        if conclusion in FAILED:
            failed.append(name)
        elif (status and status != "COMPLETED") or conclusion in {"PENDING", "EXPECTED", ""}:
            pending += 1
        else:
            passed += 1
    if not rollup:
        return "none", []
    return f"{passed} ok / {pending} pending / {len(failed)} failed", failed


def pull_requests_query(numbers: list[int]) -> str:
    """One query that answers for every pull request, each under the alias `pr<number>`."""
    aliases = "\n".join(f"    pr{n}: pullRequest(number: {n}) {{ ...PullRequestFields }}" for n in dict.fromkeys(numbers))
    return ("query($owner: String!, $name: String!) {\n"
            f"  repository(owner: $owner, name: $name) {{\n{aliases}\n  }}\n}}\n{PULL_REQUEST_FIELDS}")


def flatten(node: dict) -> dict:
    """A pull request as the report reads it: the connections as lists, the review feedback under `feedback`."""
    commit = (node["commits"]["nodes"] or [{}])[0].get("commit") or {}
    contexts = ((commit.get("statusCheckRollup") or {}).get("contexts") or {}).get("nodes") or []
    pr = {key: value for key, value in node.items() if key != "commits" and key not in FEEDBACK_KEYS}
    pr["labels"] = node["labels"]["nodes"]
    pr["closingIssuesReferences"] = node["closingIssuesReferences"]["nodes"]
    pr["statusCheckRollup"] = contexts
    pr["feedback"] = {key: node[key] for key in FEEDBACK_KEYS}
    return pr


def fetched_oids() -> dict[str, str]:
    """Ref name -> object id for everything the last `git fetch` brought, read by name from FETCH_HEAD."""
    path = run(["git", "rev-parse", "--git-path", "FETCH_HEAD"]).strip()
    with open(path, encoding="utf-8", errors="replace") as handle:
        matches = [m for m in map(FETCH_HEAD_LINE.match, handle) if m]
    return {("refs/heads/" if m["branch"] else "") + m["ref"]: m["oid"] for m in matches}


def containment(prs: list[dict]) -> list[str]:
    """One line for each pull request whose head contains the different head of another.

    One `git merge-base --independent` over the distinct heads names the ones no other head contains;
    only the rest can be contained, so only they are put to `--is-ancestor`, against the other heads.
    """
    heads = list(dict.fromkeys(pr["headRefOid"] for pr in prs))
    independent = set(run(["git", "merge-base", "--independent", *heads]).split())
    inside = {a: {b for b in heads if b != a
                  and subprocess.run(["git", "merge-base", "--is-ancestor", a, b]).returncode == 0}
              for a in heads if a not in independent}
    return [f"#{b['number']} already contains #{a['number']}'s head" for a in prs for b in prs
            if b["headRefOid"] in inside.get(a["headRefOid"], ())]


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("prs", nargs="+", type=int, help="PR numbers, in the user's preferred order")
    parser.add_argument("--target", required=True, help="target branch name, e.g. dev")
    parser.add_argument("--remote", default="origin", help="remote that hosts the PRs (default origin)")
    parser.add_argument("--repo", help="owner/name; default is the gh repo for the current directory")
    parser.add_argument("--json-out", help="also write the full inventory as JSON to this path")
    opts = parser.parse_args()

    repo = gh_json(["repo", "view", *([opts.repo] if opts.repo else []), "--json", "owner,name,defaultBranchRef"])
    owner, name = repo["owner"]["login"], repo["name"]
    default_branch = (repo.get("defaultBranchRef") or {}).get("name")

    # Two network calls whatever the number of pull requests: one query for all of them, taken
    # first so the fetch shows a head that moved after it, then one fetch of the target and every head.
    nodes = gh_json(["api", "graphql", "-F", f"owner={owner}", "-F", f"name={name}",
                     "-f", f"query={pull_requests_query(opts.prs)}"])["data"]["repository"]
    target_ref = f"refs/heads/{opts.target}"
    run(["git", "fetch", "--quiet", opts.remote, target_ref, *(f"refs/pull/{n}/head" for n in opts.prs)])
    oids = fetched_oids()
    target_sha = oids[target_ref]

    prs: list[dict] = []
    blockers: list[str] = []
    for number in opts.prs:
        pr = flatten(nodes[f"pr{number}"])
        fetched = oids[f"refs/pull/{number}/head"]
        if fetched != pr["headRefOid"]:
            blockers.append(f"#{number}: head moved during inventory ({pr['headRefOid'][:12]} -> {fetched[:12]}); rerun")
        head = pr["headRefOid"]
        merge_base = run(["git", "merge-base", target_sha, head]).strip()
        behind, ahead = run(["git", "rev-list", "--left-right", "--count", f"{target_sha}...{head}"]).split()
        pr["mergeBase"] = merge_base
        pr["ahead"], pr["behind"] = int(ahead), int(behind)
        pr["changedFiles"] = [f for f in run(["git", "diff", "--name-only", "--no-renames", merge_base, head]).splitlines() if f]
        pr["checkSummary"], pr["failedChecks"] = summarize_checks(pr["statusCheckRollup"])
        prs.append(pr)

    by_head_ref = {pr["headRefName"]: pr for pr in prs if not pr["isCrossRepository"]}
    for pr in prs:
        n = pr["number"]
        flags = []
        if pr["state"] != "OPEN":
            flags.append(pr["state"].lower())
            blockers.append(f"#{n}: PR is {pr['state']}")
        if pr["isDraft"]:
            flags.append("draft")
            blockers.append(f"#{n}: draft")
        labels = [label["name"] for label in pr.get("labels") or []]
        if any(HOLD_PATTERN.search(text) for text in labels + [pr["title"]]):
            flags.append("hold?")
            blockers.append(f"#{n}: title or label suggests a hold ({', '.join(labels) or pr['title']})")
        if pr["isCrossRepository"]:
            flags.append(f"fork:{pr['headRepositoryOwner']['login']}")
        if pr["mergeable"] == "CONFLICTING":
            flags.append("conflicts-with-base (no CI runs)")
        if pr["failedChecks"]:
            flags.append("failing checks")
        if pr["reviewDecision"] == "CHANGES_REQUESTED":
            flags.append("changes requested")
        parent = by_head_ref.get(pr["baseRefName"])
        pr["stackParent"] = parent["number"] if parent else None
        if pr["baseRefName"] != opts.target and not parent:
            flags.append(f"base {pr['baseRefName']} outside set")
            blockers.append(f"#{n}: based on {pr['baseRefName']}, which is neither {opts.target} nor a listed PR")
        pr["flags"] = flags

    # Stacked parents first, otherwise the order given.
    ordered: list[dict] = []
    placed: set[int] = set()

    def place(pr: dict, seen: frozenset = frozenset()) -> None:
        if pr["number"] in placed or pr["number"] in seen:
            return
        parent = pr["stackParent"]
        if parent is not None:
            place(next(p for p in prs if p["number"] == parent), seen | {pr["number"]})
        placed.add(pr["number"])
        ordered.append(pr)

    for pr in prs:
        place(pr)

    contains = containment(prs)

    out = []
    out.append(f"# PR inventory for `{opts.target}`\n")
    out.append(f"Target `{opts.target}` @ `{target_sha}` in {owner}/{name}"
               f" (default branch: `{default_branch}`).\n")
    out.append("| Order | PR | Title | Base | Head ref | Head SHA | Ahead/behind | Mergeable | Checks | Review | Flags |")
    out.append("|---|---|---|---|---|---|---|---|---|---|---|")
    for index, pr in enumerate(ordered, 1):
        out.append(f"| {index} | #{pr['number']} | {one_line(pr['title'], 60)} | {pr['baseRefName']} | "
                   f"{pr['headRefName']} | `{pr['headRefOid'][:12]}` | +{pr['ahead']}/-{pr['behind']} | "
                   f"{pr['mergeable']} | {pr['checkSummary']} | {pr['reviewDecision'] or '-'} | "
                   f"{', '.join(pr['flags']) or '-'} |")

    stacked = [pr for pr in ordered if pr["stackParent"]]
    if stacked or contains:
        out.append("\n## Stacks and ancestry\n")
        for pr in stacked:
            out.append(f"- #{pr['number']} is stacked on #{pr['stackParent']} (base `{pr['baseRefName']}`)")
        for line in contains:
            out.append(f"- {line}")

    out.append("\n## Review feedback to read before merging\n")
    for pr in ordered:
        fb = pr["feedback"]
        author = (pr.get("author") or {}).get("login")
        threads = [t for t in fb["reviewThreads"]["nodes"] if not t["isResolved"]]
        reviews = [r for r in fb["reviews"]["nodes"] if (r.get("body") or "").strip()]
        comments = [c for c in fb["comments"]["nodes"] if (c.get("author") or {}).get("login") != author]
        if pr["failedChecks"]:
            out.append(f"- #{pr['number']} failing checks: {', '.join(pr['failedChecks'])}")
        if not (threads or reviews or comments):
            out.append(f"- #{pr['number']}: no review feedback")
            continue
        out.append(f"- #{pr['number']}: {len(threads)} unresolved thread(s), {len(reviews)} review body(ies), "
                   f"{len(comments)} comment(s) from others")
        for t in threads:
            first = (t["comments"]["nodes"] or [{}])[0]
            who = (first.get("author") or {}).get("login", "?")
            outdated = " (outdated)" if t["isOutdated"] else ""
            out.append(f"  - thread `{t['path']}:{t['line']}`{outdated} {who}: {one_line(first.get('body', ''), 160)}")
        for r in reviews:
            out.append(f"  - review {r['state']} {(r.get('author') or {}).get('login', '?')}: {one_line(r['body'], 200)}")
        for c in comments[:10]:
            out.append(f"  - comment {(c.get('author') or {}).get('login', '?')}: {one_line(c['body'], 160)}")

    out.append("\n## Linked issues\n")
    for pr in ordered:
        issues = [f"#{i['number']}" for i in pr.get("closingIssuesReferences") or []]
        out.append(f"- #{pr['number']}: {', '.join(issues) or 'none linked (check the body)'}")
    if default_branch and opts.target != default_branch:
        out.append(f"\nClosing keywords only fire on `{default_branch}`; issues for `{opts.target}` need closing by hand.")

    out.append("\n## File overlap\n")
    pairs = []
    for a, b in itertools.combinations(ordered, 2):
        shared = sorted(set(a["changedFiles"]) & set(b["changedFiles"]))
        if shared:
            pairs.append((a, b, shared))
    if not pairs:
        out.append("No two PRs change the same file.")
    for a, b, shared in pairs:
        shown = ", ".join(f"`{f}`" for f in shared[:8]) + (" …" if len(shared) > 8 else "")
        out.append(f"- #{a['number']} × #{b['number']}: {len(shared)} file(s): {shown}")

    out.append("\n## Pinned arguments for simulate_merge.py and audit_combined.py\n")
    out.append("```")
    out.append(f"--target {target_sha} " + " ".join(f"{pr['number']}={pr['headRefOid']}" for pr in ordered))
    out.append("```")

    if blockers:
        out.append("\n## Stop and ask\n")
        out.extend(f"- {b}" for b in blockers)

    print("\n".join(out))
    if opts.json_out:
        with open(opts.json_out, "w", encoding="utf-8") as handle:
            json.dump({"target": opts.target, "targetSha": target_sha, "repo": f"{owner}/{name}",
                       "defaultBranch": default_branch, "order": [p["number"] for p in ordered],
                       "blockers": blockers, "prs": ordered}, handle, indent=2)
    return 2 if blockers else 0


if __name__ == "__main__":
    sys.exit(main())
