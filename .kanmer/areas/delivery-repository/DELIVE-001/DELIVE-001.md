---
id: DELIVE-001
type: ticket
title: >-
  Harden flaky CI tests (SQL deadlock, QDOS soak, cancellation race,
  pwsh-subprocess)
status: done
area: delivery-repository
order: 450
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-17T04:23:51.013Z'
  review: '2026-08-17T04:39:48.673Z'
  verifying: '2026-08-17T04:50:15.562Z'
  done: '2026-08-18T12:22:51.461Z'
labels: []
groups:
  - EPIC-001
links:
  - SIMPLI-006
docs_todo: true
commits:
  - 4b1cfed8be9530e367225a3deac4a651ae0da534
  - 14ce3843
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/378'
deployment: n/a
archived: false
created: '2026-08-14T11:15:00.236Z'
updated: '2026-08-26T14:34:43.339Z'
---

## What

Four CI jobs fail intermittently, unrelated to the change under test. Seen on PR #374 (docs+comments only; the `documentation` job passes). `dev` passes all four and each has been seen green on re-run — flaky, not regressions.

## The four (evidence: runs 31754103856 / 31754103867)

1. **`unit` → `WorkerActivationReleaseContractTests.LocalDeploymentPlanRejectsAppendedRogueHardCodedWorkerSetting`** — shells out to `pwsh Test-AzureDeploymentPlan.ps1`; the script threw an exception (a runner path) instead of the expected rejection (`Assert.Contains` sub-string not found). Fixtures/scripts are byte-identical to `dev` and the script reads no doc, so the result is independent of branch content — a pwsh-subprocess/temp-dir/runner flake. Capture stdout+stderr on failure; retry the spawn.
2. **`sql-integration (1)` → `QdosAllocationRecoveryTests.DistinctParallelRetriesResolveToOneCaseAggregate`** — `SqlException: was deadlocked ... chosen as the deadlock victim`. The message itself suggests `EnableRetryOnFailure` on the test `UseSqlServer` call, or a deterministic lock order for parallel-retry aggregates.
3. **`qdos-pressure` → `CapacitySoakTests.EightConcurrentStaffCompleteBoundedCallerPressureWithoutLostReceipts`** — 8-concurrent soak fails under CI load (`Invoke-QdosAlphaAcceptance.ps1:563`). Tune concurrency/timeout for the runner envelope, or move to a nightly/soak lane off the per-PR gate.
4. **`source-workspaces` → `CollisionDocNet.Email.Tests.Extract_CancellationDuringLargeBase64Scan_ReturnsCancelledWithoutDecodedEvidence`** — `Expected Cancelled, Actual ResourceLimitExceeded`: a race between cancellation and resource-limit detection. Make cancellation win deterministically, or accept either outcome.

## Why

A slow, flaky per-PR gate blocks unrelated (e.g. docs) PRs and trains reviewers to ignore red. None are product regressions.

## Verification

- [x] Each test either passes deterministically 20x locally under load, or is moved off the per-PR required set with rationale.

## Outcome

Shipped via PR #378 (merged 2026-08-17T04:50:07Z, `14ce3843`; on `main` since #394): Worker validator aligned to the 1/1 replica contract, diagnostics on the pwsh subprocess test, deadlock-1205 retry in the parallel allocation test, cancellation made deterministic in the extractor, and the pressure soak moved to a nightly lane. That nightly lane was itself retired on 2026-08-18 ([[DELIV-007]]). Architecture suite 96/96 and all three SQL shards green on the release-9 SHA. Worktree cleanup owed on workstation `PC`; the remote branch was deleted. Closed out 2026-08-18.
