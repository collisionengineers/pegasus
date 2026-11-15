#!/usr/bin/env python3
"""Watch a merge request's pipeline jobs and stop at the first failure.

Usage:
    python watch_checks.py 1137                 # poll until first failure or all done
    python watch_checks.py 1137 --once          # print the current table and exit
    python watch_checks.py 1137 --interval 45 --timeout 90

Why this exists: the merge request page reports the pipeline as a whole, so an
early failure in one lane goes unreported while the SQL shards run on. This
polls the jobs of the merge request's head pipeline and of every child pipeline
it triggers, prints the table, and exits the moment any job fails, with the
command that reads its log while the pipeline is still live:

    glab api projects/:id/jobs/<job-id>/trace

Exit codes: 0 = every job finished and passed; 1 = a job failed (printed
first); 3 = the merge request conflicts with its target branch; 4 = timed out
while jobs were still pending; 2 = error.

A merge request with no head pipeline yet is polled until one appears or the
timeout passes. No `jq` is needed.
"""
from __future__ import annotations

import argparse
import json
import subprocess
import sys
import time
from urllib.parse import quote

FAILED = {"failed", "canceled", "canceling"}


class GlabError(Exception):
    pass


def run(args: list[str]) -> tuple[int, str, str]:
    result = subprocess.run(args, capture_output=True, text=True, encoding="utf-8", errors="replace")
    return result.returncode, result.stdout, result.stderr


def glab_json(path: str, paginate: bool = False):
    code, out, err = run(["glab", "api", path, *(["--paginate"] if paginate else [])])
    if code != 0:
        raise GlabError(err.strip() or f"glab api {path} failed")
    return json.loads(out)


def project_path(repo: str | None) -> str:
    """The project's API path: --repo URL-encoded, else glab's placeholder for the current repository."""
    return "projects/" + (quote(repo, safe="") if repo else ":id")


def bucket(job: dict) -> str:
    status = job.get("status") or ""
    if status == "success":
        return "pass"
    if status in FAILED:
        return "fail"
    # A manual job that may fail does not hold the pipeline; one that may not, waits for someone.
    if status == "skipped" or (status == "manual" and job.get("allow_failure")):
        return "skipping"
    return "pending"


def pipeline_jobs(project: str, pipeline_id: int) -> list[dict]:
    """The pipeline's jobs, with each trigger job replaced by the jobs of the child pipeline it started."""
    jobs = glab_json(f"{project}/pipelines/{pipeline_id}/jobs?per_page=100", paginate=True)
    for bridge in glab_json(f"{project}/pipelines/{pipeline_id}/bridges?per_page=100", paginate=True):
        child = bridge.get("downstream_pipeline")
        jobs.extend(pipeline_jobs(project, child["id"]) if child else [bridge])
    return jobs


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("pr", type=int, metavar="MR", help="merge request number (!N)")
    parser.add_argument("--repo", help="group/name; defaults to the current repository")
    parser.add_argument("--interval", type=int, default=30, help="seconds between polls")
    parser.add_argument("--timeout", type=int, default=120, help="minutes before giving up")
    parser.add_argument("--once", action="store_true", help="print the current state and exit")
    opts = parser.parse_args()
    project = project_path(opts.repo)
    mr_path = f"{project}/merge_requests/{opts.pr}"

    try:
        mr = glab_json(mr_path)
    except GlabError as error:
        print(error, file=sys.stderr)
        return 2
    print(f"Merge request !{opts.pr} {mr['web_url']} head {mr['sha'][:12]} "
          f"has_conflicts={str(bool(mr.get('has_conflicts'))).lower()} merge_status={mr.get('detailed_merge_status')}")
    if mr.get("has_conflicts") or mr.get("detailed_merge_status") == "conflict":
        print("The merge request conflicts with its target branch and cannot merge, whatever its pipeline says. "
              "Resolve the conflict first.")
        return 3

    deadline = time.time() + opts.timeout * 60
    first_poll = True
    while True:
        try:
            if not first_poll:
                mr = glab_json(mr_path)  # a push replaces the head pipeline
            first_poll = False
            pipeline = mr.get("head_pipeline")
            checks = [dict(job, bucket=bucket(job)) for job in pipeline_jobs(project, pipeline["id"])] if pipeline else []
        except GlabError as error:
            print(error, file=sys.stderr)
            return 2

        failed = [c for c in checks if c["bucket"] == "fail"]
        pending = [c for c in checks if c["bucket"] == "pending"]
        passed = [c for c in checks if c["bucket"] == "pass"]
        skipped = [c for c in checks if c["bucket"] == "skipping"]

        stamp = time.strftime("%H:%M:%S")
        print(f"[{stamp}] {len(passed)} passed, {len(pending)} pending, {len(failed)} failed, {len(skipped)} skipped, {len(checks)} total")
        if opts.once:
            for c in checks:
                print(f"  {c['bucket']:9} {c.get('status') or '?':12} {c['name']}  {c.get('web_url') or ''}")

        if failed:
            first = sorted(failed, key=lambda c: c.get("finished_at") or "")[0]
            print(f"\nFIRST FAILURE: {first['name']} ({first.get('status')})")
            print(f"  {first.get('web_url') or ''}")
            print(f"  log: glab api {project}/jobs/{first['id']}/trace")
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
            print("  no pipeline jobs yet (the merge request has no head pipeline, or it has not created its jobs)")
        if time.time() > deadline:
            print(f"Timed out after {opts.timeout} minutes with {len(pending)} check(s) pending.")
            return 4
        time.sleep(opts.interval)


if __name__ == "__main__":
    sys.exit(main())
