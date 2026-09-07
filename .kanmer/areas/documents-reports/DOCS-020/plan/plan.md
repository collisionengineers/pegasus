# Plan — DOCS-020: consistent report snapshots

## Objective
A report generation freezes one guarded set of relevant inputs and becomes
undeliverable when those inputs change, without invalidating for notes/recipients.

## Starting state
Base: origin/dev 2e50fde474ce35eb32eff8677eb2327cb6aad272.
Research: research@69548b17434a1524; files index corrected to the existing
CaseArtifactCustodyRecoveryTests name before this plan. No external declared
sources. Root agreed the FRD11 non-material-change exception and exact generated
artifact identity; root is the sole heavy verifier. INTK061 confirmed disjoint
custody ownership. Existing missing report UPDATE permissions are in scope.

## Governing docs
Meets docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md immutable
source-labelled snapshots and relevant-input invalidation. Clarify consistent
read checks, source/output distinction and profile invalidation in that FRD.
No new ADR: reuse existing report store, Core guards/resolver, custody
transactions, Identity administration and LondonCalendar.

## Required changes
Read initial workflow before all snapshot components, compare workspace version
and recheck captured version under freeze transaction. Revalidate the resolved
current signatory tuple inside that transaction. Preserve external reads outside
locks. Wire source add/remove/confirmation and signatory mutations into existing
same-context stale helper. Exclude only exact report outputs identified through
GeneratedCaseArtifacts.OperationKey from report source collection/invalidation.
Retain preparation/current-generation and staff-send guards; never silently
send stale output. Use London civil date for freeze/preview and report time
presentation. Grant only the real Web/Worker table operations this path needs.

## Expected files
- `src/Pegasus.Infrastructure/Persistence/EfAssessmentReportProjectionSource.cs`
- `src/Pegasus.Infrastructure/Persistence/EfCaseReportGenerationStore.cs`
- `src/Pegasus.Infrastructure/Persistence/EfDocumentCustodyStore.cs`
- `src/Pegasus.Infrastructure/Persistence/EfQueuedCustodyProcessor.cs`
- `src/Pegasus.Infrastructure/Custody/EfCaseArtifactCustody.cs`
- `src/Pegasus.Infrastructure/Persistence/EfStaffAccountAdministration.cs`
- `src/Pegasus.Core/Reports/AssessmentReportProjection.cs`
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseReport.cshtml`
- `src/Pegasus.Infrastructure/Persistence/Migrations/20260907210000_ReportInputInvalidationPermissions.cs`
- `scripts/Invoke-AzureDatabaseBootstrap.ps1`
- `tests/Pegasus.IntegrationTests/Reports/CaseReportGenerationPersistenceTests.cs`
- `tests/Pegasus.IntegrationTests/Reports/CaseReportDeliveryPreparationPersistenceTests.cs`
- `tests/Pegasus.IntegrationTests/Reports/AssessmentReportDraftWebTests.cs`
- `tests/Pegasus.IntegrationTests/DocumentCustodyDurabilityTests.cs`
- `tests/Pegasus.IntegrationTests/StaffAccountAdministrationPersistenceTests.cs`
- `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs`
- `tests/Pegasus.IntegrationTests/CaseArtifactCustodyRecoveryTests.cs`
- `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`

## Do not modify
- `src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs`
- `src/Pegasus.Core/Cases/CaseContracts.cs`
- `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs`
- `corpus/**`

## Constraints
No new package/schema column/runtime unit/framework or policy owner. Do not
hold Box/Chromium/Graph work in snapshot database transactions. Preserve issued
history, operation-key replay, Case leases and meaningful failures. Grant-only
migration is additive, SQL Server guarded and model-neutral. No cloud/email
writes. Root owns test execution and UI capture; implementation stops before PR.

## Ordered steps
1. Add snapshot/version and tuple guards plus London dates, extending focused
   existing report tests to expose concurrent-source and BST-midnight failures.
2. Wire atomic source/signatory invalidation through real callers, retaining
   precise generated-output exclusion, no-op/replay and unrelated-change behavior;
   add persistence tests for add/remove/profile changes and stale preparation/send.
3. Add minimal report permission migration and bootstrap census, extend actual
   role caller verification; document the coherent report-input contract.
4. Perform read-only diff/whitespace inspection, record focused test filters and
   code handoff. Root runs tests/capture before any PR/Review transition.

## Acceptance checks
- Mutation between snapshot reads/freeze refuses a mixed or stale snapshot.
- Source add/remove/confirmation and signatory tuple/eligibility changes stale
  affected current generations atomically; existing preparation/send guard refuses.
- Generated report outputs do not invalidate themselves or become input sources.
- Superseded snapshots remain immutable. Notes/recipient edits do not require
  regenerating a report; prepared addressing changes still require re-preparation.
- London midnight in BST sets the next civil day for preview and generation.
- Actual runtime-role update/read caller executes with narrow grants.
- Focused new assertions remain unexecuted until root verification; no false PASS.

## Commands
Worker: git diff --check; git diff --stat; inspect changed caller/test source.
Root: focused existing report generation/delivery persistence, document custody,
staff administration, custody recovery and runtime-role filters, plus report
preview/date coverage. Root captures the affected case-details UI snapshot.
Do not launch dotnet, CI or deployment from this worker.

## Failure and deviation rules
A discovered additional mutation path must be recorded in plan/files before
editing. Preserve all failures. Do not add broad grants or replace shared
infrastructure to overcome a test/setup failure.

## Stop condition
Leave the taken DOCS-020 worktree with reviewable code/tests and a precise
validation handoff to root before PR/Review. Do not self-review, merge, deploy
or start another ticket.

## Exact-merge verification correction — 7 September 2026

Add `tests/Pegasus.IntegrationTests/CaseWorkflowMigrationTests.cs` to this scope: the exact pending-migration list must include `20260907210000_ReportInputInvalidationPermissions`. Preserve the historical migration target and every identity/table assertion. No application/schema change. Reuse the recorded DOCS-020 branch/worktree, merge current dev without rewriting author history, apply the two-line expectation correction and submit a new follow-up PR because PR682 is already merged. Root runs only the previously failing named migration test after incremental compilation. Independent review of the new exact head precedes merge; final exact-merge proof preserves attempt1 FAIL and its 52 unaffected passes.
