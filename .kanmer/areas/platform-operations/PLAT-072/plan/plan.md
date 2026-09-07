# Plan — PLAT-072: remove obsolete completeness confirmation state

## Objective

Only real instruction/image completeness remains; there is no separate
instruction/image staff-review checkbox, request value or stored column.

## Starting state

Research base dev522e67f270ab4d6086d9fba04095988db3598888.
Evidence: research@730c6cb64d8766b6; files@21479727f857fc53.
EPIC014/current user supersedes old group build/merge restrictions. Research
found the prior configuration removal already complete; only residual state
and consumers need this change. No external sources declared. Root requires
Preparing until retained overlapping claims/ENG041 fixture are cleared;
no branch, packet, claim or application edit before that clearance.

## Governing docs

Meets FRD-01:67-73 and FRD-12:410-412, which already retire staff review and
require persisted-fact completeness. No behavior-doc rewrite or new ADR is
needed. Preserve protected operator-notes and actual handoff/Glass mechanics.

## Required changes

Reduce CaseCompleteness to InstructionComplete and ImagesComplete; remove
unused automaticallyDefinitive arguments and correct the null-guard/stale
waiver comments. Remove exactly two Create checkboxes, their bound properties,
Details handler arguments and hidden workflow inputs. Keep real completeness
controls, semantics, existing authorization/lease/reason/validation and actions.

Remove both flags from Cases and IntakeAllocationAttempts entities, mappings,
materialization, allocation reconstruction and new acceptance command material
(existing material SchemaVersion4 becomes5). Keep existing permanent history
and operation keys; do not add a legacy hash/replay adapter.

Use one normal EF migration to drop all four obsolete columns and update its
target model/current snapshot. Down recreates bool columns defaultfalse; lost
obsolete flag values cannot be recovered by Down. No Case, receipt, reference,
history, document, real completeness value or table is deleted. Existing
table-level grants remain sufficient: no new permission or bootstrap rewrite.

Update every observed current constructor/form/SQL fixture without changing
scenario assertions. Preserve historical-schema SQL fixtures, historical
migrations/designers, intentional absence assertions and standalone Audit
ConfirmedByStaffId. Reconcile exact applied/pending migration arrays with
the actual tail, including DOCS020's permission migration.

## Expected files

