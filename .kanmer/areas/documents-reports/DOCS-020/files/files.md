# Files — DOCS-020

## Where the change lands
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
Existing report/custody/admin stores are the mutation boundary; tests extend
their existing harnesses. The permission-only migration does not alter the EF
model. The FRD records consistent freeze and invalidation without blanket Case
version rejection. London presentation is one existing report partial, with
focused Test UI capture delegated to root before delivery.

## Context files
| Path | What it establishes |
| --- | --- |
| src/Pegasus.Core/Reports/CaseReportGeneration.cs | Existing immutable snapshot, readiness and stale-reason owner |
| src/Pegasus.Core/Reports/CaseReportDeliveryPreparation.cs | Current-generation and prepared-address/version send guards |
| src/Pegasus.Core/Lifecycle/CaseLifecycle.cs | Signatory fallback and eligibility resolver |
| src/Pegasus.Infrastructure/Persistence/CaseMutationGuard.cs | Existing guarded Case version mutation |
| src/Pegasus.Core/LondonCalendar.cs | Europe/London civil date and time conversion |
| src/Pegasus.Infrastructure/Persistence/EfStaffAccountQueries.cs | Reuse eligible account tuple query in freeze transaction |
| src/Pegasus.Infrastructure/Persistence/Migrations/20260906054658_V1PlatformFoundation.cs | Existing insufficient report permissions |
| src/Pegasus.Infrastructure/Persistence/EfStaffMailSendStore.cs | Transport context and retained-attachment checks |

## Ripple effects
No new package/model/composition unit. Report artifacts must not invalidate their
own source snapshot. User profile changes invalidate only affected current
generations; superseded issued history is immutable. Root owns all compiler,
runtime-role and UI snapshot validation.

## Out of scope
Intake routing/handover, principal identity changes, CaseContracts, new release
mechanisms, unrelated report feature scope, live mail/cloud writes, test
infrastructure and blanket generation-to-current-Case-version refusal.

## Exact-merge verification correction — 7 September 2026

Add `tests/Pegasus.IntegrationTests/CaseWorkflowMigrationTests.cs` to this scope: the exact pending-migration list must include `20260907210000_ReportInputInvalidationPermissions`. Preserve the historical migration target and every identity/table assertion. No application/schema change. Reuse the recorded DOCS-020 branch/worktree, merge current dev without rewriting author history, apply the two-line expectation correction and submit a new follow-up PR because PR682 is already merged. Root runs only the previously failing named migration test after incremental compilation. Independent review of the new exact head precedes merge; final exact-merge proof preserves attempt1 FAIL and its 52 unaffected passes.
