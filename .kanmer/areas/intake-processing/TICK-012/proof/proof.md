# Proof — TICK-012 (INT-25): Automatic case creation from definitive authorised intake

Local caller-proof (activation tier 2–4) evidence. The live/deployed
Worker-caller tier (tier 5) is NOT covered here — see "Not covered".

## What was verified

The INT-25 mechanism is implemented, wired through the real durable caller, and
green at the local caller-proof tier. Contract as exercised:

- **Core owner / entry**: `AllocateIntake.AttemptAutomaticAsync`
  (`src/Pegasus.Core/Intake/IntakeAllocation.cs:213`), system actor
  `system-worker:intake-processing`.
- **Caller chain**: timer `PendingWorkDispatchFunction` → `intake-work` queue →
  `ProcessQueuedIntake.ExecuteAsync` (`DurableIntake.cs:589`) → allocation call
  (`DurableIntake.cs:742`) → `AcceptIntake` → `EfCaseAcceptanceStore.AcceptAsync`.
- **Reference minting**: `QDOSyyNNN` (`EfCaseAcceptanceStore.cs:252`) + `a.`/`ap.`
  via `AuditIdentity.Create` (`CaseContracts.cs:93-108`).
- **Behaviours confirmed by named passing tests**:
  - Automatic allocation uses the persisted typed case type —
    `AllocateDefinitiveIntakeTests.AutomaticAllocationUsesPersistedTypedCaseType`.
  - A failed automatic attempt is durable and NOT retried in the background —
    `...FailedAutomaticAttemptIsDurableAndIsNotRetriedInBackground`.
  - The automation actor cannot invoke staff retry —
    `...AutomationActorCannotInvokeStaffRetry`.
  - Definitive-Audit triple condition + negation exclusion —
    `QdosMailClassificationPolicyTests` (instruction + separate original report +
    exactly one literal `repairable`/`total loss`; negated/subword excluded).
  - Unique existing-case match bypasses new allocation exactly once —
    `QdosAllocationRecoveryTests.UniqueExistingCaseAssociationBypassesNewAllocationExactlyOnce`.
  - Processing decision is separated from a failed allocation —
    `IntakeAllocationConsumerTests.ReceivedProjectionSeparatesProcessingDecisionFromFailedAllocation`.
  - Reasoned staff retry of the frozen command allocates exactly once —
    `QdosAllocationRecoveryTests.MissingPrincipalFailurePersistsAndReasonedStaffRetryAllocatesExactlyOnce`.

## Evidence

Environment: .NET SDK 10.0.303, Windows, SQL Server Express LocalDB
(`MSSQLLocalDB`). Worktree `pegasus-worktrees/int-25-doc-01-planning` on branch
`task/int-25-doc-01-planning` created from `origin/dev` (no source changes).

Release build (whole solution):
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:51.47
```

Focused Core intake suites (`AllocateDefinitiveIntakeTests`,
`DefinitiveIntakeCaseTypeTests`, `Qdos/QdosMailClassificationPolicyTests`,
`CaseMatching/EvaluateIntakeCaseMatchTests`):
```
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build \
  --filter "FullyQualifiedName~AllocateDefinitiveIntakeTests|FullyQualifiedName~DefinitiveIntakeCaseTypeTests|FullyQualifiedName~QdosMailClassificationPolicyTests|FullyQualifiedName~EvaluateIntakeCaseMatchTests"

Passed!  - Failed:     0, Passed:    64, Skipped:     0, Total:    64, Duration: 125 ms - Pegasus.Core.Tests.dll (net10.0)
```

Focused integration recovery/replay suites (real LocalDB;
`QdosAllocationRecoveryTests`, `IntakeAllocationConsumerTests`,
`CaseAcceptanceReplayTests`):
```
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build \
  --filter "(Category!=Corpus&Category!=Browser)&(FullyQualifiedName~QdosAllocationRecoveryTests|FullyQualifiedName~IntakeAllocationConsumerTests|FullyQualifiedName~CaseAcceptanceReplayTests)"

