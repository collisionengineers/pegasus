#!/usr/bin/env python3
"""Watch a pull request's checks and stop at the first failure.

Usage:
    python watch_checks.py 1137                 # poll until first failure or all done
    python watch_checks.py 1137 --once          # print the current table and exit
    python watch_checks.py 1137 --interval 45 --timeout 90

Why this exists: `gh pr checks --watch` waits for every job, so an early
failure in one lane goes unreported while five SQL shards run on. This polls,
prints the table, and exits the moment any check fails, with the job id so the
log can be read while the run is still live:

    gh api repos/{owner}/{repo}/actions/jobs/<job-id>/logs

Exit codes: 0 = every check finished and passed; 1 = a check failed (printed
first); 3 = the PR is not mergeable (CONFLICTING), so no CI will run; 4 =
timed out while checks were still pending; 2 = error.

`gh pr checks` exits non-zero while checks are pending or failing, so the
output is captured before the exit code is read. No `jq` is needed.
"""
from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
import time

FAIL_STATES = {"FAILURE", "TIMED_OUT", "CANCELLED", "ACTION_REQUIRED", "STARTUP_FAILURE", "ERROR"}


def run(args: list[str]) -> tuple[int, str, str]:
    result = subprocess.run(args, capture_output=True, text=True, encoding="utf-8", errors="replace")
    return result.returncode, result.stdout, result.stderr


def job_id(link: str) -> str:
    match = re.search(r"/job/(\d+)", link or "")
    return match.group(1) if match else ""


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("pr", type=int)
    parser.add_argument("--repo", help="owner/name; defaults to the current repository")
    parser.add_argument("--interval", type=int, default=30, help="seconds between polls")
    parser.add_argument("--timeout", type=int, default=120, help="minutes before giving up")
    parser.add_argument("--once", action="store_true", help="print the current state and exit")
    opts = parser.parse_args()
    repo_args = ["--repo", opts.repo] if opts.repo else []

    code, out, err = run(["gh", "pr", "view", str(opts.pr), *repo_args, "--json", "mergeable,mergeStateStatus,headRefOid,url"])
    if code != 0:
        print(err.strip(), file=sys.stderr)
        return 2
    view = json.loads(out)
    print(f"PR #{opts.pr} {view['url']} head {view['headRefOid'][:12]} mergeable={view['mergeable']} state={view['mergeStateStatus']}")
    if view["mergeable"] == "CONFLICTING":
        print("The PR conflicts with its base, so GitHub builds no merge ref and no CI will run. Resolve the conflict first.")
        return 3

    deadline = time.time() + opts.timeout * 60
    while True:
        code, out, err = run(["gh", "pr", "checks", str(opts.pr), *repo_args, "--json", "name,state,bucket,link,startedAt,completedAt"])
        if not out.strip():
            if "no checks reported" in (err or "").lower():
                checks = []
            else:
                print(err.strip() or "gh pr checks printed nothing", file=sys.stderr)
                return 2
        else:
            checks = json.loads(out)

        failed = [c for c in checks if c.get("bucket") == "fail" or c.get("state") in FAIL_STATES]
        pending = [c for c in checks if c.get("bucket") == "pending" or c.get("state") in {"PENDING", "QUEUED", "IN_PROGRESS", "WAITING"}]
        passed = [c for c in checks if c.get("bucket") == "pass"]
        skipped = [c for c in checks if c.get("bucket") == "skipping"]

        stamp = time.strftime("%H:%M:%S")
        print(f"[{stamp}] {len(passed)} passed, {len(pending)} pending, {len(failed)} failed, {len(skipped)} skipped, {len(checks)} total")
        if opts.once:
            for c in checks:
                print(f"  {c.get('bucket', '?'):9} {c.get('state', '?'):12} {c['name']}  {c.get('link', '')}")

        if failed:
            first = sorted(failed, key=lambda c: c.get("completedAt") or "")[0]
            jid = job_id(first.get("link", ""))
            print(f"\nFIRST FAILURE: {first['name']} ({first.get('state')})")
            print(f"  {first.get('link', '')}")
            if jid:
                print(f"  log: gh api repos/{{owner}}/{{repo}}/actions/jobs/{jid}/logs")
            for c in failed:
                if c is not first:
                    print(f"  also failed: {c['name']}")
            return 1
        if not pending and checks:
            print("All checks finished and passed.")
            return 0
        if opts.once:
            return 0
        if not checks:
            print("  no checks reported yet (a workflow may not have started; confirm the PR is mergeable)")
        if time.time() > deadline:
            print(f"Timed out after {opts.timeout} minutes with {len(pending)} check(s) pending.")
            return 4
        time.sleep(opts.interval)


if __name__ == "__main__":
    sys.exit(main())
