# Post-implementation report — INTK-044

Branch `task/intk-044-audit-allocation-recovery`, worktree
`C:/Users/Alex/Documents/GitHub/pegasus-worktrees/intk-044-audit-allocation-recovery`,
commits `2112057d` (fix + tests) and `f1bb150a` (simplification pass), PR #572 → `dev`.

## Outcome against the ticket

1. **Root cause found and fixed.** Reproduced by the new integration test
   `ConcurrentAutomaticAuditAndInspectionAllocationsForOnePrincipalBothSucceed`
   (two automatic allocations for one principal via `Task.WhenAll`, one a
   standalone Audit), which failed on round 0 with the live shape
   (`FailedRecoverable/Unexpected`) and captured the exception the Worker lost:
   `InvalidOperationException("…likely due to a transient failure…") →
   DbUpdateException → SqlException 1205` from
   `EfCaseAcceptanceStore.AcceptOnceAsync` `SaveChangesAsync`. The
   `Serializable` acceptances deadlocked on the `CaseSequences` row; the store's
   `IsRetryableConcurrencyFailure` unwrapped only `DbUpdateException`, so EF's
   outer wrapper escaped the retry loop and `Classify` fell to `Unexpected`.
   Fix: unwrap every layer (the `EfIntakeReceiptStore` convention). The same
   test then exposed a second deadlock in `EfIntakeAllocationStore.BeginAsync`
   (`Serializable` range locks across different receipts' check-then-insert);
   `BeginAsync` now runs read-committed — the per-receipt applock and the
   unique indexes on `OperationKey` / `(IntakeReceiptId, AttemptNumber)` are the
   guard. Six rounds of the reproduction pass.
2. **Staff recovery route.** `AllocateIntake.Classify` maps an unclassified
   fault to `Unexpected`/`ReloadThenRetry`; the existing Intake Details retry
   form renders once `CanRetry` is true and `RetryAsync` re-runs the immutable
   command (same receipt version, same `StandaloneAuditEvidenceId`).
   `SequenceExhausted` stays `Blocked`; no UI or copy change. Browser test
   `UnexpectedAutomaticAuditFailureIsRetriedFromTheReceipt` proves
   `/Received/{id}` → retry → `a.` case linked to the same evidence.
3. **EREF10 exists as an Audit case** already: `a.QDOS26025` (receipt
   `acf537e3`, 10:51Z re-forward). The original receipt `f2ac0509` keeps its
   persisted `blocked` attempt `f598ac23` and is orphaned; retrying it would
   create a second Audit for one instruction, so it is left as-is and nothing
   was deleted.

## FRD reading

FRD-02 "Pre-Case outcomes": a failed outcome "offers reasoned resolve and retry
actions"; "No background or automatic business retry is permitted"; Audit "is
created only by this retained-email route". A reasoned staff retry of the
recorded automatic command is inside that; a manual Audit create is not. No
operator-only question arose.

## Commands and exit codes

| Command | Exit |
| --- | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 (0 warnings) |
| `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"` (`artifacts/intk-044/test-full.log`) | 1 — Core 1002/1002, Architecture 100/100, Integration 988/989 |
| Re-run `--filter FullyQualifiedName~CraftedOrOversizedCorrectionsFailClosedWithoutHistoryWrites` (`artifacts/intk-044/test-rerun-mailworkspace.log`) | 0 (PASS) |

The single integration failure was `MailWorkspaceWebTests.CraftedOrOversizedCorrectionsFailClosedWithoutHistoryWrites`:
`SqlException: Connection Timeout Expired. The timeout period elapsed during the post-login phase … [Post-Login] complete=14001` — LocalDB contention while another lane's suite ran; untouched by this diff; passed on the one permitted re-run.

## Deviations from the plan

- `EfIntakeAllocationStore.BeginAsync` isolation change was not in the plan; it
  was found by the reproduction and is on the same concurrent path (recorded in
  `plan` and `files`).

## Follow-up (not in scope)

- Six other stores (`EfCaseReportSentEvidenceStore`, `EfIntakeSubmissionGroupStore`,
  `EfIntakeWorkStore`, `EfLinkedCaseReplacementStore`, `EfVehicleWorkflowStore`,
  `EfOrganizationAdministration`) still unwrap only `DbUpdateException` and can
  swallow the same wrapped deadlock; one shared predicate is the right shape.
- Staff retry of an automatic command is evaluated as staff completeness
  (CASE-013 waiver applies only to the system-worker actor), so a retried case
  may open `Not ready` until confirmed — pre-existing behaviour of every staff
  retry.
