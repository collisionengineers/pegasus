---
id: DELIVE-001
type: ticket
title: >-
  Harden flaky CI tests (SQL deadlock, QDOS soak, cancellation race,
  pwsh-subprocess)
status: backlog
area: delivery-build-ci-documentation-governance
assignee: ''
profile: feature
labels: []
groups:
  - EPIC-001
links:
  - SIMPLI-006
docs_todo: true
archived: false
created: '2026-08-14T11:15:00.236Z'
updated: '2026-08-17T04:04:47.169Z'
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

- [ ] Each test either passes deterministically 20x locally under load, or is moved off the per-PR required set with rationale.
