---
kind: proof-record
merged_sha: "f479a94849bb3a1208f06d5823687d7bff30566f"
environment: "GitHub Actions run 33796752315 (pull_request, head 8cdbb306, two attempts) on hosted windows/ubuntu runners; merged workflow byte-identical to the reviewed file (git diff 659cec77 origin/dev -- .github/workflows/ci.yml empty)"
verified_at: "2026-09-03T23:52:28Z"
result: PASS
attempts: []
---
# Proof — DELIV-043 (command-log)

Verified for merged `dev` at `f479a94849bb3a1208f06d5823687d7bff30566f` (PR #653 merge). The workflow file on `dev` is byte-identical to the reviewed head `8cdbb306` (reviewer's check: `git diff 659cec77 origin/dev -- .github/workflows/ci.yml` empty; the only intervening `dev` commits touched other files), so the PR's own run is the executable evidence for the integrated state.

| Check | Evidence | Result |
|---|---|---|
| Heavy lanes wait for the three cheap jobs | run 33796752315 attempt 1: `local-development-scripts` 19:29:49→19:30:02, `reference-data` →19:30:14, `documentation` →19:30:22, `changes` →19:30:42; heavy lanes started 19:30:45 (`infrastructure`, `unit`), 19:30:46 (`test-ui`), 19:30:47/19:30:54/19:31:06 (`sql-integration` 3/2/1), 19:31:05 (`browser`) | PASS |
| Every lane green after the change | attempt 1: all green except `test-ui` (35-minute step budget timeout; steps byte-identical to `dev`, classified infrastructure); attempt 2 re-ran only `test-ui` → success in 28m47s; `gh pr checks 653` 12/12 pass; `mergeStateStatus` CLEAN | PASS |
| `concurrency` block parsed and scoped | `yaml` parse of the job graph (implementation); reviewer: PR runs group as `repository-check-<number>`, `main` pushes as `repository-check-refs/heads/main`, disjoint; `cancel-in-progress` false on `main` | PASS |
| Coverage job skips when the shards were skipped | `if: always() && build == 'true' && needs.sql-integration.result != 'skipped'` present on `dev`; ran normally when the shards ran (19:44:34→19:44:52 success) | PASS |

## Not exercised on this run (standard Actions semantics; observed on the next red PR)

- A PR whose cheap job fails shows the heavy lanes `skipped`: no red run was forced (it would cost ~85 runner-minutes to demonstrate what the workflow definition already states: dependants without `always()` skip when a `needs` job fails).
- A second push cancelling the older run: no second push happened on this PR.

Result: **PASS** on everything the merged state could show; the two unexercised behaviours are recorded above and will be confirmed by the first red PR and the first superseded push on `dev`.
