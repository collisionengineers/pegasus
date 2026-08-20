---
id: CASE-005
type: ticket
title: Resolve the SQL deadlock in parallel Qdos case allocation retries
status: verifying
area: case-reference-workflow
assignee: claude-code
profile: fix
stageEntered:
  implementing: '2026-08-20T16:45:06.386Z'
  review: '2026-08-20T17:40:51.357Z'
  verifying: '2026-08-20T18:22:38.218Z'
taken_at: '2026-08-20T16:45:00.646Z'
branch: task/case-005-allocation-deadlock
worktree: ../pegasus-worktrees/case-005
labels:
  - defect
  - concurrency
  - allocation
  - flaky-ci
links:
  - DELIV-012
refs:
  - docs/frd/frd-01-case-identity-and-lifecycle.md
archived: false
created: '2026-08-19T14:05:43.815Z'
updated: '2026-08-20T18:22:38.218Z'
---

## What

`QdosAllocationRecoveryTests.DistinctParallelRetriesResolveToOneCaseAggregate` fails intermittently with a genuine SQL Server deadlock:

```
System.InvalidOperationException : An exception has been raised that is likely due to a transient failure.
Consider enabling transient error resiliency by adding 'EnableRetryOnFailure' to the 'UseSqlServer' call.
---- Microsoft.EntityFrameworkCore.DbUpdateException : An error occurred while saving the entity changes.
-------- Microsoft.Data.SqlClient.SqlException : Transaction (Process ID 64) was deadlocked on lock
         resources with another process and has been chosen as the deadlock victim. Rerun the transaction.
```

## Why this matters

This is not only a flaky test. The test exists because two parallel retries of the same allocation **must** resolve to one case aggregate — that is the fail-closed identity guarantee in the product invariants. A deadlock means one of the two transactions is killed by SQL Server and the exception propagates uncaught, so in production a concurrent retry can fail outright rather than converging. Case/PO allocation is the one place the repository treats ambiguity as unacceptable.

## Evidence that it is pre-existing and not caused by release-12 work

Observed on two independent runs, on different branches, with the same test and the same deadlock:

| Run | Head | Branch context |
| --- | --- | --- |
| `32247164106` | `4f67a83e` | `dev` (PR #419), **before** any DELIV-012 branch existed |
| `32259768976` | `2d410159` | PR #425 (repair-specification store wiring) |

A third sighting is recorded against PR #423 (INTK-008), where the same test returned `Pending` instead of `Succeeded` — a different symptom of the same contention. PRs #416 and #422 passed the same shard, so it is intermittent rather than deterministic. [[DELIV-012]] confirmed the Qdos allocation tests do not reference `ICaseAssessmentStore`, so the repair-specification change is not implicated.

## Approach

- Reproduce deterministically first — run the test repeatedly under load, and capture the deadlock graph from SQL Server (`system_health` extended events) so the two conflicting lock paths are named rather than guessed.
- Decide the fix from the graph, not from the exception text. `EnableRetryOnFailure` is what the exception *suggests*, but a retry policy on top of an explicit serializable transaction can mask a lock-ordering defect instead of fixing it; the allocation path already has a recovery design worth re-reading first.
- Whatever the fix, the test must assert convergence under contention, not merely stop throwing.

## Verification

- [ ] The deadlock graph is captured and the two conflicting statements are named.
- [ ] The test passes repeatedly (say 20 consecutive runs) under parallel load.
- [ ] A deadlock, if it can still occur, resolves to one case aggregate rather than surfacing to the caller.

## Outcome
