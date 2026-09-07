# Post-implementation report — DOCS-020

## Summary
Implementation candidate on DOCS-020-report-consistency, based on dev
2e50fde474ce35eb32eff8677eb2327cb6aad272, in .worktrees/docs-020.
Not committed, reviewed, tested or merged. The parent-directed stop is the
focused-verifier handoff before PR/Review.

The report source now captures the Case version before component reads,
checks workspace and final versions, and the freeze transaction requires the
same version plus the same effective signatory tuple. Real source and signatory
mutations invalidate current generations atomically. Exact generated-report
operation identity excludes report outputs, not other Generated source inputs.
Dates and displayed report times reuse LondonCalendar.

## Changes
| File | Change / rationale |
| --- | --- |
| src/Pegasus.Infrastructure/Persistence/EfAssessmentReportProjectionSource.cs | Start/end Case version checks and exact generated-output exclusion |
| src/Pegasus.Infrastructure/Persistence/EfCaseReportGenerationStore.cs | Guard freeze input version and signatory tuple; reuse existing stale helper for source/profile changes; London date |
| src/Pegasus.Infrastructure/Persistence/EfDocumentCustodyStore.cs | Add/remove source invalidation in the mutation transaction; shared PrepareAddAsync also serves market-research attachment completion |
| src/Pegasus.Infrastructure/Persistence/EfQueuedCustodyProcessor.cs | Retained intake-file registration invalidates current reports |
| src/Pegasus.Infrastructure/Custody/EfCaseArtifactCustody.cs | Immediate and recovered custody confirmation commit source invalidation and Case advancement together, outside external writes |
| src/Pegasus.Infrastructure/Persistence/EfStaffAccountAdministration.cs | Profile, enabled-state, deletion and role changes compare actual effective signatories and stale only changed current tuples |
| src/Pegasus.Core/Reports/AssessmentReportProjection.cs | Preview defaults to London's civil date |
| src/Pegasus.Web/Pages/Cases/Shared/_CaseReport.cshtml | Display generation time in London |
| src/Pegasus.Infrastructure/Persistence/Migrations/20260907210000_ReportInputInvalidationPermissions.cs | Grant-only migration: Web UPDATE report generations/artifacts; Worker SELECT/UPDATE generations and SELECT artifacts; existing DELETE denies unchanged |
| scripts/Invoke-AzureDatabaseBootstrap.ps1 | Matching narrow permission census |
| tests/Pegasus.IntegrationTests/Reports/CaseReportGenerationPersistenceTests.cs | Real source read race, stale cached freeze inputs/signatory, source add/remove and prepared-delivery refusal, seven signatory variants, exact output exclusion, Web freeze/confirm and both custody runtime-role callers, BST date |
| tests/Pegasus.IntegrationTests/Reports/AssessmentReportDraftWebTests.cs | Preview BST-midnight regression with existing fake renderer |
| tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs | Latest migration permission expectations |
| tests/Pegasus.IntegrationTests/CaseArtifactCustodyRecoveryTests.cs | Seed the real Case/workflow relationship and assert Pending does not advance while confirmation does |
| docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md | Clarify consistent freeze, precise output/input distinction, invalidation and London dates |

## Governing docs
Implements the linked FRD-11 immutable snapshot and relevant-input rules.
Existing Core CaseEditAuthority, CaseMutationGuard adapter, signatory resolver,
CaseReportStaleReasons and delivery readiness remain their owners.
No new architecture, package, schema column, model snapshot or runtime unit.
No blanket generation.CaseVersion/current-Case-version delivery comparison was
added: notes/recipient edits do not require report regeneration; an existing
preparation still checks its current Case/addressing. The existing staff send
engine invokes ReportSendReadiness again immediately before submitting.

## Author inspection and unresolved validation
Normal repository-configured git diff --check returned exit 0, including the
final source state. One later diagnostic incorrectly supplied
-c core.autocrlf=false; it returned exit 1 by treating the Windows CRLF checkout
as whole-file changes/trailing CR characters. No files or Git configuration were
changed by that override. The ordinary command was rerun and returned exit 0.
No compiler, test, UI capture, CI, cloud or email command ran in this worker.

The tests are implementation candidates, not PASS evidence. Root owns the sole
heavy verifier. No weakened assertions or replacement test framework were added.
The report harness now seeds an actual eligible Identity profile because the
freeze correctly re-reads it, and the custody harness now seeds its workflow.

## Verification hand-off
First focused integration filter:
FullyQualifiedName~CaseReportGenerationPersistenceTests|FullyQualifiedName~AssessmentReportDraftWebTests|FullyQualifiedName~AzureSqlRuntimeRoleMigrationTests.LatestMigrationGivesFoundationTablesTheirExactRuntimePermissions|FullyQualifiedName~CaseArtifactCustodyRecoveryTests

Complementary existing coverage when sharing the same verification run:
FullyQualifiedName~CaseReportDeliveryPreparationPersistenceTests|FullyQualifiedName~DocumentCustodyDurabilityTests|FullyQualifiedName~StaffAccountAdministrationPersistenceTests|FullyQualifiedName~AssessmentPersistenceIntegrationTests.ReportDraftGenerationThroughProductionProjectionResolvesSignOffAndFailsClosedWithoutIt

Use the established Release per-project commands/filter, not a second build
lane. Capture the affected case-details report UI snapshots and verify the UI
catalogue before delivery. The grant migration must execute in the normal
bootstrap path; the actual-role tests exercise real report freeze/confirmation
and source-confirmation persistence under runtime impersonation, not catalogue
checks alone.

Root should run focused validation before author commit/PR; independent
kanmer-review then reviews the exact head. Post-merge proof belongs on configured
integration branch dev, not the stale main wording of the report template.

## Risks / follow-ups
All changed behavior and tests remain unexecuted until the root verifier reports
their exit codes. No deployed correctness claim is made. Keep this worktree and
lease as the exact resume target. Root owns any necessary UI capture artifacts.
INTK-061 and PLAT-028 remain disjoint; neither EfDocumentRequestStore nor
Core/Cases/CaseContracts was edited. No out-of-scope implementation was started.
