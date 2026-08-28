# Plan — INTK-044

## Premises (verified read-only unless marked assumed)

- Prod SQL 2026-08-27: attempt `614a21bd` (EREF8, `inspection_and_audit`) started/completed 10:25:26 but its case `QDOS26024` has `CreatedAtUtc` 10:25:27 — the acceptance transaction was still open at 10:25:27, when attempt `f598ac23` (EREF10, `audit`, `StandaloneAuditEvidenceId = 3a45b161`, `ExpectedReceiptVersion = 1`) began; it failed at 10:25:29 `unexpected`/`blocked`. Both automatic, same principal `QDOS`, same sequence lineage. Concurrency is the only distinguishing condition. Evidence row valid (`ResultingReceiptVersion = 1`, hash length 64, automatic actor).
- The failure exception is unrecoverable: the Worker is an isolated Functions host (`docs/current-architecture.md`), so `ContainerAppConsoleLogs` does not carry it, and App Insights was capped (MAIL-020). The original `.eml` is not in the local corpus (corpus EREF10 is a different claimant).
- `EfCaseAcceptanceStore.AcceptAsync` retries only `DbUpdateConcurrencyException` and SQL 1205/2601/2627, three times, then throws `IntakeVersionConflictException` → `ConcurrencyConflict`/`ReloadThenRetry`. So a deadlock alone cannot produce `unexpected`; the lost exception was outside that list (any other `SqlException`, e.g. an Azure SQL transient — the `DbContextFactory` has no `EnableRetryOnFailure` — or a non-SQL fault). `IntakeExceptionPolicy.IsTransientFailure` already treats any `DbException` as transient elsewhere in the pipeline.
- `AllocateIntake.Classify` maps every unrecognised exception to `Unexpected`/`Blocked`; `IntakeAllocationState.CanRetry` excludes it; `Cases/Create` refuses Audit. That is the dead end.
- FRD-02 (`docs/frd/frd-02-intake-and-source-identity.md` "Pre-Case outcomes"): a failed pre-Case outcome "offers reasoned resolve and retry actions, and retains … each retry result"; "No background or automatic business retry is permitted"; "The manual case-create screen does not offer Audit; it is created only by this retained-email route." A reasoned staff retry that re-runs the immutable automatic command (same receipt version, same retained evidence id) is therefore inside policy; a manual Audit create is not. No operator-only question arises.
- `RetryAsync` re-executes `current.Command` verbatim (including `StandaloneAuditEvidenceId`), and `EfIntakeAllocationStore.BeginAsync` already admits a StaffRetry over `reload_then_retry`. The Intake Details page already renders the retry form when `CanRetry` is true — no UI change and no new copy (`docs/design/README.md#no-explanatory-copy-and-page-economy`).
- Assumed: the staff retry evaluates completeness as staff (not `automaticallyDefinitive`), so the retried case may open `Not ready` until confirmed — existing behaviour of every staff retry (CASE-013), unchanged here.

## Steps

1. **Reproduce concurrently** (integration, `QdosAllocationRecoveryTests`): seed one principal; per round store an `inspection_and_audit` receipt and an `audit` receipt with seeded automatic evidence; run both `AttemptAutomaticAsync` via `Task.WhenAll` through the real `IAcceptIntake` and a real `EfIntakeAllocationStore` wired to `CapturingLogger` (reuses the shape at `ConcurrencyAndUnexpectedFailuresUseExactTaxonomyAndOneStructuredLogEach`); assert both `Succeeded` and print any captured 4721 exception. If it surfaces a retryable SQL number, add it to `IsRetryableConcurrencyFailure`; otherwise record "not reproduced locally" honestly.
2. **Recovery route** (Core): `Classify` default arm → `Unexpected`/`ReloadThenRetry`, same safe reason. `SequenceExhausted` stays `Blocked`; `CaseTypeUnavailable` stays `ManualReview`. No other layer changes: projection (`FromAttempt`), store admission, Details page and MCP status all key off the disposition already.
3. **Tests**: Core — extend `SequenceExhaustionIsBlockedAndUnexpectedFailureIsSafe` and add a test that an unexpected automatic Audit failure is retryable and the retry hands acceptance the identical command (evidence id preserved). Integration — flip the `Blocked` expectation at `ConcurrencyAndUnexpectedFailuresUseExactTaxonomyAndOneStructuredLogEach`; add `AllocationTestData.SeedAutomaticAuditEvidenceAsync` and reuse it from `CustodyOutboxIntegrationTests` (one helper); browser — persist an unexpected automatic-Audit failure through `ThrowingAcceptIntake`, then retry from `/Received/{id}` and land on an `a.` case.
4. Verify: `dotnet restore ./Pegasus.slnx --locked-mode`; `dotnet build ./Pegasus.slnx --configuration Release --no-restore`; `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"` logged to `artifacts/intk-044/test-full.log`.
5. Commit, push, PR to `dev`, post-implementation report, move to review.

## Out of scope

- The prod row `f598ac23` keeps its persisted `blocked` disposition: EREF10 already exists as `a.QDOS26025` (receipt `acf537e3`), so retrying the original receipt `f2ac0509` would create a second Audit for one instruction. Nothing is deleted.
- Automatic (background) retry of transient SQL faults inside acceptance — FRD-02 forbids automatic business retry; the staff route is the recovery.

## Simplification pass

Pending — recorded after implementation.

## Execution deviations (2026-08-27)

- The concurrent reproduction succeeded on round 0 and captured the lost exception: `InvalidOperationException("…likely due to a transient failure…") → DbUpdateException → SqlException 1205` from `EfCaseAcceptanceStore.AcceptOnceAsync` `SaveChangesAsync`. Root cause: `IsRetryableConcurrencyFailure` unwrapped only `DbUpdateException`, not EF's outer wrapper, so the deadlock escaped the retry loop. Fixed by unwrapping every layer (the `EfIntakeReceiptStore` convention).
- With that fixed, the same test exposed a second deadlock in `EfIntakeAllocationStore.BeginAsync` (`Serializable` range locks across two different receipts' check-then-insert). `BeginAsync` now runs read-committed: the per-receipt applock serialises same-receipt Begins and the unique indexes on `OperationKey` and `(IntakeReceiptId, AttemptNumber)` enforce the invariants. `files` updated.

## Simplification pass — 2026-08-27

Independent code-simplifier read of the branch diff (report-only), dispositions:

1. Concurrent test built the failure message eagerly every round and replayed earlier rounds' exceptions — **applied** (message built only on failure).
2. `results[0]/[1]` positional meaning — **applied** (named `inspectionAllocation`/`auditAllocation`).
3. Move `ThrowingAcceptIntake` beside `AllocationTestData` — **rejected**: it is already in the same file, directly after its primary caller.
4. Six-line comment in `EfCaseAcceptanceStore` — **applied** (two lines).
5. Ten-line comment in `EfIntakeAllocationStore.BeginAsync` — **applied** (shortened; history stays in git).
6. Six other stores (`EfCaseReportSentEvidenceStore`, `EfIntakeSubmissionGroupStore`, `EfIntakeWorkStore`, `EfLinkedCaseReplacementStore`, `EfVehicleWorkflowStore`, `EfOrganizationAdministration`) still unwrap only `DbUpdateException` and can swallow the same wrapped deadlock; one shared predicate is the right shape — **deferred to a follow-up ticket** (scope beyond INTK-044; noted in the report).
7. Core change and new test fakes at the right layer, no existing fake to reuse — no finding.
8. `rounds = 6` is the race budget — kept.
