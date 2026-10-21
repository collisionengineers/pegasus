# Watching CI on the PR

Each item here cost an earlier run time or credibility.

## Before arming anything

- Confirm the PR is mergeable: `gh pr view N --json mergeable,mergeStateStatus`.
  A `CONFLICTING` PR has no merge ref, so no `pull_request` workflow starts and
  the PR shows **no checks at all**, which looks exactly like a slow queue.
- Run `scripts/watch_checks.py N --once` by hand and read real output before
  running it in the background. A poller that dies on a missing tool looks the
  same as one that is still waiting.
- Never promise "I'll report when CI finishes" via `gh pr checks --watch`; it
  waits for every job while an early failure sits unreported.

## On the first failure

- Read the failing job's log while the run is still live:
  `gh api repos/{owner}/{repo}/actions/jobs/<job-id>/logs` (the run-level
  `--log-failed` is refused until the run ends). If the repo has a status
  script (Pegasus: `scripts/Get-CiStatus.ps1`), it prints the failing test
  names from that log.
- Decide whose failure it is before fixing: a test the change did not touch,
  that also fails on the integration branch or on a sibling PR, is not this
  PR's defect. Report it with the evidence rather than "fixing" it.
- Fix on the task branch, push, re-run the watcher. Do not rebase or
  force-push a branch that has a PR; add a commit.

## A job that dies inside checkout

A `changes`/checkout job that hangs and is cancelled at almost exactly 5 (or
15) minutes inside `actions/checkout`'s fetch of `refs/pull/N/merge` is a stale
merge ref, usually because branches were deleted while GitHub computed the
merge commit. Re-running can never work; the run is pinned to the dead ref.

```bash
gh pr close N && gh pr reopen N        # forces a fresh merge ref and a new run
```

If the old run stays `queued` and holds the per-PR concurrency group,
`gh api -X POST repos/{owner}/{repo}/actions/runs/<id>/force-cancel` frees it.
Watch the new run by its id (`gh run view <id> --json jobs`), not the PR
rollup, which mixes both runs. Do not push an empty commit to retrigger; it
moves the head away from what was tested.

## Reading green

- A green run on this PR's exact head is the evidence. Record the head SHA
  with it.
- A deferred or skipped job (stack-aware CI) is not evidence. Read which jobs
  actually ran.
- Flaky-looking failures: check whether the same test failed on a recent
  integration-branch run (`gh run list --branch <base> --workflow ci.yml`).