Passed!  - Failed:     0, Passed:    22, Skipped:     0, Total:    22, Duration: 1 m 26 s - Pegasus.IntegrationTests.dll (net10.0)
```

Local caller-proof total for INT-25: **86 passed, 0 failed** (64 Core + 22
integration), plus a 0-error Release build.

## Not covered

- **Live/deployed Worker-caller journey (tier 5)** — no enabled Worker caller
  has created a case against the deployed estate here. This is
  `requires-live-approval` and is consistent with the retired pre-Kanmer tracker (historical evidence)'s own warning ("No
  browser journey has exercised … an enabled Worker caller against the deployed
  estate"). Deferred pending explicit operator approval; the ticket therefore
  holds at `review`, not `done`, until that activation decision is made.
- **QDOS-only breadth** — only `Qdos*` provider policies are registered
  (`DependencyInjection.cs:123-130`); non-QDOS principals need a human key. Not a
  mechanism defect; candidate follow-up ticket if the operator wants it tracked.
- **OCR-literal Audit bound** — automatic Audit outcome depends on literal
  `repairable`/`total loss` text; scanned/OCR-needed reports fail closed to
  `NeedsSorting` (correct behaviour, bounds auto-create rate). Candidate
  follow-up.

---

## LIVE PRODUCTION PROOF — 2026-08-14 (tier 5: enabled Worker caller against the deployed estate)

A forwarded QDOS **Audit** email was auto-processed by the deployed Worker end-to-end and created a case + real Box folder — the first live automatic case creation on the production estate.

**Evidence (read-only prod SQL + Box CLI):**
- Case minted automatically by the Worker: `QDOS26001` · Type `audit` · AuditReference `a.QDOS26001` · `IntakeAllocationAttempts` succeeded.
- Real Box custody folder created via the Box API under the production root `405543781910`:
  `box folders:items 405543781910` → `{ id: 409001353539, name: "a.QDOS26001", type: folder }`.
- `Cases`: `CustodyState=confirmed`, `CustodyRootRemoteId=409001353539`, `AuditCustodyRemoteId=409001353539`, `CustodyConfirmedAtUtc=2026-08-14T08:53:41Z`.

**What it took (root cause):** deploying dev fix #2 (`73a3380d`) to prod was necessary but NOT sufficient. The true blocker was that the Worker's least-privilege SQL role `pegasus_worker_runtime_role` was never granted the case-creation permissions (it predated auto-allocation moving to the Worker; the WorkerGrants matrix in `20260729199000_RuntimeRoleReconciliation.cs` is stale). The acceptance transaction (`EfCaseAcceptanceStore.AcceptOnceAsync`) INSERTs ~20 tables in one batch; a full worker grant reconciliation (incl. the EF cascade child `CaseDataFields`) was applied as a prod hotfix. Local tests never caught this because LocalDB runs full-privilege.

**Still owed:** codify the worker grants as a migration (they are currently manual drift); clear the stuck pre-fix backlog via staff Retry allocation; the DOC-01 UI link + dead-code removal (TICK-017); docs refresh.

---

## Verification on merged `main` `f1e116c6` — 2026-08-18 (release 9)

- The manual Worker grant hotfix from 2026-08-14 is now codified: migration `20260814092852_AddWorkerCaseCreationGrants` was applied to production by release 9 (`efbundle.exe`, `__EFMigrationsHistory` readback) and `Invoke-AzureDatabaseBootstrap.ps1` verified the full matrix ("Verified 459 catalogued permission/denial rows and 306 effective runtime DML rows"). The "still owed" grant migration is closed.
- The Worker package at `f1e116c6` is deployed and polling (`ApprovedInboxPollStates.LastCompletedAtUtc` advancing; all nine functions enabled; smoke passed).
- Remaining follow-ups are tracked outside this ticket: DOC-01 UI link / dead-code removal ([[TICK-017]]); clearing the stuck pre-fix backlog via staff Retry allocation (operational task); docs refresh landed with release 9 (PR #404).

Work landed via PR #376 (2026-08-17) and shipped to `main` in #394 and release 9.
