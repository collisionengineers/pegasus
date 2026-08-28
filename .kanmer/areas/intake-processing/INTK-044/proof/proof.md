---
kind: proof-record
merged_sha: "935d58ff2e5f505620672e211bf420a9df71b295"
environment: "Detached verification worktree ../pegasus-worktrees/verify-intk-044 at 935d58ff (HEAD detached, clean); Windows 11, PowerShell 7, .NET 10 SDK, SQL Server LocalDB; prod check via az AAD token against pegasus-prod-sql-252ow37gij/pegasus (read-only)"
verified_at: "2026-08-27T17:15:00Z"
result: PASS
attempts:
  - attempted_at: "2026-08-27T16:57:00Z"
    command: "gh pr view 572 --json state,mergeCommit,url"
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus"
    exit_code: 0
    result: PASS
    summary: "state=MERGED, mergeCommit.oid=935d58ff2e5f505620672e211bf420a9df71b295, url=https://github.com/collisionengineers/pegasus/pull/572"
  - attempted_at: "2026-08-27T16:57:30Z"
    command: "git fetch origin; git worktree add --detach ../pegasus-worktrees/verify-intk-044 935d58ff2e5f505620672e211bf420a9df71b295; rev-parse HEAD; symbolic-ref --short -q HEAD; status --short --branch"
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus"
    exit_code: 0
    result: PASS
    summary: "HEAD=935d58ff2e5f505620672e211bf420a9df71b295; symbolic-ref empty (detached); status '## HEAD (no branch)' with no changes"
  - attempted_at: "2026-08-27T16:58:21Z"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: "../pegasus-worktrees/verify-intk-044"
    exit_code: 0
    result: PASS
    summary: "All projects restored under locked mode"
  - attempted_at: "2026-08-27T16:58:36Z"
    command: "dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    cwd: "../pegasus-worktrees/verify-intk-044"
    exit_code: 0
    result: PASS
    summary: "0 Warning(s), 0 Error(s)"
  - attempted_at: "2026-08-27T17:01:57Z"
    command: "dotnet test tests/Pegasus.Core.Tests --configuration Release --no-build"
    cwd: "../pegasus-worktrees/verify-intk-044"
    exit_code: 0
    result: PASS
    summary: "Passed! Failed: 0, Passed: 1002, Skipped: 0, Total: 1002 (includes the new classification/retry tests)"
  - attempted_at: "2026-08-27T17:02:08Z"
    command: "dotnet test tests/Pegasus.IntegrationTests --configuration Release --no-build --filter \"FullyQualifiedName~ConcurrentAutomaticAuditAndInspectionAllocationsForOnePrincipalBothSucceed\""
    cwd: "../pegasus-worktrees/verify-intk-044"
    exit_code: 0
    result: PASS
    summary: "Passed! Failed: 0, Passed: 1, Total: 1, Duration 2 m 15 s — concurrent deadlock reproduction; single attempt, no LocalDB timeout"
  - attempted_at: "2026-08-27T17:04:33Z"
    command: "dotnet test tests/Pegasus.IntegrationTests --configuration Release --no-build --filter \"FullyQualifiedName~UnexpectedAutomaticAuditFailureIsRetriedFromTheReceipt\""
    cwd: "../pegasus-worktrees/verify-intk-044"
    exit_code: 0
    result: PASS
    summary: "Passed! Failed: 0, Passed: 1, Total: 1, Duration 1 m 53 s — browser staff-retry route to an a. case; single attempt"
  - attempted_at: "2026-08-27T17:03:00Z"
    command: "Read-only prod SQL (AAD token via az account get-access-token --resource https://database.windows.net): SELECT Reference, CaseType, CreatedAtUtc FROM Cases WHERE Reference IN ('a.QDOS26025','QDOS26024'); SELECT Id, FailureKind, RecoveryDisposition, StartedAtUtc FROM IntakeAllocationAttempts WHERE IntakeReceiptId='f2ac0509-5de5-4555-93a2-399f4fea7587'"
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus"
    exit_code: 1
    result: FAIL
    summary: "Query error 'Invalid column name CaseType' — verifier's guessed column name, not an application fault; connection and AAD auth succeeded. Retried with valid columns below."
  - attempted_at: "2026-08-27T17:09:00Z"
    command: "Read-only prod SQL (AAD token): SELECT Reference, CreatedAtUtc FROM Cases WHERE Reference IN ('a.QDOS26025','QDOS26024'); SELECT Id, FailureKind, RecoveryDisposition FROM IntakeAllocationAttempts WHERE IntakeReceiptId='f2ac0509-5de5-4555-93a2-399f4fea7587'"
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus"
    exit_code: 0
    result: PASS
    summary: "QDOS26024 CreatedAtUtc 2026-08-27 10:25:27Z; a.QDOS26025 CreatedAtUtc 2026-08-27 10:51:16Z (EREF10 Audit case exists). Attempt f598ac23-3af5-4247-8ef3-41495b5f1630 on receipt f2ac0509 still FailureKind=unexpected, RecoveryDisposition=blocked (historical, unchanged by design). No writes."
---

# Proof — INTK-044 (PR #572, merge 935d58ff)

## Scope of this verification

Code-level verification at the exact merge SHA in a disposable detached
worktree. The full integration suite was not run here (another suite occupied
the machine); the ticket's own three new tests were run once each and passed
on the first attempt, and CI at the PR head was green (see `scratch/review`).

## Outcome against the ticket

1. Root cause fixed: the wrapped `SqlException 1205` deadlock now enters the
   acceptance retry loop (`EfCaseAcceptanceStore.IsRetryableConcurrencyFailure`
   unwraps every layer) and `EfIntakeAllocationStore.BeginAsync` runs
   read-committed. Proven by the concurrent reproduction test (PASS, 6 rounds).
2. Staff recovery route: an unclassified automatic failure is
   `Unexpected`/`ReloadThenRetry`, so the Intake Details retry form renders and
   re-runs the immutable command. Proven by Core 1002/1002 and the browser test
   `UnexpectedAutomaticAuditFailureIsRetriedFromTheReceipt` (PASS).
3. EREF10 exists as an Audit case: `a.QDOS26025` confirmed in prod `Cases`.

## Deployment note

The fix is merged to `dev` only; it is **not deployed** to production. Receipt
`f2ac0509…` deliberately keeps its historical `blocked` attempt `f598ac23…`
(EREF10 already has `a.QDOS26025`; retrying would create a duplicate Audit).
Live proof of the retry route and the deadlock fix in production belongs to a
later release ticket (the next `dev` → `main` promotion), not to this record.

## Worktree cleanup

Disposable worktree `../pegasus-worktrees/verify-intk-044` removed after this
record was written; the implementation worktree and branch were not touched.

## Traceability (closeout)

PR: https://github.com/collisionengineers/pegasus/pull/572 — merged into
`dev` 2026-08-27T16:56:14Z at `935d58ff2e5f505620672e211bf420a9df71b295`.
