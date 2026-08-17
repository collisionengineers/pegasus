# Proof — SIMPLI-009 (verified on merged `dev`)

Supersedes the pre-merge proof written on 2026-08-13 against `195154f9`; this is the verification of what actually landed.

## What landed

PR #385, merged into `dev` as **`fc144848`** on 2026-08-17 (merge commit; the merged tree is byte-identical to the CI-tested PR head `8bf0a3e6` — `git diff 8bf0a3e6 fc144848` is empty). Commits `195154f9` (implementation), `e9f27fe7` (merge of `origin/dev`), `caad05e8` (temp-plan removal), `8bf0a3e6` (review blockers + simplification pass). Net diff vs the previous `dev` (`e6422250`): 31 files, +873/−817. Independent review: NEEDS-CHANGES → fixed → re-verified **PASS** (`scratch-review`).

## Verification on `fc144848` (ticket worktree, detached at the merge commit; 2026-08-17 12:41–12:54 BST)

| Command | Result |
| --- | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | restored |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | Build succeeded — 0 warnings, 0 errors (1m15s) |
| `dotnet test tests/Pegasus.Core.Tests --configuration Release --no-build` | **572 passed**, 0 failed |
| `dotnet test tests/Pegasus.ArchitectureTests --configuration Release --no-build` | **94 passed**, 0 failed |
| `dotnet test tests/Pegasus.IntegrationTests --configuration Release --no-build` (full) | **530 passed, 16 skipped (corpus/profile-gated), 0 failed** — 546 total, 11m30s |
| CI on PR head `8bf0a3e6` (same tree) | 10/10 checks pass: unit, browser, sql-integration (1)(2)(3), sql-integration-coverage, documentation (incl. markdown placement), infrastructure, reference-data, changes |

Logs: `verify-fc144848.log` and `verify-fc144848-integration-full.log` in the session scratchpad.

## Behaviour proven by the suite (mapping to the ticket's Verification line)

- **Duplicate delivery** — `RecoveryTests.DurableIntakeReplayAndExpiredDispatchLeaseRecoverIdempotently`, `QdosAllocationRecoveryTests` replay cases: one receipt, one evaluation, one case.
- **Crash after stage / Web stages only** — `QdosIntakeWebTests.ReadableManualUploadStagesPendingWorkAndOpensItsStatusPage`: Web leaves a `Pending` work item, no evaluation, and **cannot resolve `ProcessQueuedIntake`** from its DI container; work is later drained by the Worker path.
- **Lease expiry** — `RecoveryTests.QueuedStatusProjectsAnActiveProcessingLease` and the expired-dispatch-lease recovery test.
- **Poison / retry exhaustion** — `RecoveryTests.TransientProcessingFailureExhaustsTheBoundedRetrySchedule` (5 attempts → Failed); poison replay tests unchanged and green.
- **Fault taxonomy** — `RecoveryTests.TransientProcessingFailureSchedulesARetry` (`io`, `dependency`, `wrapped-database` → `RetryScheduled`, code `intake_processing_failure`); `RecoveryTests.UnexpectedProcessingFailureIsPersistedThenRethrown` (row Failed with `unexpected_intake_processing_failure` → exception reaches the host → redelivery `NoOp` → status page shows Failed without leaking the code).
- **Web/Worker permission boundary** — `AzureSqlRuntimeRoleMigrationTests` (unchanged, green: Web role lacks `IntakeReceipts:INSERT`); bicep removes Web's queue-sender role (source only).

## Read-only production check (2026-08-17, SELECT only, Entra admin identity)

`IntakeWorkItems`: 10 rows — completed 9, failed 1; unleased `dispatched` = **0**. The ticket's "repair stranded dispatched work" line therefore has nothing to repair; the lost-message resilience gap is filed as [[INTK-003]].

## Not claimed

No deployment, no live Worker execution, no cloud write. `docs/operations.md` and `docs/current-architecture.md` describe source/as-built shape only.

## Follow-ups filed

[[INTK-001]] (retry-scheduled honesty + auto-associated case link), [[INTK-002]] (adapter-wide fault naming, Web-composition architecture fact, `IIntakeSubmission` leftover), [[INTK-003]] (stale `dispatched` recovery), [[DELIV-001]] (simplicity rails in AGENTS.md).