- `src/Pegasus.Core/Cases/CaseContracts.cs`
- `src/Pegasus.Core/Cases/CaseDataOperations.cs`
- `src/Pegasus.Core/Intake/AcceptIntake.cs`
- `src/Pegasus.Core/Intake/IntakeAllocation.cs`
- `src/Pegasus.Infrastructure/Persistence/EfCaseAcceptanceStore.cs`
- `src/Pegasus.Infrastructure/Persistence/EfCaseDataStore.cs`
- `src/Pegasus.Infrastructure/Persistence/EfCaseWorkspaceStore.cs`
- `src/Pegasus.Infrastructure/Persistence/EfIntakeAllocationStore.cs`
- `src/Pegasus.Infrastructure/Persistence/EfLinkedCaseReplacementStore.cs`
- `src/Pegasus.Infrastructure/Persistence/EfQueuedCustodyProcessor.cs`
- `src/Pegasus.Infrastructure/Persistence/IntakeAllocationEntities.cs`
- `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`
- `src/Pegasus.Web/Pages/Cases/Create.cshtml`
- `src/Pegasus.Web/Pages/Cases/Create.cshtml.cs`
- `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs`
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkflow.cshtml`
- `tests/Pegasus.Core.Tests/AiWork/AiWorkTests.cs`
- `tests/Pegasus.Core.Tests/Cases/AutomaticCaseReadinessTests.cs`
- `tests/Pegasus.Core.Tests/Cases/CaseDataOperationsTests.cs`
- `tests/Pegasus.Core.Tests/Cases/CaseWorkspaceTests.cs`
- `tests/Pegasus.Core.Tests/Cases/ImmediateExternalPublicationTests.cs`
- `tests/Pegasus.Core.Tests/Reports/CaseReportDeliveryPreparationTests.cs`
- `tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs`
- `tests/Pegasus.IntegrationTests/AssessmentVehiclePrefillWebTests.cs`
- `tests/Pegasus.IntegrationTests/AssessmentWorkspaceTestData.cs`
- `tests/Pegasus.IntegrationTests/AutomaticVehicleLookupTests.cs`
- `tests/Pegasus.IntegrationTests/AutomationDocumentIngressTests.cs`
- `tests/Pegasus.IntegrationTests/AutomationMcpTestSupport.cs`
- `tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs`
- `tests/Pegasus.IntegrationTests/Browser/OperatorJourneyTests.cs`
- `tests/Pegasus.IntegrationTests/CaseAcceptanceReplayTests.cs`
- `tests/Pegasus.IntegrationTests/CaseCreateWebTests.cs`
- `tests/Pegasus.IntegrationTests/CaseDataCompletenessPersistenceTests.cs`
- `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs`
- `tests/Pegasus.IntegrationTests/CaseMatchIntegrationTests.cs`
- `tests/Pegasus.IntegrationTests/CaseTaskArchivePersistenceTests.cs`
- `tests/Pegasus.IntegrationTests/CaseWorkflowMigrationTests.cs`
- `tests/Pegasus.IntegrationTests/CaseWorkflowPersistenceTests.cs`
- `tests/Pegasus.IntegrationTests/ConcurrencyTokenPersistenceTests.cs`
- `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs`
- `tests/Pegasus.IntegrationTests/DueChaserSweepPersistenceTests.cs`
- `tests/Pegasus.IntegrationTests/GlassRepairEstimateCallbackWebTests.cs`
- `tests/Pegasus.IntegrationTests/ImageCaseCustodyIntegrationTests.cs`
- `tests/Pegasus.IntegrationTests/ImageIntakePersistenceTests.cs`
- `tests/Pegasus.IntegrationTests/ImageIntakeWebTests.cs`
- `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs`
- `tests/Pegasus.IntegrationTests/OrganizationAdministrationPersistenceTests.cs`
- `tests/Pegasus.IntegrationTests/ProviderApiCaseDataSnapshotPersistenceTests.cs`
- `tests/Pegasus.IntegrationTests/ProviderApiSubmissionTests.cs`
- `tests/Pegasus.IntegrationTests/ProviderInspectionModeAcceptanceTests.cs`
- `tests/Pegasus.IntegrationTests/QdosAllocationRecoveryTests.cs`
- `tests/Pegasus.IntegrationTests/QdosTriageIntegrationTests.cs`
- `tests/Pegasus.IntegrationTests/RailCountsWebTests.cs`
- `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs`
- `tests/Pegasus.IntegrationTests/SendToAiIntegrationTests.cs`
- `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs`
- `tests/Pegasus.IntegrationTests/UnidentifiedReconciliationTests.cs`
- `tests/Pegasus.IntegrationTests/VehicleLookupBackfillTests.cs`
- `tests/Pegasus.IntegrationTests/VehicleLookupGapFillTests.cs`
- `tests/Pegasus.IntegrationTests/VehicleWorkflowTerminalTests.cs`
- `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs`
- `src/Pegasus.Infrastructure/Persistence/Migrations/20260907221500_RemoveCaseStaffConfirmation.cs`
- `src/Pegasus.Infrastructure/Persistence/Migrations/20260907221500_RemoveCaseStaffConfirmation.Designer.cs`
- `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`
- `docs/design/test-ui/pages/case-create--default.html`
- `docs/design/test-ui/pages/case-details--default.html`
- `docs/design/test-ui/pages/case-details--conflict.html`
- `docs/design/test-ui/pages/case-details--unavailable.html`
- `docs/design/test-ui/index.html`

## Do not modify

- docs/operator-notes.md
- src/Pegasus.Core/Reports/CaseReportDeliveryPreparation.cs
- src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs
- tests/Pegasus.IntegrationTests/RepairSpecificationMigrationTests.cs
- tests/Pegasus.IntegrationTests/TypedCaseDataMigrationTests.cs
- corpus/**

Existing historical migration/designer files are read-only. Only the new
named migration/designer and current model snapshot may change.

## Constraints

No package, framework, new storage owner, compatibility route or state-machine
rewrite. No live database/cloud/mail action. Root owns all build/test/capture.
Refresh origin/dev and changed-file ownership before the real packet/claim;
use one fresh .worktrees/plat-072 on PLAT-072-remove-staff-confirmation.
If actual migration tail overtakes the named new ID, amend plan/files before
scaffolding. Do not force another ticket's claim or mutate retained worktrees.

## Ordered steps

1. After root clears the open orchestration question, issue the real execution
   packet, validate/create the fresh worktree and take it. Remove Core/EF/Web
   dead fields and parameters, preserving two factual completeness values.
2. Add the four-column drop migration/target model/snapshot, then reconcile
   all known constructors and latest-schema SQL/form fixtures. Keep old-schema
   fixtures intact and update exact migration inventories.
3. Extend existing AutomaticCaseReadinessTests and CaseCreateWebTests for
   complete/missing facts without review inputs and absence of both controls;
   use existing CaseDataCompletenessPersistenceTests for column absence,
   remaining values/caller behavior and reversible schema shape. Hand root
   source-frozen code, affected filters and scoped capture.

## Acceptance checks

- Complete instructions and images satisfy the existing completeness rule;
  either missing stays not ready; handoff remains the implicit review.
- Create has only the two real completeness inputs. No obsolete production
  field/request argument/hidden input remains.
- Latest EF model/SQL schema lacks the four columns; actual accept/read/update
  and allocation replay still execute; exact operation replay allocates once.
- Migration retains runtime table grants and all other persisted data.
  Historical names are permitted only in historical schema evidence and
  intentional absence checks; standalone Audit evidence stays distinct.
- Fixtures preserve their original scenario assertions; no broad test framework.
- Root captures case-create and case-details only; no manual visual or
  runtime PASS is inferred from static source.

## Commands

Worker: git diff --check, static symbol/constructor/current-SQL scans only.
Root: one locked restore/Release build then focused existing Core filters
AutomaticCaseReadinessTests|CaseDataOperationsTests and Integration
CaseCreateWebTests|CaseDataCompletenessPersistenceTests|CaseAcceptanceReplayTests|
IntakePersistenceIntegrationTests|CaseWorkflowMigrationTests.
The final handed-off filter must use exact FullyQualifiedName predicates.
Include representative updated latest-SQL fixture cohorts (RailCountsWebTests,
ImageIntakeWebTests, RetainedMailPersistenceTests) and overlap-owner checks only
when root's verification gap calls for them; avoid a duplicate whole suite.
Root runs Test-MigrationGrants; capture scope case-create,case-details using
existing CaseCreateWebTests/CaseDetailsWebTests plus unavailable owner only
when needed. Final integrated CI stays root's one coordinated release gate.

## Failure and deviation rules

Retain every failure. Unknown callers, changed schemas, permission gaps or
overlap beyond this bounded removal require plan/files correction before edit.
No silent test weakening, historical-fixture deletion or feature expansion.

## Stop condition

For this preparation: stop in Preparing with open orchestration hold; no app
edits. After explicit root clearance and execution, stop with source-frozen
diff and focused checks handed to root. No builds/tests/captures, PR, merge,
deployment, Done or cleanup without the next authorized phase.
