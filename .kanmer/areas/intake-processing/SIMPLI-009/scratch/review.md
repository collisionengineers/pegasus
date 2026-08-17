# Review — PR #385 (SIMPLI-009 + SIMPLI-008) — 2026-08-17

Reviewer: independent subagent (no session context) commissioned by claude-code; claude-code (author-of-record for the merge/rebase, not the implementation commit) drove the process. This is an independent plan-vs-diff review; the merge and disposition are claude-code's.

## Changes (reviewer's words)
Web `/Upload` now only stages bytes + a `Pending` work item via `ReceiveIntake` and redirects to `/Upload/Status/{id}`; `ProcessIntakeSubmission`, inline receive, `ReceiveForProcessingAsync`, request-local polling and the Web `ProcessQueuedIntake` registration are gone. `ProcessQueuedIntake` returns a bounded outcome with an explicit taxonomy (integrity/invalid → terminal; named transient set → bounded retry; other → terminal `unexpected_intake_processing_failure`, no rethrow). Blob adapter translates `RequestFailedException` → `IntakeDependencyUnavailableException`. New `IQueuedIntakeStatusQueries`/`EfQueuedIntakeStatusQueries` and `UploadStatus` page (auth, 404, four states, CSP-safe auto-refresh, case/receipt links). Web queue-sender role removed from bicep. FRD-02/design/current-architecture/operations updated. Tests re-cut around explicit stage → dispatch → Worker drain.

## Comments
**Blocking**
- B1 `DurableIntake.cs` `IsTransientProcessingFailure` — `DbException` does not match EF's `DbUpdateException` wrapper (SaveChanges deadlock/transient/connection faults) nor `EfIntakeReceiptStore.StoreAsync`'s `InvalidOperationException` after retry exhaustion → these fall to the unexpected catch and become terminal. Durability regression vs old `IsRecoverable`. Fix: unwrap `InnerException` chain; have `EfIntakeReceiptStore.StoreAsync` throw the named `IntakeOperationConflictException`; add a RecoveryTests case for a wrapped `DbException`.
- B2 `UploadStatus.cshtml` — nested `<main>` inside `_Layout`'s `<main id="main-content">`; classes `page-shell`, `page-header`, `stack`, `summary-list`, `button`, `button--primary` do not exist in `site.css`. Fix: use `_PageHeader`/`page-heading`, `panel`, `detail-list`, `button-row`, `primary-action`/`secondary-action`.

**Non-blocking, fix-in-PR**
- N1 unexpected-failure exception detail dropped (Core swallows; Worker logs id only) → carry exception type name into the outcome or `Activity.Current?.AddException`.
- N2 `IntakeSourceIdentityConflictException` from `receiptStore.StoreAsync` lands in the unexpected catch; `source_identity_conflict` failure code unreachable → explicit terminal catch.
- N3 `site.js` new block precedes `'use strict';` → move below the directive.
- N4 `OperatorLabels.IntakeFailure` lacks labels for `unexpected_intake_processing_failure`, `queue_poisoned`, `processing_lease_expired`, `invalid_intake_data`; Failed message lacks trailing full stop.
- N5 dead `TempData["UploadOutcomeMessage"]` readers in `Cases/Create.cshtml(.cs)`, `Intake/Details.cshtml`.
- N6 `docs/current-architecture.md` route paragraph / implementation map omit `/Upload/Status/{id}`, `UploadStatus.cshtml.cs`, `EfQueuedIntakeStatusQueries`.
- Report wording: redirect target is `/Upload/Status/{id}` (route), not `/UploadStatus/...` (page name).

**Non-blocking, ticket**
- T1 status projection reads only `CaseIntakeLinks`; auto-associated receipts (`IntakeManualAssociations` / `CurrentCaseId`) show "Open receipt" not "Open case".
- T2 auto-refresh unbounded when nothing is draining (DevelopmentOffline / stranded row).
- T3 no reconciliation for unleased `dispatched` rows (lost queue message; legacy inline-path residue). Plan's "no repair needed — disposable test data" is not proven: production releases 5–8 shipped the inline path (`operations.md:231-236`). Needs a read-only production check + stale-`dispatched` re-dispatch in `FindNextDispatchCandidateAsync`.

**Notes (no action)**: catch-chain/redelivery semantics sound (poison/redelivery only for parse errors, cancellation, `FailProcessingAsync` failure, post-completion advisory throws; leases recovered by `RecoverExpiredLeasesAsync`); blob adapter read path still 404→null; `CaseIntakeLinks` PK is receipt id so `SingleOrDefault` safe; TempData bool round-trips; MCP `"Queued"` literal is behaviour-neutral; bicep removal safe (no Web queue client); `/Cases/Create` deep-link loss is a plan choice consistent with FRD-02 and `/Received/{id}` "Create a case"; no ADR needed (ADR-0002 already fixes Worker-owned queue processing); no stale references to deleted symbols.

## Plan coverage
Steps 1,2,4,5,6,8,10,11 DONE; 3 PARTIAL (B1); 7 DONE functionally / off-system visually (B2); 9 DONE-mostly (impact-listed `AzureSqlRuntimeRoleMigrationTests`, `WorkerCompositionTests`, `ProcessIntakeTests`, `FailureInjectionTests` unchanged — judged acceptable; crash-after-stage only implicit).

## Ticket-vs-plan gaps
- SIMPLI-009 "Repair stranded dispatched work" — dropped by the plan on unproven reasoning (T3).
- SIMPLI-008 "recovery view" — Complete-without-case → `/Received/{id}` (has allocation retry + Create a case) covered; terminal Failed has no recovery action beyond "Upload another file" — undocumented.

## Report accuracy
29 files / +742 / −653 matches; every diff file listed and vice versa; deviations section candid; proof's "database faults retry" overstated (B1).

## Verdict
**NEEDS-CHANGES** (B1, B2). Disposition below is appended as work lands.
