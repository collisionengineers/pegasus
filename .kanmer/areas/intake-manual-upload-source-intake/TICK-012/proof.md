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
  `requires-live-approval` and is consistent with NOW.md's own warning ("No
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
